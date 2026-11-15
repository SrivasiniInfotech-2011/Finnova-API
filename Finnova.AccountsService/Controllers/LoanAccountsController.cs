using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.LoanAccounts.Commands;
using Finnova.Service.LoanAccounts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/loan-accounts")]
public sealed class LoanAccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public LoanAccountsController(IMediator mediator) => _mediator = mediator;

    // Identities come from the validated JWT, never from the request body (four-eyes depends on this).
    private string ActorText => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "authenticated-user";
    private Guid ActorId => Guid.TryParse(ActorText, out var id)
        ? id : throw new LoanAccountValidationException("The authenticated user id is not a valid identifier.");
    private string ActorName => User.FindFirstValue(ClaimTypes.Name) ?? ActorText;
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet, Authorize(Policy = "AccountsRead")]
    public async Task<ActionResult<PaginatedResponse<LoanAccountListItemResponse>>> List([FromQuery] LoanAccountSearchRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoanAccountsQuery(r), ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "AccountsRead")]
    public async Task<ActionResult<LoanAccountResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoanAccountByIdQuery(id), ct));

    [HttpPut("{id:guid}"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<ActionResult<LoanAccountResponse>> Update(Guid id, UpdateLoanAccountRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateLoanAccountCommand(id, r.AccountName, r.Status, r.RowVersion, ActorText, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/resolution-requests"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<ActionResult<KycResolutionRequestResponse>> SubmitResolution(Guid id, SubmitKycResolutionRequest r, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitKycResolutionCommand(id, r.Reason, ActorId, ActorName, CorrelationId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    [HttpPost("{id:guid}/kyc/resolution-requests/{requestId:guid}/approve"), Authorize(Policy = "KycApprover")]
    public async Task<ActionResult<LoanAccountResponse>> Approve(Guid id, Guid requestId, DecideKycResolutionRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new DecideKycResolutionCommand(id, requestId, true, r.Comment, ActorId, ActorName, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/resolution-requests/{requestId:guid}/reject"), Authorize(Policy = "KycApprover")]
    public async Task<ActionResult<LoanAccountResponse>> Reject(Guid id, Guid requestId, DecideKycResolutionRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new DecideKycResolutionCommand(id, requestId, false, r.Comment, ActorId, ActorName, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/recheck"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<IActionResult> Recheck(Guid id, RecheckKycRequest r, CancellationToken ct)
    {
        await _mediator.Send(new RequestKycRecheckCommand(id, r.Reason, ActorText, CorrelationId), ct);
        return Accepted();
    }

    [HttpGet("operations/queue-status"), Authorize(Policy = "AccountsOps")]
    public async Task<ActionResult<AccountQueueStatusResponse>> QueueStatus(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAccountQueueStatusQuery(), ct));

    [HttpGet("operations/dead-letters"), Authorize(Policy = "AccountsOps")]
    public async Task<ActionResult<PaginatedResponse<DeadLetterItemResponse>>> DeadLetters([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAccountDeadLettersQuery(page, pageSize), ct));

    [HttpPost("operations/requests/{requestId:guid}/reprocess"), Authorize(Policy = "AccountsOps")]
    public async Task<IActionResult> Reprocess(Guid requestId, ReprocessRequest r, CancellationToken ct)
    {
        await _mediator.Send(new ReprocessAccountRequestCommand(requestId, r.Reason, ActorText, CorrelationId), ct);
        return Ok();
    }
}
