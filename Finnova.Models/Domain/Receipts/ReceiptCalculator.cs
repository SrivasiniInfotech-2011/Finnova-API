using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Receipts;

/// <summary>Pure amount arithmetic (R4.5, R9.1). Decimal sums of two-decimal inputs are exact.</summary>
public static class ReceiptCalculator
{
    public const decimal MaxAmount = 999999999.99m;   // 99,99,99,999.99 (A18)

    public static decimal LinesTotal(IEnumerable<decimal> lineAmounts) => lineAmounts.Sum();

    /// <summary>Sum of Add rows minus the sum of Less rows.</summary>
    public static decimal AdjustmentTotal(IEnumerable<(AddLessKind Kind, decimal Amount)> rows)
        => rows.Sum(r => r.Kind == AddLessKind.Add ? r.Amount : -r.Amount);

    /// <summary>Doc Amount = sum(Lines) + sum(Add) - sum(Less).</summary>
    public static decimal DocAmount(IEnumerable<decimal> lineAmounts, IEnumerable<(AddLessKind Kind, decimal Amount)> addLess)
        => LinesTotal(lineAmounts) + AdjustmentTotal(addLess);

    public static decimal DenominationAmount(decimal faceValue, int count) => faceValue * count;

    public static decimal DenominationTotal(IEnumerable<(decimal FaceValue, int Count)> rows)
        => rows.Sum(r => DenominationAmount(r.FaceValue, r.Count));

    public static bool HasAtMostTwoDecimals(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;
}