using MediatR;

namespace Finnova.Service.GL.Commands.EnableJvPosting;

public record EnableJvPostingCommand(Guid CompanyId, string ActingUser) : IRequest<Unit>;
