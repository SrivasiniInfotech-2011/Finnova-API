using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptReferenceRepository(FinnovaDbContext db) : IReceiptReferenceRepository
{
    public Task<RefLob?> GetActiveLobAsync(Guid id, CancellationToken ct) =>
        db.LinesOfBusiness.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefLob(x.Id, x.LOB_Name)).FirstOrDefaultAsync(ct);

    public Task<RefBranch?> GetActiveBranchAsync(Guid id, CancellationToken ct) =>
        db.Locations.AsNoTracking().Where(x => x.Id == id && x.IsActive && x.Level == 5)
            .Select(x => new RefBranch(x.Id, x.Code, x.Name)).FirstOrDefaultAsync(ct);

    public Task<RefOfficer?> GetActiveOfficerAsync(Guid id, CancellationToken ct) =>
        db.UserAccounts.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefOfficer(x.Id, x.UserCode, x.Name)).FirstOrDefaultAsync(ct);

    public Task<List<RefOfficer>> GetActiveOfficersAsync(CancellationToken ct) =>
        db.UserAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new RefOfficer(x.Id, x.UserCode, x.Name)).ToListAsync(ct);

    public async Task<RefCustomer?> GetActiveCustomerByCodeAsync(string code, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Addresses)
            .FirstOrDefaultAsync(x => x.CustomerCode == code && x.Status == CustomerLifecycleStatus.Active, ct);
        return c is null ? null : ToCustomerRef(c);
    }

    public async Task<RefCustomer?> GetCustomerByIdAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToCustomerRef(c);
    }

    public async Task<(List<RefCustomer> Items, int Total)> SearchActiveCustomersAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().Where(x => x.Status == CustomerLifecycleStatus.Active);
        var t = term?.Trim();
        if (!string.IsNullOrEmpty(t))
            q = q.Where(x => x.CustomerCode.Contains(t) || (x.FirstName != null && x.FirstName.Contains(t)));
        var total = await q.CountAsync(ct);
        var rows = await q.Include(x => x.Addresses).OrderBy(x => x.CustomerCode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (rows.Select(ToCustomerRef).ToList(), total);
    }

    public async Task<Dictionary<string, RefContract>> GetContractsAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct)
    {
        var list = await db.LoanAccounts.AsNoTracking().Where(x => accountNumbers.Contains(x.AccountNumber))
            .Select(x => new RefContract(x.Id, x.AccountNumber, x.AccountName, x.CustomerId, x.Status, x.ClosedAt))
            .ToListAsync(ct);
        return list.ToDictionary(x => x.AccountNumber, StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<RefContract>> GetLiveContractsOfCustomerAsync(Guid customerId, CancellationToken ct) =>
        db.LoanAccounts.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Status == AccountStatus.Active && x.ClosedAt == null)
            .OrderBy(x => x.AccountNumber)
            .Select(x => new RefContract(x.Id, x.AccountNumber, x.AccountName, x.CustomerId, x.Status, x.ClosedAt))
            .ToListAsync(ct);

    public Task<RefGl?> GetPostableGlByCodeAsync(Guid companyId, string code, CancellationToken ct) =>
        db.GLAccounts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.GLCode == code && x.Status == GLStatus.Active
                        && x.AuthorizedVersionNumber > 0 && x.IsPostable)
            .Select(x => new RefGl(x.Id, x.GLCode)).FirstOrDefaultAsync(ct);

    public Task<RefGl?> GetPostableGlByIdAsync(Guid companyId, Guid glAccountId, CancellationToken ct) =>
        db.GLAccounts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == glAccountId && x.Status == GLStatus.Active
                        && x.AuthorizedVersionNumber > 0 && x.IsPostable)
            .Select(x => new RefGl(x.Id, x.GLCode)).FirstOrDefaultAsync(ct);

    public Task<RefDraweeBank?> GetActiveDraweeBankAsync(Guid id, CancellationToken ct) =>
        db.DraweeBanks.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefDraweeBank(x.Id, x.BankCode, x.BankName)).FirstOrDefaultAsync(ct);

    public Task<List<RefDraweeBank>> GetActiveDraweeBanksAsync(CancellationToken ct) =>
        db.DraweeBanks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BankName).ThenBy(x => x.BankCode)
            .Select(x => new RefDraweeBank(x.Id, x.BankCode, x.BankName)).ToListAsync(ct);

    public async Task<RefDraweeBranch?> GetDraweeBranchAsync(Guid bankId, Guid branchId, CancellationToken ct)
    {
        var b = await db.DraweeBranches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == branchId && x.DraweeBankId == bankId, ct);
        return b is null ? null : ToBranchRef(b);
    }

    public async Task<List<RefDraweeBranch>> GetDraweeBranchesAsync(Guid bankId, DateOnly asOf, CancellationToken ct)
    {
        var rows = await db.DraweeBranches.AsNoTracking().Where(x => x.DraweeBankId == bankId).ToListAsync(ct);
        return rows.Select(ToBranchRef).Where(x => x.IsEffectiveOn(asOf)).OrderBy(x => x.PlaceName).ToList();
    }

    public Task<FaBankAccount?> GetActiveFaBankAccountAsync(Guid id, CancellationToken ct) =>
        db.FaBankAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    public Task<List<FaBankAccount>> GetActiveFaBankAccountsAsync(CancellationToken ct) =>
        db.FaBankAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BankName).ThenBy(x => x.AccountNumber).ToListAsync(ct);

    public Task<List<LookupDropdownItemResponse>> GetActiveComponentsAsync(CancellationToken ct) =>
        db.LookupValues.AsNoTracking()
            .Where(x => x.Module == ReceiptLookup.Module && x.LookupType == ReceiptLookup.ComponentType && x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Value)
            .Select(x => new LookupDropdownItemResponse(x.Code, x.Value)).ToListAsync(ct);

    // ---- mapping helpers ----
    private static RefDraweeBranch ToBranchRef(DraweeBranch b) => new(
        b.Id, b.PlaceCode, b.PlaceName, DateOnly.FromDateTime(b.StartDate),
        b.EndDate is null ? null : DateOnly.FromDateTime(b.EndDate.Value));

    /// <summary>Customer name = the non-empty name parts joined by a space.</summary>
    private static string NameOf(Customer c) => string.Join(' ',
        new[] { c.FirstName, c.SecondName, c.ThirdName, c.FourthName, c.FifthName, c.SixthName }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

    private static RefCustomer ToCustomerRef(Customer c)
    {
        // Communication address first; fall back to Permanent (R13.3).
        var a = c.Addresses.OrderBy(x => x.AddressType == CustomerAddressType.Communication ? 0 : 1).FirstOrDefault();
        return new RefCustomer(c.Id, c.CustomerCode, NameOf(c), c.CompanyId, c.GLAccountId,
            AddressText(a), a?.Telephone, a?.Mobile, a?.Email, a?.Website);
    }

    private static string? AddressText(CustomerAddress? a)
    {
        if (a is null) return null;
        var text = string.Join(", ", new[] { a.HouseNumber, a.BlockNumber, a.WayNumber, a.POBoxNumber, a.City, a.State, a.PostalCode, a.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return text.Length == 0 ? null : text;
    }
}