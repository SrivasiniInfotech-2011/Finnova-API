using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class GLAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<GLAuditEntry>(finnovaDbContext), IGLAuditRepository
{
    public async Task<List<GLAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
            .Where(x => x.GLAccountId == glAccountId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R10.3
            .ToListAsync(ct);                                                     // empty when none (R10.5)

    public async Task<int> CountForGLAccountAsync(Guid glAccountId, CancellationToken ct = default)
        => await DbSet.AsNoTracking().CountAsync(x => x.GLAccountId == glAccountId, ct);
}
