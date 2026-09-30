using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class EntityAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<EntityAuditEntry>(finnovaDbContext), IEntityAuditRepository
{
    public async Task<List<EntityAuditEntry>> GetByEntityIdAsync(Guid entityId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.EntityId == entityId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R5.5
            .ToListAsync(ct);   // empty when none (R5.6)
    }
}