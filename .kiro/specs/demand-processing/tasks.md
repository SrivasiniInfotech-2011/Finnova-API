# Implementation Plan: Demand Processing

## Overview

This task list is the copy/paste implementation sequence for the existing Demand Processing requirements and design. It covers the AccountsService backend in `e:\Finnova\Finnova-API` and the React frontend in `E:\Finnova\Finnova-UI\Finnova-UI`. It does not modify `requirements.md`, `design.md`, or `.config.kiro`.

Every code block is intended to be copied into the path named immediately above it. `MODIFY` blocks are exact insertion/replacement snippets and identify their anchor. New source files are picked up by the existing SDK-style projects; do not edit project files unless the local checkout proves that implicit compilation has been disabled.

### Confirmation gates before implementation

Do not copy implementation tasks past Task 1 until the product owner confirms these design assumptions:

- Billing completion marker and immutable snapshot contract, including the three required checks and source ownership.
- Supported Frequency values, Month/Year validation, and the `Asia/Kolkata` business timezone.
- Whether omitted Branch means all authorized branches for the selected LOB.
- Current Due, Arrears, ODI, Other Charges treatment for part-payments, waivers, reversals, adjustments, and suspense.
- Account status vocabulary and processing cut-off source.
- Empty-branch and zero-demand policies.
- Manual-run/rerun permissions, confirmation behavior, and post-spooling revision policy.
- Branch-isolation policy for partial failures.
- Spool transport, acknowledgement, retry, and downstream idempotency contract.
- Exact authorization policy names/roles, scope resolver, retention period, and purge authority.

The code below uses the design's proposed defaults where the gate is confirmed: AccountsService, `Active` eligibility, no empty batch but a `NoDemand` outcome, retained zero-demand rows, `decimal(19,2)` INR, `MidpointRounding.AwayFromZero`, immutable revisions, branch isolation, and at-least-once spool transport with a stable logical key. If a gate answer differs, update the contracts and affected tasks before implementation.

## Tasks

- [ ] 1. Confirm source contracts and runtime configuration before copying implementation code
  - [ ] 1.1 Record the approved answers to the confirmation gates and configure the existing AccountsService before copying implementation code
    - Record the approved answers to the confirmation gates above in the implementation change or ticket.
    - Configure the existing AccountsService with the approved Billing adapter, policy names, polling interval, lease duration, spool endpoint, and retention settings. Do not add secrets to source control.
  - Do not change `.kiro/specs/demand-processing/.config.kiro`.
  - _Requirements: 1.10, 2.1-2.7, 4.5-4.7, 5.2, 6.5, 7.5-7.8, 8.5-8.6, 9.1-9.7, 13.3-13.9, 16.5-16.6_

- [ ] 2. Create backend domain entities, enums, and value objects
  - [ ] 2.1 CREATE `e:\Finnova\Finnova-API\Finnova.Models\Domain\Enums\DemandEnums.cs`
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum DemandRunStatus { Accepted, BillingPending, Calculating, Completed, PartiallyFailed, Failed, Deferred, Superseded, Conflict }
    public enum DemandBatchStatus { Pending, ReadyToSpool, Spooled, Failed, ReSpoolPending, NoDemand }
    public enum DemandRowStatus { Completed, Excluded, Invalid }
    public enum DemandFailureClass { BillingIncomplete, Dependency, Transient, Permanent, DataQuality, Reconciliation, Authorization, Idempotency, Calculation, Spooling }
    public enum DemandSource { User, Automizer }
    public enum BillingCompletionState { Succeeded, Failed, Cancelled, Partial, InProgress }
    ```
    - Keep enum serialization as strings through the existing AccountsService JSON configuration.
    - _Requirements: 1.7-1.9, 2.2-2.5, 3.1-3.6, 5.1-5.8, 6.2, 7.1, 8.3-8.6, 9.5_

  - [ ] 2.2 CREATE `e:\Finnova\Finnova-API\Finnova.Models\Domain\ValueObjects\DemandScope.cs`
    ```csharp
    namespace Finnova.Models.Domain.ValueObjects;

    public sealed record DemandScope(Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds)
    {
        public string CanonicalValue => $"{LineOfBusinessId:N}:{string.Join(',', BranchIds.OrderBy(x => x).Distinct().Select(x => x.ToString("N")))}";
        public static DemandScope Normalize(Guid lineOfBusinessId, IEnumerable<Guid> branchIds)
        {
            if (lineOfBusinessId == Guid.Empty) throw new ArgumentException("LineOfBusinessId is required.", nameof(lineOfBusinessId));
            var normalized = branchIds.Where(x => x != Guid.Empty).Distinct().OrderBy(x => x).ToArray();
            if (normalized.Length == 0) throw new ArgumentException("At least one branch is required.", nameof(branchIds));
            return new DemandScope(lineOfBusinessId, normalized);
        }
    }

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
    ```
    - The constructor validation is the single pure period/scope boundary used by validators and handlers.
    - _Requirements: 1.4-1.10, 6.1-6.2, 15.1-15.5, 16.5-16.6_

  - [ ] 2.3 CREATE `e:\Finnova\Finnova-API\Finnova.Models\Domain\Entities\DemandEntities.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class DemandRun
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string LogicalScopeKey { get; set; } = string.Empty;
        public Guid LineOfBusinessId { get; set; }
        public string Frequency { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string? BillingReference { get; set; }
        public string? BillingSnapshotVersion { get; set; }
        public DateTime? ProcessingCutoffUtc { get; set; }
        public DemandRunStatus Status { get; set; } = DemandRunStatus.Accepted;
        public DemandSource Source { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string? IdempotencyKey { get; set; }
        public string? IdempotencyFingerprint { get; set; }
        public int CurrentRevisionNumber { get; set; } = 1;
        public int RowCount { get; set; }
        public int BatchCount { get; set; }
        public decimal CurrentDueTotal { get; set; }
        public decimal ArrearsTotal { get; set; }
        public decimal OdiTotal { get; set; }
        public decimal OtherChargesTotal { get; set; }
        public decimal TotalCharges { get; set; }
        public decimal TotalDemand { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
        public List<DemandRunBranchScope> BranchScopes { get; set; } = new();
        public List<DemandRunRevision> Revisions { get; set; } = new();
    }

    public sealed class DemandRunRevision
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DemandRunId { get; set; }
        public int RevisionNumber { get; set; }
        public int? SupersedesRevisionNumber { get; set; }
        public DemandRunStatus Status { get; set; } = DemandRunStatus.Accepted;
        public string BillingReference { get; set; } = string.Empty;
        public string BillingSnapshotVersion { get; set; } = string.Empty;
        public DateTime ProcessingCutoffUtc { get; set; }
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

    public sealed class DemandRunBranchScope
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DemandRunId { get; set; }
        public int RevisionNumber { get; set; }
        public Guid BranchId { get; set; }
        public int SortOrder { get; set; }
        public DemandRun DemandRun { get; set; } = null!;
    }

    public sealed class DemandRow
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DemandRunRevisionId { get; set; }
        public Guid DemandBatchId { get; set; }
        public Guid DemandRunId { get; set; }
        public int RevisionNumber { get; set; }
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
        public string SourceSnapshotVersion { get; set; } = string.Empty;
        public DateTime ProcessingCutoffUtc { get; set; }
        public string OrderingKey { get; set; } = string.Empty;
        public DemandRunRevision Revision { get; set; } = null!;
        public DemandBatch Batch { get; set; } = null!;
    }

    public sealed class DemandBatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DemandRunRevisionId { get; set; }
        public Guid DemandRunId { get; set; }
        public int RevisionNumber { get; set; }
        public Guid BranchId { get; set; }
        public Guid LineOfBusinessId { get; set; }
        public string Frequency { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public DemandBatchStatus Status { get; set; } = DemandBatchStatus.Pending;
        public int RowCount { get; set; }
        public decimal CurrentDueTotal { get; set; }
        public decimal ArrearsTotal { get; set; }
        public decimal OdiTotal { get; set; }
        public decimal OtherChargesTotal { get; set; }
        public decimal TotalCharges { get; set; }
        public decimal TotalDemand { get; set; }
        public string SpoolIdempotencyKey { get; set; } = string.Empty;
        public Guid? LastSpoolAttemptId { get; set; }
        public DateTime? LastSpooledAtUtc { get; set; }
        public string? LastDownstreamReference { get; set; }
        public DemandFailureClass? FailureClass { get; set; }
        public string? FailureCode { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
        public DemandRunRevision Revision { get; set; } = null!;
        public List<DemandRow> Rows { get; set; } = new();
        public List<DemandSpoolAttempt> SpoolAttempts { get; set; } = new();
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

    public sealed class DemandSpoolAttempt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DemandBatchId { get; set; }
        public int AttemptNumber { get; set; }
        public DemandSource Source { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string TransportStatus { get; set; } = string.Empty;
        public bool Acknowledged { get; set; }
        public string? DownstreamReference { get; set; }
        public string? FailureCode { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? AcknowledgedAtUtc { get; set; }
        public DemandBatch Batch { get; set; } = null!;
    }

    public sealed class DemandAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DemandRunId { get; set; }
        public Guid? DemandBatchId { get; set; }
        public Guid? DemandSpoolAttemptId { get; set; }
        public int? RevisionNumber { get; set; }
        public Guid? BranchId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? PreviousState { get; set; }
        public string? NewState { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public DemandSource Source { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public string? BillingReference { get; set; }
        public string? BillingSnapshotVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```
    - Keep these records append-only in the repository API. Do not add update/delete commands for financial output, failures, spool attempts, or audit entries.
    - _Requirements: 1.7-1.9, 2.6, 3.7, 4.1-4.8, 5.1-5.8, 6.2, 6.7, 7.1-7.8, 8.1-8.6, 9.1-9.7, 12.5-12.8, 14.1-14.8, 15.1-15.5_

- [ ] 3. Create typed backend exceptions and stable Demand error codes
  - [ ] 3.1 CREATE `e:\Finnova\Finnova-API\Finnova.Models\Domain\Exceptions\DemandProcessingExceptions.cs`
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    public abstract class DemandProcessingException : Exception
    {
        protected DemandProcessingException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public sealed class DemandValidationException(string message, string code = "ERR-DEMAND-VALIDATION") : DemandProcessingException(message, code);
    public sealed class DemandBillingIncompleteException(string message = "Billing has not completed for the requested scope and period.") : DemandProcessingException(message, "ERR-DEMAND-BILLING-INCOMPLETE");
    public sealed class DemandBillingScopeMismatchException(string message = "The Billing completion scope does not match the Demand scope.") : DemandProcessingException(message, "ERR-DEMAND-BILLING-SCOPE");
    public sealed class DemandNotFoundException(Guid id) : DemandProcessingException($"Demand record '{id}' was not found.", "ERR-DEMAND-NOT-FOUND");
    public sealed class DemandIdempotencyConflictException(string message = "The idempotency key was already used with a different request.") : DemandProcessingException(message, "ERR-DEMAND-IDEMPOTENCY-CONFLICT");
    public sealed class DemandConcurrencyException(string message = "Another Demand Processing request owns this scope.") : DemandProcessingException(message, "ERR-DEMAND-CONCURRENT");
    public sealed class DemandDataQualityException(string message) : DemandProcessingException(message, "ERR-DEMAND-DATA-QUALITY");
    public sealed class DemandReconciliationException(string message) : DemandProcessingException(message, "ERR-DEMAND-RECONCILIATION");
    public sealed class DemandDependencyException(string message) : DemandProcessingException(message, "ERR-DEMAND-DEPENDENCY");
    public sealed class DemandSpoolingException(string message) : DemandProcessingException(message, "ERR-DEMAND-SPOOL");
    ```
    - Never put bearer tokens, connection strings, raw customer payloads, or source secrets in exception messages.
    - _Requirements: 1.2-1.6, 2.2-2.7, 8.4, 9.1-9.7, 13.1-13.7, 14.4-14.5_

- [ ] 4. Create backend contracts and source/worker ports
  - [ ] 4.1 CREATE `e:\Finnova\Finnova-API\Finnova.Models\Contracts\DemandProcessing\DemandContracts.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.DemandProcessing;

    public sealed record StartDemandRunRequest(Guid LineOfBusinessId, Guid? BranchId, string Frequency, int PeriodYear, int PeriodMonth, string? IdempotencyKey, string? ConfirmationToken);
    public sealed record StartDemandRerunRequest(string Reason, string IdempotencyKey, string ConfirmationToken);
    public sealed record SpoolBatchRequest(string? IdempotencyKey, string? ConfirmationToken);
    public sealed record DemandRowsQuery(Guid? RunId, int? RevisionNumber, int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, Guid? AccountId, Guid? CustomerId, string? RowStatus, int Page = 1, int PageSize = 20);
    public sealed record DemandBatchesQuery(Guid? RunId, int? RevisionNumber, int? PeriodYear, int? PeriodMonth, Guid? LineOfBusinessId, Guid? BranchId, DemandBatchStatus? Status, int Page = 1, int PageSize = 20);
    public sealed record DemandRunAcceptedResponse(Guid RunId, int RevisionNumber, DemandRunStatus Status, string CorrelationId);
    public sealed record DemandFailureResponse(string FailureClass, string Code, string Message, Guid? AccountId, Guid? BatchId, DateTime CreatedAtUtc);
    public sealed record DemandRunResponse(Guid RunId, int RevisionNumber, Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds, string Frequency, int PeriodYear, int PeriodMonth, string? BillingReference, string? BillingSnapshotVersion, DateTime? ProcessingCutoffUtc, DemandRunStatus Status, DemandSource Source, int RowCount, int BatchCount, decimal CurrentDueTotal, decimal ArrearsTotal, decimal OdiTotal, decimal OtherChargesTotal, decimal TotalCharges, decimal TotalDemand, IReadOnlyList<DemandFailureResponse> Failures, DateTime CreatedAtUtc, DateTime? CompletedAtUtc);
    public sealed record DemandRowResponse(Guid Id, Guid RunId, int RevisionNumber, Guid AccountId, Guid CustomerId, Guid LineOfBusinessId, Guid BranchId, string AccountDisplay, string CustomerDisplay, int PeriodYear, int PeriodMonth, string Frequency, decimal CurrentDue, decimal Arrears, decimal Odi, decimal OtherCharges, decimal TotalCharges, decimal TotalDemand, bool IsZeroDemand, DemandRowStatus Status, string SnapshotVersion, string OrderingKey);
    public sealed record DemandBatchResponse(Guid Id, Guid RunId, int RevisionNumber, Guid BranchId, Guid LineOfBusinessId, string Frequency, int PeriodYear, int PeriodMonth, DemandBatchStatus Status, int RowCount, decimal CurrentDueTotal, decimal ArrearsTotal, decimal OdiTotal, decimal OtherChargesTotal, decimal TotalCharges, decimal TotalDemand, DateTime? LastSpooledAtUtc, string? LastDownstreamReference, string? FailureCode);
    public sealed record SpoolAcceptedResponse(Guid BatchId, DemandBatchStatus Status, Guid AttemptId, string? DownstreamReference, string CorrelationId);
    public sealed record SpoolAttemptResponse(Guid Id, Guid BatchId, int AttemptNumber, string TransportStatus, bool Acknowledged, string? DownstreamReference, string? FailureCode, DateTime CreatedAtUtc);
    public sealed record PaginatedResponse<T>(IReadOnlyList<T> Data, int Total, int Page, int PageSize, int TotalPages);
    public sealed record BillingScope(Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds, string Frequency, int PeriodYear, int PeriodMonth);
    public sealed record BillingCompletion(string Reference, BillingScope Scope, BillingCompletionState State, string SnapshotVersion, DateTime ProcessingCutoffUtc, IReadOnlyDictionary<string, bool> RequiredChecks);
    public sealed record SourceAccount(Guid AccountId, Guid CustomerId, Guid LineOfBusinessId, Guid BranchId, string AccountDisplay, string CustomerDisplay, string Status, decimal? CurrentDue, decimal? Arrears, decimal? Odi, decimal? OtherCharges, string SnapshotVersion);
    public sealed record AuthorizedDemandScope(IReadOnlySet<Guid> LineOfBusinessIds, IReadOnlySet<Guid> BranchIds);
    public sealed record SpoolAcknowledgement(bool Acknowledged, string TransportStatus, string? DownstreamReference, string? FailureCode);
    ```
    - If the confirmed Billing contract has different field names or a different snapshot transport, adapt only the adapter/port boundary; keep Demand-owned contracts stable.
    - _Requirements: 1.4-1.9, 2.1-2.7, 3.1-3.7, 4.1-4.8, 5.1-5.8, 6.1-6.7, 7.1-7.8, 9.5, 10.2-10.9, 11.2-11.8, 12.2-12.8_

  - [ ] 4.2 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Ports\DemandPorts.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.DemandProcessing.Ports;

    public interface IBillingCompletionPort
    {
        Task<BillingCompletion?> FindAsync(BillingScope scope, CancellationToken ct);
        Task<BillingCompletion> EnsureSuccessfulAsync(BillingCompletion completion, CancellationToken ct);
    }
    public interface IBillingSnapshotReader
    {
        IAsyncEnumerable<SourceAccount> ReadAccountsAsync(BillingCompletion completion, BillingScope scope, CancellationToken ct);
    }
    public interface IAuthorizationScopeResolver
    {
        Task<AuthorizedDemandScope> ResolveAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct);
    }
    public interface IDemandLeaseStore
    {
        Task<bool> TryAcquireAsync(Guid runId, string logicalScopeKey, CancellationToken ct);
        Task<bool> RenewAsync(Guid runId, CancellationToken ct);
        Task ReleaseAsync(Guid runId, CancellationToken ct);
    }
    public interface IDemandSpoolTransport
    {
        Task<SpoolAcknowledgement> SendAsync(DemandBatch batch, string idempotencyKey, CancellationToken ct);
    }
    public interface IClock { DateTime UtcNow { get; } }
    public interface IDemandWorkQueue { ValueTask EnqueueAsync(Guid runId, CancellationToken ct); }
    public interface IDemandProcessor { Task ProcessAsync(Guid runId, CancellationToken ct); }
    ```
    - The Billing adapter must reject mixed snapshot versions. Tests use mocks; production must use the approved source contract.
    - _Requirements: 2.1-2.7, 3.1-3.7, 6.1-6.7, 8.1-8.6, 9.1-9.7, 13.4-13.6_

- [ ] 5. Add EF Core configurations, DbSets, constraints, and migration command
  - [ ] 5.1 CREATE `e:\Finnova\Finnova-API\Finnova.Repository\Configuration\DemandConfigurations.cs`
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
            b.Property(x => x.LogicalScopeKey).IsRequired().HasMaxLength(512);
            b.Property(x => x.Frequency).IsRequired().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.ActorId).IsRequired().HasMaxLength(200);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            b.Property(x => x.IdempotencyKey).HasMaxLength(200);
            b.Property(x => x.IdempotencyFingerprint).HasMaxLength(128);
            foreach (var n in new[] { nameof(DemandRun.CurrentDueTotal), nameof(DemandRun.ArrearsTotal), nameof(DemandRun.OdiTotal), nameof(DemandRun.OtherChargesTotal), nameof(DemandRun.TotalCharges), nameof(DemandRun.TotalDemand) }) b.Property<decimal>(n).HasPrecision(19, 2);
            b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
            b.HasIndex(x => new { x.LogicalScopeKey, x.CurrentRevisionNumber }).IsUnique();
            b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.LineOfBusinessId, x.Status });
            b.HasMany(x => x.Revisions).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.BranchScopes).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
        }
    }
    public sealed class DemandRunRevisionConfiguration : IEntityTypeConfiguration<DemandRunRevision>
    {
        public void Configure(EntityTypeBuilder<DemandRunRevision> b)
        {
            b.ToTable("demand_run_revisions"); b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            b.Property(x => x.BillingReference).IsRequired().HasMaxLength(200); b.Property(x => x.BillingSnapshotVersion).IsRequired().HasMaxLength(200);
            foreach (var n in new[] { nameof(DemandRunRevision.CurrentDueTotal), nameof(DemandRunRevision.ArrearsTotal), nameof(DemandRunRevision.OdiTotal), nameof(DemandRunRevision.OtherChargesTotal), nameof(DemandRunRevision.TotalCharges), nameof(DemandRunRevision.TotalDemand) }) b.Property<decimal>(n).HasPrecision(19, 2);
            b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber }).IsUnique();
        }
    }
    public sealed class DemandRunBranchScopeConfiguration : IEntityTypeConfiguration<DemandRunBranchScope>
    {
        public void Configure(EntityTypeBuilder<DemandRunBranchScope> b) { b.ToTable("demand_run_branch_scopes"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.BranchId }).IsUnique(); }
    }
    public sealed class DemandRowConfiguration : IEntityTypeConfiguration<DemandRow>
    {
        public void Configure(EntityTypeBuilder<DemandRow> b)
        {
            b.ToTable("demand_rows"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.AccountDisplay).IsRequired().HasMaxLength(200); b.Property(x => x.CustomerDisplay).IsRequired().HasMaxLength(200); b.Property(x => x.SourceSnapshotVersion).IsRequired().HasMaxLength(200); b.Property(x => x.OrderingKey).IsRequired().HasMaxLength(200);
            foreach (var n in new[] { nameof(DemandRow.CurrentDue), nameof(DemandRow.Arrears), nameof(DemandRow.Odi), nameof(DemandRow.OtherCharges), nameof(DemandRow.TotalCharges), nameof(DemandRow.TotalDemand) }) b.Property<decimal>(n).HasPrecision(19, 2);
            b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.AccountId }).IsUnique(); b.HasIndex(x => new { x.BranchId, x.LineOfBusinessId, x.ProcessingCutoffUtc }); b.HasOne(x => x.Revision).WithMany(x => x.Rows).HasForeignKey(x => x.DemandRunRevisionId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Batch).WithMany(x => x.Rows).HasForeignKey(x => x.DemandBatchId).OnDelete(DeleteBehavior.Restrict);
        }
    }
    public sealed class DemandBatchConfiguration : IEntityTypeConfiguration<DemandBatch>
    {
        public void Configure(EntityTypeBuilder<DemandBatch> b)
        {
            b.ToTable("demand_batches"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Frequency).IsRequired().HasMaxLength(40); b.Property(x => x.SpoolIdempotencyKey).IsRequired().HasMaxLength(300); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
            foreach (var n in new[] { nameof(DemandBatch.CurrentDueTotal), nameof(DemandBatch.ArrearsTotal), nameof(DemandBatch.OdiTotal), nameof(DemandBatch.OtherChargesTotal), nameof(DemandBatch.TotalCharges), nameof(DemandBatch.TotalDemand) }) b.Property<decimal>(n).HasPrecision(19, 2);
            b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.BranchId }).IsUnique(); b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.BranchId, x.Status }); b.HasMany(x => x.SpoolAttempts).WithOne(x => x.Batch).HasForeignKey(x => x.DemandBatchId).OnDelete(DeleteBehavior.Restrict);
        }
    }
    public sealed class DemandFailureConfiguration : IEntityTypeConfiguration<DemandFailure> { public void Configure(EntityTypeBuilder<DemandFailure> b) { b.ToTable("demand_failures"); b.HasKey(x => x.Id); b.Property(x => x.FailureClass).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Code).IsRequired().HasMaxLength(80); b.Property(x => x.Message).IsRequired().HasMaxLength(1000); b.Property(x => x.Scope).IsRequired().HasMaxLength(512); b.HasIndex(x => new { x.DemandRunId, x.CreatedAtUtc }); } }
    public sealed class DemandSpoolAttemptConfiguration : IEntityTypeConfiguration<DemandSpoolAttempt> { public void Configure(EntityTypeBuilder<DemandSpoolAttempt> b) { b.ToTable("demand_spool_attempts"); b.HasKey(x => x.Id); b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired(); b.Property(x => x.ActorId).IsRequired().HasMaxLength(200); b.Property(x => x.TransportStatus).IsRequired().HasMaxLength(80); b.HasIndex(x => new { x.DemandBatchId, x.AttemptNumber }).IsUnique(); } }
    public sealed class DemandAuditEntryConfiguration : IEntityTypeConfiguration<DemandAuditEntry> { public void Configure(EntityTypeBuilder<DemandAuditEntry> b) { b.ToTable("demand_audit_entries"); b.HasKey(x => x.Id); b.Property(x => x.EventType).IsRequired().HasMaxLength(80); b.Property(x => x.ActorId).IsRequired().HasMaxLength(200); b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired(); b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100); b.HasIndex(x => new { x.DemandRunId, x.CreatedAtUtc }); } }
    ```
    - If the confirmed database provider does not support a filtered unique lease index, enforce the active lease uniqueness in the repository transaction with a unique lease table or equivalent constraint. Do not use an in-memory lock.
    - _Requirements: 4.6, 5.5-5.8, 7.1, 8.1-8.4, 9.4, 14.1-14.8, 16.1_

  - [ ] 5.2 MODIFY `e:\Finnova\Finnova-API\Finnova.Repository\Context\FinnovaDbContext.cs`
    - Insert these DbSets immediately after `public DbSet<ApplicationStatusAuditEntry> ApplicationStatusAuditEntries => Set<ApplicationStatusAuditEntry>();`:
    ```csharp
    public DbSet<DemandRun> DemandRuns => Set<DemandRun>();
    public DbSet<DemandRunRevision> DemandRunRevisions => Set<DemandRunRevision>();
    public DbSet<DemandRunBranchScope> DemandRunBranchScopes => Set<DemandRunBranchScope>();
    public DbSet<DemandRow> DemandRows => Set<DemandRow>();
    public DbSet<DemandBatch> DemandBatches => Set<DemandBatch>();
    public DbSet<DemandFailure> DemandFailures => Set<DemandFailure>();
    public DbSet<DemandSpoolAttempt> DemandSpoolAttempts => Set<DemandSpoolAttempt>();
    public DbSet<DemandAuditEntry> DemandAuditEntries => Set<DemandAuditEntry>();
    ```
    - Add `using Finnova.Models.Domain.Entities;` if it is not already present. `ApplyConfigurationsFromAssembly` remains unchanged.
    - _Requirements: 5.1-5.8, 7.1, 8.1-8.4, 14.1-14.8_

  - [ ] 5.3 Generate and review the migration after the source contract gate
    - Run from `e:\Finnova\Finnova-API` only after confirming the SQL schema and ownership:
    ```powershell
    dotnet ef migrations add AddDemandProcessing --project Finnova.Repository --startup-project Finnova.AccountsService --context FinnovaDbContext --output-dir Migrations
    ```
    - Review the generated migration for `decimal(19,2)`, UTC datetime columns, rowversion columns, restricted deletes, and all unique/index constraints. Do not apply it in this task list and do not backfill historical output.
    - _Requirements: 5.5-5.8, 7.1-7.8, 8.1-8.4, 14.7-14.8, 16.1_

- [ ] 6. Add repositories, source adapters, unit-of-work/lease boundaries, and DI registration
  - [ ] 6.1 CREATE `e:\Finnova\Finnova-API\Finnova.Repository\Interfaces\IDemandRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Repository.Interfaces;

    public interface IDemandRepository
    {
        Task<DemandRun?> FindByScopeAsync(string logicalScopeKey, CancellationToken ct);
        Task<DemandRun?> GetRunAsync(Guid id, CancellationToken ct);
        Task<DemandBatch?> GetBatchAsync(Guid id, CancellationToken ct);
        Task<(List<DemandRun> Items, int Total)> SearchRunsAsync(int? year, int? month, Guid? lob, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct);
        Task<(List<DemandRow> Items, int Total)> SearchRowsAsync(Guid? runId, int? revision, int? year, int? month, Guid? lob, Guid? branch, Guid? account, Guid? customer, DemandRowStatus? status, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct);
        Task<(List<DemandBatch> Items, int Total)> SearchBatchesAsync(Guid? runId, int? revision, int? year, int? month, Guid? lob, Guid? branch, DemandBatchStatus? status, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct);
        Task<List<DemandSpoolAttempt>> GetSpoolAttemptsAsync(Guid batchId, IReadOnlySet<Guid> branches, CancellationToken ct);
        Task AddRunAsync(DemandRun run, CancellationToken ct);
        Task AddRevisionAsync(DemandRunRevision revision, CancellationToken ct);
        Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct);
        Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct);
        Task AddFailureAsync(DemandFailure failure, CancellationToken ct);
        Task AddSpoolAttemptAsync(DemandSpoolAttempt attempt, CancellationToken ct);
        Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct);
        Task SaveAsync(CancellationToken ct);
        Task<IDisposable> BeginTransactionAsync(CancellationToken ct);
    }
    ```
    - Every query parameter must be applied in SQL before paging; the branch set is mandatory for all read queries.
    - _Requirements: 7.2, 8.1-8.4, 9.5-9.7, 11.4-11.5, 12.1-12.8, 13.4-13.5_

  - [ ] 6.2 CREATE `e:\Finnova\Finnova-API\Finnova.Repository\Repositories\DemandRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public sealed class DemandRepository(FinnovaDbContext db) : IDemandRepository
    {
        private IQueryable<DemandRun> Runs => db.DemandRuns.Include(x => x.BranchScopes).Include(x => x.Revisions);
        public Task<DemandRun?> FindByScopeAsync(string key, CancellationToken ct) => Runs.AsNoTracking().FirstOrDefaultAsync(x => x.LogicalScopeKey == key, ct);
        public Task<DemandRun?> GetRunAsync(Guid id, CancellationToken ct) => Runs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        public Task<DemandBatch?> GetBatchAsync(Guid id, CancellationToken ct) => db.DemandBatches.AsNoTracking().Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id, ct);
        public async Task<(List<DemandRun> Items, int Total)> SearchRunsAsync(int? year, int? month, Guid? lob, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct) { var q = Runs.AsNoTracking().Where(x => x.BranchScopes.Any(b => branches.Contains(b.BranchId))); if (year.HasValue) q = q.Where(x => x.PeriodYear == year); if (month.HasValue) q = q.Where(x => x.PeriodMonth == month); if (lob.HasValue) q = q.Where(x => x.LineOfBusinessId == lob); var total = await q.CountAsync(ct); var items = await q.OrderBy(x => x.PeriodYear).ThenBy(x => x.PeriodMonth).ThenBy(x => x.CompletedAtUtc).ThenBy(x => x.CurrentRevisionNumber).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return (items, total); }
        public async Task<(List<DemandRow> Items, int Total)> SearchRowsAsync(Guid? runId, int? revision, int? year, int? month, Guid? lob, Guid? branch, Guid? account, Guid? customer, DemandRowStatus? status, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct) { var q = db.DemandRows.AsNoTracking().Where(x => branches.Contains(x.BranchId) && x.Status == DemandRowStatus.Completed); if (runId.HasValue) q = q.Where(x => x.DemandRunId == runId); if (revision.HasValue) q = q.Where(x => x.RevisionNumber == revision); if (lob.HasValue) q = q.Where(x => x.LineOfBusinessId == lob); if (branch.HasValue) q = q.Where(x => x.BranchId == branch); if (account.HasValue) q = q.Where(x => x.AccountId == account); if (customer.HasValue) q = q.Where(x => x.CustomerId == customer); if (status.HasValue) q = q.Where(x => x.Status == status); var total = await q.CountAsync(ct); var items = await q.OrderBy(x => x.OrderingKey).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return (items, total); }
        public async Task<(List<DemandBatch> Items, int Total)> SearchBatchesAsync(Guid? runId, int? revision, int? year, int? month, Guid? lob, Guid? branch, DemandBatchStatus? status, IReadOnlySet<Guid> branches, int page, int pageSize, CancellationToken ct) { var q = db.DemandBatches.AsNoTracking().Where(x => branches.Contains(x.BranchId)); if (runId.HasValue) q = q.Where(x => x.DemandRunId == runId); if (revision.HasValue) q = q.Where(x => x.RevisionNumber == revision); if (year.HasValue) q = q.Where(x => x.PeriodYear == year); if (month.HasValue) q = q.Where(x => x.PeriodMonth == month); if (lob.HasValue) q = q.Where(x => x.LineOfBusinessId == lob); if (branch.HasValue) q = q.Where(x => x.BranchId == branch); if (status.HasValue) q = q.Where(x => x.Status == status); var total = await q.CountAsync(ct); var items = await q.OrderBy(x => x.BranchId).ThenBy(x => x.DemandRunId).ThenBy(x => x.RevisionNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return (items, total); }
        public Task<List<DemandSpoolAttempt>> GetSpoolAttemptsAsync(Guid batchId, IReadOnlySet<Guid> branches, CancellationToken ct) => db.DemandSpoolAttempts.AsNoTracking().Include(x => x.Batch).Where(x => x.DemandBatchId == batchId && branches.Contains(x.Batch.BranchId)).OrderBy(x => x.AttemptNumber).ToListAsync(ct);
        public Task AddRunAsync(DemandRun run, CancellationToken ct) => db.DemandRuns.AddAsync(run, ct).AsTask();
        public Task AddRevisionAsync(DemandRunRevision revision, CancellationToken ct) => db.DemandRunRevisions.AddAsync(revision, ct).AsTask();
        public async Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct) => await db.DemandRows.AddRangeAsync(rows, ct);
        public async Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct) => await db.DemandBatches.AddRangeAsync(batches, ct);
        public Task AddFailureAsync(DemandFailure failure, CancellationToken ct) => db.DemandFailures.AddAsync(failure, ct).AsTask();
        public Task AddSpoolAttemptAsync(DemandSpoolAttempt attempt, CancellationToken ct) => db.DemandSpoolAttempts.AddAsync(attempt, ct).AsTask();
        public Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct) => db.DemandAuditEntries.AddAsync(entry, ct).AsTask();
        public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => await db.Database.BeginTransactionAsync(ct);
    }
    ```
    - Replace the compact query methods with project formatting if required, but preserve SQL-side scope filtering, deterministic ordering, and no live unscoped Customer query.
    - _Requirements: 7.2, 8.1-8.4, 11.1-11.8, 12.1-12.8, 13.4-13.5_

  - [ ] 6.3 MODIFY `e:\Finnova\Finnova-API\Finnova.Repository\DependencyInjection.cs`
    - Insert before `return services;`:
    ```csharp
    services.AddScoped<IDemandRepository, DemandRepository>();
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IBillingCompletionPort, Finnova.Service.DemandProcessing.Adapters.BillingCompletionAdapter>();
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IBillingSnapshotReader, Finnova.Service.DemandProcessing.Adapters.BillingSnapshotAdapter>();
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IAuthorizationScopeResolver, Finnova.Service.DemandProcessing.Adapters.AuthorizationScopeResolver>();
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IDemandLeaseStore, Finnova.Service.DemandProcessing.Adapters.DemandLeaseStore>();
    services.AddScoped<Finnova.Service.DemandProcessing.Ports.IDemandSpoolTransport, Finnova.Service.DemandProcessing.Adapters.DemandSpoolTransport>();
    services.AddSingleton<Finnova.Service.DemandProcessing.Ports.IClock, Finnova.Service.DemandProcessing.Adapters.SystemClock>();
    ```
    - CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Adapters\SystemClock.cs`:
    ```csharp
    using Finnova.Service.DemandProcessing.Ports;
    namespace Finnova.Service.DemandProcessing.Adapters;
    public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    ```
    - CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Adapters\ContractAdapters.cs` with the approved Billing/account/scope/spool implementations. The adapter must not return sample data in production:
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Ports;
    using System.Security.Claims;

    namespace Finnova.Service.DemandProcessing.Adapters;
    public sealed class BillingCompletionAdapter : IBillingCompletionPort
    {
        public Task<BillingCompletion?> FindAsync(BillingScope scope, CancellationToken ct) => throw new NotImplementedException("Bind this port to the approved Billing completion contract before enabling Demand Processing.");
        public Task<BillingCompletion> EnsureSuccessfulAsync(BillingCompletion completion, CancellationToken ct) => completion.State == Finnova.Models.Domain.Enums.BillingCompletionState.Succeeded && completion.RequiredChecks.Values.All(x => x) ? Task.FromResult(completion) : throw new Finnova.Models.Domain.Exceptions.DemandBillingIncompleteException();
    }
    public sealed class BillingSnapshotAdapter : IBillingSnapshotReader { public IAsyncEnumerable<SourceAccount> ReadAccountsAsync(BillingCompletion completion, BillingScope scope, CancellationToken ct) => throw new NotImplementedException("Bind this port to the approved immutable Billing snapshot contract before enabling Demand Processing."); }
    public sealed class AuthorizationScopeResolver : IAuthorizationScopeResolver { public Task<AuthorizedDemandScope> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct) => Task.FromResult(new AuthorizedDemandScope(new HashSet<Guid>(), new HashSet<Guid>())); }
    public sealed class DemandLeaseStore : IDemandLeaseStore { public Task<bool> TryAcquireAsync(Guid runId, string logicalScopeKey, CancellationToken ct) => Task.FromResult(true); public Task<bool> RenewAsync(Guid runId, CancellationToken ct) => Task.FromResult(true); public Task ReleaseAsync(Guid runId, CancellationToken ct) => Task.CompletedTask; }
    public sealed class DemandSpoolTransport : IDemandSpoolTransport { public Task<SpoolAcknowledgement> SendAsync(DemandBatch batch, string idempotencyKey, CancellationToken ct) => throw new NotImplementedException("Bind this port to the approved downstream spool acknowledgement contract before enabling spooling."); }
    ```
    - The three `NotImplementedException` adapters are deliberate confirmation gates. Replace them with real adapters before registering production routes or Automizer. Test projects must inject mocks instead.
    - _Requirements: 2.1-2.7, 6.1-6.7, 8.1-8.6, 9.1-9.7, 13.4-13.6_

- [ ] 7. Implement pure period, calculation, eligibility, batching, reconciliation, and idempotency helpers
  - [ ] 7.1 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Rules\DemandRules.cs`
    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.DemandProcessing.Rules;

    public sealed record CalculatedDemand(decimal CurrentDue, decimal Arrears, decimal Odi, decimal OtherCharges, decimal TotalCharges, decimal TotalDemand, bool IsZeroDemand);
    public sealed record EligibilityResult(bool Eligible, string? Code, string? Message);
    public static class DemandRules
    {
        public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        public static CalculatedDemand Calculate(SourceAccount source)
        {
            if (source.CurrentDue is null || source.Arrears is null || source.Odi is null || source.OtherCharges is null) throw new InvalidOperationException("A demand amount is missing.");
            if (new[] { source.CurrentDue.Value, source.Arrears.Value, source.Odi.Value, source.OtherCharges.Value }.Any(x => x < 0m)) throw new InvalidOperationException("Demand amounts cannot be negative.");
            var current = Money(source.CurrentDue.Value); var arrears = Money(source.Arrears.Value); var odi = Money(source.Odi.Value); var other = Money(source.OtherCharges.Value); var charges = Money(odi + other); var total = Money(current + arrears + charges);
            return new CalculatedDemand(current, arrears, odi, other, charges, total, total == 0m);
        }
        public static EligibilityResult Check(SourceAccount x, DemandScope scope) => x.Status == "Active" && scope.LineOfBusinessId == x.LineOfBusinessId && scope.BranchIds.Contains(x.BranchId) && x.CustomerId != Guid.Empty && x.AccountId != Guid.Empty ? new(true, null, null) : new(false, "ERR-DEMAND-DATA-QUALITY", "The account is outside scope, inactive, or has an unresolved reference.");
        public static string Fingerprint(BillingScope scope, string billingReference, string action, string? idempotencyKey) { var text = $"{scope.LineOfBusinessId:N}|{string.Join(',', scope.BranchIds.OrderBy(x => x).Select(x => x.ToString("N")))}|{scope.Frequency.Trim()}|{scope.PeriodYear:D4}-{scope.PeriodMonth:D2}|{billingReference}|{action}|{idempotencyKey}"; return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))); }
        public static DemandBatch BuildBatch(DemandRunRevision revision, Guid branchId, IEnumerable<DemandRow> rows)
        {
            var list = rows.Where(x => x.BranchId == branchId).OrderBy(x => x.OrderingKey).ThenBy(x => x.Id).ToList();
            var b = new DemandBatch { DemandRunRevisionId = revision.Id, DemandRunId = revision.DemandRunId, RevisionNumber = revision.RevisionNumber, BranchId = branchId, LineOfBusinessId = revision.DemandRun.LineOfBusinessId, Frequency = revision.DemandRun.Frequency, PeriodYear = revision.DemandRun.PeriodYear, PeriodMonth = revision.DemandRun.PeriodMonth, Status = list.Count == 0 ? DemandBatchStatus.NoDemand : DemandBatchStatus.ReadyToSpool, RowCount = list.Count, SpoolIdempotencyKey = $"{revision.DemandRunId:N}:{revision.RevisionNumber}:{branchId:N}" };
            b.CurrentDueTotal = Money(list.Sum(x => x.CurrentDue)); b.ArrearsTotal = Money(list.Sum(x => x.Arrears)); b.OdiTotal = Money(list.Sum(x => x.Odi)); b.OtherChargesTotal = Money(list.Sum(x => x.OtherCharges)); b.TotalCharges = Money(list.Sum(x => x.TotalCharges)); b.TotalDemand = Money(list.Sum(x => x.TotalDemand)); return b;
        }
        public static void Reconcile(DemandBatch batch, IEnumerable<DemandRow> rows) { var list = rows.Where(x => x.DemandBatchId == batch.Id).ToList(); if (batch.RowCount != list.Count || batch.TotalDemand != Money(list.Sum(x => x.TotalDemand)) || batch.TotalCharges != Money(list.Sum(x => x.TotalCharges))) throw new InvalidOperationException("Demand batch totals do not reconcile."); }
    }
    ```
    - Add focused helper methods rather than moving financial calculation into controllers or EF queries.
    - _Requirements: 3.1-3.7, 4.1-4.8, 5.1-5.8, 7.3-7.4, 8.3-8.6, 9.3-9.4, 15.1-15.6, 16.1_

  - [ ] 7.2 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Processing\DemandRunWorker.cs`
    ```csharp
    using System.Threading.Channels;
    using Finnova.Service.DemandProcessing.Ports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    namespace Finnova.Service.DemandProcessing.Processing;
    public sealed class DemandWorkQueue : IDemandWorkQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
        public ValueTask EnqueueAsync(Guid runId, CancellationToken ct) => _channel.Writer.WriteAsync(runId, ct);
        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }
    public sealed class DemandRunWorker(DemandWorkQueue queue, IServiceScopeFactory scopes, ILogger<DemandRunWorker> log) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var runId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IDemandProcessor>().ProcessAsync(runId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { log.LogError(ex, "Demand run {RunId} failed during background processing", runId); }
            }
        }
    }
    ```
    - This is an AccountsService-hosted lifecycle, not a second external worker. Register the queue as singleton and worker as hosted service. The accepted Run is durable before it is enqueued; recovery after restart must query durable `Accepted`, `Deferred`, and `Calculating` runs and enqueue them during startup or an approved recovery job.
    - _Requirements: 6.2-6.7, 8.1-8.6, 9.2, 9.7_

- [ ] 8. Implement CQRS start/rerun/calculation/read/spooling slices
  - [ ] 8.1 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\DemandHandlers.cs`
    ```csharp
    using FluentValidation;
    using MediatR;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Rules;

    namespace Finnova.Service.DemandProcessing;
    public sealed record StartDemandRunCommand(StartDemandRunRequest Request, string ActorId, DemandSource Source, string CorrelationId, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<DemandRunAcceptedResponse>;
    public sealed record RerunDemandRunCommand(Guid RunId, StartDemandRerunRequest Request, string ActorId, string CorrelationId, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<DemandRunAcceptedResponse>;
    public sealed record CalculateDemandRunCommand(Guid RunId) : IRequest;
    public sealed record SpoolDemandBatchCommand(Guid BatchId, SpoolBatchRequest Request, string ActorId, DemandSource Source, string CorrelationId, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<SpoolAcceptedResponse>;
    public sealed record GetDemandRunQuery(Guid RunId, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<DemandRunResponse>;
    public sealed record GetDemandHistoryQuery(int? Year, int? Month, Guid? Lob, int Page, int PageSize, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRunResponse>>;
    public sealed record GetDemandRowsQuery(DemandRowsQuery Query, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRowResponse>>;
    public sealed record GetDemandBatchesQuery(DemandBatchesQuery Query, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandBatchResponse>>;
    public sealed record GetSpoolAttemptsQuery(Guid BatchId, System.Security.Claims.ClaimsPrincipal Principal) : IRequest<IReadOnlyList<SpoolAttemptResponse>>;

    public sealed class StartDemandRunValidator : AbstractValidator<StartDemandRunCommand>
    {
        public StartDemandRunValidator() { RuleFor(x => x.Request.LineOfBusinessId).NotEmpty(); RuleFor(x => x.Request.Frequency).NotEmpty().MaximumLength(40); RuleFor(x => x.Request.PeriodYear).InclusiveBetween(2000, 2100); RuleFor(x => x.Request.PeriodMonth).InclusiveBetween(1, 12); RuleFor(x => x.Request.IdempotencyKey).MaximumLength(200); }
    }
    public sealed class StartDemandRunHandler(IDemandRepository repo, IBillingCompletionPort billing, IAuthorizationScopeResolver scopes, IDemandWorkQueue queue, IClock clock) : IRequestHandler<StartDemandRunCommand, DemandRunAcceptedResponse>
    {
        public async Task<DemandRunAcceptedResponse> Handle(StartDemandRunCommand c, CancellationToken ct)
        {
            var allowed = await scopes.ResolveAsync(c.Principal, ct); if (!allowed.LineOfBusinessIds.Contains(c.Request.LineOfBusinessId)) throw new UnauthorizedAccessException();
            var branches = c.Request.BranchId.HasValue ? new[] { c.Request.BranchId.Value } : allowed.BranchIds.ToArray(); if (branches.Length == 0 || branches.Any(x => !allowed.BranchIds.Contains(x))) throw new UnauthorizedAccessException();
            var scope = new BillingScope(c.Request.LineOfBusinessId, branches, c.Request.Frequency.Trim(), c.Request.PeriodYear, c.Request.PeriodMonth); var completion = await billing.FindAsync(scope, ct); if (completion is null) throw new DemandBillingIncompleteException(); await billing.EnsureSuccessfulAsync(completion, ct);
            var key = $"{scope.LineOfBusinessId:N}:{string.Join(',', scope.BranchIds.OrderBy(x => x).Select(x => x.ToString("N")))}:{scope.Frequency}:{scope.PeriodYear:D4}-{scope.PeriodMonth:D2}:{completion.Reference}"; var existing = await repo.FindByScopeAsync(key, ct); if (existing is not null) return new(existing.Id, existing.CurrentRevisionNumber, existing.Status, existing.CorrelationId);
            var run = new DemandRun { LogicalScopeKey = key, LineOfBusinessId = scope.LineOfBusinessId, Frequency = scope.Frequency, PeriodYear = scope.PeriodYear, PeriodMonth = scope.PeriodMonth, BillingReference = completion.Reference, BillingSnapshotVersion = completion.SnapshotVersion, ProcessingCutoffUtc = completion.ProcessingCutoffUtc, Source = c.Source, ActorId = c.ActorId, CorrelationId = c.CorrelationId, IdempotencyKey = c.Request.IdempotencyKey, IdempotencyFingerprint = DemandRules.Fingerprint(scope, completion.Reference, "start", c.Request.IdempotencyKey), Status = DemandRunStatus.Accepted };
            run.BranchScopes = branches.OrderBy(x => x).Select((x, i) => new DemandRunBranchScope { DemandRunId = run.Id, BranchId = x, SortOrder = i + 1, RevisionNumber = 1 }).ToList(); var rev = new DemandRunRevision { DemandRunId = run.Id, RevisionNumber = 1, Status = DemandRunStatus.Accepted, BillingReference = completion.Reference, BillingSnapshotVersion = completion.SnapshotVersion, ProcessingCutoffUtc = completion.ProcessingCutoffUtc, DemandRun = run }; run.Revisions.Add(rev); await repo.AddRunAsync(run, ct); await repo.AddRevisionAsync(rev, ct); await repo.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, EventType = "RunAccepted", NewState = run.Status.ToString(), ActorId = c.ActorId, Source = c.Source, CorrelationId = c.CorrelationId, BillingReference = completion.Reference, BillingSnapshotVersion = completion.SnapshotVersion, Reason = "Demand run accepted." }, ct); await repo.SaveAsync(ct); await queue.EnqueueAsync(run.Id, ct); return new(run.Id, 1, run.Status, run.CorrelationId);
        }
    }
    ```
    - Add the remaining handlers in the same file or split into the design folders. They must preserve the rules: reruns append a revision, calculation reads one immutable snapshot, successful rows/batches publish in one transaction, queries apply scope before paging, and spooling never recalculates.
    - _Requirements: 1.1-1.10, 2.1-2.7, 6.1-6.7, 7.3-7.7, 8.1-8.4, 13.1-13.7, 14.1-14.6_

  - [ ] 8.2 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Calculation\DemandProcessor.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.DemandProcessing.Ports;
    using Finnova.Service.DemandProcessing.Rules;

    namespace Finnova.Service.DemandProcessing.Calculation;
    public sealed class DemandProcessor(IDemandRepository repo, IBillingCompletionPort billing, IBillingSnapshotReader snapshot, IDemandLeaseStore lease, IClock clock) : IDemandProcessor
    {
        public async Task ProcessAsync(Guid runId, CancellationToken ct)
        {
            var run = await repo.GetRunAsync(runId, ct) ?? throw new InvalidOperationException("Demand run not found."); var rev = run.Revisions.Single(x => x.RevisionNumber == run.CurrentRevisionNumber); if (!await lease.TryAcquireAsync(runId, run.LogicalScopeKey, ct)) throw new InvalidOperationException("Demand run lease is unavailable.");
            try
            {
                run.Status = DemandRunStatus.Calculating; run.StartedAtUtc = clock.UtcNow; await repo.SaveAsync(ct); var completion = await billing.FindAsync(new(run.LineOfBusinessId, run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId).ToArray(), run.Frequency, run.PeriodYear, run.PeriodMonth), ct) ?? throw new InvalidOperationException("Billing completion is missing.");
                var rows = new List<DemandRow>(); await foreach (var source in snapshot.ReadAccountsAsync(completion, new(run.LineOfBusinessId, run.BranchScopes.Select(x => x.BranchId).ToArray(), run.Frequency, run.PeriodYear, run.PeriodMonth), ct)) { var eligibility = DemandRules.Check(source, new(run.LineOfBusinessId, run.BranchScopes.Select(x => x.BranchId).ToArray())); if (!eligibility.Eligible) { await repo.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, AccountId = source.AccountId, FailureClass = DemandFailureClass.DataQuality, Code = eligibility.Code!, Message = eligibility.Message!, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId }, ct); continue; } var amount = DemandRules.Calculate(source); rows.Add(new DemandRow { DemandRunId = run.Id, DemandRunRevisionId = rev.Id, RevisionNumber = rev.RevisionNumber, AccountId = source.AccountId, CustomerId = source.CustomerId, LineOfBusinessId = source.LineOfBusinessId, BranchId = source.BranchId, AccountDisplay = source.AccountDisplay, CustomerDisplay = source.CustomerDisplay, CurrentDue = amount.CurrentDue, Arrears = amount.Arrears, Odi = amount.Odi, OtherCharges = amount.OtherCharges, TotalCharges = amount.TotalCharges, TotalDemand = amount.TotalDemand, IsZeroDemand = amount.IsZeroDemand, SourceSnapshotVersion = source.SnapshotVersion, ProcessingCutoffUtc = completion.ProcessingCutoffUtc, OrderingKey = source.AccountDisplay }); }
                var batches = run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => DemandRules.BuildBatch(rev, x.BranchId, rows)).ToList(); foreach (var batch in batches) { batch.Rows.AddRange(rows.Where(r => r.BranchId == batch.BranchId)); foreach (var row in batch.Rows) row.DemandBatchId = batch.Id; }
                using var tx = await repo.BeginTransactionAsync(ct); await repo.AddRowsAsync(rows, ct); await repo.AddBatchesAsync(batches, ct); rev.Rows = rows; rev.Batches = batches; rev.RowCount = rows.Count; rev.BatchCount = batches.Count; rev.CurrentDueTotal = DemandRules.Money(rows.Sum(x => x.CurrentDue)); rev.ArrearsTotal = DemandRules.Money(rows.Sum(x => x.Arrears)); rev.OdiTotal = DemandRules.Money(rows.Sum(x => x.Odi)); rev.OtherChargesTotal = DemandRules.Money(rows.Sum(x => x.OtherCharges)); rev.TotalCharges = DemandRules.Money(rows.Sum(x => x.TotalCharges)); rev.TotalDemand = DemandRules.Money(rows.Sum(x => x.TotalDemand)); rev.Status = DemandRunStatus.Completed; rev.CompletedAtUtc = clock.UtcNow; run.Status = DemandRunStatus.Completed; run.RowCount = rev.RowCount; run.BatchCount = rev.BatchCount; run.TotalDemand = rev.TotalDemand; run.TotalCharges = rev.TotalCharges; run.CompletedAtUtc = rev.CompletedAtUtc; await repo.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = rev.RevisionNumber, EventType = "RunCompleted", PreviousState = DemandRunStatus.Calculating.ToString(), NewState = run.Status.ToString(), ActorId = run.ActorId, Source = run.Source, CorrelationId = run.CorrelationId, BillingReference = run.BillingReference, BillingSnapshotVersion = run.BillingSnapshotVersion, Reason = "Demand rows and batches reconciled." }, ct); await repo.SaveAsync(ct); if (tx is Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx) await efTx.CommitAsync(ct);
            }
            finally { await lease.ReleaseAsync(runId, ct); }
        }
    }
    ```
    - Before enabling this handler, add the confirmed failure/partial-branch state transitions and reconciliation checks from the design. A row with a bad amount must create a failure and never enter a completed batch; a lease loss must prevent `Completed` publication.
    - _Requirements: 2.6, 3.1-3.7, 4.1-4.8, 5.1-5.8, 8.3-8.6, 9.1-9.4, 15.1-15.5_

  - [ ] 8.3 CREATE `e:\Finnova\Finnova-API\Finnova.Service\DemandProcessing\Mappers\DemandMappers.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.DemandProcessing.Mappers;
    public static class DemandMappers
    {
        public static DemandRowResponse ToResponse(this DemandRow x) => new(x.Id, x.DemandRunId, x.RevisionNumber, x.AccountId, x.CustomerId, x.LineOfBusinessId, x.BranchId, x.AccountDisplay, x.CustomerDisplay, x.Revision.DemandRun.PeriodYear, x.Revision.DemandRun.PeriodMonth, x.Revision.DemandRun.Frequency, x.CurrentDue, x.Arrears, x.Odi, x.OtherCharges, x.TotalCharges, x.TotalDemand, x.IsZeroDemand, x.Status, x.SourceSnapshotVersion, x.OrderingKey);
        public static DemandBatchResponse ToResponse(this DemandBatch x) => new(x.Id, x.DemandRunId, x.RevisionNumber, x.BranchId, x.LineOfBusinessId, x.Frequency, x.PeriodYear, x.PeriodMonth, x.Status, x.RowCount, x.CurrentDueTotal, x.ArrearsTotal, x.OdiTotal, x.OtherChargesTotal, x.TotalCharges, x.TotalDemand, x.LastSpooledAtUtc, x.LastDownstreamReference, x.FailureCode);
        public static SpoolAttemptResponse ToResponse(this DemandSpoolAttempt x) => new(x.Id, x.DemandBatchId, x.AttemptNumber, x.TransportStatus, x.Acknowledged, x.DownstreamReference, x.FailureCode, x.CreatedAtUtc);
    }
    ```
    - Add the Run mapper with its failure projection and ensure query handlers use `AsNoTracking` projections/includes needed by the mapper.
    - _Requirements: 7.1-7.2, 9.5-9.7, 11.2-11.8, 12.2-12.8_

  - [ ] 8.4 Register the asynchronous worker and application handlers in AccountsService
    - MODIFY `e:\Finnova\Finnova-API\Finnova.AccountsService\Program.cs` by adding these registrations after `builder.Services.AddFinnovaRepository(connectionString!);`:
    ```csharp
    builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>();
    builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Ports.IDemandWorkQueue>(sp => sp.GetRequiredService<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>());
    builder.Services.AddScoped<Finnova.Service.DemandProcessing.Ports.IDemandProcessor, Finnova.Service.DemandProcessing.Calculation.DemandProcessor>();
    builder.Services.AddHostedService<Finnova.Service.DemandProcessing.Processing.DemandRunWorker>();
    ```
    - Add an approved durable recovery dispatcher before enabling production automation. It must enqueue `Accepted`, `Deferred`, and interrupted `Calculating` records after restart without creating duplicate logical runs.
    - _Requirements: 6.2-6.7, 8.1-8.6, 9.2, 9.7_

- [ ] 9. Add controller, authorization policies, and safe middleware mapping
  - [ ] 9.1 MODIFY `e:\Finnova\Finnova-API\Finnova.AccountsService\Program.cs` in the existing `AddAuthorization` block
    - Insert the following policies after `ApplicationProcessor`:
    ```csharp
    options.AddPolicy("DemandRun", p => p.RequireRole("Operations", "Accounts", "SystemAdmin"));
    options.AddPolicy("DemandOutputRead", p => p.RequireRole("Operations", "Accounts", "BranchOperations", "SystemAdmin"));
    options.AddPolicy("DemandSpool", p => p.RequireRole("Operations", "BranchOperations", "SystemAdmin", "Automizer"));
    options.AddPolicy("DemandAuditRead", p => p.RequireRole("Operations", "SystemAdmin"));
    ```
    - Replace role names only with the confirmed least-privilege policy mapping from Task 1; do not grant broad access as a shortcut.

  - [ ] 9.2 CREATE `e:\Finnova\Finnova-API\Finnova.AccountsService\Controllers\DemandProcessingController.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Service.DemandProcessing;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.AccountsService.Controllers;
    [ApiController, Route("api/demand-processing")]
    public sealed class DemandProcessingController(IMediator mediator) : ControllerBase
    {
        private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
        private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;
        [HttpPost("runs"), Authorize(Policy = "DemandRun")] public async Task<ActionResult<DemandRunAcceptedResponse>> Start(StartDemandRunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new StartDemandRunCommand(request, Actor, User.IsInRole("Automizer") ? Finnova.Models.Domain.Enums.DemandSource.Automizer : Finnova.Models.Domain.Enums.DemandSource.User, Correlation, User), ct));
        [HttpGet("runs/{runId:guid}"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<DemandRunResponse>> GetRun(Guid runId, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRunQuery(runId, User), ct));
        [HttpGet("runs"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<object>> History([FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? lob, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await mediator.Send(new GetDemandHistoryQuery(year, month, lob, page, pageSize, User), ct));
        [HttpPost("runs/{runId:guid}/rerun"), Authorize(Policy = "DemandRun")] public async Task<ActionResult<DemandRunAcceptedResponse>> Rerun(Guid runId, StartDemandRerunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new RerunDemandRunCommand(runId, request, Actor, Correlation, User), ct));
        [HttpGet("runs/{runId:guid}/rows"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<object>> Rows(Guid runId, [FromQuery] DemandRowsQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRowsQuery(query with { RunId = runId }, User), ct));
        [HttpGet("batches"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<object>> Batches([FromQuery] DemandBatchesQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandBatchesQuery(query, User), ct));
        [HttpGet("batches/{batchId:guid}"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<DemandBatchResponse>> Batch(Guid batchId, CancellationToken ct) => Ok(await mediator.Send(new GetDemandBatchesQuery(new DemandBatchesQuery(null, null, null, null, null, null, null, 1, 1), User), ct));
        [HttpPost("batches/{batchId:guid}/spool"), Authorize(Policy = "DemandSpool")] public async Task<ActionResult<SpoolAcceptedResponse>> Spool(Guid batchId, SpoolBatchRequest request, CancellationToken ct) => Accepted(await mediator.Send(new SpoolDemandBatchCommand(batchId, request, Actor, User.IsInRole("Automizer") ? Finnova.Models.Domain.Enums.DemandSource.Automizer : Finnova.Models.Domain.Enums.DemandSource.User, Correlation, User), ct));
        [HttpGet("batches/{batchId:guid}/spool-attempts"), Authorize(Policy = "DemandOutputRead")] public async Task<ActionResult<IReadOnlyList<SpoolAttemptResponse>>> Attempts(Guid batchId, CancellationToken ct) => Ok(await mediator.Send(new GetSpoolAttemptsQuery(batchId, User), ct));
    }
    ```
    - Complete the `Batch` handler with a dedicated `GetDemandBatchQuery` before copying. It must return only the requested branch scope; never use a broad query and filter in the controller.
    - _Requirements: 1.1-1.3, 6.1-6.7, 7.2, 9.5-9.7, 10.1-10.9, 11.1-11.8, 12.1-12.8, 13.1-13.9, 14.4-14.6_

  - [ ] 9.3 MODIFY `e:\Finnova\Finnova-API\Finnova.AccountsService\Middlewares\ExceptionHandlingMiddleware.cs`
    - Add `using Finnova.Models.Domain.Exceptions;` if missing, then add these switch arms before the existing fallback arm:
    ```csharp
    DemandProcessingException d => (d.ErrorCode switch
    {
        "ERR-DEMAND-NOT-FOUND" => StatusCodes.Status404NotFound,
        "ERR-DEMAND-DATA-QUALITY" or "ERR-DEMAND-RECONCILIATION" => StatusCodes.Status422UnprocessableEntity,
        "ERR-DEMAND-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
        "ERR-DEMAND-SPOOL" or "ERR-DEMAND-BILLING-INCOMPLETE" or "ERR-DEMAND-BILLING-SCOPE" or "ERR-DEMAND-IDEMPOTENCY-CONFLICT" or "ERR-DEMAND-CONCURRENT" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    }, d.ErrorCode, d.Message),
    ```
    - Add `UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "ERR-AUTH-403", "You are not authorized for this Demand Processing scope.")` before the fallback. Preserve the existing 401 platform behavior for authentication middleware and do not disclose demand data.
    - _Requirements: 1.2-1.6, 2.2-2.7, 8.4, 9.1-9.7, 13.1-13.7, 14.4-14.5_

- [ ] 10. Add API gateway aliases for the UI-composed path
  - [ ] 10.1 MODIFY `e:\Finnova\Finnova-API\Finnova.ApiGateway\appsettings.json`
    - Insert these two route objects at the beginning of `ReverseProxy.Routes`, before the catch-all `ua-route`:
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
    - Do not change cluster addresses or Authorization forwarding. The real UI service uses `/demand-processing` and the shared axios base prefix composes `/api/ua/api/demand-processing`.
    - _Requirements: 6.1-6.2, 10.3, 10.9, 13.8_

- [ ] 11. Add backend property, unit, and integration tests
  - [ ]* 11.1 CREATE `e:\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandRulesPropertyTests.cs`
    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Rules;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;
    public sealed class DemandRulesPropertyTests
    {
        // Feature: demand-processing, Property 1: identical valid snapshot inputs preserve monetary components, totals, and ordering.
        [Property(MaxTest = 100)]
        public Property MonetaryCalculationIsDeterministic(NonNegativeInt current, NonNegativeInt arrears, NonNegativeInt odi, NonNegativeInt other)
        {
            var source = new SourceAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ACC-001", "Customer 001", "Active", current.Get / 100m, arrears.Get / 100m, odi.Get / 100m, other.Get / 100m, "snapshot-1"); var a = DemandRules.Calculate(source); var b = DemandRules.Calculate(source);
            return (a.TotalCharges == DemandRules.Money(a.Odi + a.OtherCharges) && a.TotalDemand == DemandRules.Money(a.CurrentDue + a.Arrears + a.TotalCharges) && a == b).ToProperty();
        }
        // Feature: demand-processing, Property 2: only in-scope Active accounts with resolvable references become eligible.
        [Property(MaxTest = 100)] public Property EligibilityPreservesActiveScope() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); var x = new SourceAccount(Guid.NewGuid(), Guid.NewGuid(), lob, branch, "ACC-001", "Customer 001", "Active", 1m, 2m, 0m, 0m, "snapshot-1"); return DemandRules.Check(x, new(lob, new[] { branch })).Eligible.ToProperty(); }
        // Feature: demand-processing, Property 3: branch batches form a deterministic reconciled partition.
        [Property(MaxTest = 100)] public Property BatchesPartitionRows() { var revision = new Finnova.Models.Domain.Entities.DemandRunRevision { DemandRunId = Guid.NewGuid(), RevisionNumber = 1, DemandRun = new() { LineOfBusinessId = Guid.NewGuid(), Frequency = "Monthly", PeriodYear = 2025, PeriodMonth = 1 } }; var branch = Guid.NewGuid(); var batch = DemandRules.BuildBatch(revision, branch, Array.Empty<Finnova.Models.Domain.Entities.DemandRow>()); return (batch.BranchId == branch && batch.Status == Finnova.Models.Domain.Enums.DemandBatchStatus.NoDemand).ToProperty(); }
        // Feature: demand-processing, Property 4: equivalent logical scope requests have one stable fingerprint.
        [Property(MaxTest = 100)] public Property FingerprintsAreStable() { var scope = new BillingScope(Guid.NewGuid(), new[] { Guid.NewGuid(), Guid.NewGuid() }, "Monthly", 2025, 1); return (DemandRules.Fingerprint(scope, "billing-1", "start", "key") == DemandRules.Fingerprint(scope, "billing-1", "start", "key")).ToProperty(); }
    }
    ```
    - Keep the required comment in each property file: `Feature: demand-processing, Property N: <property text>`. The test must use the actual FsCheck generators and run at least 100 cases; the compact examples above are the starting file, not permission to reduce coverage.
    - _Requirements: 15.1-15.6, 4.1-4.8, 3.1-3.7, 5.1-5.8, 7.3-7.4_

  - [ ]* 11.2 CREATE `e:\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandRulesExampleTests.cs`
    ```csharp
    using Finnova.Models.Contracts.DemandProcessing;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.DemandProcessing.Rules;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;
    public sealed class DemandRulesExampleTests
    {
        [Fact] public void ExcludedStatusesDoNotBecomeEligible() { var scope = new DemandScope(Guid.NewGuid(), new[] { Guid.NewGuid() }); foreach (var status in new[] { "Closed", "Cancelled", "Pre-Closed", "Unknown" }) Assert.False(DemandRules.Check(new(Guid.NewGuid(), Guid.NewGuid(), scope.LineOfBusinessId, scope.BranchIds[0], "A", "C", status, 1m, 1m, 0m, 0m, "s"), scope).Eligible); }
        [Fact] public void InvalidAmountsAreRejected() { var x = new SourceAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "C", "Active", -1m, 0m, 0m, 0m, "s"); Assert.Throws<InvalidOperationException>(() => DemandRules.Calculate(x)); }
        [Fact] public void ZeroDemandIsRetained() { var x = new SourceAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "C", "Active", 0m, 0m, 0m, 0m, "s"); Assert.True(DemandRules.Calculate(x).IsZeroDemand); }
        [Fact] public void ScopeSortsBranchesDeterministically() { var a = Guid.NewGuid(); var b = Guid.NewGuid(); Assert.Equal($"{a:N},{b:N}", new DemandScope(Guid.NewGuid(), new[] { b, a }).CanonicalValue.Split(':')[1]); }
    }
    ```
    - Add examples for BillingIncomplete, scope mismatch, reconciliation mismatch, partial branch failure, safe diagnostics, rerun lineage, duplicate spool, and no mutation on unauthorized access.
    - _Requirements: 2.2-2.7, 3.2-3.6, 4.4, 4.7, 5.7-5.8, 7.4-7.6, 8.4-8.6, 9.1-9.7, 12.5-12.8_

  - [ ]* 11.3 CREATE `e:\Finnova\Finnova-API\Finnova.Tests\DemandProcessing\DemandProcessingIntegrationTests.cs`
    ```csharp
    using System.Net;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.DemandProcessing;
    public sealed class DemandProcessingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        public DemandProcessingIntegrationTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();
        [Fact] public async Task MissingTokenReturns401WithoutDemandData() { var response = await _client.GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.DoesNotContain("TotalDemand", await response.Content.ReadAsStringAsync()); }
        [Fact] public async Task UnknownRouteDoesNotBypassHostAuthorization() { var response = await _client.GetAsync("/api/demand-processing/batches"); Assert.NotEqual(HttpStatusCode.OK, response.StatusCode); }
    }
    ```
    - Replace the default factory services with isolated EF/test ports in a test-specific factory. Mint valid JWTs for in-scope and out-of-scope roles and cover 401/403, exact Billing completion, immutable snapshot use, concurrent starts, atomic publication, deterministic paging, rerun lineage, spool acknowledgement, duplicate spool, and downstream recovery.
    - _Requirements: 1.1-1.3, 2.1-2.7, 6.1-6.7, 7.2-7.8, 8.1-8.6, 9.1-9.7, 13.1-13.9, 14.1-14.8, 15.7_

- [ ] 12. Create frontend models, service interface, real/mock implementations, toggle, and barrels
  - [ ] 12.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\demandProcessing.model.ts`
    ```typescript
    export type DemandRunStatus = 'Accepted'|'BillingPending'|'Calculating'|'Completed'|'PartiallyFailed'|'Failed'|'Deferred'|'Superseded'|'Conflict';
    export type DemandBatchStatus = 'Pending'|'ReadyToSpool'|'Spooled'|'Failed'|'ReSpoolPending'|'NoDemand';
    export type DemandRowStatus = 'Completed'|'Excluded'|'Invalid';
    export interface Paginated<T>{data:T[];total:number;page:number;pageSize:number;totalPages:number;}
    export interface DemandRunRequest{lineOfBusinessId:string;branchId?:string;frequency:string;periodYear:number;periodMonth:number;idempotencyKey?:string;confirmationToken?:string;}
    export interface DemandRerunRequest{reason:string;idempotencyKey:string;confirmationToken:string;}
    export interface DemandRun{runId:string;revisionNumber:number;lineOfBusinessId:string;branchIds:string[];frequency:string;periodYear:number;periodMonth:number;billingReference?:string;billingSnapshotVersion?:string;processingCutoffUtc?:string;status:DemandRunStatus;source:string;rowCount:number;batchCount:number;currentDueTotal:number;arrearsTotal:number;odiTotal:number;otherChargesTotal:number;totalCharges:number;totalDemand:number;failures:DemandFailure[];createdAtUtc:string;completedAtUtc?:string;}
    export interface DemandRow{id:string;runId:string;revisionNumber:number;accountId:string;customerId:string;lineOfBusinessId:string;branchId:string;accountDisplay:string;customerDisplay:string;periodYear:number;periodMonth:number;frequency:string;currentDue:number;arrears:number;odi:number;otherCharges:number;totalCharges:number;totalDemand:number;isZeroDemand:boolean;status:DemandRowStatus;snapshotVersion:string;orderingKey:string;}
    export interface DemandBatch{id:string;runId:string;revisionNumber:number;branchId:string;lineOfBusinessId:string;frequency:string;periodYear:number;periodMonth:number;status:DemandBatchStatus;rowCount:number;currentDueTotal:number;arrearsTotal:number;odiTotal:number;otherChargesTotal:number;totalCharges:number;totalDemand:number;lastSpooledAtUtc?:string;lastDownstreamReference?:string;failureCode?:string;}
    export interface DemandFailure{failureClass:string;code:string;message:string;accountId?:string;batchId?:string;createdAtUtc:string;}
    export interface SpoolAttempt{id:string;batchId:string;attemptNumber:number;transportStatus:string;acknowledged:boolean;downstreamReference?:string;failureCode?:string;createdAtUtc:string;}
    export interface DemandRunAcceptedResponse{runId:string;revisionNumber:number;status:DemandRunStatus;correlationId:string;}
    export interface SpoolAcceptedResponse{batchId:string;status:DemandBatchStatus;attemptId:string;downstreamReference?:string;correlationId:string;}
    export interface DemandRowsQuery{runId?:string;revisionNumber?:number;periodYear?:number;periodMonth?:number;lineOfBusinessId?:string;branchId?:string;accountId?:string;customerId?:string;rowStatus?:string;page?:number;pageSize?:number;}
    export interface DemandBatchesQuery{runId?:string;revisionNumber?:number;periodYear?:number;periodMonth?:number;lineOfBusinessId?:string;branchId?:string;status?:DemandBatchStatus;page?:number;pageSize?:number;}
    ```
    - Use one English display value per business value. Do not add language suffixes, translation fields, RTL metadata, or client-side financial calculations.
    - _Requirements: 10.2-10.9, 11.2-11.8, 12.1-12.8, 16.1-16.4_

  - [ ] 12.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\demandProcessing.interface.ts`, `src/services/real/demandProcessing.real.ts`, `src/services/mock/demandProcessing.mock.ts`, and `src/services/demandProcessing.service.ts`
    - CREATE `src/services/interfaces/demandProcessing.interface.ts`:
    ```typescript
    import type {DemandBatchesQuery,DemandBatch,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolAcceptedResponse,SpoolAttempt} from '../../models/demandProcessing.model';
    export interface IDemandProcessingService{startRun(request:DemandRunRequest):Promise<DemandRunAcceptedResponse>;getRun(runId:string):Promise<DemandRun>;getHistory(query:Record<string,unknown>):Promise<Paginated<DemandRun>>;rerun(runId:string,request:DemandRerunRequest):Promise<DemandRunAcceptedResponse>;getRows(query:DemandRowsQuery):Promise<Paginated<DemandRow>>;getBatches(query:DemandBatchesQuery):Promise<Paginated<DemandBatch>>;spoolBatch(batchId:string,request:{idempotencyKey?:string;confirmationToken?:string}):Promise<SpoolAcceptedResponse>;getSpoolAttempts(batchId:string):Promise<SpoolAttempt[]>;}
    ```
    - CREATE `src/services/real/demandProcessing.real.ts`:
    ```typescript
    import api from '../api'; import type {IDemandProcessingService} from '../interfaces/demandProcessing.interface'; import type {DemandBatchesQuery,DemandBatch,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolAcceptedResponse,SpoolAttempt} from '../../models/demandProcessing.model';
    class DemandProcessingRealService implements IDemandProcessingService{private readonly base='/demand-processing'; async startRun(x:DemandRunRequest){return (await api.post<DemandRunAcceptedResponse>(`${this.base}/runs`,x)).data;} async getRun(id:string){return (await api.get<DemandRun>(`${this.base}/runs/${id}`)).data;} async getHistory(q:Record<string,unknown>){return (await api.get<Paginated<DemandRun>>(`${this.base}/runs`,{params:q})).data;} async rerun(id:string,x:DemandRerunRequest){return (await api.post<DemandRunAcceptedResponse>(`${this.base}/runs/${id}/rerun`,x)).data;} async getRows(q:DemandRowsQuery){return (await api.get<Paginated<DemandRow>>(`${this.base}/runs/${q.runId}/rows`,{params:q})).data;} async getBatches(q:DemandBatchesQuery){return (await api.get<Paginated<DemandBatch>>(`${this.base}/batches`,{params:q})).data;} async spoolBatch(id:string,x:{idempotencyKey?:string;confirmationToken?:string}){return (await api.post<SpoolAcceptedResponse>(`${this.base}/batches/${id}/spool`,x)).data;} async getSpoolAttempts(id:string){return (await api.get<SpoolAttempt[]>(`${this.base}/batches/${id}/spool-attempts`)).data;}}
    export const demandProcessingRealService=new DemandProcessingRealService();
    ```
    - CREATE `src/services/mock/demandProcessing.mock.ts`:
    ```typescript
    import type {IDemandProcessingService} from '../interfaces/demandProcessing.interface'; import type {DemandBatchesQuery,DemandBatch,DemandRerunRequest,DemandRow,DemandRowsQuery,DemandRun,DemandRunAcceptedResponse,DemandRunRequest,Paginated,SpoolAcceptedResponse,SpoolAttempt} from '../../models/demandProcessing.model';
    const id=()=>crypto.randomUUID(); const page=<T,>(data:T[],p=1,s=20):Paginated<T>=>({data:data.slice((p-1)*s,p*s),total:data.length,page:p,pageSize:s,totalPages:Math.max(1,Math.ceil(data.length/s))});
    class DemandProcessingMockService implements IDemandProcessingService{private runs:DemandRun[]=[];private rows:DemandRow[]=[];private batches:DemandBatch[]=[];private attempts:SpoolAttempt[]=[];async startRun(x:DemandRunRequest):Promise<DemandRunAcceptedResponse>{if(!x.lineOfBusinessId||!x.frequency||!x.periodYear||!x.periodMonth)throw this.error(400,'ERR-DEMAND-VALIDATION','Line of business, frequency, and period are required.');const run:DemandRun={runId:id(),revisionNumber:1,lineOfBusinessId:x.lineOfBusinessId,branchIds:x.branchId?[x.branchId]:['branch-1'],frequency:x.frequency,periodYear:x.periodYear,periodMonth:x.periodMonth,status:'Accepted',source:'User',rowCount:0,batchCount:0,currentDueTotal:0,arrearsTotal:0,odiTotal:0,otherChargesTotal:0,totalCharges:0,totalDemand:0,failures:[],createdAtUtc:new Date().toISOString()};this.runs.push(run);return{runId:run.runId,revisionNumber:1,status:run.status,correlationId:id()};}async getRun(runId:string){const x=this.runs.find(r=>r.runId===runId);if(!x)throw this.error(404,'ERR-DEMAND-NOT-FOUND','Demand run was not found.');return x;}async getHistory(q:Record<string,unknown>){return page(this.runs,Number(q.page??1),Number(q.pageSize??20));}async rerun(runId:string,_:DemandRerunRequest){const x=await this.getRun(runId);return{runId:x.runId,revisionNumber:x.revisionNumber+1,status:'Accepted',correlationId:id()};}async getRows(q:DemandRowsQuery){return page(this.rows.filter(x=>!q.runId||x.runId===q.runId),q.page,q.pageSize);}async getBatches(q:DemandBatchesQuery){return page(this.batches.filter(x=>!q.runId||x.runId===q.runId),q.page,q.pageSize);}async spoolBatch(batchId:string,_:{idempotencyKey?:string;confirmationToken?:string}){const b=this.batches.find(x=>x.id===batchId);if(!b)throw this.error(404,'ERR-DEMAND-NOT-FOUND','Demand batch was not found.');const a:SpoolAttempt={id:id(),batchId,attemptNumber:this.attempts.filter(x=>x.batchId===batchId).length+1,transportStatus:'Acknowledged',acknowledged:true,downstreamReference:`MOCK-${batchId.slice(0,8)}`,createdAtUtc:new Date().toISOString()};this.attempts.push(a);b.status='Spooled';b.lastSpooledAtUtc=a.createdAtUtc;b.lastDownstreamReference=a.downstreamReference;return{batchId,status:b.status,attemptId:a.id,downstreamReference:a.downstreamReference,correlationId:id()};}async getSpoolAttempts(batchId:string){return this.attempts.filter(x=>x.batchId===batchId);}private error(status:number,code:string,message:string){const e=new Error(message) as Error&{response:{status:number;data:{code:string;message:string}}};e.response={status,data:{code,message}};return e;}}
    export const demandProcessingMockService=new DemandProcessingMockService();
    ```
    - CREATE `src/services/demandProcessing.service.ts`:
    ```typescript
    import type {IDemandProcessingService} from './interfaces/demandProcessing.interface'; import {demandProcessingMockService} from './mock/demandProcessing.mock'; import {demandProcessingRealService} from './real/demandProcessing.real'; export const demandProcessingService:IDemandProcessingService=import.meta.env.VITE_USE_MOCK_API==='true'?demandProcessingMockService:demandProcessingRealService;
    ```
    - _Requirements: 10.3-10.9, 11.4-11.8, 12.4-12.8, 13.8, 15.8, 16.2-16.4_

  - [ ] 12.3 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\index.ts` and `src/services/index.ts`
    - Append to `src/models/index.ts`:
    ```typescript
    export * from './demandProcessing.model';
    ```
    - Append to `src/services/index.ts`:
    ```typescript
    export { demandProcessingService } from './demandProcessing.service';
    export type { IDemandProcessingService } from './interfaces/demandProcessing.interface';
    ```
    - _Requirements: 10.3, 11.2, 12.2, 15.8_

- [ ] 13. Add frontend pages/components, protected routes, and navigation
  - [ ] 13.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandFailurePanel.tsx` and `SpoolConfirmationDialog.tsx`
    - `DemandFailurePanel.tsx`:
    ```tsx
    import {Alert,Stack,Typography} from '@mui/material'; import type {DemandFailure} from '../../models/demandProcessing.model'; export default function DemandFailurePanel({failures}:{failures:DemandFailure[]}){if(!failures.length)return null;return <Stack spacing={1}>{failures.map((f)=><Alert key={`${f.code}-${f.createdAtUtc}`} severity="error"><Typography variant="body2">{f.code}: {f.message}</Typography></Alert>)}</Stack>;}
    ```
    - `SpoolConfirmationDialog.tsx`:
    ```tsx
    import {Button,Dialog,DialogActions,DialogContent,DialogTitle,Typography} from '@mui/material'; export default function SpoolConfirmationDialog({open,onClose,onConfirm}:{open:boolean;onClose:()=>void;onConfirm:()=>void}){return <Dialog open={open} onClose={onClose}><DialogTitle>Confirm demand spooling</DialogTitle><DialogContent><Typography>Send this completed branch batch to the downstream process?</Typography></DialogContent><DialogActions><Button onClick={onClose}>Cancel</Button><Button variant="contained" onClick={onConfirm}>Spool batch</Button></DialogActions></Dialog>;}
    ```
    - _Requirements: 9.5-9.7, 12.3-12.8, 16.2-16.4_

  - [ ] 13.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandRunForm.tsx` and `DemandRunStatus.tsx`
    - `DemandRunForm.tsx`:
    ```tsx
    import {Button,FormControl,InputLabel,MenuItem,Select,Stack,TextField} from '@mui/material'; import {useState} from 'react'; import type {DemandRunRequest} from '../../models/demandProcessing.model'; export default function DemandRunForm({onSubmit,disabled}:{onSubmit:(x:DemandRunRequest)=>void;disabled?:boolean}){const [x,setX]=useState<DemandRunRequest>({lineOfBusinessId:'',frequency:'Monthly',periodYear:new Date().getFullYear(),periodMonth:new Date().getMonth()+1});const [error,setError]=useState('');const submit=()=>{if(!x.lineOfBusinessId||!x.frequency||x.periodMonth<1||x.periodMonth>12){setError('Line of business, frequency, and a valid month are required.');return;}setError('');onSubmit(x);};return <Stack spacing={2}><TextField label="Line of Business" value={x.lineOfBusinessId} onChange={e=>setX({...x,lineOfBusinessId:e.target.value})} required error={!!error}/><TextField label="Branch" value={x.branchId??''} onChange={e=>setX({...x,branchId:e.target.value||undefined})}/><FormControl><InputLabel>Frequency</InputLabel><Select label="Frequency" value={x.frequency} onChange={e=>setX({...x,frequency:e.target.value})}><MenuItem value="Monthly">Monthly</MenuItem><MenuItem value="Quarterly">Quarterly</MenuItem></Select></FormControl><TextField type="number" label="Month" value={x.periodMonth} onChange={e=>setX({...x,periodMonth:Number(e.target.value)})}/><TextField type="number" label="Year" value={x.periodYear} onChange={e=>setX({...x,periodYear:Number(e.target.value)})}/>{error&&<span role="alert">{error}</span>}<Button variant="contained" onClick={submit} disabled={disabled}>Start Demand Run</Button></Stack>;}
    ```
    - `DemandRunStatus.tsx`:
    ```tsx
    import {Alert,Typography} from '@mui/material'; import type {DemandRunStatus} from '../../models/demandProcessing.model'; export default function DemandRunStatus({runId,status}:{runId?:string;status?:DemandRunStatus}){return <Alert severity={status==='Failed'||status==='PartiallyFailed'?'error':status==='Completed'?'success':'info'}><Typography>Run {runId??'not started'}: {status??'Ready'}</Typography></Alert>;}
    ```
    - Replace the free-text LOB/Branch controls with the approved reference service and permitted-scope options before production. The server remains authoritative.
    - _Requirements: 10.2-10.8, 13.8-13.9_

  - [ ] 13.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\demandProcessing\DemandRowsGrid.tsx` and `DemandBatchesGrid.tsx`
    - `DemandRowsGrid.tsx`:
    ```tsx
    import {DataGrid,type GridColDef} from '@mui/x-data-grid'; import type {DemandRow} from '../../models/demandProcessing.model'; const money=(v:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2}).format(v); const columns:GridColDef<DemandRow>[]=[{field:'accountDisplay',headerName:'Account',width:150},{field:'customerDisplay',headerName:'Customer',width:180},{field:'branchId',headerName:'Branch',width:130},{field:'currentDue',headerName:'Current Due',valueFormatter:p=>money(Number(p.value)),width:130},{field:'arrears',headerName:'Arrears',valueFormatter:p=>money(Number(p.value)),width:130},{field:'odi',headerName:'ODI',valueFormatter:p=>money(Number(p.value)),width:120},{field:'otherCharges',headerName:'Other Charges',valueFormatter:p=>money(Number(p.value)),width:140},{field:'totalCharges',headerName:'Total Charges',valueFormatter:p=>money(Number(p.value)),width:140},{field:'totalDemand',headerName:'Total Demand',valueFormatter:p=>money(Number(p.value)),width:140},{field:'status',headerName:'Status',width:120}]; export default function DemandRowsGrid({rows,loading}:{rows:DemandRow[];loading:boolean}){return <DataGrid rows={rows} columns={columns} loading={loading} getRowId={x=>x.id} disableRowSelectionOnClick/>;}
    ```
    - `DemandBatchesGrid.tsx`:
    ```tsx
    import {Button,Chip} from '@mui/material'; import {DataGrid,type GridColDef} from '@mui/x-data-grid'; import type {DemandBatch} from '../../models/demandProcessing.model'; const money=(v:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2}).format(v); export default function DemandBatchesGrid({batches,loading,onSpool}:{batches:DemandBatch[];loading:boolean;onSpool:(x:DemandBatch)=>void}){const columns:GridColDef<DemandBatch>[]=[{field:'branchId',headerName:'Branch',width:140},{field:'periodYear',headerName:'Year',width:80},{field:'periodMonth',headerName:'Month',width:80},{field:'rowCount',headerName:'Rows',width:80},{field:'totalDemand',headerName:'Total Demand',valueFormatter:p=>money(Number(p.value)),width:150},{field:'status',headerName:'Status',renderCell:p=><Chip size="small" label={p.value}/>,width:140},{field:'action',headerName:'Action',sortable:false,renderCell:p=><Button size="small" disabled={p.row.status!=='ReadyToSpool'&&p.row.status!=='ReSpoolPending'} onClick={()=>onSpool(p.row)}>Spool</Button>,width:120}];return <DataGrid rows={batches} columns={columns} loading={loading} getRowId={x=>x.id} disableRowSelectionOnClick/>;}
    ```
    - Render every required component total and explain disabled spool reasons in the page; never calculate replacement totals in the browser.
    - _Requirements: 11.2-11.8, 12.2-12.8, 16.1-16.4_

  - [ ] 13.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandRun.tsx`, `DemandGenerationOutput.tsx`, and `DemandSpooling.tsx`
    - `DemandRun.tsx`:
    ```tsx
    import {useState} from 'react'; import {Stack,Typography} from '@mui/material'; import {demandProcessingService} from '../services'; import DemandRunForm from '../components/demandProcessing/DemandRunForm'; import DemandRunStatus from '../components/demandProcessing/DemandRunStatus'; import type {DemandRunRequest,DemandRunStatus as Status} from '../models'; export default function DemandRun(){const [runId,setRunId]=useState<string>();const [status,setStatus]=useState<Status>();const [error,setError]=useState('');const submit=async(x:DemandRunRequest)=>{setError('');try{const r=await demandProcessingService.startRun(x);setRunId(r.runId);setStatus(r.status);}catch(e){setError(e instanceof Error?e.message:'Demand run could not be started.');}};return <Stack spacing={3}><Typography variant="h4">Demand Run</Typography><DemandRunForm onSubmit={submit}/><DemandRunStatus runId={runId} status={status}/>{error&&<Typography role="alert" color="error">{error}</Typography>}</Stack>;}
    ```
    - `DemandGenerationOutput.tsx`:
    ```tsx
    import {useEffect,useState} from 'react'; import {Alert,Stack,Typography} from '@mui/material'; import {demandProcessingService} from '../services'; import DemandRowsGrid from '../components/demandProcessing/DemandRowsGrid'; import type {DemandRow} from '../models'; export default function DemandGenerationOutput(){const [rows,setRows]=useState<DemandRow[]>([]);const [loading,setLoading]=useState(true);const [error,setError]=useState('');useEffect(()=>{demandProcessingService.getRows({page:1,pageSize:20}).then(x=>setRows(x.data)).catch(e=>setError(e instanceof Error?e.message:'Output could not be loaded.')).finally(()=>setLoading(false));},[]);return <Stack spacing={2}><Typography variant="h4">Demand Generation Output</Typography>{error&&<Alert severity="error">{error}</Alert>}{!loading&&!error&&!rows.length&&<Alert severity="info">No demand rows match the selected filters.</Alert>}<DemandRowsGrid rows={rows} loading={loading}/></Stack>;}
    ```
    - `DemandSpooling.tsx`:
    ```tsx
    import {useEffect,useState} from 'react'; import {Alert,Stack,Typography} from '@mui/material'; import {demandProcessingService} from '../services'; import DemandBatchesGrid from '../components/demandProcessing/DemandBatchesGrid'; import SpoolConfirmationDialog from '../components/demandProcessing/SpoolConfirmationDialog'; import type {DemandBatch} from '../models'; export default function DemandSpooling(){const [batches,setBatches]=useState<DemandBatch[]>([]);const [selected,setSelected]=useState<DemandBatch>();const [error,setError]=useState('');useEffect(()=>{demandProcessingService.getBatches({page:1,pageSize:20}).then(x=>setBatches(x.data)).catch(e=>setError(e instanceof Error?e.message:'Batches could not be loaded.'));},[]);const spool=async()=>{if(!selected)return;try{await demandProcessingService.spoolBatch(selected.id,{idempotencyKey:`ui:${selected.id}`});setSelected(undefined);setBatches(await demandProcessingService.getBatches({page:1,pageSize:20}).then(x=>x.data));}catch(e){setError(e instanceof Error?e.message:'Spooling failed.');}};return <Stack spacing={2}><Typography variant="h4">Demand Spooling</Typography>{error&&<Alert severity="error">{error}</Alert>}<DemandBatchesGrid batches={batches} loading={false} onSpool={setSelected}/><SpoolConfirmationDialog open={!!selected} onClose={()=>setSelected(undefined)} onConfirm={spool}/></Stack>;}
    ```
    - Expand the first implementation with server-side filter/paging controls, polling for processing states, last-good-data preservation, permission-gated controls, BillingIncomplete/conflict/timeout states, and spool-attempt history before considering the screens complete.
    - _Requirements: 10.1-10.9, 11.1-11.8, 12.1-12.8, 13.8, 16.2-16.4_

  - [ ] 13.5 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx`
    - Add imports after the existing `ApplicationProcessing` import:
    ```tsx
    import DemandRun from './pages/DemandRun';
    import DemandGenerationOutput from './pages/DemandGenerationOutput';
    import DemandSpooling from './pages/DemandSpooling';
    ```
    - Add these routes inside the existing authenticated `<Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>` group, immediately after the application-processing route:
    ```tsx
    <Route path="/demand-processing/run" element={<DemandRun />} />
    <Route path="/demand-processing/output" element={<DemandGenerationOutput />} />
    <Route path="/demand-processing/spooling" element={<DemandSpooling />} />
    ```
    - Keep all three routes inside the existing auth guard. Add a permission guard in the page/component layer if the current permission context supports operation-specific checks.
    - _Requirements: 10.1, 10.8, 11.1, 12.1, 13.8_

  - [ ] 13.6 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx`
    - `RequestQuoteIcon` is already imported in the current `Layout.tsx`; do not add a duplicate import. Add this group before `Administration` in `menuGroups`:
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
    - Keep navigation visibility aligned with the existing permission/navigation convention when the exact policy mapping is confirmed. Do not add RTL or localization infrastructure.
    - _Requirements: 10.1-10.2, 10.8, 11.1, 12.1, 13.8-13.9, 16.2-16.4_

- [ ] 14. Add Vitest and React Testing Library coverage
  - [ ]* 14.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\demandProcessing.service.test.ts`
    ```typescript
    import {describe,expect,it,vi} from 'vitest'; import {demandProcessingService} from './demandProcessing.service'; describe('demandProcessingService',()=>{it('selects a service implementation and starts a run',async()=>{const result=await demandProcessingService.startRun({lineOfBusinessId:'lob-1',frequency:'Monthly',periodYear:2025,periodMonth:1});expect(result.runId).toBeTruthy();});it('does not create a second axios client',async()=>{const module=await import('./api');expect(module.default).toBeDefined();});});
    ```
    - Mock `VITE_USE_MOCK_API` for both true and false cases; mock the existing shared `api` instance for real-service tests and assert the `/demand-processing` path, JWT/error propagation, and no ad-hoc axios import.
    - _Requirements: 10.3-10.9, 13.8, 15.8_

  - [ ]* 14.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DemandProcessing.test.tsx`
    ```tsx
    import {render,screen} from '@testing-library/react'; import {MemoryRouter} from 'react-router-dom'; import {describe,expect,it,vi} from 'vitest'; import DemandRun from './DemandRun'; import DemandGenerationOutput from './DemandGenerationOutput'; import DemandSpooling from './DemandSpooling';
    vi.mock('../services',()=>({demandProcessingService:{startRun:vi.fn().mockResolvedValue({runId:'run-1',revisionNumber:1,status:'Accepted',correlationId:'c-1'}),getRows:vi.fn().mockResolvedValue({data:[],total:0,page:1,pageSize:20,totalPages:1}),getBatches:vi.fn().mockResolvedValue({data:[],total:0,page:1,pageSize:20,totalPages:1}),spoolBatch:vi.fn()}}));
    describe('Demand Processing pages',()=>{it('validates Demand Run fields',()=>{render(<MemoryRouter><DemandRun/></MemoryRouter>);expect(screen.getByText('Start Demand Run')).toBeInTheDocument();});it('shows distinct output empty state',async()=>{render(<MemoryRouter><DemandGenerationOutput/></MemoryRouter>);expect(await screen.findByText('No demand rows match the selected filters.')).toBeInTheDocument();});it('renders spooling page',()=>{render(<MemoryRouter><DemandSpooling/></MemoryRouter>);expect(screen.getByText('Demand Spooling')).toBeInTheDocument();});});
    ```
    - Add tests for unauthorized routes/permission-gated controls, scope-restricted options, successful start, polling, BillingIncomplete/conflict/timeout/failure, output filters/pagination/INR columns/last-good preservation, spool eligibility/confirmation/acknowledgement/retry/attempt history, English-only labels, and no `dir="rtl"` attributes.
    - _Requirements: 10.1-10.9, 11.1-11.8, 12.1-12.8, 13.8, 15.8, 16.2-16.4_

- [ ] 15. Checkpoint: review the complete implementation against the confirmed contracts
  - [ ] 15.1 Review the complete implementation against the confirmed contracts and verify feature wiring without changing unrelated code
  - Confirm no implementation task introduced a second HTTP client, Redux/RxJS, client-side authoritative calculation, unscoped query, mutable financial history, non-English data, bilingual fields, or RTL handling.
  - Confirm the worker is hosted by AccountsService, durable accepted/calculating states have a restart recovery path, and the queue is not treated as the source of truth.
  - Confirm migration generation/review, gateway aliases, backend authorization, frontend protected routes, and all test factories are wired before deployment.
  - _Requirements: 6.2-6.7, 8.1-8.6, 9.6-9.7, 13.1-13.9, 14.1-14.8, 15.6-15.8, 16.1-16.6_

## Notes

- Tasks marked with `*` are optional in the dependency graph. The testing tasks below are marked optional because the workspace convention allows test sub-tasks to be skipped for a faster MVP, but the requirements explicitly expect them before production.
- Task 1 is a confirmation gate, not permission to invent missing Billing, authorization, retention, or spool contracts.
- The snippets marked with a deliberate `NotImplementedException` are adapters that must be replaced with the confirmed production ports before enabling the feature. They are not acceptable production behavior.
- No migration file is copied into this plan because the design requires schema/source ownership confirmation before generation. The migration command and review checks are included in Task 5.3.
- All amounts remain persisted API values. The UI formats INR but does not recompute Total Charges or Total Demand.
- The snippets are intentionally limited to feature files and exact insertion points. Do not refactor unrelated AccountsService, gateway, repository, or UI code.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "3.1", "4.1", "4.2"] },
    { "id": 2, "tasks": ["5.1", "5.2", "6.1", "7.1"] },
    { "id": 3, "tasks": ["5.3", "6.2", "6.3", "7.2"] },
    { "id": 4, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 5, "tasks": ["9.1", "9.2", "9.3", "10.1"] },
    { "id": 6, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 7, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 8, "tasks": ["13.1", "13.2", "13.3", "13.4", "13.5", "13.6"] },
    { "id": 9, "tasks": ["14.1", "14.2"] },
    { "id": 10, "tasks": ["15.1"] }
  ]
}
```
