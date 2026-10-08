using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.Assets.Queries.GetAssetById;

public record GetAssetByIdQuery(Guid Id) : IRequest<AssetResponse>;
