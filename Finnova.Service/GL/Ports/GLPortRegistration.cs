using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service.GL.Ports;

/// <summary>
/// Registers the GL dependency ports consumed by the GL MediatR command/query handlers.
///
/// Every backend host registers the shared <c>Finnova.Service</c> MediatR handlers by scanning
/// the whole assembly, which includes the GL handlers. Those handlers depend on these ports, so
/// each host that calls <c>AddMediatR(... Finnova.Service ...)</c> must also call this method,
/// otherwise DI validation fails at startup with
/// "Unable to resolve service for type 'ICompanyQueryPort'/'IGLBalanceQueryPort'/..." etc.
///
/// NOTE: these are DEV STUBS (design assumption A2). They must be replaced with real adapters to
/// FINNOVA-25 and the referenced masters before production.
/// </summary>
public static class GLPortRegistration
{
    public static IServiceCollection AddGLPorts(this IServiceCollection services)
    {
        services.AddScoped<IGLBalanceQueryPort, StubGLBalanceQueryPort>();
        services.AddScoped<IVoucherReferenceQueryPort, StubVoucherReferenceQueryPort>();
        services.AddScoped<IGLReferenceCheckPort, StubGLReferenceCheckPort>();
        services.AddScoped<ICompanyQueryPort, StubCompanyQueryPort>();
        services.AddScoped<IAccountingCalendarPort, StubAccountingCalendarPort>();
        services.AddScoped<IBranchQueryPort, StubBranchQueryPort>();
        services.AddScoped<ICurrencyQueryPort, StubCurrencyQueryPort>();
        return services;
    }
}
