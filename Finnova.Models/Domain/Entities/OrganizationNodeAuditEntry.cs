using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one change to an organization node (R6). Written on
/// create, update (rename and/or re-parent), and delete within the same unit of work.
/// Never updated or deleted. Because two fields are editable (Name, ParentId), before/after
/// values are captured per editable field.
/// </summary>
public class OrganizationNodeAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NodeId { get; set; }                        // affected node (R6.1/6.2/6.8)
    public OrganizationNodeAuditAction Action { get; set; } // Create | Update | Delete

    // Editable-field before/after pairs. On Create, Old* are null and New* are the created values.
    // On Delete, New* are null and Old* are the prior values of the deleted node.
    public string? OldName { get; set; }
    public string? NewName { get; set; }
    public Guid? OldParentId { get; set; }                  // absent parent = null (root) (R6.1)
    public Guid? NewParentId { get; set; }

    public string ChangedBy { get; set; } = string.Empty;   // acting admin id from JWT (R6.1/6.2/6.8)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC timestamp (R6.1/6.2/6.8)
}