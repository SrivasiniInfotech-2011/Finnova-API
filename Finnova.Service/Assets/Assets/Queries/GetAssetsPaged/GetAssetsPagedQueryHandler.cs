using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.Assets.Queries.GetAssetsPaged;

public class GetAssetsPagedQueryHandler
    : IRequestHandler<GetAssetsPagedQuery, PaginatedResponse<AssetResponse>>
{
    private readonly IAssetRepository _repository;
    public GetAssetsPagedQueryHandler(IAssetRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<AssetResponse>> Handle(
        GetAssetsPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Page, request.PageSize, ct);              // R5.2, R5.6, R5.7
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R5.1
        return new PaginatedResponse<AssetResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
