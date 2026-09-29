using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.UpdateNumberingScheme;

/// <summary>
/// Handles updating a scheme's editable fields (R3). Validates the template, detects a no-op
/// (no change, no audit), applies the changes, and records exactly one Update audit entry with a
/// before/after JSON snapshot and a human-readable change summary. Never touches Code/DocumentType/
/// Scope/ScopeId or the live sequence state.
/// </summary>
public class UpdateNumberingSchemeCommandHandler
    : IRequestHandler<UpdateNumberingSchemeCommand, NumberingSchemeResponse>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly INumberingSchemeAuditRepository _auditRepository;

    public UpdateNumberingSchemeCommandHandler(
        INumberingSchemeRepository repository,
        INumberingSchemeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<NumberingSchemeResponse> Handle(
        UpdateNumberingSchemeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NumberingSchemeNotFoundException(request.Id);   // R3.5

        var newName = request.Name.Trim();
        var newTemplate = request.FormatTemplate.Trim();

        // Template validity (R3.4).
        var templateErrors = NumberFormatter.Validate(newTemplate);
        if (templateErrors.Count > 0)
            throw new NumberingSchemeValidationException(string.Join(" ", templateErrors));

        // No-op detection: all editable fields equal current (R3.6).
        var unchanged =
            entity.Name == newName &&
            entity.FormatTemplate == newTemplate &&
            entity.Prefix == request.Prefix &&
            entity.Suffix == request.Suffix &&
            entity.SeqIncrement == request.SeqIncrement &&
            entity.SeqPadding == request.SeqPadding &&
            entity.ResetRule == request.ResetRule &&
            entity.IsActive == request.IsActive;
        if (unchanged)
            return entity.ToResponse();

        var before = SchemeAuditSnapshot.Editable(entity);
        var changes = new List<string>();
        if (entity.Name != newName) changes.Add("Name");
        if (entity.FormatTemplate != newTemplate) changes.Add("FormatTemplate");
        if (entity.Prefix != request.Prefix) changes.Add("Prefix");
        if (entity.Suffix != request.Suffix) changes.Add("Suffix");
        if (entity.SeqIncrement != request.SeqIncrement) changes.Add("SeqIncrement");
        if (entity.SeqPadding != request.SeqPadding) changes.Add("SeqPadding");
        if (entity.ResetRule != request.ResetRule) changes.Add($"ResetRule {entity.ResetRule}->{request.ResetRule}");
        if (entity.IsActive != request.IsActive) changes.Add($"IsActive {entity.IsActive}->{request.IsActive}");

        entity.Name = newName;
        entity.FormatTemplate = newTemplate;
        entity.Prefix = request.Prefix;
        entity.Suffix = request.Suffix;
        entity.SeqIncrement = request.SeqIncrement;
        entity.SeqPadding = request.SeqPadding;
        entity.ResetRule = request.ResetRule;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new NumberingSchemeAuditEntry
        {
            SchemeId = entity.Id,
            Action = NumberingSchemeAuditAction.Update,
            OldValues = before,
            NewValues = SchemeAuditSnapshot.Editable(entity),
            Summary = "Updated: " + string.Join(", ", changes),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
