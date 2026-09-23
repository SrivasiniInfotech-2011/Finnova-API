using MediatR;
using Finnova.Models.Contracts.Locations;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Locations.Queries.GetLocationsByLevel;

public class GetLocationsByLevelQueryHandler : IRequestHandler<GetLocationsByLevelQuery, List<LocationResponse>>
{
    private readonly ILocationRepository _repository;

    public GetLocationsByLevelQueryHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LocationResponse>> Handle(GetLocationsByLevelQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByLevelAsync(request.Level, cancellationToken);
        return items.ToResponseList();
    }
}
