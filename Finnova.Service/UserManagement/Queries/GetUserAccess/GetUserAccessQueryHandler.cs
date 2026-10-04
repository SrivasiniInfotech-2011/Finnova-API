using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAccess;

public class GetUserAccessQueryHandler : IRequestHandler<GetUserAccessQuery, UserAccessResponse>
{
    private readonly IUserManagementRepository _repository;

    public GetUserAccessQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccessResponse> Handle(GetUserAccessQuery request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());

        var (rows, branches) = await _repository.GetAccessAsync(request.Id, request.LineOfBusiness, ct);
        return new UserAccessResponse(
            request.LineOfBusiness,
            rows.Select(r => new AccessRightRow(r.RoleCode, r.RoleCenterName, r.ProgramName,
                r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete)).ToList(),
            branches.Select(b => b.BranchCode).ToList());
    }
}
