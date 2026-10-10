namespace Finnova.Models.Domain.Entities;

/// <summary>A Make Code: asset manufacturer/make.</summary>
public class MakeCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;        // max 20, unique within make_codes
    public string Description { get; set; } = string.Empty; // max 100
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
