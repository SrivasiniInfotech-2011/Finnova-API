using Finnova.Models.Domain.Enums;

namespace Finnova.Service.GL.Internal;

/// <summary>Pure nature/level derivation rules (R1.1/R1.8/R1.10). No I/O — property-testable.</summary>
public static class NatureRules
{
    /// <summary>Asset/Expense -> Debit; Liability/Equity/Income -> Credit. Total (R1.10).</summary>
    public static NormalBalance NormalBalanceFor(AccountNature nature) => nature switch
    {
        AccountNature.Asset => NormalBalance.Debit,
        AccountNature.Expense => NormalBalance.Debit,
        AccountNature.Liability => NormalBalance.Credit,
        AccountNature.Equity => NormalBalance.Credit,
        AccountNature.Income => NormalBalance.Credit,
        _ => NormalBalance.Debit
    };

    /// <summary>L1 iff no parent; else parent+1. Never exceeds L4 (R1.1/R1.8).</summary>
    public static GLLevel LevelFrom(GLLevel? parentLevel)
    {
        if (parentLevel is null) return GLLevel.L1;
        return parentLevel.Value switch
        {
            GLLevel.L1 => GLLevel.L2,
            GLLevel.L2 => GLLevel.L3,
            GLLevel.L3 => GLLevel.L4,
            _ => throw new InvalidOperationException("A parent at L4 cannot have children.")
        };
    }

    /// <summary>Postable iff the account is a leaf at L4 (R1.2).</summary>
    public static bool IsPostableFor(GLLevel level) => level == GLLevel.L4;
}
