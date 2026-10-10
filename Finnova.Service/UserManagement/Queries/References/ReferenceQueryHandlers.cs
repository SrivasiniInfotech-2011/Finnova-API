using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
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
    : IRequestHandler<GetAccessibleLinesOfBusinessQuery, List<LineOfBusinessRefResponse>>
{
    private readonly IUserManagementRepository _repository;
    public GetAccessibleLinesOfBusinessQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<LineOfBusinessRefResponse>> Handle(GetAccessibleLinesOfBusinessQuery request, CancellationToken ct)
    {
        var lobs = await _repository.GetActiveLinesOfBusinessAsync(ct);
        return lobs.Select(l => new LineOfBusinessRefResponse(l.Id, l.LOB_Name, l.LOB_Description)).ToList();
    }
}

public class GetProgramsRefQueryHandler : IRequestHandler<GetProgramsRefQuery, List<ProgramRefResponse>>
{
    private readonly IUserManagementRepository _repository;
    public GetProgramsRefQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<ProgramRefResponse>> Handle(GetProgramsRefQuery request, CancellationToken ct)
    {
        var programs = await _repository.GetActiveProgramsAsync(ct);
        return programs
            .OrderBy(p => p.ProgramName, StringComparer.Ordinal)
            .Select(p => new ProgramRefResponse(p.Id, p.ProgramName, p.DisplayName))
            .ToList();
    }
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
    private readonly IUserManagementRepository _repository;
    public GetRoleCenterProgramsQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<AccessRightRow>> Handle(GetRoleCenterProgramsQuery request, CancellationToken ct)
    {
        // Resolve each catalog program NAME to its active programs row so the emitted row carries
        // the FK (ProgramId) and a DisplayName label; names with no active program row are skipped.
        var byName = (await _repository.GetActiveProgramsAsync(ct))
            .ToDictionary(p => p.ProgramName, StringComparer.OrdinalIgnoreCase);

        var rows = new List<AccessRightRow>();
        foreach (var name in RoleCenterCatalog.ProgramsFor(request.RoleCenterName))
        {
            if (!byName.TryGetValue(name, out var program))
                continue;

            rows.Add(new AccessRightRow(
                RoleCodeBuilder.Build(request.RoleCenterName, program.ProgramName),   // R8.4
                request.RoleCenterName,
                program.Id,
                program.ProgramName,
                program.DisplayName,
                false, false, false, false));
        }

        return rows;
    }
}

public class GetBranchLocationTreeQueryHandler
    : IRequestHandler<GetBranchLocationTreeQuery, List<BranchTreeNodeResponse>>
{
    private readonly ILocationRepository _locations;
    public GetBranchLocationTreeQueryHandler(ILocationRepository locations) => _locations = locations;

    public async Task<List<BranchTreeNodeResponse>> Handle(GetBranchLocationTreeQuery request, CancellationToken ct)
    {
        // Build the branch tree from the real locations master so each node carries a Guid Id the
        // UI can send back as the authoritative LocationId. Level 5 => "Branch" (selectable leaf),
        // level 1 => "Location", others => "Region" -- preserving the existing UI level vocabulary.
        var all = await _locations.GetAllFlatAsync(ct);
        var childrenByParent = all
            .GroupBy(l => l.ParentId ?? Guid.Empty)   // null parent => Guid.Empty root sentinel (avoids null dictionary key)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Name, StringComparer.Ordinal).ToList());

        var tree = new List<BranchTreeNodeResponse>
        {
            new(null, "ALL", "ALL", "Location", Array.Empty<BranchTreeNodeResponse>()),   // R9.1-9.3
        };

        var roots = childrenByParent.TryGetValue(Guid.Empty, out var topLevel) ? topLevel : new List<Location>();
        tree.AddRange(roots.Select(r => Build(r, childrenByParent)));
        return tree;
    }

    private static BranchTreeNodeResponse Build(Location node, IReadOnlyDictionary<Guid, List<Location>> childrenByParent)
    {
        var children = childrenByParent.TryGetValue(node.Id, out var kids)
            ? kids.Select(k => Build(k, childrenByParent)).ToList()
            : new List<BranchTreeNodeResponse>();

        var level = node.Level switch { 5 => "Branch", 1 => "Location", _ => "Region" };
        return new BranchTreeNodeResponse(node.Id.ToString(), node.Code, node.Name, level, children);
    }
}

public class GetUserLookupsQueryHandler : IRequestHandler<GetUserLookupsQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetUserLookupsQuery request, CancellationToken ct)
        => Task.FromResult(UserLookupCatalog.For(request.Type));
}
