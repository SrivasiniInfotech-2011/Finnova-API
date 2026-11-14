using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Maker-checker request to clear a KYC flag manually. At most one Pending per Account.</summary>
public sealed class AccountKycResolutionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public KycResolutionStatus Status { get; set; } = KycResolutionStatus.Pending;
    public Guid MakerUserId { get; set; }
    public string MakerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public KycStatus PreviousKycStatus { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CheckerUserId { get; set; }
    public string? CheckerName { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}