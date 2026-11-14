using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Phase 2 work item (the KYC Check) for one Account. Re-armed by a KYC re-check.</summary>
public sealed class AccountKycJob
{
    public Guid AccountId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? RecheckRequestedBy { get; set; }
    public string? RecheckReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}