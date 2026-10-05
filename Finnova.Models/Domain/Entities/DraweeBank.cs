namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Aggregate root: a drawee bank reference record. India-only platform: one English BankName.
/// Owns its DraweeBranch collection and an optional RestrictionDetail (R1, R3, R4).
/// </summary>
public class DraweeBank
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BankCode { get; set; } = string.Empty;   // max 20, unique case-insensitive (R1, R2)
    public string BankName { get; set; } = string.Empty;   // max 150, single English name (R1, R5)

    public bool IsActive { get; set; } = true;             // default true (R1.2)

    // Owned children — persisted/loaded with the aggregate.
    public List<DraweeBranch> Branches { get; set; } = new();   // R3
    public RestrictionDetail? Restriction { get; set; }         // optional (R4.4)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
