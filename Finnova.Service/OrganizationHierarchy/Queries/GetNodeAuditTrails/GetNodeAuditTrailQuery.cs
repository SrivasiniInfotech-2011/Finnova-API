using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeAuditTrails;

public record GetNodeAuditTrailQuery(Guid NodeId) : IRequest<List<OrganizationNodeAuditEntryResponse>>;

