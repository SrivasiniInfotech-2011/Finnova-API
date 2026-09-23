using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IOrganizationRepository : IRepository<Organization>
{
    Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single company for this product instance.
    /// The product supports exactly one organization.
    /// </summary>
    Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
