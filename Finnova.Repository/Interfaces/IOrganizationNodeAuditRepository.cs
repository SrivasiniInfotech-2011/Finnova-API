using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IOrganizationNodeAuditRepository : IRepository<OrganizationNodeAuditEntry>
{

    /// <summary>
    /// Gets all audit entries for a specific organization node by its ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of audit entries</returns>
    Task<List<OrganizationNodeAuditEntry>> GetByNodeIdAsync(Guid nodeId, CancellationToken ct = default);
}
