using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Nationality.Queries.GetNationalitiesPaged;

public class GetNationalitiesPagedQueryHandler
    : IRequestHandler<GetNationalitiesPagedQuery, PaginatedResponse<NationalityResponse>>
{
    private readonly INationalityRepository _repository;

    public GetNationalitiesPagedQueryHandler(INationalityRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResponse<NationalityResponse>> Handle(
        GetNationalitiesPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.Page, request.PageSize, cancellationToken); // R5.1-R5.3, R5.10

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R5.9

        return new PaginatedResponse<NationalityResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages); // R5.4, R5.11
    }
}
