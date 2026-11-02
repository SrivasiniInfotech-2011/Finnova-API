using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.External;
using Microsoft.Extensions.Options;
namespace Finnova.Service.IdentityVerification.Storage;
public interface IProtectedBlobClient
{
    Task EnsureContainerAsync(CancellationToken ct);
    Task UploadAsync(string storageKey, Stream content, BlobHttpHeaders headers, IReadOnlyDictionary<string, string> metadata, bool overwrite, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteIfExistsAsync(string storageKey, CancellationToken ct);
}
public sealed class AzureProtectedBlobClient(BlobContainerClient container) : IProtectedBlobClient
{
    public async Task EnsureContainerAsync(CancellationToken ct) => await container.CreateIfNotExistsAsync(cancellationToken: ct);
    public async Task UploadAsync(string storageKey, Stream content, BlobHttpHeaders headers, IReadOnlyDictionary<string, string> metadata, bool overwrite, CancellationToken ct)
    {
        var options = new BlobUploadOptions { HttpHeaders = headers, Metadata = metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) };
        if (!overwrite) options.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
        await container.GetBlobClient(storageKey).UploadAsync(content, options, ct);
    }
    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        try { return (await container.GetBlobClient(storageKey).DownloadStreamingAsync(cancellationToken: ct)).Value.Content; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
    public async Task DeleteIfExistsAsync(string storageKey, CancellationToken ct) => await container.GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: ct);
}
public sealed class AzureBlobProtectedDocumentStore(IOptions<ProtectedBlobStorageOptions> options, IProtectedBlobClient blobs) : IProtectedDocumentStore
{
    private readonly ProtectedBlobStorageOptions settings = Validate(options.Value);
    public async Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        if (content is null || !content.CanRead || sizeBytes <= 0) throw new IdentityVerificationDocumentFormatException();
        if (sizeBytes > settings.MaxDocumentBytes || sizeBytes > 5 * 1024 * 1024) throw new IdentityVerificationDocumentSizeException();
        if (!settings.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)) throw new IdentityVerificationDocumentFormatException();
        ct.ThrowIfCancellationRequested();
        var key = $"{settings.Prefix}/{Guid.NewGuid():N}.bin";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.UploadTimeoutSeconds));
        try
        {
            if (settings.CreateContainerIfMissing) await blobs.EnsureContainerAsync(timeout.Token);
            await using var hashing = new ExactSizeHashingReadStream(content, sizeBytes);
            await blobs.UploadAsync(key, hashing, new BlobHttpHeaders { ContentType = contentType, CacheControl = "no-store", ContentDisposition = "attachment; filename=identity-document" }, new Dictionary<string, string>(StringComparer.Ordinal) { ["purpose"] = "identity-verification", ["schema-version"] = "1" }, settings.AllowOverwrite, timeout.Token);
            if (hashing.BytesRead != sizeBytes) { try { await blobs.DeleteIfExistsAsync(key, CancellationToken.None); } catch { } throw new IdentityVerificationDocumentFormatException(); }
            return new ProtectedDocumentReference { StorageKey = key, ContentType = contentType, SizeBytes = hashing.BytesRead, ContentHash = Convert.ToHexString(hashing.GetHash()).ToLowerInvariant(), CreatedUtc = DateTime.UtcNow };
        }
        catch (IdentityVerificationDocumentFormatException) { try { await blobs.DeleteIfExistsAsync(key, CancellationToken.None); } catch { } throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (RequestFailedException) { throw new IdentityVerificationProviderUnavailableException(); }
    }
    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        ValidateStorageKey(storageKey, settings.Prefix); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds));
        try { return await blobs.OpenReadAsync(storageKey, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (RequestFailedException) { throw new IdentityVerificationProviderUnavailableException(); }
    }
    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ValidateStorageKey(storageKey, settings.Prefix); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.DeleteTimeoutSeconds));
        try { await blobs.DeleteIfExistsAsync(storageKey, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (RequestFailedException) { throw new IdentityVerificationProviderUnavailableException(); }
    }
    private static ProtectedBlobStorageOptions Validate(ProtectedBlobStorageOptions value)
    {
        if (value.MaxDocumentBytes is <= 0 or > 5 * 1024 * 1024 || value.UploadTimeoutSeconds <= 0 || value.DownloadTimeoutSeconds <= 0 || value.DeleteTimeoutSeconds <= 0) throw new IdentityVerificationConfigurationException();
        if (!System.Text.RegularExpressions.Regex.IsMatch(value.ContainerName ?? "", "^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$")) throw new IdentityVerificationConfigurationException();
        if (string.IsNullOrWhiteSpace(value.Prefix) || value.Prefix.Length > 128 || value.Prefix.StartsWith('/') || value.Prefix.EndsWith('/') || value.Prefix.Contains("..", StringComparison.Ordinal) || value.Prefix.Contains('\\') || value.Prefix.Contains("//", StringComparison.Ordinal) || !System.Text.RegularExpressions.Regex.IsMatch(value.Prefix, "^[a-z0-9](?:[a-z0-9/_-]{0,126}[a-z0-9])?$")) throw new IdentityVerificationConfigurationException();
        if (value.AcceptedContentTypes.Length == 0 || value.AcceptedContentTypes.Any(x => x is not ("image/jpeg" or "image/png" or "application/pdf")) || value.AllowOverwrite) throw new IdentityVerificationConfigurationException();
        return value;
    }
    private static void ValidateStorageKey(string key, string prefix)
    {
        var marker = prefix + "/";
        if (!key.StartsWith(marker, StringComparison.Ordinal) || key.Contains("..", StringComparison.Ordinal) || key.Contains('\\') || key.Contains("//", StringComparison.Ordinal)) throw new IdentityVerificationValidationException(["StorageKey"]);
        var leaf = key[marker.Length..]; if (!leaf.EndsWith(".bin", StringComparison.Ordinal) || !Guid.TryParseExact(leaf[..^4], "N", out _)) throw new IdentityVerificationValidationException(["StorageKey"]);
    }
    private sealed class ExactSizeHashingReadStream(Stream inner, long expected) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public long BytesRead { get; private set; }
        public byte[] GetHash() => hash.GetHashAndReset();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => expected; public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (BytesRead == expected) { Span<byte> extra = stackalloc byte[1]; if (inner.Read(extra) != 0) throw new IdentityVerificationDocumentSizeException(); return 0; }
            var read = inner.Read(buffer, offset, (int)Math.Min(count, expected - BytesRead)); if (read > 0) { hash.AppendData(buffer, offset, read); BytesRead += read; } return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (BytesRead == expected) { var extra = new byte[1]; if (await inner.ReadAsync(extra, cancellationToken) != 0) throw new IdentityVerificationDocumentSizeException(); return 0; }
            var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, expected - BytesRead)], cancellationToken); if (read > 0) { hash.AppendData(buffer.Span[..read]); BytesRead += read; } return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
    }
}
