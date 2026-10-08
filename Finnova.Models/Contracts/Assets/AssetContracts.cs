namespace Finnova.Models.Contracts.Assets;

// Create request MUST NOT include AssetCode — it is generated server-side (R3.2).
public record CreateAssetRequest(
    Guid ClassCodeId,                 // Asset Category (R4.1, R4.4)
    Guid TypeCodeId,                  // Asset Type (R4.4)
    Guid? MakeCodeId,                 // optional (taxonomy assumption)
    Guid? ModelCodeId,                // optional
    string Description,               // Asset Code Description (R4.1, R4.7)
    string BookDepreciationCategory,
    decimal BookDepreciationRate,     // 0..100 <=2dp (R4.5)
    string StockDepreciationCategory,
    decimal StockDepreciationRate,    // 0..100 <=2dp (R4.5)
    decimal GuidelineLimit,           // >= 0 (R4.6)
    bool? IsActive                    // defaults true (R4.2)
);

// Update preserves the existing AssetCode (R3.5) — it is not part of the request.
public record UpdateAssetRequest(
    Guid ClassCodeId,
    Guid TypeCodeId,
    Guid? MakeCodeId,
    Guid? ModelCodeId,
    string Description,
    string BookDepreciationCategory,
    decimal BookDepreciationRate,
    string StockDepreciationCategory,
    decimal StockDepreciationRate,
    decimal GuidelineLimit,
    bool IsActive
);

// Response includes the generated AssetCode (R3.4) and resolved reference codes for grid display.
public record AssetResponse(
    Guid Id,
    string AssetCode,
    string Description,
    Guid ClassCodeId,
    string? ClassCodeValue,           // resolved Class Code string for the grid (R10.1)
    Guid TypeCodeId,
    string? TypeCodeValue,            // resolved Type Code string for the grid (R10.1)
    Guid? MakeCodeId,
    Guid? ModelCodeId,
    string BookDepreciationCategory,
    decimal BookDepreciationRate,
    string StockDepreciationCategory,
    decimal StockDepreciationRate,
    decimal GuidelineLimit,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
