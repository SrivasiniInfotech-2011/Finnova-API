using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Auth;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Auth.Commands.Login;
using Finnova.Service.Auth.Queries.GetMyPermissions;

namespace Finnova.UAService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(request.UserName, request.Password));
        return result is null
            ? Unauthorized(new { message = "Invalid username or password." })
            : Ok(result);
    }

    [Authorize]
    [HttpGet("me/permissions")]
    public async Task<ActionResult<MyPermissionsResponse>> MyPermissions()
    {
        // User id lives in NameIdentifier (TokenService emits it) with sub as a fallback.
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(idValue, out var userId))
            return Unauthorized(new { message = "Invalid or missing user identity." });

        // Admin (or SystemAdmin) bypasses per-screen gating.
        var isAdmin = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole("SystemAdmin");

        var result = await _mediator.Send(new GetMyPermissionsQuery(userId, isAdmin));
        return Ok(result);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Stateless JWT — the client simply discards the token.
        // Endpoint kept for API symmetry with the UI auth service.
        return NoContent();
    }

    [HttpPost("refresh")]
    public IActionResult Refresh()
    {
        // Refresh tokens are not yet implemented server-side.
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Token refresh is not supported." });
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // Placeholder — password change is not yet implemented.
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Password change is not supported." });
    }
}
