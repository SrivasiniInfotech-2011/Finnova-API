using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.AppropriationLogic;

public sealed record AppropriationLogicDetailRequest(AppropriationDueType DueType, decimal Percentage);
public sealed record AppropriationLogicDetailResponse(Guid Id, int SequenceNumber, AppropriationDueType DueType, string CashFlowDueTypeCode, decimal Percentage);

public sealed record CreateAppropriationLogicRequest(
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool Active,
    IReadOnlyList<AppropriationLogicDetailRequest> Details,
    string IdempotencyKey);

public sealed record UpdateAppropriationLogicRequest(
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool Active,
    IReadOnlyList<AppropriationLogicDetailRequest> Details,
    int ExpectedVersion,
    string IdempotencyKey);

public sealed record LifecycleAppropriationLogicRequest(int ExpectedVersion, string IdempotencyKey);

public sealed record AppropriationLogicResponse(
    Guid RuleId,
    Guid RuleVersionId,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool IsActive,
    int VersionNumber,
    IReadOnlyList<AppropriationLogicDetailResponse> Details,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? DeactivatedAtUtc,
    string CorrelationId);

public sealed record AppropriationLogicListItem(
    Guid RuleId,
    Guid RuleVersionId,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool IsActive,
    int VersionNumber,
    decimal PercentageTotal,
    DateTime CreatedAtUtc);

public sealed record AppropriationResolutionDetail(
    Guid DetailId,
    AppropriationDueType DueType,
    string CashFlowDueTypeCode,
    decimal Percentage,
    int SequenceNumber);

public sealed record AppropriationResolutionResponse(
    string ResolverContractVersion,
    Guid RuleId,
    Guid RuleVersionId,
    int VersionNumber,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    bool UsedGlobalFallback,
    AppropriationDirection Direction,
    bool FullDue,
    IReadOnlyList<AppropriationResolutionDetail> Details);