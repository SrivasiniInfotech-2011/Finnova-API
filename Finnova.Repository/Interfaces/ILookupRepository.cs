using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ILookupRepository : IRepository<LookupValue>
{
    /// <summary>
    /// Filtered + paged query for the admin grid (R1, R4). Any filter may be null.
    /// Ordered by DisplayOrder asc, then Id asc as a deterministic tie-break (R4.6).
    /// Returns the page items and the total matching count.
    /// </summary>
    Task<(List<LookupValue> Items, int Total)> GetPagedAsync(
        string? module,
        string? lookupType,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active values for a Module + Lookup Type, ordered by DisplayOrder asc then
    /// Value asc (R6.1, R6.2). Used by consuming-module dropdowns.
    /// </summary>
    Task<List<LookupValue>> GetActiveByModuleAndTypeAsync(
        string module, string lookupType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uniqueness check for Code within a Module + Lookup Type scope (R2.8, R5.3).
    /// excludeId lets update skip the row being edited.
    /// </summary>
    Task<bool> ExistsByCodeAsync(
        string module, string lookupType, string code,
        Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the given Module + Lookup Type scope is defined in the system (R1.7, R6.5).
    /// </summary>
    Task<bool> ScopeExistsAsync(
        string module, string lookupType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single lookup value by its Module + Lookup Type + Code identity (R6.1, R6.2).
    /// </summary>
    Task<LookupValue?> GetByModuleTypeCodeAsync(
        string module, string lookupType, string code, CancellationToken cancellationToken = default);
}

