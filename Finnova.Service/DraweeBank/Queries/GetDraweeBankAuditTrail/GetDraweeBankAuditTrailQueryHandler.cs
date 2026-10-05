using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;

public class GetDraweeBankAuditTrailQueryHandler
    : IRequestHandler<GetDraweeBankAuditTrailQuery, List<DraweeBankAuditEntryResponse>>
{
    private readonly IDraweeBankAuditRepository _auditRepository;

    public GetDraweeBankAuditTrailQueryHandler(IDraweeBankAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<List<DraweeBankAuditEntryResponse>> Handle(
        GetDraweeBankAuditTrailQuery request, CancellationToken ct)
    {
        // Ordered newest-first; empty list for an unknown id, never an error (R7.4, R7.5).
        var entries = await _auditRepository.GetByDraweeBankIdAsync(request.DraweeBankId, ct);
        return entries.Select(e => e.ToResponse()).ToList();
    }
}
