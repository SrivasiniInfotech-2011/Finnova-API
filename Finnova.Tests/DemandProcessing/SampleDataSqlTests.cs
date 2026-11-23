using Finnova.Data.Seeder.DemandSample;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Context;
using Finnova.Tests.DemandProcessing.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataSqlTests
{
    private static string ConnectionString()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FINNOVA_SAMPLE_SQL_CONNECTION") ?? throw new InvalidOperationException("FINNOVA_SAMPLE_SQL_CONNECTION must point to a SQL Server instance."); var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"FinnovaSample_{Guid.NewGuid():N}" }; return builder.ConnectionString;
    }

    private static FinnovaDbContext Context(string connection) => new(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(connection).Options);
    private static SampleDataOptions Options() => new();
    private static async Task<string> MigrateAndSeedAsync(string connection, CancellationToken ct)
    { await using var db = Context(connection); await db.Database.MigrateAsync(ct); var seed = new SampleDataSeeder(db, Options(), new SampleOwnershipStore(db), NullLogger<SampleDataSeeder>.Instance); return await seed.SeedAsync(ct); }

    [Fact]
    public async Task SeedIsRepeatableAndRepairsOwnedRows()
    {
        var connection = ConnectionString(); try { var first = await MigrateAndSeedAsync(connection, CancellationToken.None); await using (var changed = Context(connection)) { var row = await changed.Locations.SingleAsync(x => x.Code == "CHN-B01"); row.Name = "Changed by test"; await changed.SaveChangesAsync(); } var second = await MigrateAndSeedAsync(connection, CancellationToken.None); Assert.Equal(first, second); await using var verify = Context(connection); Assert.Equal("Chennai Central Branch", (await verify.Locations.SingleAsync(x => x.Code == "CHN-B01")).Name); Assert.Equal(8, await verify.DemandRunBranchScopes.CountAsync(x => x.DemandRunId == SampleDataCatalog.StableId("DemandRun", "2026-02"))); }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task HistoricalGraphsHaveExactLinksAndAggregates()
    {
        var connection = ConnectionString(); try { await MigrateAndSeedAsync(connection, CancellationToken.None); await using var db = Context(connection); foreach (var period in SampleDataCatalog.HistoryPeriods) { var run = await db.DemandRuns.SingleAsync(x => x.Id == SampleDataCatalog.StableId("DemandRun", period.Key)); Assert.Equal(8, await db.DemandRunBranchScopes.CountAsync(x => x.DemandRunId == run.Id)); Assert.Equal(7, await db.DemandRows.CountAsync(x => x.DemandSnapshotId == run.DemandSnapshotId)); Assert.Equal(8, await db.DemandBatches.CountAsync(x => x.DemandRunId == run.Id)); Assert.Equal(1, await db.DemandBatches.CountAsync(x => x.DemandRunId == run.Id && x.Status == Finnova.Models.Domain.Enums.DemandBatchStatus.NoDemand)); var billing = await db.BillingSnapshots.SingleAsync(x => x.Id == run.BillingSnapshotId); var demand = await db.DemandSnapshots.SingleAsync(x => x.Id == run.DemandSnapshotId); Assert.True(billing.IsPublished); Assert.True(demand.IsPublished); Assert.Equal(billing.BillingStageRunId, (await db.BillingStageRuns.SingleAsync(x => x.Id == billing.BillingStageRunId)).Id); Assert.Equal(billing.Id, demand.BillingSnapshotId); } }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task PreviousDemandRequiresExactPublishedTuple()
    {
        var connection = ConnectionString(); try { await MigrateAndSeedAsync(connection, CancellationToken.None); await using var db = Context(connection); var lob = SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey); var branches = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).ToArray(); var port = new SamplePreviousDemandPort(db, new SampleOwnershipStore(db)); var exact = DemandScope.Create(lob, branches); var result = await port.GetAsync(SampleDataCatalog.StableId("LoanAccount", "LN-DP-0002"), exact, DemandPeriod.Create(2026, 3), SampleDataCatalog.SnapshotReference, CancellationToken.None); Assert.NotNull(result); Assert.Equal(12000m, result!.Arrears); Assert.Null(await port.GetAsync(SampleDataCatalog.StableId("LoanAccount", "LN-DP-0002"), exact, DemandPeriod.Create(2026, 3), "wrong-marker", CancellationToken.None)); Assert.Null(await port.GetAsync(SampleDataCatalog.StableId("LoanAccount", "LN-DP-0002"), DemandScope.Create(lob, branches[..7]), DemandPeriod.Create(2026, 3), SampleDataCatalog.SnapshotReference, CancellationToken.None)); }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ReferenceLocationsStayUnownedAndUnownedCollisionRollsBack()
    {
        var connection = ConnectionString();
        try
        {
            await MigrateAndSeedAsync(connection, CancellationToken.None);
            await using (var db = Context(connection))
            {
                Assert.False(await db.LookupValues.AnyAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && (x.Code == "Location:TN" || x.Code == "Location:CHN")));
                var ownership = await db.LookupValues.SingleAsync(x => x.Code == "Organization:" + SampleDataCatalog.OrganizationCode);
                db.LookupValues.Remove(ownership);
                await db.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<SampleDataCollisionException>(() => MigrateAndSeedAsync(connection, CancellationToken.None));
        }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }
}