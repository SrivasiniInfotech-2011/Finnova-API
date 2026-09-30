using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IEntityAuditRepository : IRepository<EntityAuditEntry>
{
    /// <summary>
    /// Audit entries for an entity, ordered ChangedAtUtc desc then Id desc (R5.5). Missing id
    /// yields an empty list (R5.6).
    /// </summary>
    Task<List<EntityAuditEntry>> GetByEntityIdAsync(Guid entityId, CancellationToken ct = default);
}