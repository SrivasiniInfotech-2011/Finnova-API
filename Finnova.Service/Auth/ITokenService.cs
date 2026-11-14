using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Auth;

public interface ITokenService
{
    /// <summary>Same as <see cref="GenerateToken(UserAccount)"/> but also emits one role claim per assigned catalog role.</summary>
    (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user, IReadOnlyCollection<string> extraRoles);
}
