using System.Security.Claims;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.DemandProcessing.Configuration;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.Extensions.Options;

namespace Finnova.Service.DemandProcessing.Adapters;

public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

public sealed class AuthorizationScopeResolver(IOptions<DemandProcessingOptions> options) : IAuthorizationScopeResolver
{
    public Task<AuthorizedDemandScope> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException();
        var lobIds = principal.Claims.Where(x => x.Type == "lob").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
        var branchIds = principal.Claims.Where(x => x.Type == "branch").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
        if (principal.Claims.Any(x => options.Value.AdminRoles.Contains(x.Value, StringComparer.OrdinalIgnoreCase))) return Task.FromResult(new AuthorizedDemandScope(lobIds, branchIds));
        return Task.FromResult(new AuthorizedDemandScope(lobIds, branchIds));
    }
}

public sealed class ConfiguredScheduleAdapter(IOptions<DemandProcessingOptions> options) : IFinalizedSchedulePort
{
    public Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct) => throw new DemandDependencyException($"The configured finalized-schedule adapter '{options.Value.BillingCompletionAdapter ?? "default"}' must be bound to the existing schedule contract.");
}

public sealed class DependencyAllocationEventPort : IAllocationEventPort
{
    public Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct) => throw new DemandDependencyException("The authoritative allocation-event source is not available.");
}

public sealed class DependencyAccountSourcePort : IAccountSourcePort
{
    public IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, CancellationToken ct) => throw new DemandDependencyException("The authoritative account projection source is not available.");
    public Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, ClaimsPrincipal principal, CancellationToken ct) => throw new DemandDependencyException("The authoritative Branch scope source is not available.");
    public Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct) => throw new DemandDependencyException("The authoritative Branch source is not available.");
}
