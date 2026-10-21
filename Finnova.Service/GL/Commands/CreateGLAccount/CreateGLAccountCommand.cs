using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.CreateGLAccount;

public record CreateGLAccountCommand(CreateGLAccountRequest Request, string ActingUser)
    : IRequest<GLAccountResponse>;
