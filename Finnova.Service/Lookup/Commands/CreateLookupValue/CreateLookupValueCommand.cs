using MediatR;
using Finnova.Models.Contracts.Lookups;

namespace Finnova.Service.Lookup.Commands.CreateLookupValue;

public record CreateLookupValueCommand(
    string Module,
    string LookupType,
    string Code,
    string Value,
    int DisplayOrder,
    bool? IsActive
) : IRequest<LookupValueResponse>;
