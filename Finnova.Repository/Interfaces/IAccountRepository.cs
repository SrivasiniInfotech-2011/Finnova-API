using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IAccountRepository : IRepository<Account>
{
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Account>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
