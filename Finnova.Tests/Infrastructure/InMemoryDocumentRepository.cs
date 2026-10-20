using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _store = new();

    public IReadOnlyList<Document> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;
    public void Seed(Document d) => _store.Add(Clone(d));

    private static Document Clone(Document d) => new()
    {
        Id = d.Id, DocCode = d.DocCode, DocName = d.DocName, DocShortName = d.DocShortName,
        Description = d.Description, DocCategory = d.DocCategory, ApplicableEntity = d.ApplicableEntity,
        IsMandatory = d.IsMandatory, HasExpiry = d.HasExpiry, RequiresMasking = d.RequiresMasking,
        IsAadhaarDocument = d.IsAadhaarDocument, ValidationEngine = d.ValidationEngine,
        FormatRegex = d.FormatRegex, MaskPattern = d.MaskPattern, MaxFileSizeMb = d.MaxFileSizeMb,
        AllowedMimeTypes = d.AllowedMimeTypes, Status = d.Status, MakerId = d.MakerId,
        CheckerId = d.CheckerId, AuthorizedVersionNumber = d.AuthorizedVersionNumber,
        RejectionReason = d.RejectionReason, PendingValuesJson = d.PendingValuesJson,
        CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
        ApplicabilityRules = d.ApplicabilityRules.Select(r => new DocumentApplicabilityRule
        {
            Id = r.Id, DocumentId = r.DocumentId, Constitution = r.Constitution,
            EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
        }).ToList()
    };

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var f = _store.FirstOrDefault(x => x.Id == id); return Task.FromResult(f is null ? null : Clone(f)); }

    public Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default)
    { var f = _store.FirstOrDefault(x => x.Id == id); return Task.FromResult(f is null ? null : Clone(f)); }

    public Task<List<Document>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(Document entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }

    public Task UpdateAsync(Document entity, CancellationToken ct = default)
    { _store.RemoveAll(x => x.Id == entity.Id); _store.Add(Clone(entity)); return Task.CompletedTask; }

    public Task DeleteAsync(Document entity, CancellationToken ct = default)
    { _store.RemoveAll(x => x.Id == entity.Id); return Task.CompletedTask; }

    public Task<int> CountAsync(Expression<Func<Document, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<(List<Document> Items, int Total)> GetPagedAsync(
        string? search, string? docCategory, DocumentStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<Document> q = _store;
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.DocCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                          || x.DocName.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(docCategory)) q = q.Where(x => x.DocCategory == docCategory);
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        var all = q.ToList();
        var items = all.OrderBy(x => x.DocCode).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task<List<Document>> GetActiveForConsumptionAsync(string constitution, string entityRole, CancellationToken ct = default)
    {
        var items = _store.Where(x => (x.Status == DocumentStatus.Active
                    || x.Status == DocumentStatus.PendingModification)
                && x.ApplicabilityRules.Any(r => r.Constitution == constitution && r.EntityRole == entityRole))
            .OrderBy(x => x.DocCode).ThenBy(x => x.Id).Select(Clone).ToList();
        return Task.FromResult(items);
    }

    public Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = docCode.Trim();
        return Task.FromResult(_store.Any(x =>
            string.Equals(x.DocCode.Trim(), c, StringComparison.OrdinalIgnoreCase)
            && (excludeId == null || x.Id != excludeId)));
    }
}