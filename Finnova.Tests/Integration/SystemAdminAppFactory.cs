using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;

namespace Finnova.Tests.Integration;

/// <summary>
/// Boots the <c>Finnova.SystemAdminService</c> host in-process for integration tests, replacing
/// the SQL Server-backed <see cref="FinnovaDbContext"/> with the EF Core InMemory provider so the
/// JWT scheme and the SystemAdmin authorization policy can be exercised WITHOUT a real database.
///
/// A fresh, uniquely-named in-memory database is used per factory instance so tests do not leak
/// state into each other, and a small deterministic seed (a system-locked SYS_TXN_TYPE row and a
/// MARITAL_STATUS row) is applied so create-then-list scenarios work.
/// </summary>
public class SystemAdminAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "SystemAdminIntegrationTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server DbContext wiring registered by AddFinnovaRepository. Beyond the
            // DbContextOptions<FinnovaDbContext> and the FinnovaDbContext itself, EF also registers
            // the SQL Server provider services and an options-configuration descriptor; leaving any
            // of them in place makes EF see two providers ("Only a single database provider can be
            // registered"). Strip every EF-related descriptor for this context before re-adding.
            RemoveEfRegistrations(services);

            // Re-add FinnovaDbContext on the EF Core InMemory provider.
            services.AddDbContext<FinnovaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Build the model + apply the entity HasData seed, then add test rows.
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            db.Database.EnsureCreated();
            SeedTestData(db);
        });
    }

    private static void RemoveEfRegistrations(IServiceCollection services)
    {
        var toRemove = services.Where(d =>
            d.ServiceType == typeof(FinnovaDbContext) ||
            d.ServiceType == typeof(DbContextOptions<FinnovaDbContext>) ||
            d.ServiceType == typeof(DbContextOptions) ||
            (d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
            .ToList();

        foreach (var d in toRemove)
        {
            services.Remove(d);
        }
    }

    /// <summary>
    /// Seeds a couple of rows required by the tests. Idempotent: only inserts rows not already
    /// present (the entity's HasData seed may already provide equivalents when the model is built).
    /// </summary>
    private static void SeedTestData(FinnovaDbContext db)
    {
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        void EnsureRow(string module, string type, string code, string value,
            int order, bool active, bool locked)
        {
            var exists = db.LookupValues.Any(x =>
                x.Module == module && x.LookupType == type && x.Code == code);
            if (exists) return;

            db.LookupValues.Add(new LookupValue
            {
                Id = Guid.NewGuid(),
                Module = module,
                LookupType = type,
                Code = code,
                Value = value,
                DisplayOrder = order,
                IsActive = active,
                IsSystemLocked = locked,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
            });
        }

        // A system-locked row (R3 protection surface).
        EnsureRow("SystemAdmin", "SYS_TXN_TYPE", "DEBIT", "Debit", 1, active: true, locked: true);
        // A MARITAL_STATUS row so dropdown/list-by-scope has data to return.
        EnsureRow("Origination", "MARITAL_STATUS", "SIN", "Single", 1, active: true, locked: false);

        db.SaveChanges();
    }
}
