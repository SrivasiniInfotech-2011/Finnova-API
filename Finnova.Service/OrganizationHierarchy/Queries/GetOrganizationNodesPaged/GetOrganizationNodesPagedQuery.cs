using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.OrganizationHierarchy;
using MediatR;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetOrganizationNodesPaged;

/// <summary>
/// Query class for retrieving a paginated list of organization nodes. This query allows filtering by a search term and supports pagination through page number and page size parameters.
/// </summary>
/// <param name="SearchTerm">The search term to filter organization nodes.</param>
/// <param name="Page">The page number to retrieve.</param>
/// <param name="PageSize">The number of items per page.</param>
public record GetOrganizationNodesPagedQuery(string? SearchTerm, int Page = 1, int PageSize = 20) : IRequest<PaginatedResponse<OrganizationNodeResponse>>;

