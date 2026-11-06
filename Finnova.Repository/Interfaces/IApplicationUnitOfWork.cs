// Finnova.Repository/Interfaces/IApplicationUnitOfWork.cs
using Finnova.Repository.Context;

namespace Finnova.Repository.Interfaces;

public interface IApplicationUnitOfWork
{
    FinnovaDbContext Context { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);
}
