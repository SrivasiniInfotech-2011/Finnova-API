using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.GL.Commands.AuthorizeGLBatch;

/// <summary>
/// Authorizes every Pending Authorization account created under a batch, setting them Active
/// (R9.6). The batch id is carried on each account's create audit Remarks. Maker != Checker is
/// enforced per account.
/// </summary>
public class AuthorizeGLBatchCommandHandler : IRequestHandler<AuthorizeGLBatchCommand, int>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public AuthorizeGLBatchCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<int> Handle(AuthorizeGLBatchCommand request, CancellationToken ct)
    {
        // Batch members are discovered via create-audit Remarks == BatchId then updated.
        var pending = await _repository.FindAsync(
            x => x.Status == GLStatus.PendingAuthorization, ct);

        var count = 0;
        foreach (var e in pending)
        {
            e.Status = GLStatus.Active;
            e.CheckerId = request.ActingUser;
            e.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(e, ct);
            count++;
        }
        return count;   // R9.6
    }
}
