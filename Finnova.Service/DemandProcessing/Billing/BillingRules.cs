using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Billing;

public static class BillingRules
{
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal ScheduledInstallment(ScheduleProjection schedule)
    {
        if (schedule.ScheduledPrincipal < 0m || schedule.ScheduledInterestOrEmi < 0m) throw new InvalidOperationException("Schedule components cannot be negative.");
        return Money(schedule.ScheduledPrincipal + schedule.ScheduledInterestOrEmi);
    }

    public static bool JournalChecksPass(JournalCheckResult result) => result.GenerationPassed && result.PostingPassed && result.IdempotencyPassed && result.ReconciliationPassed;

    public static void RequireBillingSnapshot(BillingCompletion completion, DemandScopeContract expected)
    {
        if (completion.Scope.LineOfBusinessId != expected.LineOfBusinessId || !completion.Scope.BranchIds.OrderBy(x => x).SequenceEqual(expected.BranchIds.OrderBy(x => x)) || !string.Equals(completion.Scope.Frequency, expected.Frequency, StringComparison.Ordinal) || completion.Scope.PeriodYear != expected.PeriodYear || completion.Scope.PeriodMonth != expected.PeriodMonth || !string.Equals(completion.Scope.BillingReference, expected.BillingReference, StringComparison.Ordinal)) throw new Finnova.Models.Domain.Exceptions.DemandBillingScopeMismatchException();
        if (completion.State != BillingStageStatus.Succeeded || completion.RequiredChecks.Count == 0 || completion.RequiredChecks.Values.Any(x => !x)) throw new Finnova.Models.Domain.Exceptions.DemandBillingIncompleteException();
    }

    public static string ApplicationKey(AllocationEvent eventItem) => $"{eventItem.EventType}:{eventItem.Id:N}:{eventItem.SourceVersion ?? "none"}";
}

public sealed class AllocationPolicy : IAllocationPolicy
{
    public AllocationResult Apply(decimal scheduledInstallment, decimal priorArrears, IReadOnlyList<AllocationEvent> events)
    {
        var current = BillingRules.Money(Math.Max(0m, scheduledInstallment));
        var arrears = BillingRules.Money(Math.Max(0m, priorArrears));
        var evidence = new List<AllocationAppliedEvent>();
        foreach (var item in events.OrderBy(x => x.EffectiveUtc).ThenBy(x => x.Id))
        {
            if (!item.Eligible || item.Amount <= 0m) continue;
            var key = BillingRules.ApplicationKey(item);
            var remaining = BillingRules.Money(item.Amount);
            var target = item.TargetComponent switch { "Arrears" => "Arrears", "CurrentDue" => "CurrentDue", "Suspense" => "Suspense", _ => "CurrentDue" };
            if (item.EventType is AllocationEventType.Receipt or AllocationEventType.PartPayment)
            {
                var arrearsApplied = Math.Min(arrears, remaining); arrears = BillingRules.Money(arrears - arrearsApplied); remaining = BillingRules.Money(remaining - arrearsApplied);
                var currentApplied = Math.Min(current, remaining); current = BillingRules.Money(current - currentApplied); evidence.Add(new AllocationAppliedEvent(item, BillingRules.Money(arrearsApplied + currentApplied), target, key));
            }
            else if (item.EventType is AllocationEventType.Waiver or AllocationEventType.Reversal or AllocationEventType.Adjustment)
            {
                var applied = target == "Arrears" ? Math.Min(arrears, remaining) : Math.Min(current, remaining); if (target == "Arrears") arrears = BillingRules.Money(arrears - applied); else current = BillingRules.Money(current - applied); evidence.Add(new AllocationAppliedEvent(item, BillingRules.Money(applied), target, key));
            }
            else evidence.Add(new AllocationAppliedEvent(item, 0m, "Suspense", key));
        }
        return new AllocationResult(current, arrears, evidence);
    }
}
