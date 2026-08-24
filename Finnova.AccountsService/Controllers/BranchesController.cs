using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Branches;
using Finnova.Service.Branches.Commands.CreateBranch;
using Finnova.Service.Branches.Commands.UpdateBranch;
using Finnova.Service.Branches.Commands.DeleteBranch;
using Finnova.Service.Branches.Commands.SoftDeleteBranch;
using Finnova.Service.Branches.Queries.GetAllBranches;
using Finnova.Service.Branches.Queries.GetBranchById;

namespace Finnova.AccountsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<BranchResponse>>> GetAll()
    {
        var result = await _mediator.Send(new GetAllBranchesQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetBranchByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BranchResponse>> Create([FromBody] CreateBranchRequest request)
    {
        var command = new CreateBranchCommand(
            request.BranchType, request.CorporateCode, request.StateCode,
            request.BranchCode, request.BranchName, request.Address,
            request.Landmark, request.State, request.Country,
            request.Pincode, request.Telephone, request.Mobile,
            request.IsActive, request.IsOperational);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> Update(Guid id, [FromBody] UpdateBranchRequest request)
    {
        var command = new UpdateBranchCommand(
            id, request.BranchType, request.CorporateCode, request.StateCode,
            request.BranchCode, request.BranchName, request.Address,
            request.Landmark, request.State, request.Country,
            request.Pincode, request.Telephone, request.Mobile,
            request.IsActive, request.IsOperational);
        var result = await _mediator.Send(command);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        var result = await _mediator.Send(new SoftDeleteBranchCommand(id));
        return result ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteBranchCommand(id));
        return result ? NoContent() : NotFound();
    }
}
