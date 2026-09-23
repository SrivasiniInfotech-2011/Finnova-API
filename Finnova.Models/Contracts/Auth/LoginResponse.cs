namespace Finnova.Models.Contracts.Auth;

public record LoginResponse(
    string Token,
    string? RefreshToken,
    AuthUserDto User,
    int ExpiresIn
);
