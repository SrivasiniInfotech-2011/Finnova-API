using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;

namespace Finnova.Service.Assets.ClassCodes.Queries.GetClassCodesPaged;

public record GetClassCodesPagedQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<ClassCodeResponse>>;
