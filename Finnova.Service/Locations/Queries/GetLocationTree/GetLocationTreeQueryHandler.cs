using MediatR;
using Finnova.Models.Contracts.Locations;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Locations.Queries.GetLocationTree;

public class GetLocationTreeQueryHandler : IRequestHandler<GetLocationTreeQuery, List<LocationResponse>>
{
    private readonly ILocationRepository _repository;

    public GetLocationTreeQueryHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LocationResponse>> Handle(GetLocationTreeQuery request, CancellationToken cancellationToken)
    {
        var all = await _repository.GetAllFlatAsync(cancellationToken);
        return all.ToTree();
    }
}
