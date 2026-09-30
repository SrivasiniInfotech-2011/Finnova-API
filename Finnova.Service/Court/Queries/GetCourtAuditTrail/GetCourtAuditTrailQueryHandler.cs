using Finnova.Models.Contracts.Courts;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtAuditTrail;

public class GetCourtAuditTrailQueryHandler
    : IRequestHandler<GetCourtAuditTrailQuery, List<CourtAuditEntryResponse>>
{
    private readonly ICourtAuditRepository _auditRepository;

    public GetCourtAuditTrailQueryHandler(ICourtAuditRepository auditRepository)
        => _auditRepository = auditRepository;

    public async Task<List<CourtAuditEntryResponse>> Handle(
        GetCourtAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var entries = await _auditRepository.GetByCourtIdAsync(request.CourtId, cancellationToken);
        return entries.Select(e => e.ToResponse()).ToList();   // R5.5, R5.6
    }
}