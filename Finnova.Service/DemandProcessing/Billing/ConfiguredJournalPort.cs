using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Service.DemandProcessing.Configuration;
using Microsoft.Extensions.Options;

namespace Finnova.Service.DemandProcessing.Billing;

public sealed class ConfiguredJournalPort(IOptions<DemandProcessingOptions> options) : IJournalPort
{
    public Task<JournalCheckResult> EnsurePostedAsync(JournalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.JournalAdapter)) throw new DemandDependencyException("The Accounting/GL Journal Port adapter is not configured.");
        throw new DemandDependencyException($"The configured Accounting/GL adapter '{options.Value.JournalAdapter}' must be bound to the existing journal capability.");
    }
}
