using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Savings;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "INR";
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? BranchCode { get; set; }
    public string? IfscCode { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
