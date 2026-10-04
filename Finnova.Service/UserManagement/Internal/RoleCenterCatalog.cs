namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Reference catalog of Role Centers -> their Programs (consumed master data, R6.1/6.3/8.1/8.3).
/// Seeded with India-only, English-only sample data mirroring the design (System Admin, Origination).
/// Replace with the real reference source when the owning master is wired.
/// </summary>
public static class RoleCenterCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> Catalog =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["System Admin"] = new[] { "Company Master", "User Master", "Lookup Master" },
            ["Origination"] = new[] { "Asset Master", "Entity Master", "Application Entry" },
        };

    public static IReadOnlyList<string> RoleCenters() => Catalog.Keys.ToList();

    public static IReadOnlyList<string> ProgramsFor(string roleCenterName)
        => Catalog.TryGetValue(roleCenterName?.Trim() ?? string.Empty, out var programs)
            ? programs
            : Array.Empty<string>();
}
