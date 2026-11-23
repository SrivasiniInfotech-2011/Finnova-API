using Finnova.Data.Seeder.DemandSample;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Tests.DemandProcessing.Support;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleReceiptParserTests
{
    private static (Transaction Row, Guid AccountId) Valid(string kind = "PART", decimal amount = 100m, DateTime? date = null)
    {
        var accountId = SampleDataCatalog.StableId("Account", "LN-DP-0001"); var reference = "R-VALID"; var description = $"SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT|LN-DP-0001|2026-03|{kind}|{amount:0.00}|Deposit|Completed|{reference}";
        return (new Transaction { Id = SampleDataCatalog.StableId("Transaction", reference), AccountId = accountId, Type = TransactionType.Deposit, Status = TransactionStatus.Completed, Amount = amount, ReferenceNumber = reference, Description = description, TransactionDate = date ?? new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) }, accountId);
    }

    [Theory][InlineData("FULL", 100)][InlineData("PART", 25)][InlineData("OVER", 125)] public void PositiveKindsCreateEvents(string kind, decimal amount) { var (row, account) = Valid(kind, amount); Assert.NotNull(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Fact] public void NoneWithZeroCreatesAnEvent() { var (row, account) = Valid("NONE", 0m); Assert.NotNull(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Theory][InlineData("SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT")][InlineData("SAMPLE-DP|V1|OTHER|RECEIPT|LN-DP-0001|2026-03|PART|100.00|Deposit|Completed|R")] public void MalformedOrWrongDatasetIsRejected(string description) { var (row, account) = Valid(); row.Description = description; Assert.Null(SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).Event); }
    [Fact] public void PersistedAmountMustMatchDescription() { var (row, account) = Valid(); row.Amount = 99m; Assert.Equal("PERSISTED_AMOUNT", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
    [Fact] public void NullAndUnsafeReferencesAreRejected() { var (row, account) = Valid(); row.ReferenceNumber = null; Assert.Equal("REFERENCE", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); row.ReferenceNumber = "R\nINJECT"; Assert.Equal("ENTITY_PAYLOAD", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
    [Fact] public void FutureBusinessDateIsRejected() { var (row, account) = Valid(date: SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate).AddTicks(1)); Assert.Equal("AFTER_CUTOFF", SampleReceiptParser.Parse(account, "LN-DP-0001", row, new DateOnly(2026, 3, 1), SampleDataCatalog.CutoffUtc(SampleDataCatalog.AsOfDate)).ReasonCode); }
}