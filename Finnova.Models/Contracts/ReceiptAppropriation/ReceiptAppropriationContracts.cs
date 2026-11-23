using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.ReceiptAppropriation;

public sealed record ReceiptAppropriationDueResponse(
    Guid DueId, Guid AccountId, string AccountNumber, CashFlowDueType CashFlowDueType,
    DateOnly DueDate, decimal OutstandingAmount, decimal AllocatedAmount, decimal RemainingAmount,
    string Currency, string DueVersion, Guid? RuleDetailId = null, int? RuleOrder = null,
    decimal? TargetAmount = null);

public sealed record AppropriationRuleDetailResponse(Guid Id, CashFlowDueType CashFlowDueType, decimal Percentage, int Order);
public sealed record AppropriationRuleSummary(Guid RuleId, string Version, AppropriationDirection Direction, bool FullDue, IReadOnlyList<AppropriationRuleDetailResponse> Details);
public sealed record AppropriationTotals(decimal ReceiptAmount, decimal DueAllocationTotal, decimal AdvanceAccountAmount, decimal ResidualAmount, bool Reconciles);
public sealed record AppropriationPreviewResponse(
    Guid ReceiptId, Guid CustomerId, Guid CompanyId, Guid LineOfBusinessId, decimal ReceiptDocumentAmount,
    AppropriationMode Mode, IReadOnlyList<ReceiptAppropriationDueResponse> Allocations,
    AppropriationRuleSummary? Rule, AppropriationTotals Totals, string ReceiptVersion, string RuleVersion,
    string DueFingerprint, DateTime GeneratedAtUtc, string CorrelationId);

public sealed record ShowAppropriationResponse(
    Guid ReceiptId, Guid CustomerId, Guid CompanyId, Guid LineOfBusinessId, decimal ReceiptDocumentAmount,
    IReadOnlyList<ReceiptAppropriationDueResponse> Data, int Total, int Page, int PageSize, int TotalPages,
    AppropriationTotals Totals, string ReceiptVersion, string DueFingerprint);

public sealed record ConfirmAllocationRequest(Guid DueId, decimal AllocatedAmount);
public sealed record ConfirmAppropriationRequest(
    Guid ClientRequestId, AppropriationMode Mode, IReadOnlyList<ConfirmAllocationRequest> Allocations,
    bool AcceptResidualTransfer, string? PreviewDueFingerprint = null, string? PreviewRuleVersion = null);

public sealed record ConfirmedAppropriationResponse(
    Guid AppropriationId, Guid ReceiptId, Guid ClientRequestId, AppropriationMode Mode,
    IReadOnlyList<ReceiptAppropriationDueResponse> Allocations, AppropriationTotals Totals,
    DateTime ConfirmedAtUtc, ReceiptAppropriationStatus Status, string Actor, Guid? AuditId, string CorrelationId);