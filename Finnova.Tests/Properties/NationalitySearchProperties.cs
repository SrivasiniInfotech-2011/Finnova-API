using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for the paged-search filter (Property 10). Drives the real
/// <see cref="GetNationalitiesPagedQueryHandler"/> over the hand-written
/// <see cref="InMemoryNationalityRepository"/> (which faithfully reproduces the EF repo's
/// trimmed, case-insensitive Code-OR-Name substring semantics), using the custom
/// <see cref="NationalityGenerators"/> dataset arbitrary. FsCheck.Xunit v2, MaxTest = 200
/// (>= the required 100 iterations). Kept in its own file to avoid conflicts with the other
/// concurrently-authored nationality property suites.
/// </summary>
public class NationalitySearchProperties
{
    // A page window large enough to hold any generated dataset, so "no record matching the term
    // within the requested page window is omitted" can be checked against the full matching set.
    private const int FullPageSize = 1000;

    private static bool MatchesTerm(Nationality n, string term) =>
        n.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        n.Name.Contains(term, StringComparison.OrdinalIgnoreCase);

    // Feature: nationality-master-management, Property 10: Search filter conjunction — for any
    // dataset and any search term: every returned record contains the trimmed term
    // (case-insensitive) as a substring of its Code or Name; a term that is empty/whitespace/null
    // imposes no filter (returns the same records as no term); no record matching the term within
    // the requested page window is omitted; and a term matching nothing yields an empty collection
    // with Total == 0. Validates: Requirements 5.1, 5.2, 5.3, 5.11.
    [Property(MaxTest = 200)]
    public Property Property10_SearchFilterConjunction()
    {
        // A dataset (unique-by-code) plus a candidate search term. The term is drawn from a mix
        // of: a substring of an existing Code/Name (so matches occur), casing/whitespace variants,
        // blank/whitespace/null (no-filter case), and random strings (often matching nothing).
        var arb = Arb.From(
            from dataset in NationalityGenerators.DatasetGen()
            from term in TermGen(dataset)
            select (dataset, term));

        return Prop.ForAll(arb, tuple =>
        {
            var (dataset, term) = tuple;

            var repo = new InMemoryNationalityRepository(dataset);
            var handler = new GetNationalitiesPagedQueryHandler(repo);

            var page = handler.Handle(
                new GetNationalitiesPagedQuery(term, 1, FullPageSize),
                CancellationToken.None).GetAwaiter().GetResult();

            var isBlank = string.IsNullOrWhiteSpace(term);

            if (isBlank)
            {
                // Blank / whitespace / null term imposes no filter (R5.2, R5.3): the result set
                // equals the full dataset (by Id), and Total equals the dataset size.
                var returnedIds = page.Data.Select(r => r.Id).OrderBy(x => x).ToList();
                var allIds = dataset.Select(n => n.Id).OrderBy(x => x).ToList();

                var noFilter = returnedIds.SequenceEqual(allIds) && page.Total == dataset.Count;
                return noFilter.Label(
                    $"blank term '{term ?? "null"}' -> no filter: returned {returnedIds.Count}, " +
                    $"expected {allIds.Count}, total {page.Total}");
            }

            var trimmed = term.Trim();

            // Oracle: the exact set of records that should match (trimmed, case-insensitive,
            // Code OR Name substring).
            var expectedIds = dataset.Where(n => MatchesTerm(n, trimmed))
                .Select(n => n.Id).OrderBy(x => x).ToList();
            var actualIds = page.Data.Select(r => r.Id).OrderBy(x => x).ToList();

            // Every returned record genuinely contains the term (soundness).
            var everyReturnedMatches = page.Data.All(r =>
                r.Code.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));

            // No matching record within the (full) page window is omitted (completeness), and no
            // extra record is included — the returned set equals the oracle set exactly.
            var setMatches = actualIds.SequenceEqual(expectedIds);

            // Total equals the count of matching records; when nothing matches it is an empty
            // collection with Total == 0 (R5.11).
            var totalOk = page.Total == expectedIds.Count;
            var emptyWhenNoMatch = expectedIds.Count != 0 || (page.Data.Count == 0 && page.Total == 0);

            return (everyReturnedMatches && setMatches && totalOk && emptyWhenNoMatch)
                .Label(
                    $"term '{term}' -> returned {actualIds.Count}, expected {expectedIds.Count}, " +
                    $"total {page.Total}, everyMatches={everyReturnedMatches}");
        });
    }

    /// <summary>
    /// Generates search terms that meaningfully exercise the filter over a given dataset:
    ///   - a substring of an existing Code or Name (guarantees matches),
    ///   - a casing / surrounding-whitespace variant of an existing Code (trimmed CI matching),
    ///   - blank / whitespace / null (no-filter case),
    ///   - a random alphanumeric string (usually matches nothing -> empty result case).
    /// </summary>
    private static Gen<string?> TermGen(List<Nationality> dataset)
    {
        var blankOrNull = Gen.Elements<string?>(null, "", " ", "   ", "\t");

        var randomMiss =
            Gen.Choose(1, 12)
                .SelectMany(len => Gen.ArrayOf(len, Gen.Elements("qwertyuiop1234567890".ToCharArray())))
                .Select(chars => (string?)new string(chars));

        if (dataset.Count == 0)
            return Gen.Frequency(
                Tuple.Create(1, blankOrNull),
                Tuple.Create(1, randomMiss));

        var substringOfExisting =
            from pick in Gen.Elements(dataset.ToArray())
            from useName in Arb.Generate<bool>()
            let source = useName ? pick.Name : pick.Code
            from start in Gen.Choose(0, Math.Max(0, source.Length - 1))
            from length in Gen.Choose(1, Math.Max(1, source.Length - start))
            select (string?)source.Substring(start, length);

        var casingVariant =
            from pick in Gen.Elements(dataset.ToArray())
            from variant in NationalityGenerators.CasingWhitespaceVariantsOf(pick.Code)
            select (string?)variant;

        return Gen.Frequency(
            Tuple.Create(4, substringOfExisting),
            Tuple.Create(2, casingVariant),
            Tuple.Create(2, blankOrNull),
            Tuple.Create(2, randomMiss));
    }
}
