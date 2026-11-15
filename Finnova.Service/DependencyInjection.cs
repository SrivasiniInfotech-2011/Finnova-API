using Finnova.Repository.Interfaces;
using Finnova.Service.Applications;
using Finnova.Service.Applications.Financial;
using Finnova.Service.Applications.Ports;
using Finnova.Service.Applications.References;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Behaviors;
using Finnova.Service.Customers;
using Finnova.Service.Document.Ports;
using Finnova.Service.GL.Ports;
using Finnova.Service.IdentityVerification;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service;

/// <summary>Optional service-layer feature areas. A host enables only the areas it serves.</summary>
[Flags]
public enum FinnovaServiceModules
{
    None = 0,

    /// <summary>Application Processing handlers and their ports (hosted by Finnova.AccountsService).</summary>
    ApplicationProcessing = 1,

    /// <summary>Identity Verification handlers, providers and document storage (hosted by Finnova.SystemAdminService).</summary>
    IdentityVerification = 2
}

/// <summary>
/// Service-layer composition entry point, the counterpart of
/// <c>Finnova.Repository.DependencyInjection.AddFinnovaRepository</c>. Hosts call both.
/// </summary>
public static class DependencyInjection
{
    private const string ApplicationsNamespace = "Finnova.Service.Applications";
    private const string IdentityVerificationNamespace = "Finnova.Service.IdentityVerification";

    /// <summary>
    /// Registers the core service layer (MediatR handlers, validators, validation pipeline, auth token
    /// service, shared ports) plus the optional <paramref name="modules"/>. MediatR only scans the
    /// handler namespaces of enabled modules, so a host never registers a handler whose
    /// dependencies it does not provide.
    /// </summary>
    public static IServiceCollection AddFinnovaServices(
        this IServiceCollection services,
        IConfiguration configuration,
        FinnovaServiceModules modules = FinnovaServiceModules.None)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceAssembly = typeof(DependencyInjection).Assembly;
        var includeApplications = modules.HasFlag(FinnovaServiceModules.ApplicationProcessing);
        var includeIdentity = modules.HasFlag(FinnovaServiceModules.IdentityVerification);

        // MediatR handlers (module namespaces are skipped unless enabled).
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(serviceAssembly);
            cfg.TypeEvaluator = t =>
                (includeApplications || !InNamespace(t, ApplicationsNamespace)) &&
                (includeIdentity || !InNamespace(t, IdentityVerificationNamespace));
        });

        // FluentValidation + the pipeline that runs it before each handler.
        services.AddValidatorsFromAssembly(serviceAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Auth token service (LoginCommandHandler depends on ITokenService).
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordPolicy, DefaultPasswordPolicy>();
        services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();

        // Feature graphs shared by every host that scans their handlers.
        services.AddCustomerServices();
        services.AddGLPorts();
        services.AddScoped<ILookupValidationPort, LookupValidationAdapter>();
        services.AddScoped<IDocumentUsageChecker, StubDocumentUsageChecker>();

        if (includeApplications) services.AddApplicationProcessingServices();
        if (includeIdentity) services.AddIdentityVerificationServices(configuration);

        return services;
    }

    /// <summary>Application Processing ports and calculators. Called by <see cref="AddFinnovaServices"/> when enabled.</summary>
    public static IServiceCollection AddApplicationProcessingServices(this IServiceCollection services)
    {
        services.AddScoped<AmortizationCalculator>();
        services.AddScoped<IApplicationReferenceValidator, ApplicationReferenceValidator>();
        services.AddSingleton<IApplicationNumberGenerator, ApplicationNumberGenerator>();
        return services;
    }

    private static bool InNamespace(Type type, string ns)
        => type.Namespace is { } typeNamespace &&
           (typeNamespace == ns || typeNamespace.StartsWith(ns + ".", StringComparison.Ordinal));
}
