using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Commands.CreateGLAccount;

public class CreateGLAccountCommandHandler : IRequestHandler<CreateGLAccountCommand, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _auditRepository;
    private readonly IAccountingCalendarPort _calendar;

    public CreateGLAccountCommandHandler(
        IGLAccountRepository repository,
        IGLAuditRepository auditRepository,
        IAccountingCalendarPort calendar)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _calendar = calendar;
    }

    public async Task<GLAccountResponse> Handle(CreateGLAccountCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var code = r.GLCode.Trim();
        var name = r.Name.Trim();

        // --- Parent resolution + hierarchy derivation (R1.1/1.6/1.7/1.8/1.9/1.10) ---
        GLAccount? parent = null;
        if (r.ParentGLId is not null)
        {
            parent = await _repository.GetByIdAsync(r.ParentGLId.Value, ct)
                ?? throw new GLConflictException("Parent GL must be an active, non-postable header.");
            if (parent.CompanyId != r.CompanyId || parent.Status != GLStatus.Active
                || parent.IsPostable || parent.Level == GLLevel.L4)
                throw new GLConflictException("Parent GL must be an active, non-postable header.");
            if (!GLCodeRules.BeginsWithParentPrefix(code, parent.GLCode))
                throw new GLConflictException("GL code must begin with the parent prefix.");
        }

        var level = NatureRules.LevelFrom(parent?.Level);
        // R1.8 — only L1 may omit a parent; non-L1 must reference one (enforced by LevelFrom + parent check).
        if (r.ParentGLId is null && level != GLLevel.L1)
            throw new GLConflictException("Only L1 accounts may omit a parent.");

        var isPostable = NatureRules.IsPostableFor(level);
        // Nature is derived from parent; for L1 the request's nature is used as the seed.
        var nature = parent?.AccountNature
            ?? (r.GLType == GLType.Tax ? AccountNature.Liability : AccountNature.Asset);
        var normalBalance = NatureRules.NormalBalanceFor(nature);

        // R1.12 — Effective From not in a closed period
        if (await _calendar.IsClosedPeriodAsync(r.CompanyId, r.EffectiveFrom, ct))
            throw new GLConflictException("Effective From cannot fall in a closed accounting period.");

        // --- Uniqueness / non-reuse (R2). Retired-code precedence over plain duplicate (R2.4). ---
        var retired = await _repository.GetInactiveByCodeAsync(r.CompanyId, code, ct);
        if (retired is not null) throw new GLRetiredCodeException();                  // R2.4 (precedence)
        if (await _repository.ExistsByCodeAsync(r.CompanyId, code, null, ct))
            throw new GLDuplicateCodeException();                                     // R2.3
        if (await _repository.ExistsByNameAsync(r.CompanyId, name, null, ct))
            throw new GLDuplicateNameException();                                     // R2.5

        var entity = new GLAccount
        {
            CompanyId = r.CompanyId,
            GLCode = code,
            Name = name,
            Level = level,
            ParentGLId = r.ParentGLId,
            AccountNature = nature,
            NormalBalance = normalBalance,
            GLType = r.GLType,
            SubLedgerType = r.SubLedgerType?.Trim(),
            TaxType = r.TaxType?.Trim(),
            IsPostable = isPostable,
            CurrencyId = r.CurrencyId,
            AllowManualJV = r.AllowManualJV,
            CostCentreMandatory = r.CostCentreMandatory,
            ProfitCentreMandatory = r.ProfitCentreMandatory,
            ReportingLine = r.ReportingLine?.Trim(),
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            Status = GLStatus.Draft,                                                   // R1.1
            MakerId = request.ActingUser,
            AuthorizedVersionNumber = 0,
            BranchApplicabilities = r.ApplicableBranchIds
                .Distinct()
                .Select(b => new GLBranchApplicability { BranchId = b })
                .ToList()
        };

        await _repository.AddAsync(entity, ct);

        await _auditRepository.AddAsync(new GLAuditEntry
        {
            GLAccountId = entity.Id,
            Action = GLAuditAction.Create,
            OldValues = null,
            NewValues = GLAuditSnapshot.Editable(entity),
            Summary = $"Created GL '{entity.GLCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow
        }, ct);

        return entity.ToResponse();
    }
}
