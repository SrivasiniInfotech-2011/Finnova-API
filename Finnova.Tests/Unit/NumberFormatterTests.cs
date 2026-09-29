using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Xunit;

namespace Finnova.Tests.Unit;

/// <summary>
/// Unit tests for the pure numbering helpers (FINNOVA-7 R1.12/1.13, R5.3, R5.5, R5.9):
/// template validation, rendering with padding + date tokens, period-key derivation, and the
/// padding capacity used for exhaustion detection.
/// </summary>
public class NumberFormatterTests
{
    private static readonly DateTime Utc = new(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("{SEQ}")]
    [InlineData("{SEQ:5}")]
    [InlineData("INV-{YYYY}{MM}-{SEQ:5}")]
    [InlineData("{PREFIX}{SEQ:4}{SUFFIX}")]
    [InlineData("LN-{YY}{MM}{DD}-{SEQ}")]
    public void Validate_AcceptsWellFormedTemplates(string template)
    {
        Assert.Empty(NumberFormatter.Validate(template));
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("INV-0001", "sequence token")]          // no SEQ token
    [InlineData("{SEQ}{SEQ}", "exactly one")]           // duplicate SEQ
    [InlineData("{FOO}{SEQ}", "Unknown token")]         // unknown token
    [InlineData("{SEQ:0}", "between 1 and 18")]         // width too small
    [InlineData("{SEQ:99}", "between 1 and 18")]        // width too large
    [InlineData("{SEQ:x}", "between 1 and 18")]         // non-numeric width
    public void Validate_RejectsBadTemplates(string template, string expectedFragment)
    {
        var errors = NumberFormatter.Validate(template);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Format_PadsSequenceAndSubstitutesDateAndAffixes()
    {
        var result = NumberFormatter.Format("INV-{YYYY}{MM}-{SEQ:5}", 42, defaultPadding: 1, prefix: null, suffix: null, Utc);
        Assert.Equal("INV-202603-00042", result);
    }

    [Fact]
    public void Format_SeqUsesDefaultPaddingWhenNoWidthSpecified()
    {
        var result = NumberFormatter.Format("{SEQ}", 7, defaultPadding: 4, prefix: null, suffix: null, Utc);
        Assert.Equal("0007", result);
    }

    [Fact]
    public void Format_SubstitutesPrefixAndSuffix()
    {
        var result = NumberFormatter.Format("{PREFIX}{SEQ:3}{SUFFIX}", 5, 1, "A-", "-Z", Utc);
        Assert.Equal("A-005-Z", result);
    }

    [Fact]
    public void Format_DoesNotTruncateValueWiderThanPadding()
    {
        var result = NumberFormatter.Format("{SEQ:2}", 12345, 1, null, null, Utc);
        Assert.Equal("12345", result);
    }

    [Theory]
    [InlineData(1, 9L)]
    [InlineData(3, 999L)]
    [InlineData(5, 99999L)]
    public void MaxValueForPadding_ReturnsCapacity(int padding, long expected)
    {
        Assert.Equal(expected, NumberFormatter.MaxValueForPadding(padding));
    }

    [Theory]
    [InlineData(NumberResetRule.Never, "ALL")]
    [InlineData(NumberResetRule.Yearly, "2026")]
    [InlineData(NumberResetRule.Monthly, "202603")]
    [InlineData(NumberResetRule.Daily, "20260309")]
    public void PeriodKey_DerivesFromRuleAndInstant(NumberResetRule rule, string expected)
    {
        Assert.Equal(expected, PeriodKey.For(rule, Utc));
    }
}
