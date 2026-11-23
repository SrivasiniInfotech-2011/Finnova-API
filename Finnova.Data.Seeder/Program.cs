using Finnova.Data.Seeder.DemandSample;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Finnova.Data.Seeder;

internal static class DataToolProgram
{
    private const string Usage = "Usage: Finnova.Data migrate | seed demand-sample | verify demand-sample";

    private static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        try
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
            var valid = (command == "migrate" && args.Length == 1)
                || (command is "seed" or "verify" && args.Length == 2 && string.Equals(args[1], "demand-sample", StringComparison.OrdinalIgnoreCase));
            if (!valid) { Console.Error.WriteLine(Usage); return 2; }

            // Guard first: nothing below this line runs outside Development or Testing.
            var environment = SampleDataEnvironmentGuard.Require(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            var connection = configuration.GetConnectionString("FinnovaConnection");
            if (string.IsNullOrWhiteSpace(connection)) { Console.Error.WriteLine("ConnectionStrings:FinnovaConnection is not configured."); return 2; }

            using var loggers = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
            await using var db = new FinnovaDbContext(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(connection).Options);

            switch (command)
            {
                case "migrate":
                    await db.Database.MigrateAsync(cts.Token);
                    Console.WriteLine("Migrations applied.");
                    return 0;
                case "seed":
                    var seeder = new SampleDataSeeder(db, new SampleDataOptions(), new SampleOwnershipStore(db), loggers.CreateLogger<SampleDataSeeder>());
                    Console.WriteLine($"Seed committed. Fingerprint {await seeder.SeedAsync(cts.Token)}");
                    return 0;
                default:
                    var problems = await new SampleDataVerifier(db).VerifyAsync(cts.Token);
                    foreach (var problem in problems) Console.Error.WriteLine(problem);
                    if (problems.Count == 0) Console.WriteLine("Sample data verified.");
                    return problems.Count == 0 ? 0 : 1;
            }
        }
        catch (SampleDataEnvironmentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        catch (Exception ex) when (ex is SampleDataValidationException or SampleDataCollisionException) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled."); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}"); return 1; }
    }
}