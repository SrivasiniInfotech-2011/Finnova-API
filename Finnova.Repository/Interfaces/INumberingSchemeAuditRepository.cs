using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface INumberingSchemeAuditRepository : IRepository<NumberingSchemeAuditEntry>
{
    /// <summary>
    /// Audit entries for a scheme, ordered ChangedAtUtc desc then Id desc (R6.6). Missing id yields
    /// an empty list (R6.7).
    /// </summary>
    Task<List<NumberingSchemeAuditEntry>> GetBySchemeIdAsync(Guid schemeId, CancellationToken ct = default);
}
