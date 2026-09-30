using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Entity.Internal;

/// <summary>Builds compact JSON snapshots of an entity's editable fields for audit entries (R5).</summary>
public static class EntityAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(EntityMaster e) => JsonSerializer.Serialize(new
    {
        e.Name,
        e.RegistrationIdentifier,
        e.ContactPerson,
        e.Email,
        e.Phone,
        e.AddressLine,
        Attributes = e.Attributes,   // stored JSON bag (or null)
        e.IsActive,
    }, Options);
}