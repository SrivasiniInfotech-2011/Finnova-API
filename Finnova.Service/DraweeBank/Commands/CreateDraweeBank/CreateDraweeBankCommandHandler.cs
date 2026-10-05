using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

public class CreateDraweeBankCommandHandler : IRequestHandler<CreateDraweeBankCommand, DraweeBankResponse>
{
    private readonly IDraweeBankRepository _repository;
    private readonly IDraweeBankAuditRepository _auditRepository;

    public CreateDraweeBankCommandHandler(
        IDraweeBankRepository repository,
        IDraweeBankAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<DraweeBankResponse> Handle(CreateDraweeBankCommand request, CancellationToken ct)
    {
        var code = request.BankCode.Trim();

        // R3.3 — reject duplicate place codes within the submitted set before any persistence.
        DraweeBankAssembler.EnsureUniquePlaceCodes(request.Branches);

        // R2.1/R2.2 — case-insensitive, trimmed uniqueness. Rejecting here persists nothing
        // and writes no audit entry (R7.3).
        if (await _repository.ExistsByBankCodeAsync(code, null, ct))
            throw new DraweeBankDuplicateCodeException();

        var bank = new DraweeBank
        {
            BankCode = code,
            BankName = request.BankName,
            IsActive = request.IsActive ?? true,          // R1.2 default true
        };
        bank.Branches = DraweeBankAssembler.ToBranchEntities(bank.Id, request.Branches);   // R3.1
        bank.Restriction = DraweeBankAssembler.ToRestrictionEntity(bank.Id, request.Restriction); // R4.1/R4.4

        // R1.1/R1.7 — persist the whole aggregate in one unit of work (all-or-nothing).
        // Challan rules are decoupled: they are NOT created here, only later via CreateChallanRuleCommand.
        await _repository.AddAsync(bank, ct);

        // R7.2 — exactly one Create audit entry; BeforeSnapshot null, AfterSnapshot = created state.
        await _auditRepository.AddAsync(new DraweeBankAuditEntry
        {
            DraweeBankId = bank.Id,
            Action = DraweeBankAuditAction.Create,
            BeforeSnapshot = null,
            AfterSnapshot = DraweeBankSnapshot.Of(bank),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return bank.ToResponse();
    }
}
