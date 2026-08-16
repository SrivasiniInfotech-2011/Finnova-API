namespace Finnova.Models.Contracts.Users;

public record CreateUserRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    Guid? OrganizationId
);
