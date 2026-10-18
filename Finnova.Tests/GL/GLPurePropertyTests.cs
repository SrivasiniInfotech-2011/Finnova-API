using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Service.GL.Internal;
using Xunit;

namespace Finnova.Tests.GL;

public class GLPurePropertyTests
{
    // PROP-NORMALBALANCE (R1.10)
    [Property(MaxTest = 200)]
    public void NormalBalance_is_total_and_correct(AccountNature nature)
    {
        var nb = NatureRules.NormalBalanceFor(nature);
        var expected = (nature == AccountNature.Asset || nature == AccountNature.Expense)
            ? NormalBalance.Debit : NormalBalance.Credit;
        Assert.Equal(expected, nb);
    }

    // PROP-GLCODE-FORMAT (R1.5)
    [Property(MaxTest = 500)]
    public void GLCode_is_exactly_nine_digits(NonNull<string> s)
    {
        var value = s.Get;
        var expected = value.Length == 9 && value.All(c => c >= '0' && c <= '9');
        Assert.Equal(expected, GLCodeRules.IsNineNumericDigits(value));
    }

    [Property(MaxTest = 200)]
    public void GLCode_true_only_for_nine_digit_strings(uint n)
    {
        var nine = (n % 1_000_000_000u).ToString("D9");
        Assert.True(GLCodeRules.IsNineNumericDigits(nine));
    }

    // PROP-PARENTPREFIX (R1.6)
    [Property(MaxTest = 300)]
    public void ParentPrefix_is_leading_substring(NonNull<string> parent, NonNull<string> suffix)
    {
        var p = parent.Get;
        var child = p + suffix.Get;
        Assert.True(GLCodeRules.BeginsWithParentPrefix(child, p));
    }

    // PROP-LEVEL-DERIVATION (R1.1/R1.8)
    [Fact]
    public void Level_from_null_is_L1()
        => Assert.Equal(GLLevel.L1, NatureRules.LevelFrom(null));

    [Property(MaxTest = 100)]
    public void Level_from_parent_is_parent_plus_one()
    {
        Assert.Equal(GLLevel.L2, NatureRules.LevelFrom(GLLevel.L1));
        Assert.Equal(GLLevel.L3, NatureRules.LevelFrom(GLLevel.L2));
        Assert.Equal(GLLevel.L4, NatureRules.LevelFrom(GLLevel.L3));
    }

    // PROP-POSTABLE-L4 (R1.2/R1.3)
    [Property(MaxTest = 100)]
    public void Postable_only_at_L4(GLLevel level)
        => Assert.Equal(level == GLLevel.L4, NatureRules.IsPostableFor(level));
}
