using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetHierarchyTree;

public record GetHierarchyTreeQuery() : IRequest<List<OrganizationNodeTreeResponse>>;