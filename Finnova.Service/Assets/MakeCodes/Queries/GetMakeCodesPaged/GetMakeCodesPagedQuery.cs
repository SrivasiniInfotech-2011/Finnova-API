using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Queries.GetMakeCodesPaged;

public record GetMakeCodesPagedQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<MakeCodeResponse>>;
