namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single node in the organization hierarchy tree (e.g. Head Office, a Region, a Branch).
/// Self-referencing via ParentId; a root node has ParentId == null and Level 1.
/// India-only platform: one English Name.
/// </summary>
public class OrganizationNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;   // max 20, unique case-insensitive (R1, R2)
    public string Name { get; set; } = string.Empty;   // max 150, single English name (R1, R4)

    public int Level { get; set; }                      // resolved from parent; root = 1 (R1.2, R1.3, R3.3)
    public Guid? ParentId { get; set; }                 // null for a root node (R1.2, R3.6)

    public bool IsActive { get; set; } = true;          // default true (R1.4)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}