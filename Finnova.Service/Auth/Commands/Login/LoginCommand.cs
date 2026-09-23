using MediatR;
using Finnova.Models.Contracts.Auth;

namespace Finnova.Service.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse?>;
