using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.DemandProcessing.Billing;

public static class BillingPublicationGate
{
    public static void RequirePublishedInputs(BillingCompletion completion, DemandScopeContract expected, int eligibleCount, int successfulCount, JournalCheckResult journal)
    {
        BillingRules.RequireBillingSnapshot(completion, expected);
        if (eligibleCount != successfulCount) throw new DemandBillingIncompleteException("Not every eligible account has a successful Billing Period Result.");
        if (!BillingRules.JournalChecksPass(journal)) throw new DemandJournalIncompleteException();
        if (string.IsNullOrWhiteSpace(completion.SnapshotVersion)) throw new DemandSnapshotBlockedException("Billing Snapshot version is missing.");
    }
}
