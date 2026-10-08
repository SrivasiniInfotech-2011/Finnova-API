using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IAssetRepository : IRepository<Asset>
{
    /// <summary>Paged list, CI substring search over AssetCode or Description (R5.2),
    /// ordered by AssetCode asc then Id asc (R5.7); items + total (R5.1).</summary>
    Task<(List<Asset> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Get-by-id including resolved Class/Type navigation for display (R5.8).</summary>
    Task<Asset?> GetByIdWithReferencesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Whether a generated Asset Code already exists (R3.3 safety net).</summary>
    Task<bool> AssetCodeExistsAsync(string assetCode, CancellationToken ct = default);

    /// <summary>Highest existing sequence for a Class-Code prefix, 0 if none (Asset Code gen).</summary>
    Task<int> GetMaxSequenceForClassAsync(string classCode, CancellationToken ct = default);

    /// <summary>True when any asset references the given code-master row (R6.4 in-use guard).</summary>
    Task<bool> IsClassCodeReferencedAsync(Guid classCodeId, CancellationToken ct = default);
    Task<bool> IsTypeCodeReferencedAsync(Guid typeCodeId, CancellationToken ct = default);
    Task<bool> IsMakeCodeReferencedAsync(Guid makeCodeId, CancellationToken ct = default);
    Task<bool> IsModelCodeReferencedAsync(Guid modelCodeId, CancellationToken ct = default);
}
