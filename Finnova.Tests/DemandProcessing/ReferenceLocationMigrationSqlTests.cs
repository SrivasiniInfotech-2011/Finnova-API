using Finnova.Data.Seeder.DemandSample;
using Finnova.Repository.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class ReferenceLocationMigrationSqlTests
{
    private static readonly string[] CityCodes = ["CBE", "CHN", "MDU", "SLM", "TNJ", "TRI"];

    private static string ConnectionString()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FINNOVA_SAMPLE_SQL_CONNECTION") ?? throw new InvalidOperationException("FINNOVA_SAMPLE_SQL_CONNECTION must point to a SQL Server instance.");
        return new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"FinnovaRef_{Guid.NewGuid():N}" }.ConnectionString;
    }

    private static FinnovaDbContext Context(string connection) => new(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(connection).Options);

    [Fact]
    public async Task MigrationCreatesTamilNaduAndSixCities()
    {
        var connection = ConnectionString();
        try
        {
            await using var db = Context(connection);
            await db.Database.MigrateAsync();
            var tn = await db.Locations.SingleAsync(x => x.Code == "TN");
            Assert.Equal(2, tn.Level);
            Assert.Equal(SampleDataCatalog.IndiaId, tn.ParentId);
            var cities = await db.Locations.Where(x => x.ParentId == tn.Id).OrderBy(x => x.Code).ToListAsync();
            Assert.Equal(CityCodes, cities.Select(x => x.Code).ToArray());
            Assert.All(cities, c => { Assert.Equal(3, c.Level); Assert.True(c.IsActive); });
        }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task MigrationReusesAnExistingStateWithADifferentId()
    {
        var connection = ConnectionString();
        try
        {
            await using var db = Context(connection);
            var all = db.Database.GetMigrations().ToList();
            Assert.EndsWith("SeedTamilNaduReferenceLocations", all[^1]);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(all[^2]);
            var adminId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [locations] ([Id],[Code],[Name],[Level],[ParentId],[IsActive],[CreatedAt],[UpdatedAt]) VALUES ({adminId}, N'TN', N'Tamil Nadu (admin)', 2, {SampleDataCatalog.IndiaId}, 1, SYSUTCDATETIME(), SYSUTCDATETIME())");

            await migrator.MigrateAsync(all[^1]);

            var states = await db.Locations.Where(x => x.Code == "TN").ToListAsync();
            var tn = Assert.Single(states);
            Assert.Equal(adminId, tn.Id);
            Assert.Equal(6, await db.Locations.CountAsync(x => x.ParentId == adminId));
        }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task SeedFailsBeforeAnyWriteWhenAReferenceCityIsMissing()
    {
        var connection = ConnectionString();
        try
        {
            await using (var setup = Context(connection))
            {
                await setup.Database.MigrateAsync();
                await setup.Database.ExecuteSqlRawAsync("DELETE FROM [locations] WHERE [Code] = N'TNJ'");
            }
            await using (var db = Context(connection))
            {
                var seeder = new SampleDataSeeder(db, new SampleDataOptions(), new SampleOwnershipStore(db), NullLogger<SampleDataSeeder>.Instance);
                var ex = await Assert.ThrowsAsync<SampleDataValidationException>(() => seeder.SeedAsync());
                Assert.Contains("TNJ", ex.Message);
                Assert.Contains("migration", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            await using var verify = Context(connection);
            Assert.False(await verify.Organizations.AnyAsync(x => x.Code == SampleDataCatalog.OrganizationCode));
            Assert.False(await verify.LookupValues.AnyAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType));
        }
        finally { await using var cleanup = Context(connection); await cleanup.Database.EnsureDeletedAsync(); }
    }
}