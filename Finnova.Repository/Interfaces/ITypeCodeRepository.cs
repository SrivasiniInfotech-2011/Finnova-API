namespace Finnova.Repository.Interfaces;

public interface ITypeCodeRepository : IRepository<Finnova.Models.Domain.Entities.TypeCode>
{
    Task<(List<Finnova.Models.Domain.Entities.TypeCode> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<List<Finnova.Models.Domain.Entities.TypeCode>> GetActiveAsync(CancellationToken ct = default);
}
