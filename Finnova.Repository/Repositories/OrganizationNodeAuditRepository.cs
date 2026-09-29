using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

/// <summary>
/// Repository class for managing OrganizationNodeAuditEntry entities. Provides methods for retrieving audit entries related to organization nodes, including fetching entries by node ID with ordering and pagination support.
/// </summary>
/// <param name="finnovaDbContext">An instance of type <see cref="FinnovaDbContext"/></param>
public class OrganizationNodeAuditRepository(FinnovaDbContext finnovaDbContext) : RepositoryBase<OrganizationNodeAuditEntry>(finnovaDbContext), IOrganizationNodeAuditRepository
{
    /// <summary>
    /// Gets all audit entries for a given node ID, ordered by ChangedAtUtc descending and then by Id descending.
    /// </summary>
    /// <param name="nodeId">The ID of the node for which to retrieve audit entries</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of audit entries</returns>
    public async Task<List<OrganizationNodeAuditEntry>> GetByNodeIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
                        .Where(x => x.NodeId == nodeId)
                        .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R6.4
                        .ToListAsync(ct);   // empty list when none match (R6.5) 
    }
}
