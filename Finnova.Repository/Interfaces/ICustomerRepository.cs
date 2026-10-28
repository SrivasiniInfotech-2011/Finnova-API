// CREATE Finnova.Repository/Interfaces/ICustomerRepository.cs
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICustomerRepository
{
    // Loads the full Customer aggregate (addresses, individual/non-individual details,
    // bank accounts, documents, tracks, factoring sub-limits) for Modify/child commands.
    Task<Customer?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByIdentityAsync(string normalizedIdentity, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
}

public interface ICustomerAuditRepository
{
    Task AddAsync(CustomerAuditEntry audit, CancellationToken ct = default);
}

public interface ICustomerReadRepository
{
    Task<(IReadOnlyCollection<Customer> Items, int Total)> SearchAsync(
        CustomerQueryRequest request, CancellationToken ct = default);
}

public interface ICustomerTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface ICustomerUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<ICustomerTransaction> BeginTransactionAsync(CancellationToken ct = default);
}