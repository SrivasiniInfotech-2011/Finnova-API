using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>Hand-written in-memory IEntityRepository mirroring the EF repo semantics.</summary>
public sealed class InMemoryEntityRepository : IEntityRepository
{
    private readonly List<EntityMaster> _store = new();

    public InMemoryEntityRepository() { }
    public InMemoryEntityRepository(IEnumerable<EntityMaster> seed) { foreach (var e in seed) _store.Add(Clone(e)); }

    public IReadOnlyList<EntityMaster> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static EntityMaster Clone(EntityMaster x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        EntityType = x.EntityType,
        RegistrationIdentifier = x.RegistrationIdentifier,
        ContactPerson = x.ContactPerson,
        Email = x.Email,
        Phone = x.Phone,
        AddressLine = x.AddressLine,
        Attributes = x.Attributes,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    public Task<EntityMaster?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<EntityMaster>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<EntityMaster>> FindAsync(Expression<Func<EntityMaster, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(EntityMaster entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }

    public Task UpdateAsync(EntityMaster entity, CancellationToken ct = default)
    {
        var i = _store.FindIndex(x => x.Id == entity.Id);
        if (i >= 0) _store[i] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(EntityMaster entity, CancellationToken ct = default) { _store.RemoveAll(x => x.Id == entity.Id); return Task.CompletedTask; }

    public Task<int> CountAsync(Expression<Func<EntityMaster, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<(List<EntityMaster> Items, int Total)> GetPagedAsync(
        string? searchTerm, EntityType? entityType, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<EntityMaster> q = _store;

        if (entityType is not null)
            q = q.Where(x => x.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var t = searchTerm.Trim();
            q = q.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (x.RegistrationIdentifier != null && x.RegistrationIdentifier.Contains(t, StringComparison.OrdinalIgnoreCase)));
        }

        var all = q.ToList();
        var items = all
            .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task<bool> ExistsByCodeAsync(string code, EntityType entityType, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Task.FromResult(_store.Any(x =>
            x.EntityType == entityType
            && string.Equals(x.Code, c, StringComparison.OrdinalIgnoreCase)
            && (excludeId == null || x.Id != excludeId)));
    }
}