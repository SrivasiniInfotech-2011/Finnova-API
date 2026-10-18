using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.GL;

// ---------- Requests ----------

public record CreateGLAccountRequest(
    Guid CompanyId,
    string GLCode,
    string Name,
    Guid? ParentGLId,
    GLType GLType,
    string? SubLedgerType,
    string? TaxType,
    Guid CurrencyId,
    bool AllowManualJV,
    bool CostCentreMandatory,
    bool ProfitCentreMandatory,
    string? ReportingLine,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    IReadOnlyList<Guid> ApplicableBranchIds);

/// <summary>Non-structural edit only (R4.1/R4.3).</summary>
public record UpdateGLAccountRequest(
    bool AllowManualJV,
    bool CostCentreMandatory,
    bool ProfitCentreMandatory,
    string? ReportingLine,
    IReadOnlyList<Guid> ApplicableBranchIds);

public record RejectGLRequest(string Reason);          // 1..500
public record FreezeGLRequest(string Reason);          // 1..500
public record DeactivateGLRequest(string? Reason);     // optional remark
public record MapSystemGLRequest(Guid CompanyId, SystemGLType Type, Guid GLAccountId);

public record GLAccountFilter(
    string? GLCode = null,
    string? Name = null,
    AccountNature? Nature = null,
    GLType? Type = null,
    GLStatus? Status = null,
    Guid? ParentGLId = null,
    int Page = 1,
    int PageSize = 20);

public record ExportGLRequest(ExportFormat Format, GLAccountFilter Filter);

// ---------- Responses ----------

public record GLAccountResponse(
    Guid Id,
    Guid CompanyId,
    string GLCode,
    string Name,
    GLLevel Level,
    Guid? ParentGLId,
    AccountNature AccountNature,
    NormalBalance NormalBalance,
    GLType GLType,
    string? SubLedgerType,
    string? TaxType,
    bool IsPostable,
    Guid CurrencyId,
    bool AllowManualJV,
    bool CostCentreMandatory,
    bool ProfitCentreMandatory,
    string? ReportingLine,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    GLStatus Status,
    int AuthorizedVersionNumber,
    IReadOnlyList<Guid> ApplicableBranchIds,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GLTreeNodeResponse(
    Guid Id,
    string GLCode,
    string Name,
    GLLevel Level,
    bool IsPostable,
    GLStatus Status,
    IReadOnlyList<GLTreeNodeResponse> Children);   // siblings ordered by GLCode asc

public record GLAuditEntryResponse(
    Guid Id,
    Guid GLAccountId,
    string Action,
    string? OldValues,
    string NewValues,
    string Summary,
    string? Remarks,
    string ChangedBy,
    string? ChangedByRole,
    DateTime ChangedAtUtc);

public record SystemGLCompletenessResponse(
    Guid CompanyId,
    bool IsComplete,
    IReadOnlyList<SystemGLType> MissingOrInvalid);

public record GLUploadRowResult(int RowNumber, string GLCode, bool Success, string? Error);
public record GLUploadReportResponse(Guid BatchId, bool AllSuccess, IReadOnlyList<GLUploadRowResult> Rows);
