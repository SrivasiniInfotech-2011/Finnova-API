namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Hierarchical location/branch node.
/// Levels: 1=Country, 2=State, 3=City, 4=Zone, 5=Branch.
/// Self-referencing tree via ParentId.
/// </summary>
public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;   // max 20 chars
    public string Name { get; set; } = string.Empty;   // max 100 chars
    public int Level { get; set; }                      // 1..5
    public Guid? ParentId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Optional geo-coordinates
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Location? Parent { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
}
