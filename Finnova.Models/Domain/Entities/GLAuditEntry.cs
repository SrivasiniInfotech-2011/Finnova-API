using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one governance action against a GL account or system-GL
/// mapping (R10). Mirrors CourtAuditEntry, extended with Remarks and ChangedByRole. Written on
/// the success path only; never updated or deleted. The editable surface is multi-field, so
/// before/after is captured as compact JSON snapshots plus a human-readable summary.
/// </summary>
public class GLAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GLAccountId { get; set; }
    public GLAuditAction Action { get; set; }

    public string? OldValues { get; set; }                   // JSON of changed fields (null on Create)
    public string NewValues { get; set; } = string.Empty;    // JSON of resulting fields (empty on Delete)
    public string Summary { get; set; } = string.Empty;      // human-readable change summary
    public string? Remarks { get; set; }                     // remarks / reason text (<= 1000)

    public string ChangedBy { get; set; } = string.Empty;    // acting user id from JWT
    public string? ChangedByRole { get; set; }               // acting role
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC, ms precision
}
