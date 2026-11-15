using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts.Queries;

public sealed record GetLoanAccountsQuery(LoanAccountSearchRequest Filters) : IRequest<PaginatedResponse<LoanAccountListItemResponse>>;
public sealed record GetLoanAccountByIdQuery(Guid Id) : IRequest<LoanAccountResponse>;
public sealed record GetAccountQueueStatusQuery : IRequest<AccountQueueStatusResponse>;
public sealed record GetAccountDeadLettersQuery(int Page, int PageSize) : IRequest<PaginatedResponse<DeadLetterItemResponse>>;

public sealed class GetLoanAccountsQueryValidator : AbstractValidator<GetLoanAccountsQuery>
{
    public GetLoanAccountsQueryValidator()
    {
        RuleFor(x => x.Filters.Search).MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");
        RuleFor(x => x.Filters.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(x => x.Filters.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetAccountDeadLettersQueryValidator : AbstractValidator<GetAccountDeadLettersQuery>
{
    public GetAccountDeadLettersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetLoanAccountsQueryHandler : IRequestHandler<GetLoanAccountsQuery, PaginatedResponse<LoanAccountListItemResponse>>
{
    private readonly ILoanAccountRepository _accounts;
    public GetLoanAccountsQueryHandler(ILoanAccountRepository accounts) => _accounts = accounts;

    public async Task<PaginatedResponse<LoanAccountListItemResponse>> Handle(GetLoanAccountsQuery q, CancellationToken ct)
    {
        var f = q.Filters;
        var (rows, total) = await _accounts.SearchAsync(new LoanAccountFilters(
            f.Search, f.KycStatus, f.KycFlagged, f.AccountStatus, f.LineOfBusinessId, f.BranchId,
            f.ProductCode, f.SchemeCode, f.HasPendingKycResolution, f.Page, f.PageSize), ct);
        var contacts = await _accounts.GetContactsAsync(
            rows.Where(r => r.Account.CustomerId.HasValue).Select(r => r.Account.CustomerId!.Value).Distinct().ToList(), ct);
        var items = rows.Select(r => r.ToListItem(
            r.Account.CustomerId is { } cid && contacts.TryGetValue(cid, out var c) ? c : null)).ToList();
        return new PaginatedResponse<LoanAccountListItemResponse>(
            items, total, f.Page, f.PageSize, (int)Math.Ceiling(total / (double)f.PageSize));
    }
}

public sealed class GetLoanAccountByIdQueryHandler : IRequestHandler<GetLoanAccountByIdQuery, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly AccountCreationKycOptions _options;
    public GetLoanAccountByIdQueryHandler(ILoanAccountRepository accounts, IOptions<AccountCreationKycOptions> options)
        => (_accounts, _options) = (accounts, options.Value);

    public Task<LoanAccountResponse> Handle(GetLoanAccountByIdQuery q, CancellationToken ct) =>
        LoanAccountViewLoader.LoadAsync(_accounts, _options, q.Id, ct);
}

public sealed class GetAccountQueueStatusQueryHandler : IRequestHandler<GetAccountQueueStatusQuery, AccountQueueStatusResponse>
{
    private readonly IAccountCreationRequestRepository _requests;
    public GetAccountQueueStatusQueryHandler(IAccountCreationRequestRepository requests) => _requests = requests;

    public async Task<AccountQueueStatusResponse> Handle(GetAccountQueueStatusQuery q, CancellationToken ct)
    {
        var counts = await _requests.CountByStatusAsync(ct);
        var all = Enum.GetValues<AccountRequestStatus>().ToDictionary(s => s.ToString(), s => counts.TryGetValue(s, out var n) ? n : 0);
        var oldest = await _requests.OldestNonCompletedAsync(ct);
        return new AccountQueueStatusResponse(all, oldest, oldest is null ? null : (DateTime.UtcNow - oldest.Value).TotalMinutes);
    }
}

public sealed class GetAccountDeadLettersQueryHandler : IRequestHandler<GetAccountDeadLettersQuery, PaginatedResponse<DeadLetterItemResponse>>
{
    private readonly IAccountCreationRequestRepository _requests;
    public GetAccountDeadLettersQueryHandler(IAccountCreationRequestRepository requests) => _requests = requests;

    public async Task<PaginatedResponse<DeadLetterItemResponse>> Handle(GetAccountDeadLettersQuery q, CancellationToken ct)
    {
        var (items, total) = await _requests.GetDeadLettersAsync(q.Page, q.PageSize, ct);
        return new PaginatedResponse<DeadLetterItemResponse>(
            items.Select(x => new DeadLetterItemResponse(x.Id, x.ApplicationNumber, x.FailureReason, x.AttemptCount, x.UpdatedAtUtc)).ToList(),
            total, q.Page, q.PageSize, (int)Math.Ceiling(total / (double)q.PageSize));
    }
}
