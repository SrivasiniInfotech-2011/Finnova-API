namespace Finnova.Models.Contracts.OrganizationHierarchy;

/// <summary>
/// Response model for an organization node in the hierarchy.
/// </summary>
/// <param name="Id">ID</param>
/// <param name="Code">Code</param>
/// <param name="Name">Name</param>
/// <param name="Level">Level</param>
/// <param name="ParentId">Parent ID</param>
/// <param name="IsActive">Is Active</param>
/// <param name="CreatedAt">Created At</param>
/// <param name="UpdatedAt">Updated At</param>
public record OrganizationNodeResponse(
    Guid Id,
    string Code,
    string Name,
    int Level,
    Guid? ParentId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);