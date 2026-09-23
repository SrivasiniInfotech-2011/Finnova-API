using MediatR;

namespace Finnova.Service.Locations.Commands.DeleteLocation;

public record DeleteLocationCommand(Guid Id) : IRequest<bool>;
