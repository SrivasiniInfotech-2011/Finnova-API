namespace Finnova.Models.Contracts.Auth;

public record LoginRequest(
    string Email,
    string Password
);
