using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IDocumentAuditRepository
{
    Task AddAsync(DocumentAuditEntry entry, CancellationToken ct = default);

    /// <summary>Entries for a document, newest-first with id desc tie-break (R8.4). Empty for
    /// an unknown id (R8.6).</summary>
    Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}