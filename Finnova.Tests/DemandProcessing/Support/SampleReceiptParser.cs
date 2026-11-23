using System.Globalization;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

using Finnova.Data.Seeder.DemandSample;

namespace Finnova.Tests.DemandProcessing.Support;

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