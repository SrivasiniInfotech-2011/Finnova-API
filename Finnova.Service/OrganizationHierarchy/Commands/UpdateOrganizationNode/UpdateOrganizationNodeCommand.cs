using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.UpdateOrganizationNode;

/// <summary>
/// Renames an existing organization node (only Name is editable; Code is immutable
/// post-create). ActingAdmin is resolved from the JWT by the controller and
/// recorded on the audit entry (R3, R4.1).
/// </summary>
public record UpdateOrganizationNodeCommand(
    Guid Id, string Name, Guid? ParentId, string ActingAdmin
) : IRequest<OrganizationNodeResponse>;
