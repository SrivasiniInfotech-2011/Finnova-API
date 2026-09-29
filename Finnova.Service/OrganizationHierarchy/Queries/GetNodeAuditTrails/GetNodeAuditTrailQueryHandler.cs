using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeAuditTrails;

public class GetNodeAuditTrailQueryHandler(IOrganizationNodeAuditRepository organizationNodeAuditRepository) : IRequestHandler<GetNodeAuditTrailQuery, List<OrganizationNodeAuditEntryResponse>>
{
    public async Task<List<OrganizationNodeAuditEntryResponse>> Handle(GetNodeAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var auditTrials = await organizationNodeAuditRepository.GetByNodeIdAsync(request.NodeId);
        var lst = auditTrials.Select(S => S.ToResponse());
        return lst.ToList();
    }
}