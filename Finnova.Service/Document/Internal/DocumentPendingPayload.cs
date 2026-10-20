using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Document.Internal;

/// <summary>
/// Serializes/deserializes the editable payload held in <see cref="Document.PendingValuesJson"/>
/// and applies it onto the entity's live columns on authorize. One payload can carry any subset
/// of: attribute edit (R3), applicability matrix (R4), validation/masking config (R9). Null
/// members mean "unchanged" for that slice.
/// </summary>
public static class DocumentPendingPayload
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public sealed record RulePayload(string Constitution, string EntityRole, bool IsMandatory);

    public sealed record Payload(
        // attribute edit slice (R3)
        string? DocName = null,
        string? DocShortName = null,
        string? Description = null,
        string? DocCategory = null,
        string? ApplicableEntity = null,
        bool? IsMandatory = null,
        bool? HasExpiry = null,
        // applicability slice (R4)
        IReadOnlyList<RulePayload>? Rules = null,
        // validation/masking slice (R9)
        string? ValidationEngine = null,
        string? FormatRegex = null,
        bool? RequiresMasking = null,
        string? MaskPattern = null,
        int? MaxFileSizeMb = null,
        IReadOnlyList<string>? AllowedMimeTypes = null
    );

    public static string Serialize(Payload payload) => JsonSerializer.Serialize(payload, Options);

    public static Payload Deserialize(string json) =>
        JsonSerializer.Deserialize<Payload>(json, Options) ?? new Payload();

    // --- builders used by the command handlers ---

    public static string ForAttributes(
        string docName, string? docShortName, string? description,
        string docCategory, string applicableEntity, bool isMandatory, bool hasExpiry) =>
        Serialize(new Payload(
            DocName: docName, DocShortName: docShortName, Description: description,
            DocCategory: docCategory, ApplicableEntity: applicableEntity,
            IsMandatory: isMandatory, HasExpiry: hasExpiry));

    public static string ForApplicability(IEnumerable<DocumentApplicabilityRule> rules) =>
        Serialize(new Payload(Rules: rules
            .Select(r => new RulePayload(r.Constitution, r.EntityRole, r.IsMandatory)).ToList()));

    public static string ForValidationConfig(
        string validationEngine, string? formatRegex, bool requiresMasking,
        string? maskPattern, int maxFileSizeMb, IReadOnlyList<string> allowedMimeTypes) =>
        Serialize(new Payload(
            ValidationEngine: validationEngine, FormatRegex: formatRegex,
            RequiresMasking: requiresMasking, MaskPattern: maskPattern,
            MaxFileSizeMb: maxFileSizeMb, AllowedMimeTypes: allowedMimeTypes));

    /// <summary>Copies a serialized pending payload onto the entity's live columns (authorize).</summary>
    public static void ApplyTo(Finnova.Models.Domain.Entities.Document entity, string json)
    {
        var p = Deserialize(json);

        if (p.DocName is not null) entity.DocName = p.DocName;
        if (p.DocShortName is not null) entity.DocShortName = p.DocShortName;
        if (p.Description is not null) entity.Description = p.Description;
        if (p.DocCategory is not null) entity.DocCategory = p.DocCategory;
        if (p.ApplicableEntity is not null) entity.ApplicableEntity = p.ApplicableEntity;
        if (p.IsMandatory is not null) entity.IsMandatory = p.IsMandatory.Value;
        if (p.HasExpiry is not null) entity.HasExpiry = p.HasExpiry.Value;

        if (p.ValidationEngine is not null) entity.ValidationEngine = p.ValidationEngine;
        if (p.FormatRegex is not null) entity.FormatRegex = p.FormatRegex;
        if (p.RequiresMasking is not null) entity.RequiresMasking = p.RequiresMasking.Value;
        if (p.MaskPattern is not null) entity.MaskPattern = p.MaskPattern;
        if (p.MaxFileSizeMb is not null) entity.MaxFileSizeMb = p.MaxFileSizeMb.Value;
        if (p.AllowedMimeTypes is not null)
            entity.AllowedMimeTypes = string.Join(',', p.AllowedMimeTypes);

        if (p.Rules is not null)
        {
            entity.ApplicabilityRules.Clear();
            foreach (var r in p.Rules)
                entity.ApplicabilityRules.Add(new DocumentApplicabilityRule
                {
                    DocumentId = entity.Id,
                    Constitution = r.Constitution,
                    EntityRole = r.EntityRole,
                    IsMandatory = r.IsMandatory
                });
        }
    }
}