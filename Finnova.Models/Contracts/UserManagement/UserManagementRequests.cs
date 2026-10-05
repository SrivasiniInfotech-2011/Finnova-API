namespace Finnova.Models.Contracts.UserManagement;

using Finnova.Models.Domain.Enums;

// ---- Create ----

/// <summary>Create an individual user (R3). DOJ/IsActive nullable so the service defaults them (R3.7/3.15).</summary>
public record CreateUserAccountRequest(
    string Name,
    string Password,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool? IsActive);

/// <summary>Create a user group (R5). MemberUserCodes: the active users tagged to the group.</summary>
public record CreateUserGroupRequest(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool? IsActive);

/// <summary>Create a functional group (R6). Code is generated from the Role Center Name.</summary>
public record CreateFunctionalGroupRequest(
    string RoleCenterName,
    bool? IsActive);

// ---- Update (Modify mode, R11) ----

/// <summary>Modify a user (R11.1/11.3). Password is changed only via ResetPassword (R11.5/11.8).</summary>
public record UpdateUserAccountRequest(
    string Name,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool IsActive);

public record ResetPasswordRequest(string NewPassword);        // Modify mode only (R11.4/11.5)

public record UpdateUserGroupRequest(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool IsActive);

// ---- Access assignment (R7/R8/R9/R10) ----

public record AccessRightRow(
    string RoleCode,
    string RoleCenterName,
    Guid ProgramId,                                            // FK -> programs.Id
    string ProgramName,                                        // retained: stable key + RoleCode parity
    string DisplayName,                                        // programs.DisplayName (UI label)
    bool CanAdd,
    bool CanModify,
    bool CanQuery,
    bool CanDelete);

public record CopyProfileRequest(string SourceUserCode, Guid SourceLineOfBusinessId);

/// <summary>
/// A single branch selection. <paramref name="LocationId"/> is the authoritative FK into locations
/// (null for the ALL sentinel). <paramref name="BranchCode"/> is reference-only display text; on
/// input it is optional (the server resolves it from the location's Code, or "ALL").
/// </summary>
public record BranchSelection(Guid? LocationId, bool IsAll, string BranchCode = "");

public record SaveUserAccessRequest(
    Guid LineOfBusinessId,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<BranchSelection> Branches,                   // ALL => { locationId:null, isAll:true } (R9.3/9.4)
    CopyProfileRequest? CopyProfile);                          // Create mode only (R10)
