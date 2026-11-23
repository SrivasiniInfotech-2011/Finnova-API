using Finnova.Models.Domain.Enums;
using Finnova.Service.ReceiptAppropriation.Ports;

namespace Finnova.Service.ReceiptAppropriation.Rules;

public sealed record AllocationLine(Guid DueId, Guid AccountId, string AccountNumber, CashFlowDueType DueType, DateOnly DueDate, decimal OutstandingAmount, decimal AllocatedAmount, Guid? RuleDetailId, int? RuleOrder, decimal? TargetAmount, string DueVersion);
public sealed record CalculationResult(IReadOnlyList<AllocationLine> Lines, decimal DueTotal, decimal Residual, bool Reconciles);

public static class AppropriationCalculator
{
    private static readonly IReadOnlyDictionary<CashFlowDueType, int> Ranks = new Dictionary<CashFlowDueType, int> { [CashFlowDueType.Installment] = 0, [CashFlowDueType.Principal] = 1, [CashFlowDueType.Interest] = 2, [CashFlowDueType.Insurance] = 3, [CashFlowDueType.ODI] = 4, [CashFlowDueType.OtherCharges] = 5 };
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static IReadOnlyList<PendingDueProjection> Order(IEnumerable<PendingDueProjection> dues) => dues.Where(x => x.OutstandingAmount > 0m).OrderBy(x => x.AccountNumber, StringComparer.Ordinal).ThenBy(x => Ranks[x.DueType]).ThenBy(x => x.DueDate).ThenBy(x => x.Id).ToArray();

    public static CalculationResult Calculate(decimal receiptAmount, IReadOnlyList<PendingDueProjection> source, AppropriationRuleProjection rule)
    {
        if (receiptAmount <= 0m || Money(receiptAmount) != receiptAmount) throw new ArgumentOutOfRangeException(nameof(receiptAmount));
        if (!rule.IsActive || rule.Details.Count == 0 || rule.Details.Any(x => x.Percentage < 0m || x.Percentage > 100m) || rule.Details.Sum(x => x.Percentage) > 100m || rule.Details.Select(x => x.DueType).Distinct().Count() != rule.Details.Count) throw new InvalidOperationException("The active FINNOVA-67 appropriation rule is invalid.");
        var ordered = Order(source).ToList(); var remaining = Money(receiptAmount); var lines = ordered.ToDictionary(x => x.Id, x => new AllocationLine(x.Id, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, Money(x.OutstandingAmount), 0m, null, null, null, x.Version));
        IEnumerable<RuleDetailProjection> details = rule.Details.OrderBy(x => x.Order);
        IEnumerable<PendingDueProjection> Traverse(RuleDetailProjection d) => rule.Direction == AppropriationDirection.Horizontal ? ordered.Where(x => x.DueType == d.DueType) : ordered;
        foreach (var detail in details)
        {
            var target = Math.Min(remaining, Money(receiptAmount * detail.Percentage / 100m));
            foreach (var due in Traverse(detail).Where(x => x.DueType == detail.DueType))
            {
                var line = lines[due.Id]; var take = Math.Min(target, Math.Min(remaining, line.OutstandingAmount - line.AllocatedAmount));
                if (take <= 0m) continue;
                lines[due.Id] = line with { AllocatedAmount = Money(line.AllocatedAmount + take), RuleDetailId = detail.Id, RuleOrder = detail.Order, TargetAmount = Money(receiptAmount * detail.Percentage / 100m) };
                target = Money(target - take); remaining = Money(remaining - take); if (target == 0m || remaining == 0m) break;
            }
        }
        var result = lines.Values.Where(x => x.AllocatedAmount > 0m).OrderBy(x => x.AccountNumber, StringComparer.Ordinal).ThenBy(x => Ranks[x.DueType]).ThenBy(x => x.DueDate).ThenBy(x => x.DueId).ToArray();
        var dueTotal = Money(result.Sum(x => x.AllocatedAmount)); var residual = Money(receiptAmount - dueTotal);
        return new(result, dueTotal, residual, Money(dueTotal + residual) == Money(receiptAmount));
    }
}