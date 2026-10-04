namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single access line: a Program (under a Role Center) within a Line of Business, with the four
/// access-right flags. RoleCode = RoleCenterName + ProgramName (R8.4). Owned by a user OR a group
/// (exactly one owner FK is non-null). Table "user_access_assignments". (R8.6/8.9)
/// </summary>
public class UserAccessAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserAccountId { get; set; }                  // owner: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)

    public string LineOfBusiness { get; set; } = string.Empty; // selected LOB (R7)
    public string RoleCenterName { get; set; } = string.Empty; // "ALL" allowed (R9.5)
    public string ProgramName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;       // RoleCenterName + ProgramName (R8.4)

    public bool CanAdd { get; set; }                           // (R8.6)
    public bool CanModify { get; set; }
    public bool CanQuery { get; set; }
    public bool CanDelete { get; set; }
}
