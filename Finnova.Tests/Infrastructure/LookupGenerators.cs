using FsCheck;
using Finnova.Models.Domain.Entities;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// A generated create/update field payload for a lookup value. Kept separate from the
/// command types so a single generated shape can drive both create and update paths.
/// </summary>
public sealed record LookupFields(
    string Module,
    string LookupType,
    string Code,
    string Value,
    int DisplayOrder,
    bool IsActive);

/// <summary>
/// Custom FsCheck (v2) generators producing valid and edge-case lookup data:
///   - non-empty, non-whitespace strings within model max lengths (Module 50, Type 100,
///     Code 50, Value 200),
///   - boundary DisplayOrder values (0 and 9999) plus interior values,
///   - a small alphabet of Modules / LookupTypes so scopes collide often enough to exercise
///     uniqueness, filtering, and ordering across generated datasets.
/// </summary>
public static class LookupGenerators
{
    private static readonly string[] Modules =
        { "Origination", "SystemAdmin", "Servicing", "Collections" };

    private static readonly string[] LookupTypes =
        { "MARITAL_STATUS", "SYS_TXN_TYPE", "GENDER", "ACCOUNT_STATUS" };

    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

    private static Gen<char> AlphaNumChar() =>
        Gen.Elements(Alphabet.ToCharArray());

    /// <summary>Non-empty string of length 1..max drawn from a safe alphabet.</summary>
    private static Gen<string> BoundedString(int max)
    {
        return Gen.Choose(1, max).SelectMany(len =>
            Gen.ArrayOf(len, AlphaNumChar()).Select(chars => new string(chars)));
    }

    private static Gen<string> ModuleGen() => Gen.Elements(Modules);

    private static Gen<string> LookupTypeGen() => Gen.Elements(LookupTypes);

    private static Gen<string> CodeGen() => BoundedString(50);

    private static Gen<string> ValueGen() => BoundedString(200);

    /// <summary>DisplayOrder favoring boundaries 0 and 9999 alongside interior values.</summary>
    private static Gen<int> DisplayOrderGen() =>
        Gen.Frequency(
            Tuple.Create(1, Gen.Constant(0)),
            Tuple.Create(1, Gen.Constant(9999)),
            Tuple.Create(3, Gen.Choose(0, 9999)));

    public static Gen<LookupFields> FieldsGen()
    {
        return
            from module in ModuleGen()
            from type in LookupTypeGen()
            from code in CodeGen()
            from value in ValueGen()
            from order in DisplayOrderGen()
            from active in Arb.Generate<bool>()
            select new LookupFields(module, type, code, value, order, active);
    }

    /// <summary>A stored LookupValue with a fresh Id and optional system-lock control.</summary>
    private static Gen<LookupValue> LookupValueGen(Gen<bool> lockedGen)
    {
        return
            from f in FieldsGen()
            from locked in lockedGen
            select new LookupValue
            {
                Id = Guid.NewGuid(),
                Module = f.Module,
                LookupType = f.LookupType,
                Code = f.Code,
                Value = f.Value,
                DisplayOrder = f.DisplayOrder,
                IsActive = f.IsActive,
                IsSystemLocked = locked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
    }

    /// <summary>
    /// A dataset of lookup values with unique (Module, LookupType, Code) scope keys so it can
    /// be loaded into a store that enforces uniqueness. Mixes locked and unlocked rows.
    /// </summary>
    public static Gen<List<LookupValue>> DatasetGen()
    {
        return Gen.ListOf(LookupValueGen(Arb.Generate<bool>()))
            .Select(list => Dedupe(list.ToList()));
    }

    private static List<LookupValue> Dedupe(List<LookupValue> list)
    {
        var seen = new HashSet<(string, string, string)>();
        var result = new List<LookupValue>();
        foreach (var v in list)
        {
            var key = (v.Module, v.LookupType, v.Code);
            if (seen.Add(key))
                result.Add(v);
        }
        return result;
    }

    // ---- Arbitrary registration ----

    public static Arbitrary<LookupFields> ArbFields() => Arb.From(FieldsGen());

    public static Arbitrary<List<LookupValue>> ArbDataset() => Arb.From(DatasetGen());

    /// <summary>An unlocked stored value.</summary>
    public static Arbitrary<LookupValue> ArbUnlockedValue() =>
        Arb.From(LookupValueGen(Gen.Constant(false)));

    /// <summary>A system-locked stored value.</summary>
    public static Arbitrary<LookupValue> ArbLockedValue() =>
        Arb.From(LookupValueGen(Gen.Constant(true)));
}

/// <summary>Marker type used to register <see cref="LookupGenerators"/> via [Properties].</summary>
public sealed class LookupArbitraries
{
    public static Arbitrary<LookupFields> Fields() => LookupGenerators.ArbFields();
    public static Arbitrary<System.Collections.Generic.List<LookupValue>> Dataset() => LookupGenerators.ArbDataset();
}
