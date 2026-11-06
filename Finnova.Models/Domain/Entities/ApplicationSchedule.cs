using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class ApplicationSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal FinanceAmount { get; set; }
    public decimal IrrPercent { get; set; }
    public int Tenure { get; set; }
    public RepaymentFrequency RepaymentFrequency { get; set; }
    public DateOnly DisbursementDate { get; set; }
    public decimal PeriodicInterestRate { get; set; }
    public decimal EmiAmount { get; set; }
    public decimal TotalInstallmentAmount { get; set; }
    public decimal TotalInterestAmount { get; set; }
    public decimal TotalPrincipalAmount { get; set; }
    public List<ApplicationInstallment> Installments { get; set; } = new();
}

public sealed class ApplicationInstallment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal InterestPortion { get; set; }
    public decimal PrincipalPortion { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal ClosingBalance { get; set; }
}
