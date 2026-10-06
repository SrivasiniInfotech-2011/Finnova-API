namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// RoleCode = RoleCenterName concatenated with ProgramName (uppercased, no separators), e.g.
/// SSCMP / OOASM / OOETM (FS §9 R8.4). Deterministic pure function — property-tested (P2).
/// </summary>
public static class RoleCodeBuilder
{
    public static string Build(string roleCenterName, string programName)
        => ((roleCenterName ?? string.Empty) + (programName ?? string.Empty))
            .ToUpperInvariant();
}
