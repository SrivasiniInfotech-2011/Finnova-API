using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class DocumentMapper
{
    public static DocumentResponse ToResponse(this Finnova.Models.Domain.Entities.Document d) => new(
        d.Id, d.DocCode, d.DocName, d.DocShortName, d.Description,
        d.DocCategory, d.ApplicableEntity,
        d.IsMandatory, d.HasExpiry, d.RequiresMasking, d.IsAadhaarDocument,
        d.ValidationEngine, d.FormatRegex, d.MaskPattern, d.MaxFileSizeMb,
        SplitMime(d.AllowedMimeTypes),
        d.Status.ToString(), d.MakerId, d.CheckerId, d.AuthorizedVersionNumber, d.RejectionReason,
        d.ApplicabilityRules.Select(r => new ApplicabilityRuleResponse(
            r.Id, r.Constitution, r.EntityRole, r.IsMandatory)).ToList(),
        d.CreatedAt, d.UpdatedAt);

    public static List<DocumentResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.Document> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static ActiveDocumentForConsumptionResponse ToConsumptionResponse(
        this Finnova.Models.Domain.Entities.Document d, bool resolvedMandatory) => new(
        d.Id, d.DocCode, d.DocName, resolvedMandatory, d.HasExpiry,
        d.ValidationEngine, d.FormatRegex, d.RequiresMasking, d.MaskPattern,
        d.MaxFileSizeMb, SplitMime(d.AllowedMimeTypes));

    public static DocumentAuditEntryResponse ToResponse(this DocumentAuditEntry e) => new(
        e.Id, e.DocumentId, e.Action.ToString(), e.OldValues, e.NewValues,
        e.Summary, e.Remarks, e.ChangedBy, e.ChangedAtUtc);

    private static IReadOnlyList<string> SplitMime(string csv) =>
        string.IsNullOrWhiteSpace(csv) ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}