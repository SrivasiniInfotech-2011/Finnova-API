using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Queries.GetActiveMakeCodes;

public record GetActiveMakeCodesQuery() : IRequest<List<CodeListItemResponse>>;
