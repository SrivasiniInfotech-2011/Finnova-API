using MediatR;

namespace Finnova.Service.GL.Commands.RejectGLBatch;

public record RejectGLBatchCommand(Guid BatchId, string Reason, string ActingUser) : IRequest<int>;
