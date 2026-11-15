namespace Finnova.Models.Domain.Enums;

public enum DemandRunStatus
{
    Accepted,
    BillingPending,
    BillingRunning,
    BillingIncomplete,
    BillingFailed,
    BillingSucceeded,
    DemandGenerating,
    Completed,
    PartiallyFailed,
    Failed,
    Deferred,
    Superseded
}

public enum BillingStageStatus
{
    Pending,
    Queued,
    Running,
    Incomplete,
    Failed,
    Cancelled,
    Succeeded,
    RetryPending,
    Superseded
}

public enum BillingPeriodResultStatus { Succeeded, Excluded, Failed, Incomplete }
public enum DemandRowStatus { Completed, Excluded, Invalid }
public enum DemandBatchStatus { NotReady, ReadyToSpool, Spooled, NoDemand, Failed }
public enum DemandFailureClass { BillingIncomplete, Dependency, Transient, Permanent, DataQuality, Reconciliation, Authorization, Idempotency, Calculation, Spooling, PdfExport }
public enum DemandSource { User, Automizer }
public enum AllocationEventType { Receipt, PartPayment, Waiver, Reversal, Adjustment, Suspense, OtherCharge }
public enum AllocationOutcome { Applied, Excluded, Deferred, Reversed, Rejected }
public enum ChargePolicyKind { Default, ConfiguredAdapter }
