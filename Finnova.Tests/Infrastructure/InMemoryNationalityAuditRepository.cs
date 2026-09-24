using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="INationalityAuditRepository"/> backed by a
/// <see cref="List{T}"/>. Mirrors the real EF-backed <c>NationalityAuditRepository</c>:
///   - <see cref="GetByNationalityIdAsync"/> filters by NationalityId and orders by
///     ChangedAtUtc descending then Id descending, returning an empty list (never an error)
///     for a nationality id with no entries (R4.4, R4.5).
///
/// Audit entries are immutable (R4.6): the store supports add and immutable reads only.
/// <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> exist to satisfy the generic
/// <see cref="IRepository{T}"/> contract but throw <see cref="NotSupportedException"/> so any
/// attempt to mutate a recorded audit entry fails, reflecting the design's immutability
/// guarantee. Read paths clone entities so callers cannot mutate stored state.
/// </summary>
public sealed class InMemoryNationalityAuditRepository : INationalityAuditRepository
{
    private readonly List<NationalityAuditEntry> _store = new();

    public InMemoryNationalityAuditRepository() { }

    public InMemoryNationalityAuditRepository(IEnumerable<NationalityAuditEntry> seed)
    {
        foreach (var a in seed)
            _store.Add(Clone(a));
    }

    /// <summary>Direct snapshot of stored audit rows (cloned) for assertions.</summary>
    public IReadOnlyList<NationalityAuditEntry> Snapshot() => _store.Select(Clone).ToList();

    public int Count => _store.Count;

    private static NationalityAuditEntry Clone(NationalityAuditEntry x) => new()
    {
        Id = x.Id,
        NationalityId = x.NationalityId,
        Action = x.Action,
        OldName = x.OldName,
        NewName = x.NewName,
        ChangedBy = x.ChangedBy,
        ChangedAtUtc = x.ChangedAtUtc,
    };

    // ---- IRepository<NationalityAuditEntry> ----

    public Task<NationalityAuditEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var found = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<List<NationalityAuditEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<NationalityAuditEntry>> FindAsync(Expression<Func<NationalityAuditEntry, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var f = predicate.Compile();
        return Task.FromResult(_store.Where(f).Select(Clone).ToList());
    }

    public Task AddAsync(NationalityAuditEntry entity, CancellationToken cancellationToken = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }

    /// <summary>Audit entries are immutable (R4.6); updating is not supported.</summary>
    public Task UpdateAsync(NationalityAuditEntry entity, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Audit entries are immutable and cannot be updated.");

    /// <summary>Audit entries are immutable (R4.6); deleting is not supported.</summary>
    public Task DeleteAsync(NationalityAuditEntry entity, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Audit entries are immutable and cannot be deleted.");

    public Task<int> CountAsync(Expression<Func<NationalityAuditEntry, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var count = predicate is null ? _store.Count : _store.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    // ---- INationalityAuditRepository ----

    public Task<List<NationalityAuditEntry>> GetByNationalityIdAsync(
        Guid nationalityId, CancellationToken ct = default)
    {
        var items = _store
            .Where(x => x.NationalityId == nationalityId)
            .OrderByDescending(x => x.ChangedAtUtc)   // newest first (R4.4)
            .ThenByDescending(x => x.Id)              // deterministic tie-break (R4.4)
            .Select(Clone)
            .ToList();                                // empty list when none match (R4.5)

        return Task.FromResult(items);
    }
}
