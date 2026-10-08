using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.Assets.Commands.UpdateAsset;

public class UpdateAssetCommandHandler(IAssetRepository assets,
                                       IClassCodeRepository classes,
                                       ITypeCodeRepository types) : IRequestHandler<UpdateAssetCommand, AssetResponse>
{
    private readonly IAssetRepository _assets = assets;
    private readonly IClassCodeRepository _classes = classes;
    private readonly ITypeCodeRepository _types = types;

    public async Task<AssetResponse> Handle(UpdateAssetCommand request, CancellationToken ct)
    {
        var entity = await _assets.GetByIdAsync(request.Id, ct)
            ?? throw new AssetNotFoundException(request.Id);                 // R4.9 -> 404

        // R4.4 — references must still resolve.
        var classCode = await _classes.GetByIdAsync(request.ClassCodeId, ct)
            ?? throw new AssetValidationException("Asset Category does not resolve to a Class Code.");
        var typeCode = await _types.GetByIdAsync(request.TypeCodeId, ct)
            ?? throw new AssetValidationException("Asset Type does not resolve to a Type Code.");

        // R3.5 / R4.8 — AssetCode is preserved (never reassigned on update).
        entity.Description = request.Description.Trim();
        entity.ClassCodeId = request.ClassCodeId;
        entity.TypeCodeId = request.TypeCodeId;
        entity.MakeCodeId = request.MakeCodeId;
        entity.ModelCodeId = request.ModelCodeId;
        entity.BookDepreciationCategory = request.BookDepreciationCategory;
        entity.BookDepreciationRate = request.BookDepreciationRate;
        entity.StockDepreciationCategory = request.StockDepreciationCategory;
        entity.StockDepreciationRate = request.StockDepreciationRate;
        entity.GuidelineLimit = request.GuidelineLimit;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _assets.UpdateAsync(entity, ct);                              // R4.8
        return entity.ToResponse(classCode.Code, typeCode.Code);
    }
}
