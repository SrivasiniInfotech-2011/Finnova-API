using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Finnova.Repository.Context;

namespace Finnova.Tests.Integration;

/// <summary>
/// Boots the <c>Finnova.AccountsService</c> host in-process for GL integration tests. The
/// entry-point type argument is <see cref="Finnova.AccountsService.Controllers.GLController"/>
/// (any public type in the AccountsService assembly) so the factory binds to the AccountsService
/// host and NOT the SystemAdminService host — both hosts declare a global <c>Program</c>, so the
/// bare <c>WebApplicationFactory&lt;Program&gt;</c> is ambiguous and would otherwise load the
/// wrong host (which does not register the GL dependency ports).
///
/// The SQL Server-backed <see cref="FinnovaDbContext"/> is replaced with the EF Core InMemory
/// provider so the JWT scheme and the GL authorization policies can be exercised without a real
/// database (mirrors <see cref="SystemAdminAppFactory"/>).
/// </summary>
public class GLAppFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.GLController>
{
    private readonly string _databaseName = "GLIntegrationTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveEfRegistrations(services);

            services.AddDbContext<FinnovaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            db.Database.EnsureCreated();
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
}
