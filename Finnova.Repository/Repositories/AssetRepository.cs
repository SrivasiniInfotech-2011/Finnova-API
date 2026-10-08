using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class AssetRepository : RepositoryBase<Asset>, IAssetRepository
{
    public AssetRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(List<Asset> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(x => x.ClassCode)
            .Include(x => x.TypeCode)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();                                 // CI via collation (R5.2)
            query = query.Where(x => x.AssetCode.Contains(term) || x.Description.Contains(term));
        }
        var total = await query.CountAsync(ct);                           // R5.1
        var items = await query
            .OrderBy(x => x.AssetCode).ThenBy(x => x.Id)                   // R5.7
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<Asset?> GetByIdWithReferencesAsync(Guid id, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
            .Include(x => x.ClassCode)
            .Include(x => x.TypeCode)
            .Include(x => x.MakeCode)
            .Include(x => x.ModelCode)
            .FirstOrDefaultAsync(x => x.Id == id, ct);                     // R5.8

    public async Task<bool> AssetCodeExistsAsync(string assetCode, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.AssetCode == assetCode, ct); // R3.3

    public async Task<int> GetMaxSequenceForClassAsync(string classCode, CancellationToken ct = default)
    {
        // Asset Codes for a class are "{CLASS}-{seq:D6}". Pull the matching prefix, parse the
        // numeric suffix, and return the maximum (0 when none exist). Done client-side after a
        // prefix filter so the D6 suffix parse is simple and provider-agnostic.
        var prefix = (classCode ?? string.Empty).Trim().ToUpperInvariant() + "-";
        var codes = await DbSet.AsNoTracking()
            .Where(x => x.AssetCode.StartsWith(prefix))
            .Select(x => x.AssetCode)
            .ToListAsync(ct);

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code.Substring(prefix.Length);
            if (int.TryParse(suffix, out var n) && n > max) max = n;
        }
        return max;
    }

    public async Task<bool> IsClassCodeReferencedAsync(Guid classCodeId, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.ClassCodeId == classCodeId, ct);   // R6.4

    public async Task<bool> IsTypeCodeReferencedAsync(Guid typeCodeId, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.TypeCodeId == typeCodeId, ct);

    public async Task<bool> IsMakeCodeReferencedAsync(Guid makeCodeId, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.MakeCodeId == makeCodeId, ct);

    public async Task<bool> IsModelCodeReferencedAsync(Guid modelCodeId, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.ModelCodeId == modelCodeId, ct);
}
