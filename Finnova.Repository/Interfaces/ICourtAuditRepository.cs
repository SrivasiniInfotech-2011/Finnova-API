using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICourtAuditRepository : IRepository<CourtAuditEntry>
{
    /// <summary>
    /// Audit entries for a court, ordered ChangedAtUtc desc then Id desc (R5.5). Missing id yields
    /// an empty list (R5.6).
    /// </summary>
    Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default);
}