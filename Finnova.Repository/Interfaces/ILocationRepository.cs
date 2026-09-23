using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ILocationRepository : IRepository<Location>
{
    /// <summary>
    /// Returns all locations as a flat list (no navigation graph loaded).
    /// Callers build the tree in memory.
    /// </summary>
    Task<List<Location>> GetAllFlatAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns locations at a specific hierarchy level (for parent selection).
    /// </summary>
    Task<List<Location>> GetByLevelAsync(int level, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the direct children of a location.
    /// </summary>
    Task<List<Location>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);
}
