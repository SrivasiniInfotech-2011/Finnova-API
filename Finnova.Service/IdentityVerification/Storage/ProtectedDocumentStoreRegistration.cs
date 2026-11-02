using Azure.Storage.Blobs;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Finnova.Service.IdentityVerification.Storage;
public static class ProtectedDocumentStoreRegistration
{
    public static IServiceCollection AddIdentityVerificationStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"{IdentityVerificationOptions.SectionName}:BlobStorage"); services.Configure<ProtectedBlobStorageOptions>(section); var settings = section.Get<ProtectedBlobStorageOptions>() ?? new ProtectedBlobStorageOptions();
        if (!settings.Enabled) { services.AddScoped<IProtectedDocumentStore, ProtectedDocumentStoreNotConfigured>(); return services; }
        if (string.IsNullOrWhiteSpace(settings.ConnectionStringName) || string.IsNullOrWhiteSpace(settings.ContainerName)) throw new IdentityVerificationConfigurationException();
        var connectionString = configuration.GetConnectionString(settings.ConnectionStringName); if (string.IsNullOrWhiteSpace(connectionString)) throw new IdentityVerificationConfigurationException();
        services.AddSingleton(_ => new BlobServiceClient(connectionString)); services.AddSingleton(sp => sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(settings.ContainerName)); services.AddSingleton<IProtectedBlobClient, AzureProtectedBlobClient>(); services.AddScoped<IProtectedDocumentStore, AzureBlobProtectedDocumentStore>(); return services;
    }
}
internal sealed class ProtectedDocumentStoreNotConfigured : IProtectedDocumentStore
{
    public Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
    public Task DeleteAsync(string storageKey, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
}
