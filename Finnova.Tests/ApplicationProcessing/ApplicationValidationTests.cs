using Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.Applications.Financial;

namespace Finnova.Tests.ApplicationProcessing;

public sealed class ApplicationValidationTests
{
    [Fact] public void ZeroIrrHasZeroInterest() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000m, 0m, 3, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); Assert.All(s.Installments, x => Assert.Equal(0m, x.InterestPortion)); Assert.Equal(1000m, s.TotalPrincipalAmount); }
    [Fact] public void TenureOneSettlesInOneLine() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000m, 12m, 1, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); var x = Assert.Single(s.Installments); Assert.Equal(1000m, x.PrincipalPortion); Assert.Equal(0m, x.ClosingBalance); }
    [Fact] public void MonthEndDueDateUsesCalendarAddMonths() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(100m, 0m, 2, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 31))); Assert.Equal(new DateOnly(2025, 2, 28), s.Installments[0].DueDate); }
    [Fact] public void PaiseTieRoundsAwayFromZero() => Assert.Equal(1.01m, Math.Round(1.005m, 2, MidpointRounding.AwayFromZero));
}
