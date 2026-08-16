namespace Finnova.Models.Contracts.Users;

public record UserResponse(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string Status,
    Guid? OrganizationId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
