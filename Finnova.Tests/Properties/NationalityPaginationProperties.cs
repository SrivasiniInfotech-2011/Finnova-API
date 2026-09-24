using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for the paged query path (Property 11: Pagination consistency). Drives the
/// real <see cref="GetNationalitiesPagedQueryHandler"/> over the hand-written in-memory
/// nationality repository (whose GetPagedAsync mirrors the EF-backed repo: filter, count-before-
/// paging, Name asc then Code asc ordering, page window). Uses the shared
/// <see cref="NationalityGenerators.DatasetGen"/> dataset generator plus generated page/pageSize.
/// Kept in its own class file to avoid conflicts with other concurrently-authored nationality
/// property suites. FsCheck.Xunit v2, MaxTest = 200 (>= the required 100 iterations).
/// </summary>
public class NationalityPaginationProperties
{
    // Feature: nationality-master-management, Property 11: Pagination consistency — for any dataset
    // and any valid page/pageSize, the response Total equals the full filtered count, TotalPages
    // equals ceil(Total / pageSize), at most pageSize items are returned, the reported Page/PageSize
    // echo the request, a page beyond the last returns an empty item collection while still
    // reporting the correct Total/Page/PageSize, and concatenating all pages in order reproduces the
    // full ordered set without duplicates or omissions. Validates: Requirements 5.4, 5.9.
    [Property(MaxTest = 200)]
    public Property Property11_PaginationConsistency()
    {
        // A deduped-by-code dataset, a valid search term (or none), a valid page and pageSize.
        var arb = Arb.From(
            from dataset in NationalityGenerators.DatasetGen()
            from search in Gen.Frequency(
                Tuple.Create(2, Gen.Constant<string?>(null)),
                Tuple.Create(1, NationalityGenerators.NameGen().Select(s => (string?)s)))
            from page in Gen.Choose(1, 5)
            from pageSize in Gen.Choose(1, 20)
            select (dataset, search, page, pageSize));

        return Prop.ForAll(arb, tuple =>
        {
            var (dataset, search, page, pageSize) = tuple;

            var repo = new InMemoryNationalityRepository(dataset);
            var handler = new GetNationalitiesPagedQueryHandler(repo);

            var response = handler.Handle(
                new GetNationalitiesPagedQuery(search, page, pageSize),
                CancellationToken.None).GetAwaiter().GetResult();

            // The full filtered, ordered set the repository would page over. Recomputed here with
            // the same filter + ordering the in-memory repo (and the real EF repo) applies so we
            // can assert Total, page windows, and the reassembled sequence against a ground truth.
            var filteredOrdered = dataset
                .Where(x =>
                    string.IsNullOrWhiteSpace(search) ||
                    x.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .ToList();

            var expectedTotal = filteredOrdered.Count;
            var expectedTotalPages = (int)Math.Ceiling(expectedTotal / (double)pageSize);

            // Total equals the full filtered count.
            var totalOk = response.Total == expectedTotal;

            // TotalPages = ceil(Total / pageSize).
            var totalPagesOk = response.TotalPages == expectedTotalPages;

            // Reported Page/PageSize echo the request.
            var echoOk = response.Page == page && response.PageSize == pageSize;

            // At most pageSize items returned.
            var windowOk = response.Data.Count <= pageSize;

            // The returned page equals exactly the corresponding slice of the ordered ground truth.
            var expectedSlice = filteredOrdered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Id)
                .ToList();
            var sliceOk = response.Data.Select(r => r.Id).SequenceEqual(expectedSlice);

            // A page beyond the last returns an empty item collection while still reporting the
            // correct Total, Page, and PageSize.
            var beyondPage = expectedTotalPages + 1;
            var beyond = handler.Handle(
                new GetNationalitiesPagedQuery(search, beyondPage, pageSize),
                CancellationToken.None).GetAwaiter().GetResult();
            var beyondOk =
                beyond.Data.Count == 0 &&
                beyond.Total == expectedTotal &&
                beyond.Page == beyondPage &&
                beyond.PageSize == pageSize;

            // Concatenating all pages in order reproduces the full ordered set with no duplicates
            // or omissions.
            var reassembled = new List<Guid>();
            for (var p = 1; p <= expectedTotalPages; p++)
            {
                var pageResponse = handler.Handle(
                    new GetNationalitiesPagedQuery(search, p, pageSize),
                    CancellationToken.None).GetAwaiter().GetResult();
                reassembled.AddRange(pageResponse.Data.Select(r => r.Id));
            }
            var reassembledOk =
                reassembled.SequenceEqual(filteredOrdered.Select(x => x.Id)) &&
                reassembled.Distinct().Count() == reassembled.Count;

            return (totalOk && totalPagesOk && echoOk && windowOk && sliceOk && beyondOk && reassembledOk)
                .Label(
                    $"total={response.Total}(exp {expectedTotal}) totalPages={response.TotalPages}" +
                    $"(exp {expectedTotalPages}) page={page} size={pageSize} returned={response.Data.Count} " +
                    $"beyondEmpty={beyond.Data.Count == 0} reassembled={reassembled.Count}");
        });
    }
}
