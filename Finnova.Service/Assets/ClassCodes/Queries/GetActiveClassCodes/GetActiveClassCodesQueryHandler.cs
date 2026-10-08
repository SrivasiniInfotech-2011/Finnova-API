using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ClassCodes.Queries.GetActiveClassCodes;

public class GetActiveClassCodesQueryHandler
    : IRequestHandler<GetActiveClassCodesQuery, List<CodeListItemResponse>>
{
    private readonly IClassCodeRepository _repository;
    public GetActiveClassCodesQueryHandler(IClassCodeRepository repository) => _repository = repository;

    public async Task<List<CodeListItemResponse>> Handle(GetActiveClassCodesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);                     // R11.3, R12.1
        return items.Select(x => x.ToListItem()).ToList();
    }
}
