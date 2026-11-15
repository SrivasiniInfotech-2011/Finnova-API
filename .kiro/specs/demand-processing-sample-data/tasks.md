# Copy-paste implementation tasks — Demand Processing sample data

Apply these tasks only after approval. This file is the implementation payload; no code in these blocks was applied while authoring this specification. Do not add a fixture migration. Apply the repository's existing migrations only in the isolated Development/Testing database used by the later tests.

## Task 1 — Add fixture options and private-setter helpers

### MODIFY `Finnova.Service/DemandProcessing/Configuration/DemandProcessingOptions.cs`

Add these properties inside `DemandProcessingOptions`, after `BusinessTimezone`, without changing existing properties:

```csharp
public bool UseSampleAdapters { get; set; }
public bool SeedSampleDataOnStartup { get; set; }
public string SampleDataKey { get; set; } = "SAMPLE-DP-V1";
public DateOnly SampleAsOfDate { get; set; } = new(2026, 3, 31);
```

### MODIFY `Finnova.Models/Domain/Entities/Customer.cs`

Add these members after `SetGeneratedCode`:

```csharp
public static Customer CreateWithId(Guid id)
{
    if (id == Guid.Empty) throw new ArgumentException("Customer id is required.", nameof(id));
    return new Customer { Id = id };
}

public void SetFixtureTimestamps(DateTime createdAtUtc, DateTime updatedAtUtc)
{
    CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
}
```

### MODIFY `Finnova.Models/Domain/Entities/CustomerAddress.cs`

Add these members inside `CustomerAddress`:

```csharp
public static CustomerAddress CreateWithId(Guid id)
{
    if (id == Guid.Empty) throw new ArgumentException("Customer address id is required.", nameof(id));
    return new CustomerAddress { Id = id };
}

public void SetFixtureIdentity(Guid customerId) => CustomerId = customerId;

public void SetFixtureTimestamps(DateTime createdAtUtc, DateTime updatedAtUtc)
{
    CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
}
```

### MODIFY `Finnova.Models/Domain/Entities/IndividualCustomerDetails.cs`

Add these members inside `IndividualCustomerDetails`:

```csharp
public static IndividualCustomerDetails CreateWithId(Guid id)
{
    if (id == Guid.Empty) throw new ArgumentException("Individual details id is required.", nameof(id));
    return new IndividualCustomerDetails { Id = id };
}

public void SetFixtureIdentity(Guid customerId) => CustomerId = customerId;

public void SetFixtureTimestamps(DateTime createdAtUtc, DateTime updatedAtUtc)
{
    CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
}
```

## Task 2 — Create the immutable catalog and preflight validator

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleDataCatalog.cs`

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed record SamplePeriod(int Year, int Month)
{
    public string Key => $"{Year:0000}-{Month:00}";
    public DateOnly DueDate => new(Year, Month, 15);
}

public sealed record SampleLocation(string Code, string Name, int Level, string ParentCode);
public sealed record SampleBranch(string Code, string Name, string CityCode, string ZoneCode);
public sealed record SampleCustomer(string Key, string Code, string FirstName, string LastName, string City, string PostalCode);
public sealed record SampleLoan(string Key, string AccountNumber, string CustomerKey, string ApplicationNumber, string BranchCode, decimal Emi, decimal FinanceAmount, int Tenure);
public sealed record SampleReceipt(string Key, string LoanKey, SamplePeriod Period, string Kind, decimal Amount, DateTime TransactionDateUtc, string Reference, bool IsDeposit = true, bool IsCompleted = true, string? Dataset = null, string? DescriptionOverride = null, decimal? PersistedAmount = null)
{
    public decimal EntityAmount => PersistedAmount ?? Amount;
    public string Description => DescriptionOverride ?? SampleDataCatalog.ReceiptDescription(this);
}

public static class SampleDataCatalog
{
    public const string DatasetKey = "SAMPLE-DP-V1";
    public const string OrganizationCode = "SMPD";
    public const string LobKey = "SAMPLE-DP-LENDING";
    public const string Frequency = "Monthly";
    public const string SnapshotReference = "SAMPLE-DP|V1|ASOF|2026-03-31";
    public const string OwnershipModule = "DemandProcessing";
    public const string OwnershipType = "SAMPLE_OWNERSHIP";
    public static readonly Guid IndiaId = new("00000000-0000-0000-0000-000000000001");
    public static readonly DateOnly AsOfDate = new(2026, 3, 31);
    public static readonly DateTime SeedTimestampUtc = new(2026, 3, 31, 18, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime ManifestTimestampUtc = new(2026, 3, 31, 18, 31, 0, DateTimeKind.Utc);
    public static readonly IReadOnlyList<SamplePeriod> Periods =
        [new(2025, 10), new(2025, 11), new(2025, 12), new(2026, 1), new(2026, 2), new(2026, 3)];
    public static IReadOnlyList<SamplePeriod> HistoryPeriods => Periods.Take(5).ToArray();
    public static readonly IReadOnlyList<SampleLocation> Locations =
    [
        new("TN", "Tamil Nadu", 2, "IND"),
        new("CHN", "Chennai", 3, "TN"), new("CBE", "Coimbatore", 3, "TN"), new("MDU", "Madurai", 3, "TN"),
        new("SLM", "Salem", 3, "TN"), new("TRI", "Tiruchirappalli", 3, "TN"), new("TNJ", "Thanjavur", 3, "TN"),
        new("CHN-Z01", "Chennai Central Zone", 4, "CHN"), new("CBE-Z01", "Coimbatore Zone", 4, "CBE"),
        new("MDU-Z01", "Madurai Zone", 4, "MDU"), new("SLM-Z01", "Salem Zone", 4, "SLM"),
        new("TRI-Z01", "Tiruchirappalli Zone", 4, "TRI"), new("TNJ-Z01", "Thanjavur Zone", 4, "TNJ")
    ];
    public static readonly IReadOnlyList<SampleBranch> Branches =
    [
        new("CHN-B01", "Chennai Central Branch", "CHN", "CHN-Z01"), new("CHN-B02", "Tambaram Branch", "CHN", "CHN-Z01"),
        new("CBE-B01", "Coimbatore Main Branch", "CBE", "CBE-Z01"), new("MDU-B01", "Madurai Main Branch", "MDU", "MDU-Z01"),
        new("MDU-B02", "Dindigul Service Branch", "MDU", "MDU-Z01"), new("SLM-B01", "Salem Main Branch", "SLM", "SLM-Z01"),
        new("TRI-B01", "Tiruchirappalli Main Branch", "TRI", "TRI-Z01"), new("TNJ-B01", "Thanjavur Main Branch", "TNJ", "TNJ-Z01")
    ];
    public static readonly IReadOnlyList<SampleCustomer> Customers =
    [
        new("C001", "CUSTDP000001", "Arun", "Kumar", "Chennai", "600001"), new("C002", "CUSTDP000002", "Meena", "Sundaram", "Chennai", "600045"),
        new("C003", "CUSTDP000003", "Ravi", "Subramanian", "Coimbatore", "641001"), new("C004", "CUSTDP000004", "Kavitha", "Raman", "Madurai", "625001"),
        new("C005", "CUSTDP000005", "Prakash", "Natarajan", "Salem", "636001"), new("C006", "CUSTDP000006", "Divya", "Mohan", "Tiruchirappalli", "620001"),
        new("C007", "CUSTDP000007", "Suresh", "Balaji", "Thanjavur", "613001"), new("C008", "CUSTDP000008", "Anitha", "Rajendran", "Chennai", "600028")
    ];
    public static readonly IReadOnlyList<SampleLoan> Loans =
    [
        new("A001", "LN-DP-0001", "C001", "APP-DP-0001", "CHN-B01", 10000m, 500000m, 60),
        new("A002", "LN-DP-0002", "C002", "APP-DP-0002", "CHN-B02", 12000m, 600000m, 60),
        new("A003", "LN-DP-0003", "C003", "APP-DP-0003", "CBE-B01", 15000m, 750000m, 60),
        new("A004", "LN-DP-0004", "C004", "APP-DP-0004", "MDU-B01", 8000m, 400000m, 60),
        new("A005", "LN-DP-0005", "C005", "APP-DP-0005", "SLM-B01", 11000m, 550000m, 60),
        new("A006", "LN-DP-0006", "C006", "APP-DP-0006", "TRI-B01", 9000m, 450000m, 60),
        new("A007", "LN-DP-0007", "C007", "APP-DP-0007", "TNJ-B01", 13500m, 675000m, 60)
    ];
    public static readonly IReadOnlyList<(string Type, string Code, string Value)> LookupRows =
    [
        ("CUSTOMER_TYPE", "INDIVIDUAL", "Individual"), ("TITLE", "MR", "Mr"), ("MARITAL_STATUS", "MARRIED", "Married"),
        ("ACCOUNT_STATUS", "ACTIVE", "Active account"), ("RECEIPT_KIND", "FULL", "Full installment receipt"),
        ("RECEIPT_KIND", "PART", "Part installment receipt"), ("RECEIPT_KIND", "OVER", "Overpayment receipt"),
        ("RECEIPT_KIND", "NONE", "No receipt"), ("DEMAND_SCENARIO", "SAMPLE_DP", "Demand Processing sample data"),
        ("BRANCH_REGION", "TN", "Tamil Nadu")
    ];

    public static IReadOnlyList<SampleReceipt> Receipts { get; } = BuildReceipts();

    public static Guid StableId(string entityType, string logicalKey) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{DatasetKey}|{entityType}|{logicalKey}"))[..16]);
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    public static TimeZoneInfo BusinessTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
    }

    public static DateTime CutoffUtc(DateOnly businessDate)
    {
        var localEnd = DateTime.SpecifyKind(businessDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localEnd, BusinessTimeZone());
    }

    public static DateTime PeriodCutoffUtc(SamplePeriod period)
    {
        var date = period.Year == AsOfDate.Year && period.Month == AsOfDate.Month
            ? AsOfDate : new DateOnly(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month));
        return CutoffUtc(date);
    }

    public static string ReceiptDescription(SampleReceipt receipt) =>
        $"SAMPLE-DP|V1|{receipt.Dataset ?? DatasetKey}|RECEIPT|{Loans.Single(x => x.Key == receipt.LoanKey).AccountNumber}|{receipt.Period.Key}|{receipt.Kind}|{receipt.Amount.ToString("0.00", CultureInfo.InvariantCulture)}|Deposit|Completed|{receipt.Reference}";

    public static void Validate()
    {
        var periods = Periods.Select(x => x.Key).ToArray();
        if (periods.Length != 6 || periods.Distinct(StringComparer.Ordinal).Count() != 6 || !periods.SequenceEqual(["2025-10", "2025-11", "2025-12", "2026-01", "2026-02", "2026-03"])) throw new SampleDataValidationException("The fixture period catalog is not the required six-month sequence.");
        RequireUnique(Locations.Select(x => x.Code), "location code"); RequireUnique(Branches.Select(x => x.Code), "branch code"); RequireUnique(Customers.Select(x => x.Key), "customer key"); RequireUnique(Customers.Select(x => x.Code), "customer code"); RequireUnique(Loans.Select(x => x.Key), "loan key"); RequireUnique(Loans.Select(x => x.AccountNumber), "account number"); RequireUnique(Loans.Select(x => x.ApplicationNumber), "application number");
        var locations = Locations.ToDictionary(x => x.Code, StringComparer.Ordinal); if (Locations.Any(x => x.ParentCode != "IND" && (!locations.TryGetValue(x.ParentCode, out var p) || p.Level != x.Level - 1))) throw new SampleDataValidationException("The location parent topology is invalid.");
        if (Branches.Any(x => !locations.TryGetValue(x.CityCode, out var city) || city.Level != 3 || !locations.TryGetValue(x.ZoneCode, out var zone) || zone.Level != 4 || zone.ParentCode != x.CityCode)) throw new SampleDataValidationException("A branch city or zone mapping is invalid.");
        if (Loans.Any(x => !Customers.Any(c => c.Key == x.CustomerKey) || !Branches.Any(b => b.Code == x.BranchCode))) throw new SampleDataValidationException("An application mapping is incomplete.");
        if (Receipts.Any(x => !Loans.Any(l => l.Key == x.LoanKey) || x.Reference.Length is < 1 or > 120 || x.Reference.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or ':' or '.')))) throw new SampleDataValidationException("A receipt reference or account mapping is invalid.");
    }

    private static void RequireUnique(IEnumerable<string> values, string name) { if (values.Count() != values.Distinct(StringComparer.Ordinal).Count()) throw new SampleDataValidationException($"Duplicate {name} in sample catalog."); }

    private static IReadOnlyList<SampleReceipt> BuildReceipts()
    {
        var rows = new List<SampleReceipt>();
        foreach (var period in Periods)
        foreach (var loan in Loans)
        {
            var amount = loan.Key switch { "A001" => loan.Emi, "A002" => 2000m, "A003" => loan.Emi + 3000m, "A004" => period.Key == "2026-03" ? 0m : 1000m, "A005" => 0m, "A006" => 0m, "A007" => period.Key == "2026-03" ? 5000m : 3000m, _ => 0m };
            var kind = amount == 0m ? "NONE" : amount > loan.Emi ? "OVER" : amount == loan.Emi ? "FULL" : "PART";
            rows.Add(new($"{loan.Key}:{period.Key}", loan.Key, period, kind, amount, new DateTime(period.Year, period.Month, 20, 9, 0, 0, DateTimeKind.Utc), $"RCPT-DP-{loan.AccountNumber}-{period.Key}-{kind}"));
        }
        var march = new SamplePeriod(2026, 3);
        rows.Add(new("A006:2026-03:FUTURE", "A006", march, "FULL", 9000m, new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-FUTURE"));
        rows.Add(new("A001:2026-03:NEGATIVE", "A001", march, "PART", -10m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-NEGATIVE"));
        rows.Add(new("A001:2026-03:NONE-POSITIVE", "A001", march, "NONE", 10m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-NONE-POSITIVE"));
        rows.Add(new("A001:2026-03:WRONG-DATASET", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-DATASET", true, true, "OTHER-DATASET"));
        rows.Add(new("A001:2026-03:WRONG-ACCOUNT", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-ACCOUNT", true, true, null, "SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT|LN-DP-9999|2026-03|PART|100.00|Deposit|Completed|RCPT-DP-WRONG-ACCOUNT"));
        rows.Add(new("A001:2026-03:WRONG-TOKENS", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-TOKENS", true, true, null, "SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT"));
        rows.Add(new("A001:2026-03:WRONG-AMOUNT", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-AMOUNT", true, true, null, null, 99m));
        rows.Add(new("A001:2026-03:WRONG-ENTITY", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-ENTITY", false, true));
        rows.Add(new("A001:2026-03:WRONG-STATUS", "A001", march, "PART", 100m, new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "RCPT-DP-WRONG-STATUS", true, false));
        return rows;
    }
}
```

The `TNJ-B01` catalog row must be exactly `new("TNJ-B01", "Thanjavur Main Branch", "TNJ", "TNJ-Z01")`; the complete line above intentionally uses that exact city code and zone code. Do not alter it when copying.

## Task 3 — Add ownership, canonical collision handling, and the transactional seeder

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleDataExceptions.cs`

```csharp
namespace Finnova.Service.DemandProcessing.SampleData;

public sealed class SampleDataCollisionException(string entityType, string logicalKey)
    : InvalidOperationException($"Sample data ownership collision for {entityType}:{logicalKey}.");

public sealed class SampleDataValidationException(string message) : InvalidOperationException(message);
public sealed record SampleOwnership(string EntityType, string LogicalKey, Guid EntityId, string ContentHash);
```

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleOwnershipStore.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed class SampleOwnershipStore(FinnovaDbContext db)
{
    public async Task<SampleOwnership?> FindAsync(string entityType, string logicalKey, CancellationToken ct)
    {
        var code = $"{entityType}:{logicalKey}";
        var row = await db.LookupValues.AsNoTracking().SingleOrDefaultAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code == code, ct);
        if (row is null) return null;
        var parts = row.Value.Split('|');
        if (parts.Length != 4 || parts[0] != SampleDataCatalog.DatasetKey || parts[1] != entityType || !Guid.TryParse(parts[2], out var id) || parts[3].Length != 64) throw new SampleDataCollisionException(entityType, logicalKey);
        return new SampleOwnership(entityType, logicalKey, id, parts[3]);
    }

    public async Task RequireAsync(string entityType, string logicalKey, Guid entityId, CancellationToken ct)
    {
        var owner = await FindAsync(entityType, logicalKey, ct);
        if (owner is null || owner.EntityId != entityId) throw new SampleDataCollisionException(entityType, logicalKey);
    }

    public async Task RegisterAsync(string entityType, string logicalKey, Guid entityId, string contentHash, CancellationToken ct)
    {
        var code = $"{entityType}:{logicalKey}";
        var value = $"{SampleDataCatalog.DatasetKey}|{entityType}|{entityId:D}|{contentHash}";
        var row = await db.LookupValues.SingleOrDefaultAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code == code, ct);
        if (row is null)
        {
            db.LookupValues.Add(new LookupValue { Id = SampleDataCatalog.StableId("Ownership", code), Module = SampleDataCatalog.OwnershipModule, LookupType = SampleDataCatalog.OwnershipType, Code = code, Value = value, DisplayOrder = 0, IsActive = true, IsSystemLocked = true, CreatedAt = SampleDataCatalog.SeedTimestampUtc, UpdatedAt = SampleDataCatalog.SeedTimestampUtc });
            return;
        }
        var existing = await FindAsync(entityType, logicalKey, ct);
        if (existing is null || existing.EntityId != entityId) throw new SampleDataCollisionException(entityType, logicalKey);
        if (row.Value != value) { row.Value = value; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; }
        row.IsActive = true; row.IsSystemLocked = true;
    }

    public Task<IReadOnlyList<SampleOwnership>> ListAsync(CancellationToken ct) => db.LookupValues.AsNoTracking().Where(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code != $"Manifest:{SampleDataCatalog.DatasetKey}").OrderBy(x => x.Code).Select(x => new SampleOwnership(x.Code.Substring(0, x.Code.IndexOf(':')), x.Code.Substring(x.Code.IndexOf(':') + 1), Guid.Parse(x.Value.Split('|')[2]), x.Value.Split('|')[3])).ToListAsync(ct).ContinueWith(x => (IReadOnlyList<SampleOwnership>)x.Result, ct);
}
```

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleDataSeeder.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Billing;
using Finnova.Service.DemandProcessing.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed class SampleDataSeeder(FinnovaDbContext db, IOptions<DemandProcessingOptions> options, SampleOwnershipStore ownership, ILogger<SampleDataSeeder> logger)
{
    private DemandProcessingOptions Options => options.Value;

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        ValidateOptions();
        SampleDataCatalog.Validate();
        var india = await db.Locations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == SampleDataCatalog.IndiaId, ct);
        if (india is null || india.Code != "IND" || india.Name != "India" || india.Level != 1 || india.ParentId is not null || !india.IsActive) throw new SampleDataValidationException("The external IND location invariants failed.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await SeedLookupsAsync(ct);
            var organization = await SeedOrganizationAsync(ct);
            var lob = await SeedLobAsync(ct);
            var locations = await SeedLocationsAsync(ct);
            var customers = await SeedCustomersAsync(organization.Id, ct);
            var loans = await SeedApplicationsAccountsSchedulesAsync(lob.Id, locations, customers, ct);
            await SeedReceiptsAsync(loans, ct);
            await SeedHistoryAsync(lob.Id, locations, loans, ct);
            await db.SaveChangesAsync(ct);
            await ValidateDatabaseAsync(lob.Id, locations, loans, ct);
            var fingerprint = await FingerprintAsync(ct);
            await ownership.RegisterAsync("Manifest", SampleDataCatalog.DatasetKey, SampleDataCatalog.StableId("Manifest", SampleDataCatalog.DatasetKey), fingerprint, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Demand sample {DatasetKey} committed with fingerprint {Fingerprint}.", SampleDataCatalog.DatasetKey, fingerprint);
            return fingerprint;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private void ValidateOptions()
    {
        if (Options.SampleDataKey != SampleDataCatalog.DatasetKey) throw new SampleDataValidationException("SampleDataKey must be SAMPLE-DP-V1.");
        if (Options.SampleAsOfDate != SampleDataCatalog.AsOfDate) throw new SampleDataValidationException("SampleAsOfDate must be 2026-03-31.");
        if (Options.BusinessTimezone is not ("Asia/Kolkata" or "India Standard Time")) throw new SampleDataValidationException("BusinessTimezone must identify Asia/Kolkata.");
    }

    private async Task SeedLookupsAsync(CancellationToken ct)
    {
        await UpsertNationalityAsync(ct);
        var order = 1;
        foreach (var item in SampleDataCatalog.LookupRows) await UpsertLookupAsync(item.Type, item.Code, item.Value, order++, ct);
    }

    private async Task UpsertNationalityAsync(CancellationToken ct)
    {
        const string key = "IN"; var id = SampleDataCatalog.StableId("Nationality", key);
        var row = await db.Nationalities.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Nationalities.SingleOrDefaultAsync(x => x.Code == key, ct);
        if (row is not null) await RequireExistingAsync("Nationality", key, id, row.Id, ct); else row = new Nationality { Id = id };
        row.Code = key; row.Name = "Indian"; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.Nationalities.Add(row);
        await ownership.RegisterAsync("Nationality", key, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.IsActive }), ct);
    }

    private async Task UpsertLookupAsync(string type, string code, string value, int displayOrder, CancellationToken ct)
    {
        var key = $"{type}:{code}"; var id = SampleDataCatalog.StableId("Lookup", key);
        var row = await db.LookupValues.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.LookupValues.SingleOrDefaultAsync(x => x.Module == "DemandProcessing" && x.LookupType == type && x.Code == code, ct);
        if (row is not null) await RequireExistingAsync("Lookup", key, id, row.Id, ct); else row = new LookupValue { Id = id };
        row.Module = "DemandProcessing"; row.LookupType = type; row.Code = code; row.Value = value; row.DisplayOrder = displayOrder; row.IsActive = true; row.IsSystemLocked = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.LookupValues.Add(row);
        await ownership.RegisterAsync("Lookup", key, id, SampleDataCatalog.Hash(new { row.Id, row.Module, row.LookupType, row.Code, row.Value, row.DisplayOrder, row.IsActive, row.IsSystemLocked }), ct);
    }

    private async Task<Organization> SeedOrganizationAsync(CancellationToken ct)
    {
        var id = SampleDataCatalog.StableId("Organization", SampleDataCatalog.OrganizationCode); var row = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Organizations.SingleOrDefaultAsync(x => x.Code == SampleDataCatalog.OrganizationCode, ct);
        if (row is not null) await RequireExistingAsync("Organization", SampleDataCatalog.OrganizationCode, id, row.Id, ct); else row = new Organization { Id = id };
        row.Code = SampleDataCatalog.OrganizationCode; row.Name = "Finnova Tamil Nadu Lending Sample"; row.ConstitutionType = ConstitutionType.PrivateLtd; row.AccountingCurrency = "INR"; row.Status = OrganizationStatus.Active; row.CorporateCity = "Chennai"; row.CorporateState = "Tamil Nadu"; row.CorporateCountry = "India"; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.Organizations.Add(row);
        await ownership.RegisterAsync("Organization", SampleDataCatalog.OrganizationCode, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.ConstitutionType, row.AccountingCurrency, row.Status, row.CorporateCity, row.CorporateState, row.CorporateCountry }), ct); return row;
    }

    private async Task<LineOfBusiness> SeedLobAsync(CancellationToken ct)
    {
        var id = SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey); var row = await db.LinesOfBusiness.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.LinesOfBusiness.SingleOrDefaultAsync(x => x.LOB_Description == SampleDataCatalog.LobKey, ct);
        if (row is not null) await RequireExistingAsync("LineOfBusiness", SampleDataCatalog.LobKey, id, row.Id, ct); else row = new LineOfBusiness { Id = id };
        row.LOB_Name = "Retail Lending Sample"; row.LOB_Description = SampleDataCatalog.LobKey; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.LinesOfBusiness.Add(row);
        await ownership.RegisterAsync("LineOfBusiness", SampleDataCatalog.LobKey, id, SampleDataCatalog.Hash(new { row.Id, row.LOB_Name, row.LOB_Description, row.IsActive }), ct); return row;
    }

    private async Task<Dictionary<string, Location>> SeedLocationsAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, Location>(StringComparer.Ordinal) { ["IND"] = await db.Locations.SingleAsync(x => x.Id == SampleDataCatalog.IndiaId, ct) };
        foreach (var item in SampleDataCatalog.Locations) { var id = SampleDataCatalog.StableId("Location", item.Code); var row = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Locations.SingleOrDefaultAsync(x => x.Code == item.Code, ct); if (row is not null) await RequireExistingAsync("Location", item.Code, id, row.Id, ct); else row = new Location { Id = id }; var parent = map[item.ParentCode]; row.Code = item.Code; row.Name = item.Name; row.Level = item.Level; row.ParentId = parent.Id; row.Description = SampleDataCatalog.DatasetKey; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Locations.Add(row); map[item.Code] = row; await ownership.RegisterAsync("Location", item.Code, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.Level, row.ParentId, row.IsActive }), ct); }
        foreach (var item in SampleDataCatalog.Branches) { var id = SampleDataCatalog.StableId("Location", item.Code); var row = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Locations.SingleOrDefaultAsync(x => x.Code == item.Code, ct); if (row is not null) await RequireExistingAsync("Location", item.Code, id, row.Id, ct); else row = new Location { Id = id }; var parent = map[item.ZoneCode]; row.Code = item.Code; row.Name = item.Name; row.Level = 5; row.ParentId = parent.Id; row.Description = $"{SampleDataCatalog.DatasetKey}|{item.CityCode}|{item.ZoneCode}"; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Locations.Add(row); map[item.Code] = row; await ownership.RegisterAsync("Location", item.Code, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.Level, row.ParentId, row.Description, row.IsActive }), ct); }
        return map;
    }

    private async Task<Dictionary<string, Customer>> SeedCustomersAsync(Guid organizationId, CancellationToken ct)
    {
        var result = new Dictionary<string, Customer>(StringComparer.Ordinal);
        foreach (var item in SampleDataCatalog.Customers)
        {
            var id = SampleDataCatalog.StableId("Customer", item.Key); var row = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Customers.SingleOrDefaultAsync(x => x.CustomerCode == item.Code, ct); if (row is not null) await RequireExistingAsync("Customer", item.Key, id, row.Id, ct); else row = Customer.CreateWithId(id);
            row.SetGeneratedCode(item.Code); row.Initialize(organizationId, true, false, false, "INDIVIDUAL", null, null, null, null, "PRIVATE", "IN", "MR", "MARRIED", null, null, null, null, item.FirstName, null, item.LastName, null, null, null, null, false, false, null, "Owned", false, null, null); row.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(row).State == EntityState.Detached) db.Customers.Add(row); await ownership.RegisterAsync("Customer", item.Key, id, SampleDataCatalog.Hash(new { row.Id, row.CustomerCode, row.CompanyId, row.CustomerTypeCode, row.NationalityCode, row.TitleCode, row.MaritalStatusCode, row.FirstName, row.ThirdName, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc }), ct);
            await SeedCustomerChildrenAsync(row, item, ct); result[item.Key] = row;
        }
        return result;
    }

    private async Task SeedCustomerChildrenAsync(Customer customer, SampleCustomer item, CancellationToken ct)
    {
        var addressId = SampleDataCatalog.StableId("CustomerAddress", item.Key); var address = await db.CustomerAddresses.SingleOrDefaultAsync(x => x.Id == addressId, ct); if (address is not null) await RequireExistingAsync("CustomerAddress", item.Key, addressId, address.Id, ct); else address = CustomerAddress.CreateWithId(addressId); address.SetFixtureIdentity(customer.Id); address.Initialize(CustomerAddressType.Permanent, "12", null, item.PostalCode, item.City, "Tamil Nadu", "India", null, "Sample Road", null, null, $"900000000{item.Key[^1]}", null, $"{item.Key.ToLowerInvariant()}@sample.finnova.in", null, null, null, null, null); address.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(address).State == EntityState.Detached) db.CustomerAddresses.Add(address); await ownership.RegisterAsync("CustomerAddress", item.Key, addressId, SampleDataCatalog.Hash(new { address.Id, address.CustomerId, address.AddressType, address.PostalCode, address.City, address.State, address.Country, address.Mobile, address.Email, address.CreatedAtUtc, address.UpdatedAtUtc }), ct);
        var detailsId = SampleDataCatalog.StableId("IndividualCustomerDetails", item.Key); var details = await db.IndividualCustomerDetails.SingleOrDefaultAsync(x => x.Id == detailsId, ct); if (details is not null) await RequireExistingAsync("IndividualCustomerDetails", item.Key, detailsId, details.Id, ct); else details = IndividualCustomerDetails.CreateWithId(detailsId); details.SetFixtureIdentity(customer.Id); details.Initialize("M", new DateOnly(1985, 5, 10), null, 1, 2, null, "Flat", true, 2500000m, 500000m, 2000000m, "Graduate", "Salaried employee", null, null, null, null); details.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(details).State == EntityState.Detached) db.IndividualCustomerDetails.Add(details); await ownership.RegisterAsync("IndividualCustomerDetails", item.Key, detailsId, SampleDataCatalog.Hash(new { details.Id, details.CustomerId, details.GenderCode, details.DateOfBirth, details.Qualification, details.Profession, details.CreatedAtUtc, details.UpdatedAtUtc }), ct);
    }

    private async Task<Dictionary<string, LoanAccount>> SeedApplicationsAccountsSchedulesAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, Customer> customers, CancellationToken ct)
    {
        var result = new Dictionary<string, LoanAccount>(StringComparer.Ordinal);
        foreach (var item in SampleDataCatalog.Loans)
        {
            var customer = customers[item.CustomerKey]; var branch = locations[item.BranchCode]; var appId = SampleDataCatalog.StableId("Application", item.ApplicationNumber); var app = await db.Applications.SingleOrDefaultAsync(x => x.Id == appId, ct) ?? await db.Applications.SingleOrDefaultAsync(x => x.ApplicationNumber == item.ApplicationNumber, ct); if (app is not null) await RequireExistingAsync("Application", item.ApplicationNumber, appId, app.Id, ct); else app = new Application { Id = appId }; app.ApplicationNumber = item.ApplicationNumber; app.LineOfBusinessId = lobId; app.CustomerId = customer.Id; app.BranchId = branch.Id; app.ProductCode = "SAMPLE-PL"; app.SchemeCode = "TN-MONTHLY"; app.FinanceAmount = item.FinanceAmount; app.IrrPercent = 12m; app.Tenure = item.Tenure; app.RepaymentFrequency = RepaymentFrequency.Monthly; app.DisbursementDate = new DateOnly(2025, 9, 15); app.ProcessingCharges = 0m; app.Status = ApplicationStatus.Processed; app.CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc; app.UpdatedAtUtc = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(app).State == EntityState.Detached) db.Applications.Add(app); await ownership.RegisterAsync("Application", item.ApplicationNumber, appId, SampleDataCatalog.Hash(new { app.Id, app.ApplicationNumber, app.LineOfBusinessId, app.CustomerId, app.BranchId, app.ProductCode, app.SchemeCode, app.FinanceAmount, app.IrrPercent, app.Tenure, app.RepaymentFrequency, app.DisbursementDate, app.Status }), ct);
            var genericId = SampleDataCatalog.StableId("Account", item.AccountNumber); var generic = await db.Accounts.SingleOrDefaultAsync(x => x.Id == genericId, ct) ?? await db.Accounts.SingleOrDefaultAsync(x => x.AccountNumber == item.AccountNumber, ct); if (generic is not null) await RequireExistingAsync("Account", item.AccountNumber, genericId, generic.Id, ct); else generic = new Account { Id = genericId }; generic.AccountNumber = item.AccountNumber; generic.AccountName = $"{item.AccountNumber} {customer.FirstName} {customer.LastName}"; generic.Type = AccountType.Loan; generic.Status = AccountStatus.Active; generic.Balance = item.FinanceAmount; generic.Currency = "INR"; generic.OrganizationId = customer.CompanyId; generic.BranchCode = item.BranchCode; generic.OpenedAt = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc); generic.CreatedAt = SampleDataCatalog.SeedTimestampUtc; generic.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(generic).State == EntityState.Detached) db.Accounts.Add(generic); await ownership.RegisterAsync("Account", item.AccountNumber, genericId, SampleDataCatalog.Hash(new { generic.Id, generic.AccountNumber, generic.AccountName, generic.Type, generic.Status, generic.Balance, generic.Currency, generic.OrganizationId, generic.BranchCode, generic.OpenedAt }), ct);
            var loanId = SampleDataCatalog.StableId("LoanAccount", item.AccountNumber); var loan = await db.LoanAccounts.SingleOrDefaultAsync(x => x.Id == loanId, ct) ?? await db.LoanAccounts.SingleOrDefaultAsync(x => x.AccountNumber == item.AccountNumber, ct); if (loan is not null) await RequireExistingAsync("LoanAccount", item.AccountNumber, loanId, loan.Id, ct); else loan = new LoanAccount { Id = loanId }; loan.AccountNumber = item.AccountNumber; loan.AccountName = generic.AccountName; loan.Type = AccountType.Loan; loan.Status = AccountStatus.Active; loan.Balance = item.FinanceAmount; loan.Currency = "INR"; loan.BranchCode = item.BranchCode; loan.OpenedAt = generic.OpenedAt; loan.CreatedAt = SampleDataCatalog.SeedTimestampUtc; loan.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; loan.ApplicationId = app.Id; loan.CustomerId = customer.Id; loan.LineOfBusinessId = lobId; loan.BranchId = branch.Id; loan.ProductCode = app.ProductCode; loan.SchemeCode = app.SchemeCode; loan.CustomerNameSnapshot = $"{customer.FirstName} {customer.LastName}"; loan.CreationNotes = SampleDataCatalog.DatasetKey; loan.KycStatus = KycStatus.Passed; loan.KycFlagged = false; if (db.Entry(loan).State == EntityState.Detached) db.LoanAccounts.Add(loan); await ownership.RegisterAsync("LoanAccount", item.AccountNumber, loanId, SampleDataCatalog.Hash(new { loan.Id, loan.AccountNumber, loan.Type, loan.Status, loan.Balance, loan.Currency, loan.ApplicationId, loan.CustomerId, loan.LineOfBusinessId, loan.BranchId, loan.ProductCode, loan.SchemeCode, loan.KycStatus, loan.KycFlagged }), ct);
            await SeedScheduleAsync(app, item, ct); result[item.Key] = loan;
        }
        return result;
    }

    private async Task SeedScheduleAsync(Application app, SampleLoan item, CancellationToken ct)
    {
        var scheduleId = SampleDataCatalog.StableId("ApplicationSchedule", item.ApplicationNumber); var schedule = await db.ApplicationSchedules.Include(x => x.Installments).SingleOrDefaultAsync(x => x.Id == scheduleId, ct); if (schedule is not null) await RequireExistingAsync("ApplicationSchedule", item.ApplicationNumber, scheduleId, schedule.Id, ct); else schedule = new ApplicationSchedule { Id = scheduleId }; var principal = BillingRules.Money(item.Emi * .72m); var interest = BillingRules.Money(item.Emi - principal); schedule.ApplicationId = app.Id; schedule.IsFinalized = true; schedule.GeneratedAtUtc = SampleDataCatalog.SeedTimestampUtc; schedule.FinanceAmount = item.FinanceAmount; schedule.IrrPercent = 12m; schedule.Tenure = item.Tenure; schedule.RepaymentFrequency = RepaymentFrequency.Monthly; schedule.DisbursementDate = app.DisbursementDate; schedule.PeriodicInterestRate = 1m; schedule.EmiAmount = item.Emi; schedule.TotalInstallmentAmount = BillingRules.Money(item.Emi * 6m); schedule.TotalPrincipalAmount = BillingRules.Money(principal * 6m); schedule.TotalInterestAmount = BillingRules.Money(interest * 6m); if (db.Entry(schedule).State == EntityState.Detached) db.ApplicationSchedules.Add(schedule);
        foreach (var (period, index) in SampleDataCatalog.Periods.Select((p, i) => (p, i + 1))) { var id = SampleDataCatalog.StableId("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}"); var installment = await db.ApplicationInstallments.SingleOrDefaultAsync(x => x.Id == id, ct); if (installment is not null) await RequireExistingAsync("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}", id, installment.Id, ct); else installment = new ApplicationInstallment { Id = id }; installment.ScheduleId = schedule.Id; installment.InstallmentNumber = index; installment.DueDate = period.DueDate; installment.OpeningBalance = BillingRules.Money(item.FinanceAmount - principal * (index - 1)); installment.PrincipalPortion = principal; installment.InterestPortion = interest; installment.InstallmentAmount = item.Emi; installment.ClosingBalance = BillingRules.Money(installment.OpeningBalance - principal); if (db.Entry(installment).State == EntityState.Detached) db.ApplicationInstallments.Add(installment); await ownership.RegisterAsync("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}", id, SampleDataCatalog.Hash(new { installment.Id, installment.ScheduleId, installment.InstallmentNumber, installment.DueDate, installment.OpeningBalance, installment.InterestPortion, installment.PrincipalPortion, installment.InstallmentAmount, installment.ClosingBalance }), ct); }
        await ownership.RegisterAsync("ApplicationSchedule", item.ApplicationNumber, scheduleId, SampleDataCatalog.Hash(new { schedule.Id, schedule.ApplicationId, schedule.IsFinalized, schedule.FinanceAmount, schedule.EmiAmount, schedule.TotalInstallmentAmount, schedule.TotalPrincipalAmount, schedule.TotalInterestAmount, InstallmentCount = 6 }), ct);
    }

    private async Task SeedReceiptsAsync(Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        foreach (var item in SampleDataCatalog.Receipts) { var loan = loans[item.LoanKey]; var generic = await db.Accounts.SingleAsync(x => x.AccountNumber == loan.AccountNumber, ct); var id = SampleDataCatalog.StableId("Transaction", item.Key); var row = await db.Transactions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is not null) await RequireExistingAsync("Transaction", item.Key, id, row.Id, ct); else row = new Transaction { Id = id }; row.AccountId = generic.Id; row.Type = item.IsDeposit ? TransactionType.Deposit : TransactionType.Withdrawal; row.Status = item.IsCompleted ? TransactionStatus.Completed : TransactionStatus.Pending; row.Amount = item.EntityAmount; row.BalanceAfter = generic.Balance; row.ReferenceNumber = item.Reference; row.Description = item.Description; row.TransactionDate = item.TransactionDateUtc; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Transactions.Add(row); await ownership.RegisterAsync("Transaction", item.Key, id, SampleDataCatalog.Hash(new { row.Id, row.AccountId, row.Type, row.Status, row.Amount, row.BalanceAfter, row.ReferenceNumber, row.Description, row.TransactionDate, row.CreatedAt }), ct); }
    }

    private async Task SeedHistoryAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        foreach (var period in SampleDataCatalog.HistoryPeriods)
        {
            var run = new DemandRun { Id = SampleDataCatalog.StableId("DemandRun", period.Key), LineOfBusinessId = lobId, Frequency = SampleDataCatalog.Frequency, PeriodYear = period.Year, PeriodMonth = period.Month, BusinessTimezone = "Asia/Kolkata", LogicalScopeKey = $"{SampleDataCatalog.DatasetKey}|{lobId:N}|{period.Key}", ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, Status = DemandRunStatus.Completed, BillingStatus = BillingStageStatus.Succeeded, Source = DemandSource.Automizer, ActorId = "sample-seeder", CorrelationId = $"{SampleDataCatalog.DatasetKey}:{period.Key}", CurrentRevisionNumber = 1, RequestedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc };
            run = await UpsertRunAsync(run, period, locations, ct);
            var revision = new DemandRunRevision { Id = SampleDataCatalog.StableId("DemandRevision", period.Key), DemandRunId = run.Id, RevisionNumber = 1, Status = DemandRunStatus.Completed, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc, DemandRun = run };
            var stage = new BillingStageRun { Id = SampleDataCatalog.StableId("BillingStage", period.Key), DemandRunId = run.Id, RevisionNumber = 1, LogicalBillingScopeKey = run.LogicalScopeKey, Status = BillingStageStatus.Succeeded, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "Default", ChargePolicyKind = ChargePolicyKind.Default, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc, CorrelationId = run.CorrelationId, DemandRun = run };
            var billing = new BillingSnapshot { Id = SampleDataCatalog.StableId("BillingSnapshot", period.Key), BillingStageRunId = stage.Id, RevisionNumber = 1, Version = $"{SampleDataCatalog.DatasetKey}:B:{period.Key}", LogicalScopeKey = run.LogicalScopeKey, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, IsPublished = true, ActorId = "sample-seeder", Source = "SampleData", CorrelationId = run.CorrelationId, PublishedAtUtc = SampleDataCatalog.SeedTimestampUtc };
            var demand = new DemandSnapshot { Id = SampleDataCatalog.StableId("DemandSnapshot", period.Key), DemandRunId = run.Id, RevisionNumber = 1, BillingSnapshotId = billing.Id, Version = $"{SampleDataCatalog.DatasetKey}:D:{period.Key}", IsPublished = true, PublishedAtUtc = SampleDataCatalog.SeedTimestampUtc, DemandRun = run };
            run.BillingStageRunId = stage.Id; run.BillingSnapshotId = billing.Id; run.DemandSnapshotId = demand.Id; revision.BillingSnapshotId = billing.Id; revision.DemandSnapshotId = demand.Id;
            db.DemandRunRevisions.Update(revision); db.BillingStageRuns.Update(stage); db.BillingSnapshots.Update(billing); db.DemandSnapshots.Update(demand);
            var rows = new List<DemandRow>();
            foreach (var item in SampleDataCatalog.Loans) { var loan = loans[item.Key]; var branch = locations[item.BranchCode]; var arrears = period.Key == "2026-02" && item.Key == "A002" ? 12000m : period.Key == "2026-02" && item.Key == "A004" ? 8000m : 0m; var resultId = SampleDataCatalog.StableId("BillingResult", $"{period.Key}:{item.Key}"); var result = new BillingPeriodResult { Id = resultId, BillingStageRunId = stage.Id, AccountId = loan.Id, AccountDisplay = item.AccountNumber, CustomerDisplay = item.CustomerKey, CustomerId = loan.CustomerId!.Value, LineOfBusinessId = lobId, BranchId = branch.Id, AccountStatusAtCutoff = "Active", IsEligible = true, Status = BillingPeriodResultStatus.Succeeded, ScheduleReference = $"{SampleDataCatalog.DatasetKey}|SCHEDULE|{item.ApplicationNumber}|{period.Key}", ScheduledPrincipal = BillingRules.Money(item.Emi * .72m), ScheduledInterestOrEmi = BillingRules.Money(item.Emi * .28m), ScheduledInstallment = item.Emi, CurrentDue = item.Emi, Arrears = arrears, Odi = 0m, OtherCharges = 0m, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "Default", ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc }; db.BillingPeriodResults.Update(result); var row = new DemandRow { Id = SampleDataCatalog.StableId("DemandRow", $"{period.Key}:{item.Key}"), DemandRunRevisionId = revision.Id, DemandSnapshotId = demand.Id, BillingPeriodResultId = result.Id, AccountId = loan.Id, CustomerId = loan.CustomerId.Value, LineOfBusinessId = lobId, BranchId = branch.Id, AccountDisplay = item.AccountNumber, CustomerDisplay = item.CustomerKey, Status = DemandRowStatus.Completed, CurrentDue = item.Emi, Arrears = arrears, Odi = 0m, OtherCharges = 0m, TotalCharges = 0m, TotalDemand = BillingRules.Money(item.Emi + arrears), ScheduleReference = result.ScheduleReference, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, OrderingKey = loan.Id.ToString("N") }; rows.Add(row); db.DemandRows.Update(row); var evidence = new BillingAllocationEvidence { Id = SampleDataCatalog.StableId("BillingEvidence", $"{period.Key}:{item.Key}"), BillingStageRunId = stage.Id, BillingPeriodResultId = result.Id, EventType = AllocationEventType.OtherCharge, SourceEventId = result.Id, AllocationPolicyReference = "ConfiguredAllocationPolicy", AllocationSequence = 0, AllocatedAmount = 0m, TargetComponent = "CurrentDue", Outcome = AllocationOutcome.Excluded, SafeReason = "Historical fixture row has no source allocation event.", ApplicationKey = $"{SampleDataCatalog.DatasetKey}:{period.Key}:{item.Key}" }; db.BillingAllocationEvidence.Update(evidence); await ownership.RegisterAsync("BillingResult", $"{period.Key}:{item.Key}", resultId, SampleDataCatalog.Hash(new { result.Id, result.BillingStageRunId, result.AccountId, result.CustomerId, result.LineOfBusinessId, result.BranchId, result.CurrentDue, result.Arrears, result.Status }), ct); await ownership.RegisterAsync("DemandRow", $"{period.Key}:{item.Key}", row.Id, SampleDataCatalog.Hash(new { row.Id, row.DemandSnapshotId, row.AccountId, row.CustomerId, row.LineOfBusinessId, row.BranchId, row.CurrentDue, row.Arrears, row.TotalDemand }), ct); await ownership.RegisterAsync("BillingEvidence", $"{period.Key}:{item.Key}", evidence.Id, SampleDataCatalog.Hash(new { evidence.Id, evidence.BillingStageRunId, evidence.BillingPeriodResultId, evidence.EventType, evidence.AllocatedAmount, evidence.Outcome }), ct); }
            foreach (var branch in SampleDataCatalog.Branches) { var batchId = SampleDataCatalog.StableId("DemandBatch", $"{period.Key}:{branch.Code}"); var batch = new DemandBatch { Id = batchId, DemandRunRevisionId = revision.Id, DemandSnapshotId = demand.Id, DemandRunId = run.Id, RevisionNumber = 1, BranchId = locations[branch.Code].Id, LineOfBusinessId = lobId, Frequency = SampleDataCatalog.Frequency, PeriodYear = period.Year, PeriodMonth = period.Month, Status = branch.Code == "MDU-B02" ? DemandBatchStatus.NoDemand : DemandBatchStatus.ReadyToSpool, SpoolIdempotencyKey = $"{run.Id:N}:1:{locations[branch.Code].Id:N}" }; foreach (var row in rows.Where(x => x.BranchId == batch.BranchId)) row.DemandBatchId = batch.Id; var branchRows = rows.Where(x => x.BranchId == batch.BranchId).ToArray(); batch.RowCount = branchRows.Length; batch.CurrentDueTotal = BillingRules.Money(branchRows.Sum(x => x.CurrentDue)); batch.ArrearsTotal = BillingRules.Money(branchRows.Sum(x => x.Arrears)); batch.TotalCharges = 0m; batch.TotalDemand = BillingRules.Money(branchRows.Sum(x => x.TotalDemand)); db.DemandBatches.Update(batch); await ownership.RegisterAsync("DemandBatch", $"{period.Key}:{branch.Code}", batchId, SampleDataCatalog.Hash(new { batch.Id, batch.DemandRunId, batch.RevisionNumber, batch.BranchId, batch.LineOfBusinessId, batch.RowCount, batch.Status, batch.CurrentDueTotal, batch.ArrearsTotal, batch.TotalDemand }), ct); }
            run.RowCount = rows.Count; run.BatchCount = SampleDataCatalog.Branches.Count; run.CurrentDueTotal = BillingRules.Money(rows.Sum(x => x.CurrentDue)); run.ArrearsTotal = BillingRules.Money(rows.Sum(x => x.Arrears)); run.TotalCharges = 0m; run.TotalDemand = BillingRules.Money(rows.Sum(x => x.TotalDemand)); revision.RowCount = run.RowCount; revision.BatchCount = run.BatchCount; revision.CurrentDueTotal = run.CurrentDueTotal; revision.ArrearsTotal = run.ArrearsTotal; revision.TotalCharges = 0m; revision.TotalDemand = run.TotalDemand; stage.EligibleAccountCount = rows.Count; stage.SuccessfulResultCount = rows.Count; billing.ResultCount = rows.Count; billing.CurrentDueTotal = run.CurrentDueTotal; billing.ArrearsTotal = run.ArrearsTotal; billing.OdiTotal = 0m; billing.OtherChargesTotal = 0m; stage.BillingSnapshotId = billing.Id; stage.BillingSnapshotVersion = billing.Version; demand.RowCount = rows.Count; demand.BatchCount = run.BatchCount; demand.TotalDemand = run.TotalDemand;
            await ownership.RegisterAsync("DemandRun", period.Key, run.Id, SampleDataCatalog.Hash(new { run.Id, run.LineOfBusinessId, run.Frequency, run.PeriodYear, run.PeriodMonth, run.BusinessTimezone, run.LogicalScopeKey, run.ProcessingCutoffReference, run.Status, run.BillingStatus, run.CurrentRevisionNumber, run.BillingStageRunId, run.BillingSnapshotId, run.DemandSnapshotId, run.RowCount, run.BatchCount, run.CurrentDueTotal, run.ArrearsTotal, run.TotalCharges, run.TotalDemand }), ct);
            await ownership.RegisterAsync("DemandRevision", period.Key, revision.Id, SampleDataCatalog.Hash(new { revision.Id, revision.DemandRunId, revision.RevisionNumber, revision.Status, revision.BillingSnapshotId, revision.DemandSnapshotId, revision.RowCount, revision.BatchCount, revision.CurrentDueTotal, revision.ArrearsTotal, revision.TotalCharges, revision.TotalDemand }), ct);
            await ownership.RegisterAsync("BillingStage", period.Key, stage.Id, SampleDataCatalog.Hash(new { stage.Id, stage.DemandRunId, stage.RevisionNumber, stage.Status, stage.BillingSnapshotId, stage.EligibleAccountCount, stage.SuccessfulResultCount }), ct);
            await ownership.RegisterAsync("BillingSnapshot", period.Key, billing.Id, SampleDataCatalog.Hash(new { billing.Id, billing.BillingStageRunId, billing.RevisionNumber, billing.Version, billing.LogicalScopeKey, billing.ProcessingCutoffReference, billing.ResultCount, billing.CurrentDueTotal, billing.ArrearsTotal, billing.OdiTotal, billing.OtherChargesTotal, billing.IsPublished }), ct);
            await ownership.RegisterAsync("DemandSnapshot", period.Key, demand.Id, SampleDataCatalog.Hash(new { demand.Id, demand.DemandRunId, demand.RevisionNumber, demand.BillingSnapshotId, demand.Version, demand.RowCount, demand.BatchCount, demand.TotalDemand, demand.IsPublished }), ct);
            var audit = new DemandAuditEntry { Id = SampleDataCatalog.StableId("DemandAudit", period.Key), DemandRunId = run.Id, RevisionNumber = 1, Scope = run.LogicalScopeKey, ActorId = "sample-seeder", Source = DemandSource.Automizer, CorrelationId = run.CorrelationId, NewStatus = DemandRunStatus.Completed.ToString(), OccurredAtUtc = SampleDataCatalog.SeedTimestampUtc, OutcomeCode = "DEMAND_COMPLETED", SafeReferences = $"billingSnapshot={billing.Id:N};demandSnapshot={demand.Id:N}" }; db.DemandAuditEntries.Update(audit); await ownership.RegisterAsync("DemandAudit", period.Key, audit.Id, SampleDataCatalog.Hash(new { audit.Id, audit.DemandRunId, audit.RevisionNumber, audit.Scope, audit.NewStatus, audit.OutcomeCode }), ct); }
    }

    private async Task<DemandRun> UpsertRunAsync(DemandRun run, SamplePeriod period, Dictionary<string, Location> locations, CancellationToken ct)
    {
        var existing = await db.DemandRuns.SingleOrDefaultAsync(x => x.Id == run.Id, ct); if (existing is not null) { await RequireExistingAsync("DemandRun", period.Key, run.Id, existing.Id, ct); db.Entry(existing).CurrentValues.SetValues(run); run = existing; } else db.DemandRuns.Add(run);
        foreach (var (branch, index) in SampleDataCatalog.Branches.Select((x, i) => (x, i))) { var id = SampleDataCatalog.StableId("DemandRunBranchScope", $"{period.Key}:{branch.Code}"); var scope = await db.DemandRunBranchScopes.SingleOrDefaultAsync(x => x.Id == id, ct); if (scope is not null) await RequireExistingAsync("DemandRunBranchScope", $"{period.Key}:{branch.Code}", id, scope.Id, ct); else scope = new DemandRunBranchScope { Id = id }; scope.DemandRunId = run.Id; scope.RevisionNumber = 1; scope.BranchId = locations[branch.Code].Id; scope.SortOrder = index; if (db.Entry(scope).State == EntityState.Detached) db.DemandRunBranchScopes.Add(scope); await ownership.RegisterAsync("DemandRunBranchScope", $"{period.Key}:{branch.Code}", id, SampleDataCatalog.Hash(new { scope.Id, scope.DemandRunId, scope.RevisionNumber, scope.BranchId, scope.SortOrder }), ct); }
        return run;
    }

    private async Task ValidateDatabaseAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        if (locations.Count != 1 + SampleDataCatalog.Locations.Count + SampleDataCatalog.Branches.Count || loans.Count != 7) throw new SampleDataValidationException("Source fixture count validation failed.");
        foreach (var period in SampleDataCatalog.HistoryPeriods) { var run = await db.DemandRuns.SingleAsync(x => x.Id == SampleDataCatalog.StableId("DemandRun", period.Key), ct); if (run.BranchScopes.Count != 8 || run.RowCount != 7 || run.BatchCount != 8 || run.DemandSnapshotId is null || run.BillingSnapshotId is null) { await db.Entry(run).Collection(x => x.BranchScopes).LoadAsync(ct); if (run.BranchScopes.Count != 8) throw new SampleDataValidationException($"History branch scope validation failed for {period.Key}."); } }
    }

    private async Task RequireExistingAsync(string entityType, string logicalKey, Guid expectedId, Guid actualId, CancellationToken ct)
    {
        if (expectedId != actualId) { await ownership.RequireAsync(entityType, logicalKey, actualId, ct); return; }
        await ownership.RequireAsync(entityType, logicalKey, actualId, ct);
    }

    private async Task<string> FingerprintAsync(CancellationToken ct)
    {
        var values = await db.LookupValues.AsNoTracking().Where(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code != $"Manifest:{SampleDataCatalog.DatasetKey}").OrderBy(x => x.Code).Select(x => x.Code + "=" + x.Value).ToListAsync(ct);
        return SampleDataCatalog.Hash(string.Join("\n", values));
    }
}
```

The seeder deliberately calls `SaveChangesAsync` before calculating the fingerprint so newly added ownership rows are included. The manifest is excluded from the query, so the first and second fingerprints are equal. The implementation must retain the explicit ownership checks before every update; an existing deterministic row without ownership is a collision, not an adoptable row.

## Task 4 — Add previous-demand seam and strict receipt parser

### MODIFY `Finnova.Service/DemandProcessing/Ports/DemandPorts.cs`

Append:

```csharp
public sealed record PreviousDemandResult(decimal Arrears, string SourceReference);

public interface IPreviousDemandPort
{
    Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct);
}
```

### CREATE `Finnova.Service/DemandProcessing/Adapters/NullPreviousDemandPort.cs`

```csharp
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Adapters;

public sealed class NullPreviousDemandPort : IPreviousDemandPort
{
    public Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct) => Task.FromResult<PreviousDemandResult?>(null);
}
```

### MODIFY `Finnova.Service/DemandProcessing/Processing/DemandProcessor.cs`

Add `IPreviousDemandPort previousDemand` to the primary constructor after `IAllocationEventPort events`. Replace only the allocation statement with:

```csharp
var previous = await previousDemand.GetAsync(account.AccountId, scope, period, snapshotReference, ct);
var allocationResult = allocation.Apply(installment, previous?.Arrears ?? 0m, sourceEvents);
```

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleReceiptParser.cs`

```csharp
using System.Globalization;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed record ReceiptParseResult(AllocationEvent? Event, string? ReasonCode, string SafeReference);

public static class SampleReceiptParser
{
    public static ReceiptParseResult Parse(Guid genericAccountId, string expectedLoanNumber, Transaction row, DateOnly targetPeriod, DateTime cutoffUtc)
    {
        var safe = SafeReference(row.ReferenceNumber);
        if (row.ReferenceNumber is null) return new(null, "REFERENCE", safe);
        if (row.Type != TransactionType.Deposit) return new(null, "ENTITY_TYPE", safe);
        if (row.Status != TransactionStatus.Completed) return new(null, "ENTITY_STATUS", safe);
        if (row.AccountId != genericAccountId) return new(null, "ACCOUNT_FK", safe);
        var tokens = (row.Description ?? string.Empty).Split('|');
        if (tokens.Length != 11) return new(null, "TOKEN_COUNT", safe);
        if (tokens[0] != "SAMPLE-DP" || tokens[1] != "V1" || tokens[2] != SampleDataCatalog.DatasetKey) return new(null, "DATASET", safe);
        if (tokens[3] != "RECEIPT" || tokens[4] != expectedLoanNumber) return new(null, "ACCOUNT", safe);
        if (!DateOnly.TryParseExact(tokens[5], "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var period) || period != new DateOnly(targetPeriod.Year, targetPeriod.Month, 1)) return new(null, "PERIOD", safe);
        if (tokens[6] is not ("FULL" or "PART" or "OVER" or "NONE")) return new(null, "KIND", safe);
        if (!decimal.TryParse(tokens[7], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0m) return new(null, "AMOUNT", safe);
        if (row.Amount != amount) return new(null, "PERSISTED_AMOUNT", safe);
        if ((tokens[6] == "NONE" && amount != 0m) || (tokens[6] != "NONE" && amount <= 0m)) return new(null, "AMOUNT_SEMANTICS", safe);
        if (tokens[8] != "Deposit" || tokens[9] != "Completed" || tokens[10] != row.ReferenceNumber || !IsSafeReference(tokens[10])) return new(null, "ENTITY_PAYLOAD", safe);
        if (row.TransactionDate > cutoffUtc) return new(null, "AFTER_CUTOFF", safe);
        return new(new AllocationEvent(row.Id, AllocationEventType.Receipt, amount, row.TransactionDate, safe, SampleDataCatalog.DatasetKey, "CurrentDue", true), null, safe);
    }

    public static string SafeReference(string? value) => value is not null && IsSafeReference(value) ? value : value is null ? "missing-reference" : "invalid-reference";
    private static bool IsSafeReference(string value) => value.Length is >= 1 and <= 120 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ':' or '.');
}
```

## Task 5 — Create complete sample source ports

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleSourcePorts.cs`

```csharp
using System.Runtime.CompilerServices;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed class SampleAccountSourcePort(FinnovaDbContext db) : IAccountSourcePort
{
    public async IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, [EnumeratorCancellation] CancellationToken ct)
    {
        var ids = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).ToArray();
        var rows = await db.LoanAccounts.AsNoTracking().Where(x => x.LineOfBusinessId == scope.LineOfBusinessId && x.Status == AccountStatus.Active && x.BranchId.HasValue && ids.Contains(x.BranchId.Value) && scope.BranchIds.Contains(x.BranchId.Value)).OrderBy(x => x.AccountNumber).ToListAsync(ct);
        foreach (var row in rows) { ct.ThrowIfCancellationRequested(); if (row.CustomerId is null || row.LineOfBusinessId is null || row.BranchId is null) continue; yield return new AccountProjection(row.Id, row.CustomerId.Value, row.LineOfBusinessId.Value, row.BranchId.Value, row.AccountNumber, row.CustomerNameSnapshot ?? row.AccountName, row.Status.ToString(), true, true, true, snapshotReference); }
    }

    public async Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct)
    {
        var validLob = await db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == lineOfBusinessId && x.LOB_Description == SampleDataCatalog.LobKey && x.IsActive, ct); if (!validLob) return Array.Empty<Guid>();
        var claims = principal.Claims.Where(x => x.Type == "branch").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
        var ids = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).ToArray();
        return await db.Locations.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Level == 5 && x.IsActive && claims.Contains(x.Id)).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
    }

    public async Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct)
    {
        var sampleBranch = SampleDataCatalog.Branches.Select(b => SampleDataCatalog.StableId("Location", b.Code)).Contains(branchId);
        var branchActive = sampleBranch && await db.Locations.AsNoTracking().AnyAsync(x => x.Id == branchId && x.Level == 5 && x.IsActive, ct);
        return branchActive && await db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == lineOfBusinessId && x.LOB_Description == SampleDataCatalog.LobKey && x.IsActive, ct);
    }
}

public sealed class SampleFinalizedSchedulePort(FinnovaDbContext db) : IFinalizedSchedulePort
{
    public async Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct)
    {
        var account = await db.LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.Status == AccountStatus.Active && x.ApplicationId.HasValue, ct); if (account?.ApplicationId is null) return null;
        var schedule = await db.ApplicationSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == account.ApplicationId && x.IsFinalized, ct); if (schedule is null) return null;
        var entry = await db.ApplicationInstallments.AsNoTracking().SingleOrDefaultAsync(x => x.ScheduleId == schedule.Id && x.DueDate == new DateOnly(period.Year, period.Month, 15), ct); if (entry is null) return null;
        return new ScheduleProjection(accountId, schedule.Id, entry.Id, $"{SampleDataCatalog.DatasetKey}|SCHEDULE|{account.ApplicationId:N}|{period.CanonicalValue}", entry.PrincipalPortion, entry.InterestPortion);
    }
}

public sealed class SampleAllocationEventPort(FinnovaDbContext db, ILogger<SampleAllocationEventPort> logger) : IAllocationEventPort
{
    public async Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct)
    {
        var loan = await db.LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.Status == AccountStatus.Active, ct); if (loan is null) return Array.Empty<AllocationEvent>();
        var generic = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.AccountNumber == loan.AccountNumber && x.Type == AccountType.Loan && x.Status == AccountStatus.Active, ct); if (generic is null) return Array.Empty<AllocationEvent>();
        var cutoff = SampleDataCatalog.PeriodCutoffUtc(new SamplePeriod(period.Year, period.Month)); var rows = await db.Transactions.AsNoTracking().Where(x => x.AccountId == generic.Id && x.TransactionDate <= cutoff).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToListAsync(ct); var events = new List<AllocationEvent>();
        foreach (var row in rows) { var parsed = SampleReceiptParser.Parse(generic.Id, loan.AccountNumber, row, new DateOnly(period.Year, period.Month, 1), cutoff); if (parsed.Event is not null) events.Add(parsed.Event); else logger.LogWarning("Demand sample receipt rejected {Reference} for {ReasonCode}.", parsed.SafeReference, parsed.ReasonCode); }
        return events;
    }
}

public sealed class SamplePreviousDemandPort(FinnovaDbContext db, SampleOwnershipStore ownership) : IPreviousDemandPort
{
    public async Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct)
    {
        if (snapshotReference != SampleDataCatalog.SnapshotReference) return null;
        var previous = targetPeriod.Month == 1 ? new SamplePeriod(targetPeriod.Year - 1, 12) : new SamplePeriod(targetPeriod.Year, targetPeriod.Month - 1); var expectedBranches = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).OrderBy(x => x).ToArray(); if (!scope.BranchIds.OrderBy(x => x).SequenceEqual(expectedBranches)) return null;
        var runId = SampleDataCatalog.StableId("DemandRun", previous.Key); var run = await db.DemandRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId && x.LineOfBusinessId == scope.LineOfBusinessId && x.Frequency == SampleDataCatalog.Frequency && x.PeriodYear == previous.Year && x.PeriodMonth == previous.Month && x.ProcessingCutoffReference == snapshotReference && x.CurrentRevisionNumber == 1, ct); if (run is null || !run.BillingSnapshotId.HasValue || !run.DemandSnapshotId.HasValue) return null;
        var revision = await db.DemandRunRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.DemandRunId == run.Id && x.Id == SampleDataCatalog.StableId("DemandRevision", previous.Key) && x.RevisionNumber == 1, ct); if (revision is null || revision.BillingSnapshotId != run.BillingSnapshotId || revision.DemandSnapshotId != run.DemandSnapshotId) return null;
        var stage = await db.BillingStageRuns.AsNoTracking().SingleOrDefaultAsync(x => x.DemandRunId == run.Id && x.RevisionNumber == 1 && x.BillingSnapshotId == run.BillingSnapshotId && x.Status == BillingStageStatus.Succeeded, ct); if (stage is null) return null;
        var billing = await db.BillingSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.BillingSnapshotId.Value && x.BillingStageRunId == stage.Id && x.RevisionNumber == 1 && x.IsPublished && x.ProcessingCutoffReference == snapshotReference, ct); if (billing is null) return null;
        var demand = await db.DemandSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.DemandSnapshotId.Value && x.DemandRunId == run.Id && x.BillingSnapshotId == billing.Id && x.RevisionNumber == 1 && x.IsPublished, ct); if (demand is null) return null;
        var loan = SampleDataCatalog.Loans.SingleOrDefault(x => SampleDataCatalog.StableId("LoanAccount", x.AccountNumber) == accountId); if (loan is null) return null; var rowId = SampleDataCatalog.StableId("DemandRow", $"{previous.Key}:{loan.Key}"); var row = await db.DemandRows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rowId && x.DemandSnapshotId == demand.Id && x.DemandRunRevisionId == revision.Id && x.AccountId == accountId && x.LineOfBusinessId == scope.LineOfBusinessId && x.Status == DemandRowStatus.Completed, ct); if (row is null || !scope.BranchIds.Contains(row.BranchId)) return null;
        try { await ownership.RequireAsync("DemandRow", $"{previous.Key}:{loan.Key}", row.Id, ct); } catch (SampleDataCollisionException) { return null; }
        return new PreviousDemandResult(row.Arrears, $"{SampleDataCatalog.DatasetKey}|ROW|{previous.Key}|{accountId:N}");
    }
}
```

The source-port code must retain the `scope.BranchIds.Contains` predicate in `ReadAsync`, exact branch-set comparison in `SamplePreviousDemandPort`, sequential null checks before using snapshot IDs, and `PeriodCutoffUtc` conversion. Do not replace these with a broad fixture-branch query or UTC end-of-day construction.

## Task 6 — Register the fixture only for both flags in Accounts Service

### MODIFY `Finnova.Service/DependencyInjection.cs`

Add the normal non-fixture registration next to the existing Demand Processing port registrations:

```csharp
services.AddScoped<IPreviousDemandPort, NullPreviousDemandPort>();
```

Add the corresponding using with the existing Demand Processing usings:

```csharp
using Finnova.Service.DemandProcessing.Adapters;
```

### CREATE `Finnova.Service/DemandProcessing/SampleData/DemandProcessingSampleDataServiceCollectionExtensions.cs`

```csharp
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Finnova.Service.DemandProcessing.SampleData;

public static class DemandProcessingSampleDataServiceCollectionExtensions
{
    public static IServiceCollection AddDemandProcessingSampleData(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var useAdapters = configuration.GetValue<bool>("DemandProcessing:UseSampleAdapters"); var seed = configuration.GetValue<bool>("DemandProcessing:SeedSampleDataOnStartup");
        if (!useAdapters && !seed) return services;
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) throw new InvalidOperationException("Demand sample data is allowed only in Development or Testing.");
        if (!useAdapters || !seed) return services;
        services.AddScoped<SampleOwnershipStore>(); services.AddScoped<SampleDataSeeder>(); services.AddScoped<IAccountSourcePort, SampleAccountSourcePort>(); services.AddScoped<IFinalizedSchedulePort, SampleFinalizedSchedulePort>(); services.AddScoped<IAllocationEventPort, SampleAllocationEventPort>(); services.AddScoped<IPreviousDemandPort, SamplePreviousDemandPort>(); services.AddHostedService<SampleDataStartupSeeder>(); return services;
    }
}
```

### CREATE `Finnova.Service/DemandProcessing/SampleData/SampleDataStartupSeeder.cs`

```csharp
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finnova.Service.DemandProcessing.SampleData;

public sealed class SampleDataStartupSeeder(IServiceScopeFactory scopes, ILogger<SampleDataStartupSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>(); await db.Database.MigrateAsync(cancellationToken); var fingerprint = await scope.ServiceProvider.GetRequiredService<SampleDataSeeder>().SeedAsync(cancellationToken); logger.LogInformation("Demand sample startup seed completed with {Fingerprint}.", fingerprint);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### MODIFY `Finnova.AccountsService/Program.cs`

Add this directive with the other `using` directives at the top of the file, before `var builder`:

```csharp
using Finnova.Service.DemandProcessing.SampleData;
```

Immediately after the existing call to `builder.Services.AddFinnovaServices(builder.Configuration, FinnovaServiceModules.ApplicationProcessing);`, add only this statement:

```csharp
builder.Services.AddDemandProcessingSampleData(builder.Configuration, builder.Environment);
```

Do not put a using directive after a top-level statement. Do not add this call to UA or SystemAdmin. Production ports remain active for all partial configurations because the extension returns before adding fixture registrations.

## Task 7 — Apply existing migrations only in the later isolated profile

Do not create or modify an EF migration. The implementer applies existing migrations with:

```powershell
dotnet ef database update --project Finnova.Repository --startup-project Finnova.AccountsService --environment Testing
```

The future ignored Development/Testing configuration is:

```json
{
  "DemandProcessing": {
    "UseSampleAdapters": true,
    "SeedSampleDataOnStartup": true,
    "SampleDataKey": "SAMPLE-DP-V1",
    "SampleAsOfDate": "2026-03-31",
    "BusinessTimezone": "Asia/Kolkata"
  }
}
```

The configuration is not created during this authoring step and must never be committed to a production profile.

## Task 8 — Add complete pure, catalog, and parser tests

### CREATE `Finnova.Tests/DemandProcessing/SampleDataCatalogTests.cs`

```csharp
using FsCheck;
using FsCheck.Xunit;
using Finnova.Service.DemandProcessing.SampleData;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataCatalogTests
{
    [Fact] public void CatalogValidationAcceptsTheCompleteFixture() => SampleDataCatalog.Validate();
    [Fact] public void CatalogHasSixPeriodsEightBranchesAndSevenLoans() { Assert.Equal(6, SampleDataCatalog.Periods.Count); Assert.Equal(8, SampleDataCatalog.Branches.Count); Assert.Equal(7, SampleDataCatalog.Loans.Count); }
    [Fact] public void EveryBranchHasExplicitCityAndZone() => Assert.All(SampleDataCatalog.Branches, branch => { var city = Assert.Single(SampleDataCatalog.Locations.Where(x => x.Code == branch.CityCode)); var zone = Assert.Single(SampleDataCatalog.Locations.Where(x => x.Code == branch.ZoneCode)); Assert.Equal(3, city.Level); Assert.Equal(4, zone.Level); Assert.Equal(city.Code, zone.ParentCode); });
    [Fact] public void EmptyBranchAndCustomerAreIntentional() { Assert.Contains(SampleDataCatalog.Branches, x => x.Code == "MDU-B02"); Assert.Contains(SampleDataCatalog.Customers, x => x.Code == "CUSTDP000008"); Assert.DoesNotContain(SampleDataCatalog.Loans, x => x.BranchCode == "MDU-B02"); Assert.DoesNotContain(SampleDataCatalog.Loans, x => x.CustomerKey == "C008"); }
    [Fact] public void InvalidReceiptCasesArePresent() { Assert.Contains(SampleDataCatalog.Receipts, x => x.Dataset == "OTHER-DATASET"); Assert.Contains(SampleDataCatalog.Receipts, x => x.Description.Split('|').Length != 11); Assert.Contains(SampleDataCatalog.Receipts, x => x.PersistedAmount.HasValue); Assert.Contains(SampleDataCatalog.Receipts, x => x.Amount < 0m); }
    [Property(MaxTest = 100)] public Property StableIdsAreRepeatable(NonEmptyString value) => (SampleDataCatalog.StableId("test", value.Get) == SampleDataCatalog.StableId("test", value.Get)).ToProperty();
    [Property(MaxTest = 100)] public Property CutoffIsAfterLocalStart(PositiveInt day) { var date = new DateOnly(2026, 3, Math.Min(day.Get, 28)); return (SampleDataCatalog.CutoffUtc(date) > date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToProperty(); }
}
```

### CREATE `Finnova.Tests/DemandProcessing/SampleReceiptParserTests.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DemandProcessing.SampleData;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleReceiptParserTests
{
    private static (Transaction Row, Guid AccountId) Valid(string kind = "PART", decimal amount = 100m, DateTime? date = null)
    {
        var accountId = SampleDataCatalog.StableId("Account", "LN-DP-0001"); var reference = "R-VALID"; var description = $"SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT|LN-DP-0001|2026-03|{kind}|{amount:0.00}|Deposit|Completed|{reference}";
        return (new Transaction { Id = SampleDataCatalog.StableId("Transaction", reference), AccountId = accountId, Type = TransactionType.Deposit, Status = TransactionStatus.Completed, Amount = amount, ReferenceNumber = reference, Description = description, TransactionDate = date ?? new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) }, accountId);
    }

    [Theory] [InlineData("FULL", 100)] [InlineData("PART", 25)] [InlineData("OVER", 125)] public void PositiveKindsCreateEvents(string kind, decimal amount) { var (row, account) = Valid(kind, amount); Assert.NotNull(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Fact] public void NoneWithZeroCreatesAnEvent() { var (row, account) = Valid("NONE", 0m); Assert.NotNull(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Theory] [InlineData("SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT")] [InlineData("SAMPLE-DP|V1|OTHER|RECEIPT|LN-DP-0001|2026-03|PART|100.00|Deposit|Completed|R")] public void MalformedOrWrongDatasetIsRejected(string description) { var (row, account) = Valid(); row.Description = description; Assert.Null(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Fact] public void PersistedAmountMustMatchDescription() { var (row, account) = Valid(); row.Amount = 99m; Assert.Equal("PERSISTED_AMOUNT", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
    [Fact] public void NullAndUnsafeReferencesAreRejected() { var (row, account) = Valid(); row.ReferenceNumber = null; Assert.Equal("REFERENCE", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); row.ReferenceNumber = "R\nINJECT"; Assert.Equal("ENTITY_PAYLOAD", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
    [Fact] public void FutureBusinessDateIsRejected() { var (row, account) = Valid(date: SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate).AddTicks(1)); Assert.Equal("AFTER_CUTOFF", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
}
```

### CREATE `Finnova.Tests/DemandProcessing/SampleDataCompositionTests.cs`

```csharp
using Finnova.Service.DemandProcessing.SampleData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataCompositionTests
{
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = typeof(SampleDataCompositionTests).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static IHostEnvironment Environment(string name) => new TestEnvironment { EnvironmentName = name };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PartialOrDisabledFlagsRegisterNoFixtureServices(bool adapters, bool seed)
    {
        var services = new ServiceCollection(); var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DemandProcessing:UseSampleAdapters"] = adapters.ToString(), ["DemandProcessing:SeedSampleDataOnStartup"] = seed.ToString() }).Build();
        services.AddDemandProcessingSampleData(configuration, Environment("Testing"));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(SampleAccountSourcePort)); Assert.DoesNotContain(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(SampleDataStartupSeeder));
    }

    [Fact]
    public void BothFlagsRegisterFixtureServices()
    {
        var services = new ServiceCollection(); var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DemandProcessing:UseSampleAdapters"] = "true", ["DemandProcessing:SeedSampleDataOnStartup"] = "true" }).Build();
        services.AddDemandProcessingSampleData(configuration, Environment("Testing"));
        Assert.Contains(services, x => x.ServiceType == typeof(SampleAccountSourcePort)); Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(SampleDataStartupSeeder));
    }

    [Theory] [InlineData(true, false)] [InlineData(false, true)] public void AnyProductionFlagIsRejected(bool adapters, bool seed)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DemandProcessing:UseSampleAdapters"] = adapters.ToString(), ["DemandProcessing:SeedSampleDataOnStartup"] = seed.ToString() }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddDemandProcessingSampleData(configuration, Environment(Environments.Production)));
    }
}
```

## Task 9 — Add SQL Server seeder and exact-history tests

### MODIFY `Finnova.Tests/Finnova.Tests.csproj`

Add this package reference inside the existing package `ItemGroup`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
```

### CREATE `Finnova.Tests/DemandProcessing/SampleDataSqlTests.cs`

```csharp
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Configuration;
using Finnova.Service.DemandProcessing.SampleData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataSqlTests
{
    private static string ConnectionString()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FINNOVA_SAMPLE_SQL_CONNECTION") ?? throw new InvalidOperationException("FINNOVA_SAMPLE_SQL_CONNECTION must point to a SQL Server instance."); var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"FinnovaSample_{Guid.NewGuid():N}" }; return builder.ConnectionString;
    }

    private static FinnovaDbContext Context(string connection) => new(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(connection).Options);
    private static IOptions<DemandProcessingOptions> Options() => Microsoft.Extensions.Options.Options.Create(new DemandProcessingOptions { SampleDataKey = SampleDataCatalog.DatasetKey, SampleAsOfDate = SampleDataCatalog.AsOfDate, BusinessTimezone = "Asia/Kolkata" });
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
}
```

Every test method remains inside `SampleDataSqlTests`; do not split methods after the closing brace or create another file with the same path.

## Task 10 — Add one complete Accounts-host SQL factory and flow tests

### CREATE `Finnova.Tests/DemandProcessing/SampleDemandProcessingSqlHostTests.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.SampleData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDemandProcessingSqlHostTests : IAsyncLifetime
{
    private readonly SampleAccountsFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync() { _client = _factory.CreateClient(); await _factory.InitializeDatabaseAsync(); }
    public async Task DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }

    [Fact]
    public async Task AuthenticatedMarchFlowUsesAllFixtureBranchesAndTotals()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.Token(allBranches: true)); var lob = SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey); var response = await _client.PostAsJsonAsync("/api/demand-processing/runs", new { lineOfBusinessId = lob, periodYear = 2026, periodMonth = 3, frequency = "Monthly", idempotencyKey = "sample-march" }); Assert.True(response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK); var body = await response.Content.ReadAsStringAsync(); Assert.Contains("runId", body, StringComparison.OrdinalIgnoreCase); await _factory.WaitForCompletionAsync(); await using var scope = _factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>(); var run = await db.DemandRuns.SingleAsync(x => x.LineOfBusinessId == lob && x.PeriodYear == 2026 && x.PeriodMonth == 3); Assert.Equal(7, run.RowCount); Assert.Equal(8, run.BatchCount); Assert.Equal(48500m, run.CurrentDueTotal); Assert.Equal(18000m, run.ArrearsTotal); Assert.Equal(0m, run.TotalCharges); Assert.Equal(66500m, run.TotalDemand); Assert.Equal(1, await db.DemandBatches.CountAsync(x => x.DemandRunId == run.Id && x.Status == Finnova.Models.Domain.Enums.DemandBatchStatus.NoDemand));
    }

    [Fact]
    public async Task BranchScopedRunDoesNotReadOtherFixtureBranches()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.Token(allBranches: false, branch: SampleDataCatalog.StableId("Location", "CHN-B01"))); var response = await _client.PostAsJsonAsync("/api/demand-processing/runs", new { lineOfBusinessId = SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey), branchId = SampleDataCatalog.StableId("Location", "CHN-B01"), periodYear = 2026, periodMonth = 3, frequency = "Monthly", idempotencyKey = "sample-branch" }); Assert.True(response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK); await _factory.WaitForCompletionAsync(); await using var scope = _factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>(); var run = await db.DemandRuns.OrderByDescending(x => x.RequestedAtUtc).FirstAsync(x => x.PeriodYear == 2026 && x.PeriodMonth == 3); Assert.All(await db.DemandRows.Where(x => x.DemandRunRevisionId == SampleDataCatalog.StableId("DemandRevision", "2026-03")).ToListAsync(), row => Assert.Equal(SampleDataCatalog.StableId("Location", "CHN-B01"), row.BranchId));
    }

    [Fact] public async Task MissingTokenRemainsUnauthorized() { var response = await _factory.CreateClient().GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }

    public sealed class SampleAccountsFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.DemandProcessingController>
    {
        private readonly string _connection; private readonly string _jwtKey;
        public SampleAccountsFactory() { _jwtKey = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY") ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY is required."); var baseConnection = Environment.GetEnvironmentVariable("FINNOVA_SAMPLE_SQL_CONNECTION") ?? throw new InvalidOperationException("FINNOVA_SAMPLE_SQL_CONNECTION is required."); var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"FinnovaHostSample_{Guid.NewGuid():N}" }; _connection = builder.ConnectionString; }
        protected override void ConfigureWebHost(IWebHostBuilder builder) { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:FinnovaConnection"] = _connection, ["Jwt:Issuer"] = "Finnova", ["Jwt:Audience"] = "FinnovaTests", ["Jwt:SigningKey"] = _jwtKey, ["DemandProcessing:UseSampleAdapters"] = "true", ["DemandProcessing:SeedSampleDataOnStartup"] = "true", ["DemandProcessing:SampleDataKey"] = SampleDataCatalog.DatasetKey, ["DemandProcessing:SampleAsOfDate"] = "2026-03-31", ["DemandProcessing:BusinessTimezone"] = "Asia/Kolkata" })); builder.ConfigureServices(services => { services.RemoveAll<DbContextOptions<FinnovaDbContext>>(); services.RemoveAll<IDbContextOptionsConfiguration<FinnovaDbContext>>(); services.AddDbContext<FinnovaDbContext>(o => o.UseSqlServer(_connection)); }); }
        public async Task InitializeDatabaseAsync() { await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>(); await db.Database.MigrateAsync(); await scope.ServiceProvider.GetRequiredService<SampleDataSeeder>().SeedAsync(); }
        public string Token(bool allBranches, Guid? branch = null) { var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "sample-test"), new(ClaimTypes.Role, "SystemAdmin"), new("lob", SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey).ToString()) }; if (allBranches) foreach (var item in SampleDataCatalog.Branches) claims.Add(new Claim("branch", SampleDataCatalog.StableId("Location", item.Code).ToString())); else if (branch.HasValue) claims.Add(new Claim("branch", branch.Value.ToString())); var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey)), SecurityAlgorithms.HmacSha256)); return new JwtSecurityTokenHandler().WriteToken(token); }
        public async Task WaitForCompletionAsync() { for (var i = 0; i < 30; i++) { await Task.Delay(100); await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>(); if (await db.DemandRuns.AnyAsync(x => x.PeriodYear == 2026 && x.PeriodMonth == 3 && x.Status == Finnova.Models.Domain.Enums.DemandRunStatus.Completed)) return; } throw new TimeoutException("Demand sample host run did not complete."); }
        protected override void Dispose(bool disposing) { if (disposing) { using var db = new FinnovaDbContext(new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(_connection).Options); db.Database.EnsureDeleted(); } base.Dispose(disposing); }
    }
}
```

The factory enables both flags, explicitly applies existing migrations, explicitly calls the seeder, and drops only its generated database. Its source file is declared once. If the host startup seeder also runs, the second seed is intentionally idempotent.

## Task 11 — Verify existing gateway/UI composition without source changes

Do not create or modify a frontend file. After the later implementation is deployed to a Development/Testing profile, verify that the existing Finnova-UI service calls the existing gateway alias and that the current Demand Processing page displays the seeded history and March output. No UI route, model, label, localization, or RTL change is part of this fixture.

## Authoring boundary

The current work changes only `requirements.md`, `design.md`, `tasks.md`, and `.config.kiro`/`.config` in this specification directory. Do not apply any task block, create a migration, modify application source, modify the database, modify Finnova-UI, or run a build or test while authoring this specification.
