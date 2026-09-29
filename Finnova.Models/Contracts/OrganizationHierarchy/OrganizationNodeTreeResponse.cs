namespace Finnova.Models.Contracts.OrganizationHierarchy;

/// <summary>
/// Response model for an organization node in the hierarchy, including its children.
/// </summary>
/// <param name="Id">ID</param>
/// <param name="Code">Code</param>
/// <param name="Name">Name</param>
/// <param name="Level">Level</param>
/// <param name="ParentId">Parent ID</param>
/// <param name="IsActive">Is Active</param>
/// <param name="Children">Children</param>
public record OrganizationNodeTreeResponse(
    Guid Id,
    string Code,
    string Name,
    int Level,
    Guid? ParentId,
    bool IsActive,
    List<OrganizationNodeTreeResponse> Children);