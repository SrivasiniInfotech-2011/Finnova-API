using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Finnova.Tests.Integration;

/// <summary>
/// Mints dev-signed HS256 JWTs for the SystemAdmin host integration tests.
///
/// The constants MUST match the host configuration in
/// <c>Finnova.SystemAdminService/appsettings.json</c> (the "Jwt" section) so that tokens
/// pass the JwtBearer validation parameters wired in <c>Program.cs</c>:
/// ValidateIssuer / ValidateAudience / ValidateLifetime / ValidateIssuerSigningKey.
///
/// UA login/token issuance does not exist yet (design A2), so these dev-signed tokens are how
/// the host is exercised in tests. The role claim uses <see cref="ClaimTypes.Role"/> because the
/// host sets <c>RoleClaimType = ClaimTypes.Role</c>.
/// </summary>
public static class DevJwt
{
    public const string Issuer = "Finnova";
    public const string Audience = "FinnovaClients";
    public const string SigningKey = "dev-only-signing-key-change-me-min-32-bytes-long-0123456789";

    public const string SystemAdminRole = "SystemAdmin";

    private static SigningCredentials Credentials() =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

    private static string Write(IEnumerable<Claim> claims, DateTime expires)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires,
            signingCredentials: Credentials());

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A valid, unexpired token carrying the given role claim (plus a subject).</summary>
    public static string ForRole(string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        };
        return Write(claims, DateTime.UtcNow.AddMinutes(30));
    }

    /// <summary>Convenience: a valid SystemAdmin token.</summary>
    public static string SystemAdmin() => ForRole(SystemAdminRole);

    /// <summary>A valid, unexpired token for an authenticated caller WITHOUT the SystemAdmin role.</summary>
    public static string NoRole()
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User"),
        };
        return Write(claims, DateTime.UtcNow.AddMinutes(30));
    }

    /// <summary>A correctly-signed token whose lifetime is already in the past.</summary>
    public static string Expired()
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, SystemAdminRole),
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: Credentials());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A garbage/tampered bearer value. Takes a validly-shaped SystemAdmin token and flips its
    /// signature segment so signature validation fails.
    /// </summary>
    public static string Tampered()
    {
        var valid = SystemAdmin();
        var parts = valid.Split('.');
        if (parts.Length == 3)
        {
            // Corrupt the signature segment while keeping the JWT shape (header.payload.signature).
            var sig = parts[2];
            var corrupted = new string(sig.Reverse().ToArray()) + "xx";
            return $"{parts[0]}.{parts[1]}.{corrupted}";
        }
        return "not-a-real-token";
    }
}
