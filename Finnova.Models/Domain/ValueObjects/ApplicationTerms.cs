using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.ValueObjects;

public sealed record ApplicationTerms(
    decimal FinanceAmount,
    decimal IrrPercent,
    int Tenure,
    RepaymentFrequency RepaymentFrequency,
    DateOnly DisbursementDate);

public sealed record ScheduleLineValue(
    int InstallmentNumber,
    DateOnly DueDate,
    decimal OpeningBalance,
    decimal InterestPortion,
    decimal PrincipalPortion,
    decimal InstallmentAmount,
    decimal ClosingBalance);

public sealed record ScheduleResult(
    decimal PeriodicInterestRate,
    decimal EmiAmount,
    IReadOnlyList<ScheduleLineValue> Installments,
    decimal TotalInstallmentAmount,
    decimal TotalInterestAmount,
    decimal TotalPrincipalAmount);
