namespace Finnova.Models.Contracts.Nationalities;

// Admin-facing response (R1.1 returns id + resolved IsActive).
public record NationalityResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
