using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

public sealed class InMemoryCourtAuditRepository : ICourtAuditRepository
{
    private readonly List<CourtAuditEntry> _store = new();

    public IReadOnlyList<CourtAuditEntry> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static CourtAuditEntry Clone(CourtAuditEntry x) => new()
    {
        Id = x.Id,
        CourtId = x.CourtId,
        Action = x.Action,
        OldValues = x.OldValues,
        NewValues = x.NewValues,
        Summary = x.Summary,
        ChangedBy = x.ChangedBy,
        ChangedAtUtc = x.ChangedAtUtc,
    };

    public Task<CourtAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<CourtAuditEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<CourtAuditEntry>> FindAsync(Expression<Func<CourtAuditEntry, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(CourtAuditEntry entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }
    public Task UpdateAsync(CourtAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");
    public Task DeleteAsync(CourtAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");

    public Task<int> CountAsync(Expression<Func<CourtAuditEntry, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default)
    {
        var items = _store.Where(x => x.CourtId == courtId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).Select(Clone).ToList();
        return Task.FromResult(items);
    }
}