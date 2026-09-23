using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;

namespace Finnova.Service.Lookup.Queries.GetLookupValuesPaged;

/// <summary>
/// Paged, filtered admin listing of lookup values (R1, R4). Any filter may be null;
/// a null filter imposes no constraint on that dimension. Defaults to page 1, size 20 (R4.4).
/// </summary>
public record GetLookupValuesPagedQuery(
    string? Module,
    string? LookupType,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedResponse<LookupValueResponse>>;
