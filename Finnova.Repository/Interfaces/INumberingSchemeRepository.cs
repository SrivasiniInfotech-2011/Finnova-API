using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Repository for numbering schemes: search/paging, uniqueness checks, and atomic issuance.
/// </summary>
public interface INumberingSchemeRepository : IRepository<NumberingScheme>
{
    /// <summary>
    /// Paged search over Code / Name / DocumentType (substring, case-insensitive; blank term = no
    /// filter). Ordered by Name asc, then Code asc (R4.7). Returns the page and the total count.
    /// </summary>
    Task<(List<NumberingScheme> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Case-insensitive, trimmed uniqueness check on Code (R2.1/2.2).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    /// <summary>Uniqueness check on (DocumentType, Scope, ScopeId, LineOfBusinessId) (R2.3/2.4, extended by R16.9).</summary>
    Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? lineOfBusinessId = null);

    /// <summary>All ACTIVE schemes of a document type (input of the pure SchemeResolver).</summary>
    Task<List<NumberingScheme>> GetActiveByDocumentTypeAsync(string documentType, CancellationToken ct = default);

    /// <summary>Atomically issues the next number for a specific scheme (same semantics as IssueNextAsync). Null when the scheme does not exist.</summary>
    Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextByIdAsync(Guid schemeId, DateTime utcNow, CancellationToken ct = default);
}
