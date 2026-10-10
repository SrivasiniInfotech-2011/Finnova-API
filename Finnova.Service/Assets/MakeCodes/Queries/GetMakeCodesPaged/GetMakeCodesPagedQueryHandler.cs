using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Queries.GetMakeCodesPaged;

public class GetMakeCodesPagedQueryHandler
    : IRequestHandler<GetMakeCodesPagedQuery, PaginatedResponse<MakeCodeResponse>>
{
    private readonly IMakeCodeRepository _repository;
    public GetMakeCodesPagedQueryHandler(IMakeCodeRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<MakeCodeResponse>> Handle(
        GetMakeCodesPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Page, request.PageSize, ct);              // R2.7, R2.8, R2.11
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize); // R2.9
        return new PaginatedResponse<MakeCodeResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
