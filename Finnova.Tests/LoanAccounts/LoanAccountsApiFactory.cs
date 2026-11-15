using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Finnova.Repository.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountsApiFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.LoanAccountsController>
{
    private readonly string _dbName = "loan-accounts-api-" + Guid.NewGuid();
    private static string Key => Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY")
        ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set for integration tests.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Finnova",
            ["Jwt:Audience"] = "FinnovaTests",
            ["Jwt:SigningKey"] = Key,
            ["ApplicationProcessing:AuthorizationPolicy"] = "ApplicationProcessor",
            ["ApplicationProcessing:LookupModule"] = "ApplicationProcessing",
            ["ApplicationProcessing:ProductLookupType"] = "PRODUCT",
            ["ApplicationProcessing:SchemeLookupType"] = "SCHEME"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<FinnovaDbContext>>();
            services.AddDbContext<FinnovaDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    public void Seed(Action<FinnovaDbContext> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        action(db);
        db.SaveChanges();
    }

    public HttpClient ClientFor(Guid? userId, params string[] roles)
    {
        var client = CreateClient();
        if (userId is null) return client;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.Value.ToString()), new(ClaimTypes.Name, "User " + userId.Value.ToString("N")[..6]) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
