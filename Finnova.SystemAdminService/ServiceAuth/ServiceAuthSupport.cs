using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Finnova.SystemAdminService.ServiceAuth;

public sealed class ServiceAuthOptions
{
    public const string SectionName = "ServiceAuth";
    public Guid SystemUserId { get; set; } = new("00000000-0000-0000-0004-000000000001");
    /// <summary>Secure configuration only (environment / secret store). Never commit a value.</summary>
    public string? SystemUserSecret { get; set; }
    public int TokenMinutes { get; set; } = 10;
}

public sealed class ServiceTokenIssuer
{
    private readonly JwtSettings _jwt;
    private readonly ServiceAuthOptions _options;
    public ServiceTokenIssuer(IOptions<JwtSettings> jwt, IOptions<ServiceAuthOptions> options)
        => (_jwt, _options) = (jwt.Value, options.Value);

    public ServiceTokenResponse Issue(Guid clientId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, clientId.ToString()),
            new(ClaimTypes.Name, "SystemUser"),
            new(ClaimTypes.Role, "ServiceAccount"),
            new("permission", "svc.dcn.issue"),      // least privilege: exactly the two internal operations
            new("permission", "svc.kyc.verify")
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var expires = DateTime.UtcNow.AddMinutes(_options.TokenMinutes);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new ServiceTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), _options.TokenMinutes * 60);
    }
}

/// <summary>Creates the SystemUser and its credential hash at startup. Idempotent; skipped when no secret is configured.</summary>
public sealed class SystemUserSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServiceAuthOptions _options;
    private readonly ILogger<SystemUserSeeder> _log;

    public SystemUserSeeder(IServiceScopeFactory scopes, IOptions<ServiceAuthOptions> options, ILogger<SystemUserSeeder> log)
        => (_scopes, _options, _log) = (scopes, options.Value, log);

    public async Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SystemUserSecret))
        {
            _log.LogWarning("ServiceAuth:SystemUserSecret is not set; the SystemUser service credential was not seeded.");
            return;
        }
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            var id = _options.SystemUserId;
            if (!await db.UserAccounts.AnyAsync(x => x.Id == id, ct))
            {
                db.UserAccounts.Add(new UserAccount
                {
                    Id = id,
                    UserCode = "SYSUSR",
                    UserName = "system.user",
                    Name = "System User",
                    PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),   // unknown password: interactive login is impossible
                    Role = UserRole.User,
                    DateOfJoining = DateTime.UtcNow,
                    Designation = "Automation",
                    Department = "System",
                    UserType = UserType.Corporate,
                    IsActive = false
                });
            }
            var credential = await db.ServiceCredentials.FirstOrDefaultAsync(x => x.ClientId == id, ct);
            if (credential is null)
                db.ServiceCredentials.Add(new ServiceCredential { ClientId = id, SecretHash = PasswordHasher.Hash(_options.SystemUserSecret!), IsActive = true });
            else if (!PasswordHasher.Verify(_options.SystemUserSecret!, credential.SecretHash))
                credential.SecretHash = PasswordHasher.Hash(_options.SystemUserSecret!);   // secret rotation
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SystemUser seeding failed.");   // never fail host start; the token endpoint then rejects everything
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Returns 404 for /internal/** when the request carries forwarded headers (it came through a proxy or the gateway).</summary>
public sealed class InternalEndpointGuardMiddleware
{
    private readonly RequestDelegate _next;
    public InternalEndpointGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/internal", StringComparison.OrdinalIgnoreCase) &&
            (ctx.Request.Headers.ContainsKey("X-Forwarded-For") || ctx.Request.Headers.ContainsKey("X-Forwarded-Host")))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await _next(ctx);
    }
}
