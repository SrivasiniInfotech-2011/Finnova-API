using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.Assets.Commands.CreateAsset;

public class CreateAssetCommandHandler : IRequestHandler<CreateAssetCommand, AssetResponse>
{
    private readonly IAssetRepository _assets;
    private readonly IClassCodeRepository _classes;
    private readonly ITypeCodeRepository _types;
    private readonly IAssetCodeGenerator _generator;

    public CreateAssetCommandHandler(
        IAssetRepository assets, IClassCodeRepository classes,
        ITypeCodeRepository types, IAssetCodeGenerator generator)
    {
        _assets = assets;
        _classes = classes;
        _types = types;
        _generator = generator;
    }

    public async Task<AssetResponse> Handle(CreateAssetCommand request, CancellationToken ct)
    {
        // R4.4 — references must resolve to existing code-master rows.
        var classCode = await _classes.GetByIdAsync(request.ClassCodeId, ct)
            ?? throw new AssetValidationException("Asset Category does not resolve to a Class Code.");
        var typeCode = await _types.GetByIdAsync(request.TypeCodeId, ct)
            ?? throw new AssetValidationException("Asset Type does not resolve to a Type Code.");

        // R3.2 / R3.3 — server-side generation; caller-supplied Asset Code (if any) is ignored.
        var maxSeq = await _assets.GetMaxSequenceForClassAsync(classCode.Code, ct);
        var assetCode = _generator.NextAssetCode(classCode.Code, maxSeq);

        var entity = new Asset
        {
            AssetCode = assetCode,
            Description = request.Description.Trim(),
            ClassCodeId = request.ClassCodeId,
            TypeCodeId = request.TypeCodeId,
            MakeCodeId = request.MakeCodeId,
            ModelCodeId = request.ModelCodeId,
            BookDepreciationCategory = request.BookDepreciationCategory,
            BookDepreciationRate = request.BookDepreciationRate,
            StockDepreciationCategory = request.StockDepreciationCategory,
            StockDepreciationRate = request.StockDepreciationRate,
            GuidelineLimit = request.GuidelineLimit,
            IsActive = request.IsActive ?? true,                     // R4.2 default true
        };
        await _assets.AddAsync(entity, ct);                          // R4.1 (unique index guards R3.6)
        return entity.ToResponse(classCode.Code, typeCode.Code);     // R3.4, R10.1
    }
}
