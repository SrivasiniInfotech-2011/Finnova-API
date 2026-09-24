using FsCheck;
using FsCheck.Xunit;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for deterministic list ordering (Property 12: List ordering is
/// deterministic). Drives the real <see cref="GetNationalitiesPagedQueryHandler"/> over the
/// hand-written <see cref="InMemoryNationalityRepository"/>, whose <c>GetPagedAsync</c> mirrors the
/// EF-backed repository's ordering: Name ascending, then Code ascending for records sharing a
/// Name, using <see cref="StringComparer.Ordinal"/> (the in-memory double's explicit stand-in for
/// SQL Server's collation-backed ordering). Uses the shared
/// <see cref="NationalityGenerators.DatasetGen"/> dataset arbitrary. FsCheck.Xunit v2,
/// MaxTest = 200 (>= the required 100 iterations). Kept in its own file to avoid conflicts with
/// the other concurrently-authored nationality property suites.
/// </summary>
public class NationalityListOrderingProperties
{
    // A page window large enough to hold any generated dataset so the full ordered sequence is
    // returned in a single page and can be asserted end to end.
    private const int FullPageSize = 1000;

    // Feature: nationality-master-management, Property 12: List ordering is deterministic — for any
    // dataset, the paged list is ordered by Name ascending and, for records sharing a Name, by Code
    // ascending (matching the repository's StringComparer.Ordinal comparer semantics); and repeated
    // queries over identical data return the same sequence. Validates: Requirements 5.10.
    [Property(MaxTest = 200)]
    public Property Property12_ListOrderingIsDeterministic()
    {
        return Prop.ForAll(Arb.From(NationalityGenerators.DatasetGen()), dataset =>
        {
            var repo = new InMemoryNationalityRepository(dataset);
            var handler = new GetNationalitiesPagedQueryHandler(repo);

            var response = handler.Handle(
                new GetNationalitiesPagedQuery(null, 1, FullPageSize),
                CancellationToken.None).GetAwaiter().GetResult();

            var actualIds = response.Data.Select(r => r.Id).ToList();

            // Oracle: the dataset sorted by (Name asc, Code asc) with the same comparer the
            // repository uses (StringComparer.Ordinal).
            var expectedIds = dataset
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .Select(x => x.Id)
                .ToList();

            // The returned sequence equals the dataset sorted by (Name asc, Code asc).
            var orderedCorrectly = actualIds.SequenceEqual(expectedIds);

            // The returned sequence is itself non-decreasing under the (Name, Code) ordinal
            // comparison — a direct, redundant check of the ordering invariant.
            var pairwiseOrdered = true;
            for (var i = 1; i < response.Data.Count; i++)
            {
                var prev = response.Data[i - 1];
                var curr = response.Data[i];
                var nameCmp = string.CompareOrdinal(prev.Name, curr.Name);
                var cmp = nameCmp != 0 ? nameCmp : string.CompareOrdinal(prev.Code, curr.Code);
                if (cmp > 0)
                {
                    pairwiseOrdered = false;
                    break;
                }
            }

            // Repeated queries over identical data return the same sequence (determinism).
            var repeat = handler.Handle(
                new GetNationalitiesPagedQuery(null, 1, FullPageSize),
                CancellationToken.None).GetAwaiter().GetResult();
            var deterministic = repeat.Data.Select(r => r.Id).SequenceEqual(actualIds);

            return (orderedCorrectly && pairwiseOrdered && deterministic)
                .Label(
                    $"count={response.Data.Count} orderedCorrectly={orderedCorrectly} " +
                    $"pairwiseOrdered={pairwiseOrdered} deterministic={deterministic}");
        });
    }
}
