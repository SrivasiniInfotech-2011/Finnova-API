using System.Text.Json;
using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.UpdateGLAccount;

public class UpdateGLAccountCommandHandler : IRequestHandler<UpdateGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _auditRepository;
    private readonly IGLBalanceQueryPort _balance;

    public UpdateGLAccountCommandHandler(
        IGLAccountRepository repository, IGLAuditRepository auditRepository, IGLBalanceQueryPort balance)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _balance = balance;
    }

    public async Task<GLAccountResponse> Handle(UpdateGLAccountCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetWithBranchesAsync(request.Id, ct)
            ?? throw new GLNotFoundException();                                       // R4.10

        // R4.4 — a modification already pending blocks further changes
        if (entity.Status == GLStatus.PendingModification)
            throw new GLConflictException("A modification is already pending authorization.");

        var r = request.Request;
        var currentBranches = entity.BranchApplicabilities.Select(b => b.BranchId).OrderBy(x => x).ToList();
        var proposedBranches = r.ApplicableBranchIds.Distinct().OrderBy(x => x).ToList();

        // R4.11 — identical no-op: leave everything unchanged, no audit
        var isNoOp = entity.AllowManualJV == r.AllowManualJV
            && entity.CostCentreMandatory == r.CostCentreMandatory
            && entity.ProfitCentreMandatory == r.ProfitCentreMandatory
            && string.Equals(entity.ReportingLine, r.ReportingLine, StringComparison.Ordinal)
            && currentBranches.SequenceEqual(proposedBranches);
        if (isNoOp) return entity.ToResponse();

        // R4.2 — structural lock once an authorized posting exists. This request only touches
        // non-structural fields, so if postings exist the versioned modification path still
        // applies; but if the record is not yet Active we edit in place (Draft/Rejected).
        if (entity.Status is GLStatus.Draft or GLStatus.Rejected)
        {
            entity.AllowManualJV = r.AllowManualJV;
            entity.CostCentreMandatory = r.CostCentreMandatory;
            entity.ProfitCentreMandatory = r.ProfitCentreMandatory;
            entity.ReportingLine = r.ReportingLine?.Trim();
            entity.BranchApplicabilities = proposedBranches
                .Select(b => new GLBranchApplicability { GLAccountId = entity.Id, BranchId = b }).ToList();
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await WriteAudit(entity, request.ActingUser, GLAuditAction.Update,
                $"Edited GL '{entity.GLCode}'.", ct);
            return entity.ToResponse();
        }

        if (entity.Status != GLStatus.Active)
            throw new GLConflictException("GL is not in a state that permits modification.");

        // R4.3 — Active: record a single pending version; last authorized config stays in force.
        var payload = JsonSerializer.Serialize(new
        {
            r.AllowManualJV,
            r.CostCentreMandatory,
            r.ProfitCentreMandatory,
            ReportingLine = r.ReportingLine?.Trim(),
            Branches = proposedBranches
        });

        entity.Status = GLStatus.PendingModification;
        entity.MakerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await WriteAudit(entity, request.ActingUser, GLAuditAction.Update,
            $"Submitted modification for GL '{entity.GLCode}'.", ct, remarks: payload);

        return entity.ToResponse();
    }

    private Task WriteAudit(GLAccount e, string actor, GLAuditAction action, string summary,
        CancellationToken ct, string? remarks = null)
        => _auditRepository.AddAsync(new GLAuditEntry
        {
            GLAccountId = e.Id,
            Action = action,
            OldValues = GLAuditSnapshot.Editable(e),
            NewValues = GLAuditSnapshot.Editable(e),
            Summary = summary,
            Remarks = remarks,
            ChangedBy = actor,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);
}
