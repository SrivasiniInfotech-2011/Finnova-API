namespace Finnova.Service.Auth;

/// <summary>
/// JWT configuration, bound from the "Jwt" configuration section.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Finnova";
    public string Audience { get; set; } = "FinnovaClients";

    /// <summary>Signing key (HMAC-SHA256). Must be at least 32 chars in production.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in seconds (default 1 hour).</summary>
    public int ExpiresInSeconds { get; set; } = 3600;
}
