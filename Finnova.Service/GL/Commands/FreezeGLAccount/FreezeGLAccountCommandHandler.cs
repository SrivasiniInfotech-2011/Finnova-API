using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.FreezeGLAccount;

public class FreezeGLAccountCommandHandler : IRequestHandler<FreezeGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public FreezeGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<GLAccountResponse> Handle(FreezeGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        // R5.13 — freeze requires Active
        if (entity.Status != GLStatus.Active)
            throw new GLConflictException("Only an Active GL account can be frozen.");

        entity.Status = GLStatus.Frozen;                 // R5.1 — balances/ties preserved (no other field changed)
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = GLAuditAction.Freeze,
            OldValues = GLAuditSnapshot.Editable(entity),
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Froze GL '{entity.GLCode}'.",
            Remarks = request.Reason.Trim(),
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
