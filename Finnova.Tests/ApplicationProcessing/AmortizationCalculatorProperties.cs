using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.Applications.Financial;

namespace Finnova.Tests.ApplicationProcessing;

public sealed class AmortizationCalculatorProperties
{
    private static readonly RepaymentFrequency[] Frequencies = Enum.GetValues<RepaymentFrequency>();
    private static ApplicationTerms Terms(decimal p, decimal irr, int n, RepaymentFrequency f, DateOnly d) => new(p, irr, n, f, d);
    [Property(MaxTest = 100)] // Feature: application-processing, Property 1: Schedule shape and installment numbering
    public Property ScheduleShape(PositiveInt amount, NonNegativeInt tenureSeed) { var n = tenureSeed.Get % 600 + 1; var r = new AmortizationCalculator().Calculate(Terms(amount.Get % 1000000m + .01m, 12m, n, Frequencies[n % 4], new DateOnly(2025, 1, 31))); return (r.Installments.Count == n && r.Installments.Select(x => x.InstallmentNumber).SequenceEqual(Enumerable.Range(1, n))).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 2: Periodic rate conversion
    public Property PeriodicRate(NonNegativeInt irr, NonNegativeInt frequency) { var i = irr.Get % 10001 / 100m; var f = Frequencies[frequency.Get % 4]; var expected = i / 100m / (f switch { RepaymentFrequency.Monthly => 12, RepaymentFrequency.Quarterly => 4, RepaymentFrequency.HalfYearly => 2, _ => 1 }); var actual = new AmortizationCalculator().Calculate(Terms(1000m, i, 12, f, new DateOnly(2025, 1, 1))).PeriodicInterestRate; return (actual == expected).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 3: Reducing-balance line recurrence
    public Property Recurrence(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(seed.Get % 100000m + 1m, 9m, 12, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return r.Installments.Skip(1).Select((x, i) => x.OpeningBalance == r.Installments[i].ClosingBalance).All(x => x).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 4: Principal conservation and non-negative balances
    public Property Conservation(PositiveInt seed) { var p = seed.Get % 100000m + 1m; var r = new AmortizationCalculator().Calculate(Terms(p, seed.Get % 10000 / 100m, 20, RepaymentFrequency.Quarterly, new DateOnly(2025, 1, 1))); return (r.TotalPrincipalAmount == p && r.Installments.All(x => x.OpeningBalance >= 0 && x.InterestPortion >= 0 && x.PrincipalPortion >= 0 && x.InstallmentAmount >= 0 && x.ClosingBalance >= 0)).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 5: Final-line settlement
    public Property FinalSettlement(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(seed.Get % 100000m + 1m, 15m, 13, RepaymentFrequency.Annual, new DateOnly(2025, 1, 1))); var x = r.Installments[^1]; return (x.PrincipalPortion == x.OpeningBalance && x.InstallmentAmount == x.PrincipalPortion + x.InterestPortion && x.ClosingBalance == 0).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 6: Balance monotonicity
    public Property Monotonicity(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(seed.Get % 100000m + 1m, 8m, 24, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return (r.Installments.All(x => x.ClosingBalance <= x.OpeningBalance) && r.Installments.Skip(1).Select((x, i) => x.OpeningBalance <= r.Installments[i].OpeningBalance).All(x => x)).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 7: Summary-total identity
    public Property SummaryIdentity(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(seed.Get % 100000m + 1m, 11m, 18, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return (r.TotalPrincipalAmount + r.TotalInterestAmount == r.TotalInstallmentAmount).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 8: Deterministic generation
    public Property Deterministic(PositiveInt seed) { var t = Terms(seed.Get % 100000m + 1m, 7m, 10, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1)); var c = new AmortizationCalculator(); var first = c.Calculate(t); var second = c.Calculate(t); return (first.PeriodicInterestRate == second.PeriodicInterestRate && first.EmiAmount == second.EmiAmount && first.TotalInstallmentAmount == second.TotalInstallmentAmount && first.TotalInterestAmount == second.TotalInterestAmount && first.TotalPrincipalAmount == second.TotalPrincipalAmount && first.Installments.SequenceEqual(second.Installments)).ToProperty(); }
    [Property(MaxTest = 100)] // Feature: application-processing, Property 9: Due-date recurrence
    public Property DueDates(PositiveInt seed) { var f = Frequencies[seed.Get % 4]; var r = new AmortizationCalculator().Calculate(Terms(1000m, 0m, 5, f, new DateOnly(2025, 1, 31))); var months = f switch { RepaymentFrequency.Monthly => 1, RepaymentFrequency.Quarterly => 3, RepaymentFrequency.HalfYearly => 6, _ => 12 }; return r.Installments.Select((x, i) => x.DueDate == new DateOnly(2025, 1, 31).AddMonths(months * (i + 1))).All(x => x).ToProperty(); }
}
