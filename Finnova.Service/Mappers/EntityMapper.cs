using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Entity.Internal;

namespace Finnova.Service.Mappers;

public static class EntityMapper
{
    public static EntityResponse ToResponse(this EntityMaster x) => new(
        x.Id,
        x.Code,
        x.Name,
        x.EntityType,
        x.RegistrationIdentifier,
        x.ContactPerson,
        x.Email,
        x.Phone,
        x.AddressLine,
        EntityTypeAttributes.Deserialize(x.Attributes),   // empty dictionary when null
        x.IsActive,
        x.CreatedAt,
        x.UpdatedAt);

    public static List<EntityResponse> ToResponseList(this IEnumerable<EntityMaster> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static EntityAuditEntryResponse ToResponse(this EntityAuditEntry a) => new(
        a.Id, a.EntityId, a.Action.ToString(), a.OldValues, a.NewValues, a.Summary, a.ChangedBy, a.ChangedAtUtc);
}