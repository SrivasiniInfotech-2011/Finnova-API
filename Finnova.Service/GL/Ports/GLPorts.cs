namespace Finnova.Service.GL.Ports;

/// <summary>Balance + posting existence (owned by FINNOVA-25). Backs R4.2 lock and R5.4 zero-balance.</summary>
public interface IGLBalanceQueryPort
{
    Task<bool> HasAuthorizedPostingAsync(Guid glAccountId, CancellationToken ct = default);
    Task<decimal> GetCurrentBalanceAsync(Guid glAccountId, CancellationToken ct = default);
}

/// <summary>Parked/draft/pending vouchers referencing a GL (R5.5). Owned by FINNOVA-25.</summary>
public interface IVoucherReferenceQueryPort
{
    Task<bool> HasOutstandingVouchersAsync(Guid glAccountId, CancellationToken ct = default);
}

/// <summary>Active system accounting rule / product / charge mapping to a GL (R5.7).</summary>
public interface IGLReferenceCheckPort
{
    Task<bool> IsMappedInActiveRuleAsync(Guid glAccountId, CancellationToken ct = default);
}

/// <summary>Company existence + multi-currency flag (R7.8).</summary>
public interface ICompanyQueryPort
{
    Task<bool> ExistsAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> IsMultiCurrencyEnabledAsync(Guid companyId, CancellationToken ct = default);
}

/// <summary>Closed-period check for Effective From (R1.12).</summary>
public interface IAccountingCalendarPort
{
    Task<bool> IsClosedPeriodAsync(Guid companyId, DateTime date, CancellationToken ct = default);
}

/// <summary>Branch existence for applicability (R6.7).</summary>
public interface IBranchQueryPort
{
    Task<bool> ExistsAsync(Guid branchId, CancellationToken ct = default);
}

/// <summary>Currency existence (R6.9).</summary>
public interface ICurrencyQueryPort
{
    Task<bool> ExistsAsync(Guid currencyId, CancellationToken ct = default);
}
