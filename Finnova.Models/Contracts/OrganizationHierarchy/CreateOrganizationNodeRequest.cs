namespace Finnova.Models.Contracts.OrganizationHierarchy;

    /// <summary>
    /// Request model for creating a new organization node in the hierarchy.
    /// </summary>
    /// <param name="Code">Code</param>
    /// <param name="Name">Name</param>
    /// <param name="ParentId">Parent ID</param>
    /// <param name="IsActive">Is Active</param>
    public record CreateOrganizationNodeRequest(string Code, string Name, Guid? ParentId, bool? IsActive);
