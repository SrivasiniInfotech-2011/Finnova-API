using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class GLAccountRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<GLAccount>(finnovaDbContext), IGLAccountRepository
{
    public async Task<(List<GLAccount> Items, int Total)> GetPagedAsync(
        GLAccountFilter filter, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.GLCode))
        {
            var code = filter.GLCode.Trim();
            query = query.Where(x => x.GLCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var name = filter.Name.Trim();
            query = query.Where(x => x.Name.Contains(name));
        }
        if (filter.Nature is not null) query = query.Where(x => x.AccountNature == filter.Nature);
        if (filter.Type is not null) query = query.Where(x => x.GLType == filter.Type);
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.ParentGLId is not null) query = query.Where(x => x.ParentGLId == filter.ParentGLId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.GLCode)                             // R8.6 sibling/listing order
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(
        Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.CompanyId == companyId && x.GLCode == c
                 && (excludeId == null || x.Id != excludeId), ct);
    }

    public async Task<GLAccount?> GetInactiveByCodeAsync(
        Guid companyId, string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(
            x => x.CompanyId == companyId && x.GLCode == c && x.Status == GLStatus.Inactive, ct);
    }

    public async Task<bool> ExistsByNameAsync(
        Guid companyId, string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var n = name.Trim();
        // Default CI collation makes == case-insensitive; ToLower keeps it explicit/portable.
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.CompanyId == companyId && x.Name.ToLower() == n.ToLower()
                 && (excludeId == null || x.Id != excludeId), ct);
    }

    public async Task<GLAccount?> GetWithBranchesAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(x => x.BranchApplicabilities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<List<GLAccount>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.GLCode)
            .ToListAsync(ct);

    public async Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(x => x.ParentGLId == id && x.Status == GLStatus.Active, ct);
}
