// CREATE Finnova.Repository/Repositories/CustomerUnitOfWork.cs
using Microsoft.EntityFrameworkCore.Storage;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class CustomerUnitOfWork : ICustomerUnitOfWork
{
    private readonly FinnovaDbContext _context;
    public CustomerUnitOfWork(FinnovaDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<ICustomerTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new CustomerTransaction(transaction);
    }

    private sealed class CustomerTransaction : ICustomerTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        public CustomerTransaction(IDbContextTransaction transaction) => _transaction = transaction;

        public async Task CommitAsync(CancellationToken ct = default)
        {
            await _transaction.CommitAsync(ct);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            await _transaction.RollbackAsync(ct);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
                await _transaction.RollbackAsync();   // no partial write on failure/early exit
            await _transaction.DisposeAsync();
        }
    }
}