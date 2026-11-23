using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using FluentValidation;
using MediatR;

namespace Finnova.Service.Receipts.Queries;

public sealed record GetReceiptByIdQuery(Guid Id) : IRequest<ReceiptResponse>;
public sealed record GetReceiptsPagedQuery(ReceiptSearchRequest Request) : IRequest<PaginatedResponse<ReceiptListItemResponse>>;
public sealed record GetReceiptAuditTrailQuery(Guid ReceiptId) : IRequest<List<ReceiptAuditEntryResponse>>;

public sealed class GetReceiptsPagedQueryValidator : AbstractValidator<GetReceiptsPagedQuery>
{
    public GetReceiptsPagedQueryValidator()
    {
        RuleFor(x => x.Request.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.Request.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetReceiptByIdQueryHandler(IReceiptRepository receipts, IReceiptReferenceRepository refs)
    : IRequestHandler<GetReceiptByIdQuery, ReceiptResponse>
{
    public async Task<ReceiptResponse> Handle(GetReceiptByIdQuery q, CancellationToken ct)
    {
        var receipt = await receipts.GetDetailAsync(q.Id, ct) ?? throw ReceiptNotFoundException.ForReceipt(q.Id);
        // Contact data is read live, not snapshotted (data minimization).
        var c = await refs.GetCustomerByIdAsync(receipt.CustomerId, ct);
        var customer = c is null ? null
            : new ReceiptCustomerResponse(c.Id, c.Code, c.Name, c.Address, c.Phone, c.Mobile, c.Email, c.Website);
        return ReceiptMapper.ToResponse(receipt, customer);
    }
}

public sealed class GetReceiptsPagedQueryHandler(IReceiptRepository receipts)
    : IRequestHandler<GetReceiptsPagedQuery, PaginatedResponse<ReceiptListItemResponse>>
{
    public async Task<PaginatedResponse<ReceiptListItemResponse>> Handle(GetReceiptsPagedQuery q, CancellationToken ct)
    {
        var r = q.Request;
        var (items, total) = await receipts.SearchAsync(new ReceiptSearchFilter(
            r.Search, r.ReceiptType, r.Mode, r.Status, r.LineOfBusinessId, r.BranchId,
            r.DocDateFrom, r.DocDateTo, r.Page, r.PageSize), ct);
        var totalPages = (int)Math.Ceiling(total / (double)r.PageSize);
        return new PaginatedResponse<ReceiptListItemResponse>(
            items.Select(ReceiptMapper.ToListItem).ToList(), total, r.Page, r.PageSize, totalPages);
    }
}

public sealed class GetReceiptAuditTrailQueryHandler(IReceiptAuditRepository audits)
    : IRequestHandler<GetReceiptAuditTrailQuery, List<ReceiptAuditEntryResponse>>
{
    public async Task<List<ReceiptAuditEntryResponse>> Handle(GetReceiptAuditTrailQuery q, CancellationToken ct)
        => (await audits.GetByReceiptIdAsync(q.ReceiptId, ct)).Select(ReceiptMapper.ToAuditResponse).ToList();
}