using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.IdentityVerification.Queries;

public sealed record GetVerificationRecordsQuery(VerificationRecordQuery Query) : IRequest<PaginatedResponse<ValidationRecordResponse>>;

public sealed class GetVerificationRecordsHandler(IIdentityVerificationRepository repo) : IRequestHandler<GetVerificationRecordsQuery, PaginatedResponse<ValidationRecordResponse>>
{
    public async Task<PaginatedResponse<ValidationRecordResponse>> Handle(GetVerificationRecordsQuery request, CancellationToken ct)
    {
        var q = request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = request.Query.PageSize == 0 ? 20 : request.Query.PageSize };
        if (q.PageSize is < 1 or > 100) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["PageSize"]);
        var (items, total) = await repo.GetPagedAsync(q, ct);
        var data = items.Select(x => new ValidationRecordResponse(x.Id, x.SessionId, x.OperationType, x.ValidationStatus, x.ComparisonOutcome, x.Method, x.CreatedUtc, x.ActorReference, x.SafeReasonCode, x.SafeMessage, x.MaskedSubjectReference, x.ProtectedDocumentReferenceId is not null)).ToList();
        return new(data, total, q.Page, q.PageSize, (int)Math.Ceiling(total / (double)q.PageSize));
    }
}
