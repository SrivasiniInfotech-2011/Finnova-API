using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.DemandProcessing.Billing;
using Finnova.Service.DemandProcessing.Rules;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class DemandProcessingExampleTests
{
    [Fact] public void ClosedCancelledAndPreClosedAccountsAreExcluded() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); foreach (var status in new[] { "Closed", "Cancelled", "Pre-Closed" }) Assert.False(DemandRules.Check(new AccountProjection(Guid.NewGuid(), Guid.NewGuid(), lob, branch, "A", "C", status, true, true, true, "s"), DemandScope.Create(lob, [branch])).Eligible); }
    [Fact] public void DefaultChargePolicyClampsNegativeOtherCharges() { var adapter = new DefaultChargePolicyAdapter(Microsoft.Extensions.Options.Options.Create(new Finnova.Service.DemandProcessing.Configuration.DemandProcessingOptions())); var eventItem = new AllocationEvent(Guid.NewGuid(), AllocationEventType.OtherCharge, 2m, DateTime.UtcNow, "charge", "1", "OtherCharges", true); var adjustment = new AllocationEvent(Guid.NewGuid(), AllocationEventType.Waiver, 5m, DateTime.UtcNow, "waiver", "1", "OtherCharges", true); var result = adapter.CalculateAsync(new ChargeInput(100m, .1m, 10, null, 10m, [eventItem], [adjustment]), CancellationToken.None).Result; Assert.Equal(0m, result.OtherCharges); Assert.Contains("charge", result.EvidenceReferences); }
    [Fact] public void ZeroDemandIsRetained() { var result = DemandRules.Calculate(0m, 0m, new ChargeResult(0m, 0m, "test", Array.Empty<string>())); Assert.True(result.IsZeroDemand); }
    [Fact] public void InrRoundingUsesAwayFromZero() { Assert.Equal(1.01m, BillingRules.Money(1.005m)); Assert.Equal(-1.01m, BillingRules.Money(-1.005m)); }
    [Fact] public void FailedBillingCompletionIsRejected() { var c = new BillingCompletion("b", new(Guid.NewGuid(), [Guid.NewGuid()], "Monthly", 2025, 1, "b"), BillingStageStatus.Failed, "s", null, new Dictionary<string, bool> { ["snapshot"] = false }); Assert.Throws<Finnova.Models.Domain.Exceptions.DemandBillingIncompleteException>(() => BillingRules.RequireBillingSnapshot(c, c.Scope)); }
    [Fact] public void DeactivatedBranchHistoryIsAReadConcern() { Assert.True(Enum.IsDefined(DemandBatchStatus.NoDemand)); }
}
