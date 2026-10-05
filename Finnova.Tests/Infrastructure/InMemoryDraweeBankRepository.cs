using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// Hand-written in-memory <see cref="IDraweeBankRepository"/> mirroring the real EF-backed repo so
/// property tests exercise the actual handlers cheaply. CI semantics via OrdinalIgnoreCase match
/// SQL Server's default collation. Read paths clone so callers cannot mutate stored state.
/// </summary>
public sealed class InMemoryDraweeBankRepository : IDraweeBankRepository
{
    private readonly List<DraweeBank> _store = new();
    private readonly List<ChallanRule> _rules = new();

    public InMemoryDraweeBankRepository() { }

    public InMemoryDraweeBankRepository(IEnumerable<DraweeBank> seed)
    {
        foreach (var b in seed) _store.Add(Clone(b));
    }

    public IReadOnlyList<DraweeBank> Snapshot() => _store.Select(Clone).ToList();
    public IReadOnlyList<ChallanRule> RuleSnapshot() => _rules.Select(Clone).ToList();
    public int Count => _store.Count;

    private static DraweeBranch Clone(DraweeBranch b) => new()
    {
        Id = b.Id,
        DraweeBankId = b.DraweeBankId,
        PlaceCode = b.PlaceCode,
        PlaceName = b.PlaceName,
        Address = b.Address,
        PostalCode = b.PostalCode,
        StartDate = b.StartDate,
        EndDate = b.EndDate
    };
    private static RestrictionDetail? Clone(RestrictionDetail? r) => r == null ? null : new()
    {
        Id = r.Id,
        DraweeBankId = r.DraweeBankId,
        ClearingDays = r.ClearingDays,
        StartDate = r.StartDate,
        EndDate = r.EndDate
    };
    private static ChallanRule Clone(ChallanRule c) => new()
    {
        Id = c.Id,
        DraweeBankId = c.DraweeBankId,
        RuleCode = c.RuleCode,
        FormatPattern = c.FormatPattern,
        ValidationExpression = c.ValidationExpression,
        RoutingTarget = c.RoutingTarget,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
    private DraweeBank Clone(DraweeBank x) => new()
    {
        Id = x.Id,
        BankCode = x.BankCode,
        BankName = x.BankName,
        IsActive = x.IsActive,
        Branches = x.Branches.Select(Clone).ToList(),
        Restriction = Clone(x.Restriction),
        // Challan rules are decoupled — stored in the separate _rules list, not on the bank.
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    // ---- IRepository<DraweeBank> ----
    public Task<DraweeBank?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }
    public Task<List<DraweeBank>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());
    public Task<List<DraweeBank>> FindAsync(Expression<Func<DraweeBank, bool>> p, CancellationToken ct = default)
        => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());
    public Task AddAsync(DraweeBank entity, CancellationToken ct = default)
    {
        _store.Add(Clone(entity));
        return Task.CompletedTask;
    }
    public Task UpdateAsync(DraweeBank entity, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(x => x.Id == entity.Id);
        if (idx >= 0) _store[idx] = Clone(entity);
        return Task.CompletedTask;
    }
    public Task DeleteAsync(DraweeBank entity, CancellationToken ct = default)
    {
        _store.RemoveAll(x => x.Id == entity.Id);
        return Task.CompletedTask;
    }
    public Task<int> CountAsync(Expression<Func<DraweeBank, bool>>? p = null, CancellationToken ct = default)
        => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));

    // ---- IDraweeBankRepository ----
    public Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<DraweeBank> query = _store;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.BankCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.BankName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        var all = query.ToList();
        var total = all.Count;
        var items = all
            .OrderBy(x => x.BankName, StringComparer.Ordinal)
            .ThenBy(x => x.BankCode, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(Clone).ToList();
        return Task.FromResult((items, total));
    }

    public Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default)
        => GetByIdAsync(id, ct);

    public Task<bool> ExistsByBankCodeAsync(string bankCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = bankCode.Trim();
        return Task.FromResult(_store.Any(x =>
            string.Equals(x.BankCode, c, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || x.Id != excludeId)));
    }

    public Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid bankId, CancellationToken ct = default)
        => Task.FromResult(_rules.Where(r => r.DraweeBankId == bankId).Select(Clone).ToList());
    public Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
    {
        var f = _rules.FirstOrDefault(r => r.Id == ruleId);
        return Task.FromResult(f is null ? null : Clone(f));
    }
    public Task<bool> ChallanRuleCodeExistsAsync(Guid bankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var code = ruleCode.Trim();
        return Task.FromResult(_rules.Any(r =>
            r.DraweeBankId == bankId &&
            string.Equals(r.RuleCode, code, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || r.Id != excludeId)));
    }
    public Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
    {
        _rules.Add(Clone(rule));
        return Task.CompletedTask;
    }
    public Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
    {
        var idx = _rules.FindIndex(r => r.Id == rule.Id);
        if (idx >= 0) _rules[idx] = Clone(rule);
        return Task.CompletedTask;
    }
}
