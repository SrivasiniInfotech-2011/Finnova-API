using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DocumentNumberControl;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemesPaged;

/// <summary>Paged, filtered scheme list (R4).</summary>
public record GetNumberingSchemesPagedQuery(string? SearchTerm, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<NumberingSchemeResponse>>;
