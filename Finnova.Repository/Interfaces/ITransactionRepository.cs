using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetByAccountIdPagedAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
