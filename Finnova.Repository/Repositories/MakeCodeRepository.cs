using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class MakeCodeRepository : RepositoryBase<MakeCode>, IMakeCodeRepository
{
    public MakeCodeRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(List<MakeCode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => x.Code.Contains(term) || x.Description.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Code).ThenBy(x => x.Id)
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

    public async Task<List<MakeCode>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
}
