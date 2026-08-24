using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class BranchRepository : RepositoryBase<Branch>, IBranchRepository
{
    public BranchRepository(FinnovaDbContext context) : base(context) { }

    public async Task<Branch?> GetByCompositeCodeAsync(string corporateCode, string stateCode, string branchCode, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(
            b => b.CorporateCode == corporateCode && b.StateCode == stateCode && b.BranchCode == branchCode,
            cancellationToken);
    }

    public async Task<List<Branch>> GetByCorporateCodeAsync(string corporateCode, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(b => b.CorporateCode == corporateCode)
            .OrderBy(b => b.BranchName)
            .ToListAsync(cancellationToken);
    }
}
