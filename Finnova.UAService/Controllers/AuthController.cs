using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Auth;
using Finnova.Service.Auth.Commands.Login;

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
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password));
        return result is null
            ? Unauthorized(new { message = "Invalid email or password." })
            : Ok(result);
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
