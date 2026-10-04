using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Auth;

public interface ITokenService
{
    /// <summary>
    /// Generates a signed JWT access token for the given user account.
    /// Returns the token string and its lifetime in seconds.
    /// </summary>
    (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user);
}
