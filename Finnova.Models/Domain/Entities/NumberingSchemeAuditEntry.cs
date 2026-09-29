using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one configuration change to a numbering scheme (FINNOVA-7 R6).
/// Written on create and update (including activate/deactivate) within the same unit of work.
/// Never updated or deleted. A scheme has many editable fields, so before/after is captured as a
/// compact JSON snapshot of the changed fields plus a human-readable summary. Number issuance is
/// NOT audited (R6.4).
/// </summary>
public class NumberingSchemeAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchemeId { get; set; }                          // affected scheme (R6.1/6.2)
    public NumberingSchemeAuditAction Action { get; set; }      // Create | Update

    public string? OldValues { get; set; }                      // JSON snapshot of changed fields (null on Create)
    public string NewValues { get; set; } = string.Empty;       // JSON snapshot of resulting/changed fields
    public string Summary { get; set; } = string.Empty;         // human-readable change summary

    public string ChangedBy { get; set; } = string.Empty;       // acting admin id from JWT
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
