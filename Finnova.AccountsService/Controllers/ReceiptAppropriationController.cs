using System.Security.Claims;
using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Service.ReceiptAppropriation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/receipt-appropriation")]
public sealed class ReceiptAppropriationController(IMediator mediator) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;

    [HttpGet("{receiptId:guid}/dues"), Authorize(Policy = "ReceiptAppropriationRead")]
    public async Task<ActionResult<ShowAppropriationResponse>> Show(Guid receiptId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) => Ok(await mediator.Send(new ShowAppropriationQuery(receiptId, page, pageSize, User), ct));
    [HttpPost("{receiptId:guid}/suggest"), Authorize(Policy = "ReceiptAppropriationRead")]
    public async Task<ActionResult<AppropriationPreviewResponse>> Suggest(Guid receiptId, CancellationToken ct) => Ok(await mediator.Send(new SuggestAppropriationQuery(receiptId, User, Correlation), ct));
    [HttpPost("{receiptId:guid}/confirm"), Authorize(Policy = "ReceiptAppropriationConfirm")]
    public async Task<ActionResult<ConfirmedAppropriationResponse>> Confirm(Guid receiptId, ConfirmAppropriationRequest request, CancellationToken ct) => Ok(await mediator.Send(new ConfirmAppropriationCommand(receiptId, request, User, Actor, Correlation), ct));
}