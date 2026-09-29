using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Numbering;

/// <summary>
/// Computes the deterministic period key for a reset rule at a UTC instant (FINNOVA-7 R5.3). Two
/// issuances fall in the same period iff they produce the same key; when the stored key differs
/// from the current key, the sequence resets to its start value. Pure; lives in Models so both the
/// repository (issuance) and the service (formatting) can use it.
/// </summary>
public static class PeriodKey
{
    public static string For(NumberResetRule rule, DateTime utcNow) => rule switch
    {
        NumberResetRule.Never => "ALL",
        NumberResetRule.Yearly => utcNow.ToString("yyyy"),
        NumberResetRule.Monthly => utcNow.ToString("yyyyMM"),
        NumberResetRule.Daily => utcNow.ToString("yyyyMMdd"),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unsupported reset rule.")
    };
}
