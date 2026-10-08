using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.Assets.Commands.CreateAsset;

// Note: NO AssetCode field; it is generated server-side (R3.2).
public record CreateAssetCommand(
    Guid ClassCodeId, Guid TypeCodeId, Guid? MakeCodeId, Guid? ModelCodeId,
    string Description, string BookDepreciationCategory, decimal BookDepreciationRate,
    string StockDepreciationCategory, decimal StockDepreciationRate,
    decimal GuidelineLimit, bool? IsActive
) : IRequest<AssetResponse>;
