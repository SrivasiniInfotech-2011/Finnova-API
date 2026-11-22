using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Service.ReceiptAppropriation.Ports;
using Finnova.Service.ReceiptAppropriation.Rules;
using Xunit;

namespace Finnova.Tests.ReceiptAppropriation;

public sealed class AppropriationCalculatorTests
{
    [Property(MaxTest = 100)]
    public Property AllocationsAreCappedAndReconciled(NonNegativeInt amount)
    {
        var receipt = Math.Max(1, amount.Get % 100000);
        var due = new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-1", Guid.NewGuid(), CashFlowDueType.Installment, new DateOnly(2026, 1, 1), receipt / 2m, "Collectible", "v1");
        var rule = new AppropriationRuleProjection(Guid.NewGuid(), "v1", Guid.NewGuid(), Guid.NewGuid(), AppropriationDirection.Vertical, true, true, new[] { new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.Installment, 100m, 1) });
        var result = AppropriationCalculator.Calculate(receipt, new[] { due }, rule);
        return (result.Lines.All(x => x.AllocatedAmount >= 0m && x.AllocatedAmount <= x.OutstandingAmount) && result.Reconciles).ToProperty();
    }

    [Fact]
    public void OrdersByAccountTypeDateAndId()
    {
        var account = Guid.NewGuid(); var first = new PendingDueProjection(Guid.NewGuid(), account, "A", Guid.NewGuid(), CashFlowDueType.ODI, new DateOnly(2026, 1, 1), 1m, "Collectible", "v1"); var second = new PendingDueProjection(Guid.NewGuid(), account, "A", Guid.NewGuid(), CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 1m, "Collectible", "v1"); Assert.Equal(CashFlowDueType.Installment, AppropriationCalculator.Order(new[] { first, second })[0].DueType);
    }

    [Fact]
    public void UsesTheThirtyFiveThousandExample()
    {
        var customer = Guid.NewGuid(); var dues = new[] { new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-889021", customer, CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 30000m, "Collectible", "1"), new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-889021", customer, CashFlowDueType.ODI, new DateOnly(2026, 1, 2), 15000m, "Collectible", "1") }; var rule = new AppropriationRuleProjection(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), AppropriationDirection.Vertical, true, true, new[] { new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.Installment, 80m, 1), new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.ODI, 20m, 2) }); var result = AppropriationCalculator.Calculate(35000m, dues, rule); Assert.Equal(28000m, result.Lines.Single(x => x.DueType == CashFlowDueType.Installment).AllocatedAmount); Assert.Equal(7000m, result.Lines.Single(x => x.DueType == CashFlowDueType.ODI).AllocatedAmount);
    }
}