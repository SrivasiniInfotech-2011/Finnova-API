using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class NumberingSchemeMapper
{
    /// <summary>Maps a scheme entity to the admin-facing response, preserving every field (R4.9).</summary>
    public static NumberingSchemeResponse ToResponse(this NumberingScheme x) => new(
        x.Id,
        x.Code,
        x.Name,
        x.DocumentType,
        x.FormatTemplate,
        x.Prefix,
        x.Suffix,
        x.SeqStart,
        x.SeqIncrement,
        x.SeqPadding,
        x.ResetRule,
        x.Scope,
        x.ScopeId,
        x.CurrentValue,
        x.PeriodKey,
        x.IsActive,
        x.CreatedAt,
        x.UpdatedAt);

    public static List<NumberingSchemeResponse> ToResponseList(this IEnumerable<NumberingScheme> items)
        => items.Select(i => i.ToResponse()).ToList();

    /// <summary>Maps an audit entry to its response, projecting the Action enum to its name.</summary>
    public static NumberingSchemeAuditEntryResponse ToResponse(this NumberingSchemeAuditEntry a) => new(
        a.Id,
        a.SchemeId,
        a.Action.ToString(),
        a.OldValues,
        a.NewValues,
        a.Summary,
        a.ChangedBy,
        a.ChangedAtUtc);
}
