using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Kyc;

public static class KycResultMapper
{
    public static KycCheckResult FromValidation(ValidationStatus? status) => status switch
    {
        ValidationStatus.Success => KycCheckResult.Passed,
        ValidationStatus.Failure => KycCheckResult.Failed,
        ValidationStatus.Unavailable => KycCheckResult.Unavailable,
        _ => KycCheckResult.NotChecked
    };

    public static KycCheckResult FromComparison(ComparisonOutcome? outcome) => outcome switch
    {
        ComparisonOutcome.Match => KycCheckResult.Passed,
        ComparisonOutcome.NonMatch => KycCheckResult.Failed,
        _ => KycCheckResult.NotChecked   // NoUsableMatch or not run
    };
}