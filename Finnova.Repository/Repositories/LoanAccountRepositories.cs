using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class LoanAccountRepository : ILoanAccountRepository
{
    private readonly FinnovaDbContext _db;
    public LoanAccountRepository(FinnovaDbContext db) => _db = db;

    public async Task<(List<LoanAccountRow> Rows, int Total)> SearchAsync(LoanAccountFilters f, CancellationToken ct)
    {
        var q = from a in _db.LoanAccounts.AsNoTracking()
                join ap in _db.Applications.AsNoTracking() on a.ApplicationId equals (Guid?)ap.Id
                join c in _db.Customers.AsNoTracking() on a.CustomerId equals (Guid?)c.Id
                where a.Type == AccountType.Loan
                select new
                {
                    Account = a,
                    ApplicationNumber = ap.ApplicationNumber,
                    CustomerCode = c.CustomerCode,
                    HasPending = _db.AccountKycResolutionRequests.Any(r => r.AccountId == a.Id && r.Status == KycResolutionStatus.Pending)
                };

        var term = f.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
            q = q.Where(x => x.ApplicationNumber.ToLower().Contains(term)
                          || x.Account.AccountNumber.ToLower().Contains(term)
                          || x.CustomerCode.ToLower().Contains(term)
                          || (x.Account.CustomerNameSnapshot ?? "").ToLower().Contains(term));
        if (f.KycStatus.HasValue) q = q.Where(x => x.Account.KycStatus == f.KycStatus);
        if (f.KycFlagged.HasValue) q = q.Where(x => x.Account.KycFlagged == f.KycFlagged);
        if (f.AccountStatus.HasValue) q = q.Where(x => x.Account.Status == f.AccountStatus);
        if (f.LineOfBusinessId.HasValue) q = q.Where(x => x.Account.LineOfBusinessId == f.LineOfBusinessId);
        if (f.BranchId.HasValue) q = q.Where(x => x.Account.BranchId == f.BranchId);
        if (!string.IsNullOrWhiteSpace(f.ProductCode)) q = q.Where(x => x.Account.ProductCode == f.ProductCode!.Trim());
        if (!string.IsNullOrWhiteSpace(f.SchemeCode)) q = q.Where(x => x.Account.SchemeCode == f.SchemeCode!.Trim());
        if (f.HasPendingKycResolution.HasValue) q = q.Where(x => x.HasPending == f.HasPendingKycResolution);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.Account.CreatedAt).ThenBy(x => x.Account.AccountNumber)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return (rows.Select(x => new LoanAccountRow(x.Account, x.ApplicationNumber, x.CustomerCode, x.HasPending)).ToList(), total);
    }

    public async Task<Dictionary<Guid, CustomerContact>> GetContactsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken ct)
    {
        if (customerIds.Count == 0) return new();
        var addresses = await _db.CustomerAddresses.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId)).ToListAsync(ct);
        return addresses.GroupBy(x => x.CustomerId).ToDictionary(g => g.Key, g =>
        {
            var a = g.FirstOrDefault(x => x.AddressType == CustomerAddressType.Communication) ?? g.First();
            return new CustomerContact(a.Mobile, a.City);
        });
    }

    public async Task<LoanAccountViewData?> GetViewDataAsync(Guid id, string panCode, string aadhaarCode, CancellationToken ct)
    {
        var account = await _db.LoanAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.Type == AccountType.Loan && a.ApplicationId != null, ct);
        if (account is null) return null;

        var app = await _db.Applications.AsNoTracking().FirstAsync(x => x.Id == account.ApplicationId, ct);
        var customer = await _db.Customers.AsNoTracking()
            .Include(c => c.Addresses).Include(c => c.IndividualDetails)
            .FirstOrDefaultAsync(c => c.Id == account.CustomerId, ct);

        var address = customer?.Addresses.FirstOrDefault(x => x.AddressType == CustomerAddressType.Communication)
                      ?? customer?.Addresses.FirstOrDefault();
        var name = customer is null
            ? account.CustomerNameSnapshot ?? string.Empty
            : string.Join(' ', new[] { customer.FirstName, customer.SecondName, customer.ThirdName, customer.FourthName, customer.FifthName, customer.SixthName }
                .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

        var docs = await _db.CustomerDocumentReferences.AsNoTracking().Where(d => d.CustomerId == account.CustomerId).ToListAsync(ct);
        var aadhaarDefinitionIds = await _db.Documents.AsNoTracking().Where(d => d.IsAadhaarDocument).Select(d => d.Id).ToListAsync(ct);
        string? pan = docs.FirstOrDefault(d => string.Equals(d.DocumentCode, panCode, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.Value))?.Value;
        string? aadhaar = docs.FirstOrDefault(d => (string.Equals(d.DocumentCode, aadhaarCode, StringComparison.OrdinalIgnoreCase)
                                                   || aadhaarDefinitionIds.Contains(d.DocumentDefinitionId)) && !string.IsNullOrWhiteSpace(d.Value))?.Value;
        var identityType = customer?.IndividualDetails?.IndiaIdentityType?.Trim();
        var identityRef = customer?.IndividualDetails?.IndiaIdentityReference;
        if (pan is null && string.Equals(identityType, "PAN", StringComparison.OrdinalIgnoreCase)) pan = identityRef;
        if (aadhaar is null && string.Equals(identityType, "AADHAAR", StringComparison.OrdinalIgnoreCase)) aadhaar = identityRef;

        var history = await _db.AccountKycHistoryEntries.AsNoTracking().Where(h => h.AccountId == id).OrderBy(h => h.OccurredAtUtc).ToListAsync(ct);
        var resolutions = await _db.AccountKycResolutionRequests.AsNoTracking().Where(r => r.AccountId == id).OrderBy(r => r.SubmittedAtUtc).ToListAsync(ct);

        return new LoanAccountViewData(
            account, app.ApplicationNumber,
            new ApplicationFinanceSummary(app.FinanceAmount, app.IrrPercent, app.Tenure, app.RepaymentFrequency.ToString(), app.DisbursementDate),
            new CustomerViewData(customer?.CustomerCode ?? string.Empty, name, address?.Mobile, address?.City),
            pan, aadhaar, history, resolutions);
    }

    public Task<LoanAccount?> GetTrackedAsync(Guid id, CancellationToken ct) =>
        _db.LoanAccounts.FirstOrDefaultAsync(a => a.Id == id && a.Type == AccountType.Loan && a.ApplicationId != null, ct);
}

public sealed class AccountCreationRequestRepository : IAccountCreationRequestRepository
{
    private readonly FinnovaDbContext _db;
    public AccountCreationRequestRepository(FinnovaDbContext db) => _db = db;

    public Task AddAsync(AccountCreationRequest request, CancellationToken ct) => _db.AccountCreationRequests.AddAsync(request, ct).AsTask();
    public Task<AccountCreationRequest?> GetTrackedAsync(Guid id, CancellationToken ct) => _db.AccountCreationRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Dictionary<AccountRequestStatus, int>> CountByStatusAsync(CancellationToken ct)
    {
        var groups = await _db.AccountCreationRequests.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        return groups.ToDictionary(g => g.Status, g => g.Count);
    }

    public async Task<DateTime?> OldestNonCompletedAsync(CancellationToken ct)
    {
        var q = _db.AccountCreationRequests.AsNoTracking().Where(x => x.Status != AccountRequestStatus.Completed);
        return await q.AnyAsync(ct) ? await q.MinAsync(x => x.CreatedAtUtc, ct) : null;
    }

    public async Task<(List<AccountCreationRequest> Items, int Total)> GetDeadLettersAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = _db.AccountCreationRequests.AsNoTracking().Where(x => x.Status == AccountRequestStatus.DeadLetter);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.UpdatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class AccountKycJobRepository : IAccountKycJobRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycJobRepository(FinnovaDbContext db) => _db = db;
    public Task<AccountKycJob?> GetTrackedAsync(Guid accountId, CancellationToken ct) => _db.AccountKycJobs.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
    public Task AddAsync(AccountKycJob job, CancellationToken ct) => _db.AccountKycJobs.AddAsync(job, ct).AsTask();
}

public sealed class AccountKycHistoryRepository : IAccountKycHistoryRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycHistoryRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountKycHistoryEntry entry, CancellationToken ct) => _db.AccountKycHistoryEntries.AddAsync(entry, ct).AsTask();
}

public sealed class AccountKycResolutionRepository : IAccountKycResolutionRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycResolutionRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountKycResolutionRequest request, CancellationToken ct) => _db.AccountKycResolutionRequests.AddAsync(request, ct).AsTask();
    public Task<AccountKycResolutionRequest?> GetTrackedAsync(Guid id, CancellationToken ct) => _db.AccountKycResolutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<AccountKycResolutionRequest?> GetPendingAsync(Guid accountId, CancellationToken ct) =>
        _db.AccountKycResolutionRequests.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.Status == KycResolutionStatus.Pending, ct);
}

public sealed class AccountAuditRepository : IAccountAuditRepository
{
    private readonly FinnovaDbContext _db;
    public AccountAuditRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountAuditEntry entry, CancellationToken ct) => _db.AccountAuditEntries.AddAsync(entry, ct).AsTask();
}

public sealed class AppRoleRepository : IAppRoleRepository
{
    private readonly FinnovaDbContext _db;
    public AppRoleRepository(FinnovaDbContext db) => _db = db;
    public async Task<IReadOnlyCollection<string>> GetRoleNamesForUserAsync(Guid userAccountId, CancellationToken ct) =>
        await (from ur in _db.UserAccountRoles.AsNoTracking()
               join r in _db.AppRoles.AsNoTracking() on ur.AppRoleId equals r.Id
               where ur.UserAccountId == userAccountId
               select r.Name).ToListAsync(ct);
}

public sealed class NumberingReferenceRepository : INumberingReferenceRepository
{
    private readonly FinnovaDbContext _db;
    public NumberingReferenceRepository(FinnovaDbContext db) => _db = db;

    public async Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct)
    {
        var name = await _db.LinesOfBusiness.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => x.LOB_Name).FirstOrDefaultAsync(ct);
        return name is null ? null : LobTokenFormatter.ToToken(name);
    }

    public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) =>
        _db.Locations.AsNoTracking().Where(x => x.Id == id && x.IsActive && x.Level == 5).Select(x => x.Code).FirstOrDefaultAsync(ct);
}