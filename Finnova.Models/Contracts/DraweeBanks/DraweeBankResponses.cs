namespace Finnova.Models.Contracts.DraweeBanks;

public record DraweeBranchResponse(
    Guid Id,
    Guid DraweeBankId,
    string PlaceCode,
    string PlaceName,
    string Address,
    string PostalCode,
    DateTime StartDate,
    DateTime? EndDate);

public record RestrictionDetailResponse(
    Guid Id,
    Guid DraweeBankId,
    int ClearingDays,
    DateTime StartDate,
    DateTime EndDate);

public record ChallanRuleResponse(
    Guid Id,
    Guid DraweeBankId,
    string RuleCode,
    string FormatPattern,
    string ValidationExpression,
    string RoutingTarget,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// Admin-facing response (R1.1 returns ids + resolved IsActive). Includes the owned children only.
// Challan rules are decoupled and NOT part of this response; they are read via GET /{id}/challan-rules.
public record DraweeBankResponse(
    Guid Id,
    string BankCode,
    string BankName,
    bool IsActive,
    IReadOnlyList<DraweeBranchResponse> Branches,
    RestrictionDetailResponse? Restriction,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// One audit row (R7.4).
public record DraweeBankAuditEntryResponse(
    Guid Id,
    Guid DraweeBankId,
    string Action,                 // "Create" | "Update" | "Delete"
    string? BeforeSnapshot,
    string? AfterSnapshot,
    string ChangedBy,
    DateTime ChangedAtUtc);
