namespace Finnova.Models.Domain.Entities;

/// <summary>Links an active UserAccount to a UserGroup (FS §9 R5.7/5.8). Table "user_group_members".</summary>
public class UserGroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserGroupId { get; set; }                     // FK -> user_groups
    public Guid UserAccountId { get; set; }                   // FK -> user_accounts (must be active, R5.8)
}
