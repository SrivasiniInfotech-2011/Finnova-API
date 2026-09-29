using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetOrganizationNodesPaged
{
    public class GetOrganizationNodesPagedQueryHandler : IRequestHandler<GetOrganizationNodesPagedQuery,PaginatedResponse<OrganizationNodeResponse>>
    {
        private readonly IOrganizationNodeRepository _repository;

        public GetOrganizationNodesPagedQueryHandler(IOrganizationNodeRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaginatedResponse<OrganizationNodeResponse>> Handle(
            GetOrganizationNodesPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await _repository.GetPagedAsync(
                request.SearchTerm, request.Page, request.PageSize, cancellationToken); // R5.1-R5.3, R5.10

            var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R5.9

            return new PaginatedResponse<OrganizationNodeResponse>(
                items.ToResponseList(), total, request.Page, request.PageSize, totalPages); // R5.4, R5.11
        }
    }

}
