using Finnova.Models.Domain.Entities;

namespace Finnova.Service.IdentityVerification.Storage;

/// <summary>
/// Provider-agnostic abstraction for protected document storage operations.
/// Implementations must use the platform-approved protected store.
/// </summary>
public interface IProtectedDocumentStore
{
    /// <summary>
    /// Uploads a document stream to protected storage and returns a reference with metadata.
    /// The caller is responsible for disposing the input stream.
    /// </summary>
    /// <param name="content">Document content stream.</param>
    /// <param name="contentType">MIME type of the document.</param>
    /// <param name="sizeBytes">Size of the document in bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Protected document reference with storage metadata.</returns>
    Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default);

    /// <summary>
    /// Opens a read-only stream to the protected document. The caller is responsible for disposing the stream.
    /// Returns null if the storage key does not exist.
    /// </summary>
    /// <param name="storageKey">The opaque storage key returned by PutAsync.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Document content stream or null if not found.</returns>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes a protected document from storage.
    /// </summary>
    /// <param name="storageKey">The opaque storage key returned by PutAsync.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
