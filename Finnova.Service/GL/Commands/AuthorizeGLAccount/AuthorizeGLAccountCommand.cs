using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.AuthorizeGLAccount;

public record AuthorizeGLAccountCommand(Guid Id, string ActingUser) : IRequest<GLAccountResponse>;
