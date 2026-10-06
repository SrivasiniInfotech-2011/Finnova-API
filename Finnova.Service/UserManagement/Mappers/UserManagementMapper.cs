using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.UserManagement.Mappers;

/// <summary>Entity -> contract mappers. The password hash is NEVER mapped into any response.</summary>
public static class UserManagementMapper
{
    public static UserAccountResponse ToResponse(this UserAccount x) => new(
        x.Id, x.UserCode, x.Name, x.DateOfJoining, x.Designation, x.Department,
        x.MobileNumber, x.Email, x.UserType, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserGroupResponse ToResponse(this UserGroup x, IReadOnlyList<UserGroupMemberResponse> members) => new(
        x.Id, x.UserGroupCode, x.Name, members, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserGroupMemberResponse ToMemberResponse(this UserAccount x) => new(x.UserCode, x.Name, x.IsActive);

    /// <summary>
    /// Maps a functional group to its response. Since FunctionalGroupFunction stores only ProgramId,
    /// the caller supplies a ProgramId -> ProgramName lookup (resolved from the programs master); an
    /// unresolved id maps to an empty name.
    /// </summary>
    public static FunctionalGroupResponse ToResponse(
        this FunctionalGroup x, IReadOnlyDictionary<Guid, string> programNamesById) => new(
        x.Id, x.FunctionalGroupCode, x.RoleCenterName,
        x.Functions.Select(f => new FunctionalGroupFunctionResponse(
            f.ProgramId,
            programNamesById.TryGetValue(f.ProgramId, out var name) ? name : string.Empty,
            f.RoleCode)).ToList(),
        x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserListItemResponse ToResponse(this Finnova.Repository.Interfaces.UserListItemResult x) => new(
        x.Id, x.Code, x.Name, x.Kind, x.IsActive);

    public static List<UserListItemResponse> ToResponseList(
        this IEnumerable<Finnova.Repository.Interfaces.UserListItemResult> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static UserManagementAuditEntryResponse ToResponse(this UserManagementAuditEntry a) => new(
        a.Id, a.RecordId, a.RecordKind, a.Action.ToString(), a.Summary, a.ChangedBy, a.ChangedAtUtc);
}
