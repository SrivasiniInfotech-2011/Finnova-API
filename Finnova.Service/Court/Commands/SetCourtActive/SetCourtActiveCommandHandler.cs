using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.SetCourtActive;

public class SetCourtActiveCommandHandler : IRequestHandler<SetCourtActiveCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public SetCourtActiveCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(SetCourtActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R6.3

        if (entity.IsActive == request.IsActive)
            return entity.ToResponse();   // no-op, no audit

        var before = CourtAuditSnapshot.Editable(entity);
        var oldActive = entity.IsActive;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Update,
            OldValues = before,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = $"IsActive {oldActive}->{request.IsActive}",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}