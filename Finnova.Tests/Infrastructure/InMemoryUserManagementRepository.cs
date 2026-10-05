using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>Hand-written in-memory IUserManagementRepository mirroring the EF repo semantics.</summary>
public sealed class InMemoryUserManagementRepository : IUserManagementRepository
{
    public readonly List<UserAccount> Users = new();
    public readonly List<UserGroup> Groups = new();
    public readonly List<FunctionalGroup> Functionals = new();
    public readonly List<UserAccessAssignment> Access = new();
    public readonly List<UserBranchAssociation> Branches = new();
    public readonly List<UserManagementAuditEntry> Audit = new();
    public readonly List<ScreenProgram> Programs = new();
    public readonly List<LineOfBusiness> Lobs = new();

    public Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == code.Trim()));

    public Task<UserAccount?> GetByUserNameAsync(string userName, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.UserName == userName.Trim()));

    public Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u => u.UserCode == code.Trim()));

    public Task AddUserAsync(UserAccount user, CancellationToken ct = default) { Users.Add(user); return Task.CompletedTask; }

    public Task UpdateUserAsync(UserAccount user, CancellationToken ct = default)
    {
        var i = Users.FindIndex(u => u.Id == user.Id);
        if (i >= 0) Users[i] = user;
        return Task.CompletedTask;
    }

    public Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Groups.FirstOrDefault(g => g.UserGroupCode == code.Trim() || g.Id.ToString() == code));

    public Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Groups.Any(g => g.UserGroupCode == code.Trim()));

    public Task AddGroupAsync(UserGroup group, CancellationToken ct = default) { Groups.Add(group); return Task.CompletedTask; }

    public Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        var i = Groups.FindIndex(g => g.Id == group.Id);
        if (i >= 0) Groups[i] = group;
        return Task.CompletedTask;
    }

    public Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Functionals.FirstOrDefault(f => f.FunctionalGroupCode == code.Trim()));

    public Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Functionals.Any(f => f.FunctionalGroupCode == code.Trim()));

    public Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default) { Functionals.Add(fg); return Task.CompletedTask; }

    public Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var set = codes.Select(c => c.Trim()).ToHashSet();
        return Task.FromResult(Users.Where(u => set.Contains(u.UserCode)).ToList());
    }

    public Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default)
    {
        var q = Users.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim();
            q = q.Where(u => u.UserCode.Contains(t) || u.Name.Contains(t));
        }
        return Task.FromResult(q.OrderBy(u => u.UserCode).ToList());
    }

    public Task ReplaceAccessAsync(Guid ownerUserId, Guid lobId,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default)
    {
        Access.RemoveAll(a => a.UserAccountId == ownerUserId && a.LineOfBusinessId == lobId);
        Branches.RemoveAll(b => b.UserAccountId == ownerUserId && b.LineOfBusinessId == lobId);
        Access.AddRange(rows);
        Branches.AddRange(branches);
        return Task.CompletedTask;
    }

    public Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, Guid lobId, CancellationToken ct = default)
        => Task.FromResult((
            Access.Where(a => a.UserAccountId == ownerUserId && a.LineOfBusinessId == lobId).ToList(),
            Branches.Where(b => b.UserAccountId == ownerUserId && b.LineOfBusinessId == lobId).ToList()));

    public Task<List<UserAccessAssignment>> GetAccessAssignmentsByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Access.Where(a => a.UserAccountId == userId).ToList());

    public Task<List<ScreenProgram>> GetActiveProgramsAsync(CancellationToken ct = default)
        => Task.FromResult(Programs.Where(p => p.IsActive).ToList());

    public Task<List<LineOfBusiness>> GetActiveLinesOfBusinessAsync(CancellationToken ct = default)
        => Task.FromResult(Lobs.Where(l => l.IsActive).OrderBy(l => l.LOB_Name).ToList());

    public Task<LineOfBusiness?> GetLineOfBusinessByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Lobs.FirstOrDefault(l => l.Id == id));

    public Task<ScreenProgram?> GetProgramByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Programs.FirstOrDefault(p => p.Id == id));

    public Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var union = Users.Select(u => new UserListItemResult(u.Id, u.UserCode, u.Name, UserConfiguration.User, u.IsActive))
            .Concat(Groups.Select(g => new UserListItemResult(g.Id, g.UserGroupCode, g.Name, UserConfiguration.UserGroup, g.IsActive)))
            .Concat(Functionals.Select(f => new UserListItemResult(f.Id, f.FunctionalGroupCode, f.RoleCenterName, UserConfiguration.FunctionalGroup, f.IsActive)));

        if (kind is not null) union = union.Where(x => x.Kind == kind);
        if (isActive is not null) union = union.Where(x => x.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim();
            union = union.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        var all = union.ToList();
        var items = all.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default) { Audit.Add(entry); return Task.CompletedTask; }

    public Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default)
        => Task.FromResult(Audit.Where(a => a.RecordId == recordId)
            .OrderByDescending(a => a.ChangedAtUtc).ThenByDescending(a => a.Id).ToList());
}
