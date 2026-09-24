using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface INationalityRepository : IRepository<Nationality>
{
    /// <summary>
    /// Search + page for the admin grid (R5). The term filters Code OR Name
    /// (substring, case-insensitive); a null/blank term applies no filter.
    /// Ordered by Name asc then Code asc as a deterministic tie-break (R5.10).
    /// Returns the page items and the total matching count.
    /// </summary>
    Task<(List<Nationality> Items, int Total)> GetPagedAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Case-insensitive, trimmed uniqueness check on Code (R2).
    /// excludeId supports self-exclusion / future rename scenarios.
    /// </summary>
    Task<bool> ExistsByCodeAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken ct = default);
}
