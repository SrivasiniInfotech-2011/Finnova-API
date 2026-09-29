using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.SetSchemeActive;

/// <summary>
/// Handles activate/deactivate (R7). Not-found -> 404 (R7.3); already in the target state -> no-op
/// with no audit; otherwise toggles IsActive, refreshes the timestamp, and records an Update audit
/// entry capturing the IsActive before/after (R6.3).
/// </summary>
public class SetSchemeActiveCommandHandler
    : IRequestHandler<SetSchemeActiveCommand, NumberingSchemeResponse>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly INumberingSchemeAuditRepository _auditRepository;

    public SetSchemeActiveCommandHandler(
        INumberingSchemeRepository repository,
        INumberingSchemeAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<NumberingSchemeResponse> Handle(
        SetSchemeActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NumberingSchemeNotFoundException(request.Id);   // R7.3

        if (entity.IsActive == request.IsActive)
            return entity.ToResponse();   // no-op, no audit

        var before = SchemeAuditSnapshot.Editable(entity);
        var oldActive = entity.IsActive;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new NumberingSchemeAuditEntry
        {
            SchemeId = entity.Id,
            Action = NumberingSchemeAuditAction.Update,
            OldValues = before,
            NewValues = SchemeAuditSnapshot.Editable(entity),
            Summary = $"IsActive {oldActive}->{request.IsActive}",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
