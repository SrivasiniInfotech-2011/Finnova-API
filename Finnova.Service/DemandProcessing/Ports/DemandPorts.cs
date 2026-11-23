using System.Security.Claims;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.ValueObjects;

namespace Finnova.Service.DemandProcessing.Ports;

public interface IAuthorizationScopeResolver
{
    Task<AuthorizedDemandScope> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct);
}

public interface IFinalizedSchedulePort
{
    Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct);
}

public interface IAccountSourcePort
{
    IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, ClaimsPrincipal principal, CancellationToken ct);
    Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct);
}

public interface IAllocationEventPort
{
    Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct);
}

public interface IAllocationPolicy
{
    AllocationResult Apply(decimal scheduledInstallment, decimal priorArrears, IReadOnlyList<AllocationEvent> events);
}

public sealed record AllocationResult(decimal CurrentDue, decimal Arrears, IReadOnlyList<AllocationAppliedEvent> Evidence);
public sealed record AllocationAppliedEvent(AllocationEvent Event, decimal AllocatedAmount, string TargetComponent, string ApplicationKey);

public interface IChargePolicyAdapter
{
    Task<ChargeResult> CalculateAsync(ChargeInput input, CancellationToken ct);
}

public interface IJournalPort
{
    Task<JournalCheckResult> EnsurePostedAsync(JournalRequest request, CancellationToken ct);
}

public interface IDemandLeaseStore
{
    Task<bool> TryAcquireAsync(string logicalScopeKey, Guid runId, CancellationToken ct);
    Task<bool> RenewAsync(Guid runId, CancellationToken ct);
    Task ReleaseAsync(Guid runId, CancellationToken ct);
}

public interface IDemandPdfExportService
{
    Task<PdfFileResult> RenderOutputAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct);
    Task<PdfFileResult> RenderBatchesAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct);
}

public interface IPdfRenderer
{
    byte[] Render(DemandPdfDocument document);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IDemandWorkQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken ct);
}

public interface IDemandProcessor
{
    Task ProcessAsync(Guid runId, CancellationToken ct);
}

public sealed record PreviousDemandResult(decimal Arrears, string SourceReference);

public interface IPreviousDemandPort
{
    Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct);
}