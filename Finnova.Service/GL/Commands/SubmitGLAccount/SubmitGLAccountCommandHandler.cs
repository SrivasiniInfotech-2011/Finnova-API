using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.SubmitGLAccount;

public class SubmitGLAccountCommandHandler : IRequestHandler<SubmitGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public SubmitGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<GLAccountResponse> Handle(SubmitGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        // R3.1 / R3.8 — Draft or Rejected -> Pending Authorization
        if (entity.Status is not (GLStatus.Draft or GLStatus.Rejected))
            throw new GLConflictException("Only Draft or Rejected GL accounts can be submitted.");

        entity.Status = GLStatus.PendingAuthorization;
        entity.MakerId = request.ActingUser;
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = GLAuditAction.Submit,
            OldValues = GLAuditSnapshot.Editable(entity),
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Submitted GL '{entity.GLCode}' for authorization.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
