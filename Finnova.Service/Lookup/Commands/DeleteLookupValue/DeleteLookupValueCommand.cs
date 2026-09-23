using MediatR;

namespace Finnova.Service.Lookup.Commands.DeleteLookupValue;

public record DeleteLookupValueCommand(Guid Id) : IRequest<bool>;
