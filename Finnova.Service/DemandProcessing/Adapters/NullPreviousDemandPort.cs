using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Adapters;

public sealed class NullPreviousDemandPort : IPreviousDemandPort
{
    public Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct) => Task.FromResult<PreviousDemandResult?>(null);
}