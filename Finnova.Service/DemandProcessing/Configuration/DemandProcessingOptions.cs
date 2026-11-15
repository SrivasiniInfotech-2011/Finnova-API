namespace Finnova.Service.DemandProcessing.Configuration;

public sealed class DemandProcessingOptions
{
    public const string SectionName = "DemandProcessing";
    public string[] Frequencies { get; set; } = ["Monthly"];
    public string DefaultFrequency { get; set; } = "Monthly";
    public string BusinessTimezone { get; set; } = "Asia/Kolkata";
    public int DefaultDayCountBasis { get; set; } = 365;
    public decimal? OdiCap { get; set; }
    public string ChargePolicy { get; set; } = "Default";
    public string[] RunPolicies { get; set; } = ["DemandRun"];
    public string[] ReadPolicies { get; set; } = ["DemandOutputRead"];
    public string[] SpoolPolicies { get; set; } = ["DemandSpool"];
    public string[] AdminRoles { get; set; } = ["SystemAdmin"];
    public int PollingSeconds { get; set; } = 10;
    public int MaxPageSize { get; set; } = 200;
    public string PdfRenderer { get; set; } = "QuestPdf";
    public string? RetentionPolicy { get; set; }
    public string? JournalAdapter { get; set; }
    public string? BillingCompletionAdapter { get; set; }
}
