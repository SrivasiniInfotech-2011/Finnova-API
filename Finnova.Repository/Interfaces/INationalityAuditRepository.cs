using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface INationalityAuditRepository : IRepository<NationalityAuditEntry>
{
    /// <summary>
    /// Audit entries for a nationality, ordered by ChangedAtUtc desc then Id desc (R4.4).
    /// A missing id returns an empty list, never an error (R4.5).
    /// </summary>
    Task<List<NationalityAuditEntry>> GetByNationalityIdAsync(
        Guid nationalityId,
        CancellationToken ct = default);
}
