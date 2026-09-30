using Finnova.Models.Contracts.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntityAuditTrail;

public class GetEntityAuditTrailQueryHandler
    : IRequestHandler<GetEntityAuditTrailQuery, List<EntityAuditEntryResponse>>
{
    private readonly IEntityAuditRepository _auditRepository;

    public GetEntityAuditTrailQueryHandler(IEntityAuditRepository auditRepository)
        => _auditRepository = auditRepository;

    public async Task<List<EntityAuditEntryResponse>> Handle(
        GetEntityAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var entries = await _auditRepository.GetByEntityIdAsync(request.EntityId, cancellationToken);
        return entries.Select(e => e.ToResponse()).ToList();   // R5.5, R5.6
    }
}