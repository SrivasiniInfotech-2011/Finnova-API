using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using MediatR;

namespace Finnova.Service.GL.Commands.DeleteGLAccount;

public class DeleteGLAccountCommandHandler : IRequestHandler<DeleteGLAccountCommand, Unit>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public DeleteGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<Unit> Handle(DeleteGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        // R5.9 — only never-authorized Draft/Rejected records may be physically deleted (R5.10)
        if (entity.Status is not (GLStatus.Draft or GLStatus.Rejected))
            throw new GLConflictException("Authorized GL accounts cannot be physically deleted.");

        // Capture the snapshot BEFORE removal for the audit (NewValues empty on delete, R10.1).
        var old = GLAuditSnapshot.Editable(entity);
        await _repository.DeleteAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = GLAuditAction.Delete,
            OldValues = old,
            NewValues = string.Empty,
            Summary = $"Deleted GL '{entity.GLCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return Unit.Value;
    }
}
