using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ClassCodes.Queries.GetClassCodesPaged;

public class GetClassCodesPagedQueryHandler
    : IRequestHandler<GetClassCodesPagedQuery, PaginatedResponse<ClassCodeResponse>>
{
    private readonly IClassCodeRepository _repository;
    public GetClassCodesPagedQueryHandler(IClassCodeRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<ClassCodeResponse>> Handle(
        GetClassCodesPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Page, request.PageSize, ct);              // R2.7, R2.8, R2.11
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R2.9
        return new PaginatedResponse<ClassCodeResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
