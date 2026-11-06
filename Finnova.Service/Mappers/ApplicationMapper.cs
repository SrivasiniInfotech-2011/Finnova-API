using Finnova.Models.Contracts.Applications;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.ValueObjects;

namespace Finnova.Service.Mappers;

public static class ApplicationMapper
{
    public static ApplicationResponse ToResponse(this Application x) => new(
        x.Id, x.ApplicationNumber, x.LineOfBusinessId, x.CustomerId, x.BranchId, x.ProductCode,
        x.SchemeCode, x.FinanceAmount, x.IrrPercent, x.Tenure, x.RepaymentFrequency,
        x.DisbursementDate, x.ProcessingCharges, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc);

    public static ApplicationScheduleResponse ToResponse(this ApplicationSchedule x) => new(
        x.ApplicationId, x.IsFinalized, x.FinanceAmount, x.IrrPercent, x.Tenure, x.RepaymentFrequency,
        x.DisbursementDate, x.PeriodicInterestRate, x.EmiAmount,
        x.Installments.OrderBy(i => i.InstallmentNumber).Select(i => new ApplicationInstallmentResponse(
            i.InstallmentNumber, i.DueDate, i.OpeningBalance, i.InterestPortion, i.PrincipalPortion,
            i.InstallmentAmount, i.ClosingBalance)).ToList(), x.TotalInstallmentAmount,
        x.TotalInterestAmount, x.TotalPrincipalAmount);

    public static ApplicationScheduleResponse ToResponse(this ScheduleResult x, Guid applicationId,
        ApplicationTerms terms) => new(applicationId, false, terms.FinanceAmount, terms.IrrPercent,
        terms.Tenure, terms.RepaymentFrequency, terms.DisbursementDate, x.PeriodicInterestRate,
        x.EmiAmount, x.Installments.Select(i => new ApplicationInstallmentResponse(i.InstallmentNumber,
            i.DueDate, i.OpeningBalance, i.InterestPortion, i.PrincipalPortion, i.InstallmentAmount,
            i.ClosingBalance)).ToList(), x.TotalInstallmentAmount, x.TotalInterestAmount,
        x.TotalPrincipalAmount);
}
