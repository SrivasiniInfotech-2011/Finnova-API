using Finnova.Models.Contracts.GL;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAuditTrail;

public class GetGLAuditTrailQueryHandler : IRequestHandler<GetGLAuditTrailQuery, List<GLAuditEntryResponse>>
{
    private readonly IGLAuditRepository _audit;
    public GetGLAuditTrailQueryHandler(IGLAuditRepository audit) => _audit = audit;

    public async Task<List<GLAuditEntryResponse>> Handle(GetGLAuditTrailQuery request, CancellationToken ct)
    {
        var entries = await _audit.GetByGLAccountIdAsync(request.GLAccountId, ct);   // [] when missing (R10.5)
        return entries.Select(e => e.ToResponse()).ToList();
    }
}
