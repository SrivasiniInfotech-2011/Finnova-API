using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class ReceiptAppropriation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid ClientRequestId { get; set; }
    public AppropriationMode Mode { get; set; }
    public ReceiptAppropriationStatus Status { get; set; } = ReceiptAppropriationStatus.Confirmed;
    public decimal ReceiptAmount { get; set; }
    public decimal DueAllocationTotal { get; set; }
    public decimal AdvanceAccountAmount { get; set; }
    public decimal ResidualAmount { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public AppropriationDirection? RuleDirection { get; set; }
    public bool? FullDue { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTime ConfirmedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ReceiptAllocation> Allocations { get; set; } = new();
    public AdvanceAccountAllocation? AdvanceAccountAllocation { get; set; }
}

public sealed class ReceiptAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptAppropriationId { get; set; }
    public Guid PendingDueId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public CashFlowDueType CashFlowDueType { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal OutstandingAmountAtConfirm { get; set; }
    public Guid? RuleDetailId { get; set; }
    public int? RuleOrder { get; set; }
    public decimal? TargetAmount { get; set; }
    public string DueRowVersionAtConfirm { get; set; } = string.Empty;
    public ReceiptAppropriation ReceiptAppropriation { get; set; } = null!;
}

public sealed class AdvanceAccountAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptAppropriationId { get; set; }
    public Guid AdvanceAccountId { get; set; }
    public decimal Amount { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ReceiptAppropriation ReceiptAppropriation { get; set; } = null!;
}

public sealed class AppropriationAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid ReceiptAppropriationId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public decimal DueAllocationTotal { get; set; }
    public decimal AdvanceAccountAmount { get; set; }
    public decimal ResidualAmount { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}