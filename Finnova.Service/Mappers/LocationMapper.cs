using Finnova.Models.Contracts.Locations;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class LocationMapper
{
    /// <summary>
    /// Maps a single entity to a response with an empty children list.
    /// </summary>
    public static LocationResponse ToResponse(this Location location) => new(
        location.Id,
        location.Code,
        location.Name,
        location.Level,
        location.ParentId,
        location.Description,
        location.IsActive,
        location.Latitude,
        location.Longitude,
        location.CreatedAt,
        location.UpdatedAt,
        new List<LocationResponse>()
    );

    /// <summary>
    /// Maps a flat list of entities to a flat list of responses (no nesting).
    /// </summary>
    public static List<LocationResponse> ToResponseList(this IEnumerable<Location> locations)
        => locations.Select(l => l.ToResponse()).ToList();

    /// <summary>
    /// Builds a nested tree of LocationResponse from a flat list of entities.
    /// Roots are nodes with a null ParentId. Children are ordered by name.
    /// </summary>
    public static List<LocationResponse> ToTree(this IEnumerable<Location> locations)
    {
        var flat = locations.ToList();

        // Create response nodes keyed by id.
        var nodes = flat.ToDictionary(l => l.Id, l => l.ToResponse());

        var roots = new List<LocationResponse>();
        foreach (var entity in flat)
        {
            var node = nodes[entity.Id];
            if (entity.ParentId is Guid pid && nodes.TryGetValue(pid, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
