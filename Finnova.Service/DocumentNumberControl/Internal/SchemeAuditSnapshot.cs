using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.DocumentNumberControl.Internal;

/// <summary>
/// Builds compact JSON snapshots of a scheme's editable configuration for audit entries (R6). Only
/// the operator-editable configuration is captured; the live sequence state (CurrentValue/PeriodKey)
/// is intentionally excluded because issuance is not audited (R6.4).
/// </summary>
public static class SchemeAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(NumberingScheme s) => JsonSerializer.Serialize(new
    {
        s.Name,
        s.FormatTemplate,
        s.Prefix,
        s.Suffix,
        s.SeqIncrement,
        s.SeqPadding,
        ResetRule = s.ResetRule.ToString(),
        s.IsActive,
    }, Options);
}
