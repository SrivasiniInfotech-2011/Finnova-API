using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public interface IDocumentRepository : IRepository<Document>
{
    /// <summary>Loads a document with its applicability rules (tracked for updates).</summary>
    Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Paged admin grid (R10). search = substring on DocCode/DocName (CI); category/status
    /// optional AND filters. Ordered by DocCode asc then Id asc. Returns (items, total).
    /// </summary>
    Task<(List<Document> Items, int Total)> GetPagedAsync(
        string? search, string? docCategory, DocumentStatus? status,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Active documents whose applicability matrix includes (constitution, entityRole) (R11.1).
    /// </summary>
    Task<List<Document>> GetActiveForConsumptionAsync(
        string constitution, string entityRole, CancellationToken ct = default);

    /// <summary>Case-insensitive, trimmed DocCode uniqueness check (R2). excludeId skips self.</summary>
    Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default);
}