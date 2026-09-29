using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="INumberingSchemeRepository"/> mirroring the real EF-backed
/// repository semantics: trimmed case-insensitive search over Code/Name/DocumentType, Name-then-Code
/// ordering, code and (DocumentType, Scope, ScopeId) uniqueness, and the atomic issuance logic
/// (period reset + increment + exhaustion guard). Being single-threaded per instance, it provides
/// the same "no duplicate/gap sequence value" contract that the real Serializable transaction gives
/// under concurrency. Read paths clone so callers cannot mutate stored state.
/// </summary>
public sealed class InMemoryNumberingSchemeRepository : INumberingSchemeRepository
{
    private readonly List<NumberingScheme> _store = new();

    public InMemoryNumberingSchemeRepository() { }

    public InMemoryNumberingSchemeRepository(IEnumerable<NumberingScheme> seed)
    {
        foreach (var s in seed) _store.Add(Clone(s));
    }

    public IReadOnlyList<NumberingScheme> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static NumberingScheme Clone(NumberingScheme x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        DocumentType = x.DocumentType,
        FormatTemplate = x.FormatTemplate,
        Prefix = x.Prefix,
        Suffix = x.Suffix,
        SeqStart = x.SeqStart,
        SeqIncrement = x.SeqIncrement,
        SeqPadding = x.SeqPadding,
        ResetRule = x.ResetRule,
        Scope = x.Scope,
        ScopeId = x.ScopeId,
        CurrentValue = x.CurrentValue,
        PeriodKey = x.PeriodKey,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    // ---- IRepository<NumberingScheme> ----

    public Task<NumberingScheme?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var found = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<List<NumberingScheme>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<NumberingScheme>> FindAsync(Expression<Func<NumberingScheme, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(NumberingScheme entity, CancellationToken ct = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NumberingScheme entity, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(x => x.Id == entity.Id);
        if (idx >= 0) _store[idx] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(NumberingScheme entity, CancellationToken ct = default)
    {
        _store.RemoveAll(x => x.Id == entity.Id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<NumberingScheme, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    // ---- INumberingSchemeRepository ----

    public Task<(List<NumberingScheme> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<NumberingScheme> query = _store;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.DocumentType.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = query.ToList();
        var total = materialized.Count;
        var items = materialized
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)
            .ToList();
        return Task.FromResult((items, total));
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        var exists = _store.Any(x =>
            string.Equals(x.Code, c, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || x.Id != excludeId));
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var dt = documentType.Trim();
        var exists = _store.Any(x =>
            string.Equals(x.DocumentType, dt, StringComparison.OrdinalIgnoreCase) &&
            x.Scope == scope && x.ScopeId == scopeId &&
            (excludeId == null || x.Id != excludeId));
        return Task.FromResult(exists);
    }

    public Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextAsync(
        string documentType, NumberScope scope, Guid? scopeId, DateTime utcNow, CancellationToken ct = default)
    {
        var scheme = _store.FirstOrDefault(x =>
            string.Equals(x.DocumentType, documentType.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.Scope == scope && x.ScopeId == scopeId);
        if (scheme is null) return Task.FromResult<(NumberingScheme, long)?>(null);
        if (!scheme.IsActive) throw new NumberingSchemeInactiveException(scheme.Code);

        var period = PeriodKey.For(scheme.ResetRule, utcNow);
        long next = scheme.PeriodKey == period && scheme.CurrentValue >= scheme.SeqStart
            ? scheme.CurrentValue + scheme.SeqIncrement
            : scheme.SeqStart;

        if (next > NumberFormatter.MaxValueForPadding(scheme.SeqPadding))
            throw new NumberSequenceExhaustedException(scheme.Code);

        scheme.CurrentValue = next;
        scheme.PeriodKey = period;
        scheme.UpdatedAt = utcNow;
        return Task.FromResult<(NumberingScheme, long)?>((Clone(scheme), next));
    }
}
