using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IClassCodeRepository : IRepository<ClassCode>
{
    /// <summary>Paged list, CI substring search over Code or Description (R2.7, R2.8),
    /// ordered by Code asc then Id asc (R2.11). Returns items + total (R2.9).</summary>
    Task<(List<ClassCode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Trimmed, case-insensitive Code uniqueness within this master (R1.2, R2.3).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Active code list for dropdowns / the class-code filter panel (R11.3, R12).</summary>
    Task<List<ClassCode>> GetActiveAsync(CancellationToken ct = default);
}
