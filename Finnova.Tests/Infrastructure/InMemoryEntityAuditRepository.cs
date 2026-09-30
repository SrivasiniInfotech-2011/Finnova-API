using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

public sealed class InMemoryEntityAuditRepository : IEntityAuditRepository
{
    private readonly List<EntityAuditEntry> _store = new();

    public IReadOnlyList<EntityAuditEntry> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static EntityAuditEntry Clone(EntityAuditEntry x) => new()
    {
        Id = x.Id,
        EntityId = x.EntityId,
        Action = x.Action,
        OldValues = x.OldValues,
        NewValues = x.NewValues,
        Summary = x.Summary,
        ChangedBy = x.ChangedBy,
        ChangedAtUtc = x.ChangedAtUtc,
    };

    public Task<EntityAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<EntityAuditEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<EntityAuditEntry>> FindAsync(Expression<Func<EntityAuditEntry, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(EntityAuditEntry entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }
    public Task UpdateAsync(EntityAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");
    public Task DeleteAsync(EntityAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");

    public Task<int> CountAsync(Expression<Func<EntityAuditEntry, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<List<EntityAuditEntry>> GetByEntityIdAsync(Guid entityId, CancellationToken ct = default)
    {
        var items = _store.Where(x => x.EntityId == entityId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).Select(Clone).ToList();
        return Task.FromResult(items);
    }
}