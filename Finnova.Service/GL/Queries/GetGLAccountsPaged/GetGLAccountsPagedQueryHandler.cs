using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.GL;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

public class GetGLAccountsPagedQueryHandler
    : IRequestHandler<GetGLAccountsPagedQuery, PaginatedResponse<GLAccountResponse>>
{
    private readonly IGLAccountRepository _repository;
    public GetGLAccountsPagedQueryHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<GLAccountResponse>> Handle(
        GetGLAccountsPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(request.Filter, ct);
        var totalPages = (int)Math.Ceiling(total / (double)request.Filter.PageSize);
        return new PaginatedResponse<GLAccountResponse>(
            items.ToResponseList(), total, request.Filter.Page, request.Filter.PageSize, totalPages);
    }
}
