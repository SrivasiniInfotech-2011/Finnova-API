using Finnova.Models.Domain.Enums;
using System.Security.Claims;

namespace Finnova.Service.ReceiptAppropriation.Ports;

public sealed record ReceiptContext(Guid Id, Guid CustomerId, string CustomerCode, Guid CompanyId, Guid LineOfBusinessId, decimal Amount, string Currency, string Status, string Version);
public sealed record PendingDueProjection(Guid Id, Guid AccountId, string AccountNumber, Guid CustomerId, CashFlowDueType DueType, DateOnly DueDate, decimal OutstandingAmount, string Status, string Version);
public sealed record RuleDetailProjection(Guid Id, CashFlowDueType DueType, decimal Percentage, int Order, string CashFlowDueTypeCode = "");
public sealed record AppropriationRuleProjection(Guid Id, string Version, Guid CompanyId, Guid LineOfBusinessId, AppropriationDirection Direction, bool FullDue, bool IsActive, IReadOnlyList<RuleDetailProjection> Details);
public sealed record AppropriationRuleResolutionResult(string Status, Guid? RuleId, Guid? RuleVersionId, int? VersionNumber, string ResolverContractVersion, Guid CompanyId, Guid? LineOfBusinessId, bool UsedGlobalFallback, AppropriationDirection? Direction, bool? FullDue, IReadOnlyList<RuleDetailProjection> Details, string? ErrorCode);
public sealed record AdvanceAccountProjection(Guid Id, Guid CompanyId, Guid CustomerId, string Currency, bool IsActive, string Version);
public sealed record AuthorizationScope(IReadOnlySet<Guid> CompanyIds, IReadOnlySet<Guid> LineOfBusinessIds, IReadOnlySet<Guid> BranchIds, bool CanAccessAllCustomers);
public sealed record DueReservation(Guid DueId, decimal Amount, string Version);

public interface IReceiptContextPort
{
    Task<ReceiptContext?> GetAsync(Guid receiptId, CancellationToken ct);
    Task<bool> CanAppropriateAsync(ReceiptContext receipt, CancellationToken ct);
}
public interface IPendingDuePort
{
    Task<IReadOnlyList<PendingDueProjection>> GetEligibleAsync(ReceiptContext receipt, CancellationToken ct);
    Task ReserveAsync(IReadOnlyList<DueReservation> reservations, CancellationToken ct);
}
public interface IAppropriationRulePort
{
    Task<IReadOnlyList<AppropriationRuleProjection>> GetActiveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}

public interface IAppropriationRuleResolver
{
    Task<AppropriationRuleResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}
public interface IAdvanceAccountPort
{
    Task<AdvanceAccountProjection?> GetActiveAsync(Guid companyId, Guid customerId, CancellationToken ct);
}
public interface IAppropriationScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);
    bool CanAccess(AuthorizationScope scope, ReceiptContext receipt);
}