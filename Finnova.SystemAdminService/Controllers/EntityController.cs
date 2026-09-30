using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Entity.Commands.CreateEntity;
using Finnova.Service.Entity.Commands.SetEntityActive;
using Finnova.Service.Entity.Commands.UpdateEntity;
using Finnova.Service.Entity.Queries.GetEntitiesPaged;
using Finnova.Service.Entity.Queries.GetEntityAuditTrail;
using Finnova.Service.Entity.Queries.GetEntityById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Entity Master endpoints (FINNOVA-11). Every action is SystemAdmin-only (R7). All work flows
/// through MediatR so the ValidationBehavior pipeline runs before each handler.
/// </summary>
[ApiController]
[Route("api/entity")]
public class EntityController : ControllerBase
{
    private readonly IMediator _mediator;
    public EntityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered entity list with optional Entity Type filter (R3).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<EntityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<EntityResponse>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery] EntityType? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetEntitiesPagedQuery(search, entityType, page, pageSize)));

    /// <summary>Single entity read (R3.12). 404 when missing.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetEntityByIdQuery(id)));

    /// <summary>Create an entity (R1, R2). 409 on duplicate code within the type.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EntityResponse>> Create([FromBody] CreateEntityRequest r)
    {
        var result = await _mediator.Send(new CreateEntityCommand(
            r.Code, r.Name, r.EntityType, r.RegistrationIdentifier, r.ContactPerson, r.Email,
            r.Phone, r.AddressLine, r.Attributes, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update an entity's editable fields (R4). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse>> Update(Guid id, [FromBody] UpdateEntityRequest r)
        => Ok(await _mediator.Send(new UpdateEntityCommand(
            id, r.Name, r.RegistrationIdentifier, r.ContactPerson, r.Email, r.Phone,
            r.AddressLine, r.Attributes, r.IsActive, GetActingAdmin())));

    /// <summary>Reactivate an entity (R6.2). 404 when missing.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse>> Activate(Guid id)
        => Ok(await _mediator.Send(new SetEntityActiveCommand(id, true, GetActingAdmin())));

    /// <summary>Deactivate an entity (soft retire) (R6.1). 404 when missing.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EntityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse>> Deactivate(Guid id)
        => Ok(await _mediator.Send(new SetEntityActiveCommand(id, false, GetActingAdmin())));

    /// <summary>Audit trail for an entity, newest-first (R5.5). Missing id yields [] (R5.6).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<EntityAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EntityAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetEntityAuditTrailQuery(id)));

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