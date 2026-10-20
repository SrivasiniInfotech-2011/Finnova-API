using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Document.Internal;

/// <summary>Compact JSON snapshots of a document's editable fields for audit entries (R8).</summary>
public static class DocumentAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(Finnova.Models.Domain.Entities.Document d) => JsonSerializer.Serialize(new
    {
        d.DocCode, d.DocName, d.DocShortName, d.Description,
        d.DocCategory, d.ApplicableEntity,
        d.IsMandatory, d.HasExpiry, d.RequiresMasking, d.IsAadhaarDocument,
        d.ValidationEngine, d.FormatRegex, d.MaskPattern, d.MaxFileSizeMb, d.AllowedMimeTypes,
        Status = d.Status.ToString(),
        d.AuthorizedVersionNumber,
        Rules = d.ApplicabilityRules
            .Select(r => new { r.Constitution, r.EntityRole, r.IsMandatory })
    }, Options);
}