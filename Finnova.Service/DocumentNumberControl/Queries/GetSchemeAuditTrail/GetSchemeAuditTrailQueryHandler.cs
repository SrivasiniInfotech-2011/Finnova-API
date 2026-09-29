using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetSchemeAuditTrail;

public class GetSchemeAuditTrailQueryHandler
    : IRequestHandler<GetSchemeAuditTrailQuery, List<NumberingSchemeAuditEntryResponse>>
{
    private readonly INumberingSchemeAuditRepository _auditRepository;

    public GetSchemeAuditTrailQueryHandler(INumberingSchemeAuditRepository auditRepository)
        => _auditRepository = auditRepository;

    public async Task<List<NumberingSchemeAuditEntryResponse>> Handle(
        GetSchemeAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var entries = await _auditRepository.GetBySchemeIdAsync(request.SchemeId, cancellationToken);
        return entries.Select(e => e.ToResponse()).ToList();   // R6.6, R6.7
    }
}
