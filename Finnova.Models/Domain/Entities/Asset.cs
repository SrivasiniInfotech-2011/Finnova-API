namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single asset record (Asset Mapping). AssetCode is generated server-side and read-only
/// after creation (R3.2, R3.5). Asset Category -> ClassCode, Asset Type -> TypeCode (R1.5, R1.6).
/// Make/Model are optional references per the taxonomy assumption.
/// </summary>
public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AssetCode { get; set; } = string.Empty;        // max 30, generated, unique (R3.3)
    public string Description { get; set; } = string.Empty;      // Asset Code Description, max 200 (R4.7)

    public Guid ClassCodeId { get; set; }                        // Asset Category -> Class Code (R1.5)
    public Guid TypeCodeId { get; set; }                         // Asset Type -> Type Code (R1.6)
    public Guid? MakeCodeId { get; set; }                        // optional (taxonomy assumption)
    public Guid? ModelCodeId { get; set; }                       // optional (taxonomy assumption)

    public string BookDepreciationCategory { get; set; } = string.Empty;   // max 50
    public decimal BookDepreciationRate { get; set; }                      // 0..100, decimal(5,2) (R4.5)
    public string StockDepreciationCategory { get; set; } = string.Empty;  // max 50
    public decimal StockDepreciationRate { get; set; }                     // 0..100, decimal(5,2) (R4.5)

    public decimal GuidelineLimit { get; set; }                            // >= 0, decimal(18,2) (R4.6)

    public bool IsActive { get; set; } = true;                             // default true (R4.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation (optional; FK ids above are authoritative).
    public ClassCode? ClassCode { get; set; }
    public TypeCode? TypeCode { get; set; }
    public MakeCode? MakeCode { get; set; }
    public ModelCode? ModelCode { get; set; }
}
