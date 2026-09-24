using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="INationalityRepository"/> backed by a <see cref="List{T}"/>.
/// Faithfully mirrors the real EF-backed <c>NationalityRepository</c> semantics so property tests
/// exercise the actual command/query handlers cheaply over 100+ iterations:
///   - <see cref="GetPagedAsync"/>: trimmed, case-insensitive substring match on Code OR Name
///     (blank/whitespace term applies no filter), total counted before paging, ordered by
///     Name asc then Code asc, then the requested page window,
///   - <see cref="ExistsByCodeAsync"/>: trimmed, case-insensitive Code comparison with optional
///     excludeId.
/// The real repository relies on SQL Server's default case-insensitive collation for both the
/// <c>Contains</c> search and the <c>Code</c> uniqueness check; this in-memory double reproduces
/// that behavior explicitly with <see cref="StringComparison.OrdinalIgnoreCase"/> so tests match
/// production semantics rather than C#'s ordinal default.
/// Read paths clone entities (the real repo uses AsNoTracking) so a caller mutating a returned
/// entity does not accidentally mutate stored state.
/// </summary>
public sealed class InMemoryNationalityRepository : INationalityRepository
{
    private readonly List<Nationality> _store = new();

    public InMemoryNationalityRepository() { }

    public InMemoryNationalityRepository(IEnumerable<Nationality> seed)
    {
        foreach (var n in seed)
            _store.Add(Clone(n));
    }

    /// <summary>Direct snapshot of stored rows (cloned) for assertions.</summary>
    public IReadOnlyList<Nationality> Snapshot() => _store.Select(Clone).ToList();

    public int Count => _store.Count;

    private static Nationality Clone(Nationality x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    // ---- IRepository<Nationality> ----

    public Task<Nationality?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var found = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<List<Nationality>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<Nationality>> FindAsync(Expression<Func<Nationality, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var f = predicate.Compile();
        return Task.FromResult(_store.Where(f).Select(Clone).ToList());
    }

    public Task AddAsync(Nationality entity, CancellationToken cancellationToken = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Nationality entity, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(x => x.Id == entity.Id);
        if (idx >= 0)
            _store[idx] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Nationality entity, CancellationToken cancellationToken = default)
    {
        _store.RemoveAll(x => x.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<Nationality, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var count = predicate is null ? _store.Count : _store.Count(predicate.Compile());
        return Task.FromResult(count);
    }

    // ---- INationalityRepository ----

    public Task<(List<Nationality> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<Nationality> query = _store;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // Case-insensitive substring on Code OR Name (R5.1), matching the real repo's
            // CI-collation-backed Contains.
            query = query.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = query.ToList();
        var total = materialized.Count;                               // total before paging (R5.4)

        var items = materialized
            .OrderBy(x => x.Name, StringComparer.Ordinal)             // deterministic order (R5.10)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)
            .ToList();

        return Task.FromResult((items, total));
    }

    public Task<bool> ExistsByCodeAsync(
        string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        var exists = _store.Any(x =>
            string.Equals(x.Code, c, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || x.Id != excludeId));                // CI via collation (R2.1)

        return Task.FromResult(exists);
    }
}
