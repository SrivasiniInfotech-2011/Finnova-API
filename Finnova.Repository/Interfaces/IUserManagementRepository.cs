using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Multi-entity data access for User Management. Spans user_accounts, user_groups,
/// functional_groups and their satellites. Backed by RepositoryBase&lt;UserAccount&gt; for the
/// generic CRUD surface, with feature-specific members below.
/// </summary>
public interface IUserManagementRepository
{
    // Users
    Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default);
    Task<UserAccount?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default);
    Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddUserAsync(UserAccount user, CancellationToken ct = default);
    Task UpdateUserAsync(UserAccount user, CancellationToken ct = default);

    // Groups / functional groups
    Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddGroupAsync(UserGroup group, CancellationToken ct = default);
    Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default);
    Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default);

    // Members / references
    Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
    Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default);

    // Access
    Task ReplaceAccessAsync(Guid ownerUserId, Guid lobId,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default);
    Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, Guid lobId, CancellationToken ct = default);
    Task<List<UserAccessAssignment>> GetAccessAssignmentsByUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<ScreenProgram>> GetActiveProgramsAsync(CancellationToken ct = default);

    // Master lookups (lines_of_business / programs)
    Task<List<LineOfBusiness>> GetActiveLinesOfBusinessAsync(CancellationToken ct = default);
    Task<LineOfBusiness?> GetLineOfBusinessByIdAsync(Guid id, CancellationToken ct = default);
    Task<ScreenProgram?> GetProgramByIdAsync(Guid id, CancellationToken ct = default);

    // List + audit
    Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default);
    Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default);
}
