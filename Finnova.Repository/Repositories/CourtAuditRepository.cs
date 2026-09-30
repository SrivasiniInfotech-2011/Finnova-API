using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class CourtAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<CourtAuditEntry>(finnovaDbContext), ICourtAuditRepository
{
    public async Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.CourtId == courtId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R5.5
            .ToListAsync(ct);   // empty when none (R5.6)
    }
}