using MediatR;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Nationality.Queries.GetNationalityAuditTrail;

public class GetNationalityAuditTrailQueryHandler
    : IRequestHandler<GetNationalityAuditTrailQuery, List<NationalityAuditEntryResponse>>
{
    private readonly INationalityAuditRepository _auditRepository;

    public GetNationalityAuditTrailQueryHandler(INationalityAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<List<NationalityAuditEntryResponse>> Handle(
        GetNationalityAuditTrailQuery request, CancellationToken cancellationToken)
    {
        // Repository returns entries ordered newest-first, or an empty list when the
        // id is unknown - never an error (R4.4, R4.5).
        var entries = await _auditRepository.GetByNationalityIdAsync(
            request.NationalityId, cancellationToken);

        return entries.Select(e => e.ToResponse()).ToList();
    }
}
