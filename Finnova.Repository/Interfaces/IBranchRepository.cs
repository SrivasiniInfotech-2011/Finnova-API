using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IBranchRepository : IRepository<Branch>
{
    Task<Branch?> GetByCompositeCodeAsync(string corporateCode, string stateCode, string branchCode, CancellationToken cancellationToken = default);
    Task<List<Branch>> GetByCorporateCodeAsync(string corporateCode, CancellationToken cancellationToken = default);
}
