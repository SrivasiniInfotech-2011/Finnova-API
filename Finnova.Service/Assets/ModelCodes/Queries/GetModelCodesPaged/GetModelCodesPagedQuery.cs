using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using MediatR;

namespace Finnova.Service.Assets.ModelCodes.Queries.GetModelCodesPaged;

public record GetModelCodesPagedQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<ModelCodeResponse>>;
