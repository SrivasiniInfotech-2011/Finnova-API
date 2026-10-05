namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Reference catalog of Role Centers -> their Programs (consumed master data, R6.1/6.3/8.1/8.3).
/// Seeded with India-only, English-only sample data mirroring the design (System Admin, Origination).
/// The program names here are the stable <c>programs.ProgramName</c> keys seeded by
/// <c>ScreenProgramConfiguration</c>, so every emitted program resolves to a real <c>programs</c>
/// row (its <c>ProgramId</c>). This keeps RoleCode = RoleCenterName + ProgramName deterministic
/// while allowing the satellite tables to hold a hard FK to <c>programs</c>.
/// </summary>
public static class RoleCenterCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> Catalog =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["System Admin"] = new[] { "LookupMaster", "NationalityMaster", "UserManagement" },
            ["Origination"] = new[] { "EntityMaster", "LocationMaster", "Organization" },
        };

    public static IReadOnlyList<string> RoleCenters() => Catalog.Keys.ToList();

    public static IReadOnlyList<string> ProgramsFor(string roleCenterName)
        => Catalog.TryGetValue(roleCenterName?.Trim() ?? string.Empty, out var programs)
            ? programs
            : Array.Empty<string>();
}
