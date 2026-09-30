using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public record GetCourtsPagedQuery(string? SearchTerm, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<CourtResponse>>;