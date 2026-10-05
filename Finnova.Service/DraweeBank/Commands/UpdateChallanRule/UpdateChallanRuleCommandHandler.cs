using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

public class UpdateChallanRuleCommandHandler : IRequestHandler<UpdateChallanRuleCommand, ChallanRuleResponse>
{
    private readonly IDraweeBankRepository _repository;
    private readonly IDraweeBankAuditRepository _auditRepository;

    public UpdateChallanRuleCommandHandler(
        IDraweeBankRepository repository,
        IDraweeBankAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<ChallanRuleResponse> Handle(UpdateChallanRuleCommand request, CancellationToken ct)
    {
        // R6.6 — unknown rule id -> not found; nothing changed.
        var rule = await _repository.GetChallanRuleByIdAsync(request.RuleId, ct)
            ?? throw new ChallanRuleNotFoundException(request.RuleId);

        var before = DraweeBankSnapshot.Of(rule);

        // R6.5 — apply mutable fields and refresh the timestamp.
        rule.FormatPattern = request.FormatPattern;
        rule.ValidationExpression = request.ValidationExpression;
        rule.RoutingTarget = request.RoutingTarget;
        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateChallanRuleAsync(rule, ct);

        // R7.1 — Update audit entry on the owning bank.
        await _auditRepository.AddAsync(new DraweeBankAuditEntry
        {
            DraweeBankId = rule.DraweeBankId,
            Action = DraweeBankAuditAction.Update,
            BeforeSnapshot = before,
            AfterSnapshot = DraweeBankSnapshot.Of(rule),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return rule.ToResponse();
    }
}
