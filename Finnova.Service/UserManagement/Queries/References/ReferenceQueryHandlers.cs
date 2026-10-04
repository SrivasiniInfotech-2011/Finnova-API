using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.References;

public class GetActiveUsersQueryHandler : IRequestHandler<GetActiveUsersQuery, List<UserGroupMemberResponse>>
{
    private readonly IUserManagementRepository _repository;
    public GetActiveUsersQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<UserGroupMemberResponse>> Handle(GetActiveUsersQuery request, CancellationToken ct)
    {
        var users = await _repository.SearchActiveUsersAsync(request.Search, ct);
        return users.Select(u => u.ToMemberResponse()).ToList();
    }
}

public class GetAccessibleLinesOfBusinessQueryHandler
    : IRequestHandler<GetAccessibleLinesOfBusinessQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetAccessibleLinesOfBusinessQuery request, CancellationToken ct)
        => Task.FromResult(LineOfBusinessCatalog.ActiveLinesOfBusiness()
            .Select(l => new ReferenceItemResponse(l, l)).ToList());
}

public class GetRoleCentersQueryHandler : IRequestHandler<GetRoleCentersQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetRoleCentersQuery request, CancellationToken ct)
    {
        var items = new List<ReferenceItemResponse> { new("ALL", "ALL") };   // R9.5
        items.AddRange(RoleCenterCatalog.RoleCenters().Select(r => new ReferenceItemResponse(r, r)));
        return Task.FromResult(items);
    }
}

public class GetRoleCenterProgramsQueryHandler : IRequestHandler<GetRoleCenterProgramsQuery, List<AccessRightRow>>
{
    public Task<List<AccessRightRow>> Handle(GetRoleCenterProgramsQuery request, CancellationToken ct)
        => Task.FromResult(RoleCenterCatalog.ProgramsFor(request.RoleCenterName)
            .Select(p => new AccessRightRow(
                RoleCodeBuilder.Build(request.RoleCenterName, p),            // R8.4
                request.RoleCenterName, p, false, false, false, false))
            .ToList());
}

public class GetBranchLocationTreeQueryHandler
    : IRequestHandler<GetBranchLocationTreeQuery, List<BranchTreeNodeResponse>>
{
    public Task<List<BranchTreeNodeResponse>> Handle(GetBranchLocationTreeQuery request, CancellationToken ct)
        => Task.FromResult(BranchTreeCatalog.Tree());   // includes ALL (R9.1-9.3)
}

public class GetUserLookupsQueryHandler : IRequestHandler<GetUserLookupsQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetUserLookupsQuery request, CancellationToken ct)
        => Task.FromResult(UserLookupCatalog.For(request.Type));
}
