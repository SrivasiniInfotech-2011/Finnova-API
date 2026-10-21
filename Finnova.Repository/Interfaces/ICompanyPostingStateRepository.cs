using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICompanyPostingStateRepository : IRepository<CompanyPostingState>
{
    Task<CompanyPostingState?> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
}
