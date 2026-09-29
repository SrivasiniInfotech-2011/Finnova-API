namespace Finnova.Models.Contracts.OrganizationHierarchy;

/// <summary>
/// Response model for an audit entry of an organization node in the hierarchy.
/// </summary>
/// <param name="Id">ID</param>
/// <param name="NodeId">Node ID</param>
/// <param name="Action">Action</param>
/// <param name="OldName">Old Name</param>
/// <param name="NewName">New Name</param>
/// <param name="OldParentId">Old Parent ID</param>
/// <param name="NewParentId">New Parent ID</param>
/// <param name="ChangedBy">Changed By</param>
/// <param name="ChangedAtUtc">Changed At (UTC)</param>
public record OrganizationNodeAuditEntryResponse(
    Guid Id,
    Guid NodeId,
    string Action,          // "Create" | "Update" | "Delete"
    string? OldName,
    string? NewName,
    Guid? OldParentId,
    Guid? NewParentId,
    string ChangedBy,
    DateTime ChangedAtUtc);