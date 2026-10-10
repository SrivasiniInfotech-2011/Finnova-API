using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.Assets.Commands.UpdateAsset;

// No AssetCode field — the existing generated code is preserved (R3.5, R4.8).
public record UpdateAssetCommand(
    Guid Id, Guid ClassCodeId, Guid TypeCodeId, Guid? MakeCodeId, Guid? ModelCodeId,
    string Description, string BookDepreciationCategory, decimal BookDepreciationRate,
    string StockDepreciationCategory, decimal StockDepreciationRate,
    decimal GuidelineLimit, bool IsActive
) : IRequest<AssetResponse>;
