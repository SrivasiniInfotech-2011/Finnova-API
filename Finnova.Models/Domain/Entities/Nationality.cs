namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single nationality reference record used across business modules
/// (e.g. KYC / origination dropdowns). India-only platform: one English Name.
/// </summary>
public class Nationality
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;   // max 10, unique case-insensitive (R1, R2)
    public string Name { get; set; } = string.Empty;   // max 100, single English name (R1, R3)

    public bool IsActive { get; set; } = true;          // default true (R1.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
