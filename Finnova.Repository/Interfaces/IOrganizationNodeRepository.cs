using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Repository interface for managing OrganizationNode entities, providing methods for searching, paging, and retrieving hierarchical data.
/// </summary>
public interface IOrganizationNodeRepository : IRepository<OrganizationNode>
{
    /// <summary>
    /// Searches for OrganizationNode entities based on a search term, with support for pagination. Returns a tuple containing the list of matching items and the total count of items.
    /// </summary>
    /// <param name="searchTerm">Search term (substring, case-insensitive)</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple containing the list of matching items and the total count</returns>
    Task<(List<OrganizationNode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Checks if an OrganizationNode with the specified code exists, optionally excluding a specific node by its ID. This is useful for ensuring unique codes when creating or updating nodes.
    /// </summary>
    /// <param name="code">The code to check for uniqueness</param>
    /// <param name="excludeId">The ID of the node to exclude from the check (if updating)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if a node with the specified code exists; otherwise, false</returns>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all direct child nodes of a specified parent node. This method is useful for building hierarchical structures and navigating the organization tree.
    /// </summary>
    /// <param name="parentId">The ID of the parent node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of direct child nodes</returns>
    Task<List<OrganizationNode>> GetByParentIdAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all descendant nodes of a specified node, including children, grandchildren, and further descendants. This method is useful for operations that require knowledge of the entire subtree under a specific node.
    /// </summary>
    /// <param name="nodeId">The ID of the node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of descendant nodes</returns>
    Task<List<OrganizationNode>> GetDescendantsAsync(Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a specified node has any child nodes. This method is useful for determining whether a node is a leaf in the hierarchy or if it has further subdivisions.
    /// </summary>
    /// <param name="nodeId">The ID of the node</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the node has children; otherwise, false</returns>
    Task<bool> HasChildrenAsync(Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all OrganizationNode entities in a hierarchical structure suitable for building a tree view. This method is useful for displaying the entire organization structure in a user interface.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all organization nodes in tree format</returns>
    Task<List<OrganizationNode>> GetAllForTreeAsync(CancellationToken ct = default);
    /// <summary>
    /// Atomically persists a re-parent: updates the moved node and every descendant whose Level
    /// was recomputed, and appends the accompanying audit entry, within a single transaction.
    /// Either every change is committed or none are (rolls back on any failure).
    /// </summary>
    Task ReparentAsync(
        OrganizationNode movedNode,
        IReadOnlyCollection<OrganizationNode> updatedDescendants,
        OrganizationNodeAuditEntry auditEntry,
        CancellationToken ct = default);
}
