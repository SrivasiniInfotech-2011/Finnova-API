using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid LineOfBusinessId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BranchId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string SchemeCode { get; set; } = string.Empty;
    public decimal FinanceAmount { get; set; }
    public decimal IrrPercent { get; set; }
    public int Tenure { get; set; }
    public RepaymentFrequency RepaymentFrequency { get; set; }
    public DateOnly DisbursementDate { get; set; }
    public decimal ProcessingCharges { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
