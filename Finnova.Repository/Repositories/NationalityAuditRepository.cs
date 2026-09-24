using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class NationalityAuditRepository : RepositoryBase<NationalityAuditEntry>, INationalityAuditRepository
{
    public NationalityAuditRepository(FinnovaDbContext context) : base(context) { }

    public async Task<List<NationalityAuditEntry>> GetByNationalityIdAsync(
        Guid nationalityId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.NationalityId == nationalityId)
            .OrderByDescending(x => x.ChangedAtUtc)   // newest first (R4.4)
            .ThenByDescending(x => x.Id)              // deterministic tie-break (R4.4)
            .ToListAsync(ct);                         // empty list when none match (R4.5)
    }
}
