namespace Finnova.Models.Domain.Entities;

public sealed class AppRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class UserAccountRole
{
    public Guid UserAccountId { get; set; }
    public Guid AppRoleId { get; set; }
}