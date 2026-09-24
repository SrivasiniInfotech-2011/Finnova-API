using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Nationalities;

namespace Finnova.Service.Nationality.Queries.GetNationalitiesPaged;

/// <summary>
/// Paged, filtered admin listing of nationalities (R5). The search term filters
/// Code OR Name (substring, case-insensitive); a null/blank term imposes no filter
/// (R5.1, R5.2, R5.11). Defaults to page 1, size 20 (R5.5, R5.6).
/// </summary>
public record GetNationalitiesPagedQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedResponse<NationalityResponse>>;
