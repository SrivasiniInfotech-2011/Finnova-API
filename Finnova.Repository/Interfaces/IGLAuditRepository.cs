using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IGLAuditRepository : IRepository<GLAuditEntry>
{
    /// <summary>Entries for a GL, ordered ChangedAtUtc desc then Id desc (R10.3). Missing id yields [] (R10.5).</summary>
    Task<List<GLAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken ct = default);

    /// <summary>Count of persisted entries for a GL (supports the audit-exactly-once property).</summary>
    Task<int> CountForGLAccountAsync(Guid glAccountId, CancellationToken ct = default);
}
