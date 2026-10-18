using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.AuthorizeGLAccount;

public class AuthorizeGLAccountCommandHandler : IRequestHandler<AuthorizeGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public AuthorizeGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<GLAccountResponse> Handle(AuthorizeGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        // R3.10 — must be awaiting authorization
        if (entity.Status is not (GLStatus.PendingAuthorization or GLStatus.PendingModification))
            throw new GLConflictException("The GL account is not awaiting authorization.");

        // R3.3 / R4.6 — self-authorization prohibited (PROP-MAKER-NE-CHECKER)
        if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
            throw new GLConflictException("Self-authorization is prohibited; maker and checker must differ.");

        var wasModification = entity.Status == GLStatus.PendingModification;
        entity.Status = GLStatus.Active;
        entity.CheckerId = request.ActingUser;
        if (wasModification) entity.AuthorizedVersionNumber += 1;                     // R4.5
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = wasModification ? GLAuditAction.ModApprove : GLAuditAction.Approve,
            OldValues = GLAuditSnapshot.Editable(entity),
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Authorized GL '{entity.GLCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
