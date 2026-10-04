namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Reference catalog of Lines of Business and whether each is linked to defined Role Codes
/// (consumed master data, R7.3/7.5). India-only, English-only sample set. Replace with the real
/// reference source when the owning master is wired.
/// </summary>
public static class LineOfBusinessCatalog
{
    // LOBs that are linked to at least one Role Code (R7.5). Sample India-only business lines.
    private static readonly HashSet<string> WithRoleCodes =
        new(StringComparer.OrdinalIgnoreCase) { "Retail Lending", "Corporate Lending", "Leasing" };

    public static IReadOnlyList<string> ActiveLinesOfBusiness() => WithRoleCodes.ToList();

    public static bool HasRoleCodes(string lob) => WithRoleCodes.Contains(lob?.Trim() ?? string.Empty);
}
