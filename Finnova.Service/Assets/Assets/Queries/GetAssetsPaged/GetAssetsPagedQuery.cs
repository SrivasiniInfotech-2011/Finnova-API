using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;

namespace Finnova.Service.Assets.Assets.Queries.GetAssetsPaged;

public record GetAssetsPagedQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<AssetResponse>>;
