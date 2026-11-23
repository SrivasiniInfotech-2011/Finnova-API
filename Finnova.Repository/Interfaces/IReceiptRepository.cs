using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public sealed record ReceiptSearchFilter(
    string? Search, ReceiptType? ReceiptType, ReceiptMode? Mode, ReceiptStatus? Status,
    Guid? LineOfBusinessId, Guid? BranchId, DateOnly? DocDateFrom, DateOnly? DocDateTo, int Page, int PageSize);

/// <summary>Add does NOT save; callers commit through <see cref="IApplicationUnitOfWork"/> (atomic save, R2.3).</summary>
public interface IReceiptRepository
{
    Task AddAsync(Receipt receipt, CancellationToken ct);
    Task<Receipt?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct);
    Task<Receipt?> GetDetailAsync(Guid id, CancellationToken ct);
    Task<bool> DocNoExistsAsync(ReceiptType type, string docNo, CancellationToken ct);
    Task<(List<Receipt> Items, int Total)> SearchAsync(ReceiptSearchFilter filter, CancellationToken ct);
}

/// <summary>Append-only: intentionally no update or delete method (R11.3).</summary>
public interface IReceiptAuditRepository
{
    Task AddAsync(ReceiptAuditEntry entry, CancellationToken ct);
    Task<List<ReceiptAuditEntry>> GetByReceiptIdAsync(Guid receiptId, CancellationToken ct);
}

public sealed record RefLob(Guid Id, string Name);
public sealed record RefBranch(Guid Id, string Code, string Name);
public sealed record RefOfficer(Guid Id, string UserCode, string Name);
public sealed record RefCustomer(
    Guid Id, string Code, string Name, Guid CompanyId, Guid? GlAccountId,
    string? Address, string? Phone, string? Mobile, string? Email, string? Website);
public sealed record RefContract(Guid Id, string AccountNumber, string AccountName, Guid? CustomerId, AccountStatus Status, DateTime? ClosedAt);
public sealed record RefGl(Guid Id, string Code);
public sealed record RefDraweeBank(Guid Id, string Code, string Name);
public sealed record RefDraweeBranch(Guid Id, string PlaceCode, string PlaceName, DateOnly StartDate, DateOnly? EndDate)
{
    public bool IsEffectiveOn(DateOnly date) => StartDate <= date && (EndDate is null || EndDate >= date);
}

/// <summary>Read-only projections over the masters used by receipt validation and the screen dropdowns.</summary>
public interface IReceiptReferenceRepository
{
    Task<RefLob?> GetActiveLobAsync(Guid id, CancellationToken ct);
    Task<RefBranch?> GetActiveBranchAsync(Guid id, CancellationToken ct);
    Task<RefOfficer?> GetActiveOfficerAsync(Guid id, CancellationToken ct);
    Task<List<RefOfficer>> GetActiveOfficersAsync(CancellationToken ct);

    Task<RefCustomer?> GetActiveCustomerByCodeAsync(string code, CancellationToken ct);
    Task<RefCustomer?> GetCustomerByIdAsync(Guid id, CancellationToken ct);
    Task<(List<RefCustomer> Items, int Total)> SearchActiveCustomersAsync(string? term, int page, int pageSize, CancellationToken ct);

    Task<Dictionary<string, RefContract>> GetContractsAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct);
    Task<List<RefContract>> GetLiveContractsOfCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>Same criteria as IGLAccountRepository.GetActiveApprovedPostableByCompanyAndCodeAsync.</summary>
    Task<RefGl?> GetPostableGlByCodeAsync(Guid companyId, string code, CancellationToken ct);
    Task<RefGl?> GetPostableGlByIdAsync(Guid companyId, Guid glAccountId, CancellationToken ct);

    Task<RefDraweeBank?> GetActiveDraweeBankAsync(Guid id, CancellationToken ct);
    Task<List<RefDraweeBank>> GetActiveDraweeBanksAsync(CancellationToken ct);
    Task<RefDraweeBranch?> GetDraweeBranchAsync(Guid bankId, Guid branchId, CancellationToken ct);
    Task<List<RefDraweeBranch>> GetDraweeBranchesAsync(Guid bankId, DateOnly asOf, CancellationToken ct);

    Task<FaBankAccount?> GetActiveFaBankAccountAsync(Guid id, CancellationToken ct);
    Task<List<FaBankAccount>> GetActiveFaBankAccountsAsync(CancellationToken ct);

    Task<List<LookupDropdownItemResponse>> GetActiveComponentsAsync(CancellationToken ct);
}