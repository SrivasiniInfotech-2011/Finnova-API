namespace Finnova.Service.Storage;

/// <summary>
/// Provider-agnostic abstraction for object/blob storage operations.
/// Implemented for both Azure Blob Storage and AWS S3.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a blob. If a blob with the same name already exists it is overwritten.
    /// </summary>
    /// <returns>Metadata describing the stored blob, including its absolute URI.</returns>
    Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a blob's contents as a stream. The caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob. Returns <c>true</c> if the blob existed and was deleted, otherwise <c>false</c>.
    /// </summary>
    Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a blob exists.
    /// </summary>
    Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the names of all blobs in a container, optionally filtered by a name prefix.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
