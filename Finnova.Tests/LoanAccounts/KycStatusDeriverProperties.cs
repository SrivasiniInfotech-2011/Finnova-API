using Finnova.Models.Domain.Enums;
using Finnova.Service.LoanAccounts.Rules;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class KycStatusDeriverProperties
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 1: KYC status derivation
    public bool StatusFollowsPrecedence(KycCheckResult a, KycCheckResult b, KycCheckResult c, KycCheckResult d)
    {
        var all = new[] { a, b, c, d };
        var (status, _) = KycStatusDeriver.Derive(a, b, c, d);
        var expected = all.All(x => x == KycCheckResult.Passed) ? KycStatus.Passed
            : all.Any(x => x == KycCheckResult.Failed) ? KycStatus.Failed
            : all.Any(x => x == KycCheckResult.Unavailable) ? KycStatus.Unavailable
            : KycStatus.PartiallyVerified;
        return status == expected && (status == KycStatus.Passed) == all.All(x => x == KycCheckResult.Passed);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 2: KYC flag equivalence
    public bool FlagIsFalseExactlyWhenPassed(KycCheckResult a, KycCheckResult b, KycCheckResult c, KycCheckResult d)
    {
        var (status, flagged) = KycStatusDeriver.Derive(a, b, c, d);
        return flagged == (status != KycStatus.Passed);
    }

    [Fact] // Property 2 (bypass part)
    public void BypassIsBypassedAndFlagged()
    {
        var (status, flagged) = KycStatusDeriver.ForBypass();
        Assert.Equal(KycStatus.Bypassed, status);
        Assert.True(flagged);
    }
}
