using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Auth;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, int ExpiresInSeconds) GenerateToken(User user)
    {
        var fullName = string.Join(' ',
            new[] { user.FirstName, user.MiddleName, user.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, fullName),
        };

        // Emit role claim(s). Admin users also receive the "SystemAdmin" role so they
        // satisfy authorization policies that require it (e.g. Lookup admin endpoints).
        claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
        if (user.Role == Finnova.Models.Domain.Enums.UserRole.Admin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SystemAdmin"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddSeconds(_settings.ExpiresInSeconds);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, _settings.ExpiresInSeconds);
    }
}
