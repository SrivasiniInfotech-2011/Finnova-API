using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

public class GetDraweeBanksPagedQueryHandler
    : IRequestHandler<GetDraweeBanksPagedQuery, PaginatedResponse<DraweeBankResponse>>
{
    private readonly IDraweeBankRepository _repository;

    public GetDraweeBanksPagedQueryHandler(IDraweeBankRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResponse<DraweeBankResponse>> Handle(
        GetDraweeBanksPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.Page, request.PageSize, ct);   // R8.1-R8.3, R8.10

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);   // R8.9

        return new PaginatedResponse<DraweeBankResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);   // R8.4, R8.11
    }
}
