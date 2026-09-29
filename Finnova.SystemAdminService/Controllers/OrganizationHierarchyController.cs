using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Service.OrganizationHierarchy.Commands.CreateOrganizationNode;
using Finnova.Service.OrganizationHierarchy.Commands.DeleteOrganizationNode;
using Finnova.Service.OrganizationHierarchy.Commands.UpdateOrganizationNode;
using Finnova.Service.OrganizationHierarchy.Queries.GetHierarchyTree;
using Finnova.Service.OrganizationHierarchy.Queries.GetNodeAuditTrails;
using Finnova.Service.OrganizationHierarchy.Queries.GetNodeChildren;
using Finnova.Service.OrganizationHierarchy.Queries.GetOrganizationNodesPaged;
using Finnova.Service.Organizations.Commands.UpdateOrganization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Finnova.SystemAdminService.Controllers;

[Route("api/orghierarchy")]
[ApiController]
public class OrganizationHierarchyController : ControllerBase
{
    private readonly IMediator _mediator;
    public OrganizationHierarchyController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Gets the Paged Collection of type <see cref="List{OrganizationNodeResponse}"/>
    /// </summary>
    /// <param name="search">Search Term</param>
    /// <param name="page">Page Number</param>
    /// <param name="pageSize">Page Size</param>
    /// <returns></returns>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<OrganizationNodeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<OrganizationNodeResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetOrganizationNodesPagedQuery(search, page, pageSize)));

    /// <summary>
    /// Gets the Paged Collection of type <see cref="List{OrganizationNodeTreeResponse}"/>
    /// </summary>
    /// <param name="search">Search Term</param>
    /// <param name="page">Page Number</param>
    /// <param name="pageSize">Page Size</param>
    /// <returns></returns>
    [HttpGet("tree")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<OrganizationNodeTreeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationNodeTreeResponse>>> GetOrganizationNodeTree()
        => Ok(await _mediator.Send(new GetHierarchyTreeQuery()));

    /// <summary>
    /// Gets the Paged Collection of type <see cref="List{OrganizationNodeTreeResponse}"/>
    /// </summary>
    /// <param name="search">Search Term</param>
    /// <param name="page">Page Number</param>
    /// <param name="pageSize">Page Size</param>
    /// <returns></returns>
    [HttpGet("{id:guid}/children")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<OrganizationNodeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationNodeResponse>>> GetOrganizationNodeChildren(Guid id)
        => Ok(await _mediator.Send(new GetNodeChildrenQuery(id)));

    /// <summary>
    /// Create New Organization Node
    /// </summary>
    /// <param name="r"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(OrganizationNodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationNodeResponse>> Create([FromBody] CreateOrganizationNodeRequest r)
    {
        var result = await _mediator.Send(new CreateOrganizationNodeCommand(
            r.Code, r.Name, r.ParentId, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetPaged), new { search = result.Code }, result);
    }

    /// <summary>Rename a nationality (R3). SystemAdmin only. 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(OrganizationNodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationNodeResponse>> Update(Guid id, [FromBody] UpdateOrganizationNodeRequest r)
        => Ok(await _mediator.Send(new UpdateOrganizationNodeCommand(id, r.Name, r.ParentId, GetActingAdmin())));

    /// <summary>Leaf-only delete. SystemAdmin only. 404 when missing, 409 when the node has children.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteOrganizationNodeCommand(id, GetActingAdmin()));
        return NoContent();
    }
    /// <summary>Audit trail for a nationality, newest first (R4.4). Missing id yields [] (R4.5).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<OrganizationNodeAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationNodeAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetNodeAuditTrailQuery(id)));

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


