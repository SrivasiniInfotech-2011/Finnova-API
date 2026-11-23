using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using System.Security.Claims;

namespace Finnova.Service.AppropriationLogic.Ports;

public sealed record CompanyScope(Guid CompanyId, bool CanAdminister);
public sealed record LineOfBusinessScope(Guid Id, Guid CompanyId, bool IsActive);
public sealed record DueHeadMapping(AppropriationDueType DueType, string CashFlowDueTypeCode, bool IsActive);
public sealed record AppropriationResolution(AppropriationRuleVersion Rule, bool UsedGlobalFallback);

public interface IAppropriationReferencePort
{
    Task<bool> IsActiveCompanyAsync(Guid companyId, CancellationToken ct);
    Task<LineOfBusinessScope?> GetLineOfBusinessAsync(Guid id, CancellationToken ct);
    Task<DueHeadMapping?> GetDueHeadMappingAsync(AppropriationDueType dueType, CancellationToken ct);
}

public interface IAppropriationAuthorizationPort
{
    Task<CompanyScope> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);
    bool CanAdminister(CompanyScope scope, Guid companyId);
}

public interface IAppropriationLogicRepository
{
    Task<IReadOnlyList<AppropriationRuleVersion>> ListAsync(Guid? companyId, Guid? lineOfBusinessId, bool includeInactive, CancellationToken ct);
    Task<AppropriationRuleVersion?> GetAsync(Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<AppropriationRuleVersion>> GetActiveCandidatesAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
    Task<RuleMutationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct);
    Task AddVersionAsync(AppropriationRuleVersion version, CancellationToken ct);
    Task AddAuditAsync(AppropriationRuleAuditEntry entry, CancellationToken ct);
    Task AddIdempotencyAsync(RuleMutationIdempotency entry, CancellationToken ct);
    Task DeactivateScopeAsync(Guid companyId, Guid? lineOfBusinessId, CancellationToken ct);
}

public interface IAppropriationLogicUnitOfWork
{
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IAppropriationRuleResolver
{
    Task<AppropriationResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}

public enum AppropriationResolutionStatus { Resolved = 0, NotFound = 1, Ambiguous = 2 }
public sealed record AppropriationResolutionResult(AppropriationResolutionStatus Status, AppropriationResolution? Resolution, string ResolverContractVersion, string? ErrorCode);