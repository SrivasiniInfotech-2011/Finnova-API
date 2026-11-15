using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DemandProcessing.Configuration;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.Extensions.Options;

namespace Finnova.Service.DemandProcessing.Billing;

public sealed class DefaultChargePolicyAdapter(IOptions<DemandProcessingOptions> options) : IChargePolicyAdapter
{
    public Task<ChargeResult> CalculateAsync(ChargeInput input, CancellationToken ct)
    {
        var basis = options.Value.DefaultDayCountBasis <= 0 ? 365 : options.Value.DefaultDayCountBasis;
        var overduePrincipal = Math.Max(0m, input.OverduePrincipalBalance - input.ScheduledInstallment);
        var odi = BillingRules.Money(overduePrincipal * input.AnnualOdiRate * input.OverdueDays / basis);
        if (options.Value.OdiCap is decimal cap) odi = BillingRules.Money(Math.Min(odi, Math.Max(0m, cap)));
        var postedNonOdi = input.ChargeEvents.Where(x => x.Eligible && x.EventType == AllocationEventType.OtherCharge).Sum(x => x.Amount);
        var eligibleAdjustments = input.ChargeAdjustments.Where(x => x.Eligible && x.EventType is AllocationEventType.Waiver or AllocationEventType.Reversal or AllocationEventType.Adjustment).Sum(x => x.Amount);
        var net = postedNonOdi - eligibleAdjustments;
        var other = BillingRules.Money(Math.Max(0m, net));
        var evidence = input.ChargeEvents.Concat(input.ChargeAdjustments).Where(x => x.Eligible).Select(x => x.SourceReference).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        return Task.FromResult(new ChargeResult(odi, other, "Default:ODI365-or-configured|OtherChargesNetPostedNonOdi", evidence));
    }
}
