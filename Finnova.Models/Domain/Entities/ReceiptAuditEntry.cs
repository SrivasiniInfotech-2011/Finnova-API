namespace Finnova.Models.Domain.Entities;

/// <summary>Immutable audit row. The repository exposes Add and read only (R11.3).</summary>
public sealed class ReceiptAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public string Action { get; set; } = "Created";
    public string DocNo { get; set; } = string.Empty;
    public long? SequenceNumber { get; set; }                  // logged per the story (R11.1)
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;  // non-sensitive only (R11.6)
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}