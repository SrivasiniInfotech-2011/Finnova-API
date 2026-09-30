using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Entity.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Commands.CreateEntity;

public class CreateEntityCommandHandler : IRequestHandler<CreateEntityCommand, EntityResponse>
{
    private readonly IEntityRepository _repository;
    private readonly IEntityAuditRepository _auditRepository;

    public CreateEntityCommandHandler(IEntityRepository repository, IEntityAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<EntityResponse> Handle(CreateEntityCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        // Per-type attribute applicability (R1.6). Runs after FluentValidation, before duplicate check.
        var inapplicable = EntityTypeAttributes.Validate(request.EntityType, request.Attributes);
        if (inapplicable.Count > 0)
            throw new EntityInvalidAttributesException(inapplicable);

        if (await _repository.ExistsByCodeAsync(code, request.EntityType, null, cancellationToken))
            throw new EntityDuplicateCodeException();   // R2.2

        // Reference the entity by its full name to avoid clashing with the Finnova.Service.Entity namespace.
        var entity = new Finnova.Models.Domain.Entities.EntityMaster
        {
            Code = code,
            Name = request.Name.Trim(),
            EntityType = request.EntityType,
            RegistrationIdentifier = request.RegistrationIdentifier?.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            AddressLine = request.AddressLine?.Trim(),
            Attributes = EntityTypeAttributes.Serialize(request.Attributes),
            IsActive = request.IsActive ?? true,   // R1.2
        };

        await _repository.AddAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new EntityAuditEntry
        {
            EntityId = entity.Id,
            Action = EntityAuditAction.Create,
            OldValues = null,
            NewValues = EntityAuditSnapshot.Editable(entity),
            Summary = $"Created {entity.EntityType} entity '{entity.Code}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}