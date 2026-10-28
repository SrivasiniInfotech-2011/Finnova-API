// CREATE Finnova.Repository/Repositories/CustomerReadRepository.cs
using Microsoft.EntityFrameworkCore;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class CustomerReadRepository : RepositoryBase<Customer>, ICustomerReadRepository
{
    public CustomerReadRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(IReadOnlyCollection<Customer> Items, int Total)> SearchAsync(
        CustomerQueryRequest request, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(x => x.Addresses)
            .Include(x => x.IndividualDetails)
            .Include(x => x.NonIndividualDetails)
            .Include(x => x.BankAccounts)
            .Include(x => x.Documents)
            .Include(x => x.Tracks)
            .Include(x => x.FactoringSubLimits)
            .AsQueryable();

        if (request.CompanyId is { } companyId)
            query = query.Where(x => x.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            var code = request.CustomerCode.Trim();
            query = query.Where(x => x.CustomerCode == code);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // EF Core translates Contains to SQL LIKE; default CI collation keeps it case-insensitive.
            query = query.Where(x =>
                x.CustomerCode.Contains(term) ||
                (x.FirstName != null && x.FirstName.Contains(term)) ||
                (x.SecondName != null && x.SecondName.Contains(term)));
        }

        var total = await query.CountAsync(ct);   // total before paging

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;

        var items = await query
            .OrderBy(x => x.CustomerCode)          // deterministic order
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}