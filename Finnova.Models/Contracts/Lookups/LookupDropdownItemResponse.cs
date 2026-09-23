namespace Finnova.Models.Contracts.Lookups;

// Slim consuming-module projection for dropdowns (R6.4: code + display label).
public record LookupDropdownItemResponse(
    string Code,
    string Label
);
