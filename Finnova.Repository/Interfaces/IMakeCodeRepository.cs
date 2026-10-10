using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IMakeCodeRepository : IRepository<MakeCode>
{
    Task<(List<MakeCode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<List<MakeCode>> GetActiveAsync(CancellationToken ct = default);
}
