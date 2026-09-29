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

    /// <summary>Uniqueness check on the (DocumentType, Scope, ScopeId) combination (R2.3/2.4).</summary>
    Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Atomically issues the next number for a document type + scope (R5.1/5.2). Loads the matching
    /// scheme, applies period reset + increment, persists the new sequence state, and returns the
    /// scheme snapshot plus the sequence value used. Serialized per scheme via a transaction on
    /// relational providers so concurrent callers never collide. Returns null when no scheme matches
    /// (R5.6). Throws <see cref="Finnova.Models.Domain.Exceptions.NumberingSchemeInactiveException"/>
    /// when the scheme is inactive (R5.7) and
    /// <see cref="Finnova.Models.Domain.Exceptions.NumberSequenceExhaustedException"/> on overflow (R5.9).
    /// </summary>
    Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextAsync(
        string documentType, NumberScope scope, Guid? scopeId, DateTime utcNow, CancellationToken ct = default);
}
