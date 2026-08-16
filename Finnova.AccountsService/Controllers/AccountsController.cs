using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Accounts;
using Finnova.Service.Accounts.Commands.CreateAccount;
using Finnova.Service.Accounts.Commands.CloseAccount;
using Finnova.Service.Accounts.Queries.GetAccountById;
using Finnova.Service.Accounts.Queries.GetAccountsByUser;

namespace Finnova.AccountsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetAccountByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<AccountResponse>>> GetByUser(Guid userId)
    {
        var result = await _mediator.Send(new GetAccountsByUserQuery(userId));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create([FromBody] CreateAccountRequest request)
    {
        var command = new CreateAccountCommand(
            request.AccountName, request.Type, request.Currency,
            request.UserId, request.OrganizationId,
            request.BranchCode, request.IfscCode, request.InitialDeposit);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<AccountResponse>> Close(Guid id)
    {
        var result = await _mediator.Send(new CloseAccountCommand(id));
        return result is null ? NotFound() : Ok(result);
    }
}
