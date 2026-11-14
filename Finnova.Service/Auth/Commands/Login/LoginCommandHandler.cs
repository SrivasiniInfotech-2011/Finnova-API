using MediatR;
using Finnova.Models.Contracts.Auth;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly IUserManagementRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IAppRoleRepository? _roles;

    public LoginCommandHandler(IUserManagementRepository userRepository, ITokenService tokenService, IAppRoleRepository? roles = null)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _roles = roles;
    }

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);

        // Return null on any failure - the controller maps this to 401 without
        // revealing whether the username exists or the password was wrong.
        if (user is null) return null;
        if (!user.IsActive) return null;
        if (!PasswordHasher.Verify(request.Password, user.PasswordHash)) return null;

        var extraRoles = _roles is null
            ? Array.Empty<string>()
            : (await _roles.GetRoleNamesForUserAsync(user.Id, cancellationToken)).ToArray();

        var (token, expiresIn) = extraRoles.Length == 0
            ? _tokenService.GenerateToken(user)
            : _tokenService.GenerateToken(user, extraRoles);

        return new LoginResponse(
            Token: token,
            RefreshToken: null,
            User: user.ToAuthUser(),
            ExpiresIn: expiresIn);
    }
}