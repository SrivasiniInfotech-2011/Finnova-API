using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IDraweeBankAuditRepository : IRepository<DraweeBankAuditEntry>
{
    /// <summary>Audit entries for a drawee bank, ordered ChangedAtUtc desc then Id desc (R7.4).
    /// Missing id returns an empty list, never an error (R7.5).</summary>
    Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(Guid draweeBankId, CancellationToken ct = default);
}
