namespace Finnova.Service.GL.Ports;

// DEV STUBS ONLY (design assumption A2). Replace with real adapters to FINNOVA-25 and the
// referenced masters before production. Defaults are chosen so the write path is permissive in
// dev: no postings exist, zero balance, no outstanding refs, no active rule mappings, every
// company/branch/currency "exists", single-currency, no closed periods.
public sealed class StubGLBalanceQueryPort : IGLBalanceQueryPort
{
    public Task<bool> HasAuthorizedPostingAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<decimal> GetCurrentBalanceAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(0m);
}

public sealed class StubVoucherReferenceQueryPort : IVoucherReferenceQueryPort
{
    public Task<bool> HasOutstandingVouchersAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
}

public sealed class StubGLReferenceCheckPort : IGLReferenceCheckPort
{
    public Task<bool> IsMappedInActiveRuleAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
}

public sealed class StubCompanyQueryPort : ICompanyQueryPort
{
    public Task<bool> ExistsAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> IsMultiCurrencyEnabledAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult(false);
}

public sealed class StubAccountingCalendarPort : IAccountingCalendarPort
{
    public Task<bool> IsClosedPeriodAsync(Guid companyId, DateTime date, CancellationToken ct = default) => Task.FromResult(false);
}

public sealed class StubBranchQueryPort : IBranchQueryPort
{
    public Task<bool> ExistsAsync(Guid branchId, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class StubCurrencyQueryPort : ICurrencyQueryPort
{
    public Task<bool> ExistsAsync(Guid currencyId, CancellationToken ct = default) => Task.FromResult(true);
}
