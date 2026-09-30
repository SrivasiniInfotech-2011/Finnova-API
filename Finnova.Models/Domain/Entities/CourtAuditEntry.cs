using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one configuration change to a court (FINNOVA-13 R5). Written on
/// create and update (including activate/deactivate) within the same unit of work. Never updated or
/// deleted. The editable surface is multi-field, so before/after is captured as a compact JSON
/// snapshot plus a human-readable summary.
/// </summary>
public class CourtAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourtId { get; set; }
    public CourtAuditAction Action { get; set; }             // Create | Update

    public string? OldValues { get; set; }                   // JSON snapshot of changed fields (null on Create)
    public string NewValues { get; set; } = string.Empty;    // JSON snapshot of resulting fields
    public string Summary { get; set; } = string.Empty;      // human-readable change summary

    public string ChangedBy { get; set; } = string.Empty;    // acting admin id from JWT
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}