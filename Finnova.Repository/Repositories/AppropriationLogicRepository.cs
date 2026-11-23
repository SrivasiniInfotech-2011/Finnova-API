using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class AppropriationLogicRepository(FinnovaDbContext db) : IAppropriationLogicRepository
{
    public async Task<IReadOnlyList<AppropriationRuleVersion>> ListAsync(Guid? companyId, Guid? lineOfBusinessId, bool includeInactive, CancellationToken ct)
    {
        var query = db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).AsQueryable();
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId.Value);
        if (lineOfBusinessId.HasValue) query = query.Where(x => x.LineOfBusinessId == lineOfBusinessId.Value);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    }

    public Task<AppropriationRuleVersion?> GetAsync(Guid ruleId, CancellationToken ct) =>
        db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).Where(x => x.RuleId == ruleId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);

    public Task<IReadOnlyList<AppropriationRuleVersion>> GetActiveCandidatesAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct) =>
        db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).Where(x => x.CompanyId == companyId && x.IsActive && (x.LineOfBusinessId == lineOfBusinessId || x.LineOfBusinessId == null)).OrderBy(x => x.LineOfBusinessId == null).ToListAsync(ct).ContinueWith(x => (IReadOnlyList<AppropriationRuleVersion>)x.Result, ct);

    public Task<RuleMutationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct) => db.RuleMutationIdempotencies.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
    public Task AddVersionAsync(AppropriationRuleVersion version, CancellationToken ct) => db.AppropriationRuleVersions.AddAsync(version, ct).AsTask();
    public Task AddAuditAsync(AppropriationRuleAuditEntry entry, CancellationToken ct) => db.AppropriationRuleAuditEntries.AddAsync(entry, ct).AsTask();
    public Task AddIdempotencyAsync(RuleMutationIdempotency entry, CancellationToken ct) => db.RuleMutationIdempotencies.AddAsync(entry, ct).AsTask();

    public Task DeactivateScopeAsync(Guid companyId, Guid? lineOfBusinessId, CancellationToken ct) =>
        db.AppropriationRuleVersions.Where(x => x.CompanyId == companyId && x.LineOfBusinessId == lineOfBusinessId && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.DeactivatedAtUtc, DateTime.UtcNow), ct);
}