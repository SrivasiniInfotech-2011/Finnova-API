using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Accounts;
using Finnova.Service.Transactions.Commands.CreateTransaction;
using Finnova.Service.Transactions.Queries.GetTransactionsByAccount;

namespace Finnova.AccountsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("account/{accountId:guid}")]
    public async Task<ActionResult<List<TransactionResponse>>> GetByAccount(
        Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTransactionsByAccountQuery(accountId, page, pageSize));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] CreateTransactionRequest request)
    {
        var command = new CreateTransactionCommand(
            request.AccountId, request.Type, request.Amount,
            request.Description, request.CounterpartyAccountId);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetByAccount), new { accountId = result.AccountId }, result);
    }
}
