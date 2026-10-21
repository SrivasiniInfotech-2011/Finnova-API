using Finnova.Service.Document.Ports;
using Finnova.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Tests.Integration.Document;

/// <summary>
/// Document Master integration host. Extends <see cref="SystemAdminAppFactory"/> (which boots the
/// SystemAdmin host in the "Testing" environment on the EF Core InMemory provider) and additionally
/// replaces the two Document ports so authorization scenarios do not depend on SQL Server or a
/// specific lookup seed:
/// <list type="bullet">
///   <item><see cref="ILookupValidationPort"/> -> an allow-all fake so the create's DOC_CATEGORY /
///     APPLICABLE_ENTITY lookup checks (R1.8) pass without seeding lookup rows.</item>
///   <item><see cref="IDocumentUsageChecker"/> -> a fake that reports "not in use".</item>
/// </list>
/// The EF-backed <see cref="Finnova.Repository.Interfaces.IDocumentRepository"/> from the host is
/// kept as-is; it persists to the InMemory database so create-then-authorize flows work in-process.
/// </summary>
public sealed class DocumentAppFactory : SystemAdminAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            Replace<ILookupValidationPort>(services,
                new FakeLookupValidationPort().WithAllowAll());
            Replace<IDocumentUsageChecker>(services,
                new FakeDocumentUsageChecker());
        });
    }

    private static void Replace<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        var existing = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var d in existing)
        {
            services.Remove(d);
        }
        services.AddSingleton(instance);
    }
}
