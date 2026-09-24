using Finnova.Models.Contracts.Nationalities;
using Finnova.Models.Domain.Entities;

// Alias the entity so it is unambiguous against the Finnova.Service.Nationality
// namespace (which otherwise shadows the bare type name in this scope).
using Nationality = Finnova.Models.Domain.Entities.Nationality;

namespace Finnova.Service.Mappers;

public static class NationalityMapper
{
    /// <summary>
    /// Maps a single nationality entity to the admin-facing response,
    /// preserving every scalar field (R1.1).
    /// </summary>
    public static NationalityResponse ToResponse(this Finnova.Models.Domain.Entities.Nationality x) => new(
        x.Id,
        x.Code,
        x.Name,
        x.IsActive,
        x.CreatedAt,
        x.UpdatedAt
    );

    /// <summary>
    /// Maps a flat list of nationality entities to a flat list of responses.
    /// </summary>
    public static List<NationalityResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.Nationality> items)
        => items.Select(i => i.ToResponse()).ToList();

    /// <summary>
    /// Maps a single audit entry to its response, preserving every field and
    /// projecting the Action enum to its name string ("Create" | "Update") (R4.1, R4.2, R4.4).
    /// </summary>
    public static NationalityAuditEntryResponse ToResponse(this NationalityAuditEntry a) => new(
        a.Id,
        a.NationalityId,
        a.Action.ToString(),
        a.OldName,
        a.NewName,
        a.ChangedBy,
        a.ChangedAtUtc
    );
}
