using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public interface IEntityRepository : IRepository<EntityMaster>
{
    /// <summary>
    /// Paged search over Code / Name / RegistrationIdentifier (substring, case-insensitive; blank
    /// term = no filter), with an optional EntityType equality filter. Ordered by Name asc, then
    /// Code asc (R3.10). Returns the page and the total count.
    /// </summary>
    Task<(List<EntityMaster> Items, int Total)> GetPagedAsync(
        string? searchTerm, EntityType? entityType, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Case-insensitive, trimmed uniqueness check on Code WITHIN an EntityType (R2.1/2.2).
    /// excludeId supports a future rename-code path.
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code, EntityType entityType, Guid? excludeId = null, CancellationToken ct = default);
}