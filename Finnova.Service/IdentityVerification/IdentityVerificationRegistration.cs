using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Correlation;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Queries;
using Finnova.Service.IdentityVerification.Storage;
using Finnova.Service.IdentityVerification.Validation;
using Finnova.Service.IdentityVerification.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finnova.Service.IdentityVerification;

public static class IdentityVerificationRegistration
{
    /// <summary>
    /// Registers the Identity Verification graph. Provider endpoints and credentials remain deployment
    /// configuration; only enabled provider adapters are registered, and an enabled operation with
    /// missing endpoint configuration returns an unavailable result and never succeeds.
    /// </summary>
    public static IServiceCollection AddIdentityVerificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.Configure<IdentityVerificationOptions>(configuration.GetSection(IdentityVerificationOptions.SectionName));
        services.AddHttpClient<ConfigurableComparisonHttpClient>();
        services.AddScoped<IRequestContextAccessor, RequestContextAccessor>();
        services.AddScoped<IProviderCallExecutor, ProviderCallExecutor>();
        services.AddScoped<IVerificationWorkflowCoordinator, VerificationWorkflowCoordinator>();
        services.AddScoped<DocumentInputValidator>();
        services.AddScoped<ProtectedDocumentLifecycle>();
        services.AddIdentityVerificationStorage(configuration);
        services.AddScoped<IIdentityVerificationDocumentQuery, ProtectedDocumentQuery>();

        var section = configuration.GetSection(IdentityVerificationOptions.SectionName);

        if (section.GetValue<bool>("PanIdentifierValidation:Enabled"))
        {
            services.AddScoped<IIdentifierValidationAdapter>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
                return CreateIdentifierAdapter(VerificationOperationType.PanIdentifierValidation, options.PanIdentifierValidation, options.Retry);
            });
        }

        if (section.GetValue<bool>("AadhaarIdentifierValidation:Enabled"))
        {
            services.AddScoped<IIdentifierValidationAdapter>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
                return CreateIdentifierAdapter(VerificationOperationType.AadhaarIdentifierValidation, options.AadhaarIdentifierValidation, options.Retry);
            });
        }

        if (section.GetValue<bool>("PanNameVerification:Enabled"))
        {
            services.AddScoped<IComparisonAdapter>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
                return CreateComparisonAdapter(VerificationOperationType.PanNameVerification, options.PanNameVerification, options.Retry, sp);
            });
        }

        if (section.GetValue<bool>("AadhaarAddressVerification:Enabled"))
        {
            services.AddScoped<IComparisonAdapter>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
                return CreateComparisonAdapter(VerificationOperationType.AadhaarAddressVerification, options.AadhaarAddressVerification, options.Retry, sp);
            });
        }

        services.AddScoped<IComparisonAdapterResolver, ConfiguredComparisonAdapterResolver>();
        return services;
    }

    private static ConfiguredIdentifierAdapter CreateIdentifierAdapter(
        VerificationOperationType operation,
        ProviderOperationOptions provider,
        RetryOptions retry)
        => new(
            operation,
            provider.BaseAddress,
            provider.Path,
            provider.HttpMethod,
            provider.RequestMode,
            provider.IdentifierFieldName,
            Math.Max(1, retry.TimeoutSeconds),
            Math.Max(1, retry.MaxAttempts),
            Math.Max(0, retry.BaseDelayMilliseconds));

    private static ConfigurableComparisonHttpClient CreateComparisonAdapter(
        VerificationOperationType operation,
        ComparisonProviderOptions provider,
        RetryOptions retry,
        IServiceProvider services)
        => new(
            services.GetRequiredService<IHttpClientFactory>().CreateClient(),
            operation,
            provider,
            retry,
            services.GetRequiredService<ILogger<ConfigurableComparisonHttpClient>>());
}

internal sealed class ConfiguredComparisonAdapterResolver(
    IEnumerable<IComparisonAdapter> adapters,
    IOptions<IdentityVerificationOptions> options) : IComparisonAdapterResolver
{
    public IComparisonAdapter Resolve(VerificationOperationType operation, string adapterName)
    {
        var provider = operation switch
        {
            VerificationOperationType.PanNameVerification => options.Value.PanNameVerification,
            VerificationOperationType.AadhaarAddressVerification => options.Value.AadhaarAddressVerification,
            _ => throw new IdentityVerificationConfigurationException()
        };

        if (!provider.Enabled ||
            string.IsNullOrWhiteSpace(provider.AdapterName) ||
            string.IsNullOrWhiteSpace(provider.BaseAddress) ||
            string.IsNullOrWhiteSpace(provider.Path))
        {
            throw new IdentityVerificationConfigurationException();
        }

        var requestedName = adapterName?.Trim();
        var configuredProfile = provider.InputMapping.MappingProfile?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedName) &&
            !string.Equals(requestedName, provider.AdapterName, StringComparison.Ordinal) &&
            !string.Equals(requestedName, configuredProfile, StringComparison.Ordinal))
        {
            throw new IdentityVerificationConfigurationException();
        }

        return adapters.SingleOrDefault(adapter => adapter.SupportedOperation == operation)
            ?? throw new IdentityVerificationConfigurationException();
    }
}
