using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Service.Lookup.Commands.CreateLookupValue;
using Finnova.Service.Lookup.Commands.UpdateLookupValue;
using Finnova.Service.Lookup.Commands.DeleteLookupValue;
using Finnova.Service.Lookup.Queries.GetLookupValuesPaged;
using Finnova.Service.Lookup.Queries.GetLookupDropdownItems;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupController : ControllerBase
{
    private readonly IMediator _mediator;
    public LookupController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered admin list. SystemAdmin only (R4, R7.2/7.3).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<LookupValueResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<LookupValueResponse>>> GetPaged(
        [FromQuery] string? module, [FromQuery] string? type, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetLookupValuesPagedQuery(module, type, isActive, page, pageSize)));

    /// <summary>Active dropdown items for a Module + Type. Any authenticated caller (R6, R7.4/7.5).</summary>
    [HttpGet("dropdown")]
    [Authorize]
    [ProducesResponseType(typeof(List<LookupDropdownItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LookupDropdownItemResponse>>> GetDropdown(
        [FromQuery] string module, [FromQuery] string type)
        => Ok(await _mediator.Send(new GetLookupDropdownItemsQuery(module, type)));

    /// <summary>Create a lookup value. SystemAdmin only (R2, R7).</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(LookupValueResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LookupValueResponse>> Create([FromBody] CreateLookupValueRequest r)
    {
        var result = await _mediator.Send(new CreateLookupValueCommand(
            r.Module, r.LookupType, r.Code, r.Value, r.DisplayOrder, r.IsActive));
        return CreatedAtAction(nameof(GetPaged), new { module = result.Module, type = result.LookupType }, result);
    }

    /// <summary>Update editable fields. SystemAdmin only. 409 on ERR-LKP-005 (R3, R5).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(LookupValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LookupValueResponse>> Update(Guid id, [FromBody] UpdateLookupValueRequest r)
        => Ok(await _mediator.Send(new UpdateLookupValueCommand(
            id, r.Code, r.Value, r.DisplayOrder, r.IsActive)));

    /// <summary>Delete. SystemAdmin only. 409 on ERR-LKP-005 / in-use (R3.1, R5).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteLookupValueCommand(id));
        return NoContent();
    }
}
