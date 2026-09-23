namespace Finnova.Models.Contracts.Lookups;

// Full admin-facing response (R3.5 exposes IsSystemLocked so UI can disable controls).
public record LookupValueResponse(
    Guid Id,
    string Module,
    string LookupType,
    string Code,
    string Value,
    int DisplayOrder,
    bool IsActive,
    bool IsSystemLocked,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
