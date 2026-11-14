using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

/// <summary>Pure KYC derivation (Properties 1 and 2). Mirrored in the Python worker.</summary>
public static class KycStatusDeriver
{
    public static (KycStatus Status, bool Flagged) Derive(
        KycCheckResult panValidation, KycCheckResult panNameMatch,
        KycCheckResult aadhaarValidation, KycCheckResult aadhaarAddressMatch)
        => Derive(new[] { panValidation, panNameMatch, aadhaarValidation, aadhaarAddressMatch });

    public static (KycStatus Status, bool Flagged) Derive(IReadOnlyCollection<KycCheckResult> results)
    {
        KycStatus status =
            results.Count == 4 && results.All(r => r == KycCheckResult.Passed) ? KycStatus.Passed
            : results.Any(r => r == KycCheckResult.Failed) ? KycStatus.Failed
            : results.Any(r => r == KycCheckResult.Unavailable) ? KycStatus.Unavailable
            : KycStatus.PartiallyVerified;
        return (status, status != KycStatus.Passed);
    }

    /// <summary>Bypass mode: status Bypassed, all four checks Bypassed, flag true (R6.2).</summary>
    public static (KycStatus Status, bool Flagged) ForBypass() => (KycStatus.Bypassed, true);
}