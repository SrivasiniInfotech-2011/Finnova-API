using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.RejectGLAccount;

public record RejectGLAccountCommand(Guid Id, string Reason, string ActingUser)
    : IRequest<GLAccountResponse>;
