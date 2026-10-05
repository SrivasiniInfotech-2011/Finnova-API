namespace Finnova.Models.Contracts.UserManagement;

using Finnova.Models.Domain.Enums;

public record UserAccountResponse(
    Guid Id, string UserCode, string Name, DateTime DateOfJoining,
    string Designation, string Department, string? MobileNumber, string? Email,
    UserType UserType, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public record UserGroupMemberResponse(string UserCode, string Name, bool IsActive);

public record UserGroupResponse(
    Guid Id, string UserGroupCode, string Name,
    IReadOnlyList<UserGroupMemberResponse> Members, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public record FunctionalGroupFunctionResponse(Guid ProgramId, string ProgramName, string RoleCode);

public record FunctionalGroupResponse(
    Guid Id, string FunctionalGroupCode, string RoleCenterName,
    IReadOnlyList<FunctionalGroupFunctionResponse> Functions, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public record UserAccessResponse(
    Guid LineOfBusinessId,
    string LineOfBusinessName,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<string> BranchCodes);

/// <summary>Slim list projection (R13): one row covers all three record kinds via a discriminator.</summary>
public record UserListItemResponse(
    Guid Id, string Code, string Name, UserConfiguration Kind, bool IsActive);

public record UserManagementAuditEntryResponse(
    Guid Id, Guid RecordId, UserConfiguration RecordKind,
    string Action, string Summary, string ChangedBy, DateTime ChangedAtUtc);

// ---- Reference LOVs (consumed master data) ----
public record ReferenceItemResponse(string Code, string Label);     // designation/department/user-type/role-center/LOB

public record BranchTreeNodeResponse(
    string Code, string Name, string Level,                         // Location | Region | Branch
    IReadOnlyList<BranchTreeNodeResponse> Children);
