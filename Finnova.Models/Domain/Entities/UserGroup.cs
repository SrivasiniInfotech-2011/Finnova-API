namespace Finnova.Models.Domain.Entities;

/// <summary>A named collection of active users (FS §9 R5). Maps to table "user_groups".</summary>
public class UserGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserGroupCode { get; set; } = string.Empty; // generated, 4-6, unique (R2.2)
    public string Name { get; set; } = string.Empty;          // max 30, English, mandatory (R5.2/5.3)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
}
