// CREATE Finnova.Repository/Repositories/CustomerRepository.cs
using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class CustomerRepository : RepositoryBase<Customer>, ICustomerRepository
{
    public CustomerRepository(FinnovaDbContext context) : base(context) { }

    public async Task<Customer?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet
            .Include(x => x.Addresses)
            .Include(x => x.IndividualDetails)
            .Include(x => x.NonIndividualDetails)
            .Include(x => x.BankAccounts)
            .Include(x => x.Documents)
            .Include(x => x.Tracks)
            .Include(x => x.FactoringSubLimits)
            .FirstOrDefaultAsync(x => x.CustomerCode == c, ct);   // tracked for Modify/child writes
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(x => x.CustomerCode == c, ct);
    }

    public async Task<bool> ExistsByIdentityAsync(string normalizedIdentity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedIdentity)) return false;
        var key = normalizedIdentity.Trim();
        // Identity de-dup is keyed on the individual identity reference projected by the
        // aggregate mapper's NormalizeIdentity. Keep this predicate in step with that function.
        return await DbSet.AsNoTracking()
            .Where(x => x.IndividualDetails != null && x.IndividualDetails.IndiaIdentityReference != null)
            .AnyAsync(x => x.IndividualDetails!.IndiaIdentityReference == key, ct);
    }

    // Stages only. The feature unit of work commits Customer + children + audit in one transaction.
    public new async Task AddAsync(Customer customer, CancellationToken ct = default)
        => await DbSet.AddAsync(customer, ct);
}
