using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class JvPostingInbox
{
    public Guid RequestId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string? RuleKey { get; set; }
    public string SchemaVersion { get; set; } = "1.0";
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly ValueDate { get; set; }
    public DateOnly? PostingDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Description { get; set; }
    public byte[] RawPayload { get; set; } = Array.Empty<byte>();
    public byte[] PayloadHash { get; set; } = Array.Empty<byte>();
    public string InitiationMode { get; set; } = string.Empty;
    public Guid? InitiatingUserId { get; set; }
    public string? AutomationContext { get; set; }
    public JvPostingStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string? WorkerId { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? HeartbeatAtUtc { get; set; }
    public Guid? JournalId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostingUserId { get; set; }
    public string? RuleVersion { get; set; }
    public decimal? DebitTotal { get; set; }
    public decimal? CreditTotal { get; set; }
    public string? LastErrorCategory { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessageSafe { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
