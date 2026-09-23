using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="ILookupRepository"/> backed by a <see cref="List{T}"/>.
/// Faithfully mirrors the real EF-backed <c>LookupRepository</c> semantics so property tests
/// exercise the actual command/query handlers cheaply over 100+ iterations:
///   - case-sensitive (ordinal) Module / LookupType matching,
///   - paged list ordered by DisplayOrder then Id,
///   - dropdown ordered by DisplayOrder then Value, active-only,
///   - Code uniqueness scoped to (Module, LookupType) with optional excludeId.
/// Mutations clone entities on read paths where the real repo uses AsNoTracking, so that a
/// caller mutating a returned entity does not accidentally mutate stored state.
/// </summary>
public sealed class InMemoryLookupRepository : ILookupRepository
{
    private readonly List<LookupValue> _store = new();

    public InMemoryLookupRepository() { }

    public InMemoryLookupRepository(IEnumerable<LookupValue> seed)
    {
        foreach (var v in seed)
            _store.Add(Clone(v));
    }

    /// <summary>Direct snapshot of stored rows (cloned) for assertions.</summary>
    public IReadOnlyList<LookupValue> Snapshot() => _store.Select(Clone).ToList();

    public int Count => _store.Count;

    private static LookupValue Clone(LookupValue x) => new()
    {
        Id = x.Id,
        Module = x.Module,
        LookupType = x.LookupType,
        Code = x.Code,
        Value = x.Value,
        DisplayOrder = x.DisplayOrder,
        IsActive = x.IsActive,
        IsSystemLocked = x.IsSystemLocked,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    // ---- IRepository<LookupValue> ----

    public Task<LookupValue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var found = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<List<LookupValue>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<LookupValue>> FindAsync(Expression<Func<LookupValue, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var f = predicate.Compile();
        return Task.FromResult(_store.Where(f).Select(Clone).ToList());
    }

    public Task AddAsync(LookupValue entity, CancellationToken cancellationToken = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LookupValue entity, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(x => x.Id == entity.Id);
        if (idx >= 0)
            _store[idx] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(LookupValue entity, CancellationToken cancellationToken = default)
    {
        _store.RemoveAll(x => x.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<LookupValue, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var count = predicate is null ? _store.Count : _store.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    // ---- ILookupRepository ----

    public Task<(List<LookupValue> Items, int Total)> GetPagedAsync(
        string? module, string? lookupType, bool? isActive,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IEnumerable<LookupValue> query = _store;

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(x => x.Module == module);
        if (!string.IsNullOrWhiteSpace(lookupType))
            query = query.Where(x => x.LookupType == lookupType);
        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var materialized = query.ToList();
        var total = materialized.Count;

        var items = materialized
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)
            .ToList();

        return Task.FromResult((items, total));
    }

    public Task<List<LookupValue>> GetActiveByModuleAndTypeAsync(
        string module, string lookupType, CancellationToken cancellationToken = default)
    {
        var items = _store
            .Where(x => x.Module == module && x.LookupType == lookupType && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Value, StringComparer.Ordinal)
            .Select(Clone)
            .ToList();

        return Task.FromResult(items);
    }

    public Task<bool> ExistsByCodeAsync(
        string module, string lookupType, string code,
        Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _store.Any(x =>
            x.Module == module &&
            x.LookupType == lookupType &&
            x.Code == code &&
            (excludeId == null || x.Id != excludeId));

        return Task.FromResult(exists);
    }

    public Task<bool> ScopeExistsAsync(
        string module, string lookupType, CancellationToken cancellationToken = default)
    {
        var exists = _store.Any(x => x.Module == module && x.LookupType == lookupType);
        return Task.FromResult(exists);
    }

    public Task<LookupValue?> GetByModuleTypeCodeAsync(
        string module, string lookupType, string code, CancellationToken cancellationToken = default)
    {
        var found = _store.FirstOrDefault(x =>
            x.Module == module && x.LookupType == lookupType && x.Code == code);
        return Task.FromResult(found is null ? null : Clone(found));
    }
}
