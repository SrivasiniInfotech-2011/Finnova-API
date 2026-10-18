using Finnova.Models.Domain.Enums;

namespace Finnova.Service.GL.Internal;

/// <summary>Immutable snapshot of a GL account's validation-relevant state for one line.</summary>
public record GLValidationSnapshot(
    Guid GLAccountId,
    Guid CompanyId,
    GLStatus Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsPostable,
    bool AllowManualJV,
    IReadOnlyCollection<Guid> ApplicableBranchIds,
    bool CostCentreMandatory,
    bool ProfitCentreMandatory,
    Guid CurrencyId,
    GLType GLType);

/// <summary>Voucher-level context shared by every line.</summary>
public record JvVoucherContext(Guid CompanyId, DateTime ValueDate, Guid BranchId, JvEntryMode Mode);

/// <summary>One line's posting details.</summary>
public record JvLine(
    int LineNumber,
    Guid GLAccountId,
    Guid CurrencyId,
    Guid? CostCentreId,
    Guid? ProfitCentreId,
    string? SubLedgerReference);

/// <summary>
/// Pure per-line validation (R6.2-R6.13). Same inputs always yield the same ordered reason set;
/// a line is Accepted iff the reason set is empty. Reasons are emitted in ascending
/// criterion-number order (R6.12). <paramref name="snapshot"/> is null when the GL could not be
/// resolved (R6.13) — in that case criteria 2..10 are skipped.
/// </summary>
public static class JvLineValidator
{
    public static IReadOnlyList<string> Evaluate(
        JvLine line, GLValidationSnapshot? snapshot, JvVoucherContext ctx)
    {
        var reasons = new List<string>();

        // R6.13 — unresolved GL: single reason, skip the rest.
        if (snapshot is null)
        {
            reasons.Add("GL does not exist.");
            return reasons;
        }

        // R6.2 — company mismatch
        if (snapshot.CompanyId != ctx.CompanyId)
            reasons.Add("GL does not belong to the voucher company.");

        // R6.3 — not active
        if (snapshot.Status != GLStatus.Active)
            reasons.Add("GL is not active.");

        // R6.4 — value date outside effective range (inclusive)
        if (ctx.ValueDate < snapshot.EffectiveFrom ||
            (snapshot.EffectiveTo is not null && ctx.ValueDate > snapshot.EffectiveTo))
            reasons.Add("Voucher value date is outside the GL effective date range.");

        // R6.5 — header GL not postable
        if (!snapshot.IsPostable)
            reasons.Add("Header GL accounts cannot be posted.");

        // R6.6 — manual JV not allowed
        if (ctx.Mode == JvEntryMode.Manual && !snapshot.AllowManualJV)
            reasons.Add("Manual journal vouchers are not allowed on the GL.");

        // R6.7 — branch not applicable
        if (!snapshot.ApplicableBranchIds.Contains(ctx.BranchId))
            reasons.Add("Voucher branch is not applicable to the GL.");

        // R6.8 — missing mandatory dimension(s)
        if (snapshot.CostCentreMandatory && line.CostCentreId is null)
            reasons.Add("Cost Centre is mandatory for the GL.");
        if (snapshot.ProfitCentreMandatory && line.ProfitCentreId is null)
            reasons.Add("Profit Centre is mandatory for the GL.");

        // R6.9 — currency mismatch
        if (line.CurrencyId != snapshot.CurrencyId)
            reasons.Add("Line currency does not match the GL currency.");

        // R6.10 — system entry, control account, missing sub-ledger reference
        if (ctx.Mode == JvEntryMode.System && snapshot.GLType == GLType.Control
            && string.IsNullOrWhiteSpace(line.SubLedgerReference))
            reasons.Add("A sub-ledger reference is mandatory for control accounts.");

        return reasons;   // already in ascending criterion order by construction (R6.12)
    }
}
