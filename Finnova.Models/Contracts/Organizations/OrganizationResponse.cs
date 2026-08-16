namespace Finnova.Models.Contracts.Organizations;

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
