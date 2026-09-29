using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.DeleteOrganizationNode;

/// <summary>
/// Command to delete an organization node. Leaf-only: a node with children is rejected.
/// </summary>
/// <param name="Id">Id of the node to delete.</param>
/// <param name="ActingAdmin">Acting administrator id resolved from the JWT.</param>
public record DeleteOrganizationNodeCommand(Guid Id, string ActingAdmin) : IRequest<Unit>;
