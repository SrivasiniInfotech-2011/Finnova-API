// CREATE Finnova.Repository/Repositories/CustomerAuditRepository.cs
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class CustomerAuditRepository : RepositoryBase<CustomerAuditEntry>, ICustomerAuditRepository
{
    public CustomerAuditRepository(FinnovaDbContext context) : base(context) { }

    // Stages only; the feature unit of work commits with the Customer aggregate.
    public new async Task AddAsync(CustomerAuditEntry audit, CancellationToken ct = default)
        => await DbSet.AddAsync(audit, ct);
}