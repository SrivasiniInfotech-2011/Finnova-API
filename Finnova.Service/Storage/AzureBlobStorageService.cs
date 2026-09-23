using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Finnova.Service.Storage;

/// <summary>
/// <see cref="IBlobStorageService"/> implementation backed by Azure Blob Storage.
/// Works against a real Azure Storage account or a local Azurite emulator, depending on
/// the configured connection string.
/// </summary>
public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _client;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var connectionString = options.Value.Azure.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Azure blob storage connection string is not configured (BlobStorage:Azure:ConnectionString).");
        }

        _client = new BlobServiceClient(connectionString);
    }

    public async Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobName);

        var headers = contentType is null
            ? null
            : new BlobHttpHeaders { ContentType = contentType };

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = headers },
            cancellationToken);

        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

        return new BlobUploadResult
        {
            ContainerName = containerName,
            BlobName = blobName,
            Uri = blob.Uri.ToString(),
            SizeInBytes = properties.Value.ContentLength,
            ContentType = properties.Value.ContentType
        };
    }

    public async Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);

        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException(
                $"Blob '{blobName}' was not found in container '{containerName}'.");
        }
    }

    public async Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var response = await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public async Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var response = await blob.ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var container = _client.GetBlobContainerClient(containerName);

        if (!await container.ExistsAsync(cancellationToken))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(
                           traits: BlobTraits.None,
                           states: BlobStates.None,
                           prefix: prefix,
                           cancellationToken: cancellationToken))
        {
            names.Add(blob.Name);
        }

        return names;
    }
}
