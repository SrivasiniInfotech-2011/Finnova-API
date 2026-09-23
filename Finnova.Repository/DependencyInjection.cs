using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Finnova.Repository.Repositories;

namespace Finnova.Repository;

public static class DependencyInjection
{
    /// <summary>
    /// Registers FinnovaDbContext and all repositories with the DI container.
    /// </summary>
    public static IServiceCollection AddFinnovaRepository(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FinnovaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();

        return services;
    }
}
