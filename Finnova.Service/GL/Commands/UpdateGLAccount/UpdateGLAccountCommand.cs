using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.UpdateGLAccount;

public record UpdateGLAccountCommand(Guid Id, UpdateGLAccountRequest Request, string ActingUser)
    : IRequest<GLAccountResponse>;
