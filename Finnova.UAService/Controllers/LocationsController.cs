using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Locations;
using Finnova.Service.Locations.Commands.CreateLocation;
using Finnova.Service.Locations.Commands.DeleteLocation;
using Finnova.Service.Locations.Queries.GetLocationTree;
using Finnova.Service.Locations.Queries.GetLocationsByLevel;

namespace Finnova.UAService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LocationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the full location hierarchy as a nested tree.
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<List<LocationResponse>>> GetTree()
    {
        var result = await _mediator.Send(new GetLocationTreeQuery());
        return Ok(result);
    }

    /// <summary>
    /// Returns locations at a specific hierarchy level (flat, for parent selection).
    /// </summary>
    [HttpGet("level/{level:int}")]
    public async Task<ActionResult<List<LocationResponse>>> GetByLevel(int level)
    {
        var result = await _mediator.Send(new GetLocationsByLevelQuery(level));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<LocationResponse>> Create([FromBody] CreateLocationRequest request)
    {
        var command = new CreateLocationCommand(
            request.Code, request.Name, request.Level, request.ParentId,
            request.Description, request.IsActive, request.Latitude, request.Longitude);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteLocationCommand(id));
        return result ? NoContent() : NotFound();
    }
}
