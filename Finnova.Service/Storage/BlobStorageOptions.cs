namespace Finnova.Service.Storage;

/// <summary>
/// Identifies which blob storage provider implementation should be registered.
/// </summary>
public enum BlobStorageProvider
{
    Azure,
    Aws
}

/// <summary>
/// Root configuration for blob storage, bound from the "BlobStorage" configuration section.
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>The active provider. Defaults to Azure (Azurite for local development).</summary>
    public BlobStorageProvider Provider { get; set; } = BlobStorageProvider.Azure;

    /// <summary>Azure Blob Storage settings. Used when <see cref="Provider"/> is Azure.</summary>
    public AzureBlobStorageOptions Azure { get; set; } = new();

    /// <summary>AWS S3 settings. Used when <see cref="Provider"/> is Aws.</summary>
    public AwsBlobStorageOptions Aws { get; set; } = new();
}

/// <summary>
/// Azure Blob Storage configuration. For local development this points at Azurite via its
/// well-known development connection string.
/// </summary>
public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Azure Storage connection string. Use "UseDevelopmentStorage=true" or the full Azurite
    /// connection string for local development.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// AWS S3 configuration. Supports pointing at a custom <see cref="ServiceUrl"/> so the same
/// implementation can target LocalStack/MinIO in local development.
/// </summary>
public sealed class AwsBlobStorageOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Optional custom service URL (e.g. a LocalStack endpoint). When set, path-style
    /// addressing is enabled automatically.
    /// </summary>
    public string? ServiceUrl { get; set; }
}
