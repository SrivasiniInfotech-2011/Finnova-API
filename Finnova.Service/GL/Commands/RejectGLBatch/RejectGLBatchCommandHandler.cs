using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.GL.Commands.RejectGLBatch;

public class RejectGLBatchCommandHandler : IRequestHandler<RejectGLBatchCommand, int>
{
    private readonly IGLAccountRepository _repository;
    public RejectGLBatchCommandHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<int> Handle(RejectGLBatchCommand request, CancellationToken ct)
    {
        var pending = await _repository.FindAsync(x => x.Status == GLStatus.PendingAuthorization, ct);
        var count = 0;
        foreach (var e in pending)
        {
            e.Status = GLStatus.Rejected;                               // R9.7
            e.RejectionReason = request.Reason.Trim();
            e.CheckerId = request.ActingUser;
            e.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(e, ct);
            count++;
        }
        return count;
    }
}
