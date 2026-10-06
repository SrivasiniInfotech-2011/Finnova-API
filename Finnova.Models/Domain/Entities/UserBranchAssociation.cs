namespace Finnova.Models.Domain.Entities;

/// <summary>A branch (or ALL) associated with a user/group access scope (FS §9 R9). Table "user_branch_associations".</summary>
public class UserBranchAssociation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserAccountId { get; set; }                  // owner: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)

    public Guid LineOfBusinessId { get; set; }                 // FK -> lines_of_business (branches scoped to the LOB)
    public Guid? LocationId { get; set; }                      // authoritative FK -> locations (null when IsAll); existence-validated on save
    public string BranchCode { get; set; } = string.Empty;     // reference-only display code (location's Code), or "ALL" (R9.3/9.4)
    public bool IsAll { get; set; }                            // true when BranchCode == "ALL" (then LocationId == null)
}
