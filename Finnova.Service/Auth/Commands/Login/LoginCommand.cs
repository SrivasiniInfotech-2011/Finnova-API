using MediatR;
using Finnova.Models.Contracts.Auth;

namespace Finnova.Service.Auth.Commands.Login;

public record LoginCommand(string UserName, string Password) : IRequest<LoginResponse?>;
