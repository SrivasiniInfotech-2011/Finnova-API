using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Processing;

public sealed class DemandRunWorker(DemandWorkQueue queue, IServiceScopeFactory scopes, ILogger<DemandRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IDemandProcessor>().ProcessAsync(runId, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Demand run {RunId} failed before publication", runId); }
        }
    }
}
