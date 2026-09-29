using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetHierarchyTree;

public class GetHierarchyTreeQueryHandler(IOrganizationNodeRepository organizationNodeRepository) : IRequestHandler<GetHierarchyTreeQuery, List<OrganizationNodeTreeResponse>>
{
    public async Task<List<OrganizationNodeTreeResponse>> Handle(GetHierarchyTreeQuery request, CancellationToken cancellationToken)
    {
        var treeNodes = await organizationNodeRepository.GetAllForTreeAsync(cancellationToken);
        return BuildTree(treeNodes);
    }
    public List<OrganizationNodeTreeResponse> BuildTree(IReadOnlyCollection<OrganizationNode> nodes)
    {
        var byParent = nodes.ToLookup(n => n.ParentId);
        List<OrganizationNodeTreeResponse> Children(Guid? parentId) =>
            byParent[parentId]
                .OrderBy(n => n.Name).ThenBy(n => n.Code)
                .Select(n => new OrganizationNodeTreeResponse(
                    n.Id, n.Code, n.Name, n.Level, n.ParentId, n.IsActive, Children(n.Id)))
                .ToList();
        return Children(null);
    }
}