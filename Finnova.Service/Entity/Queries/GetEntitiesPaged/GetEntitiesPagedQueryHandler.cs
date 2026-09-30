using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntitiesPaged;

public class GetEntitiesPagedQueryHandler
    : IRequestHandler<GetEntitiesPagedQuery, PaginatedResponse<EntityResponse>>
{
    private readonly IEntityRepository _repository;

    public GetEntitiesPagedQueryHandler(IEntityRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<EntityResponse>> Handle(
        GetEntitiesPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.EntityType, request.Page, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedResponse<EntityResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}