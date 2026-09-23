using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Lookup.Queries.GetLookupValuesPaged;

public class GetLookupValuesPagedQueryHandler
    : IRequestHandler<GetLookupValuesPagedQuery, PaginatedResponse<LookupValueResponse>>
{
    private readonly ILookupRepository _repository;

    public GetLookupValuesPagedQueryHandler(ILookupRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResponse<LookupValueResponse>> Handle(
        GetLookupValuesPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Module, request.LookupType, request.IsActive,
            request.Page, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedResponse<LookupValueResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages); // R4.1
    }
}
