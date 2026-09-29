using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Commands.CreateOrganizationNode;

/// <summary>
/// Command class for creating a new organization node. This command encapsulates the necessary data required to create an organization node, including its code, name, parent ID, active status, and the acting administrator's information.
/// </summary>
/// <param name="Code">Code</param>
/// <param name="Name">Name</param>
/// <param name="ParentId">Parent ID</param>
/// <param name="IsActive">Active status</param>
/// <param name="ActingAdmin">Acting administrator's information</param>
public record CreateOrganizationNodeCommand(string Code, string Name, Guid? ParentId, bool? IsActive, string ActingAdmin) : IRequest<OrganizationNodeResponse>;
