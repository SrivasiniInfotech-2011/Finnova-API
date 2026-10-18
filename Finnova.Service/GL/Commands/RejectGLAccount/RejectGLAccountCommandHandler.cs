using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.RejectGLAccount;

public class RejectGLAccountCommandHandler : IRequestHandler<RejectGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public RejectGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<GLAccountResponse> Handle(RejectGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        if (entity.Status is not (GLStatus.PendingAuthorization or GLStatus.PendingModification))
            throw new GLConflictException("The GL account is not awaiting authorization.");   // R3.10

        if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
            throw new GLConflictException("Self-authorization is prohibited; maker and checker must differ."); // R3.3

        var wasModification = entity.Status == GLStatus.PendingModification;
        // R4.7 — rejecting a modification restores Active; R3.7 — rejecting a new record -> Rejected
        entity.Status = wasModification ? GLStatus.Active : GLStatus.Rejected;
        entity.CheckerId = request.ActingUser;
        entity.RejectionReason = request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = wasModification ? GLAuditAction.ModReject : GLAuditAction.Reject,
            OldValues = GLAuditSnapshot.Editable(entity),
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Rejected GL '{entity.GLCode}'.",
            Remarks = request.Reason.Trim(),
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
