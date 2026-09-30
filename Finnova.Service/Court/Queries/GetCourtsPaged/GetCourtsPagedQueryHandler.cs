using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public class GetCourtsPagedQueryHandler
    : IRequestHandler<GetCourtsPagedQuery, PaginatedResponse<CourtResponse>>
{
    private readonly ICourtRepository _repository;

    public GetCourtsPagedQueryHandler(ICourtRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<CourtResponse>> Handle(
        GetCourtsPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.Page, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedResponse<CourtResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}