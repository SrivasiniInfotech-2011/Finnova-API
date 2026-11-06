using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Repositories;
using Finnova.Service.IdentityVerification.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 10.6 — verifies the protected-document transaction boundary with an in-memory
/// protected-store fake. Provider and persistence failures must remove the uploaded blob;
/// successful persistence must leave exactly one matching reference and blob; manual decisions
/// must append a new record without changing the automated record.
/// </summary>
public sealed class ProtectedDocumentTransactionTests
{
    [Fact]
    public async Task ProviderFailureLeavesNoOrphanedBlob()
    {
        var fake = new TrackingStore();
        var bytes = "%PDF-provider"u8.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(
                new MemoryStream(bytes),
                "application/pdf",
                bytes.Length,
                _ => throw new InvalidOperationException("provider failed"),
                CancellationToken.None));

        Assert.Empty(fake.Keys);
    }

    [Fact]
    public async Task PersistenceFailureLeavesNoOrphanedBlob()
    {
        var fake = new TrackingStore();
        var bytes = "%PDF-persistence"u8.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(
                new MemoryStream(bytes),
                "application/pdf",
                bytes.Length,
                _ => throw new InvalidOperationException("database failed"),
                CancellationToken.None));

        Assert.Empty(fake.Keys);
    }

    [Fact]
    public async Task SuccessfulPersistenceLeavesExactlyOneReferenceAndMatchingBlob()
    {
        var databaseName = $"protected-document-success-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FinnovaDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using var db = new FinnovaDbContext(options);
        var fake = new TrackingStore();
        var bytes = "%PDF-success"u8.ToArray();

        var result = await new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(
            new MemoryStream(bytes),
            "application/pdf",
            bytes.Length,
            async reference =>
            {
                db.ProtectedDocumentReferences.Add(reference);
                await db.SaveChangesAsync();
                return reference.Id;
            },
            CancellationToken.None);

        var reference = Assert.Single(await db.ProtectedDocumentReferences.AsNoTracking().ToListAsync());
        Assert.Equal(reference.Id, result);
        var key = Assert.Single(fake.Keys);
        Assert.Equal(reference.StorageKey, key);
        Assert.Equal(bytes, fake.GetBytes(key));
        Assert.Equal("application/pdf", reference.ContentType);
        Assert.Equal(bytes.Length, reference.SizeBytes);
    }

    [Fact]
    public async Task ManualRecordsAreAppendOnlyAndPreserveAutomatedRecord()
    {
        var options = new DbContextOptionsBuilder<FinnovaDbContext>()
            .UseInMemoryDatabase($"protected-document-records-{Guid.NewGuid():N}")
            .Options;
        await using var db = new FinnovaDbContext(options);
        var repository = new IdentityVerificationRepository(db);
        var sourceId = Guid.NewGuid();

        var automated = new ValidationRecord
        {
            Id = sourceId,
            OperationType = VerificationOperationType.PanNameVerification,
            ComparisonOutcome = ComparisonOutcome.NonMatch,
            Method = VerificationMethod.Auto,
            ActorReference = "system-admin",
            CorrelationId = "correlation-automated",
            SafeReasonCode = "NameMismatch",
            SafeMessage = "The submitted name did not match.",
            SourceSummary = new SourceSummary { SourceType = VerificationSourceType.DocumentFile }
        };
        await repository.AppendRecordAsync(automated);

        var manual = new ValidationRecord
        {
            OperationType = VerificationOperationType.PanNameVerification,
            ComparisonOutcome = ComparisonOutcome.Match,
            Method = VerificationMethod.ManualOverride,
            SourceRecordId = sourceId,
            ActorReference = "system-admin",
            CorrelationId = "correlation-manual",
            SafeReasonCode = "ReviewedByAdministrator",
            SafeMessage = "The administrator approved the document.",
            SourceSummary = new SourceSummary { SourceType = VerificationSourceType.DocumentFile }
        };
        await repository.AppendRecordAsync(manual);

        var records = await db.ValidationRecords
            .AsNoTracking()
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, records.Count);
        var storedAutomated = Assert.Single(records, x => x.Id == sourceId);
        var storedManual = Assert.Single(records, x => x.Method == VerificationMethod.ManualOverride);
        Assert.Equal(ComparisonOutcome.NonMatch, storedAutomated.ComparisonOutcome);
        Assert.Equal(VerificationMethod.Auto, storedAutomated.Method);
        Assert.Equal(sourceId, storedManual.SourceRecordId);
        Assert.Equal(ComparisonOutcome.Match, storedManual.ComparisonOutcome);
    }

    [Fact]
    public async Task FailedPersistenceLeavesNoOrphanedProtectedDocumentReferenceOrBlob()
    {
        var databaseName = $"protected-document-failure-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FinnovaDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using var failingDb = new FailingSaveChangesDbContext(options);
        var fake = new TrackingStore();
        var bytes = "%PDF-no-orphan"u8.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(
                new MemoryStream(bytes),
                "application/pdf",
                bytes.Length,
                async reference =>
                {
                    failingDb.ProtectedDocumentReferences.Add(reference);
                    await failingDb.SaveChangesAsync();
                    return reference.Id;
                },
                CancellationToken.None));

        await using var verificationDb = new FinnovaDbContext(options);
        Assert.Equal(0, await verificationDb.ProtectedDocumentReferences.CountAsync());
        Assert.Empty(fake.Keys);
    }

    private sealed class FailingSaveChangesDbContext(DbContextOptions<FinnovaDbContext> options)
        : FinnovaDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SaveChangesAsync failed");
    }

    private sealed class TrackingStore : IProtectedDocumentStore
    {
        private readonly Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => blobs.Keys;

        public async Task<ProtectedDocumentReference> PutAsync(
            Stream content,
            string contentType,
            long sizeBytes,
            CancellationToken ct = default)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);

            var reference = new ProtectedDocumentReference
            {
                StorageKey = $"identity-verification/{Guid.NewGuid():N}.bin",
                ContentType = contentType,
                SizeBytes = sizeBytes,
                ContentHash = "test"
            };
            blobs.Add(reference.StorageKey, copy.ToArray());
            return reference;
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(
                blobs.TryGetValue(storageKey, out var bytes)
                    ? new MemoryStream(bytes, writable: false)
                    : null);

        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            blobs.Remove(storageKey);
            return Task.CompletedTask;
        }

        public byte[] GetBytes(string storageKey) => blobs[storageKey];
    }
}
