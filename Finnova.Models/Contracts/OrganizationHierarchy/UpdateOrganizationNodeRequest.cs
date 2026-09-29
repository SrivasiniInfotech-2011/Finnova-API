namespace Finnova.Models.Contracts.OrganizationHierarchy;

/// <summary>
/// Request model for updating an existing organization node in the hierarchy.
/// </summary>
/// <param name="Name">Name</param>
/// <param name="ParentId">Parent ID</param>
public record UpdateOrganizationNodeRequest(string Name, Guid? ParentId);
