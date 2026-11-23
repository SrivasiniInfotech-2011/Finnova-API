using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service.ReceiptAppropriation.Ports;

public static class ReceiptAppropriationAdapterRegistration
{
    public static IServiceCollection AddReceiptAppropriationAdapters(this IServiceCollection services)
    {
        // Replace these registrations with the concrete FINNOVA-61, due-ledger, rule-master,
        // Advance Account, and scope adapters supplied by their owning modules. Do not register
        // in-memory or duplicate receipt/due/rule sources here.
        throw new InvalidOperationException(
            "FINNOVA-62 adapters are unresolved. Register the authoritative Receipt, due-ledger, rule-master, Advance Account, and scope implementations before enabling the feature.");
    }
}