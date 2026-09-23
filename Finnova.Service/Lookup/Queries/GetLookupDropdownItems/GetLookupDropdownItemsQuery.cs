using MediatR;
using Finnova.Models.Contracts.Lookups;

namespace Finnova.Service.Lookup.Queries.GetLookupDropdownItems;

/// <summary>
/// Returns the active dropdown items (Code + En/Ar labels) for a Module + Lookup Type
/// scope, for consumption by other modules' dropdowns (R6).
/// </summary>
public record GetLookupDropdownItemsQuery(string Module, string LookupType)
    : IRequest<List<LookupDropdownItemResponse>>;
