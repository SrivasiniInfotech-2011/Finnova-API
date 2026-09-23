namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single lookup catalog entry (code-value) scoped to a Module and Lookup Type.
/// Populates dropdowns and classification categories across business modules.
/// India-only platform: a single English display value (no localization).
/// </summary>
public class LookupValue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Module { get; set; } = string.Empty;      // max 50  (e.g. "Origination")
    public string LookupType { get; set; } = string.Empty;  // max 100 (e.g. "MARITAL_STATUS")
    public string Code { get; set; } = string.Empty;        // max 50  (e.g. "WID")
    public string Value { get; set; } = string.Empty;       // max 200 (e.g. "Widowed")

    public int DisplayOrder { get; set; }                   // 0..9999 inclusive
    public bool IsActive { get; set; } = true;              // default true (R2.4)
    public bool IsSystemLocked { get; set; }                // seeded/admin-set, read+enforced here (R3)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
