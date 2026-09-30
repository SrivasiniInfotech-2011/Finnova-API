using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class CourtMapper
{
    public static CourtResponse ToResponse(this Finnova.Models.Domain.Entities.Court x) => new(
        x.Id, x.Code, x.Name, x.CourtType, x.Jurisdiction, x.Location, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static List<CourtResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.Court> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static CourtAuditEntryResponse ToResponse(this Finnova.Models.Domain.Entities.CourtAuditEntry a) => new(
        a.Id, a.CourtId, a.Action.ToString(), a.OldValues, a.NewValues, a.Summary, a.ChangedBy, a.ChangedAtUtc);
}