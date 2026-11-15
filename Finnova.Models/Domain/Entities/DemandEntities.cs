using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class DemandRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LineOfBusinessId { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string BusinessTimezone { get; set; } = "Asia/Kolkata";
    public string LogicalScopeKey { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string PayloadFingerprint { get; set; } = string.Empty;
    public string? ProcessingCutoffReference { get; set; }
    public DemandRunStatus Status { get; set; } = DemandRunStatus.Accepted;
    public BillingStageStatus BillingStatus { get; set; } = BillingStageStatus.Pending;
    public DemandSource Source { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int CurrentRevisionNumber { get; set; } = 1;
    public Guid? BillingStageRunId { get; set; }
    public Guid? BillingSnapshotId { get; set; }
    public Guid? DemandSnapshotId { get; set; }
    public int RowCount { get; set; }
    public int BatchCount { get; set; }
    public decimal CurrentDueTotal { get; set; }
    public decimal ArrearsTotal { get; set; }
    public decimal OdiTotal { get; set; }
    public decimal OtherChargesTotal { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalDemand { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
    public List<DemandRunBranchScope> BranchScopes { get; set; } = new();
    public List<DemandRunRevision> Revisions { get; set; } = new();
}

public sealed class DemandRunBranchScope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid BranchId { get; set; }
    public int SortOrder { get; set; }
    public DemandRun DemandRun { get; set; } = null!;
}

public sealed class DemandRunRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunId { get; set; }
    public int RevisionNumber { get; set; }
    public int? SupersedesRevisionNumber { get; set; }
    public DemandRunStatus Status { get; set; } = DemandRunStatus.Accepted;
    public Guid? BillingSnapshotId { get; set; }
    public Guid? DemandSnapshotId { get; set; }
    public string? ProcessingCutoffReference { get; set; }
    public int RowCount { get; set; }
    public int BatchCount { get; set; }
    public decimal CurrentDueTotal { get; set; }
    public decimal ArrearsTotal { get; set; }
    public decimal OdiTotal { get; set; }
    public decimal OtherChargesTotal { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalDemand { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
    public DemandRun DemandRun { get; set; } = null!;
    public List<DemandRow> Rows { get; set; } = new();
    public List<DemandBatch> Batches { get; set; } = new();
}

public sealed class BillingStageRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunId { get; set; }
    public int RevisionNumber { get; set; }
    public string LogicalBillingScopeKey { get; set; } = string.Empty;
    public BillingStageStatus Status { get; set; } = BillingStageStatus.Pending;
    public string? ProcessingCutoffReference { get; set; }
    public string? ScheduleReference { get; set; }
    public string AllocationPolicyReference { get; set; } = string.Empty;
    public string ChargePolicyReference { get; set; } = string.Empty;
    public ChargePolicyKind ChargePolicyKind { get; set; } = ChargePolicyKind.Default;
    public string? JournalBatchReference { get; set; }
    public string? JournalCheckSummary { get; set; }
    public Guid? BillingSnapshotId { get; set; }
    public string? BillingSnapshotVersion { get; set; }
    public int EligibleAccountCount { get; set; }
    public int SuccessfulResultCount { get; set; }
    public int FailedResultCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public byte[] Version { get; set; } = Array.Empty<byte>();
    public DemandRun DemandRun { get; set; } = null!;
    public List<BillingPeriodResult> Results { get; set; } = new();
    public List<BillingAllocationEvidence> AllocationEvidence { get; set; } = new();
}

public sealed class BillingPeriodResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillingStageRunId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountDisplay { get; set; } = string.Empty;
    public string CustomerDisplay { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid BranchId { get; set; }
    public string AccountStatusAtCutoff { get; set; } = string.Empty;
    public bool IsEligible { get; set; }
    public BillingPeriodResultStatus Status { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? ScheduleEntryId { get; set; }
    public string? ScheduleReference { get; set; }
    public decimal ScheduledPrincipal { get; set; }
    public decimal ScheduledInterestOrEmi { get; set; }
    public decimal ScheduledInstallment { get; set; }
    public decimal CurrentDue { get; set; }
    public decimal Arrears { get; set; }
    public decimal Odi { get; set; }
    public decimal OtherCharges { get; set; }
    public string AllocationPolicyReference { get; set; } = string.Empty;
    public string ChargePolicyReference { get; set; } = string.Empty;
    public string? JournalReference { get; set; }
    public string? JournalCheckSummary { get; set; }
    public string? ProcessingCutoffReference { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public BillingStageRun BillingStageRun { get; set; } = null!;
}

public sealed class BillingAllocationEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillingStageRunId { get; set; }
    public Guid BillingPeriodResultId { get; set; }
    public AllocationEventType EventType { get; set; }
    public Guid SourceEventId { get; set; }
    public string? SourceEventVersion { get; set; }
    public DateTime? EventEffectiveUtc { get; set; }
    public string AllocationPolicyReference { get; set; } = string.Empty;
    public int AllocationSequence { get; set; }
    public decimal AllocatedAmount { get; set; }
    public string TargetComponent { get; set; } = string.Empty;
    public AllocationOutcome Outcome { get; set; }
    public string SafeReason { get; set; } = string.Empty;
    public string ApplicationKey { get; set; } = string.Empty;
    public BillingPeriodResult BillingPeriodResult { get; set; } = null!;
}

public sealed class BillingSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillingStageRunId { get; set; }
    public int RevisionNumber { get; set; }
    public string Version { get; set; } = string.Empty;
    public string LogicalScopeKey { get; set; } = string.Empty;
    public string? ProcessingCutoffReference { get; set; }
    public int ResultCount { get; set; }
    public decimal CurrentDueTotal { get; set; }
    public decimal ArrearsTotal { get; set; }
    public decimal OdiTotal { get; set; }
    public decimal OtherChargesTotal { get; set; }
    public string? JournalBatchReference { get; set; }
    public string? JournalCheckSummary { get; set; }
    public bool IsPublished { get; set; }
    public string ContentFingerprint { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime? PublishedAtUtc { get; set; }
    public BillingStageRun BillingStageRun { get; set; } = null!;
}

public sealed class DemandSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid BillingSnapshotId { get; set; }
    public string Version { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public int BatchCount { get; set; }
    public decimal TotalDemand { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DemandRun DemandRun { get; set; } = null!;
}

public sealed class DemandRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunRevisionId { get; set; }
    public Guid DemandSnapshotId { get; set; }
    public Guid DemandBatchId { get; set; }
    public Guid BillingPeriodResultId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid BranchId { get; set; }
    public string AccountDisplay { get; set; } = string.Empty;
    public string CustomerDisplay { get; set; } = string.Empty;
    public DemandRowStatus Status { get; set; } = DemandRowStatus.Completed;
    public decimal CurrentDue { get; set; }
    public decimal Arrears { get; set; }
    public decimal Odi { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalDemand { get; set; }
    public bool IsZeroDemand { get; set; }
    public string? ScheduleReference { get; set; }
    public string? ProcessingCutoffReference { get; set; }
    public string OrderingKey { get; set; } = string.Empty;
    public DemandRunRevision Revision { get; set; } = null!;
    public DemandBatch Batch { get; set; } = null!;
}

public sealed class DemandBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandRunRevisionId { get; set; }
    public Guid DemandSnapshotId { get; set; }
    public Guid DemandRunId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DemandBatchStatus Status { get; set; } = DemandBatchStatus.NotReady;
    public int RowCount { get; set; }
    public decimal CurrentDueTotal { get; set; }
    public decimal ArrearsTotal { get; set; }
    public decimal OdiTotal { get; set; }
    public decimal OtherChargesTotal { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalDemand { get; set; }
    public string SpoolIdempotencyKey { get; set; } = string.Empty;
    public string? LastSpoolActorId { get; set; }
    public DemandSource? LastSpoolSource { get; set; }
    public DateTime? LastSpooledAtUtc { get; set; }
    public DemandFailureClass? FailureClass { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
    public DemandRunRevision Revision { get; set; } = null!;
    public List<DemandRow> Rows { get; set; } = new();
}

public sealed class DemandFailure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DemandRunId { get; set; }
    public Guid? DemandBatchId { get; set; }
    public Guid? AccountId { get; set; }
    public DemandFailureClass FailureClass { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DemandProcessingLease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LogicalScopeKey { get; set; } = string.Empty;
    public Guid DemandRunId { get; set; }
    public DateTime AcquiredAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public byte[] Version { get; set; } = Array.Empty<byte>();
}

public sealed class DemandAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DemandRunId { get; set; }
    public int? RevisionNumber { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DemandBatchId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public DemandSource Source { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string OutcomeCode { get; set; } = string.Empty;
    public string SafeReferences { get; set; } = string.Empty;
}
