using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public class UpdateCourtCommandHandler : IRequestHandler<UpdateCourtCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public UpdateCourtCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(UpdateCourtCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R4.4

        var newName = request.Name.Trim();
        var newJurisdiction = request.Jurisdiction.Trim();
        var newLocation = request.Location.Trim();

        // No-op detection: all editable fields equal current (R4.5).
        var unchanged =
            entity.Name == newName &&
            entity.CourtType == request.CourtType &&
            entity.Jurisdiction == newJurisdiction &&
            entity.Location == newLocation &&
            entity.IsActive == request.IsActive;
        if (unchanged)
            return entity.ToResponse();

        var before = CourtAuditSnapshot.Editable(entity);
        var changes = new List<string>();
        if (entity.Name != newName) changes.Add("Name");
        if (entity.CourtType != request.CourtType) changes.Add($"CourtType {entity.CourtType}->{request.CourtType}");
        if (entity.Jurisdiction != newJurisdiction) changes.Add("Jurisdiction");
        if (entity.Location != newLocation) changes.Add("Location");
        if (entity.IsActive != request.IsActive) changes.Add($"IsActive {entity.IsActive}->{request.IsActive}");

        entity.Name = newName;
        entity.CourtType = request.CourtType;
        entity.Jurisdiction = newJurisdiction;
        entity.Location = newLocation;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Update,
            OldValues = before,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = "Updated: " + string.Join(", ", changes),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}