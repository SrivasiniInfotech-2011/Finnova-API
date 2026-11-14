namespace Finnova.Models.Domain.Entities;

/// <summary>Immutable audit row. The repository exposes Add only (no update or delete).</summary>
public sealed class AccountAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AccountId { get; set; }
    public Guid? RequestId { get; set; }
    public string Action { get; set; } = string.Empty;   // Created|KycOutcome|Updated|StatusChanged|KycResolutionSubmitted|KycResolutionApproved|KycResolutionRejected|KycRecheck|Reprocessed|DeadLettered
    public string Source { get; set; } = "User";         // Worker|User
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty; // non-sensitive only
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}