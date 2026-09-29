using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Service.DocumentNumberControl.Commands.CreateNumberingScheme;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Service.DocumentNumberControl.Commands.SetSchemeActive;
using Finnova.Service.DocumentNumberControl.Commands.UpdateNumberingScheme;
using Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemeById;
using Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemesPaged;
using Finnova.Service.DocumentNumberControl.Queries.GetSchemeAuditTrail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Document Number Control (DCN) Master endpoints (FINNOVA-7). Every action is SystemAdmin-only,
/// including issuance (R8). All work flows through MediatR so the ValidationBehavior pipeline runs.
/// </summary>
[ApiController]
[Route("api/dcn")]
public class DocumentNumberControlController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentNumberControlController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered scheme list (R4).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<NumberingSchemeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<NumberingSchemeResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetNumberingSchemesPagedQuery(search, page, pageSize)));

    /// <summary>Single scheme read incl. sequence state (R4.9). 404 when missing.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NumberingSchemeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NumberingSchemeResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetNumberingSchemeByIdQuery(id)));

    /// <summary>Create a scheme (R1, R2). 409 on duplicate code / scope conflict.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NumberingSchemeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NumberingSchemeResponse>> Create([FromBody] CreateNumberingSchemeRequest r)
    {
        var result = await _mediator.Send(new CreateNumberingSchemeCommand(
            r.Code, r.Name, r.DocumentType, r.FormatTemplate, r.Prefix, r.Suffix,
            r.SeqStart, r.SeqIncrement, r.SeqPadding, r.ResetRule, r.Scope, r.ScopeId, r.IsActive,
            GetActingAdmin()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a scheme's editable fields (R3). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NumberingSchemeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NumberingSchemeResponse>> Update(Guid id, [FromBody] UpdateNumberingSchemeRequest r)
        => Ok(await _mediator.Send(new UpdateNumberingSchemeCommand(
            id, r.Name, r.FormatTemplate, r.Prefix, r.Suffix, r.SeqIncrement, r.SeqPadding,
            r.ResetRule, r.IsActive, GetActingAdmin())));

    /// <summary>Reactivate a scheme (R7.2). 404 when missing.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NumberingSchemeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NumberingSchemeResponse>> Activate(Guid id)
        => Ok(await _mediator.Send(new SetSchemeActiveCommand(id, true, GetActingAdmin())));

    /// <summary>Deactivate a scheme (soft retire) (R7.1). 404 when missing.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NumberingSchemeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NumberingSchemeResponse>> Deactivate(Guid id)
        => Ok(await _mediator.Send(new SetSchemeActiveCommand(id, false, GetActingAdmin())));

    /// <summary>Issue the next number for a document type + scope (R5). 404/409 per rules.</summary>
    [HttpPost("issue")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(IssuedNumberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IssuedNumberResponse>> Issue([FromBody] IssueNumberRequest r)
        => Ok(await _mediator.Send(new IssueNumberCommand(r.DocumentType, r.Scope, r.ScopeId, GetActingAdmin())));

    /// <summary>Audit trail for a scheme, newest-first (R6.6). Missing id yields [] (R6.7).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<NumberingSchemeAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NumberingSchemeAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetSchemeAuditTrailQuery(id)));

    /// <summary>
    /// Resolves the acting administrator id from the validated JWT principal so the audit actor
    /// cannot be spoofed by the request body. Prefers NameIdentifier (sub), falling back to Name.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
