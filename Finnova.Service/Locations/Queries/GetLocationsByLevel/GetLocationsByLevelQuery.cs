using MediatR;
using Finnova.Models.Contracts.Locations;

namespace Finnova.Service.Locations.Queries.GetLocationsByLevel;

/// <summary>
/// Returns locations at a specific hierarchy level (flat, for parent dropdowns).
/// </summary>
public record GetLocationsByLevelQuery(int Level) : IRequest<List<LocationResponse>>;
