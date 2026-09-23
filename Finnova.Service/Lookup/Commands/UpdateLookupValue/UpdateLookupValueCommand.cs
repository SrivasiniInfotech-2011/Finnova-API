using MediatR;
using Finnova.Models.Contracts.Lookups;

namespace Finnova.Service.Lookup.Commands.UpdateLookupValue;

public record UpdateLookupValueCommand(
    Guid Id,
    string Code,
    string Value,
    int DisplayOrder,
    bool IsActive
) : IRequest<LookupValueResponse>;
