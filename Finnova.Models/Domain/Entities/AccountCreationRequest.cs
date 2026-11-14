using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Outbox row inserted when an Application becomes Processed; claimed by the Python worker.</summary>
public sealed class AccountCreationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? InitiatedByUserId { get; set; }
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}