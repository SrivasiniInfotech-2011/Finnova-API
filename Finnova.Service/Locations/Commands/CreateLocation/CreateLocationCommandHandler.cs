using MediatR;
using Finnova.Models.Contracts.Locations;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Locations.Commands.CreateLocation;

public class CreateLocationCommandHandler : IRequestHandler<CreateLocationCommand, LocationResponse>
{
    private readonly ILocationRepository _repository;

    public CreateLocationCommandHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<LocationResponse> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = new Location
        {
            Code = request.Code,
            Name = request.Name,
            Level = request.Level,
            ParentId = request.ParentId,
            Description = request.Description,
            IsActive = request.IsActive,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
        };

        await _repository.AddAsync(location, cancellationToken);
        return location.ToResponse();
    }
}
