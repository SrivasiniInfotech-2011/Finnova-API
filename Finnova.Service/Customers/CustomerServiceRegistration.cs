using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service.Customers;

/// <summary>
/// Registers the Customer master service graph (reference resolvers, validators, mappers and
/// supporting adapters) that the Customer MediatR handlers depend on.
///
/// Every backend host registers the shared <c>Finnova.Service</c> MediatR handlers by scanning
/// the whole assembly, which includes the Customer command/query handlers. Those handlers need
/// this graph in the container, so each host that calls <c>AddMediatR(... Finnova.Service ...)</c>
/// must also call this method, otherwise DI validation fails at startup with
/// "Unable to resolve service for type 'ILookupReferenceResolver'/'ICustomerMasterReferenceValidator'".
/// </summary>
public static class CustomerServiceRegistration
{
    public static IServiceCollection AddCustomerServices(this IServiceCollection services)
    {
        // Reference resolvers
        services.AddScoped<ILookupReferenceResolver, LookupReferenceResolver>();
        services.AddScoped<INationalityReferenceResolver, NationalityReferenceResolver>();
        services.AddScoped<IGLPostingGroupResolver, GLPostingGroupResolver>();
        services.AddScoped<IDocumentDefinitionResolver, DocumentDefinitionResolver>();
        services.AddScoped<IIndiaAddressControl, IndiaAddressControlDeferredAdapter>();

        // Validation
        services.AddScoped<CustomerFieldApplicabilityEvaluator>();
        services.AddScoped<ICustomerMasterReferenceValidator, CustomerMasterReferenceValidator>();

        // Code generation
        services.AddScoped<IDocumentNumberGenerator, CustomerCodeGenerator>();

        // Mappers (concrete + interface aliases pointing at the same scoped instance)
        services.AddScoped<CustomerAggregateMapper>();
        services.AddScoped<ICustomerAggregateMapper>(sp => sp.GetRequiredService<CustomerAggregateMapper>());
        services.AddScoped<ICustomerProjectionMapper, CustomerProjectionMapper>();
        services.AddScoped<CustomerChildMapper>();
        services.AddScoped<ICustomerChildMapper>(sp => sp.GetRequiredService<CustomerChildMapper>());

        // Account boundary adapter
        services.AddScoped<AccountContractBoundary>();
        services.AddScoped<IAccountContractBoundary>(sp => sp.GetRequiredService<AccountContractBoundary>());

        return services;
    }
}
