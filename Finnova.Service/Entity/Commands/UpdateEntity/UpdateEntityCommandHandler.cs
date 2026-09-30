using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Entity.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Commands.UpdateEntity;

public class UpdateEntityCommandHandler : IRequestHandler<UpdateEntityCommand, EntityResponse>
{
    private readonly IEntityRepository _repository;
    private readonly IEntityAuditRepository _auditRepository;

    public UpdateEntityCommandHandler(IEntityRepository repository, IEntityAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<EntityResponse> Handle(UpdateEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(request.Id);   // R4.4

        // Per-type attribute applicability against the record's (immutable) Entity Type (R4.3).
        var inapplicable = EntityTypeAttributes.Validate(entity.EntityType, request.Attributes);
        if (inapplicable.Count > 0)
            throw new EntityInvalidAttributesException(inapplicable);

        var newName = request.Name.Trim();
        var newRegistration = request.RegistrationIdentifier?.Trim();
        var newContactPerson = request.ContactPerson?.Trim();
        var newEmail = request.Email?.Trim();
        var newPhone = request.Phone?.Trim();
        var newAddressLine = request.AddressLine?.Trim();
        var newAttributes = EntityTypeAttributes.Serialize(request.Attributes);

        // No-op detection: all editable fields equal current (R4.5).
        var unchanged =
            entity.Name == newName &&
            entity.RegistrationIdentifier == newRegistration &&
            entity.ContactPerson == newContactPerson &&
            entity.Email == newEmail &&
            entity.Phone == newPhone &&
            entity.AddressLine == newAddressLine &&
            entity.Attributes == newAttributes &&
            entity.IsActive == request.IsActive;
        if (unchanged)
            return entity.ToResponse();

        var before = EntityAuditSnapshot.Editable(entity);
        var changes = new List<string>();
        if (entity.Name != newName) changes.Add("Name");
        if (entity.RegistrationIdentifier != newRegistration) changes.Add("RegistrationIdentifier");
        if (entity.ContactPerson != newContactPerson) changes.Add("ContactPerson");
        if (entity.Email != newEmail) changes.Add("Email");
        if (entity.Phone != newPhone) changes.Add("Phone");
        if (entity.AddressLine != newAddressLine) changes.Add("AddressLine");
        if (entity.Attributes != newAttributes) changes.Add("Attributes");
        if (entity.IsActive != request.IsActive) changes.Add($"IsActive {entity.IsActive}->{request.IsActive}");

        entity.Name = newName;
        entity.RegistrationIdentifier = newRegistration;
        entity.ContactPerson = newContactPerson;
        entity.Email = newEmail;
        entity.Phone = newPhone;
        entity.AddressLine = newAddressLine;
        entity.Attributes = newAttributes;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new EntityAuditEntry
        {
            EntityId = entity.Id,
            Action = EntityAuditAction.Update,
            OldValues = before,
            NewValues = EntityAuditSnapshot.Editable(entity),
            Summary = "Updated: " + string.Join(", ", changes),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}