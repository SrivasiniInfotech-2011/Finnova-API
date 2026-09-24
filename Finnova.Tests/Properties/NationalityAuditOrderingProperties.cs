using FsCheck;
using FsCheck.Xunit;
using Finnova.Service.Nationality.Queries.GetNationalityAuditTrail;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for the audit-trail read path (Property 9: Audit trail ordering is
/// deterministic). Drives the real <see cref="GetNationalityAuditTrailQueryHandler"/> over the
/// hand-written in-memory audit repository (whose GetByNationalityIdAsync mirrors the EF-backed
/// repo: filter by NationalityId, order by ChangedAtUtc descending then Id descending, empty list
/// for an unknown id). Uses the shared <see cref="NationalityGenerators.AuditEntrySetGen"/>
/// generator, which deliberately includes the empty set and colliding timestamps so the Id-desc
/// tie-break is exercised. Kept in its own class file to avoid conflicts with other concurrently-
/// authored nationality property suites. FsCheck.Xunit v2, MaxTest = 200 (>= the required 100
/// iterations).
/// </summary>
public class NationalityAuditOrderingProperties
{
    // Feature: nationality-master-management, Property 9: Audit trail ordering is deterministic —
    // for any set of audit entries recorded for a nationality (including the empty set), the
    // audit-trail query returns them ordered by ChangedAtUtc descending and, for entries sharing a
    // timestamp, by Id descending; repeated queries over identical data return the same sequence,
    // and a nationality id with no entries yields an empty result.
    // Validates: Requirements 4.4, 4.5.
    [Property(MaxTest = 200)]
    public Property Property9_AuditTrailOrderingIsDeterministic()
    {
        var arb = Arb.From(NationalityGenerators.AuditEntrySetGen());

        return Prop.ForAll(arb, tuple =>
        {
            var (nationalityId, entries) = tuple;

            var audit = new InMemoryNationalityAuditRepository(entries);
            var handler = new GetNationalityAuditTrailQueryHandler(audit);

            var result = handler.Handle(
                new GetNationalityAuditTrailQuery(nationalityId),
                CancellationToken.None).GetAwaiter().GetResult();

            // Ground truth: the generated entries for this nationality, ordered ChangedAtUtc desc
            // then Id desc (the same ordering the repo/query is contracted to apply, R4.4).
            var expected = entries
                .OrderByDescending(e => e.ChangedAtUtc)
                .ThenByDescending(e => e.Id)
                .Select(e => e.Id)
                .ToList();

            // Every returned entry belongs to the queried nationality.
            var allBelong = result.All(r => r.NationalityId == nationalityId);

            // Same membership and order as the ground truth (R4.4).
            var orderOk = result.Select(r => r.Id).SequenceEqual(expected);

            // The order is a true non-increasing sequence on (ChangedAtUtc, Id): each adjacent
            // pair is either strictly newer, or same timestamp with a strictly larger Id.
            var pairwiseOk = true;
            for (var i = 1; i < result.Count; i++)
            {
                var prev = result[i - 1];
                var cur = result[i];
                var ordered =
                    prev.ChangedAtUtc > cur.ChangedAtUtc ||
                    (prev.ChangedAtUtc == cur.ChangedAtUtc && prev.Id.CompareTo(cur.Id) > 0);
                if (!ordered)
                {
                    pairwiseOk = false;
                    break;
                }
            }

            // Repeated queries over identical data return the same sequence (determinism, R4.4).
            var again = handler.Handle(
                new GetNationalityAuditTrailQuery(nationalityId),
                CancellationToken.None).GetAwaiter().GetResult();
            var deterministicOk = again.Select(r => r.Id).SequenceEqual(result.Select(r => r.Id));

            // The empty set yields an empty result (R4.5); querying an unknown id also yields [].
            var emptyOk = entries.Count != 0 || result.Count == 0;

            var unknownId = Guid.NewGuid();
            while (unknownId == nationalityId)
                unknownId = Guid.NewGuid();
            var unknown = handler.Handle(
                new GetNationalityAuditTrailQuery(unknownId),
                CancellationToken.None).GetAwaiter().GetResult();
            var unknownEmptyOk = unknown.Count == 0;

            return (allBelong && orderOk && pairwiseOk && deterministicOk && emptyOk && unknownEmptyOk)
                .Label(
                    $"entries={entries.Count} returned={result.Count} orderOk={orderOk} " +
                    $"pairwiseOk={pairwiseOk} deterministicOk={deterministicOk} " +
                    $"emptyOk={emptyOk} unknownEmptyOk={unknownEmptyOk}");
        });
    }
}
