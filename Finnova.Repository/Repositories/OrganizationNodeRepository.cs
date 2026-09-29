using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

/// <summary>
/// Repository class for managing OrganizationNode entities. Provides methods for searching, retrieving, and checking the existence of organization nodes, as well as retrieving hierarchical structures and descendants.
/// </summary>
/// <param name="finnovaDbContext">An instance of type <see cref="FinnovaDbContext"/></param>
public class OrganizationNodeRepository(FinnovaDbContext finnovaDbContext) : RepositoryBase<OrganizationNode>(finnovaDbContext), IOrganizationNodeRepository
{
    /// <summary>
    /// Searches for OrganizationNode entities based on a search term, with support for pagination. Returns a tuple containing the list of matching items and the total count of items.
    /// </summary>
    /// <param name="searchTerm">Search term (substring, case-insensitive)</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple containing the list of matching items and the total count</returns>
    public async Task<(List<OrganizationNode> Items, int Total)> GetPagedAsync(
    string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // EF Core translates Contains to SQL LIKE; default CI collation makes it case-insensitive (R5.1).
            query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
        }
        var total = await query.CountAsync(ct);                            // total before paging (R5.3)
        var items = await query
            .OrderBy(x => x.Level).ThenBy(x => x.Name).ThenBy(x => x.Code)  // deterministic order (R5.9)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>
    /// Checks if an OrganizationNode with the specified code exists, optionally excluding a specific node by its ID. This is useful for ensuring unique codes when creating or updating nodes.
    /// </summary>
    /// <param name="code">The code to check for uniqueness</param>
    /// <param name="excludeId">The ID of the node to exclude from the check (if updating)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if a node with the specified code exists; otherwise, false</returns>
    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.Code == c && (excludeId == null || x.Id != excludeId), ct);   // CI via collation (R2.1)
    }

    /// <summary>
    /// Retrieves all direct child nodes of a specified parent node. This method is useful for building hierarchical structures and navigating the organization tree.
    /// </summary>
    /// <param name="parentId">The ID of the parent node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of direct child nodes</returns>
    public async Task<List<OrganizationNode>> GetByParentIdAsync(Guid parentId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Name).ThenBy(x => x.Code)   // R5.10
            .ToListAsync(ct);

    /// <summary>
    /// Checks if a specified node has any child nodes. This method is useful for determining whether a node is a leaf in the hierarchy or if it has further subdivisions.
    /// </summary>
    /// <param name="nodeId">The ID of the node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the node has children; otherwise, false</returns>
    public async Task<bool> HasChildrenAsync(Guid nodeId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking().AnyAsync(x => x.ParentId == nodeId, ct);   // R8.2

    /// <summary>
    /// Retrieves all descendant nodes of a specified node, including children, grandchildren, and further descendants. This method is useful for operations that require knowledge of the entire subtree under a specific node.
    /// </summary>
    /// <param name="nodeId">The ID of the node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of descendant nodes</returns>
    public async Task<List<OrganizationNode>> GetDescendantsAsync(Guid nodeId, CancellationToken ct = default)
    {
        var all = await DbSet.AsNoTracking().ToListAsync(ct);
        var byParent = all.ToLookup(x => x.ParentId);
        var result = new List<OrganizationNode>();
        var frontier = new Queue<Guid>(byParent[nodeId].Select(c => c.Id));
        var seen = new HashSet<Guid>();
        while (frontier.Count > 0)
        {
            var id = frontier.Dequeue();
            if (!seen.Add(id)) continue;                 // guards against pre-existing bad data
            var node = all.First(x => x.Id == id);
            result.Add(node);
            foreach (var child in byParent[id]) frontier.Enqueue(child.Id);
        }
        return result;                                    // excludes nodeId itself (R3.2, R3.4)
    }

    /// <summary>
    /// Retrieves all OrganizationNode entities in a hierarchical structure suitable for building a tree view. This method is useful for displaying the entire organization structure in a user interface.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all organization nodes in tree format</returns>
    public async Task<List<OrganizationNode>> GetAllForTreeAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(ct);
    }
    /// <summary>
    /// Atomically persists a re-parent (moved node + recomputed descendants + audit entry).
    /// Wraps the writes in an explicit transaction on relational providers; the EF Core InMemory
    /// provider does not support transactions, so it falls back to a single SaveChanges.
    /// </summary>
    public async Task ReparentAsync(
        OrganizationNode movedNode,
        IReadOnlyCollection<OrganizationNode> updatedDescendants,
        OrganizationNodeAuditEntry auditEntry,
        CancellationToken ct = default)
    {
        DbSet.Update(movedNode);
        foreach (var descendant in updatedDescendants)
            DbSet.Update(descendant);
        Context.Set<OrganizationNodeAuditEntry>().Add(auditEntry);

        if (Context.Database.IsRelational())
        {
            await using var tx = await Context.Database.BeginTransactionAsync(ct);
            try
            {
                await Context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        else
        {
            await Context.SaveChangesAsync(ct);
        }
    }
}
