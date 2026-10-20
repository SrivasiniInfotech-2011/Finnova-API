using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Internal;
using Finnova.Service.Document.Ports;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;
using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

namespace Finnova.Service.Document.Commands.UpdateApplicabilityRules;

public record UpdateApplicabilityRulesCommand(
    Guid Id, IReadOnlyList<ApplicabilityRuleRequest> Rules, string ActingUser)
    : IRequest<DocumentResponse>;

public class UpdateApplicabilityRulesCommandHandler
    : IRequestHandler<UpdateApplicabilityRulesCommand, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentAuditRepository _audit;
    private readonly ILookupValidationPort _lookups;

    public UpdateApplicabilityRulesCommandHandler(IDocumentRepository repository,
        IDocumentAuditRepository audit, ILookupValidationPort lookups)
    { _repository = repository; _audit = audit; _lookups = lookups; }

    public async Task<DocumentResponse> Handle(UpdateApplicabilityRulesCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();

        if (entity.Status is DocumentStatus.PendingModification
            || (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null))
            throw new DocumentConflictException("A change is already pending authorization."); // R3.5

        // R4.4 — reject duplicate (Constitution, EntityRole) combinations in the submission.
        var seen = new HashSet<(string, string)>();
        foreach (var r in request.Rules)
            if (!seen.Add((r.Constitution, r.EntityRole)))
                throw new ValidationException("Duplicate applicability mapping in submission.");

        // R4.3 — each Constitution/EntityRole must be an ACTIVE lookup value.
        foreach (var r in request.Rules)
        {
            if (!await _lookups.IsActiveLookupAsync("CONSTITUTION", r.Constitution, ct))
                throw new ValidationException($"Constitution '{r.Constitution}' is not an active lookup value.");
            if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", r.EntityRole, ct))
                throw new ValidationException($"Entity Role '{r.EntityRole}' is not an active lookup value.");
        }

        // R4.1 / R4.6 — stage as a pending change; the live matrix keeps serving consumers.
        var pendingRules = request.Rules
            .Select(r => new DocumentApplicabilityRule
            {
                DocumentId = entity.Id, Constitution = r.Constitution,
                EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
            }).ToList();
        entity.PendingValuesJson = DocumentPendingPayload.ForApplicability(pendingRules);
        entity.Status = entity.Status == DocumentStatus.Active
            ? DocumentStatus.PendingModification : DocumentStatus.PendingApproval;
        entity.MakerId = request.ActingUser;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        await _audit.AddAsync(new DocAudit
        {
            DocumentId = entity.Id,
            Action = DocumentAuditAction.ApplicabilityChange,
            OldValues = DocumentAuditSnapshot.Editable(entity),
            NewValues = entity.PendingValuesJson,
            Summary = $"Submitted applicability change for '{entity.DocCode}'.",
            ChangedBy = request.ActingUser,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return entity.ToResponse();
    }
}

public class UpdateApplicabilityRulesCommandValidator : AbstractValidator<UpdateApplicabilityRulesCommand>
{
    public UpdateApplicabilityRulesCommandValidator()
    {
        RuleFor(x => x.Rules).NotNull().Must(r => r.Count > 0)
            .WithMessage("At least one applicability rule is required.");   // R4.1
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.Constitution).NotEmpty().WithMessage("Constitution is required.");
            rule.RuleFor(r => r.EntityRole).NotEmpty().WithMessage("Entity Role is required.");
        });
    }
}