using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// Binds the "BlobStorage" configuration section and registers the
    /// <see cref="IBlobStorageService"/> implementation matching the configured provider.
    /// </summary>
    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(BlobStorageOptions.SectionName);
        services.Configure<BlobStorageOptions>(section);

        var options = section.Get<BlobStorageOptions>() ?? new BlobStorageOptions();

        switch (options.Provider)
        {
            case BlobStorageProvider.Aws:
                services.AddSingleton<IBlobStorageService, AwsS3BlobStorageService>();
                break;

            case BlobStorageProvider.Azure:
            default:
                services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
                break;
        }

        return services;
    }
}
