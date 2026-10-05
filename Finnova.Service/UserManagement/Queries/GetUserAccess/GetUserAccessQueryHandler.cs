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

        var lob = await _repository.GetLineOfBusinessByIdAsync(request.LineOfBusinessId, ct)
            ?? throw new UserValidationException("The selected Line of Business was not found.");

        // Resolve each row's ProgramId -> ProgramName/DisplayName from the active-programs lookup.
        var programsById = (await _repository.GetActiveProgramsAsync(ct))
            .ToDictionary(p => p.Id);

        var (rows, branches) = await _repository.GetAccessAsync(request.Id, request.LineOfBusinessId, ct);

        return new UserAccessResponse(
            lob.Id,
            lob.LOB_Name,
            rows.Select(r =>
            {
                var hasProgram = programsById.TryGetValue(r.ProgramId, out var program);
                return new AccessRightRow(
                    r.RoleCode, r.RoleCenterName, r.ProgramId,
                    hasProgram ? program!.ProgramName : string.Empty,
                    hasProgram ? program!.DisplayName : string.Empty,
                    r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete);
            }).ToList(),
            branches.Select(b => b.BranchCode).ToList());
    }
}
