using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IGLAccountRepository : IRepository<GLAccount>
{
    /// <summary>Paged, AND-combined, company-scoped filter; ordered by GLCode asc (R8.1/8.2/8.6).</summary>
    Task<(List<GLAccount> Items, int Total)> GetPagedAsync(GLAccountFilter filter, CancellationToken ct = default);

    /// <summary>Trimmed exact-match code existence scoped to company; optionally excludes an id (R2.1/2.3).</summary>
    Task<bool> ExistsByCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Returns a matching Inactive (retired) account for retired-code precedence (R2.4).</summary>
    Task<GLAccount?> GetInactiveByCodeAsync(Guid companyId, string code, CancellationToken ct = default);

    /// <summary>Case-insensitive trimmed name existence scoped to company; optionally excludes an id (R2.5).</summary>
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Loads a GL with its branch applicabilities (tracked for edit).</summary>
    Task<GLAccount?> GetWithBranchesAsync(Guid id, CancellationToken ct = default);

    /// <summary>All accounts for a company (for tree assembly), ordered by GLCode asc (R8.6).</summary>
    Task<List<GLAccount>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>True when the GL has one or more Active child accounts (R5.6).</summary>
    Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken ct = default);
}
