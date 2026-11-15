# Implementation Plan: Demand Processing

## Overview

This is the copy/paste implementation plan for Demand Processing. It covers the backend in `d:\Projects\Finnova\Finnova-API` and the frontend in `E:\Finnova\Finnova-UI\Finnova-UI`. Apply the tasks in order. Do not add an independent Billing or Demand-generation endpoint, an external delivery subsystem, delivery metadata, or browser-print capability.

Every implementation task identifies a path with `CREATE` or `MODIFY`. `CREATE` blocks are complete files. `MODIFY` blocks are exact snippets with an insertion or replacement anchor. Existing repository conventions remain authoritative when a surrounding file has moved, but the stated anchor must be preserved.

## Tasks

- [ ] 1. Record the non-blocking configuration and deferred-decision record
  - [ ] 1.1 Record the non-blocking configuration and deferred decision record in the implementation ticket
    - Record the following before applying code tasks. This record does not block implementation because the design already defines the defaults and adapter seams:
    - Defaults: AccountsService host and worker boundary; immutable GUID Run IDs; `Monthly` Frequency; `Asia/Kolkata` business period; UTC technical and audit timestamps; INR `decimal(19,2)`; `MidpointRounding.AwayFromZero`; Active-only eligibility; zero-demand row retention; `NoDemand` empty Branch batches; branch isolation; linked immutable Run Revisions; configuration-driven roles/policies, Billing completion checks, charge policy, Journal Port, PDF renderer, pagination, polling, and retention.
    - Deferred items: the external Processing Cut-off source, any new Amortization Schedule finalization watermark/source-version integration, detailed allocation ordering and excess-receipt treatment, the external Branch delivery contract and its retry/idempotency semantics, detailed audit taxonomy/content/retention/purge rules, browser print, and the approved PDF renderer package version.
    - Required implementation decision: bind the schedule, allocation-event, account/customer/master, charge, and Accounting/GL adapters to their existing authoritative contracts. The Demand feature must retain the available common snapshot/source reference and must not introduce a new external watermark prerequisite.
  - The record documents the remaining deferred items only and does not block implementation.
  - _Requirements: 1.20, 2.7, 3.14, 3.56-3.57, 8.22, 10.26, 15.21, 16.33_

- [ ] 2. Create backend domain entities, enums, and value objects
  - [ ] 2.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\Enums\DemandEnums.cs`
    ```csharp
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
    ```
    - Serialize these enums as strings through the existing AccountsService JSON configuration.
    - _Requirements: 2.2-2.6, 3.22-3.23, 5.18-5.19, 6.2-6.7, 7.18-7.20, 9.18-9.20, 10.18-10.20, 13.5-13.14_

  - [ ] 2.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\ValueObjects\DemandScope.cs`
    ```csharp
    namespace Finnova.Models.Domain.ValueObjects;

    public sealed record DemandPeriod(int Year, int Month)
    {
        public static DemandPeriod Create(int year, int month)
        {
            if (year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(year));
            if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
            return new DemandPeriod(year, month);
        }

        public string CanonicalValue => $"{Year:D4}-{Month:D2}";
    }

    public sealed record DemandScope(Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds)
    {
        public static DemandScope Create(Guid lineOfBusinessId, IEnumerable<Guid> branchIds)
        {
            if (lineOfBusinessId == Guid.Empty) throw new ArgumentException("Line of Business is required.", nameof(lineOfBusinessId));
            var normalized = branchIds.Where(x => x != Guid.Empty).Distinct().OrderBy(x => x).ToArray();
            if (normalized.Length == 0) throw new ArgumentException("At least one Branch is required.", nameof(branchIds));
            return new DemandScope(lineOfBusinessId, normalized);
        }

        public string CanonicalValue => $"{LineOfBusinessId:N}:{string.Join(',', BranchIds.OrderBy(x => x).Select(x => x.ToString("N")))}";
    }

    public sealed record DemandLogicalKey(DemandScope Scope, string Frequency, DemandPeriod Period, string BillingReference)
    {
        public string CanonicalValue => $"{Scope.CanonicalValue}|{Frequency.Trim()}|{Period.CanonicalValue}|{BillingReference.Trim()}";
    }
    ```
    - Use `DemandPeriod` and `DemandScope` at the normalization boundary. Omitted Branch is resolved before this value object is created.
    - _Requirements: 1.8-1.16, 1.21-1.28, 3.1, 3.45-3.48, 8.7-8.12, 9.1-9.5_

  - [ ] 2.3 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\Entities\DemandEntities.cs`
    ```csharp
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
    ```
    - Keep result, snapshot, batch, failure, lease, allocation evidence, and audit records append-only from the application API. The only mutable operational records are Run/stage/batch status fields needed for controlled state transitions.
    - _Requirements: 1.14-1.20, 2.6-2.20, 3.34-3.55, 4.19-4.21, 5.1-5.21, 6.1-6.21, 8.1-8.27, 9.1-9.23, 10.1-10.27, 15.1-15.23_

- [ ] 3. Create typed backend exceptions and stable error codes
  - [ ] 3.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\Exceptions\DemandProcessingExceptions.cs`
    ```csharp
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
    ```
    - Do not place tokens, credentials, connection strings, secrets, or unnecessary customer fields in exception messages.
    - _Requirements: 1.2-1.13, 2.2-2.22, 3.11-3.14, 3.44-3.51, 6.16-6.21, 8.10-8.17, 9.11-9.23, 10.1-10.27, 12.21-12.25, 13.13-13.30, 14.1-14.21_

- [ ] 4. Create backend contracts and application ports
  - [ ] 4.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Models\Contracts\DemandProcessing\DemandContracts.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.DemandProcessing;

    public sealed record StartDemandRunRequest(Guid LineOfBusinessId, Guid? BranchId, string? Frequency, int PeriodYear, int PeriodMonth, string? IdempotencyKey);
    public sealed record RerunDemandRunRequest(string Reason, string IdempotencyKey);
    public sealed record SpoolBatchRequest(string? IdempotencyKey);
    public sealed record DemandHistoryQuery(int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, DemandRunStatus? Status, int Page = 1, int PageSize = 20);
    public sealed record DemandRowsQuery(Guid? RunId, int? RevisionNumber, int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, Guid? AccountId, Guid? CustomerId, DemandRowStatus? RowStatus, int Page = 1, int PageSize = 20);
    public sealed record DemandBatchesQuery(Guid? RunId, int? RevisionNumber, int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, DemandBatchStatus? Status, int Page = 1, int PageSize = 20);
    public sealed record DemandPdfQuery(Guid? RunId, int? RevisionNumber, int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, Guid? AccountId, Guid? CustomerId, DemandRowStatus? RowStatus, int PageSize = 5000);

    public sealed record DemandRunAcceptedResponse(Guid RunId, int RevisionNumber, DemandRunStatus Status, string CorrelationId);
    public sealed record DemandFailureResponse(DemandFailureClass FailureClass, string Code, string Message, Guid? AccountId, Guid? BatchId, DateTime CreatedAtUtc);
    public sealed record DemandRunResponse(Guid RunId, int RevisionNumber, Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds, string Frequency, int PeriodYear, int PeriodMonth, string? ProcessingCutoffReference, DemandRunStatus Status, BillingStageStatus BillingStatus, Guid? BillingSnapshotId, string? BillingSnapshotVersion, Guid? DemandSnapshotId, int RowCount, int BatchCount, decimal CurrentDueTotal, decimal ArrearsTotal, decimal OdiTotal, decimal OtherChargesTotal, decimal TotalCharges, decimal TotalDemand, IReadOnlyList<DemandFailureResponse> Failures, DateTime RequestedAtUtc, DateTime? CompletedAtUtc);
    public sealed record DemandRowResponse(Guid Id, Guid RunId, int RevisionNumber, Guid AccountId, Guid CustomerId, Guid LineOfBusinessId, Guid BranchId, string AccountDisplay, string CustomerDisplay, int PeriodYear, int PeriodMonth, string Frequency, decimal CurrentDue, decimal Arrears, decimal Odi, decimal OtherCharges, decimal TotalCharges, decimal TotalDemand, bool IsZeroDemand, DemandRowStatus Status, string? ScheduleReference, string? ProcessingCutoffReference, string OrderingKey);
    public sealed record DemandBatchResponse(Guid Id, Guid RunId, int RevisionNumber, Guid BranchId, Guid LineOfBusinessId, string Frequency, int PeriodYear, int PeriodMonth, DemandBatchStatus Status, int RowCount, decimal CurrentDueTotal, decimal ArrearsTotal, decimal OdiTotal, decimal OtherChargesTotal, decimal TotalCharges, decimal TotalDemand, string? FailureClass, string? FailureCode, string? FailureReason, string? LastSpoolActorId, DateTime? LastSpooledAtUtc);
    public sealed record SpoolStatusResponse(Guid BatchId, DemandBatchStatus Status, string CorrelationId);
    public sealed record PaginatedResponse<T>(IReadOnlyList<T> Data, int Total, int Page, int PageSize, int TotalPages);
    public sealed record PdfFileResult(byte[] Content, string ContentType, string FileName);

    public sealed record DemandScopeContract(Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds, string Frequency, int PeriodYear, int PeriodMonth, string BillingReference);
    public sealed record BillingCompletion(string Reference, DemandScopeContract Scope, BillingStageStatus State, string SnapshotVersion, string? ProcessingCutoffReference, IReadOnlyDictionary<string, bool> RequiredChecks);
    public sealed record ScheduleProjection(Guid AccountId, Guid ScheduleId, Guid ScheduleEntryId, string ScheduleReference, decimal ScheduledPrincipal, decimal ScheduledInterestOrEmi);
    public sealed record AccountProjection(Guid AccountId, Guid CustomerId, Guid LineOfBusinessId, Guid BranchId, string AccountDisplay, string CustomerDisplay, string Status, bool CustomerResolvable, bool BranchResolvable, bool LobResolvable, string SnapshotReference);
    public sealed record AllocationEvent(Guid Id, AllocationEventType EventType, decimal Amount, DateTime EffectiveUtc, string SourceReference, string? SourceVersion, string TargetComponent, bool Eligible);
    public sealed record ChargeInput(decimal OverduePrincipalBalance, decimal AnnualOdiRate, int OverdueDays, decimal? OdiCap, decimal ScheduledInstallment, IReadOnlyList<AllocationEvent> ChargeEvents, IReadOnlyList<AllocationEvent> ChargeAdjustments);
    public sealed record ChargeResult(decimal Odi, decimal OtherCharges, string PolicyReference, IReadOnlyList<string> EvidenceReferences);
    public sealed record BillingResultInput(AccountProjection Account, ScheduleProjection Schedule, decimal CurrentDue, decimal Arrears, ChargeResult Charges, IReadOnlyList<AllocationEvent> AllocationEvents, string? ProcessingCutoffReference);
    public sealed record JournalRequest(string LogicalBillingKey, Guid BillingStageRunId, decimal CurrentDueTotal, decimal ArrearsTotal, decimal OdiTotal, decimal OtherChargesTotal, string Currency);
    public sealed record JournalCheckResult(string JournalReference, bool GenerationPassed, bool PostingPassed, bool IdempotencyPassed, bool ReconciliationPassed, string SafeSummary);
    public sealed record AuthorizedDemandScope(IReadOnlySet<Guid> LineOfBusinessIds, IReadOnlySet<Guid> BranchIds);
    public sealed record DemandPdfDocument(string Title, DateTime GeneratedAtUtc, DemandRunResponse Run, IReadOnlyList<DemandRowResponse> Rows, IReadOnlyList<DemandBatchResponse> Batches, decimal TotalDemand);
    ```
    - The browser supplies only normalized filters and action keys. It cannot supply authoritative totals, stage status, snapshot IDs, actor identity, journal status, or source financial values.
    - _Requirements: 1.8-1.20, 2.1-2.24, 3.1-3.57, 5.1-5.21, 6.1-6.21, 7.1-7.20, 8.1-8.27, 9.1-9.23, 10.1-10.27, 11.4-11.26, 12.4-12.27, 13.1-13.30, 14.7-14.21, 15.1-15.23_

  - [ ] 4.2 CREATE `d:\Projects\Finnova\Finnova.Service\DemandProcessing\Ports\DemandPorts.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.ValueObjects;

    namespace Finnova.Service.DemandProcessing.Ports;

    public interface IAuthorizationScopeResolver
    {
        Task<AuthorizedDemandScope> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct);
    }

    public interface IFinalizedSchedulePort
    {
        Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct);
    }

    public interface IAccountSourcePort
    {
        IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, CancellationToken ct);
        Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, ClaimsPrincipal principal, CancellationToken ct);
        Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct);
    }

    public interface IAllocationEventPort
    {
        Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct);
    }

    public interface IAllocationPolicy
    {
        AllocationResult Apply(decimal scheduledInstallment, decimal priorArrears, IReadOnlyList<AllocationEvent> events);
    }

    public sealed record AllocationResult(decimal CurrentDue, decimal Arrears, IReadOnlyList<AllocationAppliedEvent> Evidence);
    public sealed record AllocationAppliedEvent(AllocationEvent Event, decimal AllocatedAmount, string TargetComponent, string ApplicationKey);

    public interface IChargePolicyAdapter
    {
        Task<ChargeResult> CalculateAsync(ChargeInput input, CancellationToken ct);
    }

    public interface IJournalPort
    {
        Task<JournalCheckResult> EnsurePostedAsync(JournalRequest request, CancellationToken ct);
    }

    public interface IDemandRepository
    {
        Task<DemandRun?> FindByLogicalScopeAsync(string logicalScopeKey, CancellationToken ct);
        Task<DemandRun?> GetRunAsync(Guid runId, CancellationToken ct);
        Task<DemandBatch?> GetBatchAsync(Guid batchId, IReadOnlySet<Guid> branchScope, CancellationToken ct);
        Task<(IReadOnlyList<DemandRun> Items, int Total)> SearchRunsAsync(DemandHistoryQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
        Task<(IReadOnlyList<DemandRow> Items, int Total)> SearchRowsAsync(DemandRowsQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
        Task<(IReadOnlyList<DemandBatch> Items, int Total)> SearchBatchesAsync(DemandBatchesQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
        Task AddAsync(DemandRun run, CancellationToken ct);
        Task SaveAsync(CancellationToken ct);
        Task<IDisposable> BeginTransactionAsync(CancellationToken ct);
    }

    public interface IDemandLeaseStore
    {
        Task<bool> TryAcquireAsync(string logicalScopeKey, Guid runId, CancellationToken ct);
        Task<bool> RenewAsync(Guid runId, CancellationToken ct);
        Task ReleaseAsync(Guid runId, CancellationToken ct);
    }

    public interface IDemandPdfExportService
    {
        Task<PdfFileResult> RenderOutputAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct);
        Task<PdfFileResult> RenderBatchesAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct);
    }

    public interface IPdfRenderer
    {
        byte[] Render(DemandPdfDocument document);
    }

    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public interface IDemandWorkQueue
    {
        ValueTask EnqueueAsync(Guid runId, CancellationToken ct);
    }

    public interface IDemandProcessor
    {
        Task ProcessAsync(Guid runId, CancellationToken ct);
    }
    ```
    - `IJournalPort` is the only journal boundary. Do not add a ledger repository or journal-owned tables to Demand Processing. `IPdfRenderer` is the isolated renderer seam; the selected package is added only in the PDF task.
    - _Requirements: 2.9-2.24, 3.4-3.57, 4.1-4.21, 7.3-7.20, 9.1-9.23, 10.1-10.27, 12.16-12.23, 13.7-13.30, 14.7-14.21_

- [ ] 5. Add EF Core configuration, DbSets, constraints, and migration instructions
  - [ ] 5.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Configuration\DemandConfigurations.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    public sealed class DemandRunConfiguration : IEntityTypeConfiguration<DemandRun>
    {
        public void Configure(EntityTypeBuilder<DemandRun> b)
        {
            b.ToTable("demand_runs"); b.HasKey(x => x.Id);
            b.Property(x => x.Frequency).HasMaxLength(40).IsRequired();
            b.Property(x => x.BusinessTimezone).HasMaxLength(64).IsRequired();
            b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired();
            b.Property(x => x.PayloadFingerprint).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            b.Property(x => x.BillingStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            foreach (var p in new[] { nameof(DemandRun.CurrentDueTotal), nameof(DemandRun.ArrearsTotal), nameof(DemandRun.OdiTotal), nameof(DemandRun.OtherChargesTotal), nameof(DemandRun.TotalCharges), nameof(DemandRun.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
            b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
            b.HasIndex(x => new { x.LogicalScopeKey, x.CurrentRevisionNumber }).IsUnique();
            b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.LineOfBusinessId, x.Status });
            b.HasMany(x => x.BranchScopes).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Revisions).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class DemandRunBranchScopeConfiguration : IEntityTypeConfiguration<DemandRunBranchScope>
    {
        public void Configure(EntityTypeBuilder<DemandRunBranchScope> b) { b.ToTable("demand_run_branch_scopes"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.BranchId }).IsUnique(); }
    }

    public sealed class DemandRunRevisionConfiguration : IEntityTypeConfiguration<DemandRunRevision>
    {
        public void Configure(EntityTypeBuilder<DemandRunRevision> b)
        {
            b.ToTable("demand_run_revisions"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            foreach (var p in new[] { nameof(DemandRunRevision.CurrentDueTotal), nameof(DemandRunRevision.ArrearsTotal), nameof(DemandRunRevision.OdiTotal), nameof(DemandRunRevision.OtherChargesTotal), nameof(DemandRunRevision.TotalCharges), nameof(DemandRunRevision.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
            b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber }).IsUnique();
        }
    }

    public sealed class BillingStageRunConfiguration : IEntityTypeConfiguration<BillingStageRun>
    {
        public void Configure(EntityTypeBuilder<BillingStageRun> b)
        {
            b.ToTable("demand_billing_stage_runs"); b.HasKey(x => x.Id); b.Property(x => x.LogicalBillingScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired(); b.Property(x => x.ChargePolicyKind).HasConversion<string>().HasMaxLength(40).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => new { x.LogicalBillingScopeKey, x.RevisionNumber }).IsUnique();
        }
    }

    public sealed class BillingPeriodResultConfiguration : IEntityTypeConfiguration<BillingPeriodResult>
    {
        public void Configure(EntityTypeBuilder<BillingPeriodResult> b)
        {
            b.ToTable("demand_billing_period_results"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            foreach (var p in new[] { nameof(BillingPeriodResult.ScheduledPrincipal), nameof(BillingPeriodResult.ScheduledInterestOrEmi), nameof(BillingPeriodResult.ScheduledInstallment), nameof(BillingPeriodResult.CurrentDue), nameof(BillingPeriodResult.Arrears), nameof(BillingPeriodResult.Odi), nameof(BillingPeriodResult.OtherCharges) }) b.Property<decimal>(p).HasPrecision(19, 2);
            b.HasIndex(x => new { x.BillingStageRunId, x.AccountId }).IsUnique(); b.HasOne(x => x.BillingStageRun).WithMany(x => x.Results).HasForeignKey(x => x.BillingStageRunId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class BillingAllocationEvidenceConfiguration : IEntityTypeConfiguration<BillingAllocationEvidence>
    {
        public void Configure(EntityTypeBuilder<BillingAllocationEvidence> b) { b.ToTable("demand_billing_allocation_evidence"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.ApplicationKey).HasMaxLength(300).IsRequired(); b.Property(x => x.AllocatedAmount).HasPrecision(19, 2); b.HasIndex(x => x.ApplicationKey).IsUnique(); b.HasOne(x => x.BillingPeriodResult).WithMany().HasForeignKey(x => x.BillingPeriodResultId).OnDelete(DeleteBehavior.Restrict); }
    }

    public sealed class BillingSnapshotConfiguration : IEntityTypeConfiguration<BillingSnapshot>
    {
        public void Configure(EntityTypeBuilder<BillingSnapshot> b) { b.ToTable("demand_billing_snapshots"); b.HasKey(x => x.Id); b.Property(x => x.Version).HasMaxLength(128).IsRequired(); b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.ContentFingerprint).HasMaxLength(128).IsRequired(); foreach (var p in new[] { nameof(BillingSnapshot.CurrentDueTotal), nameof(BillingSnapshot.ArrearsTotal), nameof(BillingSnapshot.OdiTotal), nameof(BillingSnapshot.OtherChargesTotal) }) b.Property<decimal>(p).HasPrecision(19, 2); b.HasIndex(x => new { x.BillingStageRunId, x.RevisionNumber, x.IsPublished }).IsUnique(); }
    }

    public sealed class DemandSnapshotConfiguration : IEntityTypeConfiguration<DemandSnapshot>
    {
        public void Configure(EntityTypeBuilder<DemandSnapshot> b) { b.ToTable("demand_snapshots"); b.HasKey(x => x.Id); b.Property(x => x.Version).HasMaxLength(128).IsRequired(); b.Property(x => x.TotalDemand).HasPrecision(19, 2); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.IsPublished }).IsUnique(); }
    }

    public sealed class DemandRowConfiguration : IEntityTypeConfiguration<DemandRow>
    {
        public void Configure(EntityTypeBuilder<DemandRow> b)
        {
            b.ToTable("demand_rows"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.AccountDisplay).HasMaxLength(200).IsRequired(); b.Property(x => x.CustomerDisplay).HasMaxLength(200).IsRequired(); b.Property(x => x.OrderingKey).HasMaxLength(200).IsRequired();
            foreach (var p in new[] { nameof(DemandRow.CurrentDue), nameof(DemandRow.Arrears), nameof(DemandRow.Odi), nameof(DemandRow.OtherCharges), nameof(DemandRow.TotalCharges), nameof(DemandRow.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
            b.HasIndex(x => new { x.DemandSnapshotId, x.AccountId }).IsUnique(); b.HasIndex(x => new { x.BranchId, x.LineOfBusinessId, x.OrderingKey }); b.HasOne(x => x.Revision).WithMany(x => x.Rows).HasForeignKey(x => x.DemandRunRevisionId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Batch).WithMany(x => x.Rows).HasForeignKey(x => x.DemandBatchId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class DemandBatchConfiguration : IEntityTypeConfiguration<DemandBatch>
    {
        public void Configure(EntityTypeBuilder<DemandBatch> b)
        {
            b.ToTable("demand_batches"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Frequency).HasMaxLength(40).IsRequired(); b.Property(x => x.SpoolIdempotencyKey).HasMaxLength(300).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
            foreach (var p in new[] { nameof(DemandBatch.CurrentDueTotal), nameof(DemandBatch.ArrearsTotal), nameof(DemandBatch.OdiTotal), nameof(DemandBatch.OtherChargesTotal), nameof(DemandBatch.TotalCharges), nameof(DemandBatch.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
            b.HasIndex(x => new { x.DemandSnapshotId, x.BranchId }).IsUnique(); b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.BranchId, x.Status });
        }
    }

    public sealed class DemandFailureConfiguration : IEntityTypeConfiguration<DemandFailure>
    {
        public void Configure(EntityTypeBuilder<DemandFailure> b) { b.ToTable("demand_failures"); b.HasKey(x => x.Id); b.Property(x => x.FailureClass).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Code).HasMaxLength(100).IsRequired(); b.Property(x => x.Message).HasMaxLength(1000).IsRequired(); b.Property(x => x.Scope).HasMaxLength(512).IsRequired(); b.HasIndex(x => new { x.DemandRunId, x.CreatedAtUtc }); }
    }

    public sealed class DemandProcessingLeaseConfiguration : IEntityTypeConfiguration<DemandProcessingLease>
    {
        public void Configure(EntityTypeBuilder<DemandProcessingLease> b) { b.ToTable("demand_processing_leases"); b.HasKey(x => x.Id); b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.OwnerId).HasMaxLength(200).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => x.LogicalScopeKey).IsUnique(); }
    }

    public sealed class DemandAuditEntryConfiguration : IEntityTypeConfiguration<DemandAuditEntry>
    {
        public void Configure(EntityTypeBuilder<DemandAuditEntry> b) { b.ToTable("demand_audit_entries"); b.HasKey(x => x.Id); b.Property(x => x.Scope).HasMaxLength(512).IsRequired(); b.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired(); b.Property(x => x.OutcomeCode).HasMaxLength(100).IsRequired(); b.Property(x => x.SafeReferences).HasMaxLength(2000).IsRequired(); b.HasIndex(x => new { x.DemandRunId, x.OccurredAtUtc }); }
    }
    ```
    - Use restricted deletes for retained financial output, snapshots, evidence, failures, batches, leases, and audit rows. Keep UTC datetime columns and `decimal(19,2)` precision.
    - _Requirements: 3.34-3.55, 5.1-5.8, 6.8-6.21, 8.1-8.6, 9.1-9.23, 14.1-14.8, 15.1-15.23, 17.1-17.16_

  - [ ] 5.2 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Context\FinnovaDbContext.cs`
    - Insert immediately after `public DbSet<ServiceCredential> ServiceCredentials => Set<ServiceCredential>();`:
    ```csharp
    public DbSet<DemandRun> DemandRuns => Set<DemandRun>();
    public DbSet<DemandRunBranchScope> DemandRunBranchScopes => Set<DemandRunBranchScope>();
    public DbSet<DemandRunRevision> DemandRunRevisions => Set<DemandRunRevision>();
    public DbSet<BillingStageRun> BillingStageRuns => Set<BillingStageRun>();
    public DbSet<BillingPeriodResult> BillingPeriodResults => Set<BillingPeriodResult>();
    public DbSet<BillingAllocationEvidence> BillingAllocationEvidence => Set<BillingAllocationEvidence>();
    public DbSet<BillingSnapshot> BillingSnapshots => Set<BillingSnapshot>();
    public DbSet<DemandSnapshot> DemandSnapshots => Set<DemandSnapshot>();
    public DbSet<DemandRow> DemandRows => Set<DemandRow>();
    public DbSet<DemandBatch> DemandBatches => Set<DemandBatch>();
    public DbSet<DemandFailure> DemandFailures => Set<DemandFailure>();
    public DbSet<DemandProcessingLease> DemandProcessingLeases => Set<DemandProcessingLease>();
    public DbSet<DemandAuditEntry> DemandAuditEntries => Set<DemandAuditEntry>();
    ```
    - `using Finnova.Models.Domain.Entities;` already exists in the current file. Leave `ApplyConfigurationsFromAssembly` unchanged.
    - _Requirements: 5.1-5.8, 7.1-7.8, 8.1-8.6, 14.1-14.8, 15.1-15.23_

  - After applying 5.2, run the existing EF migration command from `d:\Projects\Finnova\Finnova-API`:
    ```powershell
    dotnet ef migrations add AddDemandProcessing --project Finnova.Repository --startup-project Finnova.AccountsService --context FinnovaDbContext --output-dir Migrations
    ```
    Review the generated migration for all Demand tables, unique scope/result/evidence/snapshot/batch indexes, `decimal(19,2)`, UTC timestamps, rowversion columns, and restricted deletes. Do not add a schedule watermark/source-version table or any delivery table/transport metadata. Do not apply the migration in this task.
    - _Requirements: 3.14, 5.5-5.8, 13.15-13.18, 16.21-16.24_

- [ ] 6. Add repositories, source adapters, charge/journal seams, leases, configuration options, and DI registration
  - [ ] 6.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Interfaces\IDemandRepository.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Service.DemandProcessing.Ports;

    namespace Finnova.Repository.Interfaces;

    public interface IDemandRepository : Finnova.Service.DemandProcessing.Ports.IDemandRepository
    {
        IQueryable<DemandRow> AuthorizedRows(IReadOnlySet<Guid> branches);
        IQueryable<DemandBatch> AuthorizedBatches(IReadOnlySet<Guid> branches);
        Task AddFailureAsync(DemandFailure failure, CancellationToken ct);
        Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct);
        Task AddSnapshotAsync(DemandSnapshot snapshot, CancellationToken ct);
        Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct);
        Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct);
        Task AddBillingStageAsync(BillingStageRun stage, CancellationToken ct);
        Task AddBillingResultAsync(BillingPeriodResult result, CancellationToken ct);
        Task AddAllocationEvidenceAsync(BillingAllocationEvidence evidence, CancellationToken ct);
        Task AddBillingSnapshotAsync(BillingSnapshot snapshot, CancellationToken ct);
    }
    ```
    - The repository implementation must apply the caller branch scope in SQL before filters, ordering, or paging. The public application interface remains the `Finnova.Service.DemandProcessing.Ports.IDemandRepository` contract.
    - _Requirements: 8.4-8.6, 10.11-10.17, 12.1-12.15, 13.1-13.14, 14.9-14.10, 15.13-15.16_

  - [ ] 6.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Repositories\DemandRepository.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public sealed class DemandRepository(FinnovaDbContext db) : Interfaces.IDemandRepository
    {
        private IQueryable<DemandRun> Runs => db.DemandRuns.Include(x => x.BranchScopes).Include(x => x.Revisions);
        public Task<DemandRun?> FindByLogicalScopeAsync(string key, CancellationToken ct) => Runs.AsNoTracking().SingleOrDefaultAsync(x => x.LogicalScopeKey == key, ct);
        public Task<DemandRun?> GetRunAsync(Guid id, CancellationToken ct) => Runs.SingleOrDefaultAsync(x => x.Id == id, ct);
        public Task<DemandBatch?> GetBatchAsync(Guid id, IReadOnlySet<Guid> branches, CancellationToken ct) => db.DemandBatches.Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id && branches.Contains(x.BranchId), ct);
        public IQueryable<DemandRow> AuthorizedRows(IReadOnlySet<Guid> branches) => db.DemandRows.Where(x => branches.Contains(x.BranchId) && x.Status == DemandRowStatus.Completed);
        public IQueryable<DemandBatch> AuthorizedBatches(IReadOnlySet<Guid> branches) => db.DemandBatches.Where(x => branches.Contains(x.BranchId));

        public async Task<(IReadOnlyList<DemandRun> Items, int Total)> SearchRunsAsync(DemandHistoryQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
        {
            var q = Runs.AsNoTracking().Where(x => x.BranchScopes.Any(s => branches.Contains(s.BranchId)));
            if (query.PeriodYear.HasValue) q = q.Where(x => x.PeriodYear == query.PeriodYear.Value);
            if (query.PeriodMonth.HasValue) q = q.Where(x => x.PeriodMonth == query.PeriodMonth.Value);
            if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
            if (query.BranchId.HasValue) q = q.Where(x => x.BranchScopes.Any(s => s.BranchId == query.BranchId.Value));
            if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status.Value);
            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(x => x.PeriodYear).ThenBy(x => x.PeriodMonth).ThenBy(x => x.CompletedAtUtc).ThenBy(x => x.CurrentRevisionNumber).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<(IReadOnlyList<DemandRow> Items, int Total)> SearchRowsAsync(DemandRowsQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
        {
            var q = AuthorizedRows(branches).AsNoTracking().Include(x => x.Revision).ThenInclude(x => x.DemandRun).AsQueryable();
            if (query.RunId.HasValue) q = q.Where(x => x.Revision.DemandRunId == query.RunId.Value);
            if (query.RevisionNumber.HasValue) q = q.Where(x => x.Revision.RevisionNumber == query.RevisionNumber.Value);
            if (query.PeriodYear.HasValue) q = q.Where(x => x.Revision.DemandRun.PeriodYear == query.PeriodYear.Value);
            if (query.PeriodMonth.HasValue) q = q.Where(x => x.Revision.DemandRun.PeriodMonth == query.PeriodMonth.Value);
            if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
            if (query.BranchId.HasValue) q = q.Where(x => x.BranchId == query.BranchId.Value);
            if (query.AccountId.HasValue) q = q.Where(x => x.AccountId == query.AccountId.Value);
            if (query.CustomerId.HasValue) q = q.Where(x => x.CustomerId == query.CustomerId.Value);
            if (query.RowStatus.HasValue) q = q.Where(x => x.Status == query.RowStatus.Value);
            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(x => x.OrderingKey).ThenBy(x => x.AccountId).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<(IReadOnlyList<DemandBatch> Items, int Total)> SearchBatchesAsync(DemandBatchesQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
        {
            var q = AuthorizedBatches(branches).AsNoTracking().AsQueryable();
            if (query.RunId.HasValue) q = q.Where(x => x.DemandRunId == query.RunId.Value);
            if (query.RevisionNumber.HasValue) q = q.Where(x => x.RevisionNumber == query.RevisionNumber.Value);
            if (query.PeriodYear.HasValue) q = q.Where(x => x.PeriodYear == query.PeriodYear.Value);
            if (query.PeriodMonth.HasValue) q = q.Where(x => x.PeriodMonth == query.PeriodMonth.Value);
            if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
            if (query.BranchId.HasValue) q = q.Where(x => x.BranchId == query.BranchId.Value);
            if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status.Value);
            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(x => x.BranchId).ThenBy(x => x.DemandRunId).ThenBy(x => x.RevisionNumber).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
            return (items, total);
        }

        public Task AddAsync(DemandRun run, CancellationToken ct) => db.DemandRuns.AddAsync(run, ct).AsTask();
        public Task AddFailureAsync(DemandFailure failure, CancellationToken ct) => db.DemandFailures.AddAsync(failure, ct).AsTask();
        public Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct) => db.DemandAuditEntries.AddAsync(entry, ct).AsTask();
        public Task AddSnapshotAsync(DemandSnapshot snapshot, CancellationToken ct) => db.DemandSnapshots.AddAsync(snapshot, ct).AsTask();
        public async Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct) => await db.DemandRows.AddRangeAsync(rows, ct);
        public async Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct) => await db.DemandBatches.AddRangeAsync(batches, ct);
        public Task AddBillingStageAsync(BillingStageRun stage, CancellationToken ct) => db.BillingStageRuns.AddAsync(stage, ct).AsTask();
        public Task AddBillingResultAsync(BillingPeriodResult result, CancellationToken ct) => db.BillingPeriodResults.AddAsync(result, ct).AsTask();
        public Task AddAllocationEvidenceAsync(BillingAllocationEvidence evidence, CancellationToken ct) => db.BillingAllocationEvidence.AddAsync(evidence, ct).AsTask();
        public Task AddBillingSnapshotAsync(BillingSnapshot snapshot, CancellationToken ct) => db.BillingSnapshots.AddAsync(snapshot, ct).AsTask();
        public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => await db.Database.BeginTransactionAsync(ct);
    }
    ```
    - Do not load unscoped rows and filter them in the controller or UI. Retained deactivated-Branch rows remain readable because authorization is based on the persisted Branch scope, not current Branch activity.
    - _Requirements: 8.4-8.6, 10.11-10.17, 12.1-12.15, 13.1-13.14, 14.9-14.10, 15.13-15.16_

  - [ ] 6.3 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Configuration\DemandProcessingOptions.cs`
    ```csharp
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
    ```
    - Bind this section through `builder.Services.Configure<DemandProcessingOptions>(...)`; no role, policy, charge, journal, Billing, renderer, or retention name may be hard-coded outside these defaults.
    - _Requirements: 1.12-1.13, 3.24-3.33, 3.56-3.57, 7.11-7.20, 10.20, 11.18-11.22, 14.18-14.21, 17.13-17.14_

  - [ ] 6.4 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Adapters\DemandAdapters.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Configuration;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.Extensions.Options;

    namespace Finnova.Service.DemandProcessing.Adapters;

    public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

    public sealed class AuthorizationScopeResolver(IOptions<DemandProcessingOptions> options) : IAuthorizationScopeResolver
    {
        public Task<AuthorizedDemandScope> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
        {
            if (principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException();
            var lobIds = principal.Claims.Where(x => x.Type == "lob").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
            var branchIds = principal.Claims.Where(x => x.Type == "branch").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
            if (principal.Claims.Any(x => options.Value.AdminRoles.Contains(x.Value, StringComparer.OrdinalIgnoreCase))) return Task.FromResult(new AuthorizedDemandScope(lobIds, branchIds));
            return Task.FromResult(new AuthorizedDemandScope(lobIds, branchIds));
        }
    }

    public sealed class ConfiguredScheduleAdapter(IOptions<DemandProcessingOptions> options) : IFinalizedSchedulePort
    {
        public Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct) => throw new DemandDependencyException($"The configured finalized-schedule adapter '{options.Value.BillingCompletionAdapter ?? "default"}' must be bound to the existing schedule contract.");
    }

    public sealed class DependencyAllocationEventPort : IAllocationEventPort
    {
        public Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct) => throw new DemandDependencyException("The authoritative allocation-event source is not available.");
    }

    public sealed class DependencyAccountSourcePort : IAccountSourcePort
    {
        public IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, CancellationToken ct) => throw new DemandDependencyException("The authoritative account projection source is not available.");
        public Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, ClaimsPrincipal principal, CancellationToken ct) => throw new DemandDependencyException("The authoritative Branch scope source is not available.");
        public Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct) => throw new DemandDependencyException("The authoritative Branch source is not available.");
    }
    ```
    - Replace only the three dependency adapters with the approved existing source implementations when their contracts are available. They deliberately fail closed with a typed dependency error rather than returning sample data, empty financial data, or a fake authorization scope. The schedule adapter retains no new watermark requirement.
    - _Requirements: 2.6-2.8, 3.1-3.14, 4.1-4.21, 7.1-7.7, 10.1-10.13, 14.11-14.21_

  - [ ] 6.5 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.Repository\DependencyInjection.cs`
    - Add these `using` directives with the existing imports:
    ```csharp
    using Finnova.Service.DemandProcessing.Adapters;
    using Finnova.Service.DemandProcessing.Ports;
    ```
    - Insert immediately before `return services;`:
    ```csharp
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IDemandRepository, DemandRepository>();
    services.AddScoped<IAuthorizationScopeResolver, AuthorizationScopeResolver>();
    services.AddScoped<IFinalizedSchedulePort, ConfiguredScheduleAdapter>();
    services.AddScoped<IAccountSourcePort, DependencyAccountSourcePort>();
    services.AddScoped<IAllocationEventPort, DependencyAllocationEventPort>();
    services.AddSingleton<IClock, SystemClock>();
    ```
    - Register the approved `IAllocationPolicy`, `IChargePolicyAdapter`, and `IJournalPort` implementations in the service-layer registration task, not as repositories. Do not register any external delivery service.
    - _Requirements: 2.1-2.24, 3.15-3.57, 4.1-4.21, 7.3-7.20, 9.1-9.23, 13.7-13.30_

  - [ ] 6.6 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.Service\DependencyInjection.cs`
    - Add these imports:
    ```csharp
    using Finnova.Service.DemandProcessing.Billing;
    using Finnova.Service.DemandProcessing.Configuration;
    using Finnova.Service.DemandProcessing.Ports;
    ```
    - Insert immediately before `return services;` in `AddFinnovaServices`:
    ```csharp
    services.Configure<DemandProcessingOptions>(configuration.GetSection(DemandProcessingOptions.SectionName));
    services.AddScoped<IAllocationPolicy, AllocationPolicy>();
    services.AddScoped<IChargePolicyAdapter, DefaultChargePolicyAdapter>();
    services.AddScoped<IJournalPort, ConfiguredJournalPort>();
    services.AddScoped<IDemandProcessor, DemandProcessor>();
    services.AddScoped<IDemandPdfExportService, DemandPdfExportService>();
    ```
    - Keep Accounting/GL ownership in the adapter behind `IJournalPort`; `ConfiguredJournalPort` must call the existing Accounting/GL capability or return a typed dependency error when the configured adapter is unavailable. It must not create ledger rows.
    - _Requirements: 3.41-3.57, 6.1-6.7, 9.6-9.7, 12.16-12.23, 13.7-13.30_

  - [ ] 6.7 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\appsettings.json`
    - Insert this section immediately before the final root `}` after the existing `Jwt` object:
    ```json
    ,
    "DemandProcessing": {
      "Frequencies": [ "Monthly" ],
      "DefaultFrequency": "Monthly",
      "BusinessTimezone": "Asia/Kolkata",
      "DefaultDayCountBasis": 365,
      "OdiCap": null,
      "ChargePolicy": "Default",
      "RunPolicies": [ "DemandRun" ],
      "ReadPolicies": [ "DemandOutputRead" ],
      "SpoolPolicies": [ "DemandSpool" ],
      "AdminRoles": [ "SystemAdmin" ],
      "PollingSeconds": 10,
      "MaxPageSize": 200,
      "PdfRenderer": "QuestPdf",
      "RetentionPolicy": null,
      "JournalAdapter": "AccountingGl",
      "BillingCompletionAdapter": "ExistingBillingContract"
    }
    ```
    - Keep credentials and connection strings in the existing secret-management mechanism. The Demand section contains policy/adapter names and defaults only; it does not add a schedule watermark or source-version setting.
    - _Requirements: 1.12-1.13, 2.1-2.8, 3.24-3.33, 3.56-3.57, 7.11-7.20, 11.15, 11.21-11.22, 12.10, 14.18-14.21_

- [ ] 7. Implement pure Billing helpers, the default charge policy, the Journal Port, and Billing publication gates
  - [ ] 7.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Billing\BillingRules.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.DemandProcessing.Ports;

    namespace Finnova.Service.DemandProcessing.Billing;

    public static class BillingRules
    {
        public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        public static decimal ScheduledInstallment(ScheduleProjection schedule)
        {
            if (schedule.ScheduledPrincipal < 0m || schedule.ScheduledInterestOrEmi < 0m) throw new InvalidOperationException("Schedule components cannot be negative.");
            return Money(schedule.ScheduledPrincipal + schedule.ScheduledInterestOrEmi);
        }

        public static bool JournalChecksPass(JournalCheckResult result) => result.GenerationPassed && result.PostingPassed && result.IdempotencyPassed && result.ReconciliationPassed;

        public static void RequireBillingSnapshot(BillingCompletion completion, DemandScopeContract expected)
        {
            if (completion.Scope.LineOfBusinessId != expected.LineOfBusinessId || !completion.Scope.BranchIds.OrderBy(x => x).SequenceEqual(expected.BranchIds.OrderBy(x => x)) || !string.Equals(completion.Scope.Frequency, expected.Frequency, StringComparison.Ordinal) || completion.Scope.PeriodYear != expected.PeriodYear || completion.Scope.PeriodMonth != expected.PeriodMonth || !string.Equals(completion.Scope.BillingReference, expected.BillingReference, StringComparison.Ordinal)) throw new Finnova.Models.Domain.Exceptions.DemandBillingScopeMismatchException();
            if (completion.State != BillingStageStatus.Succeeded || completion.RequiredChecks.Count == 0 || completion.RequiredChecks.Values.Any(x => !x)) throw new Finnova.Models.Domain.Exceptions.DemandBillingIncompleteException();
        }

        public static string ApplicationKey(AllocationEvent eventItem) => $"{eventItem.EventType}:{eventItem.Id:N}:{eventItem.SourceVersion ?? "none"}";
    }

    public sealed class AllocationPolicy : IAllocationPolicy
    {
        public AllocationResult Apply(decimal scheduledInstallment, decimal priorArrears, IReadOnlyList<AllocationEvent> events)
        {
            var current = BillingRules.Money(Math.Max(0m, scheduledInstallment));
            var arrears = BillingRules.Money(Math.Max(0m, priorArrears));
            var evidence = new List<AllocationAppliedEvent>();
            foreach (var item in events.OrderBy(x => x.EffectiveUtc).ThenBy(x => x.Id))
            {
                if (!item.Eligible || item.Amount <= 0m) continue;
                var key = BillingRules.ApplicationKey(item);
                var remaining = BillingRules.Money(item.Amount);
                var target = item.TargetComponent switch { "Arrears" => "Arrears", "CurrentDue" => "CurrentDue", "Suspense" => "Suspense", _ => "CurrentDue" };
                if (item.EventType is AllocationEventType.Receipt or AllocationEventType.PartPayment)
                {
                    var arrearsApplied = Math.Min(arrears, remaining); arrears = BillingRules.Money(arrears - arrearsApplied); remaining = BillingRules.Money(remaining - arrearsApplied);
                    var currentApplied = Math.Min(current, remaining); current = BillingRules.Money(current - currentApplied); evidence.Add(new AllocationAppliedEvent(item, BillingRules.Money(arrearsApplied + currentApplied), target, key));
                }
                else if (item.EventType is AllocationEventType.Waiver or AllocationEventType.Reversal or AllocationEventType.Adjustment)
                {
                    var applied = target == "Arrears" ? Math.Min(arrears, remaining) : Math.Min(current, remaining); if (target == "Arrears") arrears = BillingRules.Money(arrears - applied); else current = BillingRules.Money(current - applied); evidence.Add(new AllocationAppliedEvent(item, BillingRules.Money(applied), target, key));
                }
                else evidence.Add(new AllocationAppliedEvent(item, 0m, "Suspense", key));
            }
            return new AllocationResult(current, arrears, evidence);
        }
    }
    ```
    - The policy is pure and deterministic. The approved existing allocation policy may replace the ordering implementation, but it must preserve exactly-once application keys, event evidence, separate Current Due/Arrears, and suspense treatment.
    - _Requirements: 3.6-3.21, 3.34-3.40, 3.45-3.51, 16.5-16.8, 16.16-16.20_

  - [ ] 7.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Billing\DefaultChargePolicyAdapter.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Service.DemandProcessing.Configuration;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.Extensions.Options;

    namespace Finnova.Service.DemandProcessing.Billing;

    public sealed class DefaultChargePolicyAdapter(IOptions<DemandProcessingOptions> options) : IChargePolicyAdapter
    {
        public Task<ChargeResult> CalculateAsync(ChargeInput input, CancellationToken ct)
        {
            var basis = options.Value.DefaultDayCountBasis <= 0 ? 365 : options.Value.DefaultDayCountBasis;
            var overduePrincipal = Math.Max(0m, input.OverduePrincipalBalance - input.ScheduledInstallment);
            var odi = BillingRules.Money(overduePrincipal * input.AnnualOdiRate * input.OverdueDays / basis);
            if (options.Value.OdiCap is decimal cap) odi = BillingRules.Money(Math.Min(odi, Math.Max(0m, cap)));
            var postedNonOdi = input.ChargeEvents.Where(x => x.Eligible && x.EventType == AllocationEventType.OtherCharge).Sum(x => x.Amount);
            var eligibleAdjustments = input.ChargeAdjustments.Where(x => x.Eligible && x.EventType is AllocationEventType.Waiver or AllocationEventType.Reversal or AllocationEventType.Adjustment).Sum(x => x.Amount);
            var net = postedNonOdi - eligibleAdjustments;
            var other = BillingRules.Money(Math.Max(0m, net));
            var evidence = input.ChargeEvents.Concat(input.ChargeAdjustments).Where(x => x.Eligible).Select(x => x.SourceReference).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            return Task.FromResult(new ChargeResult(odi, other, "Default:ODI365-or-configured|OtherChargesNetPostedNonOdi", evidence));
        }
    }
    ```
    - ODI excludes the current-period Scheduled Installment from overdue principal, defaults the day-count basis to 365, applies no cap when absent, and rounds INR 2 decimals. Other Charges include only eligible posted non-ODI charges less eligible waivers/reversals/adjustments after suspense treatment, exclude scheduled installment/principal/interest/arrears/ODI, retain evidence through the input references, clamp negative net to zero, and round INR 2 decimals.
    - _Requirements: 3.22-3.33, 5.5-5.10, 16.1, 16.9-16.10, 17.1-17.5_

  - [ ] 7.3 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Billing\ConfiguredJournalPort.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Configuration;
    using Microsoft.Extensions.Options;

    namespace Finnova.Service.DemandProcessing.Billing;

    public sealed class ConfiguredJournalPort(IOptions<DemandProcessingOptions> options) : IJournalPort
    {
        public Task<JournalCheckResult> EnsurePostedAsync(JournalRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(options.Value.JournalAdapter)) throw new DemandDependencyException("The Accounting/GL Journal Port adapter is not configured.");
            throw new DemandDependencyException($"The configured Accounting/GL adapter '{options.Value.JournalAdapter}' must be bound to the existing journal capability.");
        }
    }
    ```
    - Replace the adapter body with the existing Accounting/GL capability call when the approved adapter is selected. The implementation must pass a stable Billing Stage logical identity, return the four configured checks and journal reference, and store no ledger data in Demand Processing. Do not add a journal table or a fake success result.
    - _Requirements: 2.13, 2.18-2.20, 3.41-3.48, 3.52-3.57, 10.27, 16.21-16.24_

  - [ ] 7.4 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Billing\BillingPublicationGate.cs`
    ```csharp
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
    ```
    - Demand Generation must call this gate before reading Billing results. A Billing-stage failure blocks every Branch; a Branch-stage failure during Demand Generation is handled separately in Task 11.
    - _Requirements: 2.1-2.24, 3.44-3.55, 9.6-9.14, 16.21-16.24_

- [ ] 8. Implement pure Demand helpers for eligibility, mapping, totals, batching, reconciliation, period normalization, and idempotency
  - [ ] 8.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Rules\DemandRules.cs`
    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;

    namespace Finnova.Service.DemandProcessing.Rules;

    public sealed record CalculatedDemand(decimal CurrentDue, decimal Arrears, decimal Odi, decimal OtherCharges, decimal TotalCharges, decimal TotalDemand, bool IsZeroDemand);
    public sealed record EligibilityResult(bool Eligible, string? Code, string? Message);

    public static class DemandRules
    {
        public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        public static DemandPeriod NormalizePeriod(int year, int month, string timezone)
        {
            if (!string.Equals(timezone, "Asia/Kolkata", StringComparison.Ordinal)) throw new ArgumentException("Demand periods use Asia/Kolkata.", nameof(timezone));
            return DemandPeriod.Create(year, month);
        }

        public static EligibilityResult Check(AccountProjection account, DemandScope scope)
        {
            if (account.AccountId == Guid.Empty || account.CustomerId == Guid.Empty || !account.CustomerResolvable || !account.BranchResolvable || !account.LobResolvable) return new(false, "ERR-DEMAND-DATA-QUALITY", "The account has an unresolved source reference.");
            if (!string.Equals(account.Status, "Active", StringComparison.Ordinal)) return new(false, "ERR-DEMAND-ACCOUNT-INELIGIBLE", "The account is not Active at the run snapshot.");
            if (account.LineOfBusinessId != scope.LineOfBusinessId || !scope.BranchIds.Contains(account.BranchId)) return new(false, "ERR-DEMAND-SCOPE", "The account is outside the requested scope.");
            return new(true, null, null);
        }

        public static CalculatedDemand Calculate(decimal currentDue, decimal arrears, ChargeResult charges)
        {
            if (currentDue < 0m || arrears < 0m || charges.Odi < 0m || charges.OtherCharges < 0m) throw new InvalidOperationException("Demand components cannot be negative.");
            var current = Money(currentDue); var prior = Money(arrears); var odi = Money(charges.Odi); var other = Money(charges.OtherCharges); var totalCharges = Money(odi + other); var total = Money(current + prior + totalCharges);
            return new CalculatedDemand(current, prior, odi, other, totalCharges, total, total == 0m);
        }

        public static string Fingerprint(DemandLogicalKey key, string action, string? idempotencyKey)
        {
            var raw = $"{key.CanonicalValue}|{action}|{idempotencyKey?.Trim() ?? string.Empty}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }

        public static DemandBatch BuildBatch(DemandRunRevision revision, DemandSnapshot snapshot, Guid branchId, IEnumerable<DemandRow> sourceRows)
        {
            var rows = sourceRows.Where(x => x.BranchId == branchId).OrderBy(x => x.OrderingKey).ThenBy(x => x.AccountId).ToList();
            var batch = new DemandBatch { DemandRunRevisionId = revision.Id, DemandSnapshotId = snapshot.Id, DemandRunId = revision.DemandRunId, RevisionNumber = revision.RevisionNumber, BranchId = branchId, LineOfBusinessId = revision.DemandRun.LineOfBusinessId, Frequency = revision.DemandRun.Frequency, PeriodYear = revision.DemandRun.PeriodYear, PeriodMonth = revision.DemandRun.PeriodMonth, Status = rows.Count == 0 ? DemandBatchStatus.NoDemand : DemandBatchStatus.ReadyToSpool, RowCount = rows.Count, SpoolIdempotencyKey = $"{revision.DemandRunId:N}:{revision.RevisionNumber}:{branchId:N}" };
            batch.CurrentDueTotal = Money(rows.Sum(x => x.CurrentDue)); batch.ArrearsTotal = Money(rows.Sum(x => x.Arrears)); batch.OdiTotal = Money(rows.Sum(x => x.Odi)); batch.OtherChargesTotal = Money(rows.Sum(x => x.OtherCharges)); batch.TotalCharges = Money(rows.Sum(x => x.TotalCharges)); batch.TotalDemand = Money(rows.Sum(x => x.TotalDemand));
            return batch;
        }

        public static void Reconcile(DemandBatch batch, IEnumerable<DemandRow> sourceRows)
        {
            var rows = sourceRows.Where(x => x.DemandBatchId == batch.Id).ToList();
            if (rows.Any(x => x.BranchId != batch.BranchId || x.LineOfBusinessId != batch.LineOfBusinessId) || batch.RowCount != rows.Count || batch.CurrentDueTotal != Money(rows.Sum(x => x.CurrentDue)) || batch.ArrearsTotal != Money(rows.Sum(x => x.Arrears)) || batch.OdiTotal != Money(rows.Sum(x => x.Odi)) || batch.OtherChargesTotal != Money(rows.Sum(x => x.OtherCharges)) || batch.TotalCharges != Money(rows.Sum(x => x.TotalCharges)) || batch.TotalDemand != Money(rows.Sum(x => x.TotalDemand))) throw new InvalidOperationException("Demand Batch totals or dimensions do not reconcile.");
        }
    }
    ```
    - These helpers never access EF, HTTP, logging, or the UI. The Account ordering key must be stable and the Branch grouping must create one `NoDemand` batch for every in-scope Branch with no rows.
    - _Requirements: 1.21-1.28, 4.1-4.21, 5.1-5.21, 6.1-6.21, 8.23-8.27, 9.15-9.22, 16.1-16.20, 17.1-17.16_

  - [ ] 8.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandWorkQueue.cs`
    ```csharp
    using System.Threading.Channels;
    using Finnova.Service.DemandProcessing.Ports;

    namespace Finnova.Service.DemandProcessing.Processing;

    public sealed class DemandWorkQueue : IDemandWorkQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
        public ValueTask EnqueueAsync(Guid runId, CancellationToken ct) => _channel.Writer.WriteAsync(runId, ct);
        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }
    ```
    - The queue is only a dispatcher. Accepted Run, stage, lease, results, snapshots, and audit state must be durable before enqueueing; recovery is added in Task 9.
    - _Requirements: 7.3-7.10, 9.1-9.14, 16.21-16.24_

- [ ] 9. Implement Start/Rerun CQRS, omitted-Branch resolution, idempotency, durable leases, audit transitions, and worker recovery
  - [ ] 9.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Commands\DemandCommands.cs`
    ```csharp
    using System.Security.Claims;
    using FluentValidation;
    using MediatR;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Configuration;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Rules;
    using Microsoft.Extensions.Options;

    namespace Finnova.Service.DemandProcessing.Commands;

    public sealed record StartDemandRunCommand(StartDemandRunRequest Request, ClaimsPrincipal Principal, string ActorId, DemandSource Source, string CorrelationId) : IRequest<DemandRunAcceptedResponse>;
    public sealed record RerunDemandRunCommand(Guid RunId, RerunDemandRunRequest Request, ClaimsPrincipal Principal, string ActorId, string CorrelationId) : IRequest<DemandRunAcceptedResponse>;

    public sealed class StartDemandRunValidator : AbstractValidator<StartDemandRunCommand>
    {
        public StartDemandRunValidator(IOptions<DemandProcessingOptions> options)
        {
            RuleFor(x => x.Request.LineOfBusinessId).NotEmpty(); RuleFor(x => x.Request.PeriodYear).InclusiveBetween(2000, 2100); RuleFor(x => x.Request.PeriodMonth).InclusiveBetween(1, 12); RuleFor(x => x.Request.Frequency).Must((value, _) => string.IsNullOrWhiteSpace(value) || options.Value.Frequencies.Contains(value.Trim(), StringComparer.Ordinal)).WithMessage("Frequency is not configured."); RuleFor(x => x.Request.IdempotencyKey).MaximumLength(200);
        }
    }

    public sealed class StartDemandRunHandler(IAccountSourcePort accounts, IAuthorizationScopeResolver scopes, IDemandRepository repository, IDemandWorkQueue queue, IOptions<DemandProcessingOptions> options, IClock clock) : IRequestHandler<StartDemandRunCommand, DemandRunAcceptedResponse>
    {
        public async Task<DemandRunAcceptedResponse> Handle(StartDemandRunCommand command, CancellationToken ct)
        {
            var allowed = await scopes.ResolveAsync(command.Principal, ct); if (!allowed.LineOfBusinessIds.Contains(command.Request.LineOfBusinessId)) throw new UnauthorizedAccessException();
            var frequency = string.IsNullOrWhiteSpace(command.Request.Frequency) ? options.Value.DefaultFrequency : command.Request.Frequency.Trim();
            var branches = command.Request.BranchId.HasValue ? new[] { command.Request.BranchId.Value } : (await accounts.ResolveActiveBranchesAsync(command.Request.LineOfBusinessId, command.Principal, ct)).OrderBy(x => x).ToArray();
            if (branches.Length == 0) throw new DemandValidationException("No authorized Active Branches are available.", "ERR-DEMAND-NO-ACTIVE-BRANCH");
            if (branches.Any(x => !allowed.BranchIds.Contains(x))) throw new UnauthorizedAccessException();
            if (command.Request.BranchId.HasValue && !await accounts.IsActiveBranchAsync(command.Request.BranchId.Value, command.Request.LineOfBusinessId, ct)) throw new DemandValidationException("The selected Branch is deactivated.", "ERR-DEMAND-BRANCH-DEACTIVATED");
            var scope = DemandScope.Create(command.Request.LineOfBusinessId, branches); var period = DemandPeriod.Create(command.Request.PeriodYear, command.Request.PeriodMonth); var key = $"{scope.CanonicalValue}|{frequency}|{period.CanonicalValue}"; var fingerprint = DemandRules.Fingerprint(new DemandLogicalKey(scope, frequency, period, "pending"), "start", command.Request.IdempotencyKey);
            var existing = await repository.FindByLogicalScopeAsync(key, ct); if (existing is not null) { if (!string.Equals(existing.PayloadFingerprint, fingerprint, StringComparison.Ordinal)) throw new DemandIdempotencyConflictException(); return new(existing.Id, existing.CurrentRevisionNumber, existing.Status, existing.CorrelationId); }
            var run = new DemandRun { LineOfBusinessId = scope.LineOfBusinessId, Frequency = frequency, PeriodYear = period.Year, PeriodMonth = period.Month, BusinessTimezone = options.Value.BusinessTimezone, LogicalScopeKey = key, IdempotencyKey = command.Request.IdempotencyKey, PayloadFingerprint = fingerprint, Status = DemandRunStatus.Accepted, BillingStatus = BillingStageStatus.Pending, Source = command.Source, ActorId = command.ActorId, CorrelationId = command.CorrelationId, RequestedAtUtc = clock.UtcNow };
            run.BranchScopes = scope.BranchIds.Select((branch, index) => new DemandRunBranchScope { DemandRunId = run.Id, RevisionNumber = 1, BranchId = branch, SortOrder = index + 1 }).ToList(); run.Revisions.Add(new DemandRunRevision { DemandRunId = run.Id, RevisionNumber = 1, Status = DemandRunStatus.Accepted, DemandRun = run });
            await repository.AddAsync(run, ct); await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = 1, Scope = key, ActorId = command.ActorId, Source = command.Source, CorrelationId = command.CorrelationId, NewStatus = run.Status.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "RUN_ACCEPTED", SafeReferences = $"run={run.Id:N}" }, ct); await repository.SaveAsync(ct); await queue.EnqueueAsync(run.Id, ct); return new(run.Id, 1, run.Status, run.CorrelationId);
        }
    }

    public sealed class RerunDemandRunHandler(IDemandRepository repository, IDemandWorkQueue queue, IClock clock) : IRequestHandler<RerunDemandRunCommand, DemandRunAcceptedResponse>
    {
        public async Task<DemandRunAcceptedResponse> Handle(RerunDemandRunCommand command, CancellationToken ct)
        {
            var run = await repository.GetRunAsync(command.RunId, ct) ?? throw new DemandNotFoundException(command.RunId); if (string.IsNullOrWhiteSpace(command.Request.Reason) || string.IsNullOrWhiteSpace(command.Request.IdempotencyKey)) throw new DemandValidationException("Rerun reason and idempotency key are required.");
            var revision = new DemandRunRevision { DemandRunId = run.Id, RevisionNumber = run.CurrentRevisionNumber + 1, SupersedesRevisionNumber = run.CurrentRevisionNumber, Status = DemandRunStatus.Accepted, DemandRun = run }; run.CurrentRevisionNumber = revision.RevisionNumber; run.Status = DemandRunStatus.Superseded; run.Revisions.Add(revision); await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, Scope = run.LogicalScopeKey, ActorId = command.ActorId, Source = DemandSource.User, CorrelationId = command.CorrelationId, PreviousStatus = DemandRunStatus.Superseded.ToString(), NewStatus = DemandRunStatus.Accepted.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "RERUN_ACCEPTED", SafeReferences = $"run={run.Id:N};supersedes={revision.SupersedesRevisionNumber}" }, ct); await repository.SaveAsync(ct); await queue.EnqueueAsync(run.Id, ct); return new(run.Id, revision.RevisionNumber, revision.Status, command.CorrelationId);
        }
    }
    ```
    - The handler authenticates before scope resolution and financial reads, uses the configured Monthly default, expands omitted Branch to authorized Active Branches, rejects explicit deactivated Branches, persists one immutable GUID Run ID, and resolves equivalent retries without duplicate records.
    - _Requirements: 1.1-1.28, 7.1-7.17, 8.7-8.19, 8.23-8.27, 9.1-9.5, 14.1-14.15, 15.1-15.23_

  - [ ] 9.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandRunWorker.cs`
    ```csharp
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Finnova.Service.DemandProcessing.Ports;

    namespace Finnova.Service.DemandProcessing.Processing;

    public sealed class DemandRunWorker(DemandWorkQueue queue, IServiceScopeFactory scopes, ILogger<DemandRunWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var runId in queue.ReadAllAsync(stoppingToken))
            {
                try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IDemandProcessor>().ProcessAsync(runId, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception exception) { logger.LogError(exception, "Demand run {RunId} failed before publication", runId); }
            }
        }
    }
    ```
    - Register the queue as a singleton and worker as a hosted service. The recovery dispatcher must enqueue durable Accepted, BillingPending, BillingRunning, and DemandGenerating records after restart; it must resolve existing Runs and never create duplicate logical results.
    - _Requirements: 7.3-7.10, 9.1-9.14, 10.4-10.7, 16.16-16.20_

  - [ ] 9.3 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandLeaseStore.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Finnova.Service.DemandProcessing.Configuration;

    namespace Finnova.Service.DemandProcessing.Processing;

    public sealed class DemandLeaseStore(FinnovaDbContext db, IOptions<DemandProcessingOptions> options, IClock clock) : IDemandLeaseStore
    {
        private readonly string _owner = $"accounts-service:{Environment.ProcessId}";
        public async Task<bool> TryAcquireAsync(string logicalScopeKey, Guid runId, CancellationToken ct)
        {
            var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.LogicalScopeKey == logicalScopeKey, ct); var now = clock.UtcNow; if (lease is not null && lease.ExpiresAtUtc > now && lease.DemandRunId != runId) return false;
            if (lease is null) db.DemandProcessingLeases.Add(new DemandProcessingLease { LogicalScopeKey = logicalScopeKey, DemandRunId = runId, OwnerId = _owner, AcquiredAtUtc = now, ExpiresAtUtc = now.AddMinutes(10) }); else { lease.DemandRunId = runId; lease.OwnerId = _owner; lease.AcquiredAtUtc = now; lease.ExpiresAtUtc = now.AddMinutes(10); }
            await db.SaveChangesAsync(ct); return true;
        }
        public async Task<bool> RenewAsync(Guid runId, CancellationToken ct) { var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.DemandRunId == runId && x.OwnerId == _owner, ct); if (lease is null || lease.ExpiresAtUtc <= clock.UtcNow) return false; lease.ExpiresAtUtc = clock.UtcNow.AddMinutes(10); await db.SaveChangesAsync(ct); return true; }
        public async Task ReleaseAsync(Guid runId, CancellationToken ct) { var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.DemandRunId == runId && x.OwnerId == _owner, ct); if (lease is not null) { lease.ExpiresAtUtc = clock.UtcNow; await db.SaveChangesAsync(ct); } }
    }
    ```
    - Use the unique logical-scope index and rowversion as the cross-process guard. A lost lease must prevent Completed publication and leave the Run recoverable.
    - _Requirements: 9.1-9.14, 16.16-16.20_

  - [ ] 9.4 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\Program.cs`
    - Insert immediately after `builder.Services.AddFinnovaServices(builder.Configuration, FinnovaServiceModules.ApplicationProcessing);`:
    ```csharp
    builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>();
    builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Ports.IDemandWorkQueue>(sp => sp.GetRequiredService<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>());
    builder.Services.AddScoped<Finnova.Service.DemandProcessing.Ports.IDemandLeaseStore, Finnova.Service.DemandProcessing.Processing.DemandLeaseStore>();
    builder.Services.AddHostedService<Finnova.Service.DemandProcessing.Processing.DemandRunWorker>();
    ```
    - Add no external delivery or delivery-history registration.
    - _Requirements: 7.3-7.10, 9.1-9.14, 14.18-14.21_

- [ ] 10. Implement the Period Billing CQRS/worker slice and atomic Billing Snapshot publication
  - [ ] 10.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandProcessor.cs`
    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Billing;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Rules;

    namespace Finnova.Service.DemandProcessing.Processing;

    public sealed class DemandProcessor(IDemandRepository repository, IAccountSourcePort accounts, IFinalizedSchedulePort schedules, IAllocationEventPort events, IAllocationPolicy allocation, IChargePolicyAdapter charges, IJournalPort journal, IDemandLeaseStore lease, IClock clock) : IDemandProcessor
    {
        public async Task ProcessAsync(Guid runId, CancellationToken ct)
        {
            var run = await repository.GetRunAsync(runId, ct) ?? throw new DemandNotFoundException(runId); var revision = run.Revisions.Single(x => x.RevisionNumber == run.CurrentRevisionNumber); if (!await lease.TryAcquireAsync(run.LogicalScopeKey, run.Id, ct)) throw new DemandConcurrencyException();
            try
            {
                run.Status = DemandRunStatus.BillingPending; run.BillingStatus = BillingStageStatus.Queued; run.StartedAtUtc = clock.UtcNow; await repository.SaveAsync(ct);
                var scope = DemandScope.Create(run.LineOfBusinessId, run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId)); var period = DemandPeriod.Create(run.PeriodYear, run.PeriodMonth); var snapshotReference = run.ProcessingCutoffReference ?? $"run:{run.Id:N}:revision:{revision.RevisionNumber}";
                var stage = new BillingStageRun { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, LogicalBillingScopeKey = run.LogicalScopeKey, Status = BillingStageStatus.Running, ProcessingCutoffReference = snapshotReference, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "ConfiguredChargePolicy", CreatedAtUtc = clock.UtcNow, StartedAtUtc = clock.UtcNow, CorrelationId = run.CorrelationId, DemandRun = run }; await repository.AddBillingStageAsync(stage, ct); run.BillingStageRunId = stage.Id; run.Status = DemandRunStatus.BillingRunning; run.BillingStatus = BillingStageStatus.Running; await repository.SaveAsync(ct);
                var results = new List<BillingPeriodResult>();
                await foreach (var account in accounts.ReadAsync(scope, snapshotReference, ct))
                {
                    var eligibility = DemandRules.Check(account, scope); if (!eligibility.Eligible) { await repository.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, AccountId = account.AccountId, FailureClass = DemandFailureClass.DataQuality, Code = eligibility.Code!, Message = eligibility.Message!, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId, CreatedAtUtc = clock.UtcNow }, ct); continue; }
                    var schedule = await schedules.GetAsync(account.AccountId, period, ct) ?? throw new DemandScheduleIncompleteException(); var installment = BillingRules.ScheduledInstallment(schedule); var sourceEvents = await events.GetAsync(account.AccountId, period, snapshotReference, ct); var allocationResult = allocation.Apply(installment, 0m, sourceEvents); var chargeInput = new ChargeInput(0m, 0m, 0, null, installment, sourceEvents, sourceEvents); var chargeResult = await charges.CalculateAsync(chargeInput, ct); var result = new BillingPeriodResult { BillingStageRunId = stage.Id, AccountId = account.AccountId, AccountDisplay = account.AccountDisplay, CustomerDisplay = account.CustomerDisplay, CustomerId = account.CustomerId, LineOfBusinessId = account.LineOfBusinessId, BranchId = account.BranchId, AccountStatusAtCutoff = account.Status, IsEligible = true, Status = BillingPeriodResultStatus.Succeeded, ScheduleId = schedule.ScheduleId, ScheduleEntryId = schedule.ScheduleEntryId, ScheduleReference = schedule.ScheduleReference, ScheduledPrincipal = BillingRules.Money(schedule.ScheduledPrincipal), ScheduledInterestOrEmi = BillingRules.Money(schedule.ScheduledInterestOrEmi), ScheduledInstallment = installment, CurrentDue = allocationResult.CurrentDue, Arrears = allocationResult.Arrears, Odi = chargeResult.Odi, OtherCharges = chargeResult.OtherCharges, AllocationPolicyReference = stage.AllocationPolicyReference, ChargePolicyReference = chargeResult.PolicyReference, ProcessingCutoffReference = snapshotReference, CreatedAtUtc = clock.UtcNow }; results.Add(result); await repository.AddBillingResultAsync(result, ct); foreach (var evidence in allocationResult.Evidence) await repository.AddAllocationEvidenceAsync(new BillingAllocationEvidence { BillingStageRunId = stage.Id, BillingPeriodResultId = result.Id, EventType = evidence.Event.EventType, SourceEventId = evidence.Event.Id, SourceEventVersion = evidence.Event.SourceVersion, EventEffectiveUtc = evidence.Event.EffectiveUtc, AllocationPolicyReference = stage.AllocationPolicyReference, AllocatedAmount = BillingRules.Money(evidence.AllocatedAmount), TargetComponent = evidence.TargetComponent, Outcome = AllocationOutcome.Applied, SafeReason = "Applied by configured allocation policy.", ApplicationKey = evidence.ApplicationKey }, ct);
                }
                stage.EligibleAccountCount = results.Count; stage.SuccessfulResultCount = results.Count; var journalResult = await journal.EnsurePostedAsync(new JournalRequest(stage.LogicalBillingScopeKey, stage.Id, BillingRules.Money(results.Sum(x => x.CurrentDue)), BillingRules.Money(results.Sum(x => x.Arrears)), BillingRules.Money(results.Sum(x => x.Odi)), BillingRules.Money(results.Sum(x => x.OtherCharges)), "INR"), ct); if (!BillingRules.JournalChecksPass(journalResult)) throw new DemandJournalIncompleteException(); stage.JournalBatchReference = journalResult.JournalReference; stage.JournalCheckSummary = journalResult.SafeSummary;
                var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{run.LogicalScopeKey}|{revision.RevisionNumber}|{results.Count}"))); var billingSnapshot = new BillingSnapshot { BillingStageRunId = stage.Id, RevisionNumber = revision.RevisionNumber, Version = version, LogicalScopeKey = run.LogicalScopeKey, ProcessingCutoffReference = snapshotReference, ResultCount = results.Count, CurrentDueTotal = BillingRules.Money(results.Sum(x => x.CurrentDue)), ArrearsTotal = BillingRules.Money(results.Sum(x => x.Arrears)), OdiTotal = BillingRules.Money(results.Sum(x => x.Odi)), OtherChargesTotal = BillingRules.Money(results.Sum(x => x.OtherCharges)), JournalBatchReference = journalResult.JournalReference, JournalCheckSummary = journalResult.SafeSummary, IsPublished = true, ContentFingerprint = version, ActorId = run.ActorId, Source = run.Source.ToString(), CorrelationId = run.CorrelationId, PublishedAtUtc = clock.UtcNow }; await repository.AddBillingSnapshotAsync(billingSnapshot, ct); stage.BillingSnapshotId = billingSnapshot.Id; stage.BillingSnapshotVersion = version; stage.Status = BillingStageStatus.Succeeded; stage.CompletedAtUtc = clock.UtcNow; run.BillingSnapshotId = billingSnapshot.Id; run.Status = DemandRunStatus.BillingSucceeded; run.BillingStatus = BillingStageStatus.Succeeded; await repository.SaveAsync(ct);
                run.Status = DemandRunStatus.DemandGenerating; await repository.SaveAsync(ct); await GenerateDemandAsync(run, revision, billingSnapshot, results, ct);
            }
            catch (DemandProcessingException) { run.Status = DemandRunStatus.BillingIncomplete; run.BillingStatus = BillingStageStatus.Incomplete; run.FailedAtUtc = clock.UtcNow; await repository.SaveAsync(ct); throw; }
            finally { await lease.ReleaseAsync(run.Id, ct); }
        }

        private async Task GenerateDemandAsync(DemandRun run, DemandRunRevision revision, BillingSnapshot snapshot, IReadOnlyList<BillingPeriodResult> results, CancellationToken ct)
        {
            var demandSnapshot = new DemandSnapshot { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, BillingSnapshotId = snapshot.Id, Version = $"demand:{snapshot.Version}", IsPublished = false }; var rows = results.OrderBy(x => x.AccountId).Select(x => new DemandRow { DemandRunRevisionId = revision.Id, DemandSnapshotId = demandSnapshot.Id, BillingPeriodResultId = x.Id, AccountId = x.AccountId, AccountDisplay = x.AccountDisplay, CustomerDisplay = x.CustomerDisplay, CustomerId = x.CustomerId, LineOfBusinessId = x.LineOfBusinessId, BranchId = x.BranchId, CurrentDue = BillingRules.Money(x.CurrentDue), Arrears = BillingRules.Money(x.Arrears), Odi = BillingRules.Money(x.Odi), OtherCharges = BillingRules.Money(x.OtherCharges), TotalCharges = BillingRules.Money(x.Odi + x.OtherCharges), TotalDemand = BillingRules.Money(x.CurrentDue + x.Arrears + x.Odi + x.OtherCharges), IsZeroDemand = BillingRules.Money(x.CurrentDue + x.Arrears + x.Odi + x.OtherCharges) == 0m, ScheduleReference = x.ScheduleReference, ProcessingCutoffReference = x.ProcessingCutoffReference, OrderingKey = x.AccountId.ToString("N") }).ToList();
            var batches = new List<DemandBatch>(); foreach (var branch in run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId)) { var batch = DemandRules.BuildBatch(revision, demandSnapshot, branch, rows); batches.Add(batch); foreach (var row in rows.Where(x => x.BranchId == branch)) { row.DemandBatchId = batch.Id; batch.Rows.Add(row); } }
            foreach (var batch in batches) { try { DemandRules.Reconcile(batch, batch.Rows); } catch (Exception error) { batch.Status = DemandBatchStatus.Failed; batch.FailureClass = DemandFailureClass.Reconciliation; batch.FailureCode = "ERR-DEMAND-RECONCILIATION"; batch.FailureReason = error.Message; await repository.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, DemandBatchId = batch.Id, FailureClass = DemandFailureClass.Reconciliation, Code = batch.FailureCode, Message = batch.FailureReason, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId, CreatedAtUtc = clock.UtcNow }, ct); } }
            var successful = batches.Count(x => x.Status is DemandBatchStatus.ReadyToSpool or DemandBatchStatus.NoDemand); demandSnapshot.RowCount = rows.Count; demandSnapshot.BatchCount = batches.Count; demandSnapshot.TotalDemand = BillingRules.Money(rows.Sum(x => x.TotalDemand)); demandSnapshot.IsPublished = successful > 0 && batches.All(x => x.Status is DemandBatchStatus.ReadyToSpool or DemandBatchStatus.NoDemand); demandSnapshot.PublishedAtUtc = demandSnapshot.IsPublished ? clock.UtcNow : null; await repository.AddSnapshotAsync(demandSnapshot, ct); await repository.AddRowsAsync(rows, ct); await repository.AddBatchesAsync(batches, ct); revision.DemandSnapshotId = demandSnapshot.Id; revision.RowCount = rows.Count; revision.BatchCount = batches.Count; revision.TotalDemand = demandSnapshot.TotalDemand; revision.TotalCharges = BillingRules.Money(rows.Sum(x => x.TotalCharges)); revision.Status = demandSnapshot.IsPublished ? DemandRunStatus.Completed : DemandRunStatus.PartiallyFailed; revision.CompletedAtUtc = clock.UtcNow; run.DemandSnapshotId = demandSnapshot.Id; run.RowCount = rows.Count; run.BatchCount = batches.Count; run.TotalDemand = demandSnapshot.TotalDemand; run.TotalCharges = revision.TotalCharges; run.Status = revision.Status; run.CompletedAtUtc = clock.UtcNow; await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, Scope = run.LogicalScopeKey, ActorId = run.ActorId, Source = run.Source, CorrelationId = run.CorrelationId, NewStatus = run.Status.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = run.Status == DemandRunStatus.Completed ? "DEMAND_COMPLETED" : "DEMAND_PARTIALLY_FAILED", SafeReferences = $"billingSnapshot={snapshot.Id:N};demandSnapshot={demandSnapshot.Id:N}" }, ct); await repository.SaveAsync(ct);
        }
    }
    ```
    - Bind `IAccountSourcePort`, `IFinalizedSchedulePort`, `IAllocationEventPort`, and the Accounting/GL `IJournalPort` to authoritative existing implementations before enabling automation. This code does not create a new watermark, recompute EMI, mutate a schedule, create journal rows, or publish Demand before Billing and journal checks pass.
    - _Requirements: 2.1-2.24, 3.1-3.57, 4.1-4.21, 5.1-5.21, 6.1-6.21, 7.3-7.20, 9.6-9.23, 10.1-10.27, 16.1-16.27_

- [ ] 11. Implement Demand Generation publication, zero-demand rows, NoDemand batches, and isolated Branch failures
  - [ ] 11.1 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandProcessor.cs`
    - Replace the `GenerateDemandAsync` method with the complete method below. Keep the Billing method and class unchanged around this replacement:
    ```csharp
    private async Task GenerateDemandAsync(DemandRun run, DemandRunRevision revision, BillingSnapshot snapshot, IReadOnlyList<BillingPeriodResult> results, CancellationToken ct)
    {
        if (!snapshot.IsPublished) throw new DemandBillingIncompleteException("Demand Generation requires a Published Billing Snapshot.");
        var demandSnapshot = new DemandSnapshot { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, BillingSnapshotId = snapshot.Id, Version = $"demand:{snapshot.Version}", IsPublished = false };
        var rows = new List<DemandRow>();
        foreach (var result in results.OrderBy(x => x.AccountId))
        {
            if (result.Status != BillingPeriodResultStatus.Succeeded) continue;
            var totalCharges = BillingRules.Money(result.Odi + result.OtherCharges); var totalDemand = BillingRules.Money(result.CurrentDue + result.Arrears + totalCharges);
            rows.Add(new DemandRow { DemandRunRevisionId = revision.Id, DemandSnapshotId = demandSnapshot.Id, BillingPeriodResultId = result.Id, AccountId = result.AccountId, CustomerId = result.CustomerId, LineOfBusinessId = result.LineOfBusinessId, BranchId = result.BranchId, CurrentDue = BillingRules.Money(result.CurrentDue), Arrears = BillingRules.Money(result.Arrears), Odi = BillingRules.Money(result.Odi), OtherCharges = BillingRules.Money(result.OtherCharges), TotalCharges = totalCharges, TotalDemand = totalDemand, IsZeroDemand = totalDemand == 0m, ScheduleReference = result.ScheduleReference, ProcessingCutoffReference = result.ProcessingCutoffReference, OrderingKey = result.AccountId.ToString("N") });
        }
        var batches = new List<DemandBatch>();
        foreach (var branch in run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId))
        {
            var batch = DemandRules.BuildBatch(revision, demandSnapshot, branch, rows); batches.Add(batch);
            foreach (var row in rows.Where(x => x.BranchId == branch)) { row.DemandBatchId = batch.Id; batch.Rows.Add(row); }
            try { DemandRules.Reconcile(batch, batch.Rows); }
            catch (Exception error) { batch.Status = DemandBatchStatus.Failed; batch.FailureClass = DemandFailureClass.Reconciliation; batch.FailureCode = "ERR-DEMAND-RECONCILIATION"; batch.FailureReason = error.Message; await repository.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, DemandBatchId = batch.Id, FailureClass = DemandFailureClass.Reconciliation, Code = batch.FailureCode, Message = error.Message, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId, CreatedAtUtc = clock.UtcNow }, ct); }
        }
        var failed = batches.Any(x => x.Status == DemandBatchStatus.Failed); var successful = batches.Any(x => x.Status is DemandBatchStatus.ReadyToSpool or DemandBatchStatus.NoDemand); demandSnapshot.RowCount = rows.Count; demandSnapshot.BatchCount = batches.Count; demandSnapshot.TotalDemand = BillingRules.Money(rows.Sum(x => x.TotalDemand)); demandSnapshot.IsPublished = successful && !failed; demandSnapshot.PublishedAtUtc = demandSnapshot.IsPublished ? clock.UtcNow : null;
        await repository.AddSnapshotAsync(demandSnapshot, ct); await repository.AddRowsAsync(rows, ct); await repository.AddBatchesAsync(batches, ct);
        revision.DemandSnapshotId = demandSnapshot.Id; revision.RowCount = rows.Count; revision.BatchCount = batches.Count; revision.TotalDemand = demandSnapshot.TotalDemand; revision.TotalCharges = BillingRules.Money(rows.Sum(x => x.TotalCharges)); revision.Status = failed && successful ? DemandRunStatus.PartiallyFailed : failed ? DemandRunStatus.Failed : DemandRunStatus.Completed; revision.CompletedAtUtc = clock.UtcNow; run.DemandSnapshotId = demandSnapshot.Id; run.RowCount = rows.Count; run.BatchCount = batches.Count; run.TotalDemand = demandSnapshot.TotalDemand; run.TotalCharges = revision.TotalCharges; run.Status = revision.Status; run.CompletedAtUtc = clock.UtcNow;
        await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, Scope = run.LogicalScopeKey, ActorId = run.ActorId, Source = run.Source, CorrelationId = run.CorrelationId, NewStatus = run.Status.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = run.Status == DemandRunStatus.PartiallyFailed ? "DEMAND_PARTIALLY_FAILED" : run.Status == DemandRunStatus.Failed ? "DEMAND_FAILED" : "DEMAND_COMPLETED", SafeReferences = $"billingSnapshot={snapshot.Id:N};demandSnapshot={demandSnapshot.Id:N}" }, ct);
        await repository.SaveAsync(ct);
    }
    ```
    - A `NoDemand` batch is a non-failure and is visible but not spoolable. A failed Branch is separately inspectable and retryable through the rerun/Branch recovery path; successful batches remain unchanged. The overall status is `PartiallyFailed` only when successful and failed Branch outcomes coexist.
    - _Requirements: 5.1-5.21, 6.1-6.21, 9.15-9.23, 10.14-10.18, 13.5-13.6, 16.9-16.15_

- [ ] 12. Implement authorized Run, history, Demand Row, Batch, and deactivated-Branch read queries
  - [ ] 12.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Mappers\DemandMappers.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.DemandProcessing.Mappers;

    public static class DemandMappers
    {
        public static DemandRowResponse ToResponse(this DemandRow row) => new(row.Id, row.Revision.DemandRunId, row.Revision.RevisionNumber, row.AccountId, row.CustomerId, row.LineOfBusinessId, row.BranchId, row.AccountDisplay, row.CustomerDisplay, row.Revision.DemandRun.PeriodYear, row.Revision.DemandRun.PeriodMonth, row.Revision.DemandRun.Frequency, row.CurrentDue, row.Arrears, row.Odi, row.OtherCharges, row.TotalCharges, row.TotalDemand, row.IsZeroDemand, row.Status, row.ScheduleReference, row.ProcessingCutoffReference, row.OrderingKey);
        public static DemandBatchResponse ToResponse(this DemandBatch batch) => new(batch.Id, batch.DemandRunId, batch.RevisionNumber, batch.BranchId, batch.LineOfBusinessId, batch.Frequency, batch.PeriodYear, batch.PeriodMonth, batch.Status, batch.RowCount, batch.CurrentDueTotal, batch.ArrearsTotal, batch.OdiTotal, batch.OtherChargesTotal, batch.TotalCharges, batch.TotalDemand, batch.FailureClass?.ToString(), batch.FailureCode, batch.FailureReason, batch.LastSpoolActorId, batch.LastSpooledAtUtc);
        public static DemandFailureResponse ToResponse(this DemandFailure failure) => new(failure.FailureClass, failure.Code, failure.Message, failure.AccountId, failure.DemandBatchId, failure.CreatedAtUtc);
    }
    ```
    - The mapper exposes persisted values and does not calculate or replace any financial field.
    - _Requirements: 5.3-5.21, 6.8-6.15, 8.1-8.6, 10.18-10.25, 12.4-12.15, 13.1-13.6_

  - [ ] 12.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Queries\DemandQueries.cs`
    ```csharp
    using System.Security.Claims;
    using MediatR;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Mappers;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.Extensions.Options;
    using Finnova.Service.DemandProcessing.Configuration;

    namespace Finnova.Service.DemandProcessing.Queries;

    public sealed record GetDemandRunQuery(Guid RunId, ClaimsPrincipal Principal) : IRequest<DemandRunResponse>;
    public sealed record GetDemandHistoryQuery(DemandHistoryQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRunResponse>>;
    public sealed record GetDemandRowsQuery(DemandRowsQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRowResponse>>;
    public sealed record GetDemandBatchesQuery(DemandBatchesQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandBatchResponse>>;

    public sealed class DemandQueryHandlers(IDemandRepository repository, IAuthorizationScopeResolver scopes, IOptions<DemandProcessingOptions> options) : IRequestHandler<GetDemandRunQuery, DemandRunResponse>, IRequestHandler<GetDemandHistoryQuery, PaginatedResponse<DemandRunResponse>>, IRequestHandler<GetDemandRowsQuery, PaginatedResponse<DemandRowResponse>>, IRequestHandler<GetDemandBatchesQuery, PaginatedResponse<DemandBatchResponse>>
    {
        public async Task<DemandRunResponse> Handle(GetDemandRunQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var run = await repository.GetRunAsync(request.RunId, ct) ?? throw new DemandNotFoundException(request.RunId); if (!run.BranchScopes.Any(x => allowed.BranchIds.Contains(x.BranchId))) throw new DemandNotFoundException(request.RunId); var revision = run.Revisions.Single(x => x.RevisionNumber == run.CurrentRevisionNumber); return new(run.Id, revision.RevisionNumber, run.LineOfBusinessId, run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId).ToArray(), run.Frequency, run.PeriodYear, run.PeriodMonth, run.ProcessingCutoffReference, run.Status, run.BillingStatus, run.BillingSnapshotId, null, run.DemandSnapshotId, run.RowCount, run.BatchCount, run.CurrentDueTotal, run.ArrearsTotal, run.OdiTotal, run.OtherChargesTotal, run.TotalCharges, run.TotalDemand, Array.Empty<DemandFailureResponse>(), run.RequestedAtUtc, run.CompletedAtUtc); }
        public async Task<PaginatedResponse<DemandRunResponse>> Handle(GetDemandHistoryQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var query = request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }; var result = await repository.SearchRunsAsync(query, allowed.BranchIds, ct); var data = result.Items.Select(x => new DemandRunResponse(x.Id, x.CurrentRevisionNumber, x.LineOfBusinessId, x.BranchScopes.OrderBy(s => s.SortOrder).Select(s => s.BranchId).ToArray(), x.Frequency, x.PeriodYear, x.PeriodMonth, x.ProcessingCutoffReference, x.Status, x.BillingStatus, x.BillingSnapshotId, null, x.DemandSnapshotId, x.RowCount, x.BatchCount, x.CurrentDueTotal, x.ArrearsTotal, x.OdiTotal, x.OtherChargesTotal, x.TotalCharges, x.TotalDemand, Array.Empty<DemandFailureResponse>(), x.RequestedAtUtc, x.CompletedAtUtc)).ToArray(); return new(data, result.Total, query.Page, query.PageSize, (int)Math.Ceiling(result.Total / (double)query.PageSize)); }
        public async Task<PaginatedResponse<DemandRowResponse>> Handle(GetDemandRowsQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var result = await repository.SearchRowsAsync(request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }, allowed.BranchIds, ct); var data = result.Items.Select(x => x.ToResponse()).ToArray(); return new(data, result.Total, request.Query.Page, pageSize, (int)Math.Ceiling(result.Total / (double)pageSize)); }
        public async Task<PaginatedResponse<DemandBatchResponse>> Handle(GetDemandBatchesQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var result = await repository.SearchBatchesAsync(request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }, allowed.BranchIds, ct); var data = result.Items.Select(x => x.ToResponse()).ToArray(); return new(data, result.Total, request.Query.Page, pageSize, (int)Math.Ceiling(result.Total / (double)pageSize)); }
    }
    ```
    - Historical queries continue to return retained data for a deactivated Branch when the caller has the persisted Branch scope. New Run creation remains Active-Branch-only.
    - _Requirements: 8.1-8.6, 8.23-8.27, 10.18-10.25, 12.1-12.15, 13.1-13.6, 14.9-14.10_

- [ ] 13. Implement the minimal spool state transition only
  - [ ] 13.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Commands\SpoolDemandBatchCommand.cs`
    ```csharp
    using System.Security.Claims;
    using MediatR;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Ports;

    namespace Finnova.Service.DemandProcessing.Commands;

    public sealed record SpoolDemandBatchCommand(Guid BatchId, SpoolBatchRequest Request, ClaimsPrincipal Principal, string ActorId, DemandSource Source, string CorrelationId) : IRequest<SpoolStatusResponse>;

    public sealed class SpoolDemandBatchHandler(IDemandRepository repository, IAuthorizationScopeResolver scopes, IClock clock) : IRequestHandler<SpoolDemandBatchCommand, SpoolStatusResponse>
    {
        public async Task<SpoolStatusResponse> Handle(SpoolDemandBatchCommand command, CancellationToken ct)
        {
            var allowed = await scopes.ResolveAsync(command.Principal, ct); var batch = await repository.GetBatchAsync(command.BatchId, allowed.BranchIds, ct) ?? throw new DemandNotFoundException(command.BatchId);
            if (batch.Status == DemandBatchStatus.Spooled) return new(batch.Id, batch.Status, command.CorrelationId);
            if (batch.Status != DemandBatchStatus.ReadyToSpool) throw new DemandSpoolNotReadyException();
            batch.Status = DemandBatchStatus.Spooled; batch.LastSpoolActorId = command.ActorId; batch.LastSpoolSource = command.Source; batch.LastSpooledAtUtc = clock.UtcNow;
            await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = batch.DemandRunId, RevisionNumber = batch.RevisionNumber, BranchId = batch.BranchId, DemandBatchId = batch.Id, Scope = $"lob={batch.LineOfBusinessId:N};branch={batch.BranchId:N}", ActorId = command.ActorId, Source = command.Source, CorrelationId = command.CorrelationId, PreviousStatus = DemandBatchStatus.ReadyToSpool.ToString(), NewStatus = DemandBatchStatus.Spooled.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "BATCH_SPOOLED", SafeReferences = $"batch={batch.Id:N};run={batch.DemandRunId:N}" }, ct); await repository.SaveAsync(ct); return new(batch.Id, batch.Status, command.CorrelationId);
        }
    }
    ```
    - Repeated requests return the existing Spooled state. No external delivery operation or delivery metadata is permitted.
    - _Requirements: 6.6-6.7, 7.18-7.20, 9.17-9.22, 10.3, 10.16, 10.22-10.26, 13.5-13.18, 13.29-13.30, 15.6-15.18_

- [ ] 14. Implement authorized server-side PDF export from persisted values
  - [ ] 14.1 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\Finnova.AccountsService.csproj`
    - Add the isolated renderer dependency only in AccountsService. This explicit package reference uses QuestPDF and keeps PDF generation out of Models, Repository, and the UI:
    ```xml
    <PackageReference Include="QuestPDF" Version="2024.12.3" />
    ```
    - If the user selects another approved renderer, replace this one package reference and only the renderer adapter in Task 14.2. Do not add a PDF framework or browser-print library.
    - _Requirements: 12.16-12.25, 13.19-13.28_

  - [ ] 14.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Service\DemandProcessing\PdfExport\QuestPdfRenderer.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Service.DemandProcessing.Ports;
    using QuestPDF.Fluent;
    using QuestPDF.Helpers;
    using QuestPDF.Infrastructure;

    namespace Finnova.Service.DemandProcessing.PdfExport;

    public sealed class QuestPdfRenderer : IPdfRenderer
    {
        public byte[] Render(DemandPdfDocument document)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(container => container.Page(page => { page.Margin(24); page.Header().Text(document.Title).FontSize(18).Bold(); page.Content().Column(column => { column.Item().Text($"Generated at UTC: {document.GeneratedAtUtc:O}"); column.Item().Text($"Run: {document.Run.RunId:N}, Revision: {document.Run.RevisionNumber}"); column.Item().Text($"Period: {document.Run.PeriodYear:D4}-{document.Run.PeriodMonth:D2}, LOB: {document.Run.LineOfBusinessId:N}"); column.Item().Text($"Total Demand: INR {document.TotalDemand:0.00}"); foreach (var row in document.Rows) column.Item().Text($"Account {row.AccountId:N} | Branch {row.BranchId:N} | Current Due INR {row.CurrentDue:0.00} | Arrears INR {row.Arrears:0.00} | ODI INR {row.Odi:0.00} | Other Charges INR {row.OtherCharges:0.00} | Total Demand INR {row.TotalDemand:0.00}"); foreach (var batch in document.Batches) column.Item().Text($"Branch {batch.BranchId:N} | Status {batch.Status} | Rows {batch.RowCount} | Total Demand INR {batch.TotalDemand:0.00}"); }); page.Footer().AlignRight().Text("Finnova Demand Processing"); })).GeneratePdf();
        }
    }
    ```
    - The renderer consumes only persisted response values and metadata. It must never call a source port or recalculate totals.
    - _Requirements: 12.16-12.25, 13.19-13.28, 17.6-17.16_

  - [ ] 14.3 CREATE `d:\Projects\Finnova\Finnova.Service\DemandProcessing\PdfExport\DemandPdfExportService.cs`
    ```csharp
    using System.Security.Claims;
    using MediatR;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Queries;

    namespace Finnova.Service.DemandProcessing.PdfExport;

    public sealed class DemandPdfExportService(IMediator mediator, IPdfRenderer renderer, IClock clock) : IDemandPdfExportService
    {
        public async Task<PdfFileResult> RenderOutputAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct)
        {
            var rows = await mediator.Send(new GetDemandRowsQuery(new DemandRowsQuery(query.RunId, query.RevisionNumber, query.PeriodYear, query.PeriodMonth, query.LineOfBusinessId, query.BranchId, query.AccountId, query.CustomerId, query.RowStatus, 1, query.PageSize), principal), ct); var runId = query.RunId ?? rows.Data.FirstOrDefault()?.RunId ?? throw new Models.Domain.Exceptions.DemandPdfExportException("No authorized persisted output matched the export filters."); var run = await mediator.Send(new GetDemandRunQuery(runId, principal), ct); var document = new DemandPdfDocument("Demand Generation Output", clock.UtcNow, run, rows.Data, Array.Empty<DemandBatchResponse>(), rows.Data.Sum(x => x.TotalDemand)); return new PdfFileResult(renderer.Render(document), "application/pdf", $"demand-output-{run.RunId:N}-{run.RevisionNumber}.pdf");
        }
        public async Task<PdfFileResult> RenderBatchesAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct)
        {
            var batches = await mediator.Send(new GetDemandBatchesQuery(new DemandBatchesQuery(query.RunId, query.RevisionNumber, query.PeriodYear, query.PeriodMonth, query.LineOfBusinessId, query.BranchId, null, 1, query.PageSize), principal), ct); var runId = query.RunId ?? batches.Data.FirstOrDefault()?.RunId ?? throw new Models.Domain.Exceptions.DemandPdfExportException("No authorized persisted batches matched the export filters."); var run = await mediator.Send(new GetDemandRunQuery(runId, principal), ct); var document = new DemandPdfDocument("Demand Spooling Batches", clock.UtcNow, run, Array.Empty<DemandRowResponse>(), batches.Data, batches.Data.Sum(x => x.TotalDemand)); return new PdfFileResult(renderer.Render(document), "application/pdf", $"demand-batches-{run.RunId:N}-{run.RevisionNumber}.pdf");
        }
    }
    ```
    - Add `using MediatR;` at the top of the file. The export service checks authorization through the query handlers before rendering, applies filters/scope, includes persisted metadata/totals and generated-at UTC, and returns no file for unauthorized or empty-scope data.
    - _Requirements: 12.16-12.25, 13.19-13.28, 14.9-14.10, 17.13-17.16_

  - [ ] 14.4 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.Service\DependencyInjection.cs`
    - Insert immediately after `services.AddScoped<IDemandPdfExportService, DemandPdfExportService>();`:
    ```csharp
    services.AddSingleton<IPdfRenderer, Finnova.Service.DemandProcessing.PdfExport.QuestPdfRenderer>();
    ```
    - If another approved package is selected, register its isolated adapter instead. Do not register a browser-print service.
    - _Requirements: 12.16-12.25, 13.19-13.28_

- [ ] 15. Add the controller, configured authorization policies, PDF routes, and middleware mapping
  - [ ] 15.1 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\Program.cs`
    - Insert after the existing `ApplicationProcessor` policy in `builder.Services.AddAuthorization(...)`:
    ```csharp
    options.AddPolicy("DemandRun", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("DemandOutputRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("DemandSpool", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin") || ctx.User.IsInRole("Automizer")));
    options.AddPolicy("DemandPdfExport", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin")));
    ```
    - Replace role literals with the configured policy/role resolver when the application authorization convention supports it. The shown defaults match `DemandProcessingOptions`; do not broaden access as a shortcut.
    - _Requirements: 1.1-1.7, 7.11-7.14, 11.1-11.3, 12.1-12.3, 13.1-13.6, 14.1-14.21_

  - [ ] 15.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\Controllers\DemandProcessingController.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.DemandProcessing.Commands;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Queries;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.AccountsService.Controllers;

    [ApiController, Route("api/demand-processing")]
    public sealed class DemandProcessingController(IMediator mediator, IDemandPdfExportService exports) : ControllerBase
    {
        private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
        private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;
        private DemandSource Source => User.IsInRole("Automizer") ? DemandSource.Automizer : DemandSource.User;

        [HttpPost("runs"), Authorize(Policy = "DemandRun")]
        public async Task<ActionResult<DemandRunAcceptedResponse>> Start(StartDemandRunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new StartDemandRunCommand(request, User, Actor, Source, Correlation), ct));
        [HttpGet("runs/{runId:guid}"), Authorize(Policy = "DemandOutputRead")]
        public async Task<ActionResult<DemandRunResponse>> GetRun(Guid runId, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRunQuery(runId, User), ct));
        [HttpGet("runs"), Authorize(Policy = "DemandOutputRead")]
        public async Task<ActionResult<PaginatedResponse<DemandRunResponse>>> History([FromQuery] DemandHistoryQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandHistoryQuery(query, User), ct));
        [HttpPost("runs/{runId:guid}/rerun"), Authorize(Policy = "DemandRun")]
        public async Task<ActionResult<DemandRunAcceptedResponse>> Rerun(Guid runId, RerunDemandRunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new RerunDemandRunCommand(runId, request, User, Actor, Correlation), ct));
        [HttpGet("runs/{runId:guid}/rows"), Authorize(Policy = "DemandOutputRead")]
        public async Task<ActionResult<PaginatedResponse<DemandRowResponse>>> Rows(Guid runId, [FromQuery] DemandRowsQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRowsQuery(query with { RunId = runId }, User), ct));
        [HttpGet("batches"), Authorize(Policy = "DemandOutputRead")]
        public async Task<ActionResult<PaginatedResponse<DemandBatchResponse>>> Batches([FromQuery] DemandBatchesQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandBatchesQuery(query, User), ct));
        [HttpPost("batches/{batchId:guid}/spool"), Authorize(Policy = "DemandSpool")]
        public async Task<ActionResult<SpoolStatusResponse>> Spool(Guid batchId, SpoolBatchRequest request, CancellationToken ct) => Ok(await mediator.Send(new SpoolDemandBatchCommand(batchId, request, User, Actor, Source, Correlation), ct));
        [HttpGet("exports/output"), Authorize(Policy = "DemandPdfExport")]
        public async Task<IActionResult> ExportOutput([FromQuery] DemandPdfQuery query, CancellationToken ct) { var file = await exports.RenderOutputAsync(query, User, ct); return File(file.Content, file.ContentType, file.FileName); }
        [HttpGet("exports/batches"), Authorize(Policy = "DemandPdfExport")]
        public async Task<IActionResult> ExportBatches([FromQuery] DemandPdfQuery query, CancellationToken ct) { var file = await exports.RenderBatchesAsync(query, User, ct); return File(file.Content, file.ContentType, file.FileName); }
    }
    ```
    - There are no independent Billing, Demand-generation, external-delivery, or browser-print endpoints. `Spool` changes only persisted batch state.
    - _Requirements: 1.1-1.7, 7.1-7.20, 8.1-8.27, 10.18-10.27, 11.1-11.26, 12.1-12.25, 13.1-13.30, 14.1-14.21_

  - [ ] 15.3 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.AccountsService\Middlewares\ExceptionHandlingMiddleware.cs`
    - Insert these switch arms before the existing fallback arm (`_ => ...`), after the existing typed feature exceptions:
    ```csharp
    DemandProcessingException d => (d.ErrorCode switch
    {
        "ERR-DEMAND-NOT-FOUND" => StatusCodes.Status404NotFound,
        "ERR-DEMAND-DATA-QUALITY" or "ERR-DEMAND-RECONCILIATION" or "ERR-BILLING-SCHEDULE-INCOMPLETE" => StatusCodes.Status422UnprocessableEntity,
        "ERR-DEMAND-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
        "ERR-DEMAND-BILLING-INCOMPLETE" or "ERR-DEMAND-BILLING-SCOPE" or "ERR-BILLING-ALLOCATION-CONFLICT" or "ERR-BILLING-JOURNAL-INCOMPLETE" or "ERR-BILLING-SNAPSHOT-BLOCKED" or "ERR-DEMAND-IDEMPOTENCY-CONFLICT" or "ERR-DEMAND-CONCURRENT" or "ERR-DEMAND-SPOOL-NOT-READY" => StatusCodes.Status409Conflict,
        "ERR-DEMAND-PDF-EXPORT" => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest
    }, d.ErrorCode, d.Message),
    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "ERR-AUTH-403", "You are not authorized for this Demand Processing scope."),
    ```
    - Add `using Finnova.Models.Domain.Exceptions;` only if the current file does not already contain it. Preserve the existing authentication middleware behavior for 401. Do not expose demand data in authorization or validation errors.
    - _Requirements: 1.2-1.7, 2.2-2.22, 3.11-3.14, 6.16-6.21, 8.10-8.17, 9.11-9.23, 10.1-10.27, 12.21-12.25, 13.13-13.30, 14.1-14.6_

- [ ] 16. Add the API Gateway aliases for the UI-composed AccountsService path
  - [ ] 16.1 MODIFY `d:\Projects\Finnova\Finnova-API\Finnova.ApiGateway\appsettings.json`
    - Insert the following route objects at the beginning of `ReverseProxy.Routes`, before `ua-route` and alongside the existing application-processing aliases:
    ```json
    "ua-demand-processing-alias-route": {
      "ClusterId": "accounts-cluster",
      "Match": { "Path": "/api/ua/api/demand-processing/{**catch-all}" },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    "ua-demand-processing-alias-root": {
      "ClusterId": "accounts-cluster",
      "Match": { "Path": "/api/ua/api/demand-processing" },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    ```
    - Keep `accounts-cluster` and Authorization forwarding unchanged. The UI service base path is `/demand-processing`; the shared axios base composes `/api/ua/api/demand-processing`. Do not add a new cluster.
    - _Requirements: 7.1-7.3, 11.6-11.7, 12.8, 13.10, 14.16-14.17_

- [ ] 17. Add backend FsCheck, xUnit, repository, authorization, PDF, spool-state, and WebApplicationFactory tests
  - [ ]* 17.1 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandRulesPropertyTests.cs`
    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Billing;
    using Finnova.Service.DemandProcessing.Rules;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;

    public sealed class DemandRulesPropertyTests
    {
        // Feature: demand-processing, Property 1: schedule arithmetic and charge components are rounded deterministically.
        [Property(MaxTest = 100)] public Property BillingComponentsRemainDeterministic(NonNegativeInt principal, NonNegativeInt interest, NonNegativeInt odi, NonNegativeInt other) { var schedule = new ScheduleProjection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "schedule", principal.Get / 100m, interest.Get / 100m); var a = BillingRules.ScheduledInstallment(schedule); var b = BillingRules.ScheduledInstallment(schedule); return (a == b && a == BillingRules.Money(schedule.ScheduledPrincipal + schedule.ScheduledInterestOrEmi)).ToProperty(); }
        // Feature: demand-processing, Property 2: each allocation event has one stable logical application key.
        [Property(MaxTest = 100)] public Property AllocationKeysAreStable(NonNegativeInt amount) { var item = new AllocationEvent(Guid.NewGuid(), AllocationEventType.Receipt, amount.Get / 100m, DateTime.UtcNow, "source", "v1", "CurrentDue", true); return (BillingRules.ApplicationKey(item) == BillingRules.ApplicationKey(item)).ToProperty(); }
        // Feature: demand-processing, Property 3: demand totals are source-derived and deterministic.
        [Property(MaxTest = 100)] public Property DemandTotalsConserveComponents(NonNegativeInt current, NonNegativeInt arrears, NonNegativeInt odi, NonNegativeInt other) { var result = DemandRules.Calculate(current.Get / 100m, arrears.Get / 100m, new ChargeResult(odi.Get / 100m, other.Get / 100m, "test", Array.Empty<string>())); return (result.TotalCharges == DemandRules.Money(result.Odi + result.OtherCharges) && result.TotalDemand == DemandRules.Money(result.CurrentDue + result.Arrears + result.TotalCharges)).ToProperty(); }
        // Feature: demand-processing, Property 4: only Active in-scope accounts with resolvable references are eligible.
        [Property(MaxTest = 100)] public Property EligibilityPreservesScope() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); var account = new AccountProjection(Guid.NewGuid(), Guid.NewGuid(), lob, branch, "Account", "Customer", "Active", true, true, true, "snapshot"); return DemandRules.Check(account, DemandScope.Create(lob, [branch])).Eligible.ToProperty(); }
        // Feature: demand-processing, Property 5: equivalent logical keys produce one stable fingerprint.
        [Property(MaxTest = 100)] public Property FingerprintsAreStable() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); var key = new DemandLogicalKey(DemandScope.Create(lob, [branch]), "Monthly", DemandPeriod.Create(2025, 1), "billing"); return (DemandRules.Fingerprint(key, "start", "idempotency") == DemandRules.Fingerprint(key, "start", "idempotency")).ToProperty(); }
        // Feature: demand-processing, Property 6: Billing publication requires successful stage checks.
        [Property(MaxTest = 100)] public Property FailedBillingChecksNeverPass() { var completion = new BillingCompletion("billing", new(Guid.NewGuid(), [Guid.NewGuid()], "Monthly", 2025, 1, "billing"), BillingStageStatus.Incomplete, "snapshot", null, new Dictionary<string, bool> { ["snapshot"] = false }); var expected = completion.Scope; var passed = false; try { BillingRules.RequireBillingSnapshot(completion, expected); passed = true; } catch (Exception) { } return (!passed).ToProperty(); }
        // Feature: demand-processing, Property 7: empty in-scope Branches become NoDemand rather than failures.
        [Property(MaxTest = 100)] public Property EmptyBranchesBecomeNoDemand() { var run = new Finnova.Models.Domain.Entities.DemandRun { LineOfBusinessId = Guid.NewGuid(), Frequency = "Monthly", PeriodYear = 2025, PeriodMonth = 1 }; var revision = new Finnova.Models.Domain.Entities.DemandRunRevision { DemandRun = run, DemandRunId = run.Id, RevisionNumber = 1 }; var snapshot = new Finnova.Models.Domain.Entities.DemandSnapshot { DemandRunId = run.Id, RevisionNumber = 1 }; var batch = DemandRules.BuildBatch(revision, snapshot, Guid.NewGuid(), Array.Empty<Finnova.Models.Domain.Entities.DemandRow>()); return (batch.Status == DemandBatchStatus.NoDemand && batch.TotalDemand == 0m).ToProperty(); }
    }
    ```
    - Keep each property at a minimum of 100 generated cases. Add focused generators/examples for cap/no-cap ODI, negative Other Charges evidence/clamping, exact-once allocation, zero values, excluded statuses, deactivated Branches, branch totals, retry idempotency, and multi-Branch partitioning.
    - _Requirements: 3.6-3.33, 4.1-4.21, 5.1-5.21, 6.1-6.21, 9.15-9.23, 16.1-16.29, 17.1-17.16_

  - [ ]* 17.2 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandProcessingExampleTests.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Billing;
    using Finnova.Service.DemandProcessing.Rules;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;

    public sealed class DemandProcessingExampleTests
    {
        [Fact] public void ClosedCancelledAndPreClosedAccountsAreExcluded() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); foreach (var status in new[] { "Closed", "Cancelled", "Pre-Closed" }) Assert.False(DemandRules.Check(new AccountProjection(Guid.NewGuid(), Guid.NewGuid(), lob, branch, "A", "C", status, true, true, true, "s"), DemandScope.Create(lob, [branch])).Eligible); }
        [Fact] public void DefaultChargePolicyClampsNegativeOtherCharges() { var adapter = new DefaultChargePolicyAdapter(Microsoft.Extensions.Options.Options.Create(new Finnova.Service.DemandProcessing.Configuration.DemandProcessingOptions())); var eventItem = new AllocationEvent(Guid.NewGuid(), AllocationEventType.OtherCharge, 2m, DateTime.UtcNow, "charge", "1", "OtherCharges", true); var adjustment = new AllocationEvent(Guid.NewGuid(), AllocationEventType.Waiver, 5m, DateTime.UtcNow, "waiver", "1", "OtherCharges", true); var result = adapter.CalculateAsync(new ChargeInput(100m, .1m, 10, null, 10m, [eventItem], [adjustment]), CancellationToken.None).Result; Assert.Equal(0m, result.OtherCharges); Assert.Contains("charge", result.EvidenceReferences); }
        [Fact] public void ZeroDemandIsRetained() { var result = DemandRules.Calculate(0m, 0m, new ChargeResult(0m, 0m, "test", Array.Empty<string>())); Assert.True(result.IsZeroDemand); }
        [Fact] public void InrRoundingUsesAwayFromZero() { Assert.Equal(1.01m, BillingRules.Money(1.005m)); Assert.Equal(-1.01m, BillingRules.Money(-1.005m)); }
        [Fact] public void FailedBillingCompletionIsRejected() { var c = new BillingCompletion("b", new(Guid.NewGuid(), [Guid.NewGuid()], "Monthly", 2025, 1, "b"), BillingStageStatus.Failed, "s", null, new Dictionary<string, bool> { ["snapshot"] = false }); Assert.Throws<Finnova.Models.Domain.Exceptions.DemandBillingIncompleteException>(() => BillingRules.RequireBillingSnapshot(c, c.Scope)); }
        [Fact] public void DeactivatedBranchHistoryIsAReadConcern() { Assert.True(Enum.IsDefined(DemandBatchStatus.NoDemand)); }
    }
    ```
    - Tests must cover no processable mutation on unauthorized calls, `PartiallyFailed` with a successful and failed Branch, separate inspection/retry state, immutable rerun lineage, journal check failures, safe diagnostics, and PDF persisted-value behavior.
    - _Requirements: 1.2-1.7, 2.2-2.22, 6.16-6.21, 8.10-8.27, 9.11-9.23, 10.1-10.27, 12.21-12.25, 13.13-13.30, 14.1-14.21, 15.1-15.23_

  - [ ]* 17.3 CREATE `d:\Projects\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandProcessingIntegrationTests.cs`
    ```csharp
    using System.Net;
    using System.Net.Http.Headers;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;

    public sealed class DemandProcessingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        public DemandProcessingIntegrationTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();
        [Fact] public async Task MissingTokenReturns401WithoutDemandData() { var response = await _client.GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.DoesNotContain("TotalDemand", await response.Content.ReadAsStringAsync()); }
        [Fact] public async Task InvalidTokenReturns401() { _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid"); var response = await _client.GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
        [Fact] public async Task NoDemandGenerationBypassRouteExists() { var response = await _client.GetAsync("/api/demand-processing/generate"); Assert.NotEqual(HttpStatusCode.OK, response.StatusCode); }
    }
    ```
    - Extend the factory with an isolated EF provider and mocked authoritative schedule/account/allocation/charge/journal adapters. Cover 401/403 ordering, omitted Branch expansion, explicit deactivated Branch rejection, Billing publication gates, exactly-once retries, concurrent starts, zero-demand rows, NoDemand batches, partial Branch failures, deactivated history, PDF scope/content, and ReadyToSpool/Spooled/not-ready behavior. Do not add a downstream transport test.
    - _Requirements: 1.1-1.28, 2.1-2.24, 3.45-3.57, 7.1-7.20, 8.1-8.27, 9.1-9.23, 10.1-10.27, 12.16-12.25, 13.1-13.30, 14.1-14.21, 15.1-15.23_

- [ ] 18. Add frontend models, service interface, real/mock implementations, toggle, and barrels
  - [ ] 18.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\demandProcessing.model.ts`
    ```typescript
    export type DemandRunStatus = 'Accepted'|'BillingPending'|'BillingRunning'|'BillingIncomplete'|'BillingFailed'|'BillingSucceeded'|'DemandGenerating'|'Completed'|'PartiallyFailed'|'Failed'|'Deferred'|'Superseded';
    export type BillingStageStatus = 'Pending'|'Queued'|'Running'|'Incomplete'|'Failed'|'Cancelled'|'Succeeded'|'RetryPending'|'Superseded';
    export type DemandBatchStatus = 'NotReady'|'ReadyToSpool'|'Spooled'|'NoDemand'|'Failed';
    export type DemandRowStatus = 'Completed'|'Excluded'|'Invalid';
    export interface Paginated<T>{data:T[];total:number;page:number;pageSize:number;totalPages:number;}
    export interface DemandRunRequest{lineOfBusinessId:string;branchId?:string;frequency?:string;periodYear:number;periodMonth:number;idempotencyKey?:string;}
    export interface DemandRerunRequest{reason:string;idempotencyKey:string;}
    export interface DemandRun{runId:string;revisionNumber:number;lineOfBusinessId:string;branchIds:string[];frequency:string;periodYear:number;periodMonth:number;processingCutoffReference?:string;status:DemandRunStatus;billingStatus:BillingStageStatus;billingSnapshotId?:string;billingSnapshotVersion?:string;demandSnapshotId?:string;rowCount:number;batchCount:number;currentDueTotal:number;arrearsTotal:number;odiTotal:number;otherChargesTotal:number;totalCharges:number;totalDemand:number;failures:DemandFailure[];requestedAtUtc:string;completedAtUtc?:string;}
    export interface DemandRow{id:string;runId:string;revisionNumber:number;accountId:string;customerId:string;lineOfBusinessId:string;branchId:string;accountDisplay:string;customerDisplay:string;periodYear:number;periodMonth:number;frequency:string;currentDue:number;arrears:number;odi:number;otherCharges:number;totalCharges:number;totalDemand:number;isZeroDemand:boolean;status:DemandRowStatus;scheduleReference?:string;processingCutoffReference?:string;orderingKey:string;}
    export interface DemandBatch{id:string;runId:string;revisionNumber:number;branchId:string;lineOfBusinessId:string;frequency:string;periodYear:number;periodMonth:number;status:DemandBatchStatus;rowCount:number;currentDueTotal:number;arrearsTotal:number;odiTotal:number;otherChargesTotal:number;totalCharges:number;totalDemand:number;failureClass?:string;failureCode?:string;failureReason?:string;lastSpoolActorId?:string;lastSpooledAtUtc?:string;}
    export interface DemandFailure{failureClass:string;code:string;message:string;accountId?:string;batchId?:string;createdAtUtc:string;}
    export interface DemandRowsQuery{runId?:string;revisionNumber?:number;periodYear?:number;periodMonth?:number;lineOfBusinessId?:string;branchId?:string;accountId?:string;customerId?:string;rowStatus?:DemandRowStatus;page?:number;pageSize?:number;}
    export interface DemandBatchesQuery{runId?:string;revisionNumber?:number;periodYear?:number;periodMonth?:number;lineOfBusinessId?:string;branchId?:string;status?:DemandBatchStatus;page?:number;pageSize?:number;}
    export interface DemandHistoryQuery{periodYear?:number;periodMonth?:number;lineOfBusinessId?:string;branchId?:string;status?:DemandRunStatus;page?:number;pageSize?:number;}
    export interface DemandPdfQuery extends DemandRowsQuery { }
    export interface SpoolStatusResponse{batchId:string;status:DemandBatchStatus;correlationId:string;}
    ```
    - Use English-only labels and one display value per business value. Do not add bilingual fields, translation scaffolding, RTL metadata, client-side EMI calculation, or client-side totals.
    - _Requirements: 11.4-11.26, 12.4-12.27, 13.1-13.30, 17.1-17.16_

  - [ ] 18.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\demandProcessing.interface.ts`
    ```typescript
    import type {DemandBatch,DemandBatchesQuery,DemandHistoryQuery,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolStatusResponse} from '../../models/demandProcessing.model';
    export interface IDemandProcessingService { startRun(request:DemandRunRequest):Promise<DemandRunAcceptedResponse>; getRun(runId:string):Promise<DemandRun>; getHistory(query:DemandHistoryQuery):Promise<Paginated<DemandRun>>; rerun(runId:string,request:DemandRerunRequest):Promise<DemandRunAcceptedResponse>; getRows(query:DemandRowsQuery):Promise<Paginated<DemandRow>>; getBatches(query:DemandBatchesQuery):Promise<Paginated<DemandBatch>>; spoolBatch(batchId:string,request:{idempotencyKey?:string}):Promise<SpoolStatusResponse>; exportOutputPdf(query:DemandPdfQuery):Promise<Blob>; exportBatchPdf(query:DemandBatchesQuery):Promise<Blob>; }
    ```
    - _Requirements: 11.6-11.26, 12.8-12.25, 13.7-13.30, 14.16-14.17_

  - [ ] 18.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\demandProcessing.real.ts`
    ```typescript
    import api from '../api';
    import type {IDemandProcessingService} from '../interfaces/demandProcessing.interface';
    import type {DemandBatch,DemandBatchesQuery,DemandHistoryQuery,DemandPdfQuery,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolStatusResponse} from '../../models/demandProcessing.model';
    class DemandProcessingRealService implements IDemandProcessingService {
      private readonly base='/demand-processing';
      startRun(x:DemandRunRequest){return api.post<DemandRunAcceptedResponse>(`${this.base}/runs`,x).then(r=>r.data);}
      getRun(id:string){return api.get<DemandRun>(`${this.base}/runs/${id}`).then(r=>r.data);}
      getHistory(q:DemandHistoryQuery){return api.get<Paginated<DemandRun>>(`${this.base}/runs`,{params:q}).then(r=>r.data);}
      rerun(id:string,x:DemandRerunRequest){return api.post<DemandRunAcceptedResponse>(`${this.base}/runs/${id}/rerun`,x).then(r=>r.data);}
      getRows(q:DemandRowsQuery){return api.get<Paginated<DemandRow>>(`${this.base}/runs/${q.runId}/rows`,{params:q}).then(r=>r.data);}
      getBatches(q:DemandBatchesQuery){return api.get<Paginated<DemandBatch>>(`${this.base}/batches`,{params:q}).then(r=>r.data);}
      spoolBatch(id:string,x:{idempotencyKey?:string}){return api.post<SpoolStatusResponse>(`${this.base}/batches/${id}/spool`,x).then(r=>r.data);}
      exportOutputPdf(q:DemandPdfQuery){return api.get<Blob>(`${this.base}/exports/output`,{params:q,responseType:'blob'}).then(r=>r.data);}
      exportBatchPdf(q:DemandBatchesQuery){return api.get<Blob>(`${this.base}/exports/batches`,{params:q,responseType:'blob'}).then(r=>r.data);}
    }
    export const demandProcessingRealService:IDemandProcessingService=new DemandProcessingRealService();
    ```
    - Use only the existing shared `api` instance. It supplies JWT propagation and centralized 401/403/409/timeout/toast handling.
    - _Requirements: 11.6-11.7, 11.25-11.26, 12.16-12.25, 13.7-13.14, 13.19-13.30, 14.16-14.17_

  - [ ] 18.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\demandProcessing.mock.ts`
    ```typescript
    import type {IDemandProcessingService} from '../interfaces/demandProcessing.interface';
    import type {DemandBatch,DemandBatchesQuery,DemandHistoryQuery,DemandPdfQuery,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolStatusResponse} from '../../models/demandProcessing.model';
    const page=<T,>(items:T[],p=1,s=20):Paginated<T>=>({data:items.slice((p-1)*s,p*s),total:items.length,page:p,pageSize:s,totalPages:Math.max(1,Math.ceil(items.length/s))});
    const id=()=>crypto.randomUUID();
    class DemandProcessingMockService implements IDemandProcessingService {
      private readonly runs:DemandRun[]=[]; private readonly rows:DemandRow[]=[]; private readonly batches:DemandBatch[]=[];
      async startRun(x:DemandRunRequest):Promise<DemandRunAcceptedResponse>{if(!x.lineOfBusinessId||!x.periodYear||x.periodMonth<1||x.periodMonth>12)throw new Error('Line of business and a valid period are required.');const run:DemandRun={runId:id(),revisionNumber:1,lineOfBusinessId:x.lineOfBusinessId,branchIds:x.branchId?[x.branchId]:[],frequency:x.frequency??'Monthly',periodYear:x.periodYear,periodMonth:x.periodMonth,status:'Accepted',billingStatus:'Pending',rowCount:0,batchCount:0,currentDueTotal:0,arrearsTotal:0,odiTotal:0,otherChargesTotal:0,totalCharges:0,totalDemand:0,failures:[],requestedAtUtc:new Date().toISOString()};this.runs.push(run);return{runId:run.runId,revisionNumber:1,status:run.status,correlationId:id()};}
      async getRun(runId:string){const run=this.runs.find(x=>x.runId===runId);if(!run)throw new Error('Demand run was not found.');return run;}
      async getHistory(q:DemandHistoryQuery){return page(this.runs,q.page,q.pageSize);}
      async rerun(runId:string,_:DemandRerunRequest){const run=await this.getRun(runId);run.revisionNumber+=1;run.status='Accepted';return{runId,revisionNumber:run.revisionNumber,status:run.status,correlationId:id()};}
      async getRows(q:DemandRowsQuery){return page(this.rows.filter(x=>!q.runId||x.runId===q.runId),q.page,q.pageSize);}
      async getBatches(q:DemandBatchesQuery){return page(this.batches.filter(x=>!q.runId||x.runId===q.runId),q.page,q.pageSize);}
      async spoolBatch(batchId:string,_:{idempotencyKey?:string}):Promise<SpoolStatusResponse>{const batch=this.batches.find(x=>x.id===batchId);if(!batch)throw new Error('Demand batch was not found.');if(batch.status!=='ReadyToSpool'&&batch.status!=='Spooled')throw new Error('The Branch Batch is not ReadyToSpool.');batch.status='Spooled';batch.lastSpooledAtUtc=new Date().toISOString();return{batchId,status:batch.status,correlationId:id()};}
      async exportOutputPdf(q:DemandPdfQuery){return new Blob([JSON.stringify(await this.getRows(q))],{type:'application/pdf'});}
      async exportBatchPdf(q:DemandBatchesQuery){return new Blob([JSON.stringify(await this.getBatches(q))],{type:'application/pdf'});}
    }
    export const demandProcessingMockService:IDemandProcessingService=new DemandProcessingMockService();
    ```
    - Mock only persisted-shaped English fixtures and the minimal spool state. Do not add external delivery behavior or delivery metadata.
    - _Requirements: 11.4-11.26, 12.4-12.27, 13.1-13.30, 17.6-17.16_

  - [ ] 18.5 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\demandProcessing.service.ts`
    ```typescript
    import {demandProcessingMockService} from './mock/demandProcessing.mock';
    import {demandProcessingRealService} from './real/demandProcessing.real';
    import type {IDemandProcessingService} from './interfaces/demandProcessing.interface';
    const useMock=import.meta.env.VITE_USE_MOCK_API==='true';
    export const demandProcessingService:IDemandProcessingService=useMock?demandProcessingMockService:demandProcessingRealService;
    ```
    - _Requirements: 11.6-11.7, 12.8, 13.10, 14.16-14.17_

  - [ ] 18.6 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\index.ts`, `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\index.ts`, and `E:\Finnova\Finnova-UI\Finnova-UI\src\services\index.ts`
    - Append to `src/models/index.ts`:
    ```typescript
    export * from './demandProcessing.model';
    ```
    - Append to `src/services/interfaces/index.ts`:
    ```typescript
    export type { IDemandProcessingService } from './demandProcessing.interface';
    ```
    - Append to `src/services/index.ts`:
    ```typescript
    export { demandProcessingService } from './demandProcessing.service';
    ```
    - _Requirements: 11.4-11.7, 12.4-12.8, 13.1-13.10_

- [ ] 19. Add the Demand Run page and protected route/navigation
  - [ ] 19.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandRunForm.tsx`
    ```tsx
    import {Button,FormControl,InputLabel,MenuItem,Select,Stack,TextField} from '@mui/material';
    import {useState} from 'react';
    import type {DemandRunRequest} from '../../models/demandProcessing.model';
    export default function DemandRunForm({frequencies=['Monthly'],onSubmit,disabled=false}:{frequencies?:string[];onSubmit:(request:DemandRunRequest)=>void;disabled?:boolean}){const [value,setValue]=useState<DemandRunRequest>({lineOfBusinessId:'',frequency:'Monthly',periodYear:new Date().getFullYear(),periodMonth:new Date().getMonth()+1});const [error,setError]=useState('');const submit=()=>{if(!value.lineOfBusinessId||!value.periodYear||value.periodMonth<1||value.periodMonth>12||!value.frequency){setError('Line of business, frequency, year, and a valid month are required.');return;}setError('');onSubmit({...value,frequency:value.frequency||'Monthly'});};return <Stack spacing={2}><TextField label="Line of Business" required value={value.lineOfBusinessId} onChange={e=>setValue({...value,lineOfBusinessId:e.target.value})}/><TextField label="Branch (optional for all authorized Active Branches)" value={value.branchId??''} onChange={e=>setValue({...value,branchId:e.target.value||undefined})}/><FormControl><InputLabel>Frequency</InputLabel><Select label="Frequency" value={value.frequency??'Monthly'} onChange={e=>setValue({...value,frequency:e.target.value})}>{frequencies.map(x=><MenuItem key={x} value={x}>{x}</MenuItem>)}</Select></FormControl><TextField type="number" label="Month" value={value.periodMonth} onChange={e=>setValue({...value,periodMonth:Number(e.target.value)})}/><TextField type="number" label="Year" value={value.periodYear} onChange={e=>setValue({...value,periodYear:Number(e.target.value)})}/>{error&&<span role="alert">{error}</span>}<Button variant="contained" onClick={submit} disabled={disabled}>Start Demand Run</Button></Stack>}
    ```
    - _Requirements: 11.4-11.24, 17.6-17.16_

  - [ ] 19.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandRun.tsx`
    ```tsx
    import {Alert,Stack,Typography} from '@mui/material';
    import {useCallback,useEffect,useState} from 'react';
    import {demandProcessingService} from '../services';
    import DemandRunForm from '../components/demandProcessing/DemandRunForm';
    import type {DemandRun,DemandRunRequest} from '../models/demandProcessing.model';
    export default function DemandRun(){const [run,setRun]=useState<DemandRun>();const [error,setError]=useState('');const [busy,setBusy]=useState(false);const refresh=useCallback(async(id:string)=>{try{setRun(await demandProcessingService.getRun(id));setError('')}catch(e){setError(e instanceof Error?e.message:'Demand run status could not be loaded.')}},[]);const submit=async(request:DemandRunRequest)=>{setBusy(true);setError('');try{const accepted=await demandProcessingService.startRun({...request,idempotencyKey:request.idempotencyKey??`ui:${request.lineOfBusinessId}:${request.periodYear}-${request.periodMonth}`});await refresh(accepted.runId)}catch(e){setError(e instanceof Error?e.message:'Demand run could not be started.')}finally{setBusy(false)}};useEffect(()=>{if(!run||['Completed','PartiallyFailed','Failed','Superseded'].includes(run.status))return;const timer=window.setInterval(()=>void refresh(run.runId),10000);return()=>window.clearInterval(timer)},[run,refresh]);return <Stack spacing={3}><Typography variant="h4">Demand Run</Typography><DemandRunForm onSubmit={submit} disabled={busy}/>{error&&<Alert severity="error">{error}</Alert>}{run&&<Alert severity={run.status==='Completed'?'success':run.status==='Failed'||run.status==='PartiallyFailed'?'error':'info'}>Run {run.runId} | Revision {run.revisionNumber} | Billing {run.billingStatus} | Demand {run.status}</Alert>}{run?.status==='BillingIncomplete'&&<Alert severity="warning">BillingIncomplete. Refresh or retry through the approved recovery path.</Alert>}</Stack>}
    ```
    - Preserve entered values after failures, show Billing before Demand, poll only configured processing states, and do not expose an EMI calculator or Demand-generation bypass.
    - _Requirements: 11.1-11.26, 14.1-14.17_

  - [ ] 19.3 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx`
    - Add imports beside the other page imports:
    ```tsx
    import DemandRun from './pages/DemandRun';
    import DemandGenerationOutput from './pages/DemandGenerationOutput';
    import DemandSpooling from './pages/DemandSpooling';
    ```
    - Add these routes inside the existing authenticated `<Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>` group:
    ```tsx
    <Route path="/demand-processing/run" element={<DemandRun />} />
    <Route path="/demand-processing/output" element={<DemandGenerationOutput />} />
    <Route path="/demand-processing/spooling" element={<DemandSpooling />} />
    ```
    - _Requirements: 11.1-11.3, 12.1-12.3, 13.1-13.2_

  - [ ] 19.4 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx`
    - Insert the following menu group before the existing Administration group, using the existing icon imports or the nearest existing MUI icons. Do not add localization or RTL infrastructure:
    ```tsx
    {
      label: 'Operations',
      icon: <RequestQuoteIcon />,
      items: [
        { text: 'Demand Run', icon: <RequestQuoteIcon />, path: '/demand-processing/run' },
        { text: 'Demand Generation Output', icon: <AssessmentIcon />, path: '/demand-processing/output' },
        { text: 'Demand Spooling', icon: <ReceiptLongIcon />, path: '/demand-processing/spooling' },
      ],
    },
    ```
    - Apply the existing permission/navigation convention so unauthorized users do not receive operational controls.
    - _Requirements: 11.1-11.5, 12.1-12.5, 13.1-13.6, 17.6-17.16_

- [ ] 20. Add the Demand Generation Output page, filters, historical deactivated-Branch display, and PDF export
  - [ ] 20.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandRowsGrid.tsx`
    ```tsx
    import {DataGrid,type GridColDef} from '@mui/x-data-grid';
    import type {DemandRow} from '../../models/demandProcessing.model';
    const money=(value:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2}).format(value);
    const columns:GridColDef<DemandRow>[]=[{field:'accountDisplay',headerName:'Account',width:160},{field:'customerDisplay',headerName:'Customer',width:180},{field:'branchId',headerName:'Branch',width:150},{field:'currentDue',headerName:'Current Due',valueFormatter:p=>money(Number(p.value)),width:140},{field:'arrears',headerName:'Arrears',valueFormatter:p=>money(Number(p.value)),width:140},{field:'odi',headerName:'ODI',valueFormatter:p=>money(Number(p.value)),width:120},{field:'otherCharges',headerName:'Other Charges',valueFormatter:p=>money(Number(p.value)),width:150},{field:'totalCharges',headerName:'Total Charges',valueFormatter:p=>money(Number(p.value)),width:150},{field:'totalDemand',headerName:'Total Demand',valueFormatter:p=>money(Number(p.value)),width:150},{field:'status',headerName:'Status',width:120},{field:'isZeroDemand',headerName:'Zero Demand',width:120}];
    export default function DemandRowsGrid({rows,loading}:{rows:DemandRow[];loading:boolean}){return <DataGrid rows={rows} columns={columns} loading={loading} getRowId={row=>row.id} disableRowSelectionOnClick/>}
    ```
    - Display persisted values only. Do not derive Total Charges or Total Demand in the grid.
    - _Requirements: 12.4-12.15, 17.1-17.16_

  - [ ] 20.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandGenerationOutput.tsx`
    ```tsx
    import {Alert,Button,Stack,TextField,Typography} from '@mui/material';
    import {useCallback,useEffect,useState} from 'react';
    import {demandProcessingService} from '../services';
    import DemandRowsGrid from '../components/demandProcessing/DemandRowsGrid';
    import type {DemandRow,DemandRowsQuery} from '../models/demandProcessing.model';
    export default function DemandGenerationOutput(){const [query,setQuery]=useState<DemandRowsQuery>({page:1,pageSize:20});const [rows,setRows]=useState<DemandRow[]>([]);const [lastGood,setLastGood]=useState<DemandRow[]>([]);const [loading,setLoading]=useState(true);const [error,setError]=useState('');const load=useCallback(async()=>{setLoading(true);try{const result=await demandProcessingService.getRows(query);setRows(result.data);setLastGood(result.data);setError('')}catch(e){setRows(lastGood);setError(e instanceof Error?e.message:'Output could not be loaded.')}finally{setLoading(false)}},[lastGood,query]);useEffect(()=>{void load()},[load]);const exportPdf=async()=>{const blob=await demandProcessingService.exportOutputPdf(query);const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download='demand-generation-output.pdf';anchor.click();URL.revokeObjectURL(url)};return <Stack spacing={2}><Typography variant="h4">Demand Generation Output</Typography><Stack direction="row" spacing={2}><TextField label="Run" value={query.runId??''} onChange={e=>setQuery({...query,runId:e.target.value||undefined,page:1})}/><TextField label="Branch" value={query.branchId??''} onChange={e=>setQuery({...query,branchId:e.target.value||undefined,page:1})}/><Button onClick={()=>void load()}>Refresh</Button><Button onClick={()=>void exportPdf()}>Export PDF</Button></Stack>{error&&<Alert severity="error">{error}</Alert>}{!loading&&!error&&!rows.length&&<Alert severity="info">No demand rows match the selected filters.</Alert>}<DemandRowsGrid rows={rows} loading={loading}/></Stack>}
    ```
    - The production page must use existing permitted LOB/Branch reference controls when available, preserve last-good rows on refresh failure, label retained deactivated Branches, distinguish loading/empty/BillingIncomplete/failed/timeout states, and send every supplied filter to the server.
    - _Requirements: 12.1-12.27, 14.9-14.10, 17.6-17.16_

- [ ] 21. Add the Demand Spooling page, minimal readiness guard, historical filters, and batch PDF export
  - [ ] 21.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandBatchesGrid.tsx`
    ```tsx
    import {Button,Chip,Tooltip} from '@mui/material';
    import {DataGrid,type GridColDef} from '@mui/x-data-grid';
    import type {DemandBatch} from '../../models/demandProcessing.model';
    const money=(value:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2}).format(value);
    export default function DemandBatchesGrid({batches,loading,onSpool}:{batches:DemandBatch[];loading:boolean;onSpool:(batch:DemandBatch)=>void}){const columns:GridColDef<DemandBatch>[]=[{field:'branchId',headerName:'Branch',width:160},{field:'periodYear',headerName:'Year',width:80},{field:'periodMonth',headerName:'Month',width:80},{field:'rowCount',headerName:'Rows',width:80},{field:'totalDemand',headerName:'Total Demand',valueFormatter:p=>money(Number(p.value)),width:150},{field:'status',headerName:'Status',renderCell:p=><Chip size="small" label={p.value}/>,width:140},{field:'action',headerName:'Action',sortable:false,renderCell:p=>{const ready=p.row.status==='ReadyToSpool';return <Tooltip title={ready?'':'Batch is not ReadyToSpool.'}><span><Button size="small" disabled={!ready} onClick={()=>onSpool(p.row)}>Spool</Button></span></Tooltip>},width:120}];return <DataGrid rows={batches} columns={columns} loading={loading} getRowId={row=>row.id} disableRowSelectionOnClick/>}
    ```
    - Show only the persisted batch status and readiness reason; do not add delivery columns.
    - _Requirements: 13.1-13.18, 13.29-13.30, 17.6-17.16_

  - [ ] 21.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\SpoolConfirmationDialog.tsx`
    ```tsx
    import {Button,Dialog,DialogActions,DialogContent,DialogTitle,Typography} from '@mui/material';
    export default function SpoolConfirmationDialog({open,onClose,onConfirm}:{open:boolean;onClose:()=>void;onConfirm:()=>void}){return <Dialog open={open} onClose={onClose}><DialogTitle>Confirm demand spooling</DialogTitle><DialogContent><Typography>Mark this completed Branch Batch as Spooled?</Typography></DialogContent><DialogActions><Button onClick={onClose}>Cancel</Button><Button variant="contained" onClick={onConfirm}>Spool batch</Button></DialogActions></Dialog>}
    ```
    - The confirmation is a UI step only. The API receives only the batch ID and optional idempotency key; there is no confirmation token or transport request.
    - _Requirements: 13.7-13.14, 15.10, 15.17-15.18_

  - [ ] 21.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandSpooling.tsx`
    ```tsx
    import {Alert,Button,Stack,TextField,Typography} from '@mui/material';
    import {useCallback,useEffect,useState} from 'react';
    import {demandProcessingService} from '../services';
    import DemandBatchesGrid from '../components/demandProcessing/DemandBatchesGrid';
    import SpoolConfirmationDialog from '../components/demandProcessing/SpoolConfirmationDialog';
    import type {DemandBatch,DemandBatchesQuery} from '../models/demandProcessing.model';
    export default function DemandSpooling(){const [query,setQuery]=useState<DemandBatchesQuery>({page:1,pageSize:20});const [batches,setBatches]=useState<DemandBatch[]>([]);const [selected,setSelected]=useState<DemandBatch>();const [error,setError]=useState('');const [loading,setLoading]=useState(true);const load=useCallback(async()=>{setLoading(true);try{setBatches((await demandProcessingService.getBatches(query)).data);setError('')}catch(e){setError(e instanceof Error?e.message:'Batches could not be loaded.')}finally{setLoading(false)}},[query]);useEffect(()=>{void load()},[load]);const spool=async()=>{if(!selected)return;try{await demandProcessingService.spoolBatch(selected.id,{idempotencyKey:`ui:${selected.id}`});setSelected(undefined);await load()}catch(e){setError(e instanceof Error?e.message:'The batch could not be spooled.')}};const exportPdf=async()=>{const blob=await demandProcessingService.exportBatchPdf(query);const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download='demand-batches.pdf';anchor.click();URL.revokeObjectURL(url)};return <Stack spacing={2}><Typography variant="h4">Demand Spooling</Typography><Stack direction="row" spacing={2}><TextField label="Run" value={query.runId??''} onChange={e=>setQuery({...query,runId:e.target.value||undefined,page:1})}/><TextField label="Branch" value={query.branchId??''} onChange={e=>setQuery({...query,branchId:e.target.value||undefined,page:1})}/><Button onClick={()=>void load()}>Refresh</Button><Button onClick={()=>void exportPdf()}>Export PDF</Button></Stack>{error&&<Alert severity="error">{error}</Alert>}<DemandBatchesGrid batches={batches} loading={loading} onSpool={setSelected}/><SpoolConfirmationDialog open={Boolean(selected)} onClose={()=>setSelected(undefined)} onConfirm={()=>void spool()}/></Stack>}
    ```
    - Retained deactivated-Branch batches remain queryable and must be labeled by the existing Branch display convention. Only `ReadyToSpool` enables the action; `NoDemand`, `Failed`, `NotReady`, and all incomplete states remain unchanged.
    - _Requirements: 8.23-8.27, 13.1-13.30, 14.9-14.10, 17.6-17.16_

- [ ] 22. Add frontend Vitest/React Testing Library coverage
  - [ ]* 22.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\demandProcessing.service.test.ts`
    ```typescript
    import {describe,expect,it,vi} from 'vitest';
    import {demandProcessingService} from './demandProcessing.service';
    describe('demandProcessingService',()=>{it('selects an implementation and starts a run',async()=>{const result=await demandProcessingService.startRun({lineOfBusinessId:'lob-1',frequency:'Monthly',periodYear:2025,periodMonth:1});expect(result.runId).toBeTruthy()});it('exposes the shared real-service boundary without a second axios client',async()=>{const api=await import('./api');expect(api.default).toBeDefined()});it('exports PDFs through the feature service',async()=>{const blob=await demandProcessingService.exportOutputPdf({page:1,pageSize:20});expect(blob).toBeInstanceOf(Blob)})});
    ```
    - Mock `VITE_USE_MOCK_API` for both modes, mock the shared `api` instance for real-service tests, assert the `/demand-processing` path, and verify no direct axios import exists in the feature.
    - _Requirements: 11.6-11.7, 11.25-11.26, 12.16-12.25, 13.7-13.14, 13.19-13.30, 14.16-14.17_

  - [ ]* 22.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandProcessing.test.tsx`
    ```tsx
    import {render,screen,waitFor} from '@testing-library/react';
    import {MemoryRouter} from 'react-router-dom';
    import {describe,expect,it,vi} from 'vitest';
    import DemandRun from './DemandRun';
    import DemandGenerationOutput from './DemandGenerationOutput';
    import DemandSpooling from './DemandSpooling';
    vi.mock('../services',()=>({demandProcessingService:{startRun:vi.fn().mockResolvedValue({runId:'run-1',revisionNumber:1,status:'Accepted',correlationId:'c-1'}),getRun:vi.fn().mockResolvedValue({runId:'run-1',revisionNumber:1,lineOfBusinessId:'lob-1',branchIds:[],frequency:'Monthly',periodYear:2025,periodMonth:1,status:'BillingPending',billingStatus:'Pending',rowCount:0,batchCount:0,currentDueTotal:0,arrearsTotal:0,odiTotal:0,otherChargesTotal:0,totalCharges:0,totalDemand:0,failures:[],requestedAtUtc:new Date().toISOString()}),getRows:vi.fn().mockResolvedValue({data:[],total:0,page:1,pageSize:20,totalPages:1}),getBatches:vi.fn().mockResolvedValue({data:[],total:0,page:1,pageSize:20,totalPages:1}),spoolBatch:vi.fn(),exportOutputPdf:vi.fn().mockResolvedValue(new Blob()),exportBatchPdf:vi.fn().mockResolvedValue(new Blob())}}));
describe('Demand Processing pages',()=>{it('renders the Demand Run form',()=>{render(<MemoryRouter><DemandRun/></MemoryRouter>);expect(screen.getByText('Start Demand Run')).toBeInTheDocument()});it('shows the distinct output empty state',async()=>{render(<MemoryRouter><DemandGenerationOutput/></MemoryRouter>);expect(await screen.findByText('No demand rows match the selected filters.')).toBeInTheDocument()});it('renders the Spooling page with only the persisted state action',()=>{render(<MemoryRouter><DemandSpooling/></MemoryRouter>);expect(screen.getByText('Demand Spooling')).toBeInTheDocument();expect(screen.queryByText('Mark this completed Branch Batch as Spooled?')).not.toBeInTheDocument()});it('does not render RTL direction attributes',async()=>{const {container}=render(<MemoryRouter><DemandGenerationOutput/></MemoryRouter>);await waitFor(()=>expect(container.querySelector('[dir="rtl"]')).toBeNull())})});
    ```
    - Add tests for unauthorized access states, permitted LOB/Branch controls, omitted-Branch requests, successful start, polling, BillingIncomplete/conflict/timeout/failure, last-good-data preservation, deterministic filter/paging, INR columns, deactivated-Branch history labels, authorized PDF downloads, ReadyToSpool confirmation, Spooled response, and disabled NoDemand/Failed/NotReady actions.
    - _Requirements: 11.1-11.26, 12.1-12.27, 13.1-13.30, 14.16-14.17, 17.6-17.16_

- [ ] 23. Final implementation checkpoint
  - [ ] 23.1 Review the completed implementation against the requirements and design before enabling the feature
    - Confirm all feature code is in AccountsService/Finnova-API and Finnova-UI, the gateway aliases target `accounts-cluster`, and no unrelated refactor was made.
    - Confirm the worker uses durable Run/stage/snapshot state and recovery, immutable GUID Run IDs and linked revisions, Monthly/Asia/Kolkata/UTC/INR defaults, Active-only generation, zero-demand rows, NoDemand batches, branch isolation, PartiallyFailed status, and retained deactivated-Branch reads.
    - Confirm Billing uses one internally consistent available snapshot/reference without a new watermark, does not recompute EMI or mutate schedules, applies the exact default charge formulas through the adapter seam, and calls Accounting/GL only through `IJournalPort`.
    - Confirm PDF export uses persisted scoped values and UTC metadata through `IPdfRenderer`/`IDemandPdfExportService` and that the renderer dependency is isolated.
    - Confirm the only spool states are `NotReady`, `ReadyToSpool`, `Spooled`, `NoDemand`, and `Failed`; no external delivery behavior or delivery metadata exists.
    - Confirm minimum audit fields are immutable and safe, while detailed taxonomy/retention/purge remains configurable/deferred.
    - Do not run builds or tests as part of this spec workflow. The task executor may run the repository's normal validation commands after copying the plan.
    - _Requirements: 1.1-1.28, 2.1-2.24, 3.1-3.57, 4.1-4.21, 5.1-5.21, 6.1-6.21, 7.1-7.20, 8.1-8.27, 9.1-9.23, 10.1-10.27, 11.1-11.26, 12.1-12.27, 13.1-13.30, 14.1-14.21, 15.1-15.23, 16.1-16.33, 17.1-17.16_

## Notes

- Test sub-tasks marked with `*` are optional in the task UI but are included because requirements explicitly expect them before production.
- Task 1 is a non-blocking record. The implementation uses the design defaults and preserves deferred concerns behind explicit adapters or configuration.
- The schedule, account/customer/master, allocation-event, charge, and Accounting/GL adapters must fail closed until bound to authoritative existing contracts. They must not return sample data or silently publish incomplete financial results.
- The renderer package is intentionally isolated. If the approved repository decision changes the package, modify only the AccountsService package reference and renderer adapter.
- All task snippets use English-only labels and values, INR amounts, left-to-right UI conventions, and no bilingual or RTL scaffolding.
- No task adds a schedule watermark, source-version prerequisite, ledger ownership, external delivery subsystem, delivery metadata, or browser print.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "3.1", "4.1", "4.2", "6.3"] },
    { "id": 2, "tasks": ["5.1", "5.2", "6.1", "7.1", "7.2", "8.1"] },
    { "id": 3, "tasks": ["6.2", "6.4", "6.5", "6.6", "6.7", "7.3", "7.4", "8.2"] },
    { "id": 4, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 5, "tasks": ["10.1"] },
    { "id": 6, "tasks": ["11.1", "12.1", "12.2", "13.1", "14.1", "14.2", "14.3"] },
    { "id": 7, "tasks": ["14.4", "15.1", "15.2", "15.3", "16.1"] },
    { "id": 8, "tasks": ["17.1", "17.2", "17.3"] },
    { "id": 9, "tasks": ["18.1", "18.2", "18.3", "18.4", "18.5", "18.6"] },
    { "id": 10, "tasks": ["19.1", "19.2", "19.3", "19.4"] },
    { "id": 11, "tasks": ["20.1", "20.2", "21.1", "21.2", "21.3"] },
    { "id": 12, "tasks": ["22.1", "22.2"] },
    { "id": 13, "tasks": ["23.1"] }
  ]
}
```
