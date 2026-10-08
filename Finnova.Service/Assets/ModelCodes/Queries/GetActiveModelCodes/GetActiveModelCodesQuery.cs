using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.ModelCodes.Queries.GetActiveModelCodes;

public record GetActiveModelCodesQuery() : IRequest<List<CodeListItemResponse>>;
