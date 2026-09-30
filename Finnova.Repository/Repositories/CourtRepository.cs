using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class CourtRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<Court>(finnovaDbContext), ICourtRepository
{
    public async Task<(List<Court> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term) || x.Name.Contains(term) || x.Jurisdiction.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Code)   // R3.7
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.Code == c && (excludeId == null || x.Id != excludeId), ct);
    }
}