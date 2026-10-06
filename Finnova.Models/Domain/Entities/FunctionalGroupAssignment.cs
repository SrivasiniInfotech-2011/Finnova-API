namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Assigns a functional group to a target user OR user group (FS §9 R6.5/6.6). Exactly one target
/// is set. Supports one-to-many and many-to-one. Table "functional_group_assignments".
/// </summary>
public class FunctionalGroupAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FunctionalGroupId { get; set; }               // FK -> functional_groups
    public Guid? UserAccountId { get; set; }                  // target: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)
}
