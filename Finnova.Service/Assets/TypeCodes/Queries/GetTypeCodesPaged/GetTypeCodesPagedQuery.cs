using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Queries.GetTypeCodesPaged;

public record GetTypeCodesPagedQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<TypeCodeResponse>>;
