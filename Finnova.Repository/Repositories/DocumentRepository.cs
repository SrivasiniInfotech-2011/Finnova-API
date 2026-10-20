using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class DocumentRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<Document>(finnovaDbContext), IDocumentRepository
{
    public async Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(x => x.ApplicabilityRules)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<(List<Document> Items, int Total)> GetPagedAsync(
        string? search, string? docCategory, DocumentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().Include(x => x.ApplicabilityRules).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.DocCode.Contains(term) || x.DocName.Contains(term)); // R10.1 (CI collation)
        }
        if (!string.IsNullOrWhiteSpace(docCategory))
            query = query.Where(x => x.DocCategory == docCategory);     // R10.2 AND filter
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);         // R10.2 AND filter

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.DocCode).ThenBy(x => x.Id)                   // R10.8
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<List<Document>> GetActiveForConsumptionAsync(
        string constitution, string entityRole, CancellationToken ct = default)
    {
        // Live documents (R11.3) whose applicability matrix contains the pair (R11.1).
        // A document under a pending modification (PendingModification) still has an authorized
        // live version and must keep serving consumers using its live columns until the edit is
        // authorized (R3.1/R3.2/R11.4). PendingApproval (never-authorized) is NOT yet live.
        return await DbSet.AsNoTracking()
            .Include(x => x.ApplicabilityRules)
            .Where(x => (x.Status == DocumentStatus.Active
                         || x.Status == DocumentStatus.PendingModification)
                        && x.ApplicabilityRules.Any(r =>
                            r.Constitution == constitution && r.EntityRole == entityRole))
            .OrderBy(x => x.DocCode).ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = docCode.Trim().ToUpper();   // trimmed + case-insensitive (R2.2)
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.DocCode.ToUpper() == c && (excludeId == null || x.Id != excludeId), ct);
    }
}