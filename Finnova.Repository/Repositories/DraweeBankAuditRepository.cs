using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class DraweeBankAuditRepository : RepositoryBase<DraweeBankAuditEntry>, IDraweeBankAuditRepository
{
    public DraweeBankAuditRepository(FinnovaDbContext context) : base(context) { }

    public async Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(
        Guid draweeBankId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.DraweeBankId == draweeBankId)
            .OrderByDescending(x => x.ChangedAtUtc)   // newest first (R7.4)
            .ThenByDescending(x => x.Id)              // deterministic tie-break (R7.4)
            .ToListAsync(ct);                         // empty list when none match (R7.5)
    }
}
