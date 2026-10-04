using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record for a create/modify of a user, user group, or functional group (FS §9 R14).
/// Written in the same unit of work as the mutation; never updated or deleted. Table "user_management_audit".
/// </summary>
public class UserManagementAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }                        // the user/group/functional-group id
    public UserConfiguration RecordKind { get; set; }         // which master kind was affected
    public UserManagementAuditAction Action { get; set; }     // Create | Modify

    public string? OldValues { get; set; }                    // JSON snapshot (null on Create)
    public string NewValues { get; set; } = string.Empty;     // JSON snapshot of result
    public string Summary { get; set; } = string.Empty;       // human-readable change summary

    public string ChangedBy { get; set; } = string.Empty;     // acting admin id from JWT (R14.1/14.2)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // transaction date (UTC)
}
