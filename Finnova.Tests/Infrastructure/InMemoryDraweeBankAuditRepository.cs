using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>In-memory audit repo mirroring the real one: newest-first by ChangedAtUtc then Id,
/// empty list for an unknown id, add + immutable reads only (R7.4, R7.5, R7.6).</summary>
public sealed class InMemoryDraweeBankAuditRepository : IDraweeBankAuditRepository
{
    private readonly List<DraweeBankAuditEntry> _store = new();

    public IReadOnlyList<DraweeBankAuditEntry> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static DraweeBankAuditEntry Clone(DraweeBankAuditEntry a) => new()
    {
        Id = a.Id,
        DraweeBankId = a.DraweeBankId,
        Action = a.Action,
        BeforeSnapshot = a.BeforeSnapshot,
        AfterSnapshot = a.AfterSnapshot,
        ChangedBy = a.ChangedBy,
        ChangedAtUtc = a.ChangedAtUtc
    };

    public Task<DraweeBankAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }
    public Task<List<DraweeBankAuditEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());
    public Task<List<DraweeBankAuditEntry>> FindAsync(Expression<Func<DraweeBankAuditEntry, bool>> p, CancellationToken ct = default)
        => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());
    public Task AddAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }
    // Immutable: Update/Delete are intentionally not meaningful for audit entries (R7.6).
    public Task UpdateAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
        => throw new InvalidOperationException("Audit entries are immutable.");
    public Task DeleteAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
        => throw new InvalidOperationException("Audit entries are immutable.");
    public Task<int> CountAsync(Expression<Func<DraweeBankAuditEntry, bool>>? p = null, CancellationToken ct = default)
        => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));

    public Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(Guid bankId, CancellationToken ct = default)
        => Task.FromResult(_store
            .Where(x => x.DraweeBankId == bankId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(Clone).ToList());
}
