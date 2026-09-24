using MediatR;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Nationality.Commands.UpdateNationalityName;

public class UpdateNationalityNameCommandHandler
    : IRequestHandler<UpdateNationalityNameCommand, NationalityResponse>
{
    private readonly INationalityRepository _repository;
    private readonly INationalityAuditRepository _auditRepository;

    public UpdateNationalityNameCommandHandler(
        INationalityRepository repository,
        INationalityAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<NationalityResponse> Handle(
        UpdateNationalityNameCommand request,
        CancellationToken cancellationToken)
    {
        // R3.4: unknown id -> not found; nothing changed, no audit.
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NationalityNotFoundException(request.Id);

        // R3.5: submitting the current Name is a successful no-op with no audit entry.
        if (string.Equals(entity.Name, request.Name, StringComparison.Ordinal))
            return entity.ToResponse();

        // R3.1: persist the rename and refresh the timestamp.
        var oldName = entity.Name;
        entity.Name = request.Name;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, cancellationToken);

        // R4.1: record exactly one Update audit entry within the same request scope.
        await _auditRepository.AddAsync(new NationalityAuditEntry
        {
            NationalityId = entity.Id,
            Action = NationalityAuditAction.Update,
            OldName = oldName,
            NewName = entity.Name,
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return entity.ToResponse();
    }
}
