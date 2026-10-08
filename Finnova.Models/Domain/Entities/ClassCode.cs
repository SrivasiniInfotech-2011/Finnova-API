namespace Finnova.Models.Domain.Entities;

/// <summary>A Class Code: asset class / category grouping. India-only, single English Description.</summary>
public class ClassCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;        // max 20, unique within class_codes (R1.2)
    public string Description { get; set; } = string.Empty; // max 100, single English value
    public bool IsActive { get; set; } = true;              // default true (R2.2)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
