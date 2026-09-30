using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using Finnova.Service.Court.Commands.CreateCourt;
using Finnova.Service.Court.Commands.SetCourtActive;
using Finnova.Service.Court.Commands.UpdateCourt;
using Finnova.Service.Court.Queries.GetCourtAuditTrail;
using Finnova.Service.Court.Queries.GetCourtById;
using Finnova.Service.Court.Queries.GetCourtsPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Court Master endpoints (FINNOVA-13). Every action is SystemAdmin-only (R7). All work flows
/// through MediatR so the ValidationBehavior pipeline runs before each handler.
/// </summary>
[ApiController]
[Route("api/court")]
public class CourtController : ControllerBase
{
    private readonly IMediator _mediator;
    public CourtController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered court list (R3).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<CourtResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<CourtResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetCourtsPagedQuery(search, page, pageSize)));

    /// <summary>Single court read (R3.9). 404 when missing.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetCourtByIdQuery(id)));

    /// <summary>Create a court (R1, R2). 409 on duplicate code.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourtResponse>> Create([FromBody] CreateCourtRequest r)
    {
        var result = await _mediator.Send(new CreateCourtCommand(
            r.Code, r.Name, r.CourtType, r.Jurisdiction, r.Location, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a court's editable fields (R4). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Update(Guid id, [FromBody] UpdateCourtRequest r)
        => Ok(await _mediator.Send(new UpdateCourtCommand(
            id, r.Name, r.CourtType, r.Jurisdiction, r.Location, r.IsActive, GetActingAdmin())));

    /// <summary>Reactivate a court (R6.2). 404 when missing.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Activate(Guid id)
        => Ok(await _mediator.Send(new SetCourtActiveCommand(id, true, GetActingAdmin())));

    /// <summary>Deactivate a court (soft retire) (R6.1). 404 when missing.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Deactivate(Guid id)
        => Ok(await _mediator.Send(new SetCourtActiveCommand(id, false, GetActingAdmin())));

    /// <summary>Audit trail for a court, newest-first (R5.5). Missing id yields [] (R5.6).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<CourtAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CourtAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetCourtAuditTrailQuery(id)));

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