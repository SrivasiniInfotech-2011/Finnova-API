namespace Finnova.Models.Contracts.Users;

public record UpdateUserRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string Status
);
