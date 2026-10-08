using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ModelCodes.Queries.GetActiveModelCodes;

public class GetActiveModelCodesQueryHandler
    : IRequestHandler<GetActiveModelCodesQuery, List<CodeListItemResponse>>
{
    private readonly IModelCodeRepository _repository;
    public GetActiveModelCodesQueryHandler(IModelCodeRepository repository) => _repository = repository;

    public async Task<List<CodeListItemResponse>> Handle(GetActiveModelCodesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);                     // R11.3, R12.1
        return items.Select(x => x.ToListItem()).ToList();
    }
}
