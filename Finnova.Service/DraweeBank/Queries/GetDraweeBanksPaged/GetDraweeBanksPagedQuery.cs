using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

/// <summary>Paged, filtered admin listing (R8). Term filters BankCode OR BankName (substring,
/// case-insensitive); blank term = no filter (R8.1, R8.2). Defaults page 1, size 20 (R8.5, R8.6).</summary>
public record GetDraweeBanksPagedQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedResponse<DraweeBankResponse>>;
