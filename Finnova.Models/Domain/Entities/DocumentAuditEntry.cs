using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record for a document action (R8). One row per persisted action. No update
/// or delete surface is exposed for this table.
/// </summary>
public class DocumentAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }

    public DocumentAuditAction Action { get; set; }
    public string? OldValues { get; set; }     // compact JSON snapshot (null for Create)
    public string? NewValues { get; set; }     // compact JSON snapshot
    public string Summary { get; set; } = string.Empty;
    public string? Remarks { get; set; }       // rejection remark on Reject (R8.2)

    public string ChangedBy { get; set; } = string.Empty;    // acting user id (from JWT)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}