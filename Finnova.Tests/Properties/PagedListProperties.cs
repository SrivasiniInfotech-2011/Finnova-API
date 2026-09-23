using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Lookup.Queries.GetLookupValuesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the paged list query handler (Properties 1, 3, 11).
/// Each test drives the real <see cref="GetLookupValuesPagedQueryHandler"/> over an
/// in-memory repository seeded from generated datasets.
/// </summary>
public class PagedListProperties
{
    private static readonly string[] Modules =
        { "Origination", "SystemAdmin", "Servicing", "Collections", null! };
    private static readonly string[] Types =
        { "MARITAL_STATUS", "SYS_TXN_TYPE", "GENDER", "ACCOUNT_STATUS", null! };

    private static Gen<string?> OptModule() => Gen.Elements(Modules).Select(x => (string?)x);
    private static Gen<string?> OptType() => Gen.Elements(Types).Select(x => (string?)x);
    private static Gen<bool?> OptActive() =>
        Gen.Elements(new bool?[] { true, false, null });

    private static GetLookupValuesPagedQueryHandler Handler(IEnumerable<LookupValue> data)
        => new(new InMemoryLookupRepository(data));

    // Feature: lookup-master-management, Property 1: Filter results satisfy the provided-filter
    // conjunction — every returned item matches all non-null filters (ordinal Module/Type,
    // IsActive), a null filter imposes no constraint, and no matching stored value is omitted.
    [Property(MaxTest = 200)]
    public Property Property1_FilterConjunction()
    {
        var arb = Arb.From(
            from data in LookupGenerators.DatasetGen()
            from module in OptModule()
            from type in OptType()
            from active in OptActive()
            select (data, module, type, active));

        return Prop.ForAll(arb, tuple =>
        {
            var (data, module, type, active) = tuple;

            // Use a large page size so the page contains every match (isolates filtering).
            var handler = Handler(data);
            var result = handler.Handle(
                new GetLookupValuesPagedQuery(module, type, active, 1, 200),
                CancellationToken.None).GetAwaiter().GetResult();

            bool Matches(LookupValue x) =>
                (module is null || x.Module == module) &&
                (type is null || x.LookupType == type) &&
                (active is null || x.IsActive == active.Value);

            // Every returned item matches all provided filters.
            var allMatch = result.Data.All(r =>
                (module is null || r.Module == module) &&
                (type is null || r.LookupType == type) &&
                (active is null || r.IsActive == active.Value));

            // No matching stored value is omitted (page big enough to hold them all).
            var expectedCount = data.Count(Matches);
            var noneOmitted = result.Data.Count == expectedCount && result.Total == expectedCount;

            return (allMatch && noneOmitted)
                .Label($"module={module} type={type} active={active} " +
                       $"returned={result.Data.Count} expected={expectedCount}");
        });
    }

    // Feature: lookup-master-management, Property 3: List ordering is DisplayOrder ascending with
    // Id tie-break, and repeated queries over identical data return the same sequence.
    [Property(MaxTest = 200)]
    public Property Property3_ListOrdering()
    {
        var arb = Arb.From(LookupGenerators.DatasetGen());

        return Prop.ForAll(arb, data =>
        {
            var handler = Handler(data);
            var q = new GetLookupValuesPagedQuery(null, null, null, 1, 500);

            var first = handler.Handle(q, CancellationToken.None).GetAwaiter().GetResult().Data;
            var second = handler.Handle(q, CancellationToken.None).GetAwaiter().GetResult().Data;

            var ordered = true;
            for (int i = 1; i < first.Count; i++)
            {
                var prev = first[i - 1];
                var cur = first[i];
                if (prev.DisplayOrder > cur.DisplayOrder)
                { ordered = false; break; }
                if (prev.DisplayOrder == cur.DisplayOrder && prev.Id.CompareTo(cur.Id) >= 0)
                { ordered = false; break; }
            }

            var deterministic = first.Select(x => x.Id).SequenceEqual(second.Select(x => x.Id));

            return (ordered && deterministic).Label("ordering DisplayOrder then Id, deterministic");
        });
    }

    // Feature: lookup-master-management, Property 11: Pagination consistency — the response echoes
    // page/pageSize, Total equals the count of matching values, TotalPages = ceil(Total/PageSize),
    // at most PageSize items are returned, and pages beyond the last are empty (Total still correct).
    [Property(MaxTest = 200)]
    public Property Property11_PaginationConsistency()
    {
        var arb = Arb.From(
            from data in LookupGenerators.DatasetGen()
            from module in OptModule()
            from type in OptType()
            from active in OptActive()
            from page in Gen.Choose(1, 8)
            from size in Gen.Choose(1, 10)
            select (data, module, type, active, page, size));

        return Prop.ForAll(arb, tuple =>
        {
            var (data, module, type, active, page, size) = tuple;
            var handler = Handler(data);

            var result = handler.Handle(
                new GetLookupValuesPagedQuery(module, type, active, page, size),
                CancellationToken.None).GetAwaiter().GetResult();

            bool Matches(LookupValue x) =>
                (module is null || x.Module == module) &&
                (type is null || x.LookupType == type) &&
                (active is null || x.IsActive == active.Value);

            var expectedTotal = data.Count(Matches);
            var expectedTotalPages = (int)Math.Ceiling(expectedTotal / (double)size);
            var lastPage = expectedTotal == 0 ? 1 : expectedTotalPages;

            var echoes = result.Page == page && result.PageSize == size;
            var totalCorrect = result.Total == expectedTotal;
            var totalPagesCorrect = result.TotalPages == expectedTotalPages;
            var withinPageSize = result.Data.Count <= size;
            var beyondLastEmpty = page <= lastPage || result.Data.Count == 0;

            return (echoes && totalCorrect && totalPagesCorrect && withinPageSize && beyondLastEmpty)
                .Label($"page={page} size={size} total={result.Total}/{expectedTotal} " +
                       $"pages={result.TotalPages}/{expectedTotalPages} items={result.Data.Count}");
        });
    }
}
