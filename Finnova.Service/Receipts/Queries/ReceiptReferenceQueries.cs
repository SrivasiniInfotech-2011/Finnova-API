using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Receipts;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.Receipts.Queries;

public sealed record GetReceiptLinesOfBusinessQuery : IRequest<List<LineOfBusinessRefResponse>>;
public sealed record GetReceiptBranchesQuery : IRequest<List<ApplicationReferenceOptionResponse>>;
public sealed record SearchReceiptCustomersQuery(string? Search, int Page, int PageSize) : IRequest<PaginatedResponse<ReceiptCustomerOption>>;
public sealed record GetReceiptCustomerQuery(string CustomerCode) : IRequest<ReceiptCustomerResponse>;
public sealed record GetReceiptContractsQuery(string CustomerCode) : IRequest<List<ReceiptContractOption>>;
public sealed record GetReceiptOfficersQuery : IRequest<List<ReceiptOfficerOption>>;
public sealed record GetReceiptDraweeBanksQuery : IRequest<List<ReceiptDraweeBankOption>>;
public sealed record GetReceiptDraweeBranchesQuery(Guid BankId, DateOnly? AsOf) : IRequest<List<ReceiptDraweeBranchOption>>;
public sealed record GetReceiptBankAccountsQuery : IRequest<List<ReceiptFaBankAccountOption>>;
public sealed record GetReceiptComponentsQuery : IRequest<List<LookupDropdownItemResponse>>;
public sealed record GetCashDenominationsQuery : IRequest<List<CashDenominationOption>>;

/// <summary>One handler class for all read-only reference queries (R13). Reuses the existing LOB and branch ports.</summary>
public sealed class ReceiptReferenceQueryHandlers(
    IReceiptReferenceRepository refs,
    ILineOfBusinessReferencePort lobs,
    IBranchReferencePort branches)
    : IRequestHandler<GetReceiptLinesOfBusinessQuery, List<LineOfBusinessRefResponse>>,
      IRequestHandler<GetReceiptBranchesQuery, List<ApplicationReferenceOptionResponse>>,
      IRequestHandler<SearchReceiptCustomersQuery, PaginatedResponse<ReceiptCustomerOption>>,
      IRequestHandler<GetReceiptCustomerQuery, ReceiptCustomerResponse>,
      IRequestHandler<GetReceiptContractsQuery, List<ReceiptContractOption>>,
      IRequestHandler<GetReceiptOfficersQuery, List<ReceiptOfficerOption>>,
      IRequestHandler<GetReceiptDraweeBanksQuery, List<ReceiptDraweeBankOption>>,
      IRequestHandler<GetReceiptDraweeBranchesQuery, List<ReceiptDraweeBranchOption>>,
      IRequestHandler<GetReceiptBankAccountsQuery, List<ReceiptFaBankAccountOption>>,
      IRequestHandler<GetReceiptComponentsQuery, List<LookupDropdownItemResponse>>,
      IRequestHandler<GetCashDenominationsQuery, List<CashDenominationOption>>
{
    public Task<List<LineOfBusinessRefResponse>> Handle(GetReceiptLinesOfBusinessQuery q, CancellationToken ct)
        => lobs.GetActiveAsync(ct);

    public Task<List<ApplicationReferenceOptionResponse>> Handle(GetReceiptBranchesQuery q, CancellationToken ct)
        => branches.GetActiveAsync(null, ct);

    public async Task<PaginatedResponse<ReceiptCustomerOption>> Handle(SearchReceiptCustomersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var (items, total) = await refs.SearchActiveCustomersAsync(q.Search, page, size, ct);
        return new PaginatedResponse<ReceiptCustomerOption>(
            items.Select(c => new ReceiptCustomerOption(c.Id, c.Code, c.Name)).ToList(),
            total, page, size, (int)Math.Ceiling(total / (double)size));
    }

    public async Task<ReceiptCustomerResponse> Handle(GetReceiptCustomerQuery q, CancellationToken ct)
    {
        var c = await refs.GetActiveCustomerByCodeAsync(q.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{q.CustomerCode.Trim()}' was not found or is inactive.");
        return new ReceiptCustomerResponse(c.Id, c.Code, c.Name, c.Address, c.Phone, c.Mobile, c.Email, c.Website);
    }

    public async Task<List<ReceiptContractOption>> Handle(GetReceiptContractsQuery q, CancellationToken ct)
    {
        var c = await refs.GetActiveCustomerByCodeAsync(q.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{q.CustomerCode.Trim()}' was not found or is inactive.");
        return (await refs.GetLiveContractsOfCustomerAsync(c.Id, ct))
            .Select(x => new ReceiptContractOption(x.AccountNumber, x.AccountName)).ToList();
    }

    public async Task<List<ReceiptOfficerOption>> Handle(GetReceiptOfficersQuery q, CancellationToken ct)
        => (await refs.GetActiveOfficersAsync(ct)).Select(x => new ReceiptOfficerOption(x.Id, x.UserCode, x.Name)).ToList();

    public async Task<List<ReceiptDraweeBankOption>> Handle(GetReceiptDraweeBanksQuery q, CancellationToken ct)
        => (await refs.GetActiveDraweeBanksAsync(ct)).Select(x => new ReceiptDraweeBankOption(x.Id, x.Code, x.Name)).ToList();

    public async Task<List<ReceiptDraweeBranchOption>> Handle(GetReceiptDraweeBranchesQuery q, CancellationToken ct)
        => (await refs.GetDraweeBranchesAsync(q.BankId, q.AsOf ?? ReceiptClock.TodayIst(), ct))
            .Select(x => new ReceiptDraweeBranchOption(x.Id, x.PlaceCode, x.PlaceName)).ToList();

    public async Task<List<ReceiptFaBankAccountOption>> Handle(GetReceiptBankAccountsQuery q, CancellationToken ct)
        => (await refs.GetActiveFaBankAccountsAsync(ct)).Select(x => new ReceiptFaBankAccountOption(
            x.Id, x.BankName, x.BankBranch, x.AccountNumber, x.MicrCode, x.GlCode, x.SlCode)).ToList();

    public Task<List<LookupDropdownItemResponse>> Handle(GetReceiptComponentsQuery q, CancellationToken ct)
        => refs.GetActiveComponentsAsync(ct);

    public Task<List<CashDenominationOption>> Handle(GetCashDenominationsQuery q, CancellationToken ct)
        => Task.FromResult(CashDenominationCatalog.Entries
            .Select(e => new CashDenominationOption(e.Kind, e.FaceValue, e.Label)).ToList());
}