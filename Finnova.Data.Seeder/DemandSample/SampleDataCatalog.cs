using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Finnova.Data.Seeder.DemandSample;

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
    public const int ReferenceLocationMaxLevel = 3;
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

    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
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