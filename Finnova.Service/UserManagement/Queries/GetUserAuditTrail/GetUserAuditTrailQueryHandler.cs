using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAuditTrail;

public class GetUserAuditTrailQueryHandler
    : IRequestHandler<GetUserAuditTrailQuery, List<UserManagementAuditEntryResponse>>
{
    private readonly IUserManagementRepository _repository;

    public GetUserAuditTrailQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<UserManagementAuditEntryResponse>> Handle(GetUserAuditTrailQuery request, CancellationToken ct)
    {
        var entries = await _repository.GetAuditAsync(request.RecordId, ct);   // newest-first
        return entries.Select(e => e.ToResponse()).ToList();
    }
}
