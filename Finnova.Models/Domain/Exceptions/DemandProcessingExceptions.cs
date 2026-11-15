namespace Finnova.Models.Domain.Exceptions;

public abstract class DemandProcessingException : Exception
{
    protected DemandProcessingException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
    public string ErrorCode { get; }
}

public sealed class DemandValidationException(string message, string code = "ERR-DEMAND-VALIDATION") : DemandProcessingException(message, code);
public sealed class DemandBillingIncompleteException(string message = "Billing is incomplete for the requested scope and period.") : DemandProcessingException(message, "ERR-DEMAND-BILLING-INCOMPLETE");
public sealed class DemandBillingScopeMismatchException(string message = "The Billing scope does not match the Demand scope.") : DemandProcessingException(message, "ERR-DEMAND-BILLING-SCOPE");
public sealed class DemandScheduleIncompleteException(string message = "The selected finalized schedule value is unavailable.") : DemandProcessingException(message, "ERR-BILLING-SCHEDULE-INCOMPLETE");
public sealed class DemandAllocationConflictException(string message = "The allocation evidence is inconsistent.") : DemandProcessingException(message, "ERR-BILLING-ALLOCATION-CONFLICT");
public sealed class DemandJournalIncompleteException(string message = "Required Accounting/GL journal checks did not pass.") : DemandProcessingException(message, "ERR-BILLING-JOURNAL-INCOMPLETE");
public sealed class DemandSnapshotBlockedException(string message = "Snapshot publication is blocked by a required check.") : DemandProcessingException(message, "ERR-BILLING-SNAPSHOT-BLOCKED");
public sealed class DemandNotFoundException(Guid id) : DemandProcessingException($"Demand record '{id}' was not found.", "ERR-DEMAND-NOT-FOUND");
public sealed class DemandIdempotencyConflictException(string message = "The idempotency key was already used with a different request.") : DemandProcessingException(message, "ERR-DEMAND-IDEMPOTENCY-CONFLICT");
public sealed class DemandConcurrencyException(string message = "Another Demand Processing operation owns this scope.") : DemandProcessingException(message, "ERR-DEMAND-CONCURRENT");
public sealed class DemandDependencyException(string message) : DemandProcessingException(message, "ERR-DEMAND-DEPENDENCY");
public sealed class DemandDataQualityException(string message) : DemandProcessingException(message, "ERR-DEMAND-DATA-QUALITY");
public sealed class DemandReconciliationException(string message) : DemandProcessingException(message, "ERR-DEMAND-RECONCILIATION");
public sealed class DemandSpoolNotReadyException(string message = "The Branch Batch is not ReadyToSpool.") : DemandProcessingException(message, "ERR-DEMAND-SPOOL-NOT-READY");
public sealed class DemandPdfExportException(string message) : DemandProcessingException(message, "ERR-DEMAND-PDF-EXPORT");
