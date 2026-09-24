using MediatR;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using NationalityEntity = Finnova.Models.Domain.Entities.Nationality;
using NationalityAuditEntry = Finnova.Models.Domain.Entities.NationalityAuditEntry;

namespace Finnova.Service.Nationality.Commands.CreateNationality;

public class CreateNationalityCommandHandler : IRequestHandler<CreateNationalityCommand, NationalityResponse>
{
    private readonly INationalityRepository _repository;
    private readonly INationalityAuditRepository _auditRepository;

    public CreateNationalityCommandHandler(
        INationalityRepository repository,
        INationalityAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<NationalityResponse> Handle(CreateNationalityCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        // R2.1/R2.2 — case-insensitive, trimmed uniqueness. Rejecting here writes
        // no record and no audit entry (R4.3).
        if (await _repository.ExistsByCodeAsync(code, null, cancellationToken))
            throw new NationalityDuplicateCodeException();

        var entity = new NationalityEntity
        {
            Code = code,
            Name = request.Name,
            IsActive = request.IsActive ?? true,   // R1.2 default true
        };

        await _repository.AddAsync(entity, cancellationToken);   // R1.1 persisted, R1.6 immediately queryable

        // R4.2 — record exactly one Create audit entry in the same scope.
        var audit = new NationalityAuditEntry
        {
            NationalityId = entity.Id,
            Action = NationalityAuditAction.Create,
            OldName = null,
            NewName = entity.Name,
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        };
        await _auditRepository.AddAsync(audit, cancellationToken);

        return entity.ToResponse();
    }
}
