using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class UserManagementRepository(FinnovaDbContext db)
    : RepositoryBase<UserAccount>(db), IUserManagementRepository
{
    // ---- Users ----
    public Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.UserCode == c, ct);
    }

    public Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Include(x => x.AccessAssignments)
            .Include(x => x.BranchAssociations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return DbSet.AsNoTracking().AnyAsync(x => x.UserCode == c, ct);
    }

    public async Task AddUserAsync(UserAccount user, CancellationToken ct = default)
    {
        await DbSet.AddAsync(user, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateUserAsync(UserAccount user, CancellationToken ct = default)
    {
        DbSet.Update(user);
        await Context.SaveChangesAsync(ct);
    }

    // ---- Groups / functional groups ----
    public Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.UserGroups.AsNoTracking()
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.UserGroupCode == c, ct);
    }

    public Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.UserGroups.AsNoTracking().AnyAsync(x => x.UserGroupCode == c, ct);
    }

    public async Task AddGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        await Context.UserGroups.AddAsync(group, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        Context.UserGroups.Update(group);
        await Context.SaveChangesAsync(ct);
    }

    public Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.FunctionalGroups.AsNoTracking()
            .Include(x => x.Functions)
            .FirstOrDefaultAsync(x => x.FunctionalGroupCode == c, ct);
    }

    public Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.FunctionalGroups.AsNoTracking().AnyAsync(x => x.FunctionalGroupCode == c, ct);
    }

    public async Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default)
    {
        await Context.FunctionalGroups.AddAsync(fg, ct);
        await Context.SaveChangesAsync(ct);
    }

    // ---- Members / references ----
    public async Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var set = codes.Select(c => c.Trim()).ToHashSet();
        return await DbSet.AsNoTracking()
            .Where(x => set.Contains(x.UserCode))
            .ToListAsync(ct);
    }

    public async Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.UserCode.Contains(term) || x.Name.Contains(term));
        }
        return await q.OrderBy(x => x.UserCode).ToListAsync(ct);
    }

    // ---- Access (replace the (owner, LOB) slice atomically) ----
    public async Task ReplaceAccessAsync(Guid ownerUserId, string lob,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default)
    {
        var existingRows = await Context.UserAccessAssignments
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        Context.UserAccessAssignments.RemoveRange(existingRows);

        var existingBranches = await Context.UserBranchAssociations
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        Context.UserBranchAssociations.RemoveRange(existingBranches);

        await Context.UserAccessAssignments.AddRangeAsync(rows, ct);
        await Context.UserBranchAssociations.AddRangeAsync(branches, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, string lob, CancellationToken ct = default)
    {
        var rows = await Context.UserAccessAssignments.AsNoTracking()
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        var branches = await Context.UserBranchAssociations.AsNoTracking()
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        return (rows, branches);
    }

    // ---- List (union of the three kinds) + audit ----
    public async Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Project each source server-side, then materialize. The three kinds live in separate
        // DbSets, and a cross-set Concat/Union is not translatable by every EF provider (notably
        // the InMemory provider used by integration tests throws at execution). Pulling each slim
        // projection first and unioning in memory keeps behavior identical across providers; the
        // projections are narrow (5 scalar columns) so the materialization cost is bounded.
        var users = await DbSet.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.UserCode, x.Name, UserConfiguration.User, x.IsActive)).ToListAsync(ct);
        var groups = await Context.UserGroups.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.UserGroupCode, x.Name, UserConfiguration.UserGroup, x.IsActive)).ToListAsync(ct);
        var functionals = await Context.FunctionalGroups.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.FunctionalGroupCode, x.RoleCenterName, UserConfiguration.FunctionalGroup, x.IsActive)).ToListAsync(ct);

        IEnumerable<UserListItemResult> union = users.Concat(groups).Concat(functionals);

        if (kind is not null)
            union = union.Where(x => x.Kind == kind);
        if (isActive is not null)
            union = union.Where(x => x.IsActive == isActive);          // R13.3
        if (term is not null)
            union = union.Where(x =>                                   // R13.2 (case-insensitive)
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

        var ordered = union
            .OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id) // R13.9 (UserCode asc -> Id asc)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToList();                                                 // empty beyond last page (R13.8)
        return (items, total);
    }

    public async Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default)
    {
        await Context.UserManagementAuditEntries.AddAsync(entry, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default)
        => await Context.UserManagementAuditEntries.AsNoTracking()
            .Where(x => x.RecordId == recordId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // newest-first (R14)
            .ToListAsync(ct);
}
