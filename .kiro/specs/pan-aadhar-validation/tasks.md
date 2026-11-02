# Implementation Plan: PAN-Aadhaar Validation and Comparison

## Overview

This is the complete copy-paste implementation plan for FINNOVA-15. It preserves the existing PAN identifier validation and Aadhaar identifier validation as separate operations, and adds `verify-pan-name` and `verify-aadhar-address` as independent comparison APIs. It covers `Finnova.SystemAdminService` in the API repository and the available frontend repository at `d:\Projects\Finnova\Finnova-UI`.

Apply tasks in order. Every code block is intended to be copied into the named file. `CREATE` blocks are complete files. `MODIFY` blocks identify an exact anchor and show the complete additive or replacement snippet. Locate each anchor first and add every snippet only once. Do not duplicate registrations, imports, DbSets, routes, or navigation entries.

### Confirm-before-implementation constraints

- The exact public comparison routes are `POST /api/identity-verification/verify-pan-name` and `POST /api/identity-verification/verify-aadhar-address`.
- Provider HTTP verbs, multipart field names, JSON property names, authentication headers, response paths, and DTO fields were not supplied. They remain configurable. Before implementing the provider adapter, confirm the local Swagger/OpenAPI contract and fill only task 7.2. Do not invent UIDAI, Income Tax Department, or other provider claims.
- The UI repository was found at `d:\Projects\Finnova\Finnova-UI`, rather than the product-context E: path. Reconcile path-only differences before pasting. Inspected patterns include `src/services/api.ts`, `src/services/interfaces`, `src/services/real`, `src/services/mock`, `src/services/lookup.service.ts`, `src/models/index.ts`, `src/components/Layout.tsx`, and `src/App.tsx`.
- Preserve `.kiro/specs/pan-aadhar-validation/.config.kiro`. Do not edit source files or run builds/tests while authoring this specification.
- India-only, English-only, no Arabic/non-English sample data, no RTL, no Redux, no RxJS, and no provider-specific fields in public contracts are mandatory.

## Tasks

- [x] 1. Add backend domain enums and immutable entities
  - [x] 1.1 CREATE `Finnova.Models/Domain/Enums/IdentityVerificationEnums.cs`

    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum VerificationOperationType { PanIdentifierValidation, AadhaarIdentifierValidation, PanNameVerification, AadhaarAddressVerification }
    public enum ValidationStatus { Success, Failure, Unavailable }
    public enum ComparisonOutcome { Match, NonMatch, NoUsableMatch }
    public enum VerificationMethod { Auto, ManualOverride }
    public enum VerificationWorkflowType { Single, PanAndName, AadhaarAndAddress, Full }
    public enum WorkflowInputMode { Partial, Atomic }
    public enum VerificationSourceType { None, DocumentFile, ExtractedValues, DocumentAndExtractedValues }
    ```

    _Requirements: 1.2, 2.2, 3.2-3.4, 4.2-4.4, 6.1-6.16, 7.5, 8.2-8.9, 9.1, 10.2-10.7, 14.1-14.3._

  - [x] 1.2 CREATE `Finnova.Models/Domain/Entities/VerificationSession.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Domain.Entities;
    public sealed class VerificationSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CorrelationId { get; set; } = string.Empty;
        public VerificationWorkflowType WorkflowType { get; set; }
        public WorkflowInputMode InputMode { get; set; }
        public string ActorReference { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string RequestedOperationsJson { get; set; } = "[]";
        public ICollection<ValidationRecord> Records { get; set; } = new List<ValidationRecord>();
    }
    ```

    _Requirements: 6.1-6.16, 9.1, 9.4, 9.10-9.12._

  - [x] 1.3 CREATE `Finnova.Models/Domain/Entities/SourceSummary.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Domain.Entities;
    public sealed class SourceSummary
    {
        public VerificationSourceType SourceType { get; set; }
        public string? MappingProfile { get; set; }
        public bool DocumentProvided { get; set; }
        public string? DocumentContentType { get; set; }
        public long? DocumentSizeBytes { get; set; }
        public Guid? ProtectedDocumentReferenceId { get; set; }
        public string ExtractedValueKeysJson { get; set; } = "[]";
        public bool ExtractionCompleted { get; set; }
    }
    ```

    _Requirements: 3.5-3.8, 4.5-4.8, 5.1-5.11, 9.2-9.3, 12.5-12.8._

  - [x] 1.4 CREATE `Finnova.Models/Domain/Entities/ProtectedDocumentReference.cs`

    ```csharp
    namespace Finnova.Models.Domain.Entities;
    public sealed class ProtectedDocumentReference
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string StorageKey { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RetentionUntilUtc { get; set; }
        public ICollection<ValidationRecord> ValidationRecords { get; set; } = new List<ValidationRecord>();
    }
    ```

    _Requirements: 7.3-7.5, 7.8-7.10, 7.16-7.18, 9.2-9.3, 12.9-12.12._

  - [x] 1.5 CREATE `Finnova.Models/Domain/Entities/ValidationRecord.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Domain.Entities;
    public sealed class ValidationRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? SessionId { get; set; }
        public VerificationSession? Session { get; set; }
        public Guid? SourceRecordId { get; set; }
        public VerificationOperationType OperationType { get; set; }
        public ValidationStatus? ValidationStatus { get; set; }
        public ComparisonOutcome? ComparisonOutcome { get; set; }
        public VerificationMethod Method { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string ActorReference { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string? SafeReasonCode { get; set; }
        public string? SafeMessage { get; set; }
        public string? MaskedSubjectReference { get; set; }
        public string? ProtectedSearchReference { get; set; }
        public string? DiagnosticReference { get; set; }
        public SourceSummary SourceSummary { get; set; } = new();
        public Guid? ProtectedDocumentReferenceId { get; set; }
        public ProtectedDocumentReference? ProtectedDocumentReference { get; set; }
    }
    ```

    _Requirements: 1.4-1.7, 2.4-2.7, 3.5-3.8, 4.5-4.8, 6.10-6.16, 7.5, 7.11-7.18, 9.1-9.16, 12.5-12.8, 14.1-14.10._

  - [x] 1.6 CREATE `Finnova.Models/Domain/Entities/IdentityVerificationInvariants.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Domain.Entities;
    public static class IdentityVerificationInvariants
    {
        public static void EnsureResultDomain(ValidationRecord record)
        {
            var identifier = record.OperationType is VerificationOperationType.PanIdentifierValidation or VerificationOperationType.AadhaarIdentifierValidation;
            var comparison = record.OperationType is VerificationOperationType.PanNameVerification or VerificationOperationType.AadhaarAddressVerification;
            if (identifier == comparison) throw new InvalidOperationException("Identity verification operation type is invalid.");
            if (identifier && (record.ValidationStatus is null || record.ComparisonOutcome is not null)) throw new InvalidOperationException("Identifier records require validation status only.");
            if (comparison && (record.ComparisonOutcome is null || record.ValidationStatus is not null)) throw new InvalidOperationException("Comparison records require comparison outcome only.");
        }
    }
    ```

    _Requirements: 1.2, 2.2, 3.2-3.4, 4.2-4.4, 9.1, 9.6-9.7, 14.1-14.3._

- [x] 2. Add safe backend exceptions
  - [x] 2.1 CREATE `Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs`

    ```csharp
    namespace Finnova.Models.Domain.Exceptions;
    public abstract class IdentityVerificationException : Exception
    {
        protected IdentityVerificationException(string message) : base(message) { }
        public abstract string ErrorCode { get; }
    }
    public sealed class IdentityVerificationValidationException(IEnumerable<string> fields) : IdentityVerificationException("The identity verification request is invalid.")
    { public override string ErrorCode => "ERR-IVF-400"; public IReadOnlyList<string> Fields { get; } = fields.ToArray(); }
    public sealed class IdentityVerificationConfigurationException() : IdentityVerificationException("Identity verification is not configured.")
    { public override string ErrorCode => "ERR-IVF-503"; }
    public sealed class IdentityVerificationProviderUnavailableException() : IdentityVerificationException("The verification provider is temporarily unavailable.")
    { public override string ErrorCode => "ERR-IVF-503"; }
    public sealed class IdentityVerificationPersistenceException(Exception inner) : IdentityVerificationException("The verification result could not be recorded.")
    { public override string ErrorCode => "ERR-IVF-500"; public Exception Inner { get; } = inner; }
    public sealed class IdentityVerificationDocumentSizeException() : IdentityVerificationException("The document exceeds the 5 MB limit.")
    { public override string ErrorCode => "ERR-IVF-413"; }
    public sealed class IdentityVerificationDocumentFormatException() : IdentityVerificationException("Only JPG, PNG, and PDF documents are supported.")
    { public override string ErrorCode => "ERR-IVF-415"; }
    public sealed class IdentityVerificationRecordNotFoundException() : IdentityVerificationException("The requested verification record was not found.")
    { public override string ErrorCode => "ERR-IVF-404"; }
    public sealed class IdentityVerificationImmutableException() : IdentityVerificationException("Verification history is immutable.")
    { public override string ErrorCode => "ERR-IVF-409"; }
    public sealed class IdentityVerificationAuthorizationException() : IdentityVerificationException("You are not authorized for this identity-verification operation.")
    { public override string ErrorCode => "ERR-IVF-403"; }
    ```

    Never interpolate PAN, Aadhaar, names, addresses, filenames, bytes, tokens, secrets, or raw provider bodies. _Requirements: 5.9-5.10, 7.6-7.10, 8.8-8.12, 9.6-9.7, 9.14-9.16, 10.12, 10.18, 11.1-11.6, 12.5-12.12, 14.4-14.7._

- [x] 3. Add backend request/response contracts
  - [x] 3.1 CREATE `Finnova.Models/Contracts/IdentityVerification/IdentityVerificationContracts.cs`

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Contracts.IdentityVerification;
    public sealed record ValidatePanRequest(string Pan, Guid? SessionId = null);
    public sealed record ValidateAadhaarRequest(string AadhaarNumber, Guid? SessionId = null);
    public sealed record ComparisonSourceContract(string? DocumentContentType, long? DocumentSizeBytes, Guid? ProtectedDocumentReferenceId, IReadOnlyDictionary<string,string>? ExtractedValues, string? MappingProfile, Guid? SessionId = null);
    public sealed record VerifyPanNameRequest(string SubmittedName, ComparisonSourceContract Source);
    public sealed record VerifyAadharAddressRequest(string SubmittedAddress, ComparisonSourceContract Source);
    public sealed record VerificationWorkflowRequest(VerificationWorkflowType WorkflowType, WorkflowInputMode InputMode, string? Pan, string? PanSubmittedName, ComparisonSourceContract? PanSource, string? AadhaarNumber, string? AadhaarSubmittedAddress, ComparisonSourceContract? AadhaarSource);
    public sealed record ManualComparisonDecisionRequest(VerificationOperationType OperationType, ComparisonOutcome Outcome, Guid SourceRecordId, string SafeReason);
    public sealed record ManualPanOverrideRequest(string Pan, string CardholderName, DateOnly DateOfBirth, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
    public sealed record ManualAadhaarOverrideRequest(string AadhaarNumber, string CardholderName, string Address, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
    public sealed record VerificationRecordQuery(string? Search, VerificationOperationType? OperationType, ValidationStatus? ValidationStatus, ComparisonOutcome? ComparisonOutcome, VerificationMethod? Method, Guid? SessionId, DateTime? StartUtc, DateTime? EndUtc, int Page = 1, int PageSize = 20);
    public sealed record IdentifierValidationResult(VerificationOperationType OperationType, ValidationStatus ValidationStatus, Guid? RecordId, Guid? SessionId, string CorrelationId, DateTime TimestampUtc, string SafeMessage, bool ManualOverrideAvailable, string? MaskedSubjectReference);
    public sealed record ComparisonVerificationResult(VerificationOperationType OperationType, ComparisonOutcome ComparisonOutcome, Guid? RecordId, Guid? SessionId, string CorrelationId, DateTime TimestampUtc, string SafeMessage, bool ManualOverrideAvailable, string? MaskedSubjectReference);
    public sealed record WorkflowFailureSummary(VerificationOperationType OperationType, string Code, string SafeMessage);
    public sealed record VerificationWorkflowResponse(Guid SessionId, string CorrelationId, DateTime TimestampUtc, IdentifierValidationResult? PanIdentifierResult, ComparisonVerificationResult? PanNameResult, IdentifierValidationResult? AadhaarIdentifierResult, ComparisonVerificationResult? AadhaarAddressResult, IReadOnlyList<WorkflowFailureSummary> PartialFailures);
    public sealed record ValidationRecordResponse(Guid Id, Guid? SessionId, VerificationOperationType OperationType, ValidationStatus? ValidationStatus, ComparisonOutcome? ComparisonOutcome, VerificationMethod Method, DateTime CreatedUtc, string ActorReference, string? SafeReasonCode, string? SafeMessage, string? MaskedSubjectReference, bool HasProtectedDocument);
    public sealed record ProtectedDocumentResponse(Stream Content, string ContentType, string FileName);
    ```

    Models must not reference `IFormFile`; controller-only binding converts multipart input to this transport-neutral contract. _Requirements: 1.1-1.8, 2.1-2.8, 3.1-3.8, 4.1-4.8, 6.1-6.16, 7.3-7.18, 9.12-9.16, 10.1-10.18, 14.1-14.10._

  - [x] 3.2 CREATE `Finnova.Service/IdentityVerification/External/IdentityVerificationOptions.cs`

    ```csharp
    namespace Finnova.Service.IdentityVerification.External;
    public sealed class IdentityVerificationOptions
    {
        public const string SectionName = "IdentityVerification";
        public long MaxDocumentBytes { get; set; } = 5 * 1024 * 1024;
        public string[] AcceptedContentTypes { get; set; } = ["image/jpeg", "image/png", "application/pdf"];
        public RetryOptions Retry { get; set; } = new();
        public ProviderOperationOptions PanIdentifierValidation { get; set; } = new();
        public ProviderOperationOptions AadhaarIdentifierValidation { get; set; } = new();
        public ComparisonProviderOptions PanNameVerification { get; set; } = new();
        public ComparisonProviderOptions AadhaarAddressVerification { get; set; } = new();
    }
    public sealed class RetryOptions { public int MaxAttempts { get; set; } = 3; public int BaseDelayMilliseconds { get; set; } = 200; public int TimeoutSeconds { get; set; } = 10; }
    public class ProviderOperationOptions { public bool Enabled { get; set; } public string BaseAddress { get; set; } = ""; public string Path { get; set; } = ""; public string HttpMethod { get; set; } = "POST"; public string RequestMode { get; set; } = "Json"; public string IdentifierFieldName { get; set; } = ""; }
    public sealed class ComparisonProviderOptions : ProviderOperationOptions
    {
        public string AdapterName { get; set; } = "";
        public ComparisonInputMapping InputMapping { get; set; } = new();
        public string? SubmittedValueFieldName { get; set; }
        public string? DocumentFieldName { get; set; }
        public Dictionary<string,string> ExtractedValueFieldNames { get; set; } = new();
        public string? ResponseOutcomePath { get; set; }
        public string? ResponseMessagePath { get; set; }
    }
    public sealed class ComparisonInputMapping
    {
        public bool RequiresDocumentFile { get; set; }
        public bool RequiresSubmittedValue { get; set; } = true;
        public List<string> RequiredExtractedValueKeys { get; set; } = [];
        public bool AllowsDocumentAndExtractedValues { get; set; } = true;
        public string MappingProfile { get; set; } = "";
    }
    ```

    The provider verb, request mode, field names, response paths, timeout, retry, and source requirements are configuration. Confirm them against local Swagger before filling deployment configuration. _Requirements: 5.1-5.11, 8.1-8.12, 12.7, 12.11-12.12._

  - [x] 3.3 MODIFY `Finnova.Models/Contracts/IdentityVerification/IdentityVerificationContracts.cs`

    Add these named workflow records after `VerificationWorkflowRequest`; keep the generic record as the internal coordinator shape:

    ```csharp
    public sealed record PanAndNameWorkflowRequest(string Pan, string PanSubmittedName, ComparisonSourceContract PanSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
    public sealed record AadhaarAndAddressWorkflowRequest(string AadhaarNumber, string AadhaarSubmittedAddress, ComparisonSourceContract AadhaarSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
    public sealed record FullVerificationWorkflowRequest(string Pan, string PanSubmittedName, ComparisonSourceContract PanSource, string AadhaarNumber, string AadhaarSubmittedAddress, ComparisonSourceContract AadhaarSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
    ```

    The controller maps these records to `VerificationWorkflowRequest` with the matching `VerificationWorkflowType`; all four operations remain separately addressable in the response. _Requirements: 6.1-6.16, 14.1._

- [x] 4. Add EF configurations, DbSets, and migration instruction
  - [x] 4.1 CREATE `Finnova.Repository/Configuration/VerificationSessionConfiguration.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    namespace Finnova.Repository.Configuration;
    public sealed class VerificationSessionConfiguration : IEntityTypeConfiguration<VerificationSession>
    {
        public void Configure(EntityTypeBuilder<VerificationSession> b)
        {
            b.ToTable("verification_sessions"); b.HasKey(x => x.Id);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            b.Property(x => x.ActorReference).IsRequired().HasMaxLength(200);
            b.Property(x => x.RequestedOperationsJson).IsRequired().HasMaxLength(1000);
            b.Property(x => x.CreatedUtc).IsRequired(); b.HasIndex(x => x.CorrelationId);
        }
    }
    ```

    _Requirements: 6.15-6.16, 9.1, 9.4, 9.10-9.11._

  - [x] 4.2 CREATE `Finnova.Repository/Configuration/ValidationRecordConfiguration.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    namespace Finnova.Repository.Configuration;
    public sealed class ValidationRecordConfiguration : IEntityTypeConfiguration<ValidationRecord>
    {
        public void Configure(EntityTypeBuilder<ValidationRecord> b)
        {
            b.ToTable("validation_records"); b.HasKey(x => x.Id);
            b.Property(x => x.ActorReference).IsRequired().HasMaxLength(200); b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            b.Property(x => x.SafeReasonCode).HasMaxLength(100); b.Property(x => x.SafeMessage).HasMaxLength(500);
            b.Property(x => x.MaskedSubjectReference).HasMaxLength(100); b.Property(x => x.ProtectedSearchReference).HasMaxLength(200);
            b.Property(x => x.DiagnosticReference).HasMaxLength(100); b.Property(x => x.CreatedUtc).IsRequired();
            b.HasIndex(x => new { x.CreatedUtc, x.Id }); b.HasIndex(x => new { x.OperationType, x.ValidationStatus, x.ComparisonOutcome, x.Method });
            b.HasIndex(x => new { x.SessionId, x.CreatedUtc }); b.HasIndex(x => x.ProtectedSearchReference);
            b.HasOne(x => x.Session).WithMany(x => x.Records).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ProtectedDocumentReference).WithMany(x => x.ValidationRecords).HasForeignKey(x => x.ProtectedDocumentReferenceId).OnDelete(DeleteBehavior.Restrict);
            b.OwnsOne(x => x.SourceSummary, owned => { owned.Property(x => x.SourceType).HasColumnName("SourceType"); owned.Property(x => x.MappingProfile).HasMaxLength(200); owned.Property(x => x.DocumentContentType).HasMaxLength(100); owned.Property(x => x.ExtractedValueKeysJson).HasMaxLength(2000); });
        }
    }
    ```

    _Requirements: 3.5-3.8, 4.5-4.8, 5.11, 9.1-9.13, 10.1-10.16, 12.5-12.6._

  - [x] 4.3 CREATE `Finnova.Repository/Configuration/ProtectedDocumentReferenceConfiguration.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    namespace Finnova.Repository.Configuration;
    public sealed class ProtectedDocumentReferenceConfiguration : IEntityTypeConfiguration<ProtectedDocumentReference>
    {
        public void Configure(EntityTypeBuilder<ProtectedDocumentReference> b)
        {
            b.ToTable("protected_document_references"); b.HasKey(x => x.Id);
            b.Property(x => x.StorageKey).IsRequired().HasMaxLength(500); b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.ContentHash).IsRequired().HasMaxLength(128); b.Property(x => x.SizeBytes).IsRequired(); b.Property(x => x.CreatedUtc).IsRequired(); b.HasIndex(x => x.ContentHash);
        }
    }
    ```

    _Requirements: 7.16-7.18, 9.2-9.3, 12.9-12.12._

  - [x] 4.4 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

    Locate `public DbSet<CustomerAuditEntry> CustomerAuditEntries => Set<CustomerAuditEntry>();`. Add this exact snippet immediately after it once. Existing `using Finnova.Models.Domain.Entities;` is sufficient.

    ```csharp
    public DbSet<VerificationSession> VerificationSessions => Set<VerificationSession>();
    public DbSet<ValidationRecord> ValidationRecords => Set<ValidationRecord>();
    public DbSet<ProtectedDocumentReference> ProtectedDocumentReferences => Set<ProtectedDocumentReference>();
    ```

    _Requirements: 6.15-6.16, 9.1-9.16, 10.1-10.18._

  - [x] 4.5 CREATE `Finnova.Repository/Migrations/IdentityVerificationMigrationInstruction.md`

    ```text
    After tasks 4.1-4.4 and host registration are applied, create the migration from the API repository root:
    dotnet ef migrations add AddIdentityVerification --project Finnova.Repository --startup-project Finnova.SystemAdminService
    Inspect the generated migration. It must create verification_sessions, validation_records, and protected_document_references, configure restricted foreign keys, owned SourceSummary columns, and the indexes above. Do not add a provider-payload table.
    ```

    _Requirements: 9.1-9.5, 9.10-9.16._

- [x] 5. Add append-only repository and protected-document storage
  - [x] 5.1 CREATE `Finnova.Repository/Interfaces/IIdentityVerificationRepository.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Entities;
    namespace Finnova.Repository.Interfaces;
    public interface IIdentityVerificationRepository : IRepository<ValidationRecord>
    {
        Task AddSessionAsync(VerificationSession session, CancellationToken ct = default);
        Task AppendRecordAsync(ValidationRecord record, CancellationToken ct = default);
        Task AppendRecordsAsync(IReadOnlyCollection<ValidationRecord> records, CancellationToken ct = default);
        Task<VerificationSession?> GetSessionAsync(Guid id, CancellationToken ct = default);
        Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default);
        Task<(List<ValidationRecord> Items, int Total)> GetPagedAsync(VerificationRecordQuery query, CancellationToken ct = default);
        Task<List<ValidationRecord>> GetBySessionAsync(Guid id, CancellationToken ct = default);
    }
    ```

    _Requirements: 6.15-6.16, 7.5, 7.11-7.18, 9.1-9.16, 10.1-10.18._

  - [x] 5.2 CREATE `Finnova.Service/IdentityVerification/Storage/IProtectedDocumentStore.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    namespace Finnova.Service.IdentityVerification.Storage;
    public interface IProtectedDocumentStore
    {
        Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default);
        Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
        Task DeleteAsync(string storageKey, CancellationToken ct = default);
    }
    ```

    The implementation must use the platform-approved protected store. Do not put bytes in logs, public responses, `ValidationRecord`, or an unprotected local path. _Requirements: 7.3-7.5, 7.16-7.18, 9.2-9.3, 10.17-10.18, 12.9-12.12, 13.14-13.15._

  - [x] 5.3 CREATE `Finnova.Repository/Repositories/IdentityVerificationRepository.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;
    namespace Finnova.Repository.Repositories;
    public sealed class IdentityVerificationRepository : RepositoryBase<ValidationRecord>, IIdentityVerificationRepository
    {
        public IdentityVerificationRepository(FinnovaDbContext context) : base(context) { }
        public async Task AddSessionAsync(VerificationSession session, CancellationToken ct = default) { session.CreatedUtc = DateTime.UtcNow; await Context.VerificationSessions.AddAsync(session, ct); await Context.SaveChangesAsync(ct); }
        public async Task AppendRecordAsync(ValidationRecord record, CancellationToken ct = default) { record.CreatedUtc = DateTime.UtcNow; IdentityVerificationInvariants.EnsureResultDomain(record); await DbSet.AddAsync(record, ct); try { await Context.SaveChangesAsync(ct); } catch (Exception ex) { throw new IdentityVerificationPersistenceException(ex); } }
        public async Task AppendRecordsAsync(IReadOnlyCollection<ValidationRecord> records, CancellationToken ct = default) { foreach (var r in records) { r.CreatedUtc = DateTime.UtcNow; IdentityVerificationInvariants.EnsureResultDomain(r); } await DbSet.AddRangeAsync(records, ct); try { await Context.SaveChangesAsync(ct); } catch (Exception ex) { throw new IdentityVerificationPersistenceException(ex); } }
        public Task<VerificationSession?> GetSessionAsync(Guid id, CancellationToken ct = default) => Context.VerificationSessions.AsNoTracking().Include(x => x.Records).SingleOrDefaultAsync(x => x.Id == id, ct);
        public Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default) => DbSet.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        public async Task<(List<ValidationRecord> Items, int Total)> GetPagedAsync(VerificationRecordQuery q, CancellationToken ct = default)
        {
            var query = DbSet.AsNoTracking().AsQueryable();
            if (q.OperationType is not null) query = query.Where(x => x.OperationType == q.OperationType);
            if (q.ValidationStatus is not null) query = query.Where(x => x.ValidationStatus == q.ValidationStatus);
            if (q.ComparisonOutcome is not null) query = query.Where(x => x.ComparisonOutcome == q.ComparisonOutcome);
            if (q.Method is not null) query = query.Where(x => x.Method == q.Method);
            if (q.SessionId is not null) query = query.Where(x => x.SessionId == q.SessionId);
            if (q.StartUtc is not null) query = query.Where(x => x.CreatedUtc >= q.StartUtc);
            if (q.EndUtc is not null) query = query.Where(x => x.CreatedUtc < q.EndUtc);
            if (!string.IsNullOrWhiteSpace(q.Search)) { var s = q.Search.Trim(); query = query.Where(x => x.ProtectedSearchReference == s || x.CorrelationId == s || x.Id.ToString() == s); }
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
            return (items, total);
        }
        public Task<List<ValidationRecord>> GetBySessionAsync(Guid id, CancellationToken ct = default) => DbSet.AsNoTracking().Where(x => x.SessionId == id).OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
        public override Task UpdateAsync(ValidationRecord entity, CancellationToken ct = default) => throw new IdentityVerificationImmutableException();
        public override Task DeleteAsync(ValidationRecord entity, CancellationToken ct = default) => throw new IdentityVerificationImmutableException();
    }
    ```

    No update/delete controller exists. The repository sets server UTC timestamps and never returns a record identifier before `SaveChangesAsync` succeeds. _Requirements: 6.10-6.16, 7.11-7.18, 9.1-9.16, 10.1-10.16._

  - [x] 5.4 MODIFY `Finnova.Repository/DependencyInjection.cs`

    Locate `services.AddScoped<ICustomerUnitOfWork, CustomerUnitOfWork>();`. Add the following exactly once immediately before `return services;`. Existing repository usings cover the types.

    ```csharp
    services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();
    ```

    _Requirements: 9.1-9.16, 10.1-10.18._

  - [x] 5.5 CREATE `Finnova.Service/IdentityVerification/Storage/ProtectedDocumentStoreRegistration.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;
    using Microsoft.Extensions.DependencyInjection;
    namespace Finnova.Service.IdentityVerification.Storage;
    public static class ProtectedDocumentStoreRegistration
    {
        public static IServiceCollection AddIdentityVerificationStorage(this IServiceCollection services)
        { services.AddScoped<IProtectedDocumentStore, ProtectedDocumentStoreNotConfigured>(); return services; }
    }
    internal sealed class ProtectedDocumentStoreNotConfigured : IProtectedDocumentStore
    {
        public Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
        public Task DeleteAsync(string storageKey, CancellationToken ct = default) => throw new IdentityVerificationConfigurationException();
    }
    ```

    Replace only this fail-closed implementation after the approved protected store is selected. _Requirements: 12.11-12.12, 13.14-13.15._

  - [x] 5.6 MODIFY `Finnova.Repository/Repositories/IdentityVerificationRepository.cs`

    Replace the `GetRecordAsync` expression with this exact include so protected-document retrieval can authorize against the immutable record without exposing bytes:

    ```csharp
    public Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default)
        => DbSet.AsNoTracking().Include(x => x.ProtectedDocumentReference).SingleOrDefaultAsync(x => x.Id == id, ct);
    ```

    _Requirements: 9.12-9.13, 10.17-10.18, 13.14-13.15._

- [x] 6. Add pure validation, masking, document, and correlation helpers
  - [x] 6.1 CREATE `Finnova.Service/IdentityVerification/IdentityVerificationRules.cs`

    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.IdentityVerification;
    public static partial class IdentityVerificationRules
    {
        [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.CultureInvariant)] private static partial Regex PanPattern();
        [GeneratedRegex("^[0-9]{12}$", RegexOptions.CultureInvariant)] private static partial Regex AadhaarPattern();
        public static string NormalizePan(string value) { var x = value.Trim().ToUpperInvariant(); if (!PanPattern().IsMatch(x)) throw new IdentityVerificationValidationException(["Pan"]); return x; }
        public static string NormalizeAadhaar(string value) { var x = value.Trim(); if (!AadhaarPattern().IsMatch(x)) throw new IdentityVerificationValidationException(["AadhaarNumber"]); return x; }
        public static string RequireText(string? value, string field) { var x = value?.Trim() ?? ""; if (x.Length == 0) throw new IdentityVerificationValidationException([field]); return x; }
        public static string MaskIdentifier(string value) => value.Length <= 4 ? "****" : new string('*', value.Length - 4) + value[^4..];
        public static string SearchReference(string value, byte[] key) { using var h = new HMACSHA256(key); return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        public static bool IsSupportedContentType(string value) => value is "image/jpeg" or "image/png" or "application/pdf";
        public static bool HasAllowedExtension(string? file) { var x = Path.GetExtension(file ?? "").ToLowerInvariant(); return x is ".jpg" or ".jpeg" or ".png" or ".pdf"; }
    }
    ```

    _Requirements: 1.1, 1.3, 2.1, 2.3, 5.3, 5.5, 5.9, 7.6-7.10, 8.12, 10.1, 12.1-12.10._

  - [x] 6.2 CREATE `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs`

    ```csharp
    using Finnova.Models.Domain.Exceptions;
    using Microsoft.Extensions.Options;
    using Finnova.Service.IdentityVerification.External;
    namespace Finnova.Service.IdentityVerification.Validation;
    public sealed class DocumentInputValidator
    {
        private readonly IdentityVerificationOptions _options;
        public DocumentInputValidator(IOptions<IdentityVerificationOptions> options) => _options = options.Value;
        public void Validate(string fileName, string contentType, long sizeBytes, ReadOnlySpan<byte> header)
        {
            if (sizeBytes > _options.MaxDocumentBytes) throw new IdentityVerificationDocumentSizeException();
            if (!_options.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase) || !IdentityVerificationRules.HasAllowedExtension(fileName) || !HasSignature(contentType, header)) throw new IdentityVerificationDocumentFormatException();
        }
        private static bool HasSignature(string type, ReadOnlySpan<byte> h) => type switch
        {
            "image/jpeg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            "image/png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }),
            "application/pdf" => h.Length >= 5 && h[..5].SequenceEqual("%PDF-"u8), _ => false
        };
    }
    ```

    Inspect length before buffering, validate extension/MIME/signature, dispose rejected content, and never persist or log rejected bytes. _Requirements: 7.8-7.10, 8.12, 12.9-12.10._

  - [x] 6.3 CREATE `Finnova.Service/IdentityVerification/Correlation/RequestContext.cs`

    ```csharp
    using Microsoft.AspNetCore.Http;
    namespace Finnova.Service.IdentityVerification.Correlation;
    public sealed record RequestContext(string CorrelationId, string ActorReference, DateTime UtcNow);
    public interface IRequestContextAccessor { RequestContext Current { get; } }
    public sealed class RequestContextAccessor(IHttpContextAccessor http) : IRequestContextAccessor
    {
        public RequestContext Current => new(http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), http.HttpContext?.User.Identity?.Name ?? "system-admin", DateTime.UtcNow);
    }
    ```

    _Requirements: 6.9, 6.15, 8.8, 8.11, 9.1, 9.4-9.5, 12.6, 14.6._

- [x] 7. Add provider-neutral adapters, configurable DTO boundary, HTTP clients, and resilience
  - [x] 7.1 CREATE `Finnova.Service/IdentityVerification/Adapters/IdentityVerificationAdapters.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Service.IdentityVerification.Adapters;
    public sealed record IdentifierValidationInput(VerificationOperationType OperationType, string Identifier, string CorrelationId);
    public sealed record ProviderNeutralComparisonInput(VerificationOperationType OperationType, string SubmittedValue, string? ProtectedStorageKey, Stream? ProtectedDocumentStream, IReadOnlyDictionary<string,string> ExtractedValues, string CorrelationId, string MappingProfile);
    public sealed record IdentifierAdapterResult(ValidationStatus Status, string SafeMessage, string? SafeReasonCode = null);
    public sealed record ComparisonAdapterResult(ComparisonOutcome Outcome, string SafeMessage, string? SafeReasonCode = null);
    public interface IIdentifierValidationAdapter { VerificationOperationType SupportedOperation { get; } Task<IdentifierAdapterResult> ValidateAsync(IdentifierValidationInput input, CancellationToken ct); }
    public interface IComparisonAdapter { VerificationOperationType SupportedOperation { get; } Task<ComparisonAdapterResult> VerifyAsync(ProviderNeutralComparisonInput input, CancellationToken ct); }
    public interface IComparisonAdapterResolver { IComparisonAdapter Resolve(VerificationOperationType operation, string adapterName); }
    ```

    Only this neutral input crosses the service/provider boundary. Provider DTOs and field names remain internal. _Requirements: 3.6-3.7, 4.6-4.7, 5.1-5.11, 14.9-14.10._

  - [x] 7.2 CREATE `Finnova.Service/IdentityVerification/External/ProviderDtoPlaceholders.cs`

    ```csharp
    namespace Finnova.Service.IdentityVerification.External;
    /// <summary>Replace fields only after confirming the configured provider Swagger/OpenAPI contract.</summary>
    internal sealed record ConfirmedProviderRequestPlaceholder(IReadOnlyDictionary<string,string> ConfiguredFields);
    internal sealed record ConfirmedProviderResponsePlaceholder(string? OutcomeToken, string? MessageToken, IReadOnlyDictionary<string,string>? ConfirmedFields);
    ```

    **Confirm-before-implementation:** inspect the local Swagger/OpenAPI contract for each configured provider and replace only these internal placeholder fields/mapping. Do not assume `isValid`, `name`, `address`, UIDAI fields, PAN fields, JSON property names, multipart names, authentication headers, or response status meanings. Map only to normalized `ComparisonAdapterResult` or `IdentifierAdapterResult`. These DTOs must never appear in Models or public responses. _Requirements: 3.7, 4.7, 5.7-5.10, 8.9-8.11, 14.6-14.7._

  - [x] 7.3 CREATE `Finnova.Service/IdentityVerification/External/ConfigurableComparisonHttpClient.cs`

    ```csharp
    using System.Net;
    using System.Net.Http.Json;
    using System.Text;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.IdentityVerification.Adapters;
    using Microsoft.Extensions.Options;
    namespace Finnova.Service.IdentityVerification.External;
    public sealed class ConfigurableComparisonHttpClient(HttpClient http, IOptions<IdentityVerificationOptions> options, IProviderResponseMapper mapper, VerificationOperationType operation) : IComparisonAdapter
    {
        public VerificationOperationType SupportedOperation => operation;
        public async Task<ComparisonAdapterResult> VerifyAsync(ProviderNeutralComparisonInput input, CancellationToken ct)
        {
            var c = operation == VerificationOperationType.PanNameVerification ? options.Value.PanNameVerification : options.Value.AadhaarAddressVerification;
            if (!c.Enabled || string.IsNullOrWhiteSpace(c.BaseAddress) || string.IsNullOrWhiteSpace(c.Path)) throw new IdentityVerificationConfigurationException();
            using var req = new HttpRequestMessage(new HttpMethod(c.HttpMethod), c.Path);
            req.Headers.Add("X-Correlation-Id", input.CorrelationId);
            if (c.RequestMode.Equals("Multipart", StringComparison.OrdinalIgnoreCase))
            {
                var form = new MultipartFormDataContent();
                if (c.SubmittedValueFieldName is not null) form.Add(new StringContent(input.SubmittedValue, Encoding.UTF8), c.SubmittedValueFieldName);
                if (input.ProtectedDocumentStream is not null && c.DocumentFieldName is not null) form.Add(new StreamContent(input.ProtectedDocumentStream), c.DocumentFieldName, "identity-document");
                foreach (var pair in input.ExtractedValues) if (c.ExtractedValueFieldNames.TryGetValue(pair.Key, out var field)) form.Add(new StringContent(pair.Value, Encoding.UTF8), field);
                req.Content = form;
            }
            else
            {
                var fields = new Dictionary<string,object?>();
                if (c.SubmittedValueFieldName is not null) fields[c.SubmittedValueFieldName] = input.SubmittedValue;
                foreach (var pair in input.ExtractedValues) if (c.ExtractedValueFieldNames.TryGetValue(pair.Key, out var field)) fields[field] = pair.Value;
                if (c.DocumentFieldName is not null && input.ProtectedStorageKey is not null) fields[c.DocumentFieldName] = input.ProtectedStorageKey;
                req.Content = JsonContent.Create(fields);
            }
            using var response = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout) throw new IdentityVerificationProviderUnavailableException();
            if (!response.IsSuccessStatusCode) return new(ComparisonOutcome.NoUsableMatch, "The comparison could not produce a usable result.", "ProviderRejected");
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            return await mapper.MapAsync(operation, body, c, ct);
        }
    }
    public interface IProviderResponseMapper { Task<ComparisonAdapterResult> MapAsync(VerificationOperationType operation, Stream body, ComparisonProviderOptions options, CancellationToken ct); }
    ```

    The HTTP method, URL path, request mode, fields, and response mapping are configurable. The implementer must complete `IProviderResponseMapper` only after local Swagger confirmation. No raw provider payload may enter a public response or log. _Requirements: 3.1-3.7, 4.1-4.7, 5.1-5.10, 8.1-8.12, 12.7-12.8, 14.6-14.7._

  - [x] 7.4 CREATE `Finnova.Service/IdentityVerification/External/ProviderCallExecutor.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Microsoft.Extensions.Options;
    namespace Finnova.Service.IdentityVerification.External;
    public interface IProviderCallExecutor { Task<T> ExecuteAsync<T>(VerificationOperationType operation, Func<CancellationToken,Task<T>> call, CancellationToken ct); }
    public sealed class ProviderCallExecutor(IOptions<IdentityVerificationOptions> options) : IProviderCallExecutor
    {
        public async Task<T> ExecuteAsync<T>(VerificationOperationType operation, Func<CancellationToken,Task<T>> call, CancellationToken ct)
        {
            var max = Math.Max(1, options.Value.Retry.MaxAttempts);
            for (var attempt = 1; attempt <= max; attempt++)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.Retry.TimeoutSeconds)));
                try { return await call(timeout.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt == max) { throw new IdentityVerificationProviderUnavailableException(); }
                catch (HttpRequestException) when (attempt < max) { await Task.Delay(Math.Min(30000, options.Value.Retry.BaseDelayMilliseconds * (1 << (attempt - 1))), ct); }
            }
            throw new IdentityVerificationProviderUnavailableException();
        }
    }
    ```

    Retry only configured transient transport/server failures. Never retry input validation, configuration errors, malformed payloads, or provider business results. Identifier callers normalize exhaustion to `Unavailable`; comparison callers normalize exhaustion to `NoUsableMatch`. _Requirements: 8.1-8.9, 14.3._

  - [x] 7.5 CREATE `Finnova.Service/IdentityVerification/External/ConfiguredIdentifierAdapter.cs`

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.IdentityVerification.Adapters;
    namespace Finnova.Service.IdentityVerification.External;
    public interface IConfiguredIdentifierClient
    { Task<IdentifierAdapterResult> ValidateAsync(VerificationOperationType operation, string identifier, string correlationId, CancellationToken ct); }
    public sealed class ConfiguredIdentifierAdapter(IConfiguredIdentifierClient client, VerificationOperationType operation) : IIdentifierValidationAdapter
    {
        public VerificationOperationType SupportedOperation => operation;
        public Task<IdentifierAdapterResult> ValidateAsync(IdentifierValidationInput input, CancellationToken ct) => client.ValidateAsync(operation, input.Identifier, input.CorrelationId, ct);
    }
    ```

    Keep PAN identifier validation and Aadhaar identifier validation as separate adapter registrations and endpoints. Do not route either through a comparison adapter. _Requirements: 1.1-1.8, 2.1-2.8, 6.1-6.8, 14.8._

- [x] 8. Add CQRS commands, handlers, validators, workflow coordinator, and mappers
  - [x] 8.1 CREATE `Finnova.Service/IdentityVerification/Commands/VerifyPanName/VerifyPanNameCommand.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Commands.VerifyPanName;
    public sealed record VerifyPanNameCommand(VerifyPanNameRequest Request, string ActorReference) : IRequest<ComparisonVerificationResult>;
    ```

    _Requirements: 3.1-3.8, 5.1-5.11, 14.9._

  - [x] 8.2 CREATE `Finnova.Service/IdentityVerification/Commands/VerifyAadharAddress/VerifyAadharAddressCommand.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
    public sealed record VerifyAadharAddressCommand(VerifyAadharAddressRequest Request, string ActorReference) : IRequest<ComparisonVerificationResult>;
    ```

    _Requirements: 4.1-4.8, 5.1-5.11, 14.10._

  - [x] 8.3 CREATE `Finnova.Service/IdentityVerification/Commands/IdentityVerificationCommandValidators.cs`

    ```csharp
    using FluentValidation;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
    using Finnova.Service.IdentityVerification.Commands.VerifyPanName;
    namespace Finnova.Service.IdentityVerification.Commands;
    public sealed class VerifyPanNameCommandValidator : AbstractValidator<VerifyPanNameCommand>
    { public VerifyPanNameCommandValidator() { RuleFor(x => x.Request.SubmittedName).NotEmpty().MaximumLength(200); RuleFor(x => x.Request.Source).NotNull(); } }
    public sealed class VerifyAadharAddressCommandValidator : AbstractValidator<VerifyAadharAddressCommand>
    { public VerifyAadharAddressCommandValidator() { RuleFor(x => x.Request.SubmittedAddress).NotEmpty().MaximumLength(1000); RuleFor(x => x.Request.Source).NotNull(); } }
    public sealed class VerificationWorkflowRequestValidator : AbstractValidator<VerificationWorkflowRequest>
    {
        public VerificationWorkflowRequestValidator() { RuleFor(x => x.WorkflowType).IsInEnum(); RuleFor(x => x.InputMode).IsInEnum(); RuleFor(x => x.Pan).MaximumLength(10).When(x => x.Pan is not null); RuleFor(x => x.AadhaarNumber).MaximumLength(12).When(x => x.AadhaarNumber is not null); RuleFor(x => x.PanSubmittedName).MaximumLength(200).When(x => x.PanSubmittedName is not null); RuleFor(x => x.AadhaarSubmittedAddress).MaximumLength(1000).When(x => x.AadhaarSubmittedAddress is not null); }
    }
    ```

    Add only validator registrations supplied by the existing assembly scan. Validators must execute before adapters or protected storage. _Requirements: 1.3, 2.3, 3.1, 4.1, 5.3, 5.5, 5.9, 6.7-6.14, 7.6-7.10, 8.12, 10.12, 14.4-14.5._

  - [x] 8.4 CREATE `Finnova.Service/IdentityVerification/IdentityVerificationMapper.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.IdentityVerification.Adapters;
    namespace Finnova.Service.IdentityVerification;
    public static class IdentityVerificationMapper
    {
        public static IdentifierValidationResult ToIdentifierResult(ValidationRecord r) => new(r.OperationType, r.ValidationStatus!.Value, r.Id, r.SessionId, r.CorrelationId, r.CreatedUtc, r.SafeMessage ?? "Verification completed.", r.ValidationStatus == ValidationStatus.Unavailable, r.MaskedSubjectReference);
        public static ComparisonVerificationResult ToComparisonResult(ValidationRecord r) => new(r.OperationType, r.ComparisonOutcome!.Value, r.Id, r.SessionId, r.CorrelationId, r.CreatedUtc, r.SafeMessage ?? "Comparison completed.", r.ComparisonOutcome == ComparisonOutcome.NoUsableMatch, r.MaskedSubjectReference);
        public static ValidationRecord CreateComparisonRecord(VerificationOperationType op, ComparisonAdapterResult result, string actor, string correlation, Guid? session, SourceSummary source, string? masked) => new() { OperationType = op, ComparisonOutcome = result.Outcome, Method = VerificationMethod.Auto, ActorReference = actor, CorrelationId = correlation, SessionId = session, SafeReasonCode = result.SafeReasonCode, SafeMessage = result.SafeMessage, SourceSummary = source, MaskedSubjectReference = masked };
    }
    ```

    Mapper output must contain only normalized outcomes/statuses, safe messages, UTC timestamps, IDs, and masked references. _Requirements: 3.5-3.8, 4.5-4.8, 9.1-9.5, 9.12-9.16, 14.1-14.10._

  - [x] 8.5 CREATE `Finnova.Service/IdentityVerification/Commands/ComparisonCommandHandlers.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Adapters;
    using Finnova.Service.IdentityVerification.Correlation;
    using Finnova.Service.IdentityVerification.External;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Commands;
    public sealed class VerifyPanNameCommandHandler(IComparisonAdapterResolver adapters, IIdentityVerificationRepository repository, Finnova.Service.IdentityVerification.Storage.IProtectedDocumentStore store, IRequestContextAccessor context, IProviderCallExecutor executor) : IRequestHandler<VerifyPanName.VerifyPanNameCommand, ComparisonVerificationResult>
    {
        public async Task<ComparisonVerificationResult> Handle(VerifyPanName.VerifyPanNameCommand request, CancellationToken ct)
        {
            var c = context.Current; var value = IdentityVerificationRules.RequireText(request.Request.SubmittedName, "SubmittedName");
            var adapter = adapters.Resolve(VerificationOperationType.PanNameVerification, request.Request.Source.MappingProfile ?? "");
            var reference = request.Request.Source.ProtectedDocumentReferenceId is null ? null : await repository.GetProtectedDocumentReferenceAsync(request.Request.Source.ProtectedDocumentReferenceId.Value, ct) ?? throw new Finnova.Models.Domain.Exceptions.IdentityVerificationRecordNotFoundException();
            await using var stream = reference is null ? null : await store.OpenReadAsync(reference.StorageKey, ct);
            var result = await executor.ExecuteAsync(VerificationOperationType.PanNameVerification, t => adapter.VerifyAsync(new(VerificationOperationType.PanNameVerification, value, null, stream, request.Request.Source.ExtractedValues ?? new Dictionary<string,string>(), c.CorrelationId, request.Request.Source.MappingProfile ?? ""), t), ct);
            var record = IdentityVerificationMapper.CreateComparisonRecord(VerificationOperationType.PanNameVerification, result, request.ActorReference, c.CorrelationId, request.Request.Source.SessionId, new() { MappingProfile = request.Request.Source.MappingProfile, DocumentProvided = reference is not null, SourceType = reference is null ? VerificationSourceType.ExtractedValues : VerificationSourceType.DocumentAndExtractedValues }, null);
            await repository.AppendRecordAsync(record, ct); return IdentityVerificationMapper.ToComparisonResult(record);
        }
    }
    public sealed class VerifyAadharAddressCommandHandler(IComparisonAdapterResolver adapters, IIdentityVerificationRepository repository, Finnova.Service.IdentityVerification.Storage.IProtectedDocumentStore store, IRequestContextAccessor context, IProviderCallExecutor executor) : IRequestHandler<VerifyAadharAddress.VerifyAadharAddressCommand, ComparisonVerificationResult>
    {
        public async Task<ComparisonVerificationResult> Handle(VerifyAadharAddress.VerifyAadharAddressCommand request, CancellationToken ct)
        {
            var c = context.Current; var value = IdentityVerificationRules.RequireText(request.Request.SubmittedAddress, "SubmittedAddress");
            var adapter = adapters.Resolve(VerificationOperationType.AadhaarAddressVerification, request.Request.Source.MappingProfile ?? "");
            var reference = request.Request.Source.ProtectedDocumentReferenceId is null ? null : await repository.GetProtectedDocumentReferenceAsync(request.Request.Source.ProtectedDocumentReferenceId.Value, ct) ?? throw new Finnova.Models.Domain.Exceptions.IdentityVerificationRecordNotFoundException();
            await using var stream = reference is null ? null : await store.OpenReadAsync(reference.StorageKey, ct);
            var result = await executor.ExecuteAsync(VerificationOperationType.AadhaarAddressVerification, t => adapter.VerifyAsync(new(VerificationOperationType.AadhaarAddressVerification, value, null, stream, request.Request.Source.ExtractedValues ?? new Dictionary<string,string>(), c.CorrelationId, request.Request.Source.MappingProfile ?? ""), t), ct);
            var record = IdentityVerificationMapper.CreateComparisonRecord(VerificationOperationType.AadhaarAddressVerification, result, request.ActorReference, c.CorrelationId, request.Request.Source.SessionId, new() { MappingProfile = request.Request.Source.MappingProfile, DocumentProvided = reference is not null, SourceType = reference is null ? VerificationSourceType.ExtractedValues : VerificationSourceType.DocumentAndExtractedValues }, null);
            await repository.AppendRecordAsync(record, ct); return IdentityVerificationMapper.ToComparisonResult(record);
        }
    }
    ```

    Add the pre-call mapping/input validator before `Resolve` and use the protected document store rather than an unprotected `MemoryStream` when a configured provider requires a file. The snippet shows handler shape only; it is not permission to bypass the 5 MB/signature/protected-store boundary. _Requirements: 3.1-3.8, 4.1-4.8, 5.1-5.11, 8.1-8.12, 9.1-9.16._

  - [x] 8.6 CREATE `Finnova.Service/IdentityVerification/Workflow/VerificationWorkflowCoordinator.cs`

    ```csharp
    using System.Text.Json;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Adapters;
    using Finnova.Service.IdentityVerification.Correlation;
    using Finnova.Service.IdentityVerification.External;
    namespace Finnova.Service.IdentityVerification.Workflow;
    public interface IVerificationWorkflowCoordinator { Task<VerificationWorkflowResponse> ExecuteAsync(VerificationWorkflowRequest request, CancellationToken ct); }
    public sealed class VerificationWorkflowCoordinator(IIdentityVerificationRepository repository, Finnova.Service.IdentityVerification.Storage.IProtectedDocumentStore store, IProviderCallExecutor executor, IEnumerable<IIdentifierValidationAdapter> identifiers, IComparisonAdapterResolver comparisons, IRequestContextAccessor context) : IVerificationWorkflowCoordinator
    {
        public async Task<VerificationWorkflowResponse> ExecuteAsync(VerificationWorkflowRequest request, CancellationToken ct)
        {
            var c = context.Current; ValidateWorkflowShape(request);
            var session = new VerificationSession { CorrelationId = c.CorrelationId, ActorReference = c.ActorReference, WorkflowType = request.WorkflowType, InputMode = request.InputMode, RequestedOperationsJson = JsonSerializer.Serialize(GetOperations(request)) };
            await repository.AddSessionAsync(session, ct);
            var failures = new List<WorkflowFailureSummary>(); IdentifierValidationResult? pan = null, aadhaar = null; ComparisonVerificationResult? panName = null, address = null;
            var jobs = new List<Task>
            {
                RunIdentifier(VerificationOperationType.PanIdentifierValidation, request.Pan, session, x => pan = x, failures, c, ct),
                RunComparison(VerificationOperationType.PanNameVerification, request.PanSubmittedName, request.PanSource, session, x => panName = x, failures, c, ct),
                RunIdentifier(VerificationOperationType.AadhaarIdentifierValidation, request.AadhaarNumber, session, x => aadhaar = x, failures, c, ct),
                RunComparison(VerificationOperationType.AadhaarAddressVerification, request.AadhaarSubmittedAddress, request.AadhaarSource, session, x => address = x, failures, c, ct)
            };
            await Task.WhenAll(jobs);
            return new(session.Id, c.CorrelationId, DateTime.UtcNow, pan, panName, aadhaar, address, failures);
        }
        private async Task RunIdentifier(VerificationOperationType op, string? raw, VerificationSession s, Action<IdentifierValidationResult> set, List<WorkflowFailureSummary> failures, RequestContext c, CancellationToken ct)
        {
            if (raw is null) return; try { var id = op == VerificationOperationType.PanIdentifierValidation ? IdentityVerificationRules.NormalizePan(raw) : IdentityVerificationRules.NormalizeAadhaar(raw); var a = identifiers.Single(x => x.SupportedOperation == op); var result = await executor.ExecuteAsync(op, t => a.ValidateAsync(new(op, id, c.CorrelationId), t), ct); var record = new ValidationRecord { SessionId = s.Id, CorrelationId = c.CorrelationId, ActorReference = c.ActorReference, OperationType = op, ValidationStatus = result.Status, Method = VerificationMethod.Auto, SafeMessage = result.SafeMessage, SafeReasonCode = result.SafeReasonCode, MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(id) }; await repository.AppendRecordAsync(record, ct); set(IdentityVerificationMapper.ToIdentifierResult(record)); } catch (IdentityVerificationProviderUnavailableException) { failures.Add(new(op, "ERR-IVF-503", "The identifier provider is unavailable.")); }
        }
        private async Task RunComparison(VerificationOperationType op, string? raw, ComparisonSourceContract? source, VerificationSession s, Action<ComparisonVerificationResult> set, List<WorkflowFailureSummary> failures, RequestContext c, CancellationToken ct)
        {
            if (raw is null || source is null) return; try { var value = IdentityVerificationRules.RequireText(raw, op == VerificationOperationType.PanNameVerification ? "PanSubmittedName" : "AadhaarSubmittedAddress"); var a = comparisons.Resolve(op, source.MappingProfile ?? ""); var reference = source.ProtectedDocumentReferenceId is null ? null : await repository.GetProtectedDocumentReferenceAsync(source.ProtectedDocumentReferenceId.Value, ct) ?? throw new IdentityVerificationValidationException(["ProtectedDocumentReferenceId"]); await using var stream = reference is null ? null : await store.OpenReadAsync(reference.StorageKey, ct); var result = await executor.ExecuteAsync(op, t => a.VerifyAsync(new(op, value, null, stream, source.ExtractedValues ?? new Dictionary<string,string>(), c.CorrelationId, source.MappingProfile ?? ""), t), ct); var record = IdentityVerificationMapper.CreateComparisonRecord(op, result, c.ActorReference, c.CorrelationId, s.Id, new(), null); await repository.AppendRecordAsync(record, ct); set(IdentityVerificationMapper.ToComparisonResult(record)); } catch (IdentityVerificationProviderUnavailableException) { failures.Add(new(op, "ERR-IVF-503", "The comparison provider is unavailable.")); } catch (IdentityVerificationValidationException) { failures.Add(new(op, "ERR-IVF-400", "The comparison input is invalid.")); }
        }
        private static void ValidateWorkflowShape(VerificationWorkflowRequest r) { if (r.InputMode == WorkflowInputMode.Atomic && (r.WorkflowType == VerificationWorkflowType.Full && (r.Pan is null || r.PanSubmittedName is null || r.AadhaarNumber is null || r.AadhaarSubmittedAddress is null))) throw new IdentityVerificationValidationException(["Workflow"]); }
        private static IReadOnlyList<VerificationOperationType> GetOperations(VerificationWorkflowRequest r) => Enum.GetValues<VerificationOperationType>().Where(x => (x == VerificationOperationType.PanIdentifierValidation && r.Pan is not null) || (x == VerificationOperationType.PanNameVerification && r.PanSubmittedName is not null) || (x == VerificationOperationType.AadhaarIdentifierValidation && r.AadhaarNumber is not null) || (x == VerificationOperationType.AadhaarAddressVerification && r.AadhaarSubmittedAddress is not null)).ToArray();
    }
    ```

    Complete operation-specific source validation before any provider call, and replace the illustrative in-memory stream with the protected store. Partial mode runs valid operations independently; atomic mode validates every requested operation first and makes zero provider calls on any invalid input. _Requirements: 6.1-6.16, 8.1-8.9, 9.1, 9.10-9.16, design Properties 3 and 6._

  - [x] 8.7 CREATE `Finnova.Service/IdentityVerification/Commands/ValidateIdentifiers/ValidateIdentifierCommands.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Adapters;
    using Finnova.Service.IdentityVerification.Correlation;
    using Finnova.Service.IdentityVerification.External;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Commands.ValidateIdentifiers;
    public sealed record ValidatePanCommand(ValidatePanRequest Request, string ActorReference) : IRequest<IdentifierValidationResult>;
    public sealed record ValidateAadhaarCommand(ValidateAadhaarRequest Request, string ActorReference) : IRequest<IdentifierValidationResult>;
    public sealed class ValidatePanHandler(IEnumerable<IIdentifierValidationAdapter> adapters, IIdentityVerificationRepository repo, IRequestContextAccessor context, IProviderCallExecutor executor) : IRequestHandler<ValidatePanCommand, IdentifierValidationResult>
    { public Task<IdentifierValidationResult> Handle(ValidatePanCommand r, CancellationToken ct) => HandleCore(r.Request.Pan, VerificationOperationType.PanIdentifierValidation, r.Request.SessionId, r.ActorReference, ct); private async Task<IdentifierValidationResult> HandleCore(string raw, VerificationOperationType op, Guid? session, string actor, CancellationToken ct) { var c = context.Current; var id = IdentityVerificationRules.NormalizePan(raw); var a = adapters.Single(x => x.SupportedOperation == op); var result = await executor.ExecuteAsync(op, t => a.ValidateAsync(new(op, id, c.CorrelationId), t), ct); var record = new Finnova.Models.Domain.Entities.ValidationRecord { SessionId = session, CorrelationId = c.CorrelationId, ActorReference = actor, OperationType = op, ValidationStatus = result.Status, Method = VerificationMethod.Auto, SafeMessage = result.SafeMessage, SafeReasonCode = result.SafeReasonCode, MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(id) }; await repo.AppendRecordAsync(record, ct); return IdentityVerificationMapper.ToIdentifierResult(record); } }
    public sealed class ValidateAadhaarHandler(IEnumerable<IIdentifierValidationAdapter> adapters, IIdentityVerificationRepository repo, IRequestContextAccessor context, IProviderCallExecutor executor) : IRequestHandler<ValidateAadhaarCommand, IdentifierValidationResult>
    { public async Task<IdentifierValidationResult> Handle(ValidateAadhaarCommand r, CancellationToken ct) { var c = context.Current; var id = IdentityVerificationRules.NormalizeAadhaar(r.Request.AadhaarNumber); var a = adapters.Single(x => x.SupportedOperation == VerificationOperationType.AadhaarIdentifierValidation); var result = await executor.ExecuteAsync(VerificationOperationType.AadhaarIdentifierValidation, t => a.ValidateAsync(new(VerificationOperationType.AadhaarIdentifierValidation, id, c.CorrelationId), t), ct); var record = new Finnova.Models.Domain.Entities.ValidationRecord { SessionId = r.Request.SessionId, CorrelationId = c.CorrelationId, ActorReference = r.ActorReference, OperationType = VerificationOperationType.AadhaarIdentifierValidation, ValidationStatus = result.Status, Method = VerificationMethod.Auto, SafeMessage = result.SafeMessage, SafeReasonCode = result.SafeReasonCode, MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(id) }; await repo.AppendRecordAsync(record, ct); return IdentityVerificationMapper.ToIdentifierResult(record); } }
    ```

    Preserve the existing identifier route names and compatible response fields by mapping these normalized records at the existing action boundary. Do not replace either identifier operation with a comparison call. _Requirements: 1.1-1.8, 2.1-2.8, 6.1-6.8, 9.1-9.5, 14.8._

  - [x] 8.8 CREATE `Finnova.Service/IdentityVerification/Commands/ManualOverrides/ManualOverrideCommands.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Correlation;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Commands.ManualOverrides;
    public sealed record SubmitManualComparisonCommand(ManualComparisonDecisionRequest Request, string ActorReference) : IRequest<ComparisonVerificationResult>;
    public sealed record SubmitManualPanCommand(ManualPanOverrideRequest Request, string ActorReference) : IRequest<IdentifierValidationResult>;
    public sealed record SubmitManualAadhaarCommand(ManualAadhaarOverrideRequest Request, string ActorReference) : IRequest<IdentifierValidationResult>;
    public sealed class SubmitManualComparisonHandler(IIdentityVerificationRepository repo, IRequestContextAccessor context) : IRequestHandler<SubmitManualComparisonCommand, ComparisonVerificationResult>
    { public async Task<ComparisonVerificationResult> Handle(SubmitManualComparisonCommand r, CancellationToken ct) { var c = context.Current; var source = await repo.GetRecordAsync(r.Request.SourceRecordId, ct) ?? throw new Finnova.Models.Domain.Exceptions.IdentityVerificationRecordNotFoundException(); if (source.ComparisonOutcome is null) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["SourceRecordId"]); var record = new Finnova.Models.Domain.Entities.ValidationRecord { SessionId = source.SessionId, SourceRecordId = source.Id, OperationType = r.Request.OperationType, ComparisonOutcome = r.Request.Outcome, Method = VerificationMethod.ManualOverride, ActorReference = r.ActorReference, CorrelationId = c.CorrelationId, SafeReasonCode = "ManualDecision", SafeMessage = "Manual comparison decision recorded.", MaskedSubjectReference = source.MaskedSubjectReference }; await repo.AppendRecordAsync(record, ct); return IdentityVerificationMapper.ToComparisonResult(record); } }
    ```

    Add separate manual PAN/Aadhaar handlers using the same append-only pattern, required identifier fields, protected-document store, `DocumentInputValidator`, and `SourceRecordId`. An automated `NonMatch` remains unchanged; a manual decision is a separate linked record. Manual decline creates no successful record. _Requirements: 7.1-7.18, 9.6-9.9, 11.1-11.6._

  - [x] 8.9 CREATE `Finnova.Service/IdentityVerification/Queries/VerificationHistoryQueries.cs`

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Repository.Interfaces;
    using MediatR;
    namespace Finnova.Service.IdentityVerification.Queries;
    public sealed record GetVerificationRecordsQuery(VerificationRecordQuery Query) : IRequest<PaginatedResponse<ValidationRecordResponse>>;
    public sealed class GetVerificationRecordsHandler(IIdentityVerificationRepository repo) : IRequestHandler<GetVerificationRecordsQuery, PaginatedResponse<ValidationRecordResponse>>
    {
        public async Task<PaginatedResponse<ValidationRecordResponse>> Handle(GetVerificationRecordsQuery request, CancellationToken ct)
        {
            var q = request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = request.Query.PageSize == 0 ? 20 : request.Query.PageSize };
            if (q.PageSize is < 1 or > 100) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["PageSize"]);
            var (items,total) = await repo.GetPagedAsync(q, ct);
            var data = items.Select(x => new ValidationRecordResponse(x.Id, x.SessionId, x.OperationType, x.ValidationStatus, x.ComparisonOutcome, x.Method, x.CreatedUtc, x.ActorReference, x.SafeReasonCode, x.SafeMessage, x.MaskedSubjectReference, x.ProtectedDocumentReferenceId is not null)).ToList();
            return new(data,total,q.Page,q.PageSize,(int)Math.Ceiling(total / (double)q.PageSize));
        }
    }
    ```

    Add a protected-document query that returns a stream only for an authorized manual record linked to a protected reference. Automated records and records without a document return the same safe not-found exception. _Requirements: 9.12-9.16, 10.1-10.18, 11.1-11.9, 13.14-13.15._

- [x] 9. Wire controllers, middleware, host, and gateway
  - [x] 9.1 CREATE `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs`

    ```csharp
    using System.Security.Claims;
    using System.Text.Json;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
    using Finnova.Service.IdentityVerification.Commands.VerifyPanName;
    using Finnova.Service.IdentityVerification.Workflow;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationController(IMediator mediator, IVerificationWorkflowCoordinator coordinator) : ControllerBase
    {
        [HttpPost("verify-pan-name"), Consumes("multipart/form-data")]
        public async Task<ActionResult<ComparisonVerificationResult>> VerifyPanName([FromForm] ComparisonForm form)
        { return Ok(await mediator.Send(new VerifyPanNameCommand(ToPanRequest(form), Actor()), HttpContext.RequestAborted)); }
        [HttpPost("verify-aadhar-address"), Consumes("multipart/form-data")]
        public async Task<ActionResult<ComparisonVerificationResult>> VerifyAadharAddress([FromForm] ComparisonForm form)
        { return Ok(await mediator.Send(new VerifyAadharAddressCommand(ToAadhaarRequest(form), Actor()), HttpContext.RequestAborted)); }
        [HttpPost("workflows/pan-and-name")] public Task<VerificationWorkflowResponse> PanAndName([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.PanAndName }, HttpContext.RequestAborted);
        [HttpPost("workflows/aadhaar-and-address")] public Task<VerificationWorkflowResponse> AadhaarAndAddress([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.AadhaarAndAddress }, HttpContext.RequestAborted);
        [HttpPost("workflows/full")] public Task<VerificationWorkflowResponse> Full([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.Full }, HttpContext.RequestAborted);
        private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system-admin";
        private static VerifyPanNameRequest ToPanRequest(ComparisonForm f) => new(f.SubmittedValue, f.ToSource());
        private static VerifyAadharAddressRequest ToAadhaarRequest(ComparisonForm f) => new(f.SubmittedValue, f.ToSource());
        public sealed class ComparisonForm
        {
            public string SubmittedValue { get; set; } = "";
            public IFormFile? DocumentFile { get; set; }
            public string? ExtractedValuesJson { get; set; }
            public string? MappingProfile { get; set; }
            public Guid? SessionId { get; set; }
            public ComparisonSourceContract ToSource()
            {
                var values = string.IsNullOrWhiteSpace(ExtractedValuesJson) ? null : JsonSerializer.Deserialize<Dictionary<string,string>>(ExtractedValuesJson);
                return new(DocumentFile?.ContentType, DocumentFile?.Length, null, values, MappingProfile, SessionId);
            }
        }
    }
    ```

    **Superseded multipart conversion:** the in-memory `ToSource` illustration in this block must not be pasted for document uploads. Apply task 9.17 instead; it uploads through the protected store before mediator/provider execution.

    These exact routes use multipart binding because a document file is optional and extracted values can be supplied as JSON text. If the confirmed local API contract requires JSON-only, add a separate `[HttpPost]` JSON action with the same route semantics and keep `IFormFile` out of Models. Do not fabricate provider wire fields. Existing PAN/Aadhaar identifier actions retain their current route and compatible response fields. _Requirements: 1.8, 2.8, 3.1-3.8, 4.1-4.8, 6.1-6.16, 11.1-11.6, 14.8-14.10._

  - [x] 9.2 CREATE `Finnova.SystemAdminService/Controllers/IdentityVerificationHistoryController.cs`

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Queries;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationHistoryController(IMediator mediator) : ControllerBase
    {
        [HttpGet("records")]
        public Task<PaginatedResponse<ValidationRecordResponse>> Records([FromQuery] VerificationRecordQuery q) => mediator.Send(new GetVerificationRecordsQuery(q), HttpContext.RequestAborted);
        [HttpGet("documents/{recordId:guid}")]
        public async Task<IActionResult> Document(Guid recordId, [FromServices] IIdentityVerificationDocumentQuery query)
        { var file = await query.GetAsync(recordId, HttpContext.RequestAborted); return File(file.Content, file.ContentType, file.FileName); }
    }
    public interface IIdentityVerificationDocumentQuery { Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct); }
    ```

    Do not add update/delete endpoints. Default history projections omit raw identifiers, submitted names/addresses, provider payloads, secrets, tokens, and document bytes. _Requirements: 9.12-9.16, 10.1-10.18, 11.1-11.9._

  - [x] 9.3 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`

    Add `using Finnova.Models.Domain.Exceptions;` with the existing using block if absent. Locate the existing path-family variables before `var (status, code, detail) = ex switch;` and add this exact block once:

    ```csharp
    var isIdentityVerification = ctx.Request.Path.StartsWithSegments("/api/identity-verification", StringComparison.OrdinalIgnoreCase);
    var validationIdentityCode = isIdentityVerification ? "ERR-IVF-400" : validationCustomerCode;
    var fallbackIdentityCode = isIdentityVerification ? "ERR-IVF-500" : fallbackCustomerCode;
    ```

    Add these cases before the existing shared `FluentValidation.ValidationException` and fallback cases. Keep every existing branch and code family unchanged:

    ```csharp
    IdentityVerificationDocumentSizeException => (StatusCodes.Status413PayloadTooLarge, ex.ErrorCode, ex.Message),
    IdentityVerificationDocumentFormatException => (StatusCodes.Status415UnsupportedMediaType, ex.ErrorCode, ex.Message),
    IdentityVerificationImmutableException => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
    IdentityVerificationRecordNotFoundException => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
    IdentityVerificationConfigurationException => (StatusCodes.Status503ServiceUnavailable, ex.ErrorCode, ex.Message),
    IdentityVerificationProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, ex.ErrorCode, ex.Message),
    IdentityVerificationPersistenceException => (StatusCodes.Status500InternalServerError, ex.ErrorCode, ex.Message),
    IdentityVerificationValidationException => (StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message),
    IdentityVerificationAuthorizationException => (StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message),
    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationIdentityCode, v.Message),
    _ => (StatusCodes.Status500InternalServerError, fallbackIdentityCode, "Unexpected error.")
    ```

    Immediately before the existing `WriteAsJsonAsync(problem)`, add `problem.Extensions["correlationId"] = ctx.TraceIdentifier; ctx.Response.Headers["X-Correlation-Id"] = ctx.TraceIdentifier;`. Never serialize exception inner details or raw provider payloads. _Requirements: 5.9-5.10, 7.6-7.10, 8.8-8.12, 9.14-9.16, 10.12, 11.1-11.6, 14.4-14.7._

  - [x] 9.4 MODIFY `Finnova.SystemAdminService/Program.cs`

    Locate the final using directives before `var builder = WebApplication.CreateBuilder(args);` and add these usings once:

    ```csharp
    using Finnova.Service.IdentityVerification;
    using Finnova.Service.IdentityVerification.Adapters;
    using Finnova.Service.IdentityVerification.Correlation;
    using Finnova.Service.IdentityVerification.External;
    using Finnova.Service.IdentityVerification.Storage;
    using Finnova.Service.IdentityVerification.Validation;
    using Finnova.Service.IdentityVerification.Workflow;
    ```

    Locate `builder.Services.AddFinnovaRepository(connectionString);` and add this snippet immediately after it once. Do not add another MediatR scan, JWT scheme, `SystemAdmin` policy, or repository registration.

    ```csharp
    builder.Services.AddHttpContextAccessor();
    builder.Services.Configure<IdentityVerificationOptions>(builder.Configuration.GetSection(IdentityVerificationOptions.SectionName));
    builder.Services.AddHttpClient<ConfigurableComparisonHttpClient>();
    builder.Services.AddScoped<IRequestContextAccessor, RequestContextAccessor>();
    builder.Services.AddScoped<IProviderCallExecutor, ProviderCallExecutor>();
    builder.Services.AddScoped<IVerificationWorkflowCoordinator, VerificationWorkflowCoordinator>();
    builder.Services.AddScoped<DocumentInputValidator>();
    builder.Services.AddIdentityVerificationStorage();
    // Add only provider adapters whose Swagger/OpenAPI contract was confirmed in task 7.2.
    ```

    Register separate PAN and Aadhaar identifier adapters and separate PAN-name and Aadhaar-address comparison adapters/resolver. The existing JWT and `SystemAdmin` policy remain the only auth mechanism. _Requirements: 8.1-8.12, 11.1-11.9, 12.11-12.12._

  - [x] 9.5 MODIFY `Finnova.ApiGateway/appsettings.json`

    The existing `systemadmin-route` already matches `/api/systemadmin/{**catch-all}`, removes `/api/systemadmin`, and forwards to `systemadmin-cluster`. Do not add a provider cluster. If the UI base URL is `/api/ua/api`, add both routes inside `ReverseProxy.Routes`, alongside the existing lookup aliases:

    ```json
    "ua-identity-verification-alias-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": { "Path": "/api/ua/api/identity-verification/{**catch-all}" },
      "Transforms": [ { "PathRemovePrefix": "/api/ua/api" }, { "PathPrefix": "/api" } ]
    },
    "ua-identity-verification-alias-root": {
      "ClusterId": "systemadmin-cluster",
      "Match": { "Path": "/api/ua/api/identity-verification" },
      "Transforms": [ { "PathRemovePrefix": "/api/ua/api" }, { "PathPrefix": "/api" } ]
    }
    ```

    **Route-prefix assumption:** add the alias only when `VITE_API_BASE_URL` resolves to `/api/ua/api`; direct `/api/systemadmin/identity-verification/**` uses the existing route. Never expose provider hosts. _Requirements: 11.7-11.9, 14.8-14.10._

  - [x] 9.6 MODIFY `Finnova.SystemAdminService/appsettings.json` and/or deployment configuration

    Add only the configured section below, with provider-specific values supplied after Swagger and secret-store confirmation. Do not commit secrets, tokens, or unconfirmed field names. This snippet is configuration shape, not a provider schema.

    ```json
    "IdentityVerification": {
      "MaxDocumentBytes": 5242880,
      "AcceptedContentTypes": [ "image/jpeg", "image/png", "application/pdf" ],
      "Retry": { "MaxAttempts": 3, "BaseDelayMilliseconds": 200, "TimeoutSeconds": 10 },
      "PanIdentifierValidation": { "Enabled": false, "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Json", "IdentifierFieldName": "" },
      "AadhaarIdentifierValidation": { "Enabled": false, "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Json", "IdentifierFieldName": "" },
      "PanNameVerification": { "Enabled": false, "AdapterName": "", "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Multipart", "InputMapping": { "RequiresDocumentFile": true, "RequiresSubmittedValue": true, "RequiredExtractedValueKeys": [], "AllowsDocumentAndExtractedValues": true, "MappingProfile": "" } },
      "AadhaarAddressVerification": { "Enabled": false, "AdapterName": "", "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Multipart", "InputMapping": { "RequiresDocumentFile": true, "RequiresSubmittedValue": true, "RequiredExtractedValueKeys": [], "AllowsDocumentAndExtractedValues": true, "MappingProfile": "" } }
    }
    ```

    Use deployment secrets for API credentials and protected storage. If required configuration is absent, fail closed before a provider call or document persistence. _Requirements: 5.1-5.11, 8.1-8.12, 12.7, 12.11-12.12._

  - [x] 9.10 MODIFY `Finnova.Service/IdentityVerification/Queries/ProtectedDocumentQuery.cs` and `Finnova.SystemAdminService/Controllers/IdentityVerificationHistoryController.cs`

    Move `IIdentityVerificationDocumentQuery` out of the host controller namespace. Add this interface at the top of the service query file, add `using Finnova.Service.IdentityVerification.Queries;` to the controller, and remove the duplicate interface declaration from the controller file.

    ```csharp
    namespace Finnova.Service.IdentityVerification.Queries;
    public interface IIdentityVerificationDocumentQuery
    { Task<Finnova.Models.Contracts.IdentityVerification.ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct); }
    ```

    Register it once in `Program.cs` with `builder.Services.AddScoped<IIdentityVerificationDocumentQuery, ProtectedDocumentQuery>();`. This keeps the service layer independent of `Finnova.SystemAdminService`. _Requirements: 10.17-10.18, 11.1-11.6._

- [x] 9.11 MODIFY `Finnova.Service/Finnova.Service.csproj`

    The repository already uses the compatible Azure SDK in `Finnova.Service`. Keep exactly one package reference inside the existing first `<ItemGroup>`; do not add Azure client packages or a second version:

    ```xml
    <PackageReference Include="Azure.Storage.Blobs" Version="12.29.2" />
    ```

    This is a no-op when the line is already present, as it is in the current repository. Do not add the unrelated generic `IBlobStorageService` to the identity-verification path. _Requirements: 7.3-7.5, 12.9-12.12._

  - [x] 9.12 MODIFY `Finnova.Service/IdentityVerification/External/IdentityVerificationOptions.cs`

    Replace the complete file created by task 3.2 with this version. It preserves the existing provider-neutral options and adds a disabled-by-default, bounded protected-storage section. The 5 MB ceiling is intentional and must not be raised by configuration.

    ```csharp
    namespace Finnova.Service.IdentityVerification.External;
    public sealed class IdentityVerificationOptions
    {
        public const string SectionName = "IdentityVerification";
        public long MaxDocumentBytes { get; set; } = 5 * 1024 * 1024;
        public string[] AcceptedContentTypes { get; set; } = ["image/jpeg", "image/png", "application/pdf"];
        public RetryOptions Retry { get; set; } = new();
        public ProtectedBlobStorageOptions BlobStorage { get; set; } = new();
        public ProviderOperationOptions PanIdentifierValidation { get; set; } = new();
        public ProviderOperationOptions AadhaarIdentifierValidation { get; set; } = new();
        public ComparisonProviderOptions PanNameVerification { get; set; } = new();
        public ComparisonProviderOptions AadhaarAddressVerification { get; set; } = new();
    }
    public sealed class ProtectedBlobStorageOptions
    {
        public bool Enabled { get; set; }
        public string ConnectionStringName { get; set; } = "IdentityVerificationBlobStorage";
        public string ContainerName { get; set; } = "identity-verification-documents";
        public string Prefix { get; set; } = "identity-verification";
        public long MaxDocumentBytes { get; set; } = 5 * 1024 * 1024;
        public string[] AcceptedContentTypes { get; set; } = ["image/jpeg", "image/png", "application/pdf"];
        public bool CreateContainerIfMissing { get; set; }
        public bool AllowOverwrite { get; set; }
        public int UploadTimeoutSeconds { get; set; } = 60;
        public int DownloadTimeoutSeconds { get; set; } = 60;
        public int DeleteTimeoutSeconds { get; set; } = 30;
    }
    public sealed class RetryOptions { public int MaxAttempts { get; set; } = 3; public int BaseDelayMilliseconds { get; set; } = 200; public int TimeoutSeconds { get; set; } = 10; }
    public class ProviderOperationOptions { public bool Enabled { get; set; } public string BaseAddress { get; set; } = ""; public string Path { get; set; } = ""; public string HttpMethod { get; set; } = "POST"; public string RequestMode { get; set; } = "Json"; public string IdentifierFieldName { get; set; } = ""; }
    public sealed class ComparisonProviderOptions : ProviderOperationOptions
    {
        public string AdapterName { get; set; } = "";
        public ComparisonInputMapping InputMapping { get; set; } = new();
        public string? SubmittedValueFieldName { get; set; }
        public string? DocumentFieldName { get; set; }
        public Dictionary<string, string> ExtractedValueFieldNames { get; set; } = new();
        public string? ResponseOutcomePath { get; set; }
        public string? ResponseMessagePath { get; set; }
    }
    public sealed class ComparisonInputMapping
    {
        public bool RequiresDocumentFile { get; set; }
        public bool RequiresSubmittedValue { get; set; } = true;
        public List<string> RequiredExtractedValueKeys { get; set; } = [];
        public bool AllowsDocumentAndExtractedValues { get; set; } = true;
        public string MappingProfile { get; set; } = "";
    }
    ```

    _Requirements: 7.6-7.10, 12.7, 12.9-12.12._

  - [x] 9.13 MODIFY `Finnova.Models/Contracts/IdentityVerification/IdentityVerificationContracts.cs`

    Replace the three byte-buffering records from task 3.1 with these exact records. A request carries only metadata and the opaque database reference after upload; it never carries document bytes into Models, MediatR, logs, or public responses.

    ```csharp
    public sealed record ComparisonSourceContract(string? DocumentContentType, long? DocumentSizeBytes, Guid? ProtectedDocumentReferenceId, IReadOnlyDictionary<string, string>? ExtractedValues, string? MappingProfile, Guid? SessionId = null);
    public sealed record ManualPanOverrideRequest(string Pan, string CardholderName, DateOnly DateOfBirth, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
    public sealed record ManualAadhaarOverrideRequest(string AadhaarNumber, string CardholderName, string Address, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
    ```

    Keep all other public records unchanged except for their use of the revised source record. Do not add a filename, public URL, SAS token, connection string, PAN, Aadhaar, name, or address to a storage reference. _Requirements: 3.1-3.8, 4.1-4.8, 7.3-7.5, 9.2-9.3, 10.17-10.18, 14.9-14.10._

  - [x] 9.14 CREATE `Finnova.Service/IdentityVerification/Storage/AzureBlobProtectedDocumentStore.cs`

    ```csharp
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
    ```

    The provider generates `prefix/{Guid:N}.bin` only, never uses the uploaded filename or identity values, sets private/no-store headers and non-sensitive metadata, refuses overwrite, validates key shape against traversal, hashes the bounded stream with SHA-256, returns metadata only, returns a fresh read stream, and maps provider failures to the existing safe typed exception. It does not return a URI or SAS. _Requirements: 7.3-7.10, 7.16-7.18, 9.2-9.3, 10.17-10.18, 12.9-12.12, 13.14-13.15._

  - [x] 9.15 MODIFY `Finnova.Service/IdentityVerification/Storage/ProtectedDocumentStoreRegistration.cs` and `Finnova.SystemAdminService/Program.cs`

    Replace the complete registration file from task 5.5 with this fail-closed registration. It uses the repository's existing direct `BlobServiceClient` convention because `Finnova.Service.csproj` does not reference `Microsoft.Extensions.Azure`; `AddAzureClients` is therefore not used. A missing connection-string reference or invalid enabled configuration throws the existing typed configuration exception during startup, while disabled configuration resolves the existing throwing implementation.

    ```csharp
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
    ```

    In `Finnova.SystemAdminService/Program.cs`, replace the existing task 9.4 line with this exact line, once:

    ```csharp
    builder.Services.AddIdentityVerificationStorage(builder.Configuration);
    ```

    Keep `IProtectedDocumentStore` provider-neutral and do not register both the throwing and Azure implementations when enabled. _Requirements: 12.11-12.12, 13.14-13.15._

  - [x] 9.16 CREATE `Finnova.Service/IdentityVerification/Storage/ProtectedDocumentLifecycle.cs` and MODIFY the comparison handlers

    ```csharp
    using Finnova.Models.Domain.Entities;
    namespace Finnova.Service.IdentityVerification.Storage;
    public sealed class ProtectedDocumentLifecycle(IProtectedDocumentStore store)
    {
        public async Task<TResult> UploadAndExecuteAsync<TResult>(Stream content, string contentType, long sizeBytes, Func<ProtectedDocumentReference, Task<TResult>> action, CancellationToken ct)
        {
            var reference = await store.PutAsync(content, contentType, sizeBytes, ct);
            try { return await action(reference); }
            catch { try { await store.DeleteAsync(reference.StorageKey, CancellationToken.None); } catch { } throw; }
        }
    }
    ```

    Replace the `DocumentBytes` handling in task 8.5. Add `IProtectedDocumentStore store` and resolve `ProtectedDocumentReferenceId` through this repository method before provider invocation:

    ```csharp
    Task<ProtectedDocumentReference?> GetProtectedDocumentReferenceAsync(Guid id, CancellationToken ct = default);
    ```

    ```csharp
    public Task<ProtectedDocumentReference?> GetProtectedDocumentReferenceAsync(Guid id, CancellationToken ct = default)
        => Context.ProtectedDocumentReferences.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    ```

    Each handler must validate the submitted text and source mapping, open a fresh stream with `OpenReadAsync(reference.StorageKey, ct)`, pass only `reference.StorageKey` and that controlled stream to `ProviderNeutralComparisonInput`, dispose the stream, append exactly one record containing `ProtectedDocumentReferenceId`, and return the normalized result. The multipart controller's lifecycle task cleans up on provider/database failure. No handler may construct a `MemoryStream` from request bytes, call an adapter with a public URL, or log document metadata. _Requirements: 3.5-3.8, 4.5-4.8, 5.1-5.11, 7.3-7.5, 9.1-9.3, 9.12-9.16, 10.17-10.18, 14.9-14.10._

  - [x] 9.17 MODIFY `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs` and `IdentityVerificationManualController.cs`

    Replace tasks 9.1 and 9.9 with bounded, stream-only controller code. The header read is capped at 16 bytes, the validator checks the 5 MB limit, JPG/PNG/PDF MIME, extension, and signature before upload, and a fresh request stream is passed to Azure. The request contract receives only the protected reference ID.

    ```csharp
    using System.Security.Claims;
    using System.Text.Json;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
    using Finnova.Service.IdentityVerification.Commands.VerifyPanName;
    using Finnova.Service.IdentityVerification.Storage;
    using Finnova.Service.IdentityVerification.Validation;
    using Finnova.Service.IdentityVerification.Workflow;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationController(IMediator mediator, DocumentInputValidator validator, ProtectedDocumentLifecycle lifecycle, IVerificationWorkflowCoordinator coordinator) : ControllerBase
    {
        [HttpPost("verify-pan-name"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyPanName([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyPanNameCommand(new(form.SubmittedValue, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
        [HttpPost("verify-aadhar-address"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyAadharAddress([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyAadharAddressCommand(new(form.SubmittedValue, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
        [HttpPost("workflows/pan-and-name")] public Task<VerificationWorkflowResponse> PanAndName([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.PanAndName }, HttpContext.RequestAborted);
        [HttpPost("workflows/aadhaar-and-address")] public Task<VerificationWorkflowResponse> AadhaarAndAddress([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.AadhaarAndAddress }, HttpContext.RequestAborted);
        [HttpPost("workflows/full")] public Task<VerificationWorkflowResponse> Full([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.Full }, HttpContext.RequestAborted);
        private async Task<ActionResult<ComparisonVerificationResult>> UploadAndSend(ComparisonForm form, Func<Guid?, Task<ComparisonVerificationResult>> send)
        {
            if (form.DocumentFile is null) return Ok(await send(null)); var file = form.DocumentFile; var header = await ReadHeaderAsync(file, HttpContext.RequestAborted); validator.Validate(file.FileName, file.ContentType, file.Length, header); await using var input = file.OpenReadStream(); var result = await lifecycle.UploadAndExecuteAsync(input, file.ContentType, file.Length, reference => send(reference.Id), HttpContext.RequestAborted); return Ok(result);
        }
        private async Task<byte[]> ReadHeaderAsync(IFormFile file, CancellationToken ct)
        {
            if (file.Length <= 0 || file.Length > 5 * 1024 * 1024) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationDocumentSizeException(); await using var input = file.OpenReadStream(); var buffer = new byte[16]; var offset = 0; while (offset < buffer.Length) { var read = await input.ReadAsync(buffer.AsMemory(offset), ct); if (read == 0) break; offset += read; } return buffer[..offset];
        }
        private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system-admin";
        public sealed class ComparisonForm { public string SubmittedValue { get; set; } = ""; public IFormFile? DocumentFile { get; set; } public string? ExtractedValuesJson { get; set; } public string? MappingProfile { get; set; } public Guid? SessionId { get; set; } public ComparisonSourceContract ToSource(Guid? id) { var values = string.IsNullOrWhiteSpace(ExtractedValuesJson) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(ExtractedValuesJson); return new(DocumentFile?.ContentType, DocumentFile?.Length, id, values, MappingProfile, SessionId); } }
    }
    ```

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.ManualOverrides;
    using Finnova.Service.IdentityVerification.Storage;
    using Finnova.Service.IdentityVerification.Validation;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification/manual"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationManualController(IMediator mediator, DocumentInputValidator validator, ProtectedDocumentLifecycle lifecycle) : ControllerBase
    {
        [HttpPost("comparison")] public Task<ComparisonVerificationResult> Comparison([FromBody] ManualComparisonDecisionRequest request) => mediator.Send(new SubmitManualComparisonCommand(request, User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted);
        [HttpPost("pan"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Pan([FromForm] ManualPanForm form) => Upload(form.DocumentFile, (id, size, type) => mediator.Send(new SubmitManualPanCommand(new(form.Pan, form.CardholderName, form.DateOfBirth, id, type, size), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted));
        [HttpPost("aadhaar"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Aadhaar([FromForm] ManualAadhaarForm form) => Upload(form.DocumentFile, (id, size, type) => mediator.Send(new SubmitManualAadhaarCommand(new(form.AadhaarNumber, form.CardholderName, form.Address, id, type, size), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted));
        private async Task<T> Upload<T>(IFormFile? file, Func<Guid, long, string, Task<T>> send)
        {
            if (file is null) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["DocumentFile"]); if (file.Length <= 0 || file.Length > 5 * 1024 * 1024) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationDocumentSizeException(); await using (var headerStream = file.OpenReadStream()) { var header = new byte[16]; var read = 0; while (read < header.Length) { var n = await headerStream.ReadAsync(header.AsMemory(read), HttpContext.RequestAborted); if (n == 0) break; read += n; } validator.Validate(file.FileName, file.ContentType, file.Length, header[..read]); } await using var input = file.OpenReadStream(); return await lifecycle.UploadAndExecuteAsync(input, file.ContentType, file.Length, reference => send(reference.Id, reference.SizeBytes, reference.ContentType), HttpContext.RequestAborted);
        }
        public sealed class ManualPanForm { public string Pan { get; set; } = ""; public string CardholderName { get; set; } = ""; public DateOnly DateOfBirth { get; set; } public IFormFile? DocumentFile { get; set; } }
        public sealed class ManualAadhaarForm { public string AadhaarNumber { get; set; } = ""; public string CardholderName { get; set; } = ""; public string Address { get; set; } = ""; public IFormFile? DocumentFile { get; set; } }
    }
    ```

    Replace the byte-array command construction in task 8.8 with the revised reference fields and set `ProtectedDocumentReferenceId` on manual records. Do not call external providers from manual actions. _Requirements: 7.1-7.18, 9.2-9.9, 11.1-11.6, 12.9-12.12, 14.4-14.7._

  - [x] 9.18 MODIFY `Finnova.Service/IdentityVerification/Queries/ProtectedDocumentQuery.cs` and `Finnova.SystemAdminService/Controllers/IdentityVerificationHistoryController.cs`

    Replace the query/controller with this exact retrieval boundary. Authorization and the manual-record check happen before the protected store is opened. The only filename exposed is a fixed safe name selected from the validated stored content type; no storage key, URL, SAS, metadata, or document bytes enter JSON.

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Storage;
    namespace Finnova.Service.IdentityVerification.Queries;
    public interface IIdentityVerificationDocumentQuery { Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct); }
    public sealed class ProtectedDocumentQuery(IIdentityVerificationRepository repository, IProtectedDocumentStore store) : IIdentityVerificationDocumentQuery
    {
        public async Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct)
        {
            var record = await repository.GetRecordAsync(recordId, ct); if (record is null || record.Method != VerificationMethod.ManualOverride || record.ProtectedDocumentReference is null) throw new IdentityVerificationRecordNotFoundException(); var reference = record.ProtectedDocumentReference; var stream = await store.OpenReadAsync(reference.StorageKey, ct) ?? throw new IdentityVerificationRecordNotFoundException(); return new(stream, reference.ContentType, SafeName(reference.ContentType));
        }
        private static string SafeName(string contentType) => contentType.ToLowerInvariant() switch { "image/jpeg" => "identity-document.jpg", "image/png" => "identity-document.png", "application/pdf" => "identity-document.pdf", _ => throw new IdentityVerificationRecordNotFoundException() };
    }
    ```

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Queries;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationHistoryController(IMediator mediator, IIdentityVerificationDocumentQuery documents) : ControllerBase
    {
        [HttpGet("records")] public Task<PaginatedResponse<ValidationRecordResponse>> Records([FromQuery] VerificationRecordQuery query) => mediator.Send(new GetVerificationRecordsQuery(query), HttpContext.RequestAborted);
        [HttpGet("documents/{recordId:guid}")] public async Task<IActionResult> Document(Guid recordId) { var file = await documents.GetAsync(recordId, HttpContext.RequestAborted); return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: false); }
    }
    ```

    Keep the existing task 9.10 service-interface move and register `IIdentityVerificationDocumentQuery` once. _Requirements: 9.12-9.16, 10.1-10.18, 11.1-11.9, 13.14-13.15._

  - [x] 9.19 CREATE `Finnova.Service/IdentityVerification/Adapters/ProviderDocumentAccess.cs`

    Keep the normal adapter contract stream-based and provider-neutral:

    ```csharp
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Storage;
    namespace Finnova.Service.IdentityVerification.Adapters;
    public interface IProviderDocumentAccess { Task<Stream> OpenReadAsync(Guid protectedDocumentReferenceId, CancellationToken ct); }
    public sealed class ProviderDocumentAccess(IIdentityVerificationRepository repository, IProtectedDocumentStore store) : IProviderDocumentAccess
    {
        public async Task<Stream> OpenReadAsync(Guid protectedDocumentReferenceId, CancellationToken ct) { var reference = await repository.GetProtectedDocumentReferenceAsync(protectedDocumentReferenceId, ct) ?? throw new IdentityVerificationRecordNotFoundException(); return await store.OpenReadAsync(reference.StorageKey, ct) ?? throw new IdentityVerificationRecordNotFoundException(); }
    }
    ```

    Do not add a URL or SAS field to any public contract. Only if the confirmed external provider refuses streams, add a separate adapter-only URL issuer using a read-only SAS scoped to this one blob and expiring in at most five minutes, with the Azure SDK's user-delegation or approved managed-identity credential. Return that URI only to the adapter, never to a controller, UI service, response DTO, log, or exception. If no provider contract requires a URL, do not implement the issuer. _Requirements: 5.7-5.10, 7.3-7.5, 8.9-8.11, 11.7-11.9, 13.14-13.15._

- [x] 9.20 MODIFY `Finnova.SystemAdminService/appsettings.json` and deployment secret configuration

    Add this safe shape under the existing root object. Keep `Enabled` false until the deployment secret and container have been provisioned. Never put an Azure connection string, SAS token, account key, or provider credential in source-controlled JSON.

    ```json
    "IdentityVerification": {
      "MaxDocumentBytes": 5242880,
      "AcceptedContentTypes": [ "image/jpeg", "image/png", "application/pdf" ],
      "BlobStorage": {
        "Enabled": false,
        "ConnectionStringName": "IdentityVerificationBlobStorage",
        "ContainerName": "identity-verification-documents",
        "Prefix": "identity-verification",
        "MaxDocumentBytes": 5242880,
        "AcceptedContentTypes": [ "image/jpeg", "image/png", "application/pdf" ],
        "CreateContainerIfMissing": false,
        "AllowOverwrite": false,
        "UploadTimeoutSeconds": 60,
        "DownloadTimeoutSeconds": 60,
        "DeleteTimeoutSeconds": 30
      },
      "PanIdentifierValidation": { "Enabled": false, "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Json", "IdentifierFieldName": "" },
      "AadhaarIdentifierValidation": { "Enabled": false, "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Json", "IdentifierFieldName": "" },
      "PanNameVerification": { "Enabled": false, "AdapterName": "", "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Multipart", "InputMapping": { "RequiresDocumentFile": true, "RequiresSubmittedValue": true, "RequiredExtractedValueKeys": [], "AllowsDocumentAndExtractedValues": true, "MappingProfile": "" } },
      "AadhaarAddressVerification": { "Enabled": false, "AdapterName": "", "BaseAddress": "", "Path": "", "HttpMethod": "POST", "RequestMode": "Multipart", "InputMapping": { "RequiresDocumentFile": true, "RequiresSubmittedValue": true, "RequiredExtractedValueKeys": [], "AllowsDocumentAndExtractedValues": true, "MappingProfile": "" } }
    }
    ```

    Supply the secret at runtime through the host environment or approved secret manager. For local PowerShell development, use a placeholder only and do not commit the session value:

    ```powershell
    $env:ConnectionStrings__IdentityVerificationBlobStorage = '<provided-by-local-secret-store>'
    ```

    Enable the feature only in the deployment configuration after the secret exists:

    ```text
    IdentityVerification__BlobStorage__Enabled=true
    ConnectionStrings__IdentityVerificationBlobStorage=<secret-manager-reference-or-runtime-secret>
    ```

    The registration must fail closed when the enabled section, connection-string reference, container name, prefix, accepted types, or size/timeouts are invalid. The UI continues to call `/api/identity-verification/documents/{recordId}` through the gateway and never receives or accesses Azure directly. _Requirements: 5.1-5.11, 7.6-7.10, 8.1-8.12, 12.7, 12.11-12.12, 13.14-13.15._

- [x] 10. Add backend automated tests
  - [x]* 10.1 CREATE `Finnova.Tests/Unit/IdentityVerificationProperties.cs`

    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Xunit;
    namespace Finnova.Tests.Unit;
    public sealed class IdentityVerificationProperties
    {
        [Property(MaxTest = 100)] public Property IdentifierAndComparisonDomainsAreExclusive(NonEmptyString _)
        {
            var a = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ValidationStatus = ValidationStatus.Success };
            var b = new ValidationRecord { OperationType = VerificationOperationType.PanNameVerification, ComparisonOutcome = ComparisonOutcome.Match };
            IdentityVerificationInvariants.EnsureResultDomain(a); IdentityVerificationInvariants.EnsureResultDomain(b);
            return (a.ComparisonOutcome is null && b.ValidationStatus is null).ToProperty();
        }
        [Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
        {
            var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
            return Prop.Throws<InvalidOperationException, bool>(() => { IdentityVerificationInvariants.EnsureResultDomain(invalid); return true; });
        }
    }
    ```

    Add separate FsCheck.Xunit properties, each with `MaxTest = 100` or more, for: Property 2 zero provider calls and no rejected-byte persistence; Property 3 independent PAN/Aadhaar results; Property 4 provider-neutral input only; Property 5 retry/timeout classification; Property 6 independent workflow records; Property 7 append-not-overwrite manual decisions; Property 8 no sensitive values in logs/errors; Property 9 update/delete rejection; Property 10 deterministic filters/order; Property 11 manual decline creates no success; Property 12 UI projection preserves safe independent rows. Use spies/mocks, fake clocks, in-memory streams, and no real provider calls. _Requirements: design Correctness Properties 1-12._

  - [x]* 10.2 CREATE `Finnova.Tests/Unit/IdentityVerificationExamples.cs`

    ```csharp
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.IdentityVerification;
    using Xunit;
    namespace Finnova.Tests.Unit;
    public sealed class IdentityVerificationExamples
    {
        [Theory, InlineData("ABCDE1234F"), InlineData("abcde1234f")]
        public void PanIsUppercaseNormalized(string input) => Assert.Equal("ABCDE1234F", IdentityVerificationRules.NormalizePan(input));
        [Theory, InlineData("ABCDE123"), InlineData("ABCDE12345")]
        public void InvalidPanIsRejected(string input) => Assert.Throws<IdentityVerificationValidationException>(() => IdentityVerificationRules.NormalizePan(input));
        [Fact] public void AadhaarRequiresTwelveDigits() => Assert.Throws<IdentityVerificationValidationException>(() => IdentityVerificationRules.NormalizeAadhaar("1234"));
        [Fact] public void FiveMbIsTheMaximum() => Assert.True(5 * 1024 * 1024 > 0);
    }
    ```

    Add xUnit facts/theories with Moq or EF Core InMemory for `Match`, `NonMatch`, `NoUsableMatch`, `Failure`, `Unavailable`; missing document/extracted input; malformed provider response; no-provider-call validation; 5 MB boundary; unsupported extension/MIME/signature; partial failures; manual decline; persistence failure; safe diagnostic reference; default pagination; empty beyond-last page; protected-document not found; privacy-safe messages; and backwards-compatible existing identifier fields. _Requirements: 1.1-1.8, 2.1-2.8, 3.1-3.8, 4.1-4.8, 5.3-5.10, 6.7-6.14, 7.6-7.15, 8.1-8.12, 9.14-9.16, 10.9-10.18, 14.1-14.10._

  - [x]* 10.3 CREATE `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`

    ```csharp
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using Xunit;
    namespace Finnova.Tests.Integration;
    public sealed class IdentityVerificationApiTests : IClassFixture<SystemAdminAppFactory>
    {
        private readonly HttpClient client;
        public IdentityVerificationApiTests(SystemAdminAppFactory factory) { client = factory.CreateClient(); }
        [Fact] public async Task MissingTokenReturns401AndDoesNotCallProvider()
        { using var r = await client.PostAsJsonAsync("/api/identity-verification/verify-pan-name", new { submittedValue = "Anita Sharma" }); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); }
        [Fact] public async Task ComparisonRouteIsProtected()
        { client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.Create("SystemAdmin")); using var r = await client.PostAsJsonAsync("/api/identity-verification/verify-aadhar-address", new { submittedValue = "Mumbai, Maharashtra" }); Assert.NotEqual(HttpStatusCode.NotFound, r.StatusCode); }
    }
    ```

    Extend this file using the actual existing `SystemAdminAppFactory`/`DevJwt` conventions. Replace external adapters and persistence with test registrations. Cover invalid non-SystemAdmin -> 403, SystemAdmin single comparison calls, PAN+name, Aadhaar+address, full workflow partial success, all four result domains, immutable history, query filters, manual protected-document retrieval, middleware error codes/correlation ID, gateway route/alias, bearer forwarding, and existing PAN/Aadhaar identifier endpoint compatibility. _Requirements: 1.8, 2.8, 6.1-6.16, 9.6-9.16, 10.1-10.18, 11.1-11.9, 14.8._

  - [x] 10.4 MODIFY `Finnova.Tests/Finnova.Tests.csproj`

    Confirm these existing packages remain; add no duplicate package references. If absent, add only the shown references inside the existing `<ItemGroup>`:

    ```xml
    <PackageReference Include="FsCheck.Xunit" Version="2.16.6" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
    ```

    _Requirements: design Testing Strategy 14.1-14.4._

  - [x]* 10.5 CREATE `Finnova.Tests/Unit/ProtectedDocumentStoreTests.cs`

    ```csharp
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
    ```

    These xUnit tests use an in-memory fake blob client, not Azure credentials. Extend the same file with boundary tests for all three accepted MIME/signature combinations, declared-size mismatch, conditional non-overwrite, fresh download streams, provider transport failure mapping, and cancellation during read/upload. Do not use real connection strings or log bytes. _Requirements: 7.3-7.10, 7.16-7.18, 9.2-9.3, 12.9-12.12._

  - [x]* 10.6 CREATE `Finnova.Tests/Integration/ProtectedDocumentTransactionTests.cs`

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Service.IdentityVerification.Storage;
    using Xunit;
    namespace Finnova.Tests.Integration;
    public sealed class ProtectedDocumentTransactionTests
    {
        [Fact] public async Task ProviderFailureLeavesNoOrphanedBlob()
        { var fake = new TrackingStore(); var bytes = "%PDF-provider"u8.ToArray(); await Assert.ThrowsAsync<InvalidOperationException>(() => new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(new MemoryStream(bytes), "application/pdf", bytes.Length, _ => throw new InvalidOperationException("provider failed"), CancellationToken.None)); Assert.Empty(fake.Keys); }
        [Fact] public async Task DatabaseFailureLeavesNoOrphanedBlob()
        { var fake = new TrackingStore(); var bytes = "%PDF-database"u8.ToArray(); await Assert.ThrowsAsync<InvalidOperationException>(() => new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(new MemoryStream(bytes), "application/pdf", bytes.Length, _ => throw new InvalidOperationException("database failed"), CancellationToken.None)); Assert.Empty(fake.Keys); }
        [Fact] public async Task SuccessfulPersistenceLeavesExactlyOneReferenceAndBlob()
        { var fake = new TrackingStore(); var references = new List<Guid>(); var bytes = "%PDF-success"u8.ToArray(); var result = await new ProtectedDocumentLifecycle(fake).UploadAndExecuteAsync(new MemoryStream(bytes), "application/pdf", bytes.Length, reference => { references.Add(reference.Id); return Task.FromResult(reference.Id); }, CancellationToken.None); Assert.Single(references); Assert.Equal(references[0], result); Assert.Single(fake.Keys); }
        private sealed class TrackingStore : IProtectedDocumentStore
        {
            private readonly Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal); public IReadOnlyCollection<string> Keys => blobs.Keys;
            public async Task<ProtectedDocumentReference> PutAsync(Stream content, string contentType, long sizeBytes, CancellationToken ct = default) { using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); var reference = new ProtectedDocumentReference { StorageKey = $"identity-verification/{Guid.NewGuid():N}.bin", ContentType = contentType, SizeBytes = sizeBytes, ContentHash = "test" }; blobs.Add(reference.StorageKey, copy.ToArray()); return reference; }
            public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default) => Task.FromResult<Stream?>(blobs.TryGetValue(storageKey, out var value) ? new MemoryStream(value, false) : null);
            public Task DeleteAsync(string storageKey, CancellationToken ct = default) { blobs.Remove(storageKey); return Task.CompletedTask; }
        }
    }
    ```

    Extend the host integration coverage to register `TrackingStore` and a failing fake provider/repository through the existing `WebApplicationFactory<Program>` service replacement convention. Assert that a provider failure and `SaveChangesAsync` failure leave zero blobs, while a successful multipart comparison leaves one database `ProtectedDocumentReference` and one matching blob. Keep authorization, the two exact comparison routes, and manual retrieval checks. _Requirements: 3.1-3.8, 4.1-4.8, 7.3-7.18, 9.2-9.16, 10.17-10.18, 11.1-11.9._

- [x] 11. Add frontend models and service implementations
  - [x] 11.1 CREATE `d:\Projects\Finnova\Finnova-UI\src\models\identityVerification.model.ts`

    ```typescript
    export type VerificationOperationType = 'PanIdentifierValidation' | 'AadhaarIdentifierValidation' | 'PanNameVerification' | 'AadhaarAddressVerification';
    export type ValidationStatus = 'Success' | 'Failure' | 'Unavailable';
    export type ComparisonOutcome = 'Match' | 'NonMatch' | 'NoUsableMatch';
    export type VerificationMethod = 'Auto' | 'ManualOverride';
    export type VerificationWorkflowType = 'PanAndName' | 'AadhaarAndAddress' | 'Full';
    export type WorkflowInputMode = 'Partial' | 'Atomic';
    export interface ComparisonSource { document?: File; extractedValues?: Record<string,string>; mappingProfile?: string; sessionId?: string; }
    export interface VerifyPanNameRequest { submittedName: string; source: ComparisonSource; }
    export interface VerifyAadharAddressRequest { submittedAddress: string; source: ComparisonSource; }
    export interface IdentifierResult { operationType: VerificationOperationType; validationStatus: ValidationStatus; recordId?: string; sessionId?: string; correlationId: string; timestampUtc: string; safeMessage: string; manualOverrideAvailable: boolean; maskedSubjectReference?: string; }
    export interface ComparisonResult { operationType: VerificationOperationType; comparisonOutcome: ComparisonOutcome; recordId?: string; sessionId?: string; correlationId: string; timestampUtc: string; safeMessage: string; manualOverrideAvailable: boolean; maskedSubjectReference?: string; }
    export interface WorkflowRequest { workflowType: VerificationWorkflowType; inputMode: WorkflowInputMode; pan?: string; panSubmittedName?: string; panSource?: ComparisonSource; aadhaarNumber?: string; aadhaarSubmittedAddress?: string; aadhaarSource?: ComparisonSource; }
    export interface WorkflowResponse { sessionId: string; correlationId: string; timestampUtc: string; panIdentifierResult?: IdentifierResult; panNameResult?: ComparisonResult; aadhaarIdentifierResult?: IdentifierResult; aadhaarAddressResult?: ComparisonResult; partialFailures: Array<{ operationType: VerificationOperationType; code: string; safeMessage: string }>; }
    export interface VerificationRecord { id: string; sessionId?: string; operationType: VerificationOperationType; validationStatus?: ValidationStatus; comparisonOutcome?: ComparisonOutcome; method: VerificationMethod; createdUtc: string; actorReference: string; safeReasonCode?: string; safeMessage?: string; maskedSubjectReference?: string; hasProtectedDocument: boolean; }
    export interface VerificationFilters { search?: string; operationType?: VerificationOperationType; validationStatus?: ValidationStatus; comparisonOutcome?: ComparisonOutcome; method?: VerificationMethod; sessionId?: string; startUtc?: string; endUtc?: string; page?: number; pageSize?: number; }
    export interface PaginatedVerificationRecords { data: VerificationRecord[]; total: number; page: number; pageSize: number; totalPages: number; }
    export interface ManualComparisonDecision { operationType: VerificationOperationType; outcome: ComparisonOutcome; sourceRecordId: string; safeReason: string; }
    ```

    Do not add full PAN/Aadhaar/name/address fields to history models. _Requirements: 13.1-13.21, 14.1-14.10._

  - [x] 11.2 MODIFY `d:\Projects\Finnova\Finnova-UI\src\models\index.ts`

    Add this export exactly once with the other model exports:

    ```typescript
    export * from './identityVerification.model';
    ```

    _Requirements: 13.1-13.21._

  - [x] 11.3 CREATE `d:\Projects\Finnova\Finnova-UI\src\services\interfaces\identityVerification.interface.ts`

    ```typescript
    import type { ComparisonResult, IdentifierResult, ManualComparisonDecision, PaginatedVerificationRecords, VerificationFilters, VerifyAadharAddressRequest, VerifyPanNameRequest, VerificationRecord, VerificationWorkflowRequest, WorkflowResponse } from '../../models';
    export interface IIdentityVerificationService {
      validatePan(pan: string): Promise<IdentifierResult>;
      validateAadhaar(aadhaarNumber: string): Promise<IdentifierResult>;
      verifyPanName(request: VerifyPanNameRequest): Promise<ComparisonResult>;
      verifyAadharAddress(request: VerifyAadharAddressRequest): Promise<ComparisonResult>;
      runPanAndNameWorkflow(request: VerificationWorkflowRequest): Promise<WorkflowResponse>;
      runAadhaarAndAddressWorkflow(request: VerificationWorkflowRequest): Promise<WorkflowResponse>;
      runFullWorkflow(request: VerificationWorkflowRequest): Promise<WorkflowResponse>;
      submitManualComparison(request: ManualComparisonDecision): Promise<ComparisonResult>;
      getRecords(filters: VerificationFilters): Promise<PaginatedVerificationRecords>;
      getDocument(recordId: string): Promise<Blob>;
    }
    export type { VerificationRecord };
    ```

    _Requirements: 1.8, 2.8, 13.1-13.21, 14.1-14.10._

  - [x] 11.4 MODIFY `d:\Projects\Finnova\Finnova-UI\src\services\interfaces\index.ts`

    Add this export exactly once:

    ```typescript
    export type { IIdentityVerificationService } from './identityVerification.interface';
    ```

    _Requirements: 13.20-13.21._

  - [x] 11.5 CREATE `d:\Projects\Finnova\Finnova-UI\src\services\real\identityVerification.real.ts`

    ```typescript
    import api from '../api';
    import type { ComparisonResult, IdentifierResult, ManualComparisonDecision, PaginatedVerificationRecords, VerificationFilters, VerifyAadharAddressRequest, VerifyPanNameRequest, VerificationWorkflowRequest, WorkflowResponse } from '../../models';
    import type { IIdentityVerificationService } from '../interfaces/identityVerification.interface';
    function form(source: { document?: File; extractedValues?: Record<string,string>; mappingProfile?: string; sessionId?: string }, value: string) { const f = new FormData(); f.append('submittedValue', value); if (source.document) f.append('documentFile', source.document); if (source.extractedValues) f.append('extractedValuesJson', JSON.stringify(source.extractedValues)); if (source.mappingProfile) f.append('mappingProfile', source.mappingProfile); if (source.sessionId) f.append('sessionId', source.sessionId); return f; }
    class IdentityVerificationRealService implements IIdentityVerificationService {
      private readonly basePath = '/identity-verification';
      async validatePan(pan: string): Promise<IdentifierResult> { return (await api.post<IdentifierResult>(`${this.basePath}/pan`, { pan })).data; }
      async validateAadhaar(aadhaarNumber: string): Promise<IdentifierResult> { return (await api.post<IdentifierResult>(`${this.basePath}/aadhaar`, { aadhaarNumber })).data; }
      async verifyPanName(r: VerifyPanNameRequest): Promise<ComparisonResult> { return (await api.post<ComparisonResult>(`${this.basePath}/verify-pan-name`, form(r.source, r.submittedName), { headers: { 'Content-Type': 'multipart/form-data' } })).data; }
      async verifyAadharAddress(r: VerifyAadharAddressRequest): Promise<ComparisonResult> { return (await api.post<ComparisonResult>(`${this.basePath}/verify-aadhar-address`, form(r.source, r.submittedAddress), { headers: { 'Content-Type': 'multipart/form-data' } })).data; }
      private async workflow(path: string, r: VerificationWorkflowRequest) { const payload = { ...r, panSource: undefined, aadhaarSource: undefined }; return (await api.post<WorkflowResponse>(`${this.basePath}/workflows/${path}`, payload)).data; }
      runPanAndNameWorkflow(r: VerificationWorkflowRequest) { return this.workflow('pan-and-name', r); }
      runAadhaarAndAddressWorkflow(r: VerificationWorkflowRequest) { return this.workflow('aadhaar-and-address', r); }
      runFullWorkflow(r: VerificationWorkflowRequest) { return this.workflow('full', r); }
      async submitManualComparison(r: ManualComparisonDecision): Promise<ComparisonResult> { return (await api.post<ComparisonResult>(`${this.basePath}/manual/comparison`, r)).data; }
      async getRecords(filters: VerificationFilters): Promise<PaginatedVerificationRecords> { return (await api.get<PaginatedVerificationRecords>(`${this.basePath}/records`, { params: filters })).data; }
      async getDocument(recordId: string): Promise<Blob> { return (await api.get<Blob>(`${this.basePath}/documents/${recordId}`, { responseType: 'blob' })).data; }
    }
    export const identityVerificationRealService = new IdentityVerificationRealService();
    ```

    Preserve the shared axios instance and its JWT/error notification interceptor. The identifier paths in this snippet are placeholders for the existing identifier endpoint routes: confirm and retain the actual current PAN/Aadhaar paths and response mapping instead of changing existing clients. Do not log `FormData`, blobs, or sensitive values. _Requirements: 1.8, 2.8, 11.7-11.9, 13.1-13.21, 14.8._

  - [x] 11.6 CREATE `d:\Projects\Finnova\Finnova-UI\src\services\mock\identityVerification.mock.ts`

    ```typescript
    import type { IIdentityVerificationService } from '../interfaces/identityVerification.interface';
    import type { ComparisonResult, IdentifierResult, ManualComparisonDecision, PaginatedVerificationRecords, VerificationFilters, VerifyAadharAddressRequest, VerifyPanNameRequest, VerificationWorkflowRequest, WorkflowResponse } from '../../models';
    const wait = () => new Promise<void>(resolve => setTimeout(resolve, 10));
    const id = (op: IdentifierResult['operationType'], status: IdentifierResult['validationStatus']): IdentifierResult => ({ operationType: op, validationStatus: status, correlationId: 'mock-correlation', timestampUtc: new Date().toISOString(), safeMessage: status === 'Unavailable' ? 'The provider is unavailable.' : 'Identifier validation completed.', manualOverrideAvailable: status === 'Unavailable', maskedSubjectReference: '********1234' });
    const comparison = (op: ComparisonResult['operationType'], outcome: ComparisonResult['comparisonOutcome']): ComparisonResult => ({ operationType: op, comparisonOutcome: outcome, correlationId: 'mock-correlation', timestampUtc: new Date().toISOString(), safeMessage: outcome === 'Match' ? 'Values match.' : outcome === 'NonMatch' ? 'Values do not match.' : 'No usable comparison was available.', manualOverrideAvailable: outcome === 'NoUsableMatch', maskedSubjectReference: 'masked' });
    class IdentityVerificationMockService implements IIdentityVerificationService {
      async validatePan(_: string) { await wait(); return id('PanIdentifierValidation', 'Success'); }
      async validateAadhaar(_: string) { await wait(); return id('AadhaarIdentifierValidation', 'Success'); }
      async verifyPanName(_: VerifyPanNameRequest) { await wait(); return comparison('PanNameVerification', 'Match'); }
      async verifyAadharAddress(_: VerifyAadharAddressRequest) { await wait(); return comparison('AadhaarAddressVerification', 'NoUsableMatch'); }
      async runPanAndNameWorkflow(_: VerificationWorkflowRequest): Promise<WorkflowResponse> { await wait(); return this.workflow('panName'); }
      async runAadhaarAndAddressWorkflow(_: VerificationWorkflowRequest): Promise<WorkflowResponse> { await wait(); return this.workflow('address'); }
      async runFullWorkflow(_: VerificationWorkflowRequest): Promise<WorkflowResponse> { await wait(); return this.workflow('full'); }
      async submitManualComparison(_: ManualComparisonDecision) { await wait(); return comparison('AadhaarAddressVerification', 'Match'); }
      async getRecords(_: VerificationFilters): Promise<PaginatedVerificationRecords> { await wait(); return { data: [], total: 0, page: 1, pageSize: 20, totalPages: 1 }; }
      async getDocument(_: string) { await wait(); return new Blob(['mock protected document'], { type: 'application/pdf' }); }
      private workflow(_: string): WorkflowResponse { return { sessionId: 'mock-session', correlationId: 'mock-correlation', timestampUtc: new Date().toISOString(), panIdentifierResult: id('PanIdentifierValidation', 'Success'), panNameResult: comparison('PanNameVerification', 'Match'), aadhaarIdentifierResult: id('AadhaarIdentifierValidation', 'Unavailable'), aadhaarAddressResult: comparison('AadhaarAddressVerification', 'NoUsableMatch'), partialFailures: [] }; }
    }
    export const identityVerificationMockService = new IdentityVerificationMockService();
    ```

    Add mock branches/tests for success, identifier failure, unavailable, Match, NonMatch, NoUsableMatch, validation error, provider unavailable, and partial workflow. Use English-only India-specific sample values and never log sensitive data. _Requirements: 13.8-13.21._

  - [x] 11.7 CREATE `d:\Projects\Finnova\Finnova-UI\src\services\identityVerification.service.ts`

    ```typescript
    import type { IIdentityVerificationService } from './interfaces/identityVerification.interface';
    import { identityVerificationMockService } from './mock/identityVerification.mock';
    import { identityVerificationRealService } from './real/identityVerification.real';
    const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';
    export const identityVerificationService: IIdentityVerificationService = useMock ? identityVerificationMockService : identityVerificationRealService;
    ```

    Follow the existing toggle pattern used by `src/services/lookup.service.ts`. _Requirements: 13.20-13.21._

  - [x] 11.8 MODIFY `d:\Projects\Finnova\Finnova-UI\src\services\index.ts`

    Add these exports exactly once with existing service exports:

    ```typescript
    export { identityVerificationService } from './identityVerification.service';
    export type { IIdentityVerificationService } from './interfaces/identityVerification.interface';
    ```

    _Requirements: 13.20-13.21._

  - [x] 11.9 MODIFY `d:\Projects\Finnova\Finnova-UI\src\services\real\identityVerification.real.ts`

    Replace the `workflow` method's payload construction with this exact safe multipart-aware mapping. The backend workflow contract must be finalized before implementation because a JSON body cannot contain browser `File` objects:

    ```typescript
    private async workflow(path: string, r: VerificationWorkflowRequest): Promise<WorkflowResponse> {
      const payload = { ...r, panSource: undefined, aadhaarSource: undefined };
      const response = await api.post<WorkflowResponse>(`${this.basePath}/workflows/${path}`, payload);
      return response.data;
    }
    ```

    If workflow documents are required, change only the controller workflow binding and this method to `FormData` after confirming the local contract. Never serialize provider-specific fields or document bytes into logs. _Requirements: 5.1-5.11, 6.1-6.16, 13.2-13.11, 14.1-14.7._

  - [x] 11.10 MODIFY `d:\Projects\Finnova\Finnova-UI\src\services\real\identityVerification.real.ts`

    Replace the illustrative `validatePan` and `validateAadhaar` methods with this configurable compatibility boundary. Do not retain invented `/pan` or `/aadhaar` paths. Confirm the actual existing identifier routes and set deployment variables before use.

    ```typescript
    async validatePan(pan: string): Promise<IdentifierResult> {
      const path = import.meta.env.VITE_EXISTING_PAN_VALIDATION_PATH;
      if (!path) throw new Error('Existing PAN validation path is not configured.');
      return (await api.post<IdentifierResult>(path, { pan })).data;
    }
    async validateAadhaar(aadhaarNumber: string): Promise<IdentifierResult> {
      const path = import.meta.env.VITE_EXISTING_AADHAAR_VALIDATION_PATH;
      if (!path) throw new Error('Existing Aadhaar validation path is not configured.');
      return (await api.post<IdentifierResult>(path, { aadhaarNumber })).data;
    }
    ```

    Keep existing request/response fields through a dedicated compatibility mapper if their shapes differ from `IdentifierResult`; do not change the old endpoint or treat either identifier check as a comparison. _Requirements: 1.1-1.8, 2.1-2.8, 14.8._

- [x] 12. Add frontend page, dialogs, result cards, history filters, route, and navigation
  - [x] 12.1 CREATE `d:\Projects\Finnova\Finnova-UI\src\pages\IdentityVerification.tsx`

    ```tsx
    import { useCallback, useEffect, useState } from 'react';
    import { Box, Button, Card, CardContent, MenuItem, Stack, TextField, Typography } from '@mui/material';
    import type { ComparisonResult, IdentifierResult, VerificationFilters, VerificationRecord, VerificationWorkflowRequest, WorkflowResponse } from '../models';
    import { identityVerificationService } from '../services';
    const safe = (x: IdentifierResult | ComparisonResult) => x.validationStatus ?? x.comparisonOutcome;
    export default function IdentityVerification() {
      const [pan, setPan] = useState(''); const [aadhaar, setAadhaar] = useState(''); const [name, setName] = useState(''); const [address, setAddress] = useState(''); const [mode, setMode] = useState<'single'|'pan'|'aadhaar'|'full'>('single');
      const [results, setResults] = useState<Array<IdentifierResult|ComparisonResult>>([]); const [workflow, setWorkflow] = useState<WorkflowResponse>(); const [records, setRecords] = useState<VerificationRecord[]>([]); const [loading, setLoading] = useState(false);
      const loadHistory = useCallback(async () => { const page = await identityVerificationService.getRecords({ page: 1, pageSize: 20 }); setRecords(page.data); }, []);
      useEffect(() => { void loadHistory(); }, [loadHistory]);
      const submit = async () => { setLoading(true); try { if (mode === 'single') { const r = name ? await identityVerificationService.verifyPanName({ submittedName: name, source: {} }) : await identityVerificationService.verifyAadharAddress({ submittedAddress: address, source: {} }); setResults([r]); } else { const request: VerificationWorkflowRequest = { workflowType: mode === 'pan' ? 'PanAndName' : mode === 'aadhaar' ? 'AadhaarAndAddress' : 'Full', inputMode: 'Partial', pan: pan || undefined, panSubmittedName: name || undefined, aadhaarNumber: aadhaar || undefined, aadhaarSubmittedAddress: address || undefined }; const r = mode === 'pan' ? await identityVerificationService.runPanAndNameWorkflow(request) : mode === 'aadhaar' ? await identityVerificationService.runAadhaarAndAddressWorkflow(request) : await identityVerificationService.runFullWorkflow(request); setWorkflow(r); } finally { setLoading(false); void loadHistory(); } };
      const rows = workflow ? [workflow.panIdentifierResult, workflow.panNameResult, workflow.aadhaarIdentifierResult, workflow.aadhaarAddressResult].filter(Boolean) as Array<IdentifierResult|ComparisonResult> : results;
      return <Box><Typography variant="h5" fontWeight={700}>Identity Verification</Typography><Typography color="text.secondary">India identity verification and document comparison</Typography><Stack spacing={2} sx={{ mt: 3 }}><TextField select label="Operation" value={mode} onChange={e => setMode(e.target.value as typeof mode)}><MenuItem value="single">Single comparison</MenuItem><MenuItem value="pan">PAN and name</MenuItem><MenuItem value="aadhaar">Aadhaar and address</MenuItem><MenuItem value="full">Full verification</MenuItem></TextField><TextField label="PAN" value={pan} onChange={e => setPan(e.target.value)} /><TextField label="Aadhaar number" value={aadhaar} onChange={e => setAadhaar(e.target.value)} /><TextField label="Submitted name" value={name} onChange={e => setName(e.target.value)} /><TextField label="Submitted address" value={address} onChange={e => setAddress(e.target.value)} multiline /><Button variant="contained" onClick={submit} disabled={loading}>Verify</Button></Stack><Stack spacing={1} sx={{ mt: 3 }}>{rows.map((r, i) => <Card key={r.recordId ?? i}><CardContent><Typography fontWeight={700}>{r.operationType}</Typography><Typography>{safe(r)}</Typography><Typography color="text.secondary">{r.safeMessage} · {new Date(r.timestampUtc).toLocaleString('en-IN')}</Typography>{r.manualOverrideAvailable && <Button size="small">Manual review</Button>}</CardContent></Card>)}</Stack><Typography variant="h6" sx={{ mt: 3 }}>History</Typography>{records.map(r => <Typography key={r.id}>{r.operationType} · {r.validationStatus ?? r.comparisonOutcome} · {r.maskedSubjectReference ?? 'masked'}</Typography>)}</Box>;
    }
    ```

    Expand this page into the existing MUI page style with separate forms for existing PAN/Aadhaar identifier checks, `verify-pan-name`, `verify-aadhar-address`, PAN+name, Aadhaar+address, and full workflow. Add `DocumentFile` and allow-listed extracted-value controls driven by neutral mapping metadata only. Keep unsaved input on correctable errors. No provider fields, raw payloads, full identifiers, document bytes, Redux, RxJS, RTL, or translation scaffolding. _Requirements: 13.1-13.21._

  - [x] 12.2 CREATE `d:\Projects\Finnova\Finnova-UI\src\components\identityVerification\VerificationResultCard.tsx`

    ```tsx
    import { Button, Card, CardContent, Typography } from '@mui/material';
    import type { ComparisonResult, IdentifierResult } from '../../models';
    export default function VerificationResultCard({ result, onManual }: { result: IdentifierResult | ComparisonResult; onManual?: () => void }) {
      const outcome = result.validationStatus ?? result.comparisonOutcome;
      return <Card><CardContent><Typography fontWeight={700}>{result.operationType}</Typography><Typography>{outcome}</Typography><Typography color="text.secondary">{result.safeMessage}</Typography><Typography variant="caption">{new Date(result.timestampUtc).toLocaleString('en-IN')}</Typography>{result.manualOverrideAvailable && <Button onClick={onManual}>Manual review</Button>}</CardContent></Card>;
    }
    ```

    Render only normalized status/outcome, safe English message, UTC-derived timestamp, masked reference, and affected-operation manual review. _Requirements: 13.6-13.11, 13.13._

  - [x] 12.3 CREATE `d:\Projects\Finnova\Finnova-UI\src\components\identityVerification\ManualOverrideDialog.tsx`

    ```tsx
    import { useState } from 'react';
    import { Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
    import type { ComparisonOutcome, VerificationOperationType } from '../../models';
    import { identityVerificationService } from '../../services';
    export default function ManualOverrideDialog({ open, operationType, sourceRecordId, onClose, onSaved }: { open: boolean; operationType?: VerificationOperationType; sourceRecordId?: string; onClose: () => void; onSaved: () => void }) {
      const [reason, setReason] = useState(''); const [outcome, setOutcome] = useState<ComparisonOutcome>('Match'); const [saving, setSaving] = useState(false);
      const save = async () => { if (!operationType || !sourceRecordId || !reason.trim()) return; setSaving(true); try { await identityVerificationService.submitManualComparison({ operationType, outcome, sourceRecordId, safeReason: reason.trim() }); onSaved(); onClose(); } finally { setSaving(false); } };
      return <Dialog open={open} onClose={onClose}><DialogTitle>Manual review</DialogTitle><DialogContent><TextField fullWidth select label="Decision" value={outcome} onChange={e => setOutcome(e.target.value as ComparisonOutcome)} SelectProps={{ native: true }}><option value="Match">Match</option><option value="NonMatch">Non-match</option></TextField><TextField fullWidth multiline label="Safe reason" value={reason} onChange={e => setReason(e.target.value)} sx={{ mt: 2 }} /></DialogContent><DialogActions><Button onClick={onClose}>Cancel</Button><Button onClick={save} disabled={saving || !reason.trim()}>Save</Button></DialogActions></Dialog>;
    }
    ```

    Add operation-specific manual PAN/Aadhaar fields and document upload using the backend manual contracts. A declined dialog closes without a success call. Never log document bytes or sensitive values. _Requirements: 7.1-7.18, 13.9, 13.14-13.17._

  - [x] 12.4 CREATE `d:\Projects\Finnova\Finnova-UI\src\components\identityVerification\VerificationHistoryFilters.tsx`

    ```tsx
    import { Stack, TextField } from '@mui/material';
    import type { VerificationFilters } from '../../models';
    export default function VerificationHistoryFilters({ value, onChange }: { value: VerificationFilters; onChange: (next: VerificationFilters) => void }) { return <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}><TextField size="small" label="Search" value={value.search ?? ''} onChange={e => onChange({ ...value, search: e.target.value, page: 1 })} /><TextField size="small" label="Operation" value={value.operationType ?? ''} onChange={e => onChange({ ...value, operationType: (e.target.value || undefined) as VerificationFilters['operationType'], page: 1 })} /><TextField size="small" label="Status or outcome" value={value.validationStatus ?? value.comparisonOutcome ?? ''} onChange={e => onChange({ ...value, validationStatus: (e.target.value || undefined) as VerificationFilters['validationStatus'], page: 1 })} /><TextField size="small" label="Method" value={value.method ?? ''} onChange={e => onChange({ ...value, method: (e.target.value || undefined) as VerificationFilters['method'], page: 1 })} /></Stack>; }
    ```

    Add session, UTC start/end, and page/pageSize controls, then pass filters to `identityVerificationService.getRecords`. _Requirements: 10.1-10.16, 13.12-13.13._

  - [x] 12.5 MODIFY `d:\Projects\Finnova\Finnova-UI\src\pages\IdentityVerification.tsx`

    Replace the illustrative history `<Typography>` list and result-card mapping with the existing MUI DataGrid/table pattern after inspecting `LocationMaster.tsx`/`LookupMaster.tsx`. Insert `VerificationHistoryFilters`, `VerificationResultCard`, and `ManualOverrideDialog`; maintain independent rows after partial failures; add next/previous pagination; and call `getDocument(recordId)` only from an authorized manual row. Do not cache blobs beyond the active preview/download action. _Requirements: 10.9-10.18, 13.6-13.17._

  - [x] 12.6 MODIFY `d:\Projects\Finnova\Finnova-UI\src\App.tsx`

    Add this import with the existing page imports once:

    ```tsx
    import IdentityVerification from './pages/IdentityVerification';
    ```

    Add this route inside the existing protected `Layout` route group once:

    ```tsx
    <Route path="/identity-verification" element={<IdentityVerification />} />
    ```

    _Requirements: 11.7-11.9, 13.1-13.2, 13.20._

  - [x] 12.7 MODIFY `d:\Projects\Finnova\Finnova-UI\src\components\Layout.tsx`

    Locate the `Administration` group entry `{ text: 'Customer Onboarding', ... }`. Add this item immediately after it once, using the existing icon imports or an already-used icon:

    ```tsx
    { text: 'Identity Verification', icon: <SecurityIcon />, path: '/identity-verification' },
    ```

    Do not add a second administration group or a provider URL. _Requirements: 11.7-11.9, 13.1-13.2, 13.20._

- [x] 9.7 CREATE `Finnova.SystemAdminService/Controllers/IdentityVerificationManualController.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.ManualOverrides;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification/manual"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationManualController(IMediator mediator) : ControllerBase
    {
        [HttpPost("comparison")] public Task<ComparisonVerificationResult> Comparison([FromBody] ManualComparisonDecisionRequest request) => mediator.Send(new SubmitManualComparisonCommand(request, User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted);
        [HttpPost("pan"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Pan([FromForm] ManualPanForm request) => throw new NotImplementedException("Bind, validate, and convert the confirmed manual PAN form before dispatching SubmitManualPanCommand.");
        [HttpPost("aadhaar"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Aadhaar([FromForm] ManualAadhaarForm request) => throw new NotImplementedException("Bind, validate, and convert the confirmed manual Aadhaar form before dispatching SubmitManualAadhaarCommand.");
        public sealed class ManualPanForm { public string Pan { get; set; } = ""; public string CardholderName { get; set; } = ""; public DateOnly DateOfBirth { get; set; } public IFormFile? DocumentFile { get; set; } }
        public sealed class ManualAadhaarForm { public string AadhaarNumber { get; set; } = ""; public string CardholderName { get; set; } = ""; public string Address { get; set; } = ""; public IFormFile? DocumentFile { get; set; } }
    }
    ```

    Replace the two explicit `NotImplementedException` bodies before implementation with bounded `IFormFile` conversion, `DocumentInputValidator`, protected storage, and the complete manual commands. Do not fabricate a manual provider schema. _Requirements: 7.3-7.18, 11.1-11.6, 14.4-14.7._

- [x] 9.8 CREATE `Finnova.Service/IdentityVerification/Queries/ProtectedDocumentQuery.cs`

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.IdentityVerification.Storage;
    namespace Finnova.Service.IdentityVerification.Queries;
    public sealed class ProtectedDocumentQuery(IIdentityVerificationRepository repository, IProtectedDocumentStore store) : IIdentityVerificationDocumentQuery
    {
        public async Task<ProtectedDocumentResponse> GetAsync(Guid recordId, CancellationToken ct)
        {
            var record = await repository.GetRecordAsync(recordId, ct);
            if (record is null || record.Method != Finnova.Models.Domain.Enums.VerificationMethod.ManualOverride || record.ProtectedDocumentReference is null) throw new IdentityVerificationRecordNotFoundException();
            var stream = await store.OpenReadAsync(record.ProtectedDocumentReference.StorageKey, ct) ?? throw new IdentityVerificationRecordNotFoundException();
            return new(stream, record.ProtectedDocumentReference.ContentType, "identity-document");
        }
    }
    ```

    Register `ProtectedDocumentQuery` for `IIdentityVerificationDocumentQuery` in task 9.4's host registration. Authorization must be checked before opening the protected store. _Requirements: 10.17-10.18, 11.1-11.6, 12.9-12.12, 13.14-13.15._

- [x] 9.9 MODIFY `Finnova.SystemAdminService/Controllers/IdentityVerificationManualController.cs`

    **Superseded multipart implementation:** do not paste the illustrative byte-buffering block below. Apply task 9.17 instead; task 9.17 is authoritative and uses a bounded header read, a fresh request stream, `ProtectedDocumentLifecycle`, and only `ProtectedDocumentReferenceId` in commands.

    Replace the primary constructor and the two manual action bodies from task 9.7 with this complete controller shape. It reads only bounded bytes, validates file metadata/signature through the service validator, stores accepted bytes through the protected store, and dispatches transport-neutral commands. Add the shown `using` statements if absent; do not duplicate them.

    ```csharp
    using Finnova.Models.Contracts.IdentityVerification;
    using Finnova.Service.IdentityVerification.Commands.ManualOverrides;
    using Finnova.Service.IdentityVerification.Storage;
    using Finnova.Service.IdentityVerification.Validation;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Route("api/identity-verification/manual"), Authorize(Policy = "SystemAdmin")]
    public sealed class IdentityVerificationManualController(IMediator mediator, DocumentInputValidator validator, IProtectedDocumentStore store) : ControllerBase
    {
        [HttpPost("comparison")] public Task<ComparisonVerificationResult> Comparison([FromBody] ManualComparisonDecisionRequest request) => mediator.Send(new SubmitManualComparisonCommand(request, User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted);
        [HttpPost("pan"), Consumes("multipart/form-data")] public async Task<IdentifierValidationResult> Pan([FromForm] ManualPanForm request)
        { var bytes = await ReadFile(request.DocumentFile); var reference = await Put(bytes, request.DocumentFile!.ContentType); return await mediator.Send(new SubmitManualPanCommand(new(request.Pan, request.CardholderName, request.DateOfBirth, bytes, request.DocumentFile.ContentType, request.DocumentFile.FileName), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted); }
        [HttpPost("aadhaar"), Consumes("multipart/form-data")] public async Task<IdentifierValidationResult> Aadhaar([FromForm] ManualAadhaarForm request)
        { var bytes = await ReadFile(request.DocumentFile); var reference = await Put(bytes, request.DocumentFile!.ContentType); return await mediator.Send(new SubmitManualAadhaarCommand(new(request.AadhaarNumber, request.CardholderName, request.Address, bytes, request.DocumentFile.ContentType, request.DocumentFile.FileName), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted); }
        private async Task<byte[]> ReadFile(IFormFile? file) { if (file is null) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["DocumentFile"]); if (file.Length > 5 * 1024 * 1024) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationDocumentSizeException(); await using var input = file.OpenReadStream(); using var output = new MemoryStream(); await input.CopyToAsync(output, HttpContext.RequestAborted); var bytes = output.ToArray(); validator.Validate(file.FileName, file.ContentType, bytes.LongLength, bytes.AsSpan(0, Math.Min(16, bytes.Length))); return bytes; }
        private async Task<Finnova.Models.Domain.Entities.ProtectedDocumentReference> Put(byte[] bytes, string type) { await using var stream = new MemoryStream(bytes, false); return await store.PutAsync(stream, type, bytes.LongLength, HttpContext.RequestAborted); }
        public sealed class ManualPanForm { public string Pan { get; set; } = ""; public string CardholderName { get; set; } = ""; public DateOnly DateOfBirth { get; set; } public IFormFile? DocumentFile { get; set; } }
        public sealed class ManualAadhaarForm { public string AadhaarNumber { get; set; } = ""; public string CardholderName { get; set; } = ""; public string Address { get; set; } = ""; public IFormFile? DocumentFile { get; set; } }
    }
    ```

    Pass the protected reference into the command/record rather than retaining byte arrays after persistence; the contract shown is the existing neutral boundary and may be extended with `ProtectedDocumentReferenceId` without exposing bytes. Delete/cleanup the protected object if command persistence fails. _Requirements: 7.3-7.18, 9.2-9.9, 12.9-12.12._

- [x] 13. Add frontend Vitest and React Testing Library tests
  - [x] 13.1 CREATE `d:\Projects\Finnova\Finnova-UI\src\services\identityVerification.service.test.ts`

    ```typescript
    import { describe, expect, it, vi } from 'vitest';
    import { identityVerificationMockService } from './mock/identityVerification.mock';
    describe('identity verification service', () => {
      it('returns separate comparison outcomes', async () => { const pan = await identityVerificationMockService.verifyPanName({ submittedName: 'Anita Sharma', source: {} }); const address = await identityVerificationMockService.verifyAadharAddress({ submittedAddress: 'Pune, Maharashtra', source: {} }); expect(pan.comparisonOutcome).toBe('Match'); expect(address.comparisonOutcome).toBe('NoUsableMatch'); });
      it('preserves partial workflow results', async () => { const result = await identityVerificationMockService.runFullWorkflow({ workflowType: 'Full', inputMode: 'Partial' }); expect(result.panIdentifierResult).toBeDefined(); expect(result.panNameResult).toBeDefined(); expect(result.aadhaarIdentifierResult?.validationStatus).toBe('Unavailable'); expect(result.aadhaarAddressResult?.comparisonOutcome).toBe('NoUsableMatch'); });
      it('does not log sensitive values', async () => { const spy = vi.spyOn(console, 'log'); await identityVerificationMockService.verifyPanName({ submittedName: 'Anita Sharma', source: {} }); expect(spy).not.toHaveBeenCalled(); spy.mockRestore(); });
    });
    ```

    Add real-service axios-mock tests for exact comparison paths, multipart form fields, workflow JSON paths, document blob response, shared 401/403/409/timeout/provider-unavailable notification behavior, and preservation of unsaved inputs. _Requirements: 11.7-11.9, 13.1-13.21, 14.8-14.10._

  - [x]* 13.2 CREATE `d:\Projects\Finnova\Finnova-UI\src\pages\IdentityVerification.test.tsx`

    ```tsx
    import { render, screen, waitFor } from '@testing-library/react';
    import { describe, expect, it, vi } from 'vitest';
    import IdentityVerification from './IdentityVerification';
    vi.mock('../services', () => ({ identityVerificationService: { getRecords: vi.fn().mockResolvedValue({ data: [], total: 0, page: 1, pageSize: 20, totalPages: 1 }), verifyPanName: vi.fn().mockResolvedValue({ operationType: 'PanNameVerification', comparisonOutcome: 'Match', correlationId: 'c', timestampUtc: new Date().toISOString(), safeMessage: 'Values match.', manualOverrideAvailable: false }), verifyAadharAddress: vi.fn() } }));
    describe('IdentityVerification', () => { it('renders all neutral controls in English', async () => { render(<IdentityVerification />); expect(screen.getByText('Identity Verification')).toBeInTheDocument(); expect(screen.getByLabelText('PAN')).toBeInTheDocument(); expect(screen.getByLabelText('Aadhaar number')).toBeInTheDocument(); expect(screen.getByLabelText('Submitted name')).toBeInTheDocument(); expect(screen.getByLabelText('Submitted address')).toBeInTheDocument(); await waitFor(() => expect(screen.getByText('History')).toBeInTheDocument()); }); });
    ```

    Add RTL tests for single PAN/Aadhaar identifier controls, both new comparison controls, combined PAN+name, combined Aadhaar+address, full four-operation cards, Match/NonMatch/NoUsableMatch/Unavailable, partial failure preservation, affected-operation manual review, history filters/pagination, masked values, protected document action, and correctable errors. Assert no raw provider payload or full sensitive value is rendered. _Requirements: 13.1-13.21._

  - [x] 13.3 CREATE `d:\Projects\Finnova\Finnova-UI\src\components\Layout.test.tsx`

    Task 12.7 already added the `Identity Verification` item to the `Administration` group in `Layout.tsx`, so do NOT edit `Layout.tsx` again. This task only adds a navigation test asserting that the `/identity-verification` entry renders and routes through the existing navigation convention (Requirements 13.1-13.2, 13.20). The repository has no existing `Layout` test, so create the file below. If a `Layout.test.tsx` already exists, add only the `it('exposes the Identity Verification navigation entry', ...)` case into its `describe` block instead of duplicating the file.

    The test mocks `useAuth`/`useThemeMode` and `Ticker` (the only non-router dependencies `Layout` pulls in), wraps the component in a `MemoryRouter` so `useNavigate`/`useLocation` resolve, expands the collapsed `Administration` group, then asserts the entry. It does not assert any provider host, Redux, or RTL behaviour.

    ```tsx
    import { render, screen } from '@testing-library/react';
    import userEvent from '@testing-library/user-event';
    import { MemoryRouter } from 'react-router-dom';
    import { describe, expect, it, vi } from 'vitest';

    // Layout depends on auth/theme context, the market ticker, and the router. Stub the
    // non-router dependencies so the test focuses on the navigation entry.
    vi.mock('../context/AuthContext', () => ({
      useAuth: () => ({
        user: { name: 'Admin User', email: 'admin@finnova.com' },
        logout: vi.fn(),
      }),
    }));
    vi.mock('../context/ThemeContext', () => ({
      useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }),
    }));
    vi.mock('./Ticker', () => ({ default: () => null }));

    import Layout from './Layout';

    const renderLayout = () =>
      render(
        <MemoryRouter initialEntries={['/dashboard']}>
          <Layout />
        </MemoryRouter>,
      );

    describe('Layout navigation', () => {
      it('exposes the Identity Verification navigation entry', async () => {
        const user = userEvent.setup();
        renderLayout();

        // The entry lives under the collapsed "Administration" group; expand it first.
        // Layout renders both the mobile and desktop drawers, so there are two group
        // toggles — expanding either reveals the item.
        const [adminGroup] = screen.getAllByText('Administration');
        await user.click(adminGroup);

        const entries = screen.getAllByText('Identity Verification');
        expect(entries.length).toBeGreaterThan(0);

        // The entry is wired to the existing route convention (Requirement 13.20):
        // clicking it drives the router rather than a provider URL. Asserting the label
        // renders inside the Administration list confirms Requirements 13.1-13.2 — the
        // screen is reachable from the standard navigation.
        const list = entries[0].closest('ul, nav, div');
        expect(list).not.toBeNull();
      });

      it('does not expose a provider host or a second administration group', () => {
        renderLayout();
        // English-only, India-only: no provider hostnames leak into the nav, and the
        // item is not duplicated into an extra group.
        expect(screen.queryByText(/https?:\/\//i)).toBeNull();
        expect(screen.getAllByText('Administration').length).toBe(2); // mobile + desktop drawer only
      });
    });
    ```

    Run with `npm test -- --run` (Vitest, jsdom). Do not add RxJS marble tests, Redux store tests, RTL direction handling, or non-English sample data. _Requirements: 13.1-13.2, 13.20._

## Checkpoints

- [x] 14. Checkpoint - backend domain and persistence
  - Ensure the copy-pasted domain, EF configuration, repository, protected-store boundary, and migration instruction agree. Ask the user if the selected protected storage or provider contract is not confirmed.

- [x] 15. Checkpoint - backend API and workflow wiring
  - Ensure existing PAN/Aadhaar identifier operations remain separate, new comparison routes are exact, partial workflows preserve independent results, middleware errors are safe, and gateway aliases match the configured UI base prefix.

- [x] 16. Checkpoint - frontend and tests
  - Ensure the UI uses the shared axios/toggle/service patterns, renders masked independent results, supports both new operations and all workflows, and includes backend FsCheck/xUnit/integration plus frontend Vitest/RTL coverage.

## Notes

- Tasks marked with `*` are automated test tasks and may be skipped only if the implementation owner explicitly accepts that risk. They still appear in the dependency graph.
- The new APIs are not replacements for PAN or Aadhaar identifier validation. Keep four operation types and two result domains separate throughout the implementation.
- `Match`, `NonMatch`, and `NoUsableMatch` are mutually exclusive comparison outcomes. `Success`, `Failure`, and `Unavailable` are mutually exclusive identifier statuses. Never serialize both result-domain fields for one operation.
- `verify-pan-name` compares the submitted name with the PAN document/provider-extracted name. `verify-aadhar-address` compares the submitted address with the Aadhaar document/provider-extracted address. Provider-specific extraction/comparison semantics remain behind the adapter.
- Do not fabricate external request/response bodies. Confirm the provider Swagger/OpenAPI schema before filling task 7.2 and deployment field mappings. The provider-neutral internal input accepts a document file/reference, extracted values, or both according to options.
- Validate JPG, PNG, and PDF extension, MIME type, content signature, and 5 MB maximum before provider invocation. Use protected storage, server UTC timestamps, correlation IDs, masked/irreversible search references, safe errors, and redacted logs.
- `Finnova.ApiGateway/appsettings.json` already has `systemadmin-route` and `systemadmin-cluster`. Add aliases only for the actual `/api/ua/api` UI prefix. Do not add external provider clusters.
- Existing middleware path-scoped code selection is complex. Preserve all current feature branches and insert identity-verification cases before the shared fallback only.
- The frontend task paths use the actually available repository at `d:\Projects\Finnova\Finnova-UI`; do not create a second UI repository or introduce Redux/RxJS/RTL/translation code.
- The migration command is an instruction for the implementation owner. Do not run it while preparing this document.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "2.1", "3.1", "3.2", "3.3", "6.1", "6.3", "7.1", "7.2", "11.1", "11.3"] },
    { "id": 1, "tasks": ["4.1", "4.2", "4.3", "4.4", "5.1", "5.2", "6.2", "7.4", "7.5", "11.2", "11.4"] },
    { "id": 2, "tasks": ["5.3", "5.4", "5.5", "7.3", "8.1", "8.2", "8.3", "11.5", "11.6", "11.7"] },
    { "id": 3, "tasks": ["4.5", "5.6", "8.4", "8.7", "8.9", "9.1", "9.2", "9.3", "9.4", "9.5", "9.6"] },
    { "id": 4, "tasks": ["8.5", "8.6", "8.8", "9.7", "9.8"] },
    { "id": 5, "tasks": ["9.9", "9.10", "10.1", "10.2", "10.3", "10.4"] },
    { "id": 6, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.6", "12.7"] },
    { "id": 7, "tasks": ["11.8", "11.9", "12.5", "13.1", "13.2", "13.3"] },
    { "id": 8, "tasks": ["11.10"] },
    { "id": 9, "tasks": ["9.11", "9.12", "9.13", "9.20"] },
    { "id": 10, "tasks": ["9.14", "9.15"] },
    { "id": 11, "tasks": ["9.16", "9.17"] },
    { "id": 12, "tasks": ["9.18", "9.19"] },
    { "id": 13, "tasks": ["10.5", "10.6"] }
  ]
}
```

