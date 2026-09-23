using MediatR;
using Finnova.Models.Contracts.Locations;

namespace Finnova.Service.Locations.Queries.GetLocationTree;

/// <summary>
/// Returns the full location hierarchy as a nested tree.
/// </summary>
public record GetLocationTreeQuery : IRequest<List<LocationResponse>>;
