using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Entity.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Commands.SetEntityActive;

public class SetEntityActiveCommandHandler : IRequestHandler<SetEntityActiveCommand, EntityResponse>
{
    private readonly IEntityRepository _repository;
    private readonly IEntityAuditRepository _auditRepository;

    public SetEntityActiveCommandHandler(IEntityRepository repository, IEntityAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<EntityResponse> Handle(SetEntityActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(request.Id);   // R6.4

        if (entity.IsActive == request.IsActive)
            return entity.ToResponse();   // no-op, no audit (R6.3)

        var before = EntityAuditSnapshot.Editable(entity);
        var oldActive = entity.IsActive;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new EntityAuditEntry
        {
            EntityId = entity.Id,
            Action = EntityAuditAction.Update,
            OldValues = before,
            NewValues = EntityAuditSnapshot.Editable(entity),
            Summary = $"IsActive {oldActive}->{request.IsActive}",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}