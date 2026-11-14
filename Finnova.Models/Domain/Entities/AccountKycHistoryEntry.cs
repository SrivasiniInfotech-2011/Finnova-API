using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Append-only KYC outcome record. Never stores PAN or Aadhaar values.</summary>
public sealed class AccountKycHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public KycSource Source { get; set; }
    public KycStatus KycStatus { get; set; }
    public KycCheckResult PanValidation { get; set; }
    public KycCheckResult PanNameMatch { get; set; }
    public KycCheckResult AadhaarValidation { get; set; }
    public KycCheckResult AadhaarAddressMatch { get; set; }
    public Guid? VerificationSessionId { get; set; }
    public string? ProviderReference { get; set; }
    public KycStatus? PreviousStatus { get; set; }
    public string? Reason { get; set; }
    public string? Comment { get; set; }
    public Guid? ResolutionRequestId { get; set; }
    public Guid? MakerUserId { get; set; }
    public Guid? CheckerUserId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}