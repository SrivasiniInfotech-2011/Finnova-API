using MediatR;

namespace Finnova.Service.GL.Commands.DeleteGLAccount;

public record DeleteGLAccountCommand(Guid Id, string ActingUser) : IRequest<Unit>;
