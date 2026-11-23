using System.Security.Claims;
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Service.Receipts.Commands.CreateReceipt;
using Finnova.Service.Receipts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/receipts")]
public sealed class ReceiptsController(IMediator mediator) : ControllerBase
{
    // Identity comes from the validated JWT, never from the request body (R14.4).
    private string ActorText => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "authenticated-user";

    [HttpPost, Authorize(Policy = "ReceiptEntry")]
    public async Task<ActionResult<ReceiptResponse>> Create(CreateReceiptRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateReceiptCommand(request, ActorText, HttpContext.TraceIdentifier), ct);
        if (result.AlreadyExisted) return Ok(result.Receipt);
        return CreatedAtAction(nameof(GetById), new { id = result.Receipt.Id }, result.Receipt);
    }

    [HttpGet, Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<PaginatedResponse<ReceiptListItemResponse>>> List([FromQuery] ReceiptSearchRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptsPagedQuery(request), ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<ReceiptResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptByIdQuery(id), ct));

    [HttpGet("{id:guid}/audit-trail"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptAuditEntryResponse>>> AuditTrail(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptAuditTrailQuery(id), ct));

    // ---------- reference data (R13) ----------
    [HttpGet("references/lines-of-business"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<LineOfBusinessRefResponse>>> LinesOfBusiness(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptLinesOfBusinessQuery(), ct));

    [HttpGet("references/branches"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ApplicationReferenceOptionResponse>>> Branches(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptBranchesQuery(), ct));

    [HttpGet("references/customers"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<PaginatedResponse<ReceiptCustomerOption>>> Customers(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchReceiptCustomersQuery(search, page, pageSize), ct));

    [HttpGet("references/customers/{code}"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<ReceiptCustomerResponse>> Customer(string code, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptCustomerQuery(code), ct));

    [HttpGet("references/customers/{code}/contracts"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptContractOption>>> Contracts(string code, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptContractsQuery(code), ct));

    [HttpGet("references/marketing-officers"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptOfficerOption>>> MarketingOfficers(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptOfficersQuery(), ct));

    [HttpGet("references/drawee-banks"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptDraweeBankOption>>> DraweeBanks(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptDraweeBanksQuery(), ct));

    [HttpGet("references/drawee-banks/{id:guid}/branches"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptDraweeBranchOption>>> DraweeBranches(Guid id, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptDraweeBranchesQuery(id, asOf), ct));

    [HttpGet("references/bank-accounts"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptFaBankAccountOption>>> BankAccounts(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptBankAccountsQuery(), ct));

    [HttpGet("references/components"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<LookupDropdownItemResponse>>> Components(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptComponentsQuery(), ct));

    [HttpGet("references/denominations"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<CashDenominationOption>>> Denominations(CancellationToken ct)
        => Ok(await mediator.Send(new GetCashDenominationsQuery(), ct));
}