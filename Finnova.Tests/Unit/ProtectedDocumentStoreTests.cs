using System.Security.Cryptography;
using Azure.Storage.Blobs.Models;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Storage;
using Microsoft.Extensions.Options;
using Xunit;
namespace Finnova.Tests.Unit;
public sealed class ProtectedDocumentStoreTests
{
    private static (AzureBlobProtectedDocumentStore Store, MemoryBlobClient Blobs) Create(long max = 5 * 1024 * 1024)
    {
        var options = Options.Create(new ProtectedBlobStorageOptions { Enabled = true, ContainerName = "identity-documents", Prefix = "identity-verification", MaxDocumentBytes = max, AcceptedContentTypes = ["image/jpeg", "image/png", "application/pdf"], AllowOverwrite = false });
        var blobs = new MemoryBlobClient(); return (new AzureBlobProtectedDocumentStore(options, blobs), blobs);
    }
    [Fact] public async Task UploadUsesOpaqueKeyAndReturnsSha256Metadata()
    {
        var (store, blobs) = Create(); var bytes = "%PDF-identity-test"u8.ToArray(); await using var input = new MemoryStream(bytes);
        var reference = await store.PutAsync(input, "application/pdf", bytes.Length);
        Assert.StartsWith("identity-verification/", reference.StorageKey); Assert.EndsWith(".bin", reference.StorageKey); Assert.DoesNotContain("PAN", reference.StorageKey, StringComparison.OrdinalIgnoreCase); Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), reference.ContentHash); Assert.Equal(1, blobs.Count);
        await using var output = await store.OpenReadAsync(reference.StorageKey); using var copy = new MemoryStream(); await output!.CopyToAsync(copy); Assert.Equal(bytes, copy.ToArray());
    }
    [Fact] public async Task DeleteRemovesOnlyTheOpaqueBlob()
    { var (store, blobs) = Create(); var bytes = "%PDF-x"u8.ToArray(); await using var input = new MemoryStream(bytes); var reference = await store.PutAsync(input, "application/pdf", bytes.Length); await store.DeleteAsync(reference.StorageKey); Assert.Equal(0, blobs.Count); }
    [Fact] public async Task RejectsEmptyAndOversizedContent()
    { var (store, _) = Create(5); await Assert.ThrowsAsync<IdentityVerificationDocumentFormatException>(() => store.PutAsync(new MemoryStream(), "application/pdf", 0)); await Assert.ThrowsAsync<IdentityVerificationDocumentSizeException>(() => store.PutAsync(new MemoryStream(new byte[6]), "application/pdf", 6)); }
    [Fact] public async Task RejectsUnsupportedContentTypeAndTraversalKey()
    { var (store, _) = Create(); await Assert.ThrowsAsync<IdentityVerificationDocumentFormatException>(() => store.PutAsync(new MemoryStream([1]), "text/plain", 1)); await Assert.ThrowsAsync<IdentityVerificationValidationException>(() => store.OpenReadAsync("identity-verification/../secret.bin")); }
    [Fact] public async Task HonorsCancellationBeforeUpload()
    { var (store, blobs) = Create(); using var cts = new CancellationTokenSource(); cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PutAsync(new MemoryStream("%PDF-x"u8.ToArray()), "application/pdf", 6, cts.Token)); Assert.Equal(0, blobs.Count); }
    [Fact] public async Task LifecycleCleansUpWhenProviderOrPersistenceFails()
    {
        var (store, blobs) = Create(); var bytes = "%PDF-failure"u8.ToArray(); await Assert.ThrowsAsync<IdentityVerificationProviderUnavailableException>(() => new ProtectedDocumentLifecycle(store).UploadAndExecuteAsync(new MemoryStream(bytes), "application/pdf", bytes.Length, _ => throw new IdentityVerificationProviderUnavailableException(), CancellationToken.None)); Assert.Equal(0, blobs.Count);
    }
    private sealed class MemoryBlobClient : IProtectedBlobClient
    {
        private readonly Dictionary<string, byte[]> data = new(StringComparer.Ordinal); public int Count => data.Count;
        public Task EnsureContainerAsync(CancellationToken ct) => Task.CompletedTask;
        public async Task UploadAsync(string key, Stream content, BlobHttpHeaders headers, IReadOnlyDictionary<string, string> metadata, bool overwrite, CancellationToken ct) { if (!overwrite && data.ContainsKey(key)) throw new InvalidOperationException("duplicate"); using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); data[key] = copy.ToArray(); }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(data.TryGetValue(key, out var bytes) ? new MemoryStream(bytes, writable: false) : null);
        public Task DeleteIfExistsAsync(string key, CancellationToken ct) { data.Remove(key); return Task.CompletedTask; }
    }
}
