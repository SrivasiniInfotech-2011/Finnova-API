using FsCheck;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// A generated create/update field payload for a nationality. Kept separate from the command
/// types so a single generated shape can drive both the create and update paths.
/// </summary>
public sealed record NationalityFields(
    string Code,
    string Name,
    bool? IsActive);

/// <summary>
/// Custom FsCheck (v2) generators producing valid and edge-case nationality data:
///   - non-empty, non-whitespace Code within max 10 and Name within max 100, including the
///     exact boundary lengths (10 / 100),
///   - casing and surrounding-whitespace variants of a code (to exercise trimmed,
///     case-insensitive uniqueness R2),
///   - blank strings (empty / whitespace-only) for validation edges,
///   - datasets with unique (case-insensitive, trimmed) Code keys so they can be loaded into a
///     store that enforces uniqueness,
///   - audit-entry sets for a nationality, including the empty set (R4.4 / R4.5).
/// </summary>
public static class NationalityGenerators
{
    public const int MaxCodeLength = 10;
    public const int MaxNameLength = 100;

    private const string CodeAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    // Names may contain spaces internally (e.g. "New Zealander"); still trimmed non-blank.
    private const string NameAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ";

    private static Gen<char> CharFrom(string alphabet) =>
        Gen.Elements(alphabet.ToCharArray());

    /// <summary>Non-empty string of length 1..max drawn from an alphabet.</summary>
    private static Gen<string> BoundedString(string alphabet, int max) =>
        Gen.Choose(1, max).SelectMany(len =>
            Gen.ArrayOf(len, CharFrom(alphabet)).Select(chars => new string(chars)));

    /// <summary>String of exactly <paramref name="len"/> characters from an alphabet.</summary>
    private static Gen<string> ExactString(string alphabet, int len) =>
        Gen.ArrayOf(len, CharFrom(alphabet)).Select(chars => new string(chars));

    // ---- Field generators ----

    /// <summary>Valid Code: 1..10 chars, favoring the exact boundary length of 10.</summary>
    public static Gen<string> CodeGen() =>
        Gen.Frequency(
            Tuple.Create(1, ExactString(CodeAlphabet, MaxCodeLength)),   // boundary: exactly 10
            Tuple.Create(4, BoundedString(CodeAlphabet, MaxCodeLength)));

    /// <summary>
    /// Valid Name: 1..100 chars, favoring the exact boundary length of 100. A generated name is
    /// trimmed so it is never blank and never leads/trails with whitespace.
    /// </summary>
    public static Gen<string> NameGen() =>
        Gen.Frequency(
                Tuple.Create(1, ExactString(NameAlphabet, MaxNameLength)),   // boundary: exactly 100
                Tuple.Create(4, BoundedString(NameAlphabet, MaxNameLength)))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);

    /// <summary>Blank strings: empty and whitespace-only variants for validation edges.</summary>
    public static Gen<string> BlankGen() =>
        Gen.Elements("", " ", "   ", "\t", " \t ", "\n");

    /// <summary>
    /// Casing / surrounding-whitespace variants of a given code that must be treated as the same
    /// code after trimming + case-insensitive comparison (R2.1, R2.4).
    /// </summary>
    public static Gen<string> CasingWhitespaceVariantsOf(string code)
    {
        var trimmed = code.Trim();
        return Gen.Elements(
            trimmed.ToUpperInvariant(),
            trimmed.ToLowerInvariant(),
            "  " + trimmed,
            trimmed + "  ",
            "  " + trimmed + "  ",
            "\t" + trimmed.ToUpperInvariant(),
            " " + trimmed.ToLowerInvariant() + " ");
    }

    /// <summary>A fully valid create/update payload (IsActive drawn from null/true/false).</summary>
    public static Gen<NationalityFields> FieldsGen()
    {
        return
            from code in CodeGen()
            from name in NameGen()
            from isActive in Gen.Elements<bool?>(null, true, false)
            select new NationalityFields(code, name, isActive);
    }

    // ---- Entity generators ----

    private static Gen<Nationality> NationalityGen()
    {
        return
            from code in CodeGen()
            from name in NameGen()
            from active in Arb.Generate<bool>()
            select new Nationality
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                IsActive = active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
    }

    /// <summary>
    /// A dataset of nationalities with unique Code scope keys (trimmed + case-insensitive) so it
    /// can be loaded into a store that enforces uniqueness.
    /// </summary>
    public static Gen<List<Nationality>> DatasetGen() =>
        Gen.ListOf(NationalityGen()).Select(list => DedupeByCode(list.ToList()));

    private static List<Nationality> DedupeByCode(List<Nationality> list)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Nationality>();
        foreach (var n in list)
        {
            if (seen.Add(n.Code.Trim()))
                result.Add(n);
        }
        return result;
    }

    // ---- Audit-entry generators ----

    private static Gen<NationalityAuditEntry> AuditEntryGen(Guid nationalityId)
    {
        return
            from action in Gen.Elements(NationalityAuditAction.Create, NationalityAuditAction.Update)
            from oldName in NameGen()
            from newName in NameGen()
            from changedBy in BoundedString(CodeAlphabet, 40)
            from offsetSeconds in Gen.Choose(0, 100_000)
            select new NationalityAuditEntry
            {
                Id = Guid.NewGuid(),
                NationalityId = nationalityId,
                Action = action,
                OldName = action == NationalityAuditAction.Create ? null : oldName,
                NewName = newName,
                ChangedBy = changedBy,
                // Deliberately allow colliding timestamps so the Id-desc tie-break is exercised.
                ChangedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(offsetSeconds),
            };
    }

    /// <summary>
    /// A set of audit entries for a single nationality, including the empty set (R4.4 / R4.5).
    /// Some timestamps may collide so the deterministic Id-descending tie-break is covered.
    /// </summary>
    public static Gen<(Guid NationalityId, List<NationalityAuditEntry> Entries)> AuditEntrySetGen()
    {
        var id = Guid.NewGuid();
        return Gen.ListOf(AuditEntryGen(id))
            .Select(entries => (id, entries.ToList()));
    }

    // ---- Arbitrary registration ----

    public static Arbitrary<NationalityFields> ArbFields() => Arb.From(FieldsGen());

    public static Arbitrary<List<Nationality>> ArbDataset() => Arb.From(DatasetGen());

    public static Arbitrary<Nationality> ArbNationality() => Arb.From(NationalityGen());

    public static Arbitrary<(Guid NationalityId, List<NationalityAuditEntry> Entries)> ArbAuditEntrySet() =>
        Arb.From(AuditEntrySetGen());
}

/// <summary>Marker type used to register <see cref="NationalityGenerators"/> via [Properties].</summary>
public sealed class NationalityArbitraries
{
    public static Arbitrary<NationalityFields> Fields() => NationalityGenerators.ArbFields();
    public static Arbitrary<List<Nationality>> Dataset() => NationalityGenerators.ArbDataset();
    public static Arbitrary<Nationality> Nationality() => NationalityGenerators.ArbNationality();
    public static Arbitrary<(Guid NationalityId, List<NationalityAuditEntry> Entries)> AuditEntrySet() =>
        NationalityGenerators.ArbAuditEntrySet();
}
