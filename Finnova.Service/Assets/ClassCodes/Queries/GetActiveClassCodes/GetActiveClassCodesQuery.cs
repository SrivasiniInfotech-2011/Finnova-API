using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.ClassCodes.Queries.GetActiveClassCodes;

public record GetActiveClassCodesQuery() : IRequest<List<CodeListItemResponse>>;
