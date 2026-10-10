using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.MakeCodes.Queries.GetActiveMakeCodes;

public class GetActiveMakeCodesQueryHandler
    : IRequestHandler<GetActiveMakeCodesQuery, List<CodeListItemResponse>>
{
    private readonly IMakeCodeRepository _repository;
    public GetActiveMakeCodesQueryHandler(IMakeCodeRepository repository) => _repository = repository;

    public async Task<List<CodeListItemResponse>> Handle(GetActiveMakeCodesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);                     // R11.3, R12.1
        return items.Select(x => x.ToListItem()).ToList();
    }
}
