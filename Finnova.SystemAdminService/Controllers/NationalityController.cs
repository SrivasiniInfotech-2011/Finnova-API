using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Service.Nationality.Commands.CreateNationality;
using Finnova.Service.Nationality.Commands.UpdateNationalityName;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Service.Nationality.Queries.GetNationalityAuditTrail;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Nationality Master endpoints (FINNOVA-9). Every action is SystemAdmin-only
/// (R6.1-R6.3, read included). All work flows through MediatR so the existing
/// ValidationBehavior pipeline runs before each handler.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NationalityController : ControllerBase
{
    private readonly IMediator _mediator;
    public NationalityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered admin list (R5). SystemAdmin only.</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<NationalityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<NationalityResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetNationalitiesPagedQuery(search, page, pageSize)));

    /// <summary>Create a nationality (R1, R2). SystemAdmin only.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NationalityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NationalityResponse>> Create([FromBody] CreateNationalityRequest r)
    {
        var result = await _mediator.Send(new CreateNationalityCommand(
            r.Code, r.Name, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetPaged), new { search = result.Code }, result);
    }

    /// <summary>Rename a nationality (R3). SystemAdmin only. 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NationalityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NationalityResponse>> Update(Guid id, [FromBody] UpdateNationalityNameRequest r)
        => Ok(await _mediator.Send(new UpdateNationalityNameCommand(id, r.Name, GetActingAdmin())));

    /// <summary>Audit trail for a nationality, newest first (R4.4). Missing id yields [] (R4.5).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<NationalityAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NationalityAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetNationalityAuditTrailQuery(id)));

    /// <summary>
    /// Resolves the acting administrator id from the validated JWT principal so the
    /// audit actor cannot be spoofed by the request body. Prefers the NameIdentifier
    /// (sub) claim, falling back to the Name claim.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
