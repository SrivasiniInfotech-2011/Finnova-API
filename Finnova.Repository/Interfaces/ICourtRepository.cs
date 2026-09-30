using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICourtRepository : IRepository<Court>
{
    /// <summary>
    /// Paged search over Code / Name / Jurisdiction (substring, case-insensitive; blank term = no
    /// filter). Ordered by Name asc, then Code asc (R3.7). Returns the page and the total count.
    /// </summary>
    Task<(List<Court> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Case-insensitive, trimmed uniqueness check on Code (R2.1/2.2).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}