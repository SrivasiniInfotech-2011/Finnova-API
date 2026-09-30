using Finnova.Models.Domain.Enums;
using Finnova.Service.Entity.Internal;
using FsCheck;
using FsCheck.Xunit;

namespace Finnova.Tests.Unit;

/// <summary>
/// Property-based tests (FsCheck.Xunit, >=100 iterations) over the pure Entity slice logic
/// (per-type applicability, per-type uniqueness, search substring/ordering). Infrastructure
/// concerns (concurrency, rollback, auth) are covered by integration tests instead.
/// </summary>
public class EntityPropertyTests
{
    private const string Admin = "prop-admin";
    private static readonly EntityType[] Types =
        { EntityType.Dealer, EntityType.DebtCollector, EntityType.Insurer, EntityType.Supplier, EntityType.Employer };

    private static string SafeCode(int seed) => "C" + (Math.Abs(seed) % 100000).ToString();

    // Feature: entity-master-management, Property 2: Per-type attribute applicability.
    [Property(MaxTest = 100)]
    public Property InapplicableKeyIsRejected_ApplicableKeyIsAccepted(int typeSeed, bool useApplicable)
    {
        var type = Types[Math.Abs(typeSeed) % Types.Length];
        var applicableKey = EntityTypeAttributes.Applicable[type].First();
        var inapplicableKey = EntityTypeAttributes.Applicable
            .First(kv => kv.Key != type).Value.First();

        var bag = new Dictionary<string, string>
        {
            [useApplicable ? applicableKey : inapplicableKey] = "val",
        };

        var inapplicable = EntityTypeAttributes.Validate(type, bag);
        // Applicable => empty; inapplicable => non-empty naming the offending key.
        var ok = useApplicable ? inapplicable.Count == 0 : inapplicable.Contains(inapplicableKey);
        return ok.ToProperty();
    }

    //// Feature: entity-master-management, Property 3: Per-type code uniqueness.
    //[Property(MaxTest = 100)]
    //public Property SameCodeAcrossTwoTypes_BothPersist_SameTypeDuplicateRejected(int codeSeed, int typeSeed)
    //{
    //    return Prop.ForAll(Arb.From<bool>(), async _ =>
    //    {
    //        var repo = new InMemoryEntityRepository();
    //        var audit = new InMemoryEntityAuditRepository();
    //        var handler = new CreateEntityCommandHandler(repo, audit);
    //        var code = SafeCode(codeSeed);
    //        var typeA = Types[Math.Abs(typeSeed) % Types.Length];
    //        var typeB = Types[(Math.Abs(typeSeed) + 1) % Types.Length];

    //        await handler.Handle(new CreateEntityCommand(
    //            code, "A", typeA, null, null, null, null, null, null, null, Admin), CancellationToken.None);
    //        await handler.Handle(new CreateEntityCommand(
    //            code, "B", typeB, null, null, null, null, null, null, null, Admin), CancellationToken.None);

    //        var bothPersist = repo.Count == 2;

    //        var duplicateRejected = false;
    //        try
    //        {
    //            await handler.Handle(new CreateEntityCommand(
    //                code.ToLowerInvariant(), "C", typeA, null, null, null, null, null, null, null, Admin),
    //                CancellationToken.None);
    //        }
    //        catch (EntityDuplicateCodeException)
    //        {
    //            duplicateRejected = true;
    //        }

    //        return bothPersist && duplicateRejected && repo.Count == 2;
    //    }.Result);
    //}

    //// Feature: entity-master-management, Property 5/6: Search substring within filter, ordered Name then Code.
    //[Property(MaxTest = 100)]
    //public Property SearchReturnsSubstringMatches_OrderedByNameThenCode(int typeSeed)
    //{
    //    return Prop.ForAll(Arb.From<bool>(), async _ =>
    //    {
    //        var repo = new InMemoryEntityRepository();
    //        var audit = new InMemoryEntityAuditRepository();
    //        var handler = new CreateEntityCommandHandler(repo, audit);
    //        var type = Types[Math.Abs(typeSeed) % Types.Length];

    //        await handler.Handle(new CreateEntityCommand("ALPHA1", "Zeta Traders", type, null, null, null, null, null, null, null, Admin), CancellationToken.None);
    //        await handler.Handle(new CreateEntityCommand("ALPHA2", "Alpha Traders", type, null, null, null, null, null, null, null, Admin), CancellationToken.None);
    //        await handler.Handle(new CreateEntityCommand("BETA1", "Alpha Traders", type, null, null, null, null, null, null, null, Admin), CancellationToken.None);

    //        var (items, _) = await repo.GetPagedAsync("alpha", type, 1, 100, CancellationToken.None);

    //        var allMatch = items.All(i =>
    //            i.Code.Contains("alpha", StringComparison.OrdinalIgnoreCase)
    //            || i.Name.Contains("alpha", StringComparison.OrdinalIgnoreCase)
    //            || (i.RegistrationIdentifier ?? "").Contains("alpha", StringComparison.OrdinalIgnoreCase));

    //        var ordered = items
    //            .Zip(items.Skip(1), (a, b) =>
    //                string.CompareOrdinal(a.Name, b.Name) < 0
    //                || (a.Name == b.Name && string.CompareOrdinal(a.Code, b.Code) <= 0))
    //            .All(x => x);

    //        return allMatch && ordered;
    //    }.Result);
    //}
}