using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="INumberingSchemeAuditRepository"/> mirroring the real repo:
/// entries ordered ChangedAtUtc desc then Id desc, empty list for an unknown id. Immutable:
/// Update/Delete throw to reflect the design guarantee.
/// </summary>
public sealed class InMemoryNumberingSchemeAuditRepository : INumberingSchemeAuditRepository
{
    private readonly List<NumberingSchemeAuditEntry> _store = new();

    public IReadOnlyList<NumberingSchemeAuditEntry> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static NumberingSchemeAuditEntry Clone(NumberingSchemeAuditEntry x) => new()
    {
        Id = x.Id,
        SchemeId = x.SchemeId,
        Action = x.Action,
        OldValues = x.OldValues,
        NewValues = x.NewValues,
        Summary = x.Summary,
        ChangedBy = x.ChangedBy,
        ChangedAtUtc = x.ChangedAtUtc,
    };

    public Task<NumberingSchemeAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var found = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<List<NumberingSchemeAuditEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<NumberingSchemeAuditEntry>> FindAsync(Expression<Func<NumberingSchemeAuditEntry, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(NumberingSchemeAuditEntry entity, CancellationToken ct = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NumberingSchemeAuditEntry entity, CancellationToken ct = default)
        => throw new NotSupportedException("Audit entries are immutable and cannot be updated.");

    public Task DeleteAsync(NumberingSchemeAuditEntry entity, CancellationToken ct = default)
        => throw new NotSupportedException("Audit entries are immutable and cannot be deleted.");

    public Task<int> CountAsync(Expression<Func<NumberingSchemeAuditEntry, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<List<NumberingSchemeAuditEntry>> GetBySchemeIdAsync(Guid schemeId, CancellationToken ct = default)
    {
        var items = _store
            .Where(x => x.SchemeId == schemeId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(Clone)
            .ToList();
        return Task.FromResult(items);
    }
}
