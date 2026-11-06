using Finnova.Repository.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finnova.Tests.ApplicationProcessing;

public sealed class ApplicationProcessingApiFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.ApplicationProcessingController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var key = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set for integration tests.");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Finnova",
            ["Jwt:Audience"] = "FinnovaTests",
            ["Jwt:SigningKey"] = key,
            ["ApplicationProcessing:AuthorizationPolicy"] = "ApplicationProcessor",
            ["ApplicationProcessing:LookupModule"] = "ApplicationProcessing",
            ["ApplicationProcessing:ProductLookupType"] = "PRODUCT",
            ["ApplicationProcessing:SchemeLookupType"] = "SCHEME"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<FinnovaDbContext>>();
            services.AddDbContext<FinnovaDbContext>(o => o.UseInMemoryDatabase("application-processing-tests"));
        });
    }
}
