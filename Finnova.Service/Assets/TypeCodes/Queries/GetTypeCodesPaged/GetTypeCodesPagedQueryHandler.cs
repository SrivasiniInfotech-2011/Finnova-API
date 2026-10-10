using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Queries.GetTypeCodesPaged;

public class GetTypeCodesPagedQueryHandler
    : IRequestHandler<GetTypeCodesPagedQuery, PaginatedResponse<TypeCodeResponse>>
{
    private readonly ITypeCodeRepository _repository;
    public GetTypeCodesPagedQueryHandler(ITypeCodeRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<TypeCodeResponse>> Handle(
        GetTypeCodesPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Page, request.PageSize, ct);              // R2.7, R2.8, R2.11
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R2.9
        return new PaginatedResponse<TypeCodeResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
