using MediatR;
using Finnova.Models.Contracts.Auth;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Return null on any failure — the controller maps this to 401 without
        // revealing whether the email exists or the password was wrong.
        if (user is null) return null;
        if (user.Status != UserStatus.Active) return null;
        if (!PasswordHasher.Verify(request.Password, user.PasswordHash)) return null;

        var (token, expiresIn) = _tokenService.GenerateToken(user);

        return new LoginResponse(
            Token: token,
            RefreshToken: null,
            User: user.ToAuthUser(),
            ExpiresIn: expiresIn);
    }
}
