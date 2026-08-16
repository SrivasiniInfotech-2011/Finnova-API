using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IOrganizationRepository : IRepository<Organization>
{
    Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
