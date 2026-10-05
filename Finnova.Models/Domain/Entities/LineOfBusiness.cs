namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A Line of Business master row (R7). The LOB is the top-level access scope a user's access
/// assignments and branch associations are grouped under. India-only platform: a single English
/// display name. Maps to table "lines_of_business".
/// </summary>
public class LineOfBusiness
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LOB_Name { get; set; } = string.Empty;        // unique business-line name, English
    public string LOB_Description { get; set; } = string.Empty; // short English description

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
