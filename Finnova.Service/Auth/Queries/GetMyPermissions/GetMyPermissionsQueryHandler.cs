using MediatR;
using Finnova.Models.Contracts.Auth;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Auth.Queries.GetMyPermissions;

public class GetMyPermissionsQueryHandler
    : IRequestHandler<GetMyPermissionsQuery, MyPermissionsResponse>
{
    private readonly IUserManagementRepository _userRepository;

    public GetMyPermissionsQueryHandler(IUserManagementRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<MyPermissionsResponse> Handle(GetMyPermissionsQuery request, CancellationToken cancellationToken)
    {
        var activePrograms = await _userRepository.GetActiveProgramsAsync(cancellationToken);

        // Admins (or any user with admin privileges) bypass per-screen gating: every active
        // program is returned with all four flags set.
        if (request.IsAdmin)
        {
            var all = activePrograms
                .OrderBy(p => p.ProgramName, StringComparer.Ordinal)
                .Select(p => new ScreenPermissionResponse(p.ProgramName, true, true, true, true))
                .ToList();
            return new MyPermissionsResponse(true, all);
        }

        // Program keys that exist in the active registry, resolved by Id -> ProgramName. The emitted
        // ProgramName stays byte-identical to today's gating string the UI resolves against.
        var nameById = activePrograms.ToDictionary(p => p.Id, p => p.ProgramName);

        var rows = await _userRepository.GetAccessAssignmentsByUserAsync(request.UserId, cancellationToken);

        // Collapse the (possibly many) rows per program by OR-ing the four flags, keep only programs
        // present in the active registry, and omit programs the user has no rows for.
        var programs = rows
            .Where(r => nameById.ContainsKey(r.ProgramId))
            .GroupBy(r => r.ProgramId)
            .Select(g => new ScreenPermissionResponse(
                nameById[g.Key],
                g.Any(r => r.CanAdd),
                g.Any(r => r.CanModify),
                g.Any(r => r.CanQuery),
                g.Any(r => r.CanDelete)))
            .OrderBy(p => p.ProgramName, StringComparer.Ordinal)
            .ToList();

        return new MyPermissionsResponse(false, programs);
    }
}
