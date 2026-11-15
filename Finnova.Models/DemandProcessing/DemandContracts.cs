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
