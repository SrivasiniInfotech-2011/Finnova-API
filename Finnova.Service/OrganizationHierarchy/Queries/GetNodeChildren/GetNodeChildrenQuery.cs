using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeChildren;

public record GetNodeChildrenQuery(Guid NodeId) : IRequest<List<OrganizationNodeResponse>>;

