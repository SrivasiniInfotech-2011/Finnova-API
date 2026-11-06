using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;

namespace Finnova.Service.Applications.Financial;

public sealed class AmortizationCalculator
{
    public ScheduleResult Calculate(ApplicationTerms t)
    {
        var periods = t.RepaymentFrequency switch
        {
            RepaymentFrequency.Monthly => 12,
            RepaymentFrequency.Quarterly => 4,
            RepaymentFrequency.HalfYearly => 2,
            RepaymentFrequency.Annual => 1,

            _ => throw new ArgumentOutOfRangeException()
        };
        var rate = t.IrrPercent / 100m / periods;
        var emi = t.IrrPercent == 0m ? Money(t.FinanceAmount / t.Tenure) :
            Money(t.FinanceAmount * rate * (decimal)Math.Pow((double)(1m + rate), t.Tenure) /
                ((decimal)Math.Pow((double)(1m + rate), t.Tenure) - 1m));
        var lines = new List<ScheduleLineValue>(t.Tenure); var opening = Money(t.FinanceAmount);
        for (var n = 1; n <= t.Tenure; n++)
        {
            var interest = Money(opening * rate); var final = n == t.Tenure;
            var principal = final ? opening : Money(emi - interest);
            var installment = final ? Money(principal + interest) : emi;
            var closing = final ? 0m : Money(opening - principal);
            lines.Add(new ScheduleLineValue(n, DueDate(t.DisbursementDate, t.RepaymentFrequency, n), opening,
                interest, principal, installment, closing)); opening = closing;
        }
        return new ScheduleResult(rate, emi, lines, Money(lines.Sum(x => x.InstallmentAmount)),
            Money(lines.Sum(x => x.InterestPortion)), Money(lines.Sum(x => x.PrincipalPortion)));
    }
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static DateOnly DueDate(DateOnly disbursement, RepaymentFrequency frequency, int number)
    {
        var months = frequency switch
        {
            RepaymentFrequency.Monthly => 1,
            RepaymentFrequency.Quarterly => 3,
            RepaymentFrequency.HalfYearly => 6,
            RepaymentFrequency.Annual => 12,
            _ => throw new ArgumentOutOfRangeException()
        };
        return disbursement.AddMonths(months * number);
    }
}
