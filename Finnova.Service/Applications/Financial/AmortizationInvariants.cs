using Finnova.Models.Domain.ValueObjects;

namespace Finnova.Service.Applications.Financial;

public static class AmortizationInvariants
{
    public static bool Holds(ScheduleResult result, decimal principal)
    {
        var rows = result.Installments;
        return rows.Count > 0 && rows.Select(x => x.InstallmentNumber).SequenceEqual(Enumerable.Range(1, rows.Count))
            && rows.All(x => x.OpeningBalance >= 0 && x.InterestPortion >= 0 && x.PrincipalPortion >= 0 && x.InstallmentAmount >= 0 && x.ClosingBalance >= 0)
            && rows[^1].ClosingBalance == 0m && rows.Sum(x => x.PrincipalPortion) == principal
            && result.TotalInstallmentAmount == result.TotalPrincipalAmount + result.TotalInterestAmount;
    }
}
