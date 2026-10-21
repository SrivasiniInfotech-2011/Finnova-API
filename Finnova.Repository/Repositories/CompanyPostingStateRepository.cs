using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class CompanyPostingStateRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<CompanyPostingState>(finnovaDbContext), ICompanyPostingStateRepository
{
    public async Task<CompanyPostingState?> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(x => x.CompanyId == companyId, ct);
}
