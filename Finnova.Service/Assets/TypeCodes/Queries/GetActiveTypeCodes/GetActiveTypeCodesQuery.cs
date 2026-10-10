using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Queries.GetActiveTypeCodes;

    public record GetActiveTypeCodesQuery() : IRequest<List<CodeListItemResponse>>;
