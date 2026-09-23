namespace Finnova.Models.Contracts.Lookups;

// Update editable fields (R5.1). Code included to allow rename attempts, which are
// blocked for system-locked rows (R3.2). Module/LookupType/IsSystemLocked are not editable.
public record UpdateLookupValueRequest(
    string Code,
    string Value,
    int DisplayOrder,
    bool IsActive
);
