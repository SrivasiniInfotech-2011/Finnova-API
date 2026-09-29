using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemesPaged;

public class GetNumberingSchemesPagedQueryHandler
    : IRequestHandler<GetNumberingSchemesPagedQuery, PaginatedResponse<NumberingSchemeResponse>>
{
    private readonly INumberingSchemeRepository _repository;

    public GetNumberingSchemesPagedQueryHandler(INumberingSchemeRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<NumberingSchemeResponse>> Handle(
        GetNumberingSchemesPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.Page, request.PageSize, cancellationToken);   // R4.1-R4.3, R4.7

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);         // R4.6

        return new PaginatedResponse<NumberingSchemeResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages); // R4.3
    }
}
