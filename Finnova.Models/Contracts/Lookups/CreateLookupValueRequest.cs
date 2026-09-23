namespace Finnova.Models.Contracts.Lookups;

// Create (R2). IsActive nullable so the service can default it to true (R2.4).
public record CreateLookupValueRequest(
    string Module,
    string LookupType,
    string Code,
    string Value,
    int DisplayOrder,
    bool? IsActive
);
