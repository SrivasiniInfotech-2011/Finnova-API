using MediatR;
using Finnova.Models.Contracts.Locations;

namespace Finnova.Service.Locations.Commands.CreateLocation;

public record CreateLocationCommand(
    string Code,
    string Name,
    int Level,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    double? Latitude,
    double? Longitude
) : IRequest<LocationResponse>;
