using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.Assets.Queries.GetAssetById;

public class GetAssetByIdQueryHandler : IRequestHandler<GetAssetByIdQuery, AssetResponse>
{
    private readonly IAssetRepository _repository;
    public GetAssetByIdQueryHandler(IAssetRepository repository) => _repository = repository;

    public async Task<AssetResponse> Handle(GetAssetByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithReferencesAsync(request.Id, ct)
            ?? throw new AssetNotFoundException(request.Id);                 // R5.9 -> 404
        return entity.ToResponse();                                          // R5.8 (nav props loaded)
    }
}
