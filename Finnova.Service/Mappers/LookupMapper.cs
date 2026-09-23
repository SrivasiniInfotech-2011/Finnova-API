using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class LookupMapper
{
    /// <summary>
    /// Maps a single entity to the full admin-facing response, preserving the
    /// system-lock flag and all scalar fields (R3.5).
    /// </summary>
    public static LookupValueResponse ToResponse(this LookupValue x) => new(
        x.Id,
        x.Module,
        x.LookupType,
        x.Code,
        x.Value,
        x.DisplayOrder,
        x.IsActive,
        x.IsSystemLocked,
        x.CreatedAt,
        x.UpdatedAt
    );

    /// <summary>
    /// Maps a flat list of entities to a flat list of responses.
    /// </summary>
    public static List<LookupValueResponse> ToResponseList(this IEnumerable<LookupValue> items)
        => items.Select(i => i.ToResponse()).ToList();

    /// <summary>
    /// Maps a single entity to the slim dropdown projection (Code + display label) (R6.4).
    /// </summary>
    public static LookupDropdownItemResponse ToDropdownItem(this LookupValue x) => new(
        x.Code,
        x.Value
    );
}
