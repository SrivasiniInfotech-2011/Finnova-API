using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.DeactivateGLAccount;

public record DeactivateGLAccountCommand(Guid Id, string? Reason, string ActingUser)
    : IRequest<GLAccountResponse>;
