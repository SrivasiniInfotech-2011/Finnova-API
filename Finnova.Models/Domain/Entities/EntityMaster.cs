using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A party in the Entity Master (FINNOVA-11) — Dealer / DebtCollector / Insurer / Supplier /
/// Employer. Flat, typed master: one table spans all five Entity Types, with a small per-type
/// attribute bag stored as JSON in <see cref="Attributes"/>. The class is named EntityMaster (not
/// Entity) to avoid clashing with EF conventions and the Finnova.Service/Entity namespace; it maps
/// to the "entities" table. India-only platform: one English Name and plain contact fields.
/// </summary>
public class EntityMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;             // max 20, unique per EntityType (CI, trimmed)
    public string Name { get; set; } = string.Empty;             // max 200, English
    public EntityType EntityType { get; set; }                   // Dealer/DebtCollector/Insurer/Supplier/Employer

    public string? RegistrationIdentifier { get; set; }          // max 50, optional (GST/PAN-style)
    public string? ContactPerson { get; set; }                   // max 100, optional
    public string? Email { get; set; }                           // max 100, optional
    public string? Phone { get; set; }                           // max 20, optional
    public string? AddressLine { get; set; }                     // max 200, optional
    public string? Attributes { get; set; }                      // max 2000, JSON bag of per-type attributes

    public bool IsActive { get; set; } = true;                   // default true (R1.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}