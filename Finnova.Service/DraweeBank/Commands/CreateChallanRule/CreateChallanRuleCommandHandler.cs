using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

public class CreateChallanRuleCommandHandler : IRequestHandler<CreateChallanRuleCommand, ChallanRuleResponse>
{
    private readonly IDraweeBankRepository _repository;
    private readonly IDraweeBankAuditRepository _auditRepository;

    public CreateChallanRuleCommandHandler(
        IDraweeBankRepository repository,
        IDraweeBankAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<ChallanRuleResponse> Handle(CreateChallanRuleCommand request, CancellationToken ct)
    {
        // R6.7 — the owning bank must exist.
        var bank = await _repository.GetByIdAsync(request.DraweeBankId, ct)
            ?? throw new DraweeBankNotFoundException(request.DraweeBankId);

        var code = request.RuleCode.Trim();

        // R6.4 — per-bank rule-code uniqueness (trimmed, case-insensitive).
        if (await _repository.ChallanRuleCodeExistsAsync(bank.Id, code, null, ct))
            throw new ChallanRuleDuplicateCodeException();

        var rule = new ChallanRule
        {
            DraweeBankId = bank.Id,
            RuleCode = code,
            FormatPattern = request.FormatPattern,
            ValidationExpression = request.ValidationExpression,
            RoutingTarget = request.RoutingTarget,
            IsActive = request.IsActive ?? true,     // R6.2 default true
        };
        await _repository.AddChallanRuleAsync(rule, ct);   // R6.1

        // R7.2 — Create audit entry on the owning bank.
        await _auditRepository.AddAsync(new DraweeBankAuditEntry
        {
            DraweeBankId = bank.Id,
            Action = DraweeBankAuditAction.Create,
            BeforeSnapshot = null,
            AfterSnapshot = DraweeBankSnapshot.Of(rule),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return rule.ToResponse();
    }
}
