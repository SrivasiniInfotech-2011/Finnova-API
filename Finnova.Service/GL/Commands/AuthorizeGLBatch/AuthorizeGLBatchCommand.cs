using MediatR;

namespace Finnova.Service.GL.Commands.AuthorizeGLBatch;

public record AuthorizeGLBatchCommand(Guid BatchId, string ActingUser) : IRequest<int>;
