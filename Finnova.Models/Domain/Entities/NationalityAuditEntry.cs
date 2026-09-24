using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one change to a nationality (R4). Written on
/// create and on name update within the same unit of work. Never updated or deleted.
/// </summary>
public class NationalityAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NationalityId { get; set; }             // affected nationality (R4.1/4.2)
    public NationalityAuditAction Action { get; set; }  // Create | Update (R4.1/4.2)

    public string? OldName { get; set; }                // before value; null on Create
    public string NewName { get; set; } = string.Empty; // after value (resolved Name)

    public string ChangedBy { get; set; } = string.Empty; // acting admin id from JWT (R4.1/4.2)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC timestamp (R4.1/4.2)
}
