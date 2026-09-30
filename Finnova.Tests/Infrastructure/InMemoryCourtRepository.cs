using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>Hand-written in-memory ICourtRepository mirroring the EF repo semantics.</summary>
public sealed class InMemoryCourtRepository : ICourtRepository
{
    private readonly List<Court> _store = new();

    public InMemoryCourtRepository() { }
    public InMemoryCourtRepository(IEnumerable<Court> seed) { foreach (var c in seed) _store.Add(Clone(c)); }

    public IReadOnlyList<Court> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static Court Clone(Court x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        CourtType = x.CourtType,
        Jurisdiction = x.Jurisdiction,
        Location = x.Location,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    public Task<Court?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<Court>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<Court>> FindAsync(Expression<Func<Court, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(Court entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }

    public Task UpdateAsync(Court entity, CancellationToken ct = default)
    {
        var i = _store.FindIndex(x => x.Id == entity.Id);
        if (i >= 0) _store[i] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Court entity, CancellationToken ct = default) { _store.RemoveAll(x => x.Id == entity.Id); return Task.CompletedTask; }

    public Task<int> CountAsync(Expression<Func<Court, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<(List<Court> Items, int Total)> GetPagedAsync(string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<Court> q = _store;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var t = searchTerm.Trim();
            q = q.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Jurisdiction.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        var all = q.ToList();
        var items = all
            .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Task.FromResult(_store.Any(x =>
            string.Equals(x.Code, c, StringComparison.OrdinalIgnoreCase) && (excludeId == null || x.Id != excludeId)));
    }
}