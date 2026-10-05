using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IDraweeBankRepository : IRepository<DraweeBank>
{
    /// <summary>Search + page (R8). Term filters BankCode OR BankName (substring, case-insensitive);
    /// null/blank term = no filter. Ordered by BankName asc then BankCode asc (R8.10). Returns page
    /// + total. Includes owned branches/restriction for the page items (challan rules are not loaded).</summary>
    Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Load the bank aggregate (bank + branches + restriction) by id, or null if absent
    /// (R5, R6.7). Challan rules are not part of the aggregate and are not loaded here.</summary>
    Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Case-insensitive, trimmed uniqueness check on BankCode (R2). excludeId supports
    /// self-exclusion on update (R2.4).</summary>
    Task<bool> ExistsByBankCodeAsync(string bankCode, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Standalone challan-rule helpers (R6). List rules for a bank, load/insert/update a
    /// rule, and check per-bank rule-code uniqueness (trimmed, case-insensitive; excludeId for
    /// self-exclusion on update). Rules are accessed only through these helpers, never with the bank.</summary>
    Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid draweeBankId, CancellationToken ct = default);
    Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default);
    Task<bool> ChallanRuleCodeExistsAsync(Guid draweeBankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default);
    Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default);
    Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default);
}
