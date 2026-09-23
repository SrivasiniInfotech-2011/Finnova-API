using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class LookupRepository : RepositoryBase<LookupValue>, ILookupRepository
{
    public LookupRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(List<LookupValue> Items, int Total)> GetPagedAsync(
        string? module, string? lookupType, bool? isActive,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(x => x.Module == module);        // case-sensitive scope (R1.2, R4.2)
        if (!string.IsNullOrWhiteSpace(lookupType))
            query = query.Where(x => x.LookupType == lookupType);
        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value); // no filter => all (R4.3)

        var total = await query.CountAsync(cancellationToken);      // total before paging (R4.5)

        var items = await query
            .OrderBy(x => x.DisplayOrder)                            // primary order (R4.6)
            .ThenBy(x => x.Id)                                       // deterministic tie-break (R4.6)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<LookupValue>> GetActiveByModuleAndTypeAsync(
        string module, string lookupType, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.Module == module && x.LookupType == lookupType && x.IsActive) // R6.1
            .OrderBy(x => x.DisplayOrder)                                                // R6.2
            .ThenBy(x => x.Value)                                                      // stable tie-break (R6.2)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(
        string module, string lookupType, string code,
        Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(x =>
            x.Module == module &&
            x.LookupType == lookupType &&
            x.Code == code &&
            (excludeId == null || x.Id != excludeId), cancellationToken);
    }

    public async Task<bool> ScopeExistsAsync(
        string module, string lookupType, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .AnyAsync(x => x.Module == module && x.LookupType == lookupType, cancellationToken);
    }

    public async Task<LookupValue?> GetByModuleTypeCodeAsync(
        string module, string lookupType, string code, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Module == module && x.LookupType == lookupType && x.Code == code, cancellationToken);
    }
}

