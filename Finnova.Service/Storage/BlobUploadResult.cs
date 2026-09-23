namespace Finnova.Service.Storage;

/// <summary>
/// Describes the outcome of a successful blob upload.
/// </summary>
public sealed record BlobUploadResult
{
    /// <summary>The container (Azure) or bucket (AWS) the blob was stored in.</summary>
    public required string ContainerName { get; init; }

    /// <summary>The name/key of the stored blob.</summary>
    public required string BlobName { get; init; }

    /// <summary>The absolute URI at which the blob can be addressed.</summary>
    public required string Uri { get; init; }

    /// <summary>The size of the uploaded content in bytes.</summary>
    public long SizeInBytes { get; init; }

    /// <summary>The content type recorded for the blob, if any.</summary>
    public string? ContentType { get; init; }
}
