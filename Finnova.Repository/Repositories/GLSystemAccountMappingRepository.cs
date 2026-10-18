using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class GLSystemAccountMappingRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<GLSystemAccountMapping>(finnovaDbContext), IGLSystemAccountMappingRepository
{
    public async Task<List<GLSystemAccountMapping>> GetActiveForCompanyAsync(
        Guid companyId, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .ToListAsync(ct);

    public async Task<GLSystemAccountMapping?> GetActiveAsync(
        Guid companyId, SystemGLType type, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            x => x.CompanyId == companyId && x.SystemGLType == type && x.IsActive, ct);
}
