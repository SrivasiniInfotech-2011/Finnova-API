using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.DeactivateGLAccount;

public class DeactivateGLAccountCommandHandler : IRequestHandler<DeactivateGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;
    private readonly IGLBalanceQueryPort _balance;
    private readonly IVoucherReferenceQueryPort _vouchers;
    private readonly IGLReferenceCheckPort _refs;

    public DeactivateGLAccountCommandHandler(
        IGLAccountRepository repository, IGLAuditRepository audit, IGLBalanceQueryPort balance,
        IVoucherReferenceQueryPort vouchers, IGLReferenceCheckPort refs)
    {
        _repository = repository;
        _audit = audit;
        _balance = balance;
        _vouchers = vouchers;
        _refs = refs;
    }

    public async Task<GLAccountResponse> Handle(DeactivateGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

        // R5.13 — deactivate requires Active or Frozen
        if (entity.Status is not (GLStatus.Active or GLStatus.Frozen))
            throw new GLConflictException("Only an Active or Frozen GL account can be deactivated.");

        // R5.3 pre-conditions (R5.4-R5.7)
        if (await _balance.GetCurrentBalanceAsync(entity.Id, ct) != 0m)
            throw new GLConflictException("GL balance must be zero to deactivate.");           // R5.4
        if (await _vouchers.HasOutstandingVouchersAsync(entity.Id, ct))
            throw new GLConflictException("Outstanding vouchers reference the GL.");            // R5.5
        if (await _repository.HasActiveChildrenAsync(entity.Id, ct))
            throw new GLConflictException("Active child GL accounts exist.");                   // R5.6
        if (await _refs.IsMappedInActiveRuleAsync(entity.Id, ct))
            throw new GLConflictException("The GL is mapped in an active rule / product / charge."); // R5.7

        entity.Status = GLStatus.Inactive;                                                     // R5.8
        entity.CheckerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = GLAuditAction.Deactivate,
            OldValues = GLAuditSnapshot.Editable(entity),
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Deactivated GL '{entity.GLCode}'.",
            Remarks = request.Reason?.Trim(),
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
