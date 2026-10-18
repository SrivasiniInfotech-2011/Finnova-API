using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Commands.SubmitGLAccount;

public record SubmitGLAccountCommand(Guid Id, string ActingUser) : IRequest<GLAccountResponse>;
