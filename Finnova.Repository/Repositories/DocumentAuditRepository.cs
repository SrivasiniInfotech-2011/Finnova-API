using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class DocumentAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<DocumentAuditEntry>(finnovaDbContext), IDocumentAuditRepository
{
    public async Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(
        Guid documentId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R8.4
            .ToListAsync(ct);   // empty when none (R8.6)
    }
}