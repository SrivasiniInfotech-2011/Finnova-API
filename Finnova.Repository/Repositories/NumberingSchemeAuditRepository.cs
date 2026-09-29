using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

/// <summary>Repository for numbering-scheme audit entries (read + append only; immutable).</summary>
public class NumberingSchemeAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<NumberingSchemeAuditEntry>(finnovaDbContext), INumberingSchemeAuditRepository
{
    public async Task<List<NumberingSchemeAuditEntry>> GetBySchemeIdAsync(Guid schemeId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.SchemeId == schemeId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R6.6
            .ToListAsync(ct);   // empty when none (R6.7)
    }
}
