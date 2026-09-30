using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Court.Internal;

/// <summary>Builds compact JSON snapshots of a court's editable fields for audit entries (R5).</summary>
public static class CourtAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(Finnova.Models.Domain.Entities.Court c) => JsonSerializer.Serialize(new
    {
        c.Name,
        CourtType = c.CourtType.ToString(),
        c.Jurisdiction,
        c.Location,
        c.IsActive,
    }, Options);
}