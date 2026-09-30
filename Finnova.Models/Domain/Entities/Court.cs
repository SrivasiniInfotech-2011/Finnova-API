using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A court in the Court Master (FINNOVA-13). Flat master with reference attributes. India-only
/// platform: one English Name.
/// </summary>
public class Court
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;         // max 20, unique (case-insensitive, trimmed)
    public string Name { get; set; } = string.Empty;         // max 150, English
    public CourtType CourtType { get; set; }                 // Supreme/High/District/Tribunal/Other
    public string Jurisdiction { get; set; } = string.Empty; // max 100
    public string Location { get; set; } = string.Empty;     // max 100

    public bool IsActive { get; set; } = true;               // default true (R1.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}