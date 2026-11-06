// Finnova.Repository/Repositories/ApplicationUnitOfWork.cs
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Finnova.Repository.Repositories;

public sealed class ApplicationUnitOfWork : IApplicationUnitOfWork
{
    public FinnovaDbContext Context { get; }
    public ApplicationUnitOfWork(FinnovaDbContext context) => Context = context;
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default)
        => await Context.Database.BeginTransactionAsync(ct);
}
