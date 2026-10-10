using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.TypeCodes.Queries.GetActiveTypeCodes;

public class GetActiveTypeCodesQueryHandler
    : IRequestHandler<GetActiveTypeCodesQuery, List<CodeListItemResponse>>
{
    private readonly ITypeCodeRepository _repository;
    public GetActiveTypeCodesQueryHandler(ITypeCodeRepository repository) => _repository = repository;

    public async Task<List<CodeListItemResponse>> Handle(GetActiveTypeCodesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);                     // R11.3, R12.1
        return items.Select(x => x.ToListItem()).ToList();
    }
}
