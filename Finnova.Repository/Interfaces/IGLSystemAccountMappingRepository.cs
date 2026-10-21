using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public interface IGLSystemAccountMappingRepository : IRepository<GLSystemAccountMapping>
{
    Task<List<GLSystemAccountMapping>> GetActiveForCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<GLSystemAccountMapping?> GetActiveAsync(Guid companyId, SystemGLType type, CancellationToken ct = default);
}
