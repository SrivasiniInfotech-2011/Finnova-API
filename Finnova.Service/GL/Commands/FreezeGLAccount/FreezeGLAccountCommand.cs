using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.FreezeGLAccount;

public record FreezeGLAccountCommand(Guid Id, string Reason, string ActingUser)
    : IRequest<GLAccountResponse>;
