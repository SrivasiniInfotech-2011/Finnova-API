namespace Finnova.Models.Contracts.Auth;

public record LoginRequest(
    string UserName,
    string Password
);
