using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

public class UpdateDraweeBankCommandHandler : IRequestHandler<UpdateDraweeBankCommand, DraweeBankResponse>
{
    private readonly IDraweeBankRepository _repository;
    private readonly IDraweeBankAuditRepository _auditRepository;

    public UpdateDraweeBankCommandHandler(
        IDraweeBankRepository repository,
        IDraweeBankAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<DraweeBankResponse> Handle(UpdateDraweeBankCommand request, CancellationToken ct)
    {
        // R3.3 — reject duplicate place codes before touching the store.
        DraweeBankAssembler.EnsureUniquePlaceCodes(request.Branches);

        // R5.4 — unknown id -> not found; nothing changed, no audit.
        var bank = await _repository.GetAggregateByIdAsync(request.Id, ct)
            ?? throw new DraweeBankNotFoundException(request.Id);

        var before = DraweeBankSnapshot.Of(bank);

        // R5.5 — if the submitted name, branch set, and restriction all equal the current values,
        // treat the update as a successful no-op with no audit entry (R7.3).
        var desired = BuildDesired(request);
        var after = DraweeBankSnapshot.Of(desired);
        if (string.Equals(before, after, StringComparison.Ordinal))
            return bank..ToResponse();

        // Apply the diff: new name, replace branch set (removal cascades, R3.7), set/clear restriction.
        bank.BankName = request.BankName;
        bank.Branches = DraweeBankAssembler.ToBranchEntities(bank.Id, request.Branches);
        bank.Restriction = DraweeBankAssembler.ToRestrictionEntity(bank.Id, request.Restriction);
        bank.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(bank, ct);

        // R7.1 — exactly one Update audit entry with before/after snapshots.
        await _auditRepository.AddAsync(new DraweeBankAuditEntry
        {
            DraweeBankId = bank.Id,
            Action = DraweeBankAuditAction.Update,
            BeforeSnapshot = before,
            AfterSnapshot = DraweeBankSnapshot.Of(bank),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return bank.ToResponse();
    }

    // Projects the submitted request into a transient aggregate for the no-op snapshot compare.
    private static Finnova.Models.Domain.Entities.DraweeBank BuildDesired(UpdateDraweeBankCommand request)
    {
        var tmp = new Finnova.Models.Domain.Entities.DraweeBank { BankName = request.BankName };
        tmp.Branches = DraweeBankAssembler.ToBranchEntities(Guid.Empty, request.Branches);
        tmp.Restriction = DraweeBankAssembler.ToRestrictionEntity(Guid.Empty, request.Restriction);
        return tmp;
    }
}
