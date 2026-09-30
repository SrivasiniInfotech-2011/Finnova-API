using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.CreateCourt;

public class CreateCourtCommandHandler : IRequestHandler<CreateCourtCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public CreateCourtCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(CreateCourtCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (await _repository.ExistsByCodeAsync(code, null, cancellationToken))
            throw new CourtDuplicateCodeException();   // R2.2

        var entity = new Finnova.Models.Domain.Entities.Court
        {
            Code = code,
            Name = request.Name.Trim(),
            CourtType = request.CourtType,
            Jurisdiction = request.Jurisdiction.Trim(),
            Location = request.Location.Trim(),
            IsActive = request.IsActive ?? true,   // R1.2
        };

        await _repository.AddAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Create,
            OldValues = null,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = $"Created court '{entity.Code}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}