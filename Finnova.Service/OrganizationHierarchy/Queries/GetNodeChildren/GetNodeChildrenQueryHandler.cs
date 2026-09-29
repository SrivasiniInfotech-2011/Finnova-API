using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Repository.Repositories;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeChildren;

/// <summary>
/// Get the Node Childrens for an Organization Node.
/// </summary>
/// <param name="organizationNodeRepository">An instance of type <see cref="OrganizationNodeRepository"/></param>
public class GetNodeChildrenQueryHandler(IOrganizationNodeRepository organizationNodeRepository) : IRequestHandler<GetNodeChildrenQuery, List<OrganizationNodeResponse>>
{
    public async Task<List<OrganizationNodeResponse>> Handle(GetNodeChildrenQuery request, CancellationToken cancellationToken = default)
    {
        var organizationNode = await organizationNodeRepository.GetByIdAsync(request.NodeId, cancellationToken);

        if (organizationNode == null)
            throw new OrganizationNodeNotFoundException(request.NodeId);

        var organizationNodes = await organizationNodeRepository.GetByParentIdAsync(request.NodeId);

        return organizationNodes.ToResponseList();
    }
}
