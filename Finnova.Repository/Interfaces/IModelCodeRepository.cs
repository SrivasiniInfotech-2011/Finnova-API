using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IModelCodeRepository : IRepository<ModelCode>
{
    Task<(List<ModelCode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<List<ModelCode>> GetActiveAsync(CancellationToken ct = default);
}
