using System.Threading.Channels;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Processing;

public sealed class DemandWorkQueue : IDemandWorkQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ValueTask EnqueueAsync(Guid runId, CancellationToken ct) => _channel.Writer.WriteAsync(runId, ct);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
