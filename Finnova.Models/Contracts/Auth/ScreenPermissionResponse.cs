namespace Finnova.Models.Contracts.Auth;

/// <summary>
/// Effective per-screen access flags for the signed-in user. Serialized as camelCase
/// (System.Text.Json default): { programName, canAdd, canModify, canQuery, canDelete }.
/// </summary>
public record ScreenPermissionResponse(
    string ProgramName,
    bool CanAdd,
    bool CanModify,
    bool CanQuery,
    bool CanDelete);

/// <summary>
/// The current user's permission snapshot. Serialized as camelCase:
/// { isAdmin, programs:[{ programName, canAdd, canModify, canQuery, canDelete }] }.
/// </summary>
public record MyPermissionsResponse(
    bool IsAdmin,
    IReadOnlyList<ScreenPermissionResponse> Programs);
