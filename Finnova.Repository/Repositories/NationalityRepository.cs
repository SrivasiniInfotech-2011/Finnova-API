using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class NationalityRepository : RepositoryBase<Nationality>, INationalityRepository
{
    public NationalityRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(List<Nationality> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // EF Core translates Contains to SQL LIKE; default CI collation makes it case-insensitive (R5.1).
            query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
        }

        var total = await query.CountAsync(ct);                       // total before paging (R5.4)

        var items = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Code)                 // deterministic order (R5.10)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(
        string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.Code == c && (excludeId == null || x.Id != excludeId), ct);   // CI via collation (R2.1)
    }
}
