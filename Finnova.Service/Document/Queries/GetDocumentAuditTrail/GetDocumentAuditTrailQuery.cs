using Finnova.Models.Contracts.Documents;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Document.Queries.GetDocumentAuditTrail;

public record GetDocumentAuditTrailQuery(Guid Id) : IRequest<List<DocumentAuditEntryResponse>>;

public class GetDocumentAuditTrailQueryHandler
    : IRequestHandler<GetDocumentAuditTrailQuery, List<DocumentAuditEntryResponse>>
{
    private readonly IDocumentAuditRepository _audit;
    public GetDocumentAuditTrailQueryHandler(IDocumentAuditRepository audit) => _audit = audit;

    public async Task<List<DocumentAuditEntryResponse>> Handle(
        GetDocumentAuditTrailQuery request, CancellationToken ct)
    {
        var entries = await _audit.GetByDocumentIdAsync(request.Id, ct);   // newest-first (R8.4), [] if none (R8.6)
        return entries.Select(e => e.ToResponse()).ToList();
    }
}