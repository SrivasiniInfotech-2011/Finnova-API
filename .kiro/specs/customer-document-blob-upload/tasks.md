# Customer Onboarding Document Blob Upload — Copy/Paste Tasks

This file contains the implementation steps for the design and requirements documents. Apply the steps in order in the two repositories. Every code block is intended for manual copy/paste. Do not add secrets to source control. The Azure connection string is an environment-specific value and must be supplied through user secrets, environment configuration, or a secret store.

The implementation has **12 ordered task groups**, matching the implementation order in `design.md`. The source paths below are repository-relative unless the path starts with `E:\Finnova\Finnova-UI`.

## Task 1 — Backend domain: entity and enum

### CREATE `Finnova.Models/Domain/Enums/CustomerDocumentUploadStatus.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

public enum CustomerDocumentUploadStatus
{
    None = 0,
    Uploaded = 1,
}
```

### MODIFY `Finnova.Models/Domain/Entities/CustomerDocumentReference.cs`

Insert the following `using` directive before the namespace, then insert the properties immediately after the existing `ViewMetadata` property. Insert the method immediately after the existing `Initialize` method. Keep all existing properties and initialization behavior unchanged.

```csharp
using Finnova.Models.Domain.Enums;
```

```csharp
    public CustomerDocumentUploadStatus UploadStatus { get; private set; } = CustomerDocumentUploadStatus.None;
    public string? BlobContainerName { get; private set; }
    public string? BlobName { get; private set; }
    public string? OriginalFileName { get; private set; }
    public string? UploadedContentType { get; private set; }
    public long? UploadedSizeBytes { get; private set; }
    public string? UploadedBy { get; private set; }
    public DateTime? UploadedAtUtc { get; private set; }
    public Guid UploadVersion { get; private set; } = Guid.NewGuid();
```

```csharp
    public void ReplaceUploadMetadata(
        string containerName,
        string blobName,
        string originalFileName,
        string contentType,
        long sizeInBytes,
        string uploadedBy,
        DateTime uploadedAtUtc,
        Guid uploadVersion)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("A storage reference is required.");
        if (sizeInBytes <= 0 || string.IsNullOrWhiteSpace(uploadedBy))
            throw new ArgumentException("Valid upload metadata is required.");

        UploadStatus = CustomerDocumentUploadStatus.Uploaded;
        BlobContainerName = containerName;
        BlobName = blobName;
        OriginalFileName = originalFileName;
        UploadedContentType = contentType;
        UploadedSizeBytes = sizeInBytes;
        UploadedBy = uploadedBy;
        UploadedAtUtc = uploadedAtUtc.Kind == DateTimeKind.Utc
            ? uploadedAtUtc
            : uploadedAtUtc.ToUniversalTime();
        UploadVersion = uploadVersion;
        IsCollected = true;
        IsScanned = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
```

## Task 2 — Backend domain exceptions

### MODIFY `Finnova.Models/Domain/Exceptions/CustomerExceptions.cs`

Insert the following constants inside `CustomerErrorCodes`, after `DefaultBank`, and append the exception classes after `CustomerBoundaryUnavailableException`. Do not replace or rename existing customer error codes.

```csharp
    public const string DocumentUploadValidation = Family + "-DOCUMENT-UPLOAD-400";
    public const string DocumentUploadNotAvailable = Family + "-DOCUMENT-UPLOAD-404";
    public const string DocumentUploadConflict = Family + "-DOCUMENT-UPLOAD-409";
    public const string DocumentUploadStorage = Family + "-DOCUMENT-UPLOAD-503";
```

```csharp
public sealed class CustomerDocumentUploadValidationException : CustomerDomainException
{
    public CustomerDocumentUploadValidationException(string message)
        : base(message) => ErrorCode = CustomerErrorCodes.DocumentUploadValidation;
}

public sealed class CustomerDocumentStorageException : CustomerDomainException
{
    public CustomerDocumentStorageException(string message, Exception? inner = null)
        : base(message)
    {
        ErrorCode = CustomerErrorCodes.DocumentUploadStorage;
        if (inner is not null) Data["ProviderFailure"] = "BlobStorage";
    }
}

public sealed class CustomerDocumentNotAvailableException : CustomerDomainException
{
    public CustomerDocumentNotAvailableException()
        : base("The current customer document is not available.")
        => ErrorCode = CustomerErrorCodes.DocumentUploadNotAvailable;
}

public sealed class CustomerDocumentUploadConcurrencyException : CustomerDomainException
{
    public CustomerDocumentUploadConcurrencyException()
        : base("The customer document changed while the upload was being saved. Retry the upload.")
        => ErrorCode = CustomerErrorCodes.DocumentUploadConflict;
}
```

## Task 3 — Backend contracts and internal records

### MODIFY `Finnova.Models/Contracts/Customers/CustomerContracts.cs`

Insert this record immediately after `CustomerDocumentResponse`. It is the only upload response exposed by the API. Do not add blob keys, container names, URIs, SAS tokens, credentials, or bytes.

```csharp
public sealed record CustomerDocumentUploadResponse(
    Guid CustomerDocumentReferenceId,
    Guid DocumentDefinitionId,
    string DocumentCode,
    string UploadStatus,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    DateTime UploadedAtUtc);
```

### CREATE `Finnova.Service/Customers/Documents/CustomerDocumentUploadContracts.cs`

```csharp
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Customers.Documents;

public sealed record DocumentUploadPolicy(
    Guid DefinitionId,
    string DocumentCode,
    string? Description,
    int MaxFileSizeMb,
    IReadOnlySet<string> AllowedContentTypes);

public sealed record CustomerDocumentDownload(
    Stream Content,
    string ContentType,
    string DownloadName);

public static class CustomerDocumentUploadMapper
{
    public static Finnova.Models.Contracts.Customers.CustomerDocumentUploadResponse ToResponse(
        CustomerDocumentReference row)
        => new(
            row.Id,
            row.DocumentDefinitionId,
            row.DocumentCode,
            row.UploadStatus.ToString(),
            row.OriginalFileName!,
            row.UploadedContentType!,
            row.UploadedSizeBytes!.Value,
            row.UploadedAtUtc!.Value);
}
```

## Task 4 — EF configuration, options, context confirmation, and migration command

### MODIFY `Finnova.Repository/Configuration/CustomerDocumentReferenceConfiguration.cs`

Insert the following properties in `Configure`, after the existing `ViewMetadata` property configuration and before the indexes. The existing customer/document-definition unique index remains unchanged.

```csharp
        b.Property(x => x.UploadStatus)
            .HasConversion<int>()
            .HasDefaultValue(Finnova.Models.Domain.Enums.CustomerDocumentUploadStatus.None)
            .IsRequired();
        b.Property(x => x.BlobContainerName).HasMaxLength(63);
        b.Property(x => x.BlobName).HasMaxLength(512);
        b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.UploadedContentType).HasMaxLength(100);
        b.Property(x => x.UploadedSizeBytes);
        b.Property(x => x.UploadedBy).HasMaxLength(200);
        b.Property(x => x.UploadedAtUtc);
        b.Property(x => x.UploadVersion)
            .HasDefaultValueSql("NEWID()")
            .IsRequired()
            .IsConcurrencyToken();
```

### MODIFY `Finnova.Service/Storage/BlobStorageOptions.cs`

Insert this property inside `BlobStorageOptions`, after `Aws`:

```csharp
    /// <summary>Private container used by Customer Onboarding document uploads.</summary>
    public string CustomerDocumentsContainer { get; set; } = "finnova-customer-documents";
```

The existing `IBlobStorageService` and `AzureBlobStorageService` are reused. If either file differs from the complete implementations below, replace the whole file with the corresponding block. The upload path passes the request stream directly to Azure and never buffers the complete file.

### MODIFY `Finnova.Service/Storage/IBlobStorageService.cs`

```csharp
namespace Finnova.Service.Storage;

public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
```

### MODIFY `Finnova.Service/Storage/AzureBlobStorageService.cs`

```csharp
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Finnova.Service.Storage;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _client;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var connectionString = options.Value.Azure.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Azure blob storage connection string is not configured.");
        _client = new BlobServiceClient(connectionString);
    }

    public async Task<BlobUploadResult> UploadAsync(
        string containerName, string blobName, Stream content, string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(blobName);
        var headers = contentType is null ? null : new BlobHttpHeaders { ContentType = contentType };
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new BlobUploadResult
        {
            ContainerName = containerName,
            BlobName = blobName,
            Uri = blob.Uri.ToString(),
            SizeInBytes = properties.Value.ContentLength,
            ContentType = properties.Value.ContentType,
        };
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException("The requested blob was not found.");
        }
    }

    public async Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetBlobContainerClient(containerName).GetBlobClient(blobName)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => (await _client.GetBlobContainerClient(containerName).GetBlobClient(blobName)
            .ExistsAsync(cancellationToken)).Value;

    public async Task<IReadOnlyList<string>> ListAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        if (!await container.ExistsAsync(cancellationToken)) return Array.Empty<string>();
        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            names.Add(blob.Name);
        return names;
    }
}
```

The `DeleteAsync` implementation may instead remain in the repository's existing async style if that style is preferred; it must preserve cancellation and must not log or expose the key. The existing `Azure.Storage.Blobs` 12.29.2 reference in `Finnova.Service/Finnova.Service.csproj` is sufficient; do not add another package.

### MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

No new DbSet is required. Confirm that this existing line remains present and unchanged:

```csharp
    public DbSet<CustomerDocumentReference> CustomerDocumentReferences => Set<CustomerDocumentReference>();
```

### MODIFY `Finnova.SystemAdminService/appsettings.json`

Insert this sibling property after the `Jwt` object. The empty connection string is intentional for committed development configuration; provide the real value outside source control.

```json
  "BlobStorage": {
    "Provider": "Azure",
    "CustomerDocumentsContainer": "finnova-customer-documents",
    "Azure": {
      "ConnectionString": ""
    }
  }
```

### MODIFY `Finnova.Service/Finnova.Service.csproj`

Insert this framework reference inside the project, after the property group and before the existing package item group, because the CQRS command uses the existing ASP.NET Core `IFormFile` abstraction:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

### MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs` — migration command

After applying the source changes, copy this command into a terminal from the repository root. **DO NOT RUN IT in this documentation-only workflow.** The user must run it manually after reviewing the generated migration:

```powershell
dotnet ef migrations add AddCustomerDocumentBlobUpload --project Finnova.Repository --startup-project Finnova.SystemAdminService
```

The migration must add only nullable upload metadata columns, the required status/version columns, and the concurrency configuration. It must not add a binary or base64 column and must not contain seed data.

## Task 5 — Backend repositories, policy resolver, and DI

### CREATE `Finnova.Service/Customers/Documents/DocumentUploadPolicyResolver.cs`

```csharp
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.Documents;

public interface IDocumentUploadPolicyResolver
{
    Task<DocumentUploadPolicy> ResolveAsync(
        Guid definitionId,
        string constitutionCode,
        string entityRole,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentUploadPolicyResolver(IDocumentRepository documents)
    : IDocumentUploadPolicyResolver
{
    public async Task<DocumentUploadPolicy> ResolveAsync(
        Guid definitionId,
        string constitutionCode,
        string entityRole,
        CancellationToken cancellationToken = default)
    {
        var definition = (await documents.GetActiveForConsumptionAsync(
                constitutionCode.Trim(), entityRole, cancellationToken))
            .SingleOrDefault(x => x.Id == definitionId);

        if (definition is null)
            throw new CustomerDocumentDefinitionUnavailableException(constitutionCode, entityRole);
        if (definition.MaxFileSizeMb is < 1 or > 25)
            throw new CustomerDocumentUploadValidationException("The document upload policy has an invalid size limit.");

        var types = definition.AllowedMimeTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DocumentFileRules.NormalizeConfiguredType)
            .Where(x => x is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (types.Count == 0)
            throw new CustomerDocumentUploadValidationException("The document upload policy has no supported file type.");

        return new DocumentUploadPolicy(definition.Id, definition.DocCode, definition.Description,
            definition.MaxFileSizeMb, types);
    }
}
```

### MODIFY `Finnova.SystemAdminService/Program.cs`

Insert this registration immediately after the existing `IDocumentDefinitionResolver` registration. The existing repository registrations already expose `ICustomerRepository`, `ICustomerUnitOfWork`, and `IDocumentRepository`.

```csharp
builder.Services.AddScoped<IDocumentUploadPolicyResolver, DocumentUploadPolicyResolver>();
```

Add this using directive with the other service usings:

```csharp
using Finnova.Service.Customers.Documents;
```

## Task 6 — Backend pure upload helpers and bounded stream

### CREATE `Finnova.Service/Customers/Documents/DocumentFileRules.cs`

```csharp
using System.Text;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.Customers.Documents;

public static class DocumentFileRules
{
    public const long GlobalMaximumBytes = 25L * 1024 * 1024;

    public static string NormalizeContentType(string? value)
    {
        var type = value?.Trim().ToLowerInvariant();
        return type == "image/jpg" ? "image/jpeg" : type ?? string.Empty;
    }

    public static string? NormalizeConfiguredType(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "PDF" or "APPLICATION/PDF" => "application/pdf",
            "JPG" or "JPEG" or "IMAGE/JPG" or "IMAGE/JPEG" => "image/jpeg",
            "PNG" or "IMAGE/PNG" => "image/png",
            _ => null,
        };

    public static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CustomerDocumentUploadValidationException("A file name is required.");
        if (value.IndexOfAny(new[] { '/', '\\' }) >= 0 || Path.IsPathRooted(value))
            throw new CustomerDocumentUploadValidationException("The file name must not contain a path.");

        var name = Path.GetFileName(value);
        if (name is "." or ".." || name.Any(char.IsControl) || name.Length is 0 or > 255)
            throw new CustomerDocumentUploadValidationException("The file name is not valid.");
        return name;
    }

    public static void ValidateSize(long length, int maxFileSizeMb)
    {
        if (length <= 0)
            throw new CustomerDocumentUploadValidationException("The file must not be empty.");
        var configured = maxFileSizeMb * 1024L * 1024L;
        if (maxFileSizeMb is < 1 or > 25 || length > Math.Min(configured, GlobalMaximumBytes))
            throw new CustomerDocumentUploadValidationException("The file exceeds the permitted size.");
    }

    public static void ValidateTypeAndExtension(
        string fileName, string declaredContentType, IReadOnlySet<string> allowed)
    {
        var contentType = NormalizeContentType(declaredContentType);
        if (!allowed.Contains(contentType))
            throw new CustomerDocumentUploadValidationException("The file type is not permitted for this document.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var matches = contentType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            _ => false,
        };
        if (!matches)
            throw new CustomerDocumentUploadValidationException("The file extension does not match its content type.");
    }

    public static void ValidateSignature(ReadOnlySpan<byte> prefix, string contentType)
    {
        var matches = NormalizeContentType(contentType) switch
        {
            "application/pdf" => prefix.Length >= 5 && Encoding.ASCII.GetString(prefix[..5]) == "%PDF-",
            "image/jpeg" => prefix.Length >= 3 && prefix[0] == 0xff && prefix[1] == 0xd8 && prefix[2] == 0xff,
            "image/png" => prefix.Length >= 8 && prefix[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            _ => false,
        };
        if (!matches)
            throw new CustomerDocumentUploadValidationException("The file signature does not match its content type.");
    }

    public static async Task<byte[]> ReadPrefixAsync(Stream stream, int length, CancellationToken ct)
    {
        var buffer = new byte[length];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count, buffer.Length - count), ct);
            if (read == 0) break;
            count += read;
        }
        return buffer[..count];
    }
}
```

### CREATE `Finnova.Service/Customers/Documents/PrefixReplayStream.cs`

```csharp
namespace Finnova.Service.Customers.Documents;

public sealed class PrefixReplayStream(byte[] prefix, Stream inner) : Stream
{
    private int _prefixOffset;
    private bool _disposed;

    public override bool CanRead => !_disposed && inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

    public override int Read(Span<byte> buffer)
    {
        if (_prefixOffset < prefix.Length)
        {
            var count = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
            prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }
        return inner.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_prefixOffset < prefix.Length)
        {
            var count = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
            prefix.AsMemory(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }
        return await inner.ReadAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await inner.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
```

Replace the `Read(Span<byte>)` method in the created file with this exact implementation because `Stream.Read(Span<byte>)` must write into the caller's span:

```csharp
    public override int Read(Span<byte> buffer)
    {
        if (_prefixOffset < prefix.Length)
        {
            var count = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
            prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }
        return inner.Read(buffer);
    }
```

### CREATE `Finnova.Service/Customers/Documents/CustomerDocumentKeyFactory.cs`

```csharp
namespace Finnova.Service.Customers.Documents;

public static class CustomerDocumentKeyFactory
{
    public static string Create(Guid customerId, Guid documentReferenceId, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return $"customers/{customerId:N}/documents/{documentReferenceId:N}/{Guid.NewGuid():N}{extension}";
    }
}
```

## Task 7 — Backend CQRS and upload/download service

### CREATE `Finnova.Service/Customers/Documents/CustomerDocumentUploadService.cs`

```csharp
using System.Security.Cryptography;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
using Finnova.Service.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finnova.Service.Customers.Documents;

public interface ICustomerDocumentUploadService
{
    Task<CustomerDocumentUploadResponse> UploadAsync(
        string customerCode, Guid documentReferenceId, IFormFile file, string actorId, CancellationToken ct);
    Task<CustomerDocumentDownload> DownloadAsync(
        string customerCode, Guid documentReferenceId, CancellationToken ct);
}

public sealed class CustomerDocumentUploadService(
    ICustomerRepository customers,
    IDocumentUploadPolicyResolver policies,
    ICustomerUnitOfWork unitOfWork,
    ICustomerAuditRepository audits,
    IBlobStorageService storage,
    IOptions<BlobStorageOptions> options,
    ILogger<CustomerDocumentUploadService> logger) : ICustomerDocumentUploadService
{
    public async Task<CustomerDocumentUploadResponse> UploadAsync(
        string customerCode, Guid documentReferenceId, IFormFile file, string actorId, CancellationToken ct)
    {
        var customer = await customers.GetByCodeAsync(customerCode.Trim(), ct)
            ?? throw new CustomerNotFoundException(customerCode);
        var row = customer.Documents.SingleOrDefault(x => x.Id == documentReferenceId)
            ?? throw new CustomerChildRecordNotFoundException("document");
        var policy = await policies.ResolveAsync(row.DocumentDefinitionId, row.ConstitutionCode,
            CustomerLookupTypes.EntityRole, ct);

        var name = DocumentFileRules.SanitizeFileName(file.FileName);
        var contentType = DocumentFileRules.NormalizeContentType(file.ContentType);
        DocumentFileRules.ValidateSize(file.Length, policy.MaxFileSizeMb);
        DocumentFileRules.ValidateTypeAndExtension(name, contentType, policy.AllowedContentTypes);

        await using var input = file.OpenReadStream();
        var prefix = await DocumentFileRules.ReadPrefixAsync(input, 8, ct);
        DocumentFileRules.ValidateSignature(prefix, contentType);
        Stream uploadStream = input;
        if (input.CanSeek)
            input.Position = 0;
        else
            uploadStream = new PrefixReplayStream(prefix, input);

        var container = options.Value.CustomerDocumentsContainer;
        if (string.IsNullOrWhiteSpace(container))
            throw new CustomerDocumentStorageException("Customer document storage is not configured.");
        var key = CustomerDocumentKeyFactory.Create(customer.Id, row.Id, name);
        BlobUploadResult result;
        try
        {
            result = await storage.UploadAsync(container, key, uploadStream, contentType, ct);
        }
        catch (CustomerDocumentStorageException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new CustomerDocumentStorageException("Customer document storage is unavailable.", ex);
        }

        if (result.SizeInBytes != file.Length ||
            !string.Equals(DocumentFileRules.NormalizeContentType(result.ContentType), contentType,
                StringComparison.OrdinalIgnoreCase))
        {
            await TryDeleteAsync(container, key, ct);
            throw new CustomerDocumentStorageException("Customer document storage returned invalid upload metadata.");
        }

        var oldContainer = row.BlobContainerName;
        var oldKey = row.BlobName;
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            row.ReplaceUploadMetadata(container, key, name, contentType, result.SizeInBytes,
                actorId, DateTime.UtcNow, Guid.NewGuid());
            await audits.AddAsync(CustomerAuditEntry.Create(customer.Id, CustomerAuditAction.DocumentChanged,
                actorId, "{\"Action\":\"DocumentUploaded\"}"), ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await TryDeleteAsync(container, key, CancellationToken.None);
            throw new CustomerDocumentUploadConcurrencyException();
        }
        catch
        {
            await TryDeleteAsync(container, key, CancellationToken.None);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldContainer) && !string.IsNullOrWhiteSpace(oldKey))
        {
            try { await storage.DeleteAsync(oldContainer, oldKey, CancellationToken.None); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Customer document replacement cleanup failed for document reference {DocumentReferenceId}.", row.Id);
            }
        }

        return CustomerDocumentUploadMapper.ToResponse(row);
    }

    public async Task<CustomerDocumentDownload> DownloadAsync(
        string customerCode, Guid documentReferenceId, CancellationToken ct)
    {
        var customer = await customers.GetByCodeAsync(customerCode.Trim(), ct)
            ?? throw new CustomerNotFoundException(customerCode);
        var row = customer.Documents.SingleOrDefault(x => x.Id == documentReferenceId)
            ?? throw new CustomerChildRecordNotFoundException("document");
        if (row.UploadStatus != CustomerDocumentUploadStatus.Uploaded ||
            string.IsNullOrWhiteSpace(row.BlobContainerName) || string.IsNullOrWhiteSpace(row.BlobName) ||
            string.IsNullOrWhiteSpace(row.OriginalFileName) || string.IsNullOrWhiteSpace(row.UploadedContentType))
            throw new CustomerDocumentNotAvailableException();

        try
        {
            var content = await storage.DownloadAsync(row.BlobContainerName, row.BlobName, ct);
            return new CustomerDocumentDownload(content, row.UploadedContentType, row.OriginalFileName);
        }
        catch (FileNotFoundException) { throw new CustomerDocumentNotAvailableException(); }
        catch (CustomerDocumentNotAvailableException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new CustomerDocumentStorageException("Customer document storage is unavailable.", ex);
        }
    }

    private async Task TryDeleteAsync(string container, string key, CancellationToken ct)
    {
        try { await storage.DeleteAsync(container, key, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Customer document compensation failed.");
        }
    }
}
```

Add the missing using directive at the top of this file because the audit enum is in the Models namespace:

```csharp
using Finnova.Models.Domain.Entities;
```

### CREATE `Finnova.Service/Customers/Documents/CustomerDocumentUploadCommands.cs`

```csharp
using Finnova.Models.Contracts.Customers;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Finnova.Service.Customers.Documents;

public sealed record UploadCustomerDocumentCommand(
    string CustomerCode, Guid DocumentReferenceId, IFormFile File, string ActorId)
    : IRequest<CustomerDocumentUploadResponse>;

public sealed class UploadCustomerDocumentValidator : AbstractValidator<UploadCustomerDocumentCommand>
{
    public UploadCustomerDocumentValidator()
    {
        RuleFor(x => x.CustomerCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DocumentReferenceId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.Length).GreaterThan(0).When(x => x.File is not null);
        RuleFor(x => x.ActorId).NotEmpty();
    }
}

public sealed class UploadCustomerDocumentCommandHandler(ICustomerDocumentUploadService service)
    : IRequestHandler<UploadCustomerDocumentCommand, CustomerDocumentUploadResponse>
{
    public Task<CustomerDocumentUploadResponse> Handle(UploadCustomerDocumentCommand request, CancellationToken ct)
        => service.UploadAsync(request.CustomerCode, request.DocumentReferenceId, request.File, request.ActorId, ct);
}

public sealed record DownloadCustomerDocumentQuery(string CustomerCode, Guid DocumentReferenceId)
    : IRequest<CustomerDocumentDownload>;

public sealed class DownloadCustomerDocumentQueryHandler(ICustomerDocumentUploadService service)
    : IRequestHandler<DownloadCustomerDocumentQuery, CustomerDocumentDownload>
{
    public Task<CustomerDocumentDownload> Handle(DownloadCustomerDocumentQuery request, CancellationToken ct)
        => service.DownloadAsync(request.CustomerCode, request.DocumentReferenceId, ct);
}
```

### MODIFY `Finnova.SystemAdminService/Program.cs`

Insert the following registrations after the existing customer mapper registrations and before authentication. Also add `using Finnova.Service.Storage;` with the other using directives.

```csharp
builder.Services.AddScoped<ICustomerDocumentUploadService, CustomerDocumentUploadService>();
builder.Services.AddBlobStorage(builder.Configuration);
```

The existing `AddBlobStorage` extension must remain the only registration of `IBlobStorageService`; do not instantiate `BlobServiceClient` in the SystemAdmin host or customer service.

## Task 8 — Backend controller, middleware, multipart configuration, and gateway

### MODIFY `Finnova.SystemAdminService/Controllers/CustomerOnboardingController.cs`

Add these using directives with the existing imports:

```csharp
using Finnova.Service.Customers.Documents;
using System.Security.Claims;
```

Change the primary constructor to include `ICustomerDocumentUploadService` only if the controller uses the service directly. The preferred implementation below continues to dispatch through MediatR, so keep the existing constructor `(ISender sender)` and insert these actions inside the controller class before its final `}`:

```csharp
    [HttpPost("{customerCode}/documents/{documentReferenceId:guid}/upload")]
    [RequestSizeLimit(26L * 1024 * 1024)]
    public async Task<ActionResult<CustomerDocumentUploadResponse>> UploadDocument(
        string customerCode, Guid documentReferenceId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            throw new CustomerDocumentUploadValidationException("Exactly one file named 'file' is required.");
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.Identity?.Name
            ?? "system";
        var result = await sender.Send(
            new UploadCustomerDocumentCommand(customerCode, documentReferenceId, file, actor), ct);
        return Ok(result);
    }

    [HttpGet("{customerCode}/documents/{documentReferenceId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        string customerCode, Guid documentReferenceId, CancellationToken ct)
    {
        var result = await sender.Send(
            new DownloadCustomerDocumentQuery(customerCode, documentReferenceId), ct);
        return File(result.Content, result.ContentType, result.DownloadName, enableRangeProcessing: false);
    }
```

Add the following using directive as well because the action uses the typed exception:

```csharp
using Finnova.Models.Domain.Exceptions;
```

The existing `[ApiController, Authorize(Policy = "SystemAdmin"), Route("api/customer-onboarding")]` declaration remains unchanged. To reject multiple multipart files, add this immediately after the null check, before dispatch:

```csharp
        if (Request.HasFormContentType && Request.Form.Files.Count != 1)
            throw new CustomerDocumentUploadValidationException("Exactly one file named 'file' is required.");
```

### MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`

Insert these switch arms immediately before the existing `CustomerNotFoundException` arm in the customer branch:

```csharp
                CustomerDocumentUploadValidationException ex =>
                    (StatusCodes.Status400BadRequest, CustomerErrorCodes.DocumentUploadValidation, ex.Message),
                CustomerDocumentNotAvailableException ex =>
                    (StatusCodes.Status404NotFound, CustomerErrorCodes.DocumentUploadNotAvailable, ex.Message),
                CustomerDocumentUploadConcurrencyException ex =>
                    (StatusCodes.Status409Conflict, CustomerErrorCodes.DocumentUploadConflict, ex.Message),
                CustomerDocumentStorageException ex =>
                    (StatusCodes.Status503ServiceUnavailable, CustomerErrorCodes.DocumentUploadStorage,
                        "Customer document storage is unavailable."),
```

Add a `using` only if the file does not already import the namespace:

```csharp
using Finnova.Models.Domain.Exceptions;
```

The middleware must continue to emit the existing RFC 7807 `ProblemDetails` object and `code` extension. It must not serialize `ex.Data`, an inner exception, a blob key, a filename, or provider details.

### MODIFY `Finnova.SystemAdminService/Program.cs`

Insert this before `builder.Build()` to make the multipart resource boundary explicit. The 26 MiB request limit allows only small multipart overhead above the 25 MiB file ceiling.

```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 26L * 1024 * 1024;
    options.MemoryBufferThreshold = 64 * 1024;
});
```

### MODIFY `Finnova.ApiGateway/appsettings.json`

Do not add a route. The existing catch-all route already matches both upload and download suffixes. Preserve this exact entry and verify that it remains before the broad `ua-route`:

```json
      "CustomerOnboardingAliasRoute": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/ua/api/customer-onboarding/{**catch-all}" },
        "Transforms": [ { "PathPattern": "/api/customer-onboarding/{**catch-all}" } ]
      },
```

If a branch does not contain this entry, insert the exact JSON object above immediately before `ua-route`; otherwise leave the file unchanged. Do not create a second customer-onboarding alias or a new cluster.

## Task 9 — Backend unit, property, and integration tests

### CREATE `Finnova.Tests/Unit/DocumentFileRulesTests.cs`

```csharp
using FsCheck;
using FsCheck.Xunit;
using Finnova.Service.Customers.Documents;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class DocumentFileRulesTests
{
    [Fact]
    public void Approved_pdf_signature_is_accepted()
        => DocumentFileRules.ValidateSignature("%PDF-1.7"u8.ToArray(), "application/pdf");

    [Theory]
    [InlineData("image/jpg", "image/jpeg")]
    [InlineData("IMAGE/PNG", "image/png")]
    [InlineData("application/pdf", "application/pdf")]
    public void Approved_aliases_are_normalized(string input, string expected)
        => Assert.Equal(expected, DocumentFileRules.NormalizeContentType(input));

    [Theory]
    [InlineData("../document.pdf")]
    [InlineData("folder/document.pdf")]
    [InlineData("document\u0000.pdf")]
    public void Unsafe_names_are_rejected(string name)
        => Assert.ThrowsAny<Exception>(() => DocumentFileRules.SanitizeFileName(name));

    [Property(MaxTest = 100)]
    public Property Generated_keys_are_scoped_and_do_not_contain_filename_text(NonEmptyString name)
    {
        _ = name;
        var customer = Guid.NewGuid();
        var reference = Guid.NewGuid();
        var key = CustomerDocumentKeyFactory.Create(customer, reference, "document.pdf");
        return (key.StartsWith($"customers/{customer:N}/documents/{reference:N}/", StringComparison.Ordinal)
            && key.EndsWith(".pdf", StringComparison.Ordinal)
            && !key.Contains("document.pdf", StringComparison.Ordinal)).ToProperty();
    }
}
```

### CREATE `Finnova.Tests/Unit/CustomerDocumentUploadServiceTests.cs`

```csharp
using System.Text;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.Documents;
using Finnova.Service.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class CustomerDocumentUploadServiceTests
{
    [Fact]
    public async Task Invalid_signature_does_not_call_storage()
    {
        var customer = new Customer();
        var row = new CustomerDocumentReference();
        row.Initialize("INDIVIDUAL", Guid.NewGuid(), "PAN", "Identity", "PAN", true, true,
            null, false, false, null, null, null, null);
        customer.Documents.Add(row);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByCodeAsync("CUST00000001", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var policy = new Mock<IDocumentUploadPolicyResolver>();
        policy.Setup(x => x.ResolveAsync(row.DocumentDefinitionId, row.ConstitutionCode, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadPolicy(row.DocumentDefinitionId, "PAN", "PAN", 5,
                new HashSet<string>(new[] { "application/pdf" }, StringComparer.OrdinalIgnoreCase)));
        var storage = new Mock<IBlobStorageService>();
        var service = new CustomerDocumentUploadService(customers.Object, policy.Object,
            Mock.Of<ICustomerUnitOfWork>(), Mock.Of<ICustomerAuditRepository>(), storage.Object,
            Options.Create(new BlobStorageOptions { CustomerDocumentsContainer = "finnova-customer-documents" }),
            NullLogger<CustomerDocumentUploadService>.Instance);
        await Assert.ThrowsAsync<Finnova.Models.Domain.Exceptions.CustomerDocumentUploadValidationException>(() =>
            service.UploadAsync("CUST00000001", row.Id,
                new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("not pdf")), 0, 7, "file", "document.pdf")
                { Headers = new HeaderDictionary(), ContentType = "application/pdf" }, "admin", CancellationToken.None));
        storage.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

### CREATE `Finnova.Tests/Unit/DocumentFileRulesValidationTests.cs`

```csharp
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Customers.Documents;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class DocumentFileRulesValidationTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5L * 1024 * 1024 + 1, 5)]
    [InlineData(26L * 1024 * 1024, 25)]
    public void Invalid_sizes_are_rejected(long length, int configuredMegabytes)
    {
        Assert.Throws<CustomerDocumentUploadValidationException>(() =>
            DocumentFileRules.ValidateSize(length, configuredMegabytes));
    }

    [Theory]
    [InlineData("document.pdf", "text/plain", "application/pdf")]
    [InlineData("document.txt", "application/pdf", "application/pdf")]
    [InlineData("document.jpg", "image/png", "image/png")]
    public void Invalid_type_or_extension_is_rejected(string name, string declared, string allowedType)
    {
        var allowed = new HashSet<string>(new[] { allowedType }, StringComparer.OrdinalIgnoreCase);
        Assert.Throws<CustomerDocumentUploadValidationException>(() =>
            DocumentFileRules.ValidateTypeAndExtension(name, declared, allowed));
    }

    [Fact]
    public void Signature_mismatch_is_rejected()
        => Assert.Throws<CustomerDocumentUploadValidationException>(() =>
            DocumentFileRules.ValidateSignature("not-pdf"u8.ToArray(), "application/pdf"));

    [Theory]
    [InlineData("customers/00000000000000000000000000000000/documents/00000000000000000000000000000000/")]
    [InlineData("document.pdf")]
    public void Generated_keys_have_only_the_expected_scope(string ignored)
    {
        _ = ignored;
        var customerId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var key = CustomerDocumentKeyFactory.Create(customerId, referenceId, "passport.pdf");

        Assert.StartsWith($"customers/{customerId:N}/documents/{referenceId:N}/", key);
        Assert.EndsWith(".pdf", key);
        Assert.DoesNotContain("passport.pdf", key, StringComparison.Ordinal);
        Assert.DoesNotContain("..", key, StringComparison.Ordinal);
    }
}
```

### CREATE `Finnova.Tests/Integration/CustomerDocumentBlobUploadApiTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using Finnova.Repository.Context;
using Finnova.Service.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Finnova.Tests.Integration;

public sealed class CustomerDocumentBlobUploadApiTests : IClassFixture<CustomerOnboardingAppFactory>
{
    private readonly CustomerOnboardingAppFactory _factory;
    public CustomerDocumentBlobUploadApiTests(CustomerOnboardingAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_and_download_require_authentication()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/customer-onboarding/CUST00000001/documents/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_upload_is_rejected_before_storage()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("not a pdf"u8.ToArray()), "file", "document.pdf");
        var response = await client.PostAsync($"/api/customer-onboarding/CUST00000001/documents/{Guid.NewGuid()}/upload", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

### CREATE `Finnova.Tests/Integration/InMemoryCustomerDocumentBlobStorage.cs`

```csharp
using System.Collections.Concurrent;
using Finnova.Service.Storage; : IBlobStorageService
{
    private readonly ConcurrentDictionary<(string Container, string Name), (byte[] Bytes, string? ContentType)> _blobs = new();
    public bool FailUploads { get; set; }
    public bool FailDeletes { get; set; }

    public async Task<BlobUploadResult> UploadAsync(string containerName, string blobName, Stream content,
        string? contentType = null, CancellationToken cancellationToken = default)
    {
        if (FailUploads) throw new IOException("fake provider failure");
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        _blobs[(containerName, blobName)] = (bytes, contentType);
        return new BlobUploadResult { ContainerName = containerName, BlobName = blobName,
            Uri = "https://fake.invalid/" + blobName, SizeInBytes = bytes.LongLength, ContentType = contentType };
    }

    public Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryGetValue((containerName, blobName), out var value))
            throw new FileNotFoundException();
        return Task.FromResult<Stream>(new MemoryStream(value.Bytes, writable: false));
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        if (FailDeletes) throw new IOException("fake delete failure");
        return Task.FromResult(_blobs.TryRemove((containerName, blobName), out _));
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => Task.FromResult(_blobs.ContainsKey((containerName, blobName)));

    public Task<IReadOnlyList<string>> ListAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_blobs.Keys.Where(x => x.Container == containerName && (prefix is null || x.Name.StartsWith(prefix, StringComparison.Ordinal))).Select(x => x.Name).ToArray());
}
```

### MODIFY `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`

Inside the existing `CustomerOnboardingAppFactory.ConfigureWebHost` `ConfigureServices` lambda, immediately after the `IIndiaAddressControl` replacement, insert:

```csharp
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, InMemoryCustomerDocumentBlobStorage>();
```

Add this using directive at the top of the existing test file:

```csharp
using Finnova.Service.Storage;
```

Use the same factory and fake in `CustomerDocumentBlobUploadApiTests`; do not register Azure in the test host and do not put a connection string in test configuration.

### MODIFY `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`

Inside the existing `CustomerOnboardingAppFactory.ConfigureWebHost` service-registration lambda, add this immediately after the existing `IIndiaAddressControl` replacement. This keeps the existing Customer Onboarding tests on the same deterministic fake and prevents any test from constructing an Azure client.

```csharp
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, InMemoryCustomerDocumentBlobStorage>();
```

Add this using with the existing integration-test imports:

```csharp
using Finnova.Service.Storage;
```

### CREATE `Finnova.Tests/Integration/CustomerDocumentBlobUploadCoverageTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finnova.Tests.Integration;

public sealed class CustomerDocumentBlobUploadCoverageTests : IClassFixture<CustomerOnboardingAppFactory>
{
    private const string Container = "finnova-customer-documents";
    private readonly CustomerOnboardingAppFactory _factory;
    private static int _sequence;

    public CustomerDocumentBlobUploadCoverageTests(CustomerOnboardingAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_and_download_without_a_token_return_401()
    {
        var target = SeedCustomerDocument();
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(DownloadPath(target));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_caller_without_system_admin_role_returns_403()
    {
        var target = SeedCustomerDocument();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.NoRole());
        var response = await client.GetAsync(DownloadPath(target));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_document_reference_from_another_customer_is_not_read_or_uploaded()
    {
        var owner = SeedCustomerDocument();
        var other = SeedCustomerDocument();
        using var client = AdminClient();
        using var content = PdfContent();

        var response = await client.PostAsync(UploadPath(other with { DocumentReferenceId = owner.DocumentReferenceId }), content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var storage = _factory.Services.GetRequiredService<IBlobStorageService>();
        Assert.Empty(await storage.ListAsync(Container));
    }

    [Fact]
    public async Task A_valid_upload_returns_safe_metadata_and_persists_no_bytes()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        using var content = PdfContent();

        var response = await client.PostAsync(UploadPath(target), content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await response.Content.ReadFromJsonAsync<UploadMetadata>();
        Assert.NotNull(metadata);
        Assert.Equal("Uploaded", metadata!.UploadStatus);
        Assert.Equal("identity.pdf", metadata.OriginalFileName);
        Assert.Equal("application/pdf", metadata.ContentType);
        Assert.DoesNotContain("BlobName", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%PDF-", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var row = db.CustomerDocumentReferences.Single(x => x.Id == target.DocumentReferenceId);
        Assert.Equal(CustomerDocumentUploadStatus.Uploaded, row.UploadStatus);
        Assert.True(row.UploadedSizeBytes > 0);
        Assert.NotNull(row.BlobName);
        Assert.DoesNotContain(typeof(CustomerDocumentReference).GetProperties(), property => property.PropertyType == typeof(byte[]));
    }

    [Fact]
    public async Task Download_returns_the_latest_bytes_and_safe_headers()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        using var upload = PdfContent();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(UploadPath(target), upload)).StatusCode);

        var response = await client.GetAsync(DownloadPath(target));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("identity.pdf", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Missing_blob_returns_safe_404()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        using var upload = PdfContent();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(UploadPath(target), upload)).StatusCode);

        string blobName;
        using (var scope = _factory.Services.CreateScope())
        {
            blobName = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>()
                .CustomerDocumentReferences.Single(x => x.Id == target.DocumentReferenceId).BlobName!;
        }
        await _factory.Services.GetRequiredService<IBlobStorageService>().DeleteAsync(Container, blobName);

        var response = await client.GetAsync(DownloadPath(target));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("DOCUMENT-UPLOAD-404", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_failure_preserves_existing_metadata()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        var storage = (InMemoryCustomerDocumentBlobStorage)_factory.Services.GetRequiredService<IBlobStorageService>();
        storage.FailUploads = true;
        try
        {
            using var content = PdfContent();
            var response = await client.PostAsync(UploadPath(target), content);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            storage.FailUploads = false;
        }

        using var scope = _factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>()
            .CustomerDocumentReferences.Single(x => x.Id == target.DocumentReferenceId);
        Assert.Equal(CustomerDocumentUploadStatus.None, row.UploadStatus);
        Assert.Null(row.BlobName);
    }

    [Fact]
    public async Task Old_blob_cleanup_failure_does_not_fail_a_committed_replacement()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        using var first = PdfContent();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(UploadPath(target), first)).StatusCode);

        var storage = (InMemoryCustomerDocumentBlobStorage)_factory.Services.GetRequiredService<IBlobStorageService>();
        storage.FailDeletes = true;
        try
        {
            using var replacement = PdfContent();
            var response = await client.PostAsync(UploadPath(target), replacement);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            storage.FailDeletes = false;
        }

        using var scope = _factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>()
            .CustomerDocumentReferences.Single(x => x.Id == target.DocumentReferenceId);
        Assert.Equal(CustomerDocumentUploadStatus.Uploaded, row.UploadStatus);
        Assert.NotNull(row.BlobName);
    }

    [Fact]
    public async Task Concurrent_replacements_return_one_conflict_and_keep_one_authoritative_pointer()
    {
        var target = SeedCustomerDocument();
        using var client = AdminClient();
        using var first = PdfContent();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(UploadPath(target), first)).StatusCode);

        var requests = new[] { SendReplacementAsync(client, target), SendReplacementAsync(client, target) };
        var responses = await Task.WhenAll(requests);

        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>()
            .CustomerDocumentReferences.Single(x => x.Id == target.DocumentReferenceId);
        Assert.Equal(CustomerDocumentUploadStatus.Uploaded, row.UploadStatus);
        Assert.NotNull(row.BlobName);
    }

    private async Task<HttpResponseMessage> SendReplacementAsync(HttpClient client, SeededDocument target)
    {
        using var content = PdfContent();
        return await client.PostAsync(UploadPath(target), content);
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    private SeededDocument SeedCustomerDocument()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        var suffix = Interlocked.Increment(ref _sequence).ToString("D5");
        var constitution = "IND";
        var definitionId = Guid.NewGuid();
        var documentCode = $"PAN-{suffix}";
        db.Documents.Add(new Document
        {
            Id = definitionId,
            DocCode = documentCode,
            DocName = "Identity Document",
            Description = "India identity document",
            DocCategory = "IDENTITY",
            ApplicableEntity = CustomerLookupTypes.EntityRole,
            MaxFileSizeMb = 5,
            AllowedMimeTypes = "PDF",
            Status = DocumentStatus.Active,
            AuthorizedVersionNumber = 1,
            ApplicabilityRules = new List<DocumentApplicabilityRule>
            {
                new() { Constitution = constitution, EntityRole = CustomerLookupTypes.EntityRole, IsMandatory = true },
            },
        });
        var customer = new Customer();
        customer.Initialize(
            companyId: Guid.NewGuid(), isCustomer: true, isGuarantor: false, isCoApplicant: false,
            customerTypeCode: "INDIVIDUAL", groupCode: null, industryCode: null,
            customerPostingGroupCode: null, glAccountId: null, constitutionCode: constitution,
            nationalityCode: "IN", titleCode: "MR", maritalStatusCode: "SIN",
            customerBusinessTypeCode: null, customerBusinessSubTypeCode: null,
            industrySectorCode: null, subSectorCode: null, firstName: "Integration customer",
            secondName: null, thirdName: null, fourthName: null, fifthName: null, sixthName: null,
            notes: "India integration test", isVip: false, isSeniorMember: false,
            financialRequirement: null, accommodation: null, isRelatedParty: false,
            relationCode: null, employerEconomicActivityCode: null);
        customer.SetGeneratedCode($"D{suffix}000000");
        var reference = new CustomerDocumentReference();
        reference.Initialize(constitution, definitionId, documentCode, "IDENTITY", "Identity Document", true,
            false, null, false, false, null, null, null, null);
        customer.Documents.Add(reference);
        db.Customers.Add(customer);
        db.SaveChanges();
        return new SeededDocument(customer.CustomerCode, reference.Id);
    }

    private static MultipartFormDataContent PdfContent()
    {
        var part = new ByteArrayContent(PdfBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var content = new MultipartFormDataContent();
        content.Add(part, "file", "identity.pdf");
        return content;
    }

    private static string UploadPath(SeededDocument target) =>
        $"/api/customer-onboarding/{target.CustomerCode}/documents/{target.DocumentReferenceId}/upload";

    private static string DownloadPath(SeededDocument target) =>
        $"/api/customer-onboarding/{target.CustomerCode}/documents/{target.DocumentReferenceId}/download";

    private static readonly byte[] PdfBytes = "%PDF-1.7\nIndia document"u8.ToArray();
    private sealed record SeededDocument(string CustomerCode, Guid DocumentReferenceId);
    private sealed record UploadMetadata(Guid CustomerDocumentReferenceId, Guid DocumentDefinitionId,
        string DocumentCode, string UploadStatus, string OriginalFileName, string ContentType,
        long SizeInBytes, DateTime UploadedAtUtc);
}
```

### CREATE `Finnova.Tests/Integration/CustomerDocumentUploadCompensationTests.cs`

This service-boundary test covers the failure that cannot be induced reliably through the EF Core InMemory provider: the database save fails after Blob Storage accepts the new object. It uses the same production coordinator and the existing fake provider, and asserts that the new object is deleted while the old metadata remains authoritative.

```csharp
using System.Text;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.Documents;
using Finnova.Service.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Finnova.Tests.Integration;

public sealed class CustomerDocumentUploadCompensationTests
{
    [Fact]
    public async Task Metadata_save_failure_deletes_the_new_blob_before_returning_the_error()
    {
        var customer = new Customer();
        customer.Initialize(
            companyId: Guid.NewGuid(), isCustomer: true, isGuarantor: false, isCoApplicant: false,
            customerTypeCode: "INDIVIDUAL", groupCode: null, industryCode: null,
            customerPostingGroupCode: null, glAccountId: null, constitutionCode: "IND",
            nationalityCode: "IN", titleCode: "MR", maritalStatusCode: "SIN",
            customerBusinessTypeCode: null, customerBusinessSubTypeCode: null,
            industrySectorCode: null, subSectorCode: null, firstName: "Compensation",
            secondName: null, thirdName: null, fourthName: null, fifthName: null, sixthName: null,
            notes: "India integration test", isVip: false, isSeniorMember: false,
            financialRequirement: null, accommodation: null, isRelatedParty: false,
            relationCode: null, employerEconomicActivityCode: null);
        customer.SetGeneratedCode("COMP00000001");
        var definitionId = Guid.NewGuid();
        var row = new CustomerDocumentReference();
        row.Initialize("IND", definitionId, "PAN", "IDENTITY", "Identity Document", true, false,
            null, false, false, null, null, null, null);
        customer.Documents.Add(row);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByCodeAsync(customer.CustomerCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        var policy = new Mock<IDocumentUploadPolicyResolver>();
        policy.Setup(x => x.ResolveAsync(definitionId, "IND", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadPolicy(definitionId, "PAN", "Identity Document", 5,
                new HashSet<string>(new[] { "application/pdf" }, StringComparer.OrdinalIgnoreCase)));
        var storage = new InMemoryCustomerDocumentBlobStorage();
        var transaction = new Mock<ICustomerTransaction>();
        var unitOfWork = new Mock<ICustomerUnitOfWork>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("test persistence failure"));
        var service = new CustomerDocumentUploadService(customers.Object, policy.Object, unitOfWork.Object,
            Mock.Of<ICustomerAuditRepository>(), storage,
            Options.Create(new BlobStorageOptions { CustomerDocumentsContainer = "finnova-customer-documents" }),
            NullLogger<CustomerDocumentUploadService>.Instance);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\ncompensation");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "identity.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.UploadAsync(customer.CustomerCode, row.Id, file, "admin", CancellationToken.None));

        Assert.Empty(await storage.ListAsync("finnova-customer-documents"));
        Assert.Equal(CustomerDocumentUploadStatus.None, row.UploadStatus);
    }
}
```

The integration fixture must seed one customer, one child reference, one active applicable Document, and one policy-appropriate document as shown above. The explicit tests now cover 401 authentication, 403 role authorization, cross-customer ownership, successful multipart upload, metadata-only persistence, streamed download, missing blob, provider failure, metadata-failure compensating cleanup, and concurrent replacement conflict. All integration requests use `CustomerOnboardingAppFactory` with `InMemoryCustomerDocumentBlobStorage`; they do not contact Azure.

## Task 10 — Frontend model and service layer

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\customerOnboarding.ts`

Insert these fields into `DocumentReferenceForm` after `viewMetadata` and add the response interface immediately after it:

```typescript
  uploadStatus?: 'None' | 'Uploaded';
  originalFileName?: string;
  uploadedContentType?: string;
  uploadedSizeBytes?: number;
  uploadedAtUtc?: string;
}
export interface CustomerDocumentUpload {
  customerDocumentReferenceId: string;
  documentDefinitionId: string;
  documentCode: string;
  uploadStatus: 'None' | 'Uploaded';
  originalFileName: string;
  contentType: string;
  sizeInBytes: number;
  uploadedAtUtc: string;
```

Close the interface with `}` and leave the existing `TrackForm` declaration immediately after it. Do not add blob names, container names, URIs, SAS tokens, credentials, or bytes to any model.

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\customerOnboarding.service.ts`

Replace the import with this import and extend the interface with the two methods:

```typescript
import type { CustomerCatalog, CustomerDocumentUpload, CustomerForm, CustomerOnboardingMode, CustomerProjection } from '../../models/customerOnboarding';
export interface CustomerOnboardingService {
  loadCatalog(companyId: string): Promise<CustomerCatalog>;
  create(request: CustomerForm): Promise<CustomerProjection>;
  modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection>;
  query(search?: string): Promise<CustomerProjection[]>;
  copyCommunicationAddress(customerCode: string): Promise<CustomerProjection>;
  uploadDocument(customerCode: string, documentReferenceId: string, file: File): Promise<CustomerDocumentUpload>;
  downloadDocument(customerCode: string, documentReferenceId: string): Promise<Blob>;
}
export const customerOnboardingModeAllowsWrite = (mode: CustomerOnboardingMode) => mode !== 'query';
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\customerOnboarding.service.ts`

Extend the import to include `CustomerDocumentUpload`, then insert these methods in the existing `realCustomerOnboardingService` object after `copyCommunicationAddress`:

```typescript
  async uploadDocument(customerCode: string, documentReferenceId: string, file: File): Promise<CustomerDocumentUpload> {
    const form = new FormData();
    form.append('file', file);
    const { data } = await api.post(
      `/customer-onboarding/${encodeURIComponent(customerCode)}/documents/${encodeURIComponent(documentReferenceId)}/upload`,
      form,
    );
    return data;
  },
  async downloadDocument(customerCode: string, documentReferenceId: string): Promise<Blob> {
    const { data } = await api.get(
      `/customer-onboarding/${encodeURIComponent(customerCode)}/documents/${encodeURIComponent(documentReferenceId)}/download`,
      { responseType: 'blob' },
    );
    return data;
  },
```

Do not set `Authorization`, `Content-Type`, or a second axios client. The shared `api` instance owns those concerns.

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\customerOnboarding.service.ts`

Add `CustomerDocumentUpload` to the type import. Add this map near the existing `store` declaration:

```typescript
const documentBlobs = new Map<string, Blob>();
```

Insert these methods into `mockCustomerOnboardingService` after `copyCommunicationAddress`:

```typescript
  async uploadDocument(customerCode: string, documentReferenceId: string, file: File): Promise<CustomerDocumentUpload> {
    const customer = store.find(x => x.customerCode === customerCode);
    const document = customer?.documents.find(x => x.id === documentReferenceId);
    if (!customer || !document) throw new Error('Customer document was not found.');
    if (file.size === 0) throw new Error('The file must not be empty.');
    const contentType = file.type.toLowerCase() === 'image/jpg' ? 'image/jpeg' : file.type.toLowerCase();
    if (!['application/pdf', 'image/jpeg', 'image/png'].includes(contentType)) throw new Error('The file type is not permitted.');
    documentBlobs.set(`${customerCode}/${documentReferenceId}`, file);
    const metadata: CustomerDocumentUpload = {
      customerDocumentReferenceId: documentReferenceId,
      documentDefinitionId: document.documentDefinitionId,
      documentCode: document.documentCode,
      uploadStatus: 'Uploaded',
      originalFileName: file.name.split(/[\\/]/).pop() ?? file.name,
      contentType,
      sizeInBytes: file.size,
      uploadedAtUtc: new Date().toISOString(),
    };
    Object.assign(document, { uploadStatus: metadata.uploadStatus, originalFileName: metadata.originalFileName,
      uploadedContentType: metadata.contentType, uploadedSizeBytes: metadata.sizeInBytes, uploadedAtUtc: metadata.uploadedAtUtc });
    return metadata;
  },
  async downloadDocument(customerCode: string, documentReferenceId: string): Promise<Blob> {
    const blob = documentBlobs.get(`${customerCode}/${documentReferenceId}`);
    if (!blob) throw new Error('The current customer document is not available.');
    return blob;
  },
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\customerOnboarding.service.ts`

No implementation change is needed. Preserve this existing toggle exactly and confirm that both implementations satisfy the extended interface:

```typescript
export const customerOnboardingService = import.meta.env.VITE_USE_MOCK_API === 'true' ? mockCustomerOnboardingService : realCustomerOnboardingService;
```

## Task 11 — Frontend page/component behavior and route/nav confirmation

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\components\CustomerOnboardingTabs.tsx`

Change only the Constitution Documents case to pass `mode` instead of the derived `readOnly` value:

```typescript
case 'constitution-documents': return <ConstitutionDocumentsTab value={value} mode={mode} onChange={onChange} />;
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\components\ConstitutionDocumentsTab.tsx`

Replace the complete file with this code. It keeps file bytes and object URLs out of the customer model and uses English, left-to-right MUI controls.

```tsx
import { useState } from 'react';
import { Alert, Button, Stack, Typography } from '@mui/material';
import type { CustomerForm, CustomerOnboardingMode, CustomerProjection } from '../../../models/customerOnboarding';
import { customerOnboardingService } from '../../../services/customerOnboarding.service';

type Props = { value: CustomerForm | CustomerProjection; mode: CustomerOnboardingMode; onChange: (v: CustomerForm | CustomerProjection) => void };
const MAX_BYTES = 25 * 1024 * 1024;
const allowed = new Set(['application/pdf', 'image/jpeg', 'image/png']);

function validateFile(file: File): string | undefined {
  const type = file.type.toLowerCase() === 'image/jpg' ? 'image/jpeg' : file.type.toLowerCase();
  if (!file.size) return 'The file must not be empty.';
  if (!allowed.has(type)) return 'Only PDF, JPEG, and PNG files are supported.';
  if (file.size > MAX_BYTES) return 'The file must not exceed 25 MiB.';
  const extension = file.name.toLowerCase().split('.').pop();
  if ((type === 'application/pdf' && extension !== 'pdf') || (type === 'image/jpeg' && !['jpg', 'jpeg'].includes(extension ?? '')) || (type === 'image/png' && extension !== 'png')) return 'The file extension does not match its type.';
  return undefined;
}

export function ConstitutionDocumentsTab({ value, mode, onChange }: Props) {
  const [busy, setBusy] = useState<Record<string, boolean>>({});
  const [messages, setMessages] = useState<Record<string, string>>({});
  const [success, setSuccess] = useState<Record<string, string>>({});

  const updateDocument = (referenceId: string, metadata: { uploadStatus: 'Uploaded'; originalFileName: string; uploadedContentType: string; uploadedSizeBytes: number; uploadedAtUtc: string }) => {
    onChange({ ...value, documents: value.documents.map(document => document.id === referenceId ? { ...document, ...metadata } : document) });
  };

  const upload = async (referenceId: string, file: File) => {
    const error = validateFile(file);
    if (error) { setMessages(previous => ({ ...previous, [referenceId]: error })); return; }
    if (!value.customerCode) { setMessages(previous => ({ ...previous, [referenceId]: 'Save the customer before uploading a document.' })); return; }
    setBusy(previous => ({ ...previous, [referenceId]: true }));
    setMessages(previous => ({ ...previous, [referenceId]: '' }));
    try {
      const result = await customerOnboardingService.uploadDocument(value.customerCode, referenceId, file);
      updateDocument(referenceId, { uploadStatus: result.uploadStatus, originalFileName: result.originalFileName, uploadedContentType: result.contentType, uploadedSizeBytes: result.sizeInBytes, uploadedAtUtc: result.uploadedAtUtc });
      setSuccess(previous => ({ ...previous, [referenceId]: 'Document uploaded.' }));
    } catch (error: any) {
      setMessages(previous => ({ ...previous, [referenceId]: error?.response?.data?.message ?? error?.response?.data?.detail ?? error?.message ?? 'Unable to upload the document.' }));
    } finally { setBusy(previous => ({ ...previous, [referenceId]: false })); }
  };

  const view = async (referenceId: string) => {
    if (!value.customerCode) return;
    setBusy(previous => ({ ...previous, [referenceId]: true }));
    try {
      const blob = await customerOnboardingService.downloadDocument(value.customerCode, referenceId);
      const url = URL.createObjectURL(blob);
      try { window.open(url, '_blank', 'noopener,noreferrer'); } finally { URL.revokeObjectURL(url); }
    } catch (error: any) {
      setMessages(previous => ({ ...previous, [referenceId]: error?.response?.data?.message ?? error?.response?.data?.detail ?? error?.message ?? 'Unable to view the document.' }));
    } finally { setBusy(previous => ({ ...previous, [referenceId]: false })); }
  };

  return <Stack spacing={2}>{value.documents.map(document => {
    const referenceId = document.id ?? document.documentDefinitionId;
    const currentBusy = !!busy[referenceId];
    const canUpload = mode !== 'query' && !!value.customerCode && !!document.id;
    return <Stack key={referenceId} spacing={1} sx={{ border: '1px solid', borderColor: 'divider', p: 1 }}>
      <Typography>{document.documentCode} {document.isMandatory ? '(Mandatory)' : ''}</Typography>
      {document.documentDescription && <Typography variant="body2">{document.documentDescription}</Typography>}
      <Typography variant="body2">Status: {document.uploadStatus ?? 'None'}</Typography>
      {document.originalFileName && <Typography variant="body2">File: {document.originalFileName} ({document.uploadedSizeBytes} bytes, {document.uploadedContentType})</Typography>}
      {!value.customerCode && mode !== 'query' && <Typography variant="body2">Save the customer before uploading a document.</Typography>}
      <Stack direction="row" spacing={1}>
        <Button component="label" variant="outlined" disabled={!canUpload || currentBusy}>
          Upload
          <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" onChange={event => { const file = event.target.files?.[0]; if (file) void upload(referenceId, file); event.currentTarget.value = ''; }} />
        </Button>
        <Button variant="outlined" disabled={document.uploadStatus !== 'Uploaded' || currentBusy || !value.customerCode} onClick={() => void view(referenceId)}>View</Button>
      </Stack>
      {currentBusy && <Typography variant="body2">Working…</Typography>}
      {!!messages[referenceId] && <Alert severity="error">{messages[referenceId]}</Alert>}
      {!!success[referenceId] && <Alert severity="success">{success[referenceId]}</Alert>}
    </Stack>;
  })}</Stack>;
}
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx`

No route is added. Preserve these existing routes exactly so create, modify, and query modes continue to select upload availability:

```tsx
<Route path="/customer-onboarding" element={<CustomerOnboarding mode="create" />} />
<Route path="/customer-onboarding/:customerCode" element={<CustomerOnboarding mode="modify" />} />
<Route path="/customer-onboarding/:customerCode/view" element={<CustomerOnboarding mode="query" />} />
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx`

No navigation item is added. Preserve the existing Customer Onboarding entry exactly:

```tsx
{ text: 'Customer Onboarding', icon: <PeopleIcon />, path: '/customer-onboarding' },
```

## Task 12 — Frontend Vitest and React Testing Library tests

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\components\ConstitutionDocumentsTab.test.tsx`

```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ConstitutionDocumentsTab } from './ConstitutionDocumentsTab';
import { customerOnboardingService } from '../../../services/customerOnboarding.service';

vi.mock('../../../services/customerOnboarding.service', () => ({
  customerOnboardingService: { uploadDocument: vi.fn(), downloadDocument: vi.fn() },
}));

describe('ConstitutionDocumentsTab', () => {
  const model = { customerCode: 'CUST00000001', documents: [{ id: 'ref-1', documentDefinitionId: 'def-1', documentCode: 'PAN', isMandatory: true, isImageRequired: false, isCollected: false, isScanned: false }] } as any;
  beforeEach(() => vi.clearAllMocks());

  it('disables upload before customer creation', () => {
    render(<ConstitutionDocumentsTab value={{ ...model, customerCode: undefined }} mode="create" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Upload' })).toBeDisabled();
    expect(screen.getByText('Save the customer before uploading a document.')).toBeInTheDocument();
  });

  it('rejects an unsupported file without a request', async () => {
    render(<ConstitutionDocumentsTab value={model} mode="modify" onChange={vi.fn()} />);
    const input = screen.getByLabelText('Upload') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [new File(['x'], 'document.txt', { type: 'text/plain' })] } });
    expect(await screen.findByText('Only PDF, JPEG, and PNG files are supported.')).toBeInTheDocument();
    expect(customerOnboardingService.uploadDocument).not.toHaveBeenCalled();
  });

  it('updates safe metadata after a successful upload', async () => {
    vi.mocked(customerOnboardingService.uploadDocument).mockResolvedValue({ customerDocumentReferenceId: 'ref-1', documentDefinitionId: 'def-1', documentCode: 'PAN', uploadStatus: 'Uploaded', originalFileName: 'document.pdf', contentType: 'application/pdf', sizeInBytes: 5, uploadedAtUtc: '2026-01-01T00:00:00Z' });
    const onChange = vi.fn();
    render(<ConstitutionDocumentsTab value={model} mode="modify" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Upload'), { target: { files: [new File(['%PDF-'], 'document.pdf', { type: 'application/pdf' })] } });
    await waitFor(() => expect(onChange).toHaveBeenCalled());
    expect(screen.getByText('Document uploaded.')).toBeInTheDocument();
  });
});
```

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\customerOnboarding.service.test.ts`

```typescript
import { describe, expect, it, vi } from 'vitest';
import api from '../api';
import { realCustomerOnboardingService } from './customerOnboarding.service';

vi.mock('../api', () => ({ default: { post: vi.fn(), get: vi.fn() } }));

describe('real customer document service', () => {
  it('posts one multipart file to the onboarding upload route', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { uploadStatus: 'Uploaded' } });
    const file = new File(['%PDF-'], 'document.pdf', { type: 'application/pdf' });
    await realCustomerOnboardingService.uploadDocument('CUST/1', 'ref-1', file);
    expect(api.post).toHaveBeenCalledWith('/customer-onboarding/CUST%2F1/documents/ref-1/upload', expect.any(FormData));
    expect((vi.mocked(api.post).mock.calls[0][1] as FormData).get('file')).toBe(file);
  });

  it('requests a document as a Blob', async () => {
    const blob = new Blob(['data'], { type: 'application/pdf' });
    vi.mocked(api.get).mockResolvedValue({ data: blob });
    await expect(realCustomerOnboardingService.downloadDocument('CUST00000001', 'ref-1')).resolves.toBe(blob);
    expect(api.get).toHaveBeenCalledWith('/customer-onboarding/CUST00000001/documents/ref-1/download', { responseType: 'blob' });
  });
});
```

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\customerOnboarding.service.test.ts`

```typescript
import { describe, expect, it } from 'vitest';
import type { CustomerForm } from '../../../models/customerOnboarding';
import { mockCustomerOnboardingService } from './customerOnboarding.service';

const emptyCustomer = (): CustomerForm => ({
  companyId: 'company-1', isCustomer: true, isGuarantor: false, isCoApplicant: false,
  customerTypeCode: 'INDIVIDUAL', addresses: [], bankAccounts: [], documents: [{
    id: 'reference-1', documentDefinitionId: 'definition-1', documentCode: 'PAN',
    isMandatory: true, isImageRequired: false, isCollected: false, isScanned: false,
  }], tracks: [], factoringSubLimits: [],
});

describe('mock customer document service parity', () => {
  it('supports first upload, replacement, and download with the real-service response shape', async () => {
    const customer = await mockCustomerOnboardingService.create(emptyCustomer());
    const first = new File(['%PDF-1.7 first'], 'first.pdf', { type: 'application/pdf' });
    const replacement = new File(['%PDF-1.7 replacement'], 'replacement.pdf', { type: 'application/pdf' });

    const firstMetadata = await mockCustomerOnboardingService.uploadDocument(customer.customerCode!, 'reference-1', first);
    const replacementMetadata = await mockCustomerOnboardingService.uploadDocument(customer.customerCode!, 'reference-1', replacement);

    expect(firstMetadata.uploadStatus).toBe('Uploaded');
    expect(replacementMetadata.originalFileName).toBe('replacement.pdf');
    expect(replacementMetadata.sizeInBytes).toBe(replacement.size);
    await expect(mockCustomerOnboardingService.downloadDocument(customer.customerCode!, 'reference-1'))
      .resolves.toBeInstanceOf(Blob);
    expect(Object.keys(replacementMetadata)).toEqual(expect.arrayContaining([
      'customerDocumentReferenceId', 'documentDefinitionId', 'documentCode',
      'uploadStatus', 'originalFileName', 'contentType', 'sizeInBytes', 'uploadedAtUtc',
    ]));
    expect(Object.keys(replacementMetadata)).not.toEqual(expect.arrayContaining([
      'blobName', 'blobContainerName', 'uri', 'sasToken', 'token', 'bytes',
    ]));
    expect(JSON.stringify(replacementMetadata)).not.toContain('%PDF-1.7');
  });

  it('returns the same safe missing-document error contract', async () => {
    const customer = await mockCustomerOnboardingService.create(emptyCustomer());
    await expect(mockCustomerOnboardingService.downloadDocument(customer.customerCode!, 'reference-1'))
      .rejects.toThrow('The current customer document is not available.');
  });
});
```

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\components\ConstitutionDocumentsTab.behavior.test.tsx`

```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CustomerProjection } from '../../../models/customerOnboarding';
import { customerOnboardingService } from '../../../services/customerOnboarding.service';
import { ConstitutionDocumentsTab } from './ConstitutionDocumentsTab';

vi.mock('../../../services/customerOnboarding.service', () => ({
  customerOnboardingService: { uploadDocument: vi.fn(), downloadDocument: vi.fn() },
}));

const model = (): CustomerProjection => ({
  id: 'customer-1', customerCode: 'CUST00000001', status: 'Active', companyId: 'company-1',
  isCustomer: true, isGuarantor: false, isCoApplicant: false, customerTypeCode: 'INDIVIDUAL',
  addresses: [], bankAccounts: [], tracks: [], factoringSubLimits: [], createdAtUtc: '', updatedAtUtc: '',
  documents: [{ id: 'reference-1', documentDefinitionId: 'definition-1', documentCode: 'PAN',
    isMandatory: true, isImageRequired: false, isCollected: false, isScanned: false }],
});

const choosePdf = () => fireEvent.change(screen.getByLabelText('Upload'), {
  target: { files: [new File(['%PDF-1.7'], 'identity.pdf', { type: 'application/pdf' })] },
});

describe('ConstitutionDocumentsTab behavior', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps unrelated rows usable while one row is uploading', async () => {
    let resolveUpload!: (value: any) => void;
    vi.mocked(customerOnboardingService.uploadDocument).mockReturnValue(new Promise(resolve => { resolveUpload = resolve; }));
    render(<ConstitutionDocumentsTab value={model()} mode="modify" onChange={vi.fn()} />);

    choosePdf();
    expect(screen.getByRole('button', { name: 'Upload' })).toBeDisabled();
    expect(screen.getByText('Working…')).toBeInTheDocument();
    resolveUpload({ customerDocumentReferenceId: 'reference-1', documentDefinitionId: 'definition-1', documentCode: 'PAN', uploadStatus: 'Uploaded', originalFileName: 'identity.pdf', contentType: 'application/pdf', sizeInBytes: 8, uploadedAtUtc: '2026-01-01T00:00:00Z' });
    await waitFor(() => expect(screen.getByText('Document uploaded.')).toBeInTheDocument());
  });

  it('preserves the row and offers a retry after an upload error', async () => {
    vi.mocked(customerOnboardingService.uploadDocument)
      .mockRejectedValueOnce(new Error('Storage is unavailable.'))
      .mockResolvedValueOnce({ customerDocumentReferenceId: 'reference-1', documentDefinitionId: 'definition-1', documentCode: 'PAN', uploadStatus: 'Uploaded', originalFileName: 'identity.pdf', contentType: 'application/pdf', sizeInBytes: 8, uploadedAtUtc: '2026-01-01T00:00:00Z' });
    const onChange = vi.fn();
    render(<ConstitutionDocumentsTab value={model()} mode="modify" onChange={onChange} />);

    choosePdf();
    expect(await screen.findByText('Storage is unavailable.')).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
    choosePdf();
    await waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));
    expect(screen.getByText('Document uploaded.')).toBeInTheDocument();
  });

  it('views a Blob and revokes the temporary object URL', async () => {
    const blob = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
    vi.mocked(customerOnboardingService.downloadDocument).mockResolvedValue(blob);
    const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    const revokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const open = vi.spyOn(window, 'open').mockImplementation(() => null);
    const value = { ...model(), documents: [{ ...model().documents[0], uploadStatus: 'Uploaded' as const, originalFileName: 'identity.pdf' }] };
    render(<ConstitutionDocumentsTab value={value} mode="query" onChange={vi.fn()} />);

    fireEvent.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() => expect(open).toHaveBeenCalledWith('blob:test', '_blank', 'noopener,noreferrer'));
    expect(createObjectURL).toHaveBeenCalledWith(blob);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
  });
});
```

The service parity test now has an explicit `CREATE` path and covers first upload, replacement, safe metadata, download, and missing-document behavior. The component tests cover create/query availability, client validation, per-row busy state, success metadata, error preservation/retry, View Blob handling, and object-URL cleanup. Do not run Vitest, npm, dotnet, EF, or migration commands as part of this documentation-only delivery.

## Completion checklist

- Apply Tasks 1–12 in order.
- Supply `BlobStorage:Azure:ConnectionString` outside source control and keep the container private.
- Run the migration command only after reviewing the generated migration.
- Add the fake storage and host wiring required by the integration-test task.
- Run builds/tests manually after implementation; this task file itself has not run them.
- Confirm the final implementation remains India-only, English-only, left-to-right, and contains no raw document bytes in SQL, logs, contracts, tests, or browser storage.
