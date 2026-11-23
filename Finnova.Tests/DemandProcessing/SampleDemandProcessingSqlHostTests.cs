using Finnova.Data.Seeder.DemandSample;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Tests.DemandProcessing.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDemandProcessingSqlHostTests : IAsyncLifetime
{
    private readonly SampleAccountsFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync() { await _factory.InitializeDatabaseAsync(); _client = _factory.CreateClient(); }
    public async Task DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }

    private static Guid LobId => SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey);

    [Fact]
    public async Task AuthenticatedMarchFlowUsesAllSampleBranchesAndTotals()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.Token(allBranches: true));
        var response = await _client.PostAsJsonAsync("/api/demand-processing/runs", new { lineOfBusinessId = LobId, periodYear = 2026, periodMonth = 3, frequency = "Monthly", idempotencyKey = "sample-march" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await _factory.WaitForCompletionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var run = await db.DemandRuns.SingleAsync(x => x.LineOfBusinessId == LobId && x.PeriodYear == 2026 && x.PeriodMonth == 3);
        Assert.Equal(7, run.RowCount);
        Assert.Equal(8, run.BatchCount);
        Assert.Equal(48500m, run.CurrentDueTotal);
        Assert.Equal(18000m, run.ArrearsTotal);
        Assert.Equal(0m, run.TotalCharges);
        Assert.Equal(66500m, run.TotalDemand);
        Assert.Equal(1, await db.DemandBatches.CountAsync(x => x.DemandRunId == run.Id && x.Status == DemandBatchStatus.NoDemand));
    }

    [Fact]
    public async Task BranchScopedRunDoesNotReadOtherSampleBranches()
    {
        var branch = SampleDataCatalog.StableId("Location", "CHN-B01");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.Token(allBranches: false, branch: branch));
        var response = await _client.PostAsJsonAsync("/api/demand-processing/runs", new { lineOfBusinessId = LobId, branchId = branch, periodYear = 2026, periodMonth = 3, frequency = "Monthly", idempotencyKey = "sample-branch" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await _factory.WaitForCompletionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var run = await db.DemandRuns.SingleAsync(x => x.LineOfBusinessId == LobId && x.PeriodYear == 2026 && x.PeriodMonth == 3);
        var revisionIds = await db.DemandRunRevisions.Where(x => x.DemandRunId == run.Id).Select(x => x.Id).ToListAsync();
        var rows = await db.DemandRows.Where(x => revisionIds.Contains(x.DemandRunRevisionId)).ToListAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(branch, row.BranchId));
    }

    [Fact]
    public async Task MissingTokenRemainsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/demand-processing/runs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public sealed class SampleAccountsFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.DemandProcessingController>
    {
        private readonly string _connection;
        private readonly string _jwtKey;

        public SampleAccountsFactory()
        {
            _jwtKey = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY") ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY is required.");
            var baseConnection = Environment.GetEnvironmentVariable("FINNOVA_SAMPLE_SQL_CONNECTION") ?? throw new InvalidOperationException("FINNOVA_SAMPLE_SQL_CONNECTION is required.");
            _connection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"FinnovaHostSample_{Guid.NewGuid():N}" }.ConnectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FinnovaConnection"] = _connection,
                ["Jwt:Issuer"] = "Finnova",
                ["Jwt:Audience"] = "FinnovaTests",
                ["Jwt:SigningKey"] = _jwtKey,
                ["DemandProcessing:BusinessTimezone"] = "Asia/Kolkata"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<FinnovaDbContext>>();
                services.AddDbContext<FinnovaDbContext>(o => o.UseSqlServer(_connection));

                // Test-only adapters. Production registrations are never changed outside this factory.
                services.AddScoped<SampleOwnershipStore>();
                services.RemoveAll<IAccountSourcePort>(); services.AddScoped<IAccountSourcePort, SampleAccountSourcePort>();
                services.RemoveAll<IFinalizedSchedulePort>(); services.AddScoped<IFinalizedSchedulePort, SampleFinalizedSchedulePort>();
                services.RemoveAll<IAllocationEventPort>(); services.AddScoped<IAllocationEventPort, SampleAllocationEventPort>();
                services.RemoveAll<IPreviousDemandPort>(); services.AddScoped<IPreviousDemandPort, SamplePreviousDemandPort>();
            });
        }

        public async Task InitializeDatabaseAsync()
        {
            await using var db = new FinnovaDbContext(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(_connection).Options);
            await db.Database.MigrateAsync();
            await new SampleDataSeeder(db, new SampleDataOptions(), new SampleOwnershipStore(db), NullLogger<SampleDataSeeder>.Instance).SeedAsync();
        }

        public string Token(bool allBranches, Guid? branch = null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "sample-test"), new(ClaimTypes.Role, "SystemAdmin"), new("lob", LobId.ToString()) };
            if (allBranches) foreach (var item in SampleDataCatalog.Branches) claims.Add(new Claim("branch", SampleDataCatalog.StableId("Location", item.Code).ToString()));
            else if (branch.HasValue) claims.Add(new Claim("branch", branch.Value.ToString()));
            var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task WaitForCompletionAsync()
        {
            for (var i = 0; i < 50; i++)
            {
                await Task.Delay(100);
                await using var scope = Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
                if (await db.DemandRuns.AnyAsync(x => x.PeriodYear == 2026 && x.PeriodMonth == 3 && x.Status == DemandRunStatus.Completed)) return;
            }
            throw new TimeoutException("The sample host run did not complete.");
        }

        private static Guid LobId => SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using var db = new FinnovaDbContext(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(_connection).Options);
                db.Database.EnsureDeleted();
            }
            base.Dispose(disposing);
        }
    }
}