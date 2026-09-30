using System.Text.Json;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Entity.Internal;

/// <summary>
/// Pure domain helper (FINNOVA-11 R1.6 / R4.3): resolves the per-type attribute keys applicable to
/// each Entity Type and validates a submitted attribute bag against them. India-only, English
/// identifiers (GSTIN / IRDAI / PAN-style); no external registry validation.
/// </summary>
public static class EntityTypeAttributes
{
    /// <summary>Applicable attribute keys per Entity Type (modest starting set; confirmation-flagged).</summary>
    public static readonly IReadOnlyDictionary<EntityType, IReadOnlySet<string>> Applicable =
        new Dictionary<EntityType, IReadOnlySet<string>>
        {
            [EntityType.Dealer] = new HashSet<string>(StringComparer.Ordinal) { "dealerLicenseNo" },
            [EntityType.DebtCollector] = new HashSet<string>(StringComparer.Ordinal) { "agencyLicenseNo" },
            [EntityType.Insurer] = new HashSet<string>(StringComparer.Ordinal) { "irdaiRegistrationNo" },
            [EntityType.Supplier] = new HashSet<string>(StringComparer.Ordinal) { "gstin" },
            [EntityType.Employer] = new HashSet<string>(StringComparer.Ordinal) { "employerRegistrationNo" },
        };

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>
    /// Returns the keys in <paramref name="attributes"/> that are NOT applicable to
    /// <paramref name="type"/>. An empty result means every submitted key is applicable (or the bag
    /// is null/empty).
    /// </summary>
    public static IReadOnlyList<string> Validate(
        EntityType type, IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
            return Array.Empty<string>();

        var allowed = Applicable.TryGetValue(type, out var set) ? set : new HashSet<string>();
        return attributes.Keys.Where(k => !allowed.Contains(k)).ToList();
    }

    /// <summary>
    /// Normalizes a submitted attribute bag to compact JSON for storage, or null when empty.
    /// Keys/values are stored as supplied (values trimmed).
    /// </summary>
    public static string? Serialize(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
            return null;

        var normalized = attributes.ToDictionary(kv => kv.Key, kv => kv.Value?.Trim() ?? string.Empty);
        return JsonSerializer.Serialize(normalized, Options);
    }

    /// <summary>Deserializes a stored JSON attribute bag into a dictionary (empty when null/blank).</summary>
    public static IReadOnlyDictionary<string, string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options)
               ?? new Dictionary<string, string>();
    }
}