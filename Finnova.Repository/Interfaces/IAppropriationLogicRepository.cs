using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

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