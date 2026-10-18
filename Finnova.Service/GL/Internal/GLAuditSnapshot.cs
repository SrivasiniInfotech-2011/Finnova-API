using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.GL.Internal;

/// <summary>Compact JSON snapshots of a GL's editable fields for audit entries (R10). Mirrors CourtAuditSnapshot.</summary>
public static class GLAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(GLAccount g) => JsonSerializer.Serialize(new
    {
        g.GLCode,
        g.Name,
        Level = g.Level.ToString(),
        g.ParentGLId,
        AccountNature = g.AccountNature.ToString(),
        NormalBalance = g.NormalBalance.ToString(),
        GLType = g.GLType.ToString(),
        g.SubLedgerType,
        g.TaxType,
        g.IsPostable,
        g.CurrencyId,
        g.AllowManualJV,
        g.CostCentreMandatory,
        g.ProfitCentreMandatory,
        g.ReportingLine,
        g.EffectiveFrom,
        g.EffectiveTo,
        Status = g.Status.ToString(),
        g.AuthorizedVersionNumber
    }, Options);
}
