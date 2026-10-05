namespace Finnova.Models.Contracts.DraweeBanks;

// ---- Child submitted inside a create/update ----

// Dates ISO-8601; EndDate nullable (open-ended branch, R3.5).
public record DraweeBranchRequest(
    string PlaceCode,
    string PlaceName,
    string Address,
    string PostalCode,
    DateTime StartDate,
    DateTime? EndDate);

// Optional restriction submitted inside a create/update.
public record RestrictionDetailRequest(
    int ClearingDays,
    DateTime StartDate,
    DateTime EndDate);

// ---- Create ----

// POST /draweebank — IsActive nullable so the service defaults it to true (R1.2).
// Challan rules are NOT created inline with the bank; they are managed only via their own route.
public record CreateDraweeBankRequest(
    string BankCode,
    string BankName,
    bool? IsActive,
    IReadOnlyList<DraweeBranchRequest> Branches,       // zero or more (R1.1)
    RestrictionDetailRequest? Restriction);            // optional (R4.4)

// ---- Update ----

// PUT /draweebank/{id} — BankCode immutable post-create. Branches/restriction are the full
// desired set; omitted branches are removed (R3.7), null restriction clears it (R4.4).
public record UpdateDraweeBankRequest(
    string BankName,
    IReadOnlyList<DraweeBranchRequest> Branches,
    RestrictionDetailRequest? Restriction);

// ---- Challan rule sub-routes ----

public record CreateChallanRuleRequest(
    string RuleCode,
    string FormatPattern,
    string ValidationExpression,
    string RoutingTarget,
    bool? IsActive);

// RuleCode immutable on update (mirrors BankCode); only mutable fields accepted (R6.5).
public record UpdateChallanRuleRequest(
    string FormatPattern,
    string ValidationExpression,
    string RoutingTarget,
    bool IsActive);
