# Implementation Plan: Document Master Management (FINNOVA-49, FINNOVA-50, FINNOVA-52)

## Overview

This plan is the **copy-paste code deliverable** for the Document Master Management feature. Each
task is marked **CREATE `<path>`** or **MODIFY `<path>`** and carries the complete code block to
apply (for MODIFY, the exact snippet and precisely where it goes). Apply the tasks in the order
below — this follows the design's Implementation-Ordered Task List exactly (dependencies before
dependents): backend domain → exceptions → contracts → EF/DbContext/migration → repositories/DI →
helpers/ports → CQRS slice → controller/middleware/gateway/Program.cs → backend tests → frontend
service layer → frontend page/components/route/nav → frontend tests.

The feature follows the platform's layered CQRS conventions exactly, as realized by the Lookup,
Nationality, Organization Hierarchy, Court, Entity, Drawee Bank, DCN, Asset, and GL masters:
`Finnova.Models` → `Finnova.Repository` (EF Core on **SQL Server**, generic `IRepository<T>` +
`RepositoryBase<T>`) → `Finnova.Service` (MediatR + FluentValidation + static `.ToResponse()`
mappers) → the existing `Finnova.SystemAdminService` host (JWT Bearer, `ValidationBehavior<,>`
pipeline, `ExceptionHandlingMiddleware`) → the `Finnova.ApiGateway` YARP proxy → the React +
TypeScript + Material UI frontend (hooks + context + shared axios, **not** Redux/RxJS).

Platform context (steering): Finnova is **India-only, English-only** — single English display
fields (`DocName`, `DocShortName`, `Description`), no bilingual fields, no Arabic content, **no
RTL**. The UI uses **MUI** (`@mui/material`, `@mui/x-data-grid`) with hooks + context, not Redux
or RxJS. Backend tests use **xUnit + FsCheck.Xunit** (min 100 iterations); frontend tests use
**Vitest + React Testing Library (jsdom)**.

> Reused and NOT re-created: the `Finnova.SystemAdminService` host (:5030), JWT Bearer auth,
> MediatR `ValidationBehavior<,>`, `ExceptionHandlingMiddleware`, `AddFinnovaRepository` +
> `FinnovaDbContext` (auto-discovers new `IEntityTypeConfiguration<T>`),
> `ILookupRepository.GetActiveByModuleAndTypeAsync`, `PaginatedResponse<T>`, the gateway
> `systemadmin-cluster` and the `ua-*-alias` route pattern, and the maker-checker / audit /
> acting-user / ports precedents from the GL and Court masters.

## Tasks

- [ ] 1. Backend domain — entities and enums (Finnova.Models)

  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/DocumentEnums.cs`
    - Domain `DocumentStatus` (not Lookup-backed) + `DocumentAuditAction`, mirroring `GLEnums.cs`.
      The host already registers `JsonStringEnumConverter`, so these serialize by name.
    - _Requirements: 1.1, 5.3, 5.6, 5.10, 6.1, 6.4, 8.1_

    ```csharp
    namespace Finnova.Models.Domain.Enums;

    /// <summary>
    /// Lifecycle state of a document definition. Domain-enum (not Lookup-backed) so the
    /// maker-checker state machine is enforced in code, consistent with GLStatus.
    /// </summary>
    public enum DocumentStatus
    {
        PendingApproval,     // newly created, never authorized (requirements: PENDING_APPROVAL)
        Active,              // authorized & consumable        (requirements: ACTIVE)
        Inactive,            // authorized then retired         (requirements: INACTIVE)
        PendingModification, // edit of an Active doc awaiting authorization (live values still served)
        ReturnedToMaker      // post-reject; pending change returned for correction (requirements: RETURNED_TO_MAKER)
    }

    /// <summary>Governance action captured by a document audit entry (R8).</summary>
    public enum DocumentAuditAction
    {
        Create,
        Submit,                 // resubmit after correction
        Update,                 // edit of attributes
        ApplicabilityChange,
        ValidationConfigChange,
        Approve,
        Reject,
        Inactivate,
        Reactivate,
        Delete
    }
    ```

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/Document.cs`
    - Carries the GL maker-checker field set plus document attributes and the pending-edit payload
      column. India-only: single English display fields.
    - _Requirements: 1.1, 1.6, 1.9, 2.1, 2.3, 3.1, 4.1, 5.3, 9.1_

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// A single document definition in the Document Master (FINNOVA-49/50/52). India-only: single
    /// English display fields. The row always holds the latest submitted values; Status gates
    /// consumer visibility. For an edit of an Active document, PendingValuesJson holds the pending
    /// edit while the live columns keep serving consumers until authorization.
    /// </summary>
    public class Document
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // --- identity & display (English-only) ---
        public string DocCode { get; set; } = string.Empty;          // max 20; letters/digits/hyphen; unique (CI)
        public string DocName { get; set; } = string.Empty;          // max 150
        public string? DocShortName { get; set; }                    // optional short label
        public string? Description { get; set; }                     // optional

        // --- classification (lookup-backed, validated against ACTIVE lookup values) ---
        public string DocCategory { get; set; } = string.Empty;      // Lookup DOC_CATEGORY code
        public string ApplicableEntity { get; set; } = string.Empty; // Lookup APPLICABLE_ENTITY code

        // --- document flags ---
        public bool IsMandatory { get; set; }                        // default false (R1.9)
        public bool HasExpiry { get; set; }                          // default false (R1.9)
        public bool RequiresMasking { get; set; }                    // default false (R1.9)
        public bool IsAadhaarDocument { get; set; }                  // Aadhaar marker (R9.4)

        // --- validation / masking configuration (stored, NOT executed — R9.11) ---
        public string ValidationEngine { get; set; } = "NONE";       // Lookup VALIDATION_ENGINE code
        public string? FormatRegex { get; set; }                     // max 500; must compile if non-empty
        public string? MaskPattern { get; set; }                     // max 200; required when RequiresMasking
        public int MaxFileSizeMb { get; set; } = 5;                  // 1..25 inclusive; default 5 (R1.6)
        public string AllowedMimeTypes { get; set; } = string.Empty; // CSV of {PDF,JPG,PNG}; non-empty

        // --- maker-checker lifecycle (mirrors GLAccount) ---
        public DocumentStatus Status { get; set; } = DocumentStatus.PendingApproval; // R1.1
        public string? MakerId { get; set; }                         // last maker of the pending change
        public string? CheckerId { get; set; }                       // last checker
        public int AuthorizedVersionNumber { get; set; }             // starts 0; +1 per authorized modification
        public string? RejectionReason { get; set; }                 // last rejection remark (<=500)

        /// <summary>
        /// Pending edit payload for an edit of an already-Active document. While set, the scalar
        /// columns above remain the LIVE (authorized) values served to consumers; the Checker view
        /// compares live columns vs this payload. Cleared on authorize (copy pending->live) or when
        /// the document was never authorized (pending values live directly in the columns).
        /// </summary>
        public string? PendingValuesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // --- applicability matrix (R4) ---
        public List<DocumentApplicabilityRule> ApplicabilityRules { get; set; } = new();
    }
    ```

  - [ ] 1.3 CREATE `Finnova.Models/Domain/Entities/DocumentApplicabilityRule.cs`
    - One row per (document × constitution × entity role) with a per-mapping mandatory flag; the
      combination is unique.
    - _Requirements: 4.1, 4.2, 4.4_

    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Maps a document to one Constitution and one Entity Role with an Is Mandatory flag (R4).
    /// The (DocumentId, Constitution, EntityRole) combination is unique.
    /// </summary>
    public class DocumentApplicabilityRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }

        public string Constitution { get; set; } = string.Empty;   // Lookup CONSTITUTION code
        public string EntityRole { get; set; } = string.Empty;     // Lookup APPLICABLE_ENTITY code
        public bool IsMandatory { get; set; }

        public Document? Document { get; set; }
    }
    ```

  - [ ] 1.4 CREATE `Finnova.Models/Domain/Entities/DocumentAuditEntry.cs`
    - Immutable audit record, mirroring `CourtAuditEntry` (JSON before/after snapshots, summary,
      optional remark, UTC timestamp).
    - _Requirements: 8.1, 8.2, 8.4, 8.5_

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Immutable audit record for a document action (R8). One row per persisted action. No update
    /// or delete surface is exposed for this table.
    /// </summary>
    public class DocumentAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }

        public DocumentAuditAction Action { get; set; }
        public string? OldValues { get; set; }     // compact JSON snapshot (null for Create)
        public string? NewValues { get; set; }     // compact JSON snapshot
        public string Summary { get; set; } = string.Empty;
        public string? Remarks { get; set; }       // rejection remark on Reject (R8.2)

        public string ChangedBy { get; set; } = string.Empty;    // acting user id (from JWT)
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```

- [ ] 2. Backend domain — exceptions (Finnova.Models)

  - [ ] 2.1 CREATE `Finnova.Models/Domain/Exceptions/DocumentExceptions.cs`
    - Typed exception set mirroring the GL/Court style; error-code constants on the types that
      carry a user-facing code. The middleware (task 8.2) maps these to ProblemDetails.
    - _Requirements: 2.2, 3.3, 3.5, 5.2, 5.5, 5.9, 7.1, 7.3, 7.4, 7.5, 12.6_

    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>Targeted document id does not exist (R3.3, R5.2, R7.4).</summary>
    public class DocumentNotFoundException : Exception
    {
        public DocumentNotFoundException() : base("The document was not found.") { }
    }

    /// <summary>DocCode already exists (trim + case-insensitive) (R2.2).</summary>
    public class DocumentDuplicateCodeException : Exception
    {
        public const string ErrorCode = "ERR-DOC-409";
        public DocumentDuplicateCodeException()
            : base("A document with this Doc Code already exists.") { }
    }

    /// <summary>State conflict: no pending change, already pending, or active-delete (R3.5, R5.9, R7.3).</summary>
    public class DocumentConflictException : Exception
    {
        public DocumentConflictException(string message) : base(message) { }
    }

    /// <summary>Delete blocked because the document is in use by Customer Master (R7.1).</summary>
    public class DocumentInUseException : Exception
    {
        public const string ErrorCode = "ERR-DOC-002";
        public DocumentInUseException()
            : base("Document is in use and cannot be deleted. You can mark it Inactive") { }
    }

    /// <summary>Self-authorization prohibited: maker == checker (R5.5, R12.6).</summary>
    public class DocumentSelfAuthorizationException : Exception
    {
        public const string ErrorCode = "ERR-DOC-001";
        public DocumentSelfAuthorizationException()
            : base("You cannot authorize a record you created or edited") { }
    }
    ```

- [ ] 3. Backend contracts (Finnova.Models/Contracts/Documents)

  - [ ] 3.1 CREATE `Finnova.Models/Contracts/Documents/DocumentRequests.cs`
    - Request records. Create flags/size are nullable so the service applies defaults (R1.6, R1.9).
      Paged listing reuses the existing `PaginatedResponse<T>` (no new paged request type).
    - _Requirements: 1.1, 1.6, 1.9, 2.3, 3.1, 4.1, 5.6, 9.1_

    ```csharp
    namespace Finnova.Models.Contracts.Documents;

    // ---- Create (R1). Nullable flags/size so the service applies defaults (R1.6, R1.9). ----
    public record CreateDocumentRequest(
        string DocCode,
        string DocName,
        string? DocShortName,
        string? Description,
        string DocCategory,
        string ApplicableEntity,
        bool? IsMandatory,
        bool? HasExpiry,
        bool? RequiresMasking,
        bool? IsAadhaarDocument,
        int? MaxFileSizeMb
    );

    // ---- Edit document attributes (R3). DocCode is NOT editable once Active (R2.3). ----
    public record UpdateDocumentRequest(
        string DocName,
        string? DocShortName,
        string? Description,
        string DocCategory,
        string ApplicableEntity,
        bool IsMandatory,
        bool HasExpiry
    );

    // ---- One applicability mapping (R4). ----
    public record ApplicabilityRuleRequest(
        string Constitution,
        string EntityRole,
        bool IsMandatory
    );

    // ---- Replace the applicability matrix as a pending change (R4.1). ----
    public record UpdateApplicabilityRulesRequest(
        IReadOnlyList<ApplicabilityRuleRequest> Rules
    );

    // ---- Validation / masking configuration (R9). ----
    public record UpdateValidationConfigRequest(
        string ValidationEngine,
        string? FormatRegex,
        bool RequiresMasking,
        string? MaskPattern,
        int MaxFileSizeMb,
        IReadOnlyList<string> AllowedMimeTypes   // subset of {PDF,JPG,PNG}, non-empty
    );

    // ---- Reject carries a mandatory remark (R5.6). Authorize/Resubmit carry no body. ----
    public record RejectDocumentRequest(string Reason);
    ```

  - [ ] 3.2 CREATE `Finnova.Models/Contracts/Documents/DocumentResponses.cs`
    - Response records. `DocumentResponse` is the full admin projection; the review response pairs
      live vs pending; the consumption response is the Customer Master projection.
    - _Requirements: 1.1, 5.1, 6.3, 8.4, 11.2_

    ```csharp
    namespace Finnova.Models.Contracts.Documents;

    // ---- Applicability rule in responses. ----
    public record ApplicabilityRuleResponse(
        Guid Id, string Constitution, string EntityRole, bool IsMandatory
    );

    // ---- Full admin-facing response. ----
    public record DocumentResponse(
        Guid Id,
        string DocCode,
        string DocName,
        string? DocShortName,
        string? Description,
        string DocCategory,
        string ApplicableEntity,
        bool IsMandatory,
        bool HasExpiry,
        bool RequiresMasking,
        bool IsAadhaarDocument,
        string ValidationEngine,
        string? FormatRegex,
        string? MaskPattern,
        int MaxFileSizeMb,
        IReadOnlyList<string> AllowedMimeTypes,
        string Status,                 // DocumentStatus name
        string? MakerId,
        string? CheckerId,
        int AuthorizedVersionNumber,
        string? RejectionReason,
        IReadOnlyList<ApplicabilityRuleResponse> ApplicabilityRules,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    // ---- Checker review: live vs pending side by side (R5.1). ----
    public record DocumentPendingReviewResponse(
        Guid Id,
        string Status,
        DocumentResponse Live,         // currently authorized values
        DocumentResponse Pending       // proposed values (from PendingValuesJson, or the row itself
                                       // for a never-authorized PendingApproval doc)
    );

    // ---- Audit entry (R8.4). ----
    public record DocumentAuditEntryResponse(
        Guid Id,
        Guid DocumentId,
        string Action,
        string? OldValues,
        string? NewValues,
        string Summary,
        string? Remarks,
        string ChangedBy,
        DateTime ChangedAtUtc
    );

    // ---- Active-document consumption projection for Customer Master (R11.2). ----
    public record ActiveDocumentForConsumptionResponse(
        Guid Id,
        string DocCode,
        string DocName,
        bool IsMandatory,              // resolved from the matching applicability rule (R11.2)
        bool HasExpiry,
        string ValidationEngine,
        string? FormatRegex,
        bool RequiresMasking,
        string? MaskPattern,
        int MaxFileSizeMb,
        IReadOnlyList<string> AllowedMimeTypes
    );
    ```

- [ ] 4. Backend EF configuration + DbContext + migration (Finnova.Repository)

  - [ ] 4.1 CREATE `Finnova.Repository/Configuration/DocumentConfigurations.cs`
    - The three `IEntityTypeConfiguration<T>` classes, mirroring `CourtConfiguration` (lowercase
      snake_case tables, `HasMaxLength`, unique + query indexes, enum-as-string). Auto-discovered
      by `FinnovaDbContext.ApplyConfigurationsFromAssembly`.
    - _Requirements: 2.1, 2.2, 4.4, 8.4, 10.1, 10.2, 10.8_

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class DocumentConfiguration : IEntityTypeConfiguration<Document>
    {
        public void Configure(EntityTypeBuilder<Document> b)
        {
            b.ToTable("document_master");
            b.HasKey(x => x.Id);

            b.Property(x => x.DocCode).IsRequired().HasMaxLength(20);
            b.Property(x => x.DocName).IsRequired().HasMaxLength(150);
            b.Property(x => x.DocShortName).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.DocCategory).IsRequired().HasMaxLength(50);
            b.Property(x => x.ApplicableEntity).IsRequired().HasMaxLength(50);

            b.Property(x => x.ValidationEngine).IsRequired().HasMaxLength(50);
            b.Property(x => x.FormatRegex).HasMaxLength(500);
            b.Property(x => x.MaskPattern).HasMaxLength(200);
            b.Property(x => x.MaxFileSizeMb).IsRequired();
            b.Property(x => x.AllowedMimeTypes).IsRequired().HasMaxLength(100);

            b.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.MakerId).HasMaxLength(100);
            b.Property(x => x.CheckerId).HasMaxLength(100);
            b.Property(x => x.RejectionReason).HasMaxLength(500);
            b.Property(x => x.PendingValuesJson);   // nvarchar(max)

            // DocCode uniqueness (R2.1/2.2). Default CI collation backs the case-insensitive rule;
            // the handler trims so stored values are canonical.
            b.HasIndex(x => x.DocCode).IsUnique();
            // Query/filter paths (R10): status + category filters, DocName substring order tie-break.
            b.HasIndex(x => new { x.Status, x.DocCategory });
            b.HasIndex(x => x.DocName);

            b.HasMany(x => x.ApplicabilityRules)
                .WithOne(r => r.Document!)
                .HasForeignKey(r => r.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class DocumentApplicabilityRuleConfiguration : IEntityTypeConfiguration<DocumentApplicabilityRule>
    {
        public void Configure(EntityTypeBuilder<DocumentApplicabilityRule> b)
        {
            b.ToTable("document_applicability_rules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Constitution).IsRequired().HasMaxLength(50);
            b.Property(x => x.EntityRole).IsRequired().HasMaxLength(50);
            b.Property(x => x.IsMandatory).IsRequired();

            // Unique applicability combination per document (R4.4).
            b.HasIndex(x => new { x.DocumentId, x.Constitution, x.EntityRole }).IsUnique();
        }
    }

    public class DocumentAuditEntryConfiguration : IEntityTypeConfiguration<DocumentAuditEntry>
    {
        public void Configure(EntityTypeBuilder<DocumentAuditEntry> b)
        {
            b.ToTable("document_audit_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).IsRequired().HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Summary).IsRequired().HasMaxLength(400);
            b.Property(x => x.Remarks).HasMaxLength(500);
            b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(100);
            b.Property(x => x.ChangedAtUtc).IsRequired();

            // Newest-first reads with a deterministic id tie-break (R8.4).
            b.HasIndex(x => new { x.DocumentId, x.ChangedAtUtc });
        }
    }
    ```

  - [ ] 4.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`
    - Add three `DbSet` properties alongside the existing ones (e.g. right after the
      `GLAuditEntries` line, before the `OnModelCreating` method). No other change — the existing
      `ApplyConfigurationsFromAssembly` auto-discovers the configurations from task 4.1.
    - _Requirements: 2.1, 4.1, 8.1_

    Insert these lines among the other `DbSet` declarations (next to `GLAuditEntries`):

    ```csharp
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentApplicabilityRule> DocumentApplicabilityRules => Set<DocumentApplicabilityRule>();
    public DbSet<DocumentAuditEntry> DocumentAuditEntries => Set<DocumentAuditEntry>();
    ```

  - [ ] 4.3 Generate the EF Core migration (command — run after tasks 1–5 are applied)
    - After the entities, configurations, DbSets, and repositories compile, generate and apply the
      migration from the repository project against the existing SystemAdmin host (SQL Server).
      This is a shell command note, not a file to author.
    - _Requirements: 2.1, 4.4, 8.4_

    ```text
    dotnet ef migrations add AddDocumentMaster \
      --project Finnova.Repository \
      --startup-project Finnova.SystemAdminService

    dotnet ef database update \
      --project Finnova.Repository \
      --startup-project Finnova.SystemAdminService
    ```

- [ ] 5. Backend repositories + interfaces + DI (Finnova.Repository)

  - [ ] 5.1 CREATE `Finnova.Repository/Interfaces/IDocumentRepository.cs`
    - _Requirements: 2.2, 3.3, 10.1, 10.2, 10.8, 11.1_

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Repository.Interfaces;

    public interface IDocumentRepository : IRepository<Document>
    {
        /// <summary>Loads a document with its applicability rules (tracked for updates).</summary>
        Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Paged admin grid (R10). search = substring on DocCode/DocName (CI); category/status
        /// optional AND filters. Ordered by DocCode asc then Id asc. Returns (items, total).
        /// </summary>
        Task<(List<Document> Items, int Total)> GetPagedAsync(
            string? search, string? docCategory, DocumentStatus? status,
            int page, int pageSize, CancellationToken ct = default);

        /// <summary>
        /// Active documents whose applicability matrix includes (constitution, entityRole) (R11.1).
        /// </summary>
        Task<List<Document>> GetActiveForConsumptionAsync(
            string constitution, string entityRole, CancellationToken ct = default);

        /// <summary>Case-insensitive, trimmed DocCode uniqueness check (R2). excludeId skips self.</summary>
        Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default);
    }
    ```

  - [ ] 5.2 CREATE `Finnova.Repository/Repositories/DocumentRepository.cs`
    - Full EF implementation, mirroring `CourtRepository`. The DocCode uniqueness uses
      `ToUpper()` on both sides so the trimmed, case-insensitive rule (R2.2) holds regardless of
      column collation. The paged grid orders by DocCode then Id (R10.8).
    - _Requirements: 2.2, 3.3, 10.1, 10.2, 10.8, 11.1, 11.3_

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class DocumentRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<Document>(finnovaDbContext), IDocumentRepository
    {
        public async Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default)
        {
            return await DbSet
                .Include(x => x.ApplicabilityRules)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<(List<Document> Items, int Total)> GetPagedAsync(
            string? search, string? docCategory, DocumentStatus? status,
            int page, int pageSize, CancellationToken ct = default)
        {
            var query = DbSet.AsNoTracking().Include(x => x.ApplicabilityRules).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.DocCode.Contains(term) || x.DocName.Contains(term)); // R10.1 (CI collation)
            }
            if (!string.IsNullOrWhiteSpace(docCategory))
                query = query.Where(x => x.DocCategory == docCategory);     // R10.2 AND filter
            if (status.HasValue)
                query = query.Where(x => x.Status == status.Value);         // R10.2 AND filter

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(x => x.DocCode).ThenBy(x => x.Id)                   // R10.8
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
            return (items, total);
        }

        public async Task<List<Document>> GetActiveForConsumptionAsync(
            string constitution, string entityRole, CancellationToken ct = default)
        {
            // Active only (R11.3) and the applicability matrix must contain the pair (R11.1).
            return await DbSet.AsNoTracking()
                .Include(x => x.ApplicabilityRules)
                .Where(x => x.Status == DocumentStatus.Active
                            && x.ApplicabilityRules.Any(r =>
                                r.Constitution == constitution && r.EntityRole == entityRole))
                .OrderBy(x => x.DocCode).ThenBy(x => x.Id)
                .ToListAsync(ct);
        }

        public async Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = docCode.Trim().ToUpper();   // trimmed + case-insensitive (R2.2)
            return await DbSet.AsNoTracking().AnyAsync(
                x => x.DocCode.ToUpper() == c && (excludeId == null || x.Id != excludeId), ct);
        }
    }
    ```

  - [ ] 5.3 CREATE `Finnova.Repository/Interfaces/IDocumentAuditRepository.cs`
    - _Requirements: 8.1, 8.4, 8.6_

    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IDocumentAuditRepository
    {
        Task AddAsync(DocumentAuditEntry entry, CancellationToken ct = default);

        /// <summary>Entries for a document, newest-first with id desc tie-break (R8.4). Empty for
        /// an unknown id (R8.6).</summary>
        Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
    }
    ```

  - [ ] 5.4 CREATE `Finnova.Repository/Repositories/DocumentAuditRepository.cs`
    - Mirrors `CourtAuditRepository`. `GetByDocumentIdAsync` orders newest-first with an id
      tie-break (R8.4) and returns an empty list for an unknown id (R8.6).
    - _Requirements: 8.1, 8.4, 8.6_

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class DocumentAuditRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<DocumentAuditEntry>(finnovaDbContext), IDocumentAuditRepository
    {
        public async Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(
            Guid documentId, CancellationToken ct = default)
        {
            return await DbSet.AsNoTracking()
                .Where(x => x.DocumentId == documentId)
                .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R8.4
                .ToListAsync(ct);   // empty when none (R8.6)
        }
    }
    ```

  - [ ] 5.5 MODIFY `Finnova.Repository/DependencyInjection.cs`
    - Register both repositories inside `AddFinnovaRepository`, next to the existing
      `IGLAuditRepository` registration (just before `return services;`).
    - _Requirements: 2.2, 8.1_

    Add these two lines to the `AddFinnovaRepository` body, alongside the other `AddScoped` calls:

    ```csharp
    services.AddScoped<IDocumentRepository, DocumentRepository>();
    services.AddScoped<IDocumentAuditRepository, DocumentAuditRepository>();
    ```

- [ ] 6. Backend domain helpers and ports (Finnova.Service/Document)

  - [ ] 6.1 CREATE `Finnova.Service/Document/Internal/DocumentAuditSnapshot.cs`
    - Compact JSON snapshot of a document's editable fields for audit entries, mirroring
      `GLAuditSnapshot`/`CourtAuditSnapshot`.
    - _Requirements: 8.1_

    ```csharp
    using System.Text.Json;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.Document.Internal;

    /// <summary>Compact JSON snapshots of a document's editable fields for audit entries (R8).</summary>
    public static class DocumentAuditSnapshot
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

        public static string Editable(Finnova.Models.Domain.Entities.Document d) => JsonSerializer.Serialize(new
        {
            d.DocCode, d.DocName, d.DocShortName, d.Description,
            d.DocCategory, d.ApplicableEntity,
            d.IsMandatory, d.HasExpiry, d.RequiresMasking, d.IsAadhaarDocument,
            d.ValidationEngine, d.FormatRegex, d.MaskPattern, d.MaxFileSizeMb, d.AllowedMimeTypes,
            Status = d.Status.ToString(),
            d.AuthorizedVersionNumber,
            Rules = d.ApplicabilityRules
                .Select(r => new { r.Constitution, r.EntityRole, r.IsMandatory })
        }, Options);
    }
    ```

  - [ ] 6.2 CREATE `Finnova.Service/Document/Internal/DocumentPendingPayload.cs`
    - Single source of truth for the pending-edit shape held in `PendingValuesJson`. Serializes
      the editable attribute subset and the applicability matrix, and applies a payload onto the
      entity's live columns on authorize. The payload is a discriminated shape so authorize can
      apply attribute edits, applicability edits, and validation-config edits uniformly.
    - _Requirements: 3.1, 4.1, 4.5, 5.3, 9.1, 9.10_

    ```csharp
    using System.Text.Json;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.Document.Internal;

    /// <summary>
    /// Serializes/deserializes the editable payload held in <see cref="Document.PendingValuesJson"/>
    /// and applies it onto the entity's live columns on authorize. One payload can carry any subset
    /// of: attribute edit (R3), applicability matrix (R4), validation/masking config (R9). Null
    /// members mean "unchanged" for that slice.
    /// </summary>
    public static class DocumentPendingPayload
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

        public sealed record RulePayload(string Constitution, string EntityRole, bool IsMandatory);

        public sealed record Payload(
            // attribute edit slice (R3)
            string? DocName = null,
            string? DocShortName = null,
            string? Description = null,
            string? DocCategory = null,
            string? ApplicableEntity = null,
            bool? IsMandatory = null,
            bool? HasExpiry = null,
            // applicability slice (R4)
            IReadOnlyList<RulePayload>? Rules = null,
            // validation/masking slice (R9)
            string? ValidationEngine = null,
            string? FormatRegex = null,
            bool? RequiresMasking = null,
            string? MaskPattern = null,
            int? MaxFileSizeMb = null,
            IReadOnlyList<string>? AllowedMimeTypes = null
        );

        public static string Serialize(Payload payload) => JsonSerializer.Serialize(payload, Options);

        public static Payload Deserialize(string json) =>
            JsonSerializer.Deserialize<Payload>(json, Options) ?? new Payload();

        // --- builders used by the command handlers ---

        public static string ForAttributes(
            string docName, string? docShortName, string? description,
            string docCategory, string applicableEntity, bool isMandatory, bool hasExpiry) =>
            Serialize(new Payload(
                DocName: docName, DocShortName: docShortName, Description: description,
                DocCategory: docCategory, ApplicableEntity: applicableEntity,
                IsMandatory: isMandatory, HasExpiry: hasExpiry));

        public static string ForApplicability(IEnumerable<DocumentApplicabilityRule> rules) =>
            Serialize(new Payload(Rules: rules
                .Select(r => new RulePayload(r.Constitution, r.EntityRole, r.IsMandatory)).ToList()));

        public static string ForValidationConfig(
            string validationEngine, string? formatRegex, bool requiresMasking,
            string? maskPattern, int maxFileSizeMb, IReadOnlyList<string> allowedMimeTypes) =>
            Serialize(new Payload(
                ValidationEngine: validationEngine, FormatRegex: formatRegex,
                RequiresMasking: requiresMasking, MaskPattern: maskPattern,
                MaxFileSizeMb: maxFileSizeMb, AllowedMimeTypes: allowedMimeTypes));

        /// <summary>Copies a serialized pending payload onto the entity's live columns (authorize).</summary>
        public static void ApplyTo(Finnova.Models.Domain.Entities.Document entity, string json)
        {
            var p = Deserialize(json);

            if (p.DocName is not null) entity.DocName = p.DocName;
            if (p.DocShortName is not null) entity.DocShortName = p.DocShortName;
            if (p.Description is not null) entity.Description = p.Description;
            if (p.DocCategory is not null) entity.DocCategory = p.DocCategory;
            if (p.ApplicableEntity is not null) entity.ApplicableEntity = p.ApplicableEntity;
            if (p.IsMandatory is not null) entity.IsMandatory = p.IsMandatory.Value;
            if (p.HasExpiry is not null) entity.HasExpiry = p.HasExpiry.Value;

            if (p.ValidationEngine is not null) entity.ValidationEngine = p.ValidationEngine;
            if (p.FormatRegex is not null) entity.FormatRegex = p.FormatRegex;
            if (p.RequiresMasking is not null) entity.RequiresMasking = p.RequiresMasking.Value;
            if (p.MaskPattern is not null) entity.MaskPattern = p.MaskPattern;
            if (p.MaxFileSizeMb is not null) entity.MaxFileSizeMb = p.MaxFileSizeMb.Value;
            if (p.AllowedMimeTypes is not null)
                entity.AllowedMimeTypes = string.Join(',', p.AllowedMimeTypes);

            if (p.Rules is not null)
            {
                entity.ApplicabilityRules.Clear();
                foreach (var r in p.Rules)
                    entity.ApplicabilityRules.Add(new DocumentApplicabilityRule
                    {
                        DocumentId = entity.Id,
                        Constitution = r.Constitution,
                        EntityRole = r.EntityRole,
                        IsMandatory = r.IsMandatory
                    });
            }
        }
    }
    ```

  - [ ] 6.3 CREATE `Finnova.Service/Document/Ports/DocumentPorts.cs`
    - Two outbound port interfaces, mirroring the GL Ports precedent.
    - _Requirements: 1.8, 4.3, 7.1, 9.2_

    ```csharp
    namespace Finnova.Service.Document.Ports;

    /// <summary>
    /// Validates lookup-backed fields against ACTIVE Lookup Master values (R1.8, R4.3, R9.2).
    /// Adapter delegates to the existing ILookupRepository.GetActiveByModuleAndTypeAsync.
    /// </summary>
    public interface ILookupValidationPort
    {
        Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default);
    }

    /// <summary>
    /// Outbound check for whether a document is referenced by Customer Master (FINNOVA-23) (R7).
    /// Owned by the Customer Master epic; a no-op dev stub reports "not in use" until it ships.
    /// </summary>
    public interface IDocumentUsageChecker
    {
        Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default);
    }
    ```

  - [ ] 6.4 CREATE `Finnova.Service/Document/Ports/DocumentPortAdapters.cs`
    - The lookup adapter (delegates to `ILookupRepository`) and the dev stub usage checker.
    - _Requirements: 1.8, 4.3, 7.1, 7.2, 9.2_

    ```csharp
    using Finnova.Repository.Interfaces;

    namespace Finnova.Service.Document.Ports;

    /// <summary>
    /// Reads ACTIVE lookup values for the Document Master's lookup-backed fields. The Module scope
    /// is "SystemAdmin" — the module under which the Lookup Master seeds DOC_CATEGORY,
    /// APPLICABLE_ENTITY, CONSTITUTION, and VALIDATION_ENGINE types that this master consumes.
    /// (Assumption A2 — confirm the seeded Module value.)
    /// </summary>
    public sealed class LookupValidationAdapter : ILookupValidationPort
    {
        public const string Module = "SystemAdmin";
        private readonly ILookupRepository _lookups;
        public LookupValidationAdapter(ILookupRepository lookups) => _lookups = lookups;

        public async Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var active = await _lookups.GetActiveByModuleAndTypeAsync(Module, lookupType, ct);
            return active.Any(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// DEV STUB (resolved assumption: in-use detection). Reports "not in use" so never-authorized
    /// documents can be deleted (R7.2) until Customer Master (FINNOVA-23) provides a real adapter.
    /// Replace before production. Fail-safe (block delete) on error/timeout is enforced by the
    /// delete handler, not by this stub.
    /// </summary>
    public sealed class StubDocumentUsageChecker : IDocumentUsageChecker
    {
        public Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(false);
    }
    ```

- [ ] 7. Backend service CQRS slice (Finnova.Service/Document)

  Every request is a record implementing `IRequest<TResponse>`; handlers implement
  `IRequestHandler<,>`; validators are `AbstractValidator<T>` run by the shared
  `ValidationBehavior<,>` (already registered in the host). Handlers resolve `ActingUser` from the
  command (set by the controller from the JWT), never from a client body. `ValidationException`
  refers to `FluentValidation.ValidationException` throughout.

  - [ ] 7.1 CREATE `Finnova.Service/Mappers/DocumentMapper.cs`
    - Static `.ToResponse()` mappers, mirroring `CourtMapper`/`LookupMapper`.
    - _Requirements: 1.1, 5.1, 8.4, 11.2_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.Mappers;

    public static class DocumentMapper
    {
        public static DocumentResponse ToResponse(this Finnova.Models.Domain.Entities.Document d) => new(
            d.Id, d.DocCode, d.DocName, d.DocShortName, d.Description,
            d.DocCategory, d.ApplicableEntity,
            d.IsMandatory, d.HasExpiry, d.RequiresMasking, d.IsAadhaarDocument,
            d.ValidationEngine, d.FormatRegex, d.MaskPattern, d.MaxFileSizeMb,
            SplitMime(d.AllowedMimeTypes),
            d.Status.ToString(), d.MakerId, d.CheckerId, d.AuthorizedVersionNumber, d.RejectionReason,
            d.ApplicabilityRules.Select(r => new ApplicabilityRuleResponse(
                r.Id, r.Constitution, r.EntityRole, r.IsMandatory)).ToList(),
            d.CreatedAt, d.UpdatedAt);

        public static List<DocumentResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.Document> items)
            => items.Select(i => i.ToResponse()).ToList();

        public static ActiveDocumentForConsumptionResponse ToConsumptionResponse(
            this Finnova.Models.Domain.Entities.Document d, bool resolvedMandatory) => new(
            d.Id, d.DocCode, d.DocName, resolvedMandatory, d.HasExpiry,
            d.ValidationEngine, d.FormatRegex, d.RequiresMasking, d.MaskPattern,
            d.MaxFileSizeMb, SplitMime(d.AllowedMimeTypes));

        public static DocumentAuditEntryResponse ToResponse(this DocumentAuditEntry e) => new(
            e.Id, e.DocumentId, e.Action.ToString(), e.OldValues, e.NewValues,
            e.Summary, e.Remarks, e.ChangedBy, e.ChangedAtUtc);

        private static IReadOnlyList<string> SplitMime(string csv) =>
            string.IsNullOrWhiteSpace(csv) ? Array.Empty<string>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    ```

  - [ ] 7.2 CREATE `Finnova.Service/Document/Commands/CreateDocument/CreateDocumentCommand.cs`
    - Command record + handler + validator (R1.3/1.4/1.5/1.7). Sets defaults (R1.6, R1.9),
      validates lookup-backed fields (R1.8), enforces trimmed/case-insensitive uniqueness (R2.2),
      persists `PendingApproval` (R1.1) invisible to consumers (R1.2), writes one audit entry.
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.2, 8.1_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Document.Ports;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;

    namespace Finnova.Service.Document.Commands.CreateDocument;

    public record CreateDocumentCommand(
        string DocCode, string DocName, string? DocShortName, string? Description,
        string DocCategory, string ApplicableEntity,
        bool? IsMandatory, bool? HasExpiry, bool? RequiresMasking, bool? IsAadhaarDocument,
        int? MaxFileSizeMb, string ActingUser) : IRequest<DocumentResponse>;

    public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;
        private readonly ILookupValidationPort _lookups;

        public CreateDocumentCommandHandler(IDocumentRepository repository,
            IDocumentAuditRepository audit, ILookupValidationPort lookups)
        { _repository = repository; _audit = audit; _lookups = lookups; }

        public async Task<DocumentResponse> Handle(CreateDocumentCommand request, CancellationToken ct)
        {
            var code = request.DocCode.Trim();

            // R1.8 lookup-backed fields must be ACTIVE lookup values.
            if (!await _lookups.IsActiveLookupAsync("DOC_CATEGORY", request.DocCategory, ct))
                throw new ValidationException("Doc Category is not an active lookup value.");
            if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.ApplicableEntity, ct))
                throw new ValidationException("Applicable Entity is not an active lookup value.");

            // R2.2 trimmed, case-insensitive uniqueness.
            if (await _repository.ExistsByCodeAsync(code, null, ct))
                throw new DocumentDuplicateCodeException();

            var entity = new Finnova.Models.Domain.Entities.Document
            {
                DocCode = code,
                DocName = request.DocName.Trim(),
                DocShortName = request.DocShortName?.Trim(),
                Description = request.Description?.Trim(),
                DocCategory = request.DocCategory,
                ApplicableEntity = request.ApplicableEntity,
                IsMandatory = request.IsMandatory ?? false,          // R1.9
                HasExpiry = request.HasExpiry ?? false,              // R1.9
                RequiresMasking = request.RequiresMasking ?? false,  // R1.9
                IsAadhaarDocument = request.IsAadhaarDocument ?? false,
                MaxFileSizeMb = request.MaxFileSizeMb ?? 5,          // R1.6 default
                Status = DocumentStatus.PendingApproval,             // R1.1 / R1.2
                MakerId = request.ActingUser,
            };

            await _repository.AddAsync(entity, ct);
            await _audit.AddAsync(new DocumentAuditEntry
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Create,
                OldValues = null,
                NewValues = DocumentAuditSnapshot.Editable(entity),
                Summary = $"Created document '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }

    public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
    {
        public CreateDocumentCommandValidator()
        {
            RuleFor(x => x.DocCode).NotEmpty().WithMessage("Doc Code is required.")           // R1.3
                .Must(c => c is not null && c.Trim().Length <= 20)
                    .WithMessage("Doc Code must not exceed 20 characters.")                   // R1.4
                .Matches("^[A-Za-z0-9-]+$")
                    .WithMessage("Doc Code may contain only letters, digits, and hyphens.");  // R1.4
            RuleFor(x => x.DocName).NotEmpty().WithMessage("Doc Name is required.")           // R1.3
                .Must(n => n is not null && n.Trim().Length <= 150)
                    .WithMessage("Doc Name must not exceed 150 characters.");                 // R1.5
            RuleFor(x => x.DocCategory).NotEmpty().WithMessage("Doc Category is required.");  // R1.3
            RuleFor(x => x.ApplicableEntity).NotEmpty().WithMessage("Applicable Entity is required."); // R1.3
            RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 25)
                .WithMessage("Max file size must be between 1 and 25 MB")                     // R1.7
                .When(x => x.MaxFileSizeMb.HasValue);
        }
    }
    ```

  - [ ] 7.3 CREATE `Finnova.Service/Document/Commands/UpdateDocument/UpdateDocumentCommand.cs`
    - Edit attributes under maker-checker. For an `Active` doc the edit is staged in
      `PendingValuesJson` and Status → `PendingModification` with live columns unchanged (R3.1,
      R3.2). For a never-authorized `PendingApproval` doc or a `ReturnedToMaker` doc the edit is
      applied in place (R3.6). A single-pending guard rejects a second edit of an already-pending
      doc (R3.5). Not-found → 404 (R3.3). DocCode is never part of the edit (R2.3 — not editable).
    - _Requirements: 2.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 8.1_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Document.Ports;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;
    using DocEntity = Finnova.Models.Domain.Entities.Document;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.UpdateDocument;

    public record UpdateDocumentCommand(
        Guid Id, string DocName, string? DocShortName, string? Description,
        string DocCategory, string ApplicableEntity, bool IsMandatory, bool HasExpiry,
        string ActingUser) : IRequest<DocumentResponse>;

    public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;
        private readonly ILookupValidationPort _lookups;

        public UpdateDocumentCommandHandler(IDocumentRepository repository,
            IDocumentAuditRepository audit, ILookupValidationPort lookups)
        { _repository = repository; _audit = audit; _lookups = lookups; }

        public async Task<DocumentResponse> Handle(UpdateDocumentCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();   // R3.3

            // R3.5 — a change is already pending; reject a further edit.
            if (entity.Status is DocumentStatus.PendingModification)
                throw new DocumentConflictException("A change is already pending authorization.");
            if (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null)
                throw new DocumentConflictException("A change is already pending authorization.");

            // R1.8 lookup-backed fields (same rule as create; R3.4 routes failures to validation).
            if (!await _lookups.IsActiveLookupAsync("DOC_CATEGORY", request.DocCategory, ct))
                throw new ValidationException("Doc Category is not an active lookup value.");
            if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.ApplicableEntity, ct))
                throw new ValidationException("Applicable Entity is not an active lookup value.");

            var name = request.DocName.Trim();
            var shortName = request.DocShortName?.Trim();
            var description = request.Description?.Trim();

            if (entity.Status == DocumentStatus.Active)
            {
                // R3.1/R3.2 — stage the pending edit; live columns keep serving consumers.
                entity.PendingValuesJson = DocumentPendingPayload.ForAttributes(
                    name, shortName, description, request.DocCategory, request.ApplicableEntity,
                    request.IsMandatory, request.HasExpiry);
                entity.Status = DocumentStatus.PendingModification;
            }
            else
            {
                // Never-authorized PendingApproval or ReturnedToMaker — apply in place (R3.6).
                entity.DocName = name;
                entity.DocShortName = shortName;
                entity.Description = description;
                entity.DocCategory = request.DocCategory;
                entity.ApplicableEntity = request.ApplicableEntity;
                entity.IsMandatory = request.IsMandatory;
                entity.HasExpiry = request.HasExpiry;
                entity.Status = DocumentStatus.PendingApproval;   // R3.6 keeps it pending for a checker
            }

            entity.MakerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Update,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = entity.PendingValuesJson ?? DocumentAuditSnapshot.Editable(entity),
                Summary = $"Submitted edit for '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }

    public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
    {
        public UpdateDocumentCommandValidator()
        {
            RuleFor(x => x.DocName).NotEmpty().WithMessage("Doc Name is required.")
                .Must(n => n is not null && n.Trim().Length <= 150)
                    .WithMessage("Doc Name must not exceed 150 characters.");   // R1.5
            RuleFor(x => x.DocCategory).NotEmpty().WithMessage("Doc Category is required.");
            RuleFor(x => x.ApplicableEntity).NotEmpty().WithMessage("Applicable Entity is required.");
        }
    }
    ```

  - [ ] 7.4 CREATE `Finnova.Service/Document/Commands/UpdateApplicabilityRules/UpdateApplicabilityRulesCommand.cs`
    - Replace the applicability matrix as a pending change. Rejects duplicate (Constitution,
      EntityRole) combos in the submission (R4.4) and validates each against ACTIVE lookups (R4.3).
      Stages the matrix in `PendingValuesJson` so the live matrix keeps serving consumers (R4.6).
    - _Requirements: 4.1, 4.3, 4.4, 4.6, 8.1_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Document.Ports;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.UpdateApplicabilityRules;

    public record UpdateApplicabilityRulesCommand(
        Guid Id, IReadOnlyList<ApplicabilityRuleRequest> Rules, string ActingUser)
        : IRequest<DocumentResponse>;

    public class UpdateApplicabilityRulesCommandHandler
        : IRequestHandler<UpdateApplicabilityRulesCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;
        private readonly ILookupValidationPort _lookups;

        public UpdateApplicabilityRulesCommandHandler(IDocumentRepository repository,
            IDocumentAuditRepository audit, ILookupValidationPort lookups)
        { _repository = repository; _audit = audit; _lookups = lookups; }

        public async Task<DocumentResponse> Handle(UpdateApplicabilityRulesCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            if (entity.Status is DocumentStatus.PendingModification
                || (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null))
                throw new DocumentConflictException("A change is already pending authorization."); // R3.5

            // R4.4 — reject duplicate (Constitution, EntityRole) combinations in the submission.
            var seen = new HashSet<(string, string)>();
            foreach (var r in request.Rules)
                if (!seen.Add((r.Constitution, r.EntityRole)))
                    throw new ValidationException("Duplicate applicability mapping in submission.");

            // R4.3 — each Constitution/EntityRole must be an ACTIVE lookup value.
            foreach (var r in request.Rules)
            {
                if (!await _lookups.IsActiveLookupAsync("CONSTITUTION", r.Constitution, ct))
                    throw new ValidationException($"Constitution '{r.Constitution}' is not an active lookup value.");
                if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", r.EntityRole, ct))
                    throw new ValidationException($"Entity Role '{r.EntityRole}' is not an active lookup value.");
            }

            // R4.1 / R4.6 — stage as a pending change; the live matrix keeps serving consumers.
            var pendingRules = request.Rules
                .Select(r => new DocumentApplicabilityRule
                {
                    DocumentId = entity.Id, Constitution = r.Constitution,
                    EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
                }).ToList();
            entity.PendingValuesJson = DocumentPendingPayload.ForApplicability(pendingRules);
            entity.Status = entity.Status == DocumentStatus.Active
                ? DocumentStatus.PendingModification : DocumentStatus.PendingApproval;
            entity.MakerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.ApplicabilityChange,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = entity.PendingValuesJson,
                Summary = $"Submitted applicability change for '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }

    public class UpdateApplicabilityRulesCommandValidator : AbstractValidator<UpdateApplicabilityRulesCommand>
    {
        public UpdateApplicabilityRulesCommandValidator()
        {
            RuleFor(x => x.Rules).NotNull().Must(r => r.Count > 0)
                .WithMessage("At least one applicability rule is required.");   // R4.1
            RuleForEach(x => x.Rules).ChildRules(rule =>
            {
                rule.RuleFor(r => r.Constitution).NotEmpty().WithMessage("Constitution is required.");
                rule.RuleFor(r => r.EntityRole).NotEmpty().WithMessage("Entity Role is required.");
            });
        }
    }
    ```

  - [ ] 7.5 CREATE `Finnova.Service/Document/Commands/UpdateValidationConfig/UpdateValidationConfigCommand.cs`
    - Set validation/masking config as a pending change (R9.1, R9.10). Enforces the exact messages
      "Mask pattern is required when masking is enabled" (R9.3), "Aadhaar must always be masked"
      (R9.4), "Max file size must be between 1 and 25 MB" (R9.8); validates `ValidationEngine`
      against ACTIVE lookups (R9.2); compiles `FormatRegex` with try/catch (R9.5); bounds Mask/
      Regex length (R9.6/R9.7) and the allowed MIME set (R9.9). Stores, never executes (R9.11).
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9, 9.10, 9.11, 8.1_

    ```csharp
    using System.Text.RegularExpressions;
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Document.Ports;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.UpdateValidationConfig;

    public record UpdateValidationConfigCommand(
        Guid Id, string ValidationEngine, string? FormatRegex, bool RequiresMasking,
        string? MaskPattern, int MaxFileSizeMb, IReadOnlyList<string> AllowedMimeTypes,
        string ActingUser) : IRequest<DocumentResponse>;

    public class UpdateValidationConfigCommandHandler
        : IRequestHandler<UpdateValidationConfigCommand, DocumentResponse>
    {
        private static readonly HashSet<string> AllowedMime =
            new(StringComparer.OrdinalIgnoreCase) { "PDF", "JPG", "PNG" };

        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;
        private readonly ILookupValidationPort _lookups;

        public UpdateValidationConfigCommandHandler(IDocumentRepository repository,
            IDocumentAuditRepository audit, ILookupValidationPort lookups)
        { _repository = repository; _audit = audit; _lookups = lookups; }

        public async Task<DocumentResponse> Handle(UpdateValidationConfigCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            if (entity.Status is DocumentStatus.PendingModification
                || (entity.Status is DocumentStatus.PendingApproval && entity.PendingValuesJson is not null))
                throw new DocumentConflictException("A change is already pending authorization."); // R3.5

            // R9.2 — ValidationEngine must be an ACTIVE lookup value (also blocks free text).
            if (!await _lookups.IsActiveLookupAsync("VALIDATION_ENGINE", request.ValidationEngine, ct))
                throw new ValidationException("Validation Engine is not an active lookup value.");

            // R9.8 — max file size bounds (exact message).
            if (request.MaxFileSizeMb is < 1 or > 25)
                throw new ValidationException("Max file size must be between 1 and 25 MB");

            // R9.4 — Aadhaar must always be masked.
            if (entity.IsAadhaarDocument && !request.RequiresMasking)
                throw new ValidationException("Aadhaar must always be masked");

            // R9.3 — mask pattern required when masking is enabled.
            if (request.RequiresMasking && string.IsNullOrWhiteSpace(request.MaskPattern))
                throw new ValidationException("Mask pattern is required when masking is enabled");

            // R9.6 / R9.7 — length caps.
            if (request.MaskPattern is { Length: > 200 })
                throw new ValidationException("Mask Pattern must not exceed 200 characters.");
            if (request.FormatRegex is { Length: > 500 })
                throw new ValidationException("Format Regex must not exceed 500 characters.");

            // R9.5 — regex must compile when present (stored, not executed — R9.11).
            if (!string.IsNullOrWhiteSpace(request.FormatRegex))
            {
                try { _ = new Regex(request.FormatRegex); }
                catch (ArgumentException)
                { throw new ValidationException("Format Regex is not a valid regular expression."); }
            }

            // R9.9 — allowed MIME set non-empty and within {PDF,JPG,PNG}.
            if (request.AllowedMimeTypes is null || request.AllowedMimeTypes.Count == 0
                || request.AllowedMimeTypes.Any(m => !AllowedMime.Contains(m)))
                throw new ValidationException("Allowed Mime Types must be a non-empty subset of PDF, JPG, PNG.");

            // R9.1 / R9.10 — stage as a pending change; live values unchanged until authorize.
            entity.PendingValuesJson = DocumentPendingPayload.ForValidationConfig(
                request.ValidationEngine, request.FormatRegex, request.RequiresMasking,
                request.MaskPattern, request.MaxFileSizeMb, request.AllowedMimeTypes);
            entity.Status = entity.Status == DocumentStatus.Active
                ? DocumentStatus.PendingModification : DocumentStatus.PendingApproval;
            entity.MakerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.ValidationConfigChange,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = entity.PendingValuesJson,
                Summary = $"Submitted validation/masking config for '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 7.6 CREATE `Finnova.Service/Document/Commands/AuthorizeDocument/AuthorizeDocumentCommand.cs`
    - Checker authorizes → copy pending → live, Status `Active`, bump version. Enforces the
      self-authorization guard (`ERR-DOC-001`) independent of roles (R5.5, R12.6) and the pending
      source-state guard (R5.9). Makes live values immediately consumable (R5.4).
    - _Requirements: 4.5, 5.3, 5.4, 5.5, 5.9, 8.1, 12.6_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Mappers;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.AuthorizeDocument;

    public record AuthorizeDocumentCommand(Guid Id, string ActingUser) : IRequest<DocumentResponse>;

    public class AuthorizeDocumentCommandHandler : IRequestHandler<AuthorizeDocumentCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;

        public AuthorizeDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
        { _repository = repository; _audit = audit; }

        public async Task<DocumentResponse> Handle(AuthorizeDocumentCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            // R5.9 — must be awaiting authorization.
            if (entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
                throw new DocumentConflictException("There is no pending change to act upon.");

            // R5.5 / R12.6 — self-authorization prohibited, independent of role membership.
            if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
                throw new DocumentSelfAuthorizationException();   // ERR-DOC-001

            var wasModification = entity.Status == DocumentStatus.PendingModification;

            // Copy pending edit -> live columns, then clear the pending payload (R5.3).
            if (wasModification && entity.PendingValuesJson is not null)
                DocumentPendingPayload.ApplyTo(entity, entity.PendingValuesJson);
            entity.PendingValuesJson = null;

            entity.Status = DocumentStatus.Active;       // R5.4 now consumable
            entity.CheckerId = request.ActingUser;
            if (wasModification) entity.AuthorizedVersionNumber += 1;   // R4.5-style versioning
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Approve,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = DocumentAuditSnapshot.Editable(entity),
                Summary = $"Authorized document '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 7.7 CREATE `Finnova.Service/Document/Commands/RejectDocument/RejectDocumentCommand.cs`
    - Checker rejects with a 1..500 remark (R5.6/R5.7) → `ReturnedToMaker`, retain pending, record
      the remark in audit (R8.2). Self-review blocked the same way as authorize (R5.5).
    - _Requirements: 5.6, 5.7, 5.9, 8.1, 8.2, 12.6_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.RejectDocument;

    public record RejectDocumentCommand(Guid Id, string Reason, string ActingUser) : IRequest<DocumentResponse>;

    public class RejectDocumentCommandHandler : IRequestHandler<RejectDocumentCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;

        public RejectDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
        { _repository = repository; _audit = audit; }

        public async Task<DocumentResponse> Handle(RejectDocumentCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            if (entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
                throw new DocumentConflictException("There is no pending change to act upon.");  // R5.9

            if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
                throw new DocumentSelfAuthorizationException();  // ERR-DOC-001 (self-review on reject too)

            // R5.7 — remark 1..500 after trim (also enforced by the validator as a fast-fail).
            var remark = (request.Reason ?? string.Empty).Trim();
            if (remark.Length is < 1 or > 500)
                throw new ValidationException("A rejection remark of 1 to 500 characters is required.");

            // R5.6 — return to maker, retain pending values, record the remark; live unchanged.
            entity.Status = DocumentStatus.ReturnedToMaker;
            entity.CheckerId = request.ActingUser;
            entity.RejectionReason = remark;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Reject,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = DocumentAuditSnapshot.Editable(entity),
                Summary = $"Rejected document '{entity.DocCode}'.",
                Remarks = remark,                                 // R8.2
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }

    public class RejectDocumentCommandValidator : AbstractValidator<RejectDocumentCommand>
    {
        public RejectDocumentCommandValidator()
        {
            RuleFor(x => x.Reason)
                .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length is >= 1 and <= 500)
                .WithMessage("A rejection remark of 1 to 500 characters is required.");  // R5.7
        }
    }
    ```

  - [ ] 7.8 CREATE `Finnova.Service/Document/Commands/ResubmitDocument/ResubmitDocumentCommand.cs`
    - Maker resubmits a `ReturnedToMaker` doc → `PendingApproval`, records the maker as the last
      editor of the pending change for the self-authorization prohibition (R5.10).
    - _Requirements: 5.10, 8.1_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Mappers;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.ResubmitDocument;

    public record ResubmitDocumentCommand(Guid Id, string ActingUser) : IRequest<DocumentResponse>;

    public class ResubmitDocumentCommandHandler : IRequestHandler<ResubmitDocumentCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;

        public ResubmitDocumentCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
        { _repository = repository; _audit = audit; }

        public async Task<DocumentResponse> Handle(ResubmitDocumentCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            // R5.10 — only a returned document can be resubmitted.
            if (entity.Status != DocumentStatus.ReturnedToMaker)
                throw new DocumentConflictException("Only a returned document can be resubmitted.");

            entity.Status = DocumentStatus.PendingApproval;   // R5.10
            entity.MakerId = request.ActingUser;              // last editor for self-auth prohibition
            entity.RejectionReason = null;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Submit,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = DocumentAuditSnapshot.Editable(entity),
                Summary = $"Resubmitted document '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 7.9 CREATE `Finnova.Service/Document/Commands/SetDocumentStatus/SetDocumentStatusCommand.cs`
    - Inactivate (`Active` → `Inactive`, R6.1) / reactivate (`Inactive` → `Active`, R6.4) under the
      source-state guard. The design routes these through maker-checker; this handler performs the
      authorized transition and audits it (the controller gates it to `DOC_MASTER_MAKER`; a fuller
      maker-checker on status can reuse the pending model if later required).
    - _Requirements: 6.1, 6.4, 8.1_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Mappers;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.SetDocumentStatus;

    public record SetDocumentStatusCommand(Guid Id, bool Active, string ActingUser) : IRequest<DocumentResponse>;

    public class SetDocumentStatusCommandHandler : IRequestHandler<SetDocumentStatusCommand, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;

        public SetDocumentStatusCommandHandler(IDocumentRepository repository, IDocumentAuditRepository audit)
        { _repository = repository; _audit = audit; }

        public async Task<DocumentResponse> Handle(SetDocumentStatusCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();

            if (request.Active)
            {
                // R6.4 — reactivate only from Inactive.
                if (entity.Status != DocumentStatus.Inactive)
                    throw new DocumentConflictException("Only an inactive document can be reactivated.");
                entity.Status = DocumentStatus.Active;
            }
            else
            {
                // R6.1 — inactivate only from Active.
                if (entity.Status != DocumentStatus.Active)
                    throw new DocumentConflictException("Only an active document can be inactivated.");
                entity.Status = DocumentStatus.Inactive;
            }

            entity.CheckerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = request.Active ? DocumentAuditAction.Reactivate : DocumentAuditAction.Inactivate,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = DocumentAuditSnapshot.Editable(entity),
                Summary = $"{(request.Active ? "Reactivated" : "Inactivated")} document '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 7.10 CREATE `Finnova.Service/Document/Commands/DeleteDocument/DeleteDocumentCommand.cs`
    - Hard-delete only a never-authorized `PendingApproval` doc (R7.2). Blocks an `Active` delete
      (R7.3). Wraps the usage check in a 5-second timeout and fails safe (block) on error/timeout
      (R7.5); throws `DocumentInUseException` (ERR-DOC-002) when referenced (R7.1); not-found → 404
      (R7.4). Writes one Delete audit entry on success.
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Document.Ports;
    using MediatR;
    using DocAudit = Finnova.Models.Domain.Entities.DocumentAuditEntry;

    namespace Finnova.Service.Document.Commands.DeleteDocument;

    public record DeleteDocumentCommand(Guid Id, string ActingUser) : IRequest<Unit>;

    public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, Unit>
    {
        private readonly IDocumentRepository _repository;
        private readonly IDocumentAuditRepository _audit;
        private readonly IDocumentUsageChecker _usage;

        public DeleteDocumentCommandHandler(IDocumentRepository repository,
            IDocumentAuditRepository audit, IDocumentUsageChecker usage)
        { _repository = repository; _audit = audit; _usage = usage; }

        public async Task<Unit> Handle(DeleteDocumentCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();   // R7.4

            // R7.3 — an active document must be inactivated, not deleted.
            if (entity.Status == DocumentStatus.Active)
                throw new DocumentConflictException("An active document must be inactivated rather than deleted.");

            // R7.2 — only a never-authorized PendingApproval doc is deletable. AuthorizedVersionNumber
            // == 0 confirms it was never authorized.
            if (entity.Status != DocumentStatus.PendingApproval || entity.AuthorizedVersionNumber > 0)
                throw new DocumentConflictException("Only a never-authorized pending document can be deleted.");

            // R7.1 / R7.5 — usage check with a 5-second fail-safe timeout.
            bool referenced;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                referenced = await _usage.IsReferencedByCustomerMasterAsync(entity.Id, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new DocumentConflictException("In-use status could not be confirmed. Deletion was blocked."); // R7.5
            }
            catch (Exception)
            {
                throw new DocumentConflictException("In-use status could not be confirmed. Deletion was blocked."); // R7.5
            }

            if (referenced)
                throw new DocumentInUseException();   // ERR-DOC-002 (R7.1)

            await _repository.DeleteAsync(entity, ct);   // R7.2 hard delete

            await _audit.AddAsync(new DocAudit
            {
                DocumentId = entity.Id,
                Action = DocumentAuditAction.Delete,
                OldValues = DocumentAuditSnapshot.Editable(entity),
                NewValues = null,
                Summary = $"Deleted document '{entity.DocCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return Unit.Value;
        }
    }
    ```

  - [ ] 7.11 CREATE `Finnova.Service/Document/Queries/GetDocumentsPaged/GetDocumentsPagedQuery.cs`
    - Paged/filtered admin grid (R10) with a validator for page/size bounds (R10.6/R10.7) and
      defaults (R10.4/R10.5). Parses the optional status filter from the string name.
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9_

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;

    namespace Finnova.Service.Document.Queries.GetDocumentsPaged;

    public record GetDocumentsPagedQuery(
        string? Search, string? Category, string? Status, int Page = 1, int PageSize = 20)
        : IRequest<PaginatedResponse<DocumentResponse>>;

    public class GetDocumentsPagedQueryHandler
        : IRequestHandler<GetDocumentsPagedQuery, PaginatedResponse<DocumentResponse>>
    {
        private readonly IDocumentRepository _repository;
        public GetDocumentsPagedQueryHandler(IDocumentRepository repository) => _repository = repository;

        public async Task<PaginatedResponse<DocumentResponse>> Handle(
            GetDocumentsPagedQuery request, CancellationToken ct)
        {
            DocumentStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status)
                && Enum.TryParse<DocumentStatus>(request.Status, ignoreCase: true, out var parsed))
                status = parsed;

            var (items, total) = await _repository.GetPagedAsync(
                request.Search, request.Category, status, request.Page, request.PageSize, ct);

            var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
            return new PaginatedResponse<DocumentResponse>(
                items.ToResponseList(), total, request.Page, request.PageSize, totalPages);  // R10.3/10.9
        }
    }

    public class GetDocumentsPagedQueryValidator : AbstractValidator<GetDocumentsPagedQuery>
    {
        public GetDocumentsPagedQueryValidator()
        {
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
                .WithMessage("Page number is out of range.");                       // R10.6
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
                .WithMessage("Page size is out of range.");                         // R10.7
        }
    }
    ```

  - [ ] 7.12 CREATE `Finnova.Service/Document/Queries/GetDocumentById/GetDocumentByIdQuery.cs`
    - Single read; returns an `Inactive` doc too (with its status indicator, R6.3). 404 when
      missing.
    - _Requirements: 6.3, 3.3_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.Document.Queries.GetDocumentById;

    public record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentResponse>;

    public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, DocumentResponse>
    {
        private readonly IDocumentRepository _repository;
        public GetDocumentByIdQueryHandler(IDocumentRepository repository) => _repository = repository;

        public async Task<DocumentResponse> Handle(GetDocumentByIdQuery request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
                ?? throw new DocumentNotFoundException();
            return entity.ToResponse();   // includes Inactive status (R6.3)
        }
    }
    ```

  - [ ] 7.13 CREATE `Finnova.Service/Document/Queries/GetPendingReview/GetPendingReviewQuery.cs`
    - Live vs pending for the checker (R5.1). Errors when the doc is missing or has no pending
      change (R5.2). Builds the Pending projection by cloning the live entity and applying the
      pending payload (for an Active-doc edit) or returning the row itself (never-authorized).
    - _Requirements: 5.1, 5.2_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Internal;
    using Finnova.Service.Mappers;
    using MediatR;
    using FluentValidation;
    using DocEntity = Finnova.Models.Domain.Entities.Document;

    namespace Finnova.Service.Document.Queries.GetPendingReview;

    public record GetPendingReviewQuery(Guid Id) : IRequest<DocumentPendingReviewResponse>;

    public class GetPendingReviewQueryHandler
        : IRequestHandler<GetPendingReviewQuery, DocumentPendingReviewResponse>
    {
        private readonly IDocumentRepository _repository;
        public GetPendingReviewQueryHandler(IDocumentRepository repository) => _repository = repository;

        public async Task<DocumentPendingReviewResponse> Handle(GetPendingReviewQuery request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct);

            // R5.2 — missing or not pending => no comparison payload.
            if (entity is null
                || entity.Status is not (DocumentStatus.PendingApproval or DocumentStatus.PendingModification))
                throw new ValidationException("There is no pending change to review.");

            var liveResponse = entity.ToResponse();

            DocumentResponse pendingResponse;
            if (entity.Status == DocumentStatus.PendingModification && entity.PendingValuesJson is not null)
            {
                // Build a shallow clone of the live entity and apply the pending payload onto it.
                var clone = Clone(entity);
                DocumentPendingPayload.ApplyTo(clone, entity.PendingValuesJson);
                pendingResponse = clone.ToResponse();
            }
            else
            {
                // Never-authorized PendingApproval: the row itself holds the proposed values.
                pendingResponse = liveResponse;
            }

            return new DocumentPendingReviewResponse(
                entity.Id, entity.Status.ToString(), liveResponse, pendingResponse);  // R5.1
        }

        private static DocEntity Clone(DocEntity d) => new()
        {
            Id = d.Id, DocCode = d.DocCode, DocName = d.DocName, DocShortName = d.DocShortName,
            Description = d.Description, DocCategory = d.DocCategory, ApplicableEntity = d.ApplicableEntity,
            IsMandatory = d.IsMandatory, HasExpiry = d.HasExpiry, RequiresMasking = d.RequiresMasking,
            IsAadhaarDocument = d.IsAadhaarDocument, ValidationEngine = d.ValidationEngine,
            FormatRegex = d.FormatRegex, MaskPattern = d.MaskPattern, MaxFileSizeMb = d.MaxFileSizeMb,
            AllowedMimeTypes = d.AllowedMimeTypes, Status = d.Status, MakerId = d.MakerId,
            CheckerId = d.CheckerId, AuthorizedVersionNumber = d.AuthorizedVersionNumber,
            RejectionReason = d.RejectionReason, CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
            ApplicabilityRules = d.ApplicabilityRules.Select(r => new Finnova.Models.Domain.Entities.DocumentApplicabilityRule
            {
                Id = r.Id, DocumentId = r.DocumentId, Constitution = r.Constitution,
                EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
            }).ToList()
        };
    }
    ```

  - [ ] 7.14 CREATE `Finnova.Service/Document/Queries/GetDocumentAuditTrail/GetDocumentAuditTrailQuery.cs`
    - Audit entries newest-first (R8.4); empty for an unknown id (R8.6).
    - _Requirements: 8.4, 8.6_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.Document.Queries.GetDocumentAuditTrail;

    public record GetDocumentAuditTrailQuery(Guid Id) : IRequest<List<DocumentAuditEntryResponse>>;

    public class GetDocumentAuditTrailQueryHandler
        : IRequestHandler<GetDocumentAuditTrailQuery, List<DocumentAuditEntryResponse>>
    {
        private readonly IDocumentAuditRepository _audit;
        public GetDocumentAuditTrailQueryHandler(IDocumentAuditRepository audit) => _audit = audit;

        public async Task<List<DocumentAuditEntryResponse>> Handle(
            GetDocumentAuditTrailQuery request, CancellationToken ct)
        {
            var entries = await _audit.GetByDocumentIdAsync(request.Id, ct);   // newest-first (R8.4), [] if none (R8.6)
            return entries.Select(e => e.ToResponse()).ToList();
        }
    }
    ```

  - [ ] 7.15 CREATE `Finnova.Service/Document/Queries/GetActiveDocumentsForConsumption/GetActiveDocumentsForConsumptionQuery.cs`
    - Customer Master consumption (R11). Unknown Constitution/Entity Role is a validation error, not
      an empty set (R11.6). Returns Active-only, applicability-matched docs with the resolved
      mandatory flag (R11.2), live values only (R11.4); empty set allowed (R11.5).
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

    ```csharp
    using Finnova.Models.Contracts.Documents;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Ports;
    using Finnova.Service.Mappers;
    using FluentValidation;
    using MediatR;

    namespace Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;

    public record GetActiveDocumentsForConsumptionQuery(string Constitution, string EntityRole)
        : IRequest<List<ActiveDocumentForConsumptionResponse>>;

    public class GetActiveDocumentsForConsumptionQueryHandler
        : IRequestHandler<GetActiveDocumentsForConsumptionQuery, List<ActiveDocumentForConsumptionResponse>>
    {
        private readonly IDocumentRepository _repository;
        private readonly ILookupValidationPort _lookups;

        public GetActiveDocumentsForConsumptionQueryHandler(
            IDocumentRepository repository, ILookupValidationPort lookups)
        { _repository = repository; _lookups = lookups; }

        public async Task<List<ActiveDocumentForConsumptionResponse>> Handle(
            GetActiveDocumentsForConsumptionQuery request, CancellationToken ct)
        {
            // R11.6 — unknown Constitution / Entity Role is a validation error, not an empty set.
            if (!await _lookups.IsActiveLookupAsync("CONSTITUTION", request.Constitution, ct))
                throw new ValidationException($"Unknown Constitution '{request.Constitution}'.");
            if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.EntityRole, ct))
                throw new ValidationException($"Unknown Entity Role '{request.EntityRole}'.");

            // R11.1/R11.3 — Active only, applicability matrix must include the pair; live values only (R11.4).
            var docs = await _repository.GetActiveForConsumptionAsync(
                request.Constitution, request.EntityRole, ct);

            return docs.Select(d =>
            {
                var rule = d.ApplicabilityRules.First(r =>
                    r.Constitution == request.Constitution && r.EntityRole == request.EntityRole);
                return d.ToConsumptionResponse(rule.IsMandatory);   // R11.2 resolved mandatory
            }).ToList();   // empty set allowed (R11.5)
        }
    }
    ```

- [ ] 8. Backend controller + middleware branch + Program.cs + gateway alias

  - [ ] 8.1 CREATE `Finnova.SystemAdminService/Controllers/DocumentController.cs`
    - Full controller mirroring `CourtController` (attribute routing, `IMediator`, `GetActingUser()`
      from the JWT). Maker-only writes use `DOC_MASTER_MAKER`; checker-only authorize/reject/review
      use `DOC_MASTER_CHECKER`; reads require authentication only.
    - _Requirements: 1.1, 3.1, 4.1, 5.1, 5.3, 5.6, 5.10, 6.1, 6.4, 7.1, 8.4, 9.1, 10.1, 11.1, 12.2, 12.3_

    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.Documents;
    using Finnova.Service.Document.Commands.AuthorizeDocument;
    using Finnova.Service.Document.Commands.CreateDocument;
    using Finnova.Service.Document.Commands.DeleteDocument;
    using Finnova.Service.Document.Commands.RejectDocument;
    using Finnova.Service.Document.Commands.ResubmitDocument;
    using Finnova.Service.Document.Commands.SetDocumentStatus;
    using Finnova.Service.Document.Commands.UpdateApplicabilityRules;
    using Finnova.Service.Document.Commands.UpdateDocument;
    using Finnova.Service.Document.Commands.UpdateValidationConfig;
    using Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;
    using Finnova.Service.Document.Queries.GetDocumentAuditTrail;
    using Finnova.Service.Document.Queries.GetDocumentById;
    using Finnova.Service.Document.Queries.GetDocumentsPaged;
    using Finnova.Service.Document.Queries.GetPendingReview;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.SystemAdminService.Controllers;

    /// <summary>Document Master endpoints (FINNOVA-49/50/52). Maker/Checker split per R12.</summary>
    [ApiController]
    [Route("api/document")]
    public class DocumentController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DocumentController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [Authorize]                                   // any authenticated SystemAdmin user (R10, R12)
        [ProducesResponseType(typeof(PaginatedResponse<DocumentResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<DocumentResponse>>> GetPaged(
            [FromQuery] string? search, [FromQuery] string? category, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDocumentsPagedQuery(search, category, status, page, pageSize)));

        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DocumentResponse>> GetById(Guid id)
            => Ok(await _mediator.Send(new GetDocumentByIdQuery(id)));

        [HttpGet("{id:guid}/review")]
        [Authorize(Policy = "DOC_MASTER_CHECKER")]
        [ProducesResponseType(typeof(DocumentPendingReviewResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<DocumentPendingReviewResponse>> GetReview(Guid id)
            => Ok(await _mediator.Send(new GetPendingReviewQuery(id)));

        [HttpGet("{id:guid}/audit")]
        [Authorize]
        [ProducesResponseType(typeof(List<DocumentAuditEntryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DocumentAuditEntryResponse>>> GetAudit(Guid id)
            => Ok(await _mediator.Send(new GetDocumentAuditTrailQuery(id)));

        [HttpGet("consume")]
        [Authorize]
        [ProducesResponseType(typeof(List<ActiveDocumentForConsumptionResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ActiveDocumentForConsumptionResponse>>> Consume(
            [FromQuery] string constitution, [FromQuery] string entityRole)
            => Ok(await _mediator.Send(new GetActiveDocumentsForConsumptionQuery(constitution, entityRole)));

        [HttpPost]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DocumentResponse>> Create([FromBody] CreateDocumentRequest r)
        {
            var result = await _mediator.Send(new CreateDocumentCommand(
                r.DocCode, r.DocName, r.DocShortName, r.Description, r.DocCategory, r.ApplicableEntity,
                r.IsMandatory, r.HasExpiry, r.RequiresMasking, r.IsAadhaarDocument, r.MaxFileSizeMb,
                GetActingUser()));
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> Update(Guid id, [FromBody] UpdateDocumentRequest r)
            => Ok(await _mediator.Send(new UpdateDocumentCommand(
                id, r.DocName, r.DocShortName, r.Description, r.DocCategory, r.ApplicableEntity,
                r.IsMandatory, r.HasExpiry, GetActingUser())));

        [HttpPut("{id:guid}/applicability")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> UpdateApplicability(
            Guid id, [FromBody] UpdateApplicabilityRulesRequest r)
            => Ok(await _mediator.Send(new UpdateApplicabilityRulesCommand(id, r.Rules, GetActingUser())));

        [HttpPut("{id:guid}/validation-config")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> UpdateValidationConfig(
            Guid id, [FromBody] UpdateValidationConfigRequest r)
            => Ok(await _mediator.Send(new UpdateValidationConfigCommand(
                id, r.ValidationEngine, r.FormatRegex, r.RequiresMasking, r.MaskPattern,
                r.MaxFileSizeMb, r.AllowedMimeTypes, GetActingUser())));

        [HttpPost("{id:guid}/authorize")]
        [Authorize(Policy = "DOC_MASTER_CHECKER")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]   // ERR-DOC-001 / not-pending
        public async Task<ActionResult<DocumentResponse>> Authorize(Guid id)
            => Ok(await _mediator.Send(new AuthorizeDocumentCommand(id, GetActingUser())));

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = "DOC_MASTER_CHECKER")]
        public async Task<ActionResult<DocumentResponse>> Reject(Guid id, [FromBody] RejectDocumentRequest r)
            => Ok(await _mediator.Send(new RejectDocumentCommand(id, r.Reason, GetActingUser())));

        [HttpPost("{id:guid}/resubmit")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> Resubmit(Guid id)
            => Ok(await _mediator.Send(new ResubmitDocumentCommand(id, GetActingUser())));

        [HttpPost("{id:guid}/inactivate")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> Inactivate(Guid id)
            => Ok(await _mediator.Send(new SetDocumentStatusCommand(id, Active: false, GetActingUser())));

        [HttpPost("{id:guid}/reactivate")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        public async Task<ActionResult<DocumentResponse>> Reactivate(Guid id)
            => Ok(await _mediator.Send(new SetDocumentStatusCommand(id, Active: true, GetActingUser())));

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "DOC_MASTER_MAKER")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]   // ERR-DOC-002 / active delete
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteDocumentCommand(id, GetActingUser()));
            return NoContent();
        }

        /// <summary>Resolves the acting user from the validated JWT principal (mirrors
        /// CourtController.GetActingAdmin), so the audit actor and self-auth check cannot be
        /// spoofed by the request body.</summary>
        private string GetActingUser()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? User.Identity?.Name
               ?? string.Empty;
    }
    ```

  - [ ] 8.2 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`
    - Add a path-scoped `/api/document` validation/fallback code pair chained onto the existing
      `validationAssetCode`/`fallbackAssetCode` chain, and a typed Document branch to the
      `ex switch`, alongside the existing Court/Entity/Asset branches. Two edits:
    - _Requirements: 2.2, 3.3, 5.5, 7.1, 7.3, 12.6_

    **Edit A** — right after the existing `isAsset` / `validationAssetCode` / `fallbackAssetCode`
    lines (the last of the path-scoped chain, just before the `var (status, code, detail) = ex switch`
    line), add:

    ```csharp
    var isDocument = ctx.Request.Path.StartsWithSegments("/api/document", StringComparison.OrdinalIgnoreCase);
    var validationDocumentCode = isDocument ? "ERR-DOC-400" : validationAssetCode;
    var fallbackDocumentCode = isDocument ? "ERR-DOC-500" : fallbackAssetCode;
    ```

    **Edit B** — inside the `ex switch`, add the Document arms next to the Asset branch (before the
    shared `FluentValidation.ValidationException` / `_` fallback arms), and change those two shared
    arms to use the new Document codes:

    ```csharp
                    // ---- new Document Master branch (typed, no message sniffing) ----
                    DocumentSelfAuthorizationException => (409, DocumentSelfAuthorizationException.ErrorCode, ex.Message), // ERR-DOC-001
                    DocumentInUseException            => (409, DocumentInUseException.ErrorCode, ex.Message),              // ERR-DOC-002
                    DocumentDuplicateCodeException    => (409, DocumentDuplicateCodeException.ErrorCode, ex.Message),      // ERR-DOC-409
                    DocumentConflictException         => (409, "ERR-DOC-409", ex.Message),
                    DocumentNotFoundException         => (404, "ERR-DOC-404", ex.Message),

                    // ---- shared: validation failures and unhandled fallback (path-scoped code) ----
                    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationDocumentCode, v.Message),
                    _ => (StatusCodes.Status500InternalServerError, fallbackDocumentCode, "Unexpected error.")
    ```

    > Note: `Finnova.Models.Domain.Exceptions` is already imported at the top of the file, so the
    > `Document*Exception` types resolve without a new using. Replace ONLY the final two shared arms
    > (`FluentValidation.ValidationException v => ...` and `_ => ...`) with the versions above —
    > leave every existing Lookup/Nationality/Org/DCN/Court/Entity/User/DraweeBank/Asset arm intact.

  - [ ] 8.3 MODIFY `Finnova.SystemAdminService/Program.cs`
    - Two edits: add the two role policies inside the existing `AddAuthorization` call, and register
      the two ports next to the other `AddScoped` service registrations (before `var app = builder.Build();`).
    - _Requirements: 12.2, 12.3, 1.8, 7.1_

    **Edit A** — replace the existing single-policy registration:

    ```csharp
    // SystemAdmin authorization policy — role claim "SystemAdmin" (R7.2/7.3)
    builder.Services.AddAuthorization(options =>
        options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin")));
    ```

    with the version that adds the two Document policies (R12.2/R12.3):

    ```csharp
    // SystemAdmin authorization policy — role claim "SystemAdmin" (R7.2/7.3)
    // Document Master maker/checker policies (R12.2/R12.3). Kept exclusive pending confirmation
    // (design decision #6): SystemAdmin is NOT accepted as a super-role here.
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));
        options.AddPolicy("DOC_MASTER_MAKER", p => p.RequireRole("DOC_MASTER_MAKER"));
        options.AddPolicy("DOC_MASTER_CHECKER", p => p.RequireRole("DOC_MASTER_CHECKER"));
    });
    ```

    **Edit B** — add the Document port registrations alongside the other `builder.Services.AddScoped`
    lines (e.g. right after the `IAssetCodeGenerator` registration), before `var app = builder.Build();`:

    ```csharp
    // Document Master ports. LookupValidationAdapter reads ACTIVE lookups; the usage checker is a
    // dev stub (resolved assumption A3) to be replaced by the Customer Master adapter (FINNOVA-23).
    builder.Services.AddScoped<Finnova.Service.Document.Ports.ILookupValidationPort,
        Finnova.Service.Document.Ports.LookupValidationAdapter>();
    builder.Services.AddScoped<Finnova.Service.Document.Ports.IDocumentUsageChecker,
        Finnova.Service.Document.Ports.StubDocumentUsageChecker>();
    ```

  - [ ] 8.4 MODIFY `Finnova.ApiGateway/appsettings.json`
    - Add two alias routes inside `ReverseProxy.Routes`, alongside the other `ua-*-alias-route` /
      `ua-*-alias-root` entries (e.g. next to `ua-asset-alias-*`). No cluster change —
      `systemadmin-cluster` already targets the host, and YARP forwards `Authorization` by default.
    - _Requirements: 12.1, 13.12_

    ```jsonc
    "ua-document-alias-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/document/{**catch-all}"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    "ua-document-alias-root": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/document"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    ```

- [ ] 9. Backend tests (Finnova.Tests)

  Property tests use **FsCheck.Xunit** (min 100 iterations, FsCheck default) with in-memory fake
  repositories and fake ports, mirroring `InMemoryCourtRepository`/`InMemoryCourtAuditRepository`.
  Each property test is tagged `// Feature: document-master-management, Property {n}: {text}`.
  Example/edge tests use xUnit with mocked dependencies; integration tests use
  `WebApplicationFactory<Program>` with dev-signed JWTs.

  - [ ] 9.1 CREATE `Finnova.Tests/Infrastructure/InMemoryDocumentRepository.cs`
    - In-memory `IDocumentRepository` fake (clone on read/write, same style as
      `InMemoryCourtRepository`), used by all property/unit tests.
    - _Requirements: test harness for Properties 1–13_

    ```csharp
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Tests.Infrastructure;

    public sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        private readonly List<Document> _store = new();

        public IReadOnlyList<Document> Snapshot() => _store.Select(Clone).ToList();
        public int Count => _store.Count;
        public void Seed(Document d) => _store.Add(Clone(d));

        private static Document Clone(Document d) => new()
        {
            Id = d.Id, DocCode = d.DocCode, DocName = d.DocName, DocShortName = d.DocShortName,
            Description = d.Description, DocCategory = d.DocCategory, ApplicableEntity = d.ApplicableEntity,
            IsMandatory = d.IsMandatory, HasExpiry = d.HasExpiry, RequiresMasking = d.RequiresMasking,
            IsAadhaarDocument = d.IsAadhaarDocument, ValidationEngine = d.ValidationEngine,
            FormatRegex = d.FormatRegex, MaskPattern = d.MaskPattern, MaxFileSizeMb = d.MaxFileSizeMb,
            AllowedMimeTypes = d.AllowedMimeTypes, Status = d.Status, MakerId = d.MakerId,
            CheckerId = d.CheckerId, AuthorizedVersionNumber = d.AuthorizedVersionNumber,
            RejectionReason = d.RejectionReason, PendingValuesJson = d.PendingValuesJson,
            CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
            ApplicabilityRules = d.ApplicabilityRules.Select(r => new DocumentApplicabilityRule
            {
                Id = r.Id, DocumentId = r.DocumentId, Constitution = r.Constitution,
                EntityRole = r.EntityRole, IsMandatory = r.IsMandatory
            }).ToList()
        };

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        { var f = _store.FirstOrDefault(x => x.Id == id); return Task.FromResult(f is null ? null : Clone(f)); }

        public Task<Document?> GetByIdWithRulesAsync(Guid id, CancellationToken ct = default)
        { var f = _store.FirstOrDefault(x => x.Id == id); return Task.FromResult(f is null ? null : Clone(f)); }

        public Task<List<Document>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());

        public Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate, CancellationToken ct = default)
            => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

        public Task AddAsync(Document entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }

        public Task UpdateAsync(Document entity, CancellationToken ct = default)
        { _store.RemoveAll(x => x.Id == entity.Id); _store.Add(Clone(entity)); return Task.CompletedTask; }

        public Task DeleteAsync(Document entity, CancellationToken ct = default)
        { _store.RemoveAll(x => x.Id == entity.Id); return Task.CompletedTask; }

        public Task<int> CountAsync(Expression<Func<Document, bool>>? predicate = null, CancellationToken ct = default)
            => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

        public Task<(List<Document> Items, int Total)> GetPagedAsync(
            string? search, string? docCategory, DocumentStatus? status, int page, int pageSize, CancellationToken ct = default)
        {
            IEnumerable<Document> q = _store;
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.DocCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                              || x.DocName.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(docCategory)) q = q.Where(x => x.DocCategory == docCategory);
            if (status.HasValue) q = q.Where(x => x.Status == status.Value);
            var all = q.ToList();
            var items = all.OrderBy(x => x.DocCode).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
            return Task.FromResult((items, all.Count));
        }

        public Task<List<Document>> GetActiveForConsumptionAsync(string constitution, string entityRole, CancellationToken ct = default)
        {
            var items = _store.Where(x => x.Status == DocumentStatus.Active
                    && x.ApplicabilityRules.Any(r => r.Constitution == constitution && r.EntityRole == entityRole))
                .OrderBy(x => x.DocCode).ThenBy(x => x.Id).Select(Clone).ToList();
            return Task.FromResult(items);
        }

        public Task<bool> ExistsByCodeAsync(string docCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = docCode.Trim();
            return Task.FromResult(_store.Any(x =>
                string.Equals(x.DocCode.Trim(), c, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || x.Id != excludeId)));
        }
    }
    ```

  - [ ] 9.2 CREATE `Finnova.Tests/Infrastructure/DocumentTestDoubles.cs`
    - In-memory audit repo (immutable, mirrors `InMemoryCourtAuditRepository`) + configurable fake
      ports for lookup validation and usage checking.
    - _Requirements: test harness for Properties 1–13, R7.5_

    ```csharp
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Document.Ports;

    namespace Finnova.Tests.Infrastructure;

    public sealed class InMemoryDocumentAuditRepository : IDocumentAuditRepository
    {
        private readonly List<DocumentAuditEntry> _store = new();
        public int Count => _store.Count;
        public IReadOnlyList<DocumentAuditEntry> Snapshot() => _store.ToList();

        public Task AddAsync(DocumentAuditEntry entry, CancellationToken ct = default)
        { _store.Add(entry); return Task.CompletedTask; }

        public Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(_store.Where(x => x.DocumentId == documentId)
                .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).ToList());
    }

    /// <summary>Fake lookup port. Treats any code in the configured active set (per type) as active.</summary>
    public sealed class FakeLookupValidationPort : ILookupValidationPort
    {
        private readonly Dictionary<string, HashSet<string>> _active = new(StringComparer.OrdinalIgnoreCase);
        public FakeLookupValidationPort AllowAll { get; private set; }

        public void SetActive(string lookupType, params string[] codes)
            => _active[lookupType] = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

        private bool _allowAll;
        public FakeLookupValidationPort WithAllowAll() { _allowAll = true; return this; }

        public Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default)
        {
            if (_allowAll) return Task.FromResult(!string.IsNullOrWhiteSpace(code));
            return Task.FromResult(_active.TryGetValue(lookupType, out var set)
                && code is not null && set.Contains(code));
        }
    }

    /// <summary>Fake usage checker: reports in-use, not-in-use, throws, or delays past the 5s timeout.</summary>
    public sealed class FakeDocumentUsageChecker : IDocumentUsageChecker
    {
        public bool Referenced { get; set; }
        public bool Throw { get; set; }
        public TimeSpan? Delay { get; set; }

        public async Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default)
        {
            if (Delay is { } d) await Task.Delay(d, ct);   // used with CancelAfter(5s) to exercise R7.5
            if (Throw) throw new InvalidOperationException("usage check failed");
            return Referenced;
        }
    }
    ```

  - [ ]* 9.3 CREATE `Finnova.Tests/Unit/Document/DocumentMakerCheckerProperties.cs`
    - FsCheck property tests for the maker-checker/uniqueness/validation properties. Provides the
      generators and three fully-worked properties (1, 2, 8) that exercise the self-auth guard,
      DocCode uniqueness, and pending-edit isolation. Add the remaining properties in the same file
      in the same style (see list below).
    - **Property 1 (self-auth), Property 2 (uniqueness), Property 8 (pending isolation)**
    - **Validates: Requirements 5.5, 12.6 (P1); 2.1, 2.2 (P2); 3.1, 3.2, 5.3, 5.4, 11.4 (P8)**

    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.Document.Commands.AuthorizeDocument;
    using Finnova.Service.Document.Commands.CreateDocument;
    using Finnova.Service.Document.Commands.UpdateDocument;
    using Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;
    using Finnova.Tests.Infrastructure;
    using Xunit;

    namespace Finnova.Tests.Unit.Document;

    public class DocumentMakerCheckerProperties
    {
        // Generators: non-empty alphanumeric-hyphen codes, 1..20 chars.
        private static Arbitrary<string> DocCodeArb() =>
            Arb.From(Gen.Choose(1, 20).SelectMany(len =>
                Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-".ToCharArray())
                   .ListOf(len).Select(cs => new string(cs.ToArray()))));

        private static (CreateDocumentCommandHandler handler,
                        InMemoryDocumentRepository repo,
                        InMemoryDocumentAuditRepository audit) NewCreateHandler()
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var lookups = new FakeLookupValidationPort().WithAllowAll();
            return (new CreateDocumentCommandHandler(repo, audit, lookups), repo, audit);
        }

        // Feature: document-master-management, Property 1: Maker cannot authorize or reject own change
        [Property(MaxTest = 100)]
        public Property MakerCannotAuthorizeOwnChange()
        {
            return Prop.ForAll(DocCodeArb(), code =>
            {
                var repo = new InMemoryDocumentRepository();
                var audit = new InMemoryDocumentAuditRepository();
                var id = Guid.NewGuid();
                repo.Seed(new Finnova.Models.Domain.Entities.Document
                {
                    Id = id, DocCode = code, DocName = "n", DocCategory = "KYC",
                    ApplicableEntity = "CUSTOMER", Status = DocumentStatus.PendingApproval,
                    MakerId = "user-1"
                });
                var handler = new AuthorizeDocumentCommandHandler(repo, audit);

                var threw = false;
                try { handler.Handle(new AuthorizeDocumentCommand(id, "user-1"), default).GetAwaiter().GetResult(); }
                catch (DocumentSelfAuthorizationException) { threw = true; }

                var after = repo.Snapshot().Single(x => x.Id == id);
                return (threw && after.Status == DocumentStatus.PendingApproval && audit.Count == 0)
                    .ToProperty();
            });
        }

        // Feature: document-master-management, Property 2: DocCode uniqueness is trimmed and case-insensitive
        [Property(MaxTest = 100)]
        public Property DocCodeUniquenessIsTrimmedAndCaseInsensitive()
        {
            return Prop.ForAll(DocCodeArb(), code =>
            {
                var (handler, repo, _) = NewCreateHandler();
                handler.Handle(Cmd(code), default).GetAwaiter().GetResult();

                var dup = $"  {code.ToLowerInvariant()}  ";   // whitespace + different case
                var threw = false;
                try { handler.Handle(Cmd(dup), default).GetAwaiter().GetResult(); }
                catch (DocumentDuplicateCodeException) { threw = true; }

                return (threw && repo.Count == 1).ToProperty();
            });

            static CreateDocumentCommand Cmd(string code) => new(
                code, "Name", null, null, "KYC", "CUSTOMER",
                null, null, null, null, null, "maker-1");
        }

        // Feature: document-master-management, Property 8: Pending edit never alters live values until authorize
        [Property(MaxTest = 100)]
        public Property PendingEditDoesNotAlterLiveUntilAuthorize()
        {
            return Prop.ForAll(Arb.From(Gen.Elements("Old Name", "First", "Base")), liveName =>
            {
                var repo = new InMemoryDocumentRepository();
                var audit = new InMemoryDocumentAuditRepository();
                var lookups = new FakeLookupValidationPort().WithAllowAll();
                var id = Guid.NewGuid();
                repo.Seed(new Finnova.Models.Domain.Entities.Document
                {
                    Id = id, DocCode = "DOC-X", DocName = liveName, DocCategory = "KYC",
                    ApplicableEntity = "CUSTOMER", Status = DocumentStatus.Active,
                    MakerId = "maker-1", AuthorizedVersionNumber = 1,
                    ApplicabilityRules = { new DocumentApplicabilityRule
                        { DocumentId = id, Constitution = "Individual", EntityRole = "CUSTOMER", IsMandatory = true } }
                });

                var update = new UpdateDocumentCommandHandler(repo, audit, lookups);
                update.Handle(new UpdateDocumentCommand(
                    id, "New Name", null, null, "KYC", "CUSTOMER", true, false, "maker-1"), default)
                    .GetAwaiter().GetResult();

                // After edit (pending), consumption still sees the live name.
                var consume = new GetActiveDocumentsForConsumptionQueryHandler(repo, lookups);
                var beforeAuth = consume.Handle(
                    new GetActiveDocumentsForConsumptionQuery("Individual", "CUSTOMER"), default)
                    .GetAwaiter().GetResult();

                // A different user authorizes.
                var authorize = new AuthorizeDocumentCommandHandler(repo, audit);
                authorize.Handle(new AuthorizeDocumentCommand(id, "checker-1"), default).GetAwaiter().GetResult();
                var afterAuth = consume.Handle(
                    new GetActiveDocumentsForConsumptionQuery("Individual", "CUSTOMER"), default)
                    .GetAwaiter().GetResult();

                return (beforeAuth.Single().DocName == liveName
                        && afterAuth.Single().DocName == "New Name").ToProperty();
            });
        }
    }
    ```

  - [ ]* 9.4 CREATE `Finnova.Tests/Unit/Document/DocumentValidationProperties.cs`
    - FsCheck property tests for the validation-config and query/audit properties. Implement one
      property test per remaining property, each tagged with its number. Representative worked
      example below (Property 5 — Max file size bounds); add Properties 3, 4, 6, 7, 9, 10, 11, 12,
      13 in the same file following this shape.
    - **Property 5 (worked) + Properties 3, 4, 6, 7, 9, 10, 11, 12, 13 (to add)**
    - **Validates: Requirements 1.7, 9.8 (P5); 9.4 (P3); 9.3 (P4); 4.4 (P6); 9.5 (P7); 5.9/6.1/6.4/7.2/7.3 (P9); 6.2/11.1/11.3 (P10); 10.3/10.8/10.9 (P11); 8.4 (P12); 8.1/8.2/8.3 (P11-audit)**

    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.Document.Commands.UpdateValidationConfig;
    using Finnova.Tests.Infrastructure;
    using FluentValidation;

    namespace Finnova.Tests.Unit.Document;

    public class DocumentValidationProperties
    {
        // Feature: document-master-management, Property 5: Max file size is bounded to 1..25
        [Property(MaxTest = 100)]
        public Property MaxFileSizeBounded()
        {
            return Prop.ForAll(Arb.From(Gen.Choose(-50, 100)), size =>
            {
                var repo = new InMemoryDocumentRepository();
                var audit = new InMemoryDocumentAuditRepository();
                var lookups = new FakeLookupValidationPort().WithAllowAll();
                var id = Guid.NewGuid();
                repo.Seed(new Finnova.Models.Domain.Entities.Document
                {
                    Id = id, DocCode = "DOC-X", DocName = "n", DocCategory = "KYC",
                    ApplicableEntity = "CUSTOMER", Status = DocumentStatus.Active,
                    MakerId = "maker-1", AuthorizedVersionNumber = 1
                });
                var handler = new UpdateValidationConfigCommandHandler(repo, audit, lookups);

                var inRange = size is >= 1 and <= 25;
                var threwWithMessage = false;
                try
                {
                    handler.Handle(new UpdateValidationConfigCommand(
                        id, "NONE", null, false, null, size, new[] { "PDF" }, "maker-1"), default)
                        .GetAwaiter().GetResult();
                }
                catch (ValidationException ex)
                {
                    threwWithMessage = ex.Message.Contains("Max file size must be between 1 and 25 MB");
                }

                // Accepted iff in range; otherwise rejected with the exact message.
                return (inRange || threwWithMessage).ToProperty();
            });
        }

        // Add here, one [Property(MaxTest = 100)] method each, tagged with its number:
        //  Property 3  — Aadhaar documents are always masked (IsAadhaarDocument && !RequiresMasking => reject
        //                "Aadhaar must always be masked").
        //  Property 4  — Mask pattern required when masking enabled (RequiresMasking && blank mask => reject
        //                "Mask pattern is required when masking is enabled").
        //  Property 6  — Applicability combination uniqueness (duplicate (Constitution, EntityRole) => whole
        //                submission rejected, nothing persisted).
        //  Property 7  — Format regex must compile when present (uncompilable regex => reject, nothing persisted).
        //  Property 9  — Status transition legality (authorize/reject only from pending; resubmit only from
        //                ReturnedToMaker; inactivate only from Active; reactivate only from Inactive; delete
        //                only from never-authorized PendingApproval).
        //  Property 10 — Consumption returns a doc iff Active && applicability contains the pair.
        //  Property 11 — Paginated query echoes page/size, correct Total, <= pageSize items, DocCode+Id order.
        //  Property 12 — Audit trail ordering deterministic newest-first (ChangedAtUtc desc, Id desc).
        //  Property 11-audit — Exactly one audit entry per persisted action; none on a rejected action.
    }
    ```

  - [ ]* 9.5 CREATE `Finnova.Tests/Unit/Document/DocumentHandlerUnitTests.cs`
    - xUnit example/edge tests (Arrange-Act-Assert, in-memory fakes). Implement the cases from the
      design's Testing Strategy; representative cases below, add the rest in the same file.
    - _Requirements: 2.2, 3.3, 3.5, 5.5, 6.1, 6.4, 7.1, 7.2, 7.3, 7.5, 9.3, 9.4, 9.5, 11.5, 11.6_

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.Document.Commands.DeleteDocument;
    using Finnova.Service.Document.Commands.UpdateValidationConfig;
    using Finnova.Tests.Infrastructure;
    using FluentValidation;
    using Xunit;

    namespace Finnova.Tests.Unit.Document;

    public class DocumentHandlerUnitTests
    {
        private static Finnova.Models.Domain.Entities.Document Seeded(
            InMemoryDocumentRepository repo, DocumentStatus status, int version = 0, bool aadhaar = false)
        {
            var id = Guid.NewGuid();
            var d = new Finnova.Models.Domain.Entities.Document
            {
                Id = id, DocCode = "DOC-X", DocName = "n", DocCategory = "KYC",
                ApplicableEntity = "CUSTOMER", Status = status, MakerId = "maker-1",
                AuthorizedVersionNumber = version, IsAadhaarDocument = aadhaar
            };
            repo.Seed(d);
            return d;
        }

        [Fact] // R9.4
        public async Task ValidationConfig_AadhaarWithoutMasking_IsRejected()
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var lookups = new FakeLookupValidationPort().WithAllowAll();
            var d = Seeded(repo, DocumentStatus.Active, version: 1, aadhaar: true);
            var handler = new UpdateValidationConfigCommandHandler(repo, audit, lookups);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                handler.Handle(new UpdateValidationConfigCommand(
                    d.Id, "NONE", null, RequiresMasking: false, null, 5, new[] { "PDF" }, "maker-1"), default));
            Assert.Contains("Aadhaar must always be masked", ex.Message);
            Assert.Equal(0, audit.Count);   // R8.3 no audit on rejected action
        }

        [Fact] // R7.3
        public async Task Delete_ActiveDocument_IsBlocked()
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var usage = new FakeDocumentUsageChecker { Referenced = false };
            var d = Seeded(repo, DocumentStatus.Active, version: 1);
            var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

            await Assert.ThrowsAsync<DocumentConflictException>(() =>
                handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
            Assert.Equal(1, repo.Count);
        }

        [Fact] // R7.1 / ERR-DOC-002
        public async Task Delete_ReferencedDocument_ThrowsInUse()
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var usage = new FakeDocumentUsageChecker { Referenced = true };
            var d = Seeded(repo, DocumentStatus.PendingApproval, version: 0);
            var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

            await Assert.ThrowsAsync<DocumentInUseException>(() =>
                handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
            Assert.Equal(1, repo.Count);
        }

        [Fact] // R7.5 fail-safe on usage-checker error
        public async Task Delete_UsageCheckerThrows_IsBlockedFailSafe()
        {
            var repo = new InMemoryDocumentRepository();
            var audit = new InMemoryDocumentAuditRepository();
            var usage = new FakeDocumentUsageChecker { Throw = true };
            var d = Seeded(repo, DocumentStatus.PendingApproval, version: 0);
            var handler = new DeleteDocumentCommandHandler(repo, audit, usage);

            await Assert.ThrowsAsync<DocumentConflictException>(() =>
                handler.Handle(new DeleteDocumentCommand(d.Id, "maker-1"), default));
            Assert.Equal(1, repo.Count);
        }

        // Remaining example/edge tests to add in this file (design Testing Strategy):
        //  - Duplicate code on create (R2.2); DocCode not part of UpdateDocumentRequest (R2.3).
        //  - Single-pending conflict on a second edit (R3.5); not-found on edit/review/delete (R3.3/R5.2/R7.4).
        //  - Self-auth on reject (R5.5); reject remark bounds 1..500 (R5.7).
        //  - Inactivate only from Active, reactivate only from Inactive (R6.1/R6.4).
        //  - Delete of a never-authorized PendingApproval succeeds and writes a Delete audit (R7.2/R8.1).
        //  - Mask required when masking enabled (R9.3); uncompilable regex rejected (R9.5);
        //    MIME set non-empty subset of {PDF,JPG,PNG} (R9.9).
        //  - Consumption unknown Constitution/Entity Role => ValidationException (R11.6);
        //    defined-but-empty scope => empty list (R11.5).
        //  - Paging bounds: page < 1 and pageSize < 1 / > 100 rejected (R10.6/R10.7).
    }
    ```

  - [ ]* 9.6 CREATE `Finnova.Tests/Integration/Document/DocumentAuthorizationTests.cs`
    - `WebApplicationFactory<Program>` integration skeleton with the authorization cases (401/403/
      2xx, dual-role self-auth block) using dev-signed JWTs, plus one create → authorize (second
      user) → consume end-to-end. Mirrors the existing SystemAdmin integration tests.
    - _Requirements: 12.1, 12.2, 12.3, 12.5, 12.6, 5.4, 11.1_

    ```csharp
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using Finnova.Models.Contracts.Documents;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.Integration.Document;

    public class DocumentAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        public DocumentAuthorizationTests(WebApplicationFactory<Program> factory) => _factory = factory;

        // Reuse the existing dev-signed JWT helper used by the Lookup/Court integration tests.
        // TestTokens.ForRoles(params string[] roles) issues a token signed with the test Jwt key
        // configured for the host (Issuer/Audience/SigningKey from test appsettings).
        private HttpClient ClientWithRoles(params string[] roles)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", TestTokens.ForRoles(roles));
            return client;
        }

        [Fact] // R12.1 / R12.5 — missing token => 401 (not 403)
        public async Task Create_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/document", NewCreate());
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact] // R12.2 — maker route without DOC_MASTER_MAKER => 403
        public async Task Create_WithoutMakerRole_Returns403()
        {
            var client = ClientWithRoles("DOC_MASTER_CHECKER");
            var resp = await client.PostAsJsonAsync("/api/document", NewCreate());
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        [Fact] // R12.3 — checker route without DOC_MASTER_CHECKER => 403
        public async Task Authorize_WithoutCheckerRole_Returns403()
        {
            var client = ClientWithRoles("DOC_MASTER_MAKER");
            var resp = await client.PostAsync($"/api/document/{Guid.NewGuid()}/authorize", null);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        [Fact] // R12.6 — dual-role user still blocked from authorizing own change (ERR-DOC-001)
        public async Task DualRoleUser_CannotAuthorizeOwnChange_Returns409()
        {
            var client = ClientWithRoles("DOC_MASTER_MAKER", "DOC_MASTER_CHECKER");
            var created = await client.PostAsJsonAsync("/api/document", NewCreate());
            created.EnsureSuccessStatusCode();
            var doc = await created.Content.ReadFromJsonAsync<DocumentResponse>();

            var authorize = await client.PostAsync($"/api/document/{doc!.Id}/authorize", null);
            Assert.Equal(HttpStatusCode.Conflict, authorize.StatusCode);
            var body = await authorize.Content.ReadAsStringAsync();
            Assert.Contains("ERR-DOC-001", body);
        }

        // End-to-end to add: maker (user A) creates -> checker (user B) authorizes -> consume returns
        // the document for its (Constitution, EntityRole) with resolved mandatory (R5.4, R11.1).
        // Requires the lookup seed to contain the DOC_CATEGORY/APPLICABLE_ENTITY/CONSTITUTION codes
        // used in NewCreate(); run against SQL Server (test container or local) like the Lookup E2E.

        private static CreateDocumentRequest NewCreate() => new(
            "DOC-PAN", "PAN Card", "PAN", "KYC PAN document", "KYC", "CUSTOMER",
            IsMandatory: true, HasExpiry: false, RequiresMasking: false, IsAadhaarDocument: false,
            MaxFileSizeMb: 5);
    }
    ```

    > Note: `TestTokens.ForRoles` refers to the existing dev-signed-JWT test helper used by the
    > current SystemAdmin integration suite (same key/issuer/audience as the host's test config).
    > If the helper is named differently in `Finnova.Tests`, use that one — do not add a new scheme.

- [ ] 10. Frontend model + service layer (Finnova-UI/Finnova-UI/src)

  All frontend tasks target the **Finnova-UI** repo (`E:\Finnova\Finnova-UI\Finnova-UI`). Mirror
  the `location.service.ts` / `lookup.service.ts` pattern: typed model → interface → real (shared
  axios) → mock → toggle → barrels. React hooks + context + MUI; **no Redux, no RxJS**. English-only,
  no RTL.

  - [ ] 10.1 CREATE `src/models/document.model.ts`
    - _Requirements: 1.1, 5.1, 10.3_

    ```typescript
    // English-only single display fields; no bilingual fields, no RTL.
    export type DocumentStatus =
      | 'PendingApproval' | 'Active' | 'Inactive' | 'PendingModification' | 'ReturnedToMaker';

    export interface ApplicabilityRule {
      id?: string;
      constitution: string;
      entityRole: string;
      isMandatory: boolean;
    }

    export interface DocumentModel {
      id: string;
      docCode: string;
      docName: string;
      docShortName?: string;
      description?: string;
      docCategory: string;
      applicableEntity: string;
      isMandatory: boolean;
      hasExpiry: boolean;
      requiresMasking: boolean;
      isAadhaarDocument: boolean;
      validationEngine: string;
      formatRegex?: string;
      maskPattern?: string;
      maxFileSizeMb: number;
      allowedMimeTypes: string[];
      status: DocumentStatus;
      makerId?: string;
      checkerId?: string;
      authorizedVersionNumber: number;
      rejectionReason?: string;
      applicabilityRules: ApplicabilityRule[];
      createdAt: string;
      updatedAt: string;
    }

    export interface PendingReview {
      id: string;
      status: DocumentStatus;
      live: DocumentModel;
      pending: DocumentModel;
    }

    export interface AuditEntry {
      id: string;
      documentId: string;
      action: string;
      oldValues?: string;
      newValues?: string;
      summary: string;
      remarks?: string;
      changedBy: string;
      changedAtUtc: string;
    }

    // Matches the backend PaginatedResponse<T> contract.
    export interface Paginated<T> {
      data: T[];
      total: number;
      page: number;
      pageSize: number;
      totalPages: number;
    }
    ```

  - [ ] 10.2 CREATE `src/services/interfaces/document.service.ts`
    - Service interface + request types. Mirrors `interfaces/lookup.service.ts`.
    - _Requirements: 1.1, 3.1, 4.1, 5.3, 5.6, 9.1, 10.1, 11.1_

    ```typescript
    import {
      DocumentModel, PendingReview, Paginated, ApplicabilityRule, AuditEntry,
    } from '../../models/document.model';

    export interface CreateDocumentReq {
      docCode: string;
      docName: string;
      docShortName?: string;
      description?: string;
      docCategory: string;
      applicableEntity: string;
      isMandatory?: boolean;
      hasExpiry?: boolean;
      requiresMasking?: boolean;
      isAadhaarDocument?: boolean;
      maxFileSizeMb?: number;
    }

    export interface UpdateDocumentReq {
      docName: string;
      docShortName?: string;
      description?: string;
      docCategory: string;
      applicableEntity: string;
      isMandatory: boolean;
      hasExpiry: boolean;
    }

    export interface ValidationConfigReq {
      validationEngine: string;
      formatRegex?: string;
      requiresMasking: boolean;
      maskPattern?: string;
      maxFileSizeMb: number;
      allowedMimeTypes: string[];
    }

    export interface GetDocumentsParams {
      search?: string;
      category?: string;
      status?: string;
      page?: number;
      pageSize?: number;
    }

    export interface IDocumentService {
      getPaged(params: GetDocumentsParams): Promise<Paginated<DocumentModel>>;
      getById(id: string): Promise<DocumentModel>;
      getReview(id: string): Promise<PendingReview>;
      getAudit(id: string): Promise<AuditEntry[]>;
      create(req: CreateDocumentReq): Promise<DocumentModel>;
      update(id: string, req: UpdateDocumentReq): Promise<DocumentModel>;
      updateApplicability(id: string, rules: ApplicabilityRule[]): Promise<DocumentModel>;
      updateValidationConfig(id: string, req: ValidationConfigReq): Promise<DocumentModel>;
      authorize(id: string): Promise<DocumentModel>;
      reject(id: string, reason: string): Promise<DocumentModel>;
      resubmit(id: string): Promise<DocumentModel>;
      inactivate(id: string): Promise<DocumentModel>;
      reactivate(id: string): Promise<DocumentModel>;
      remove(id: string): Promise<void>;
    }
    ```

  - [ ] 10.3 CREATE `src/services/real/document.service.real.ts`
    - Real axios implementation on the shared instance (`src/services/api.ts`) so the Bearer token,
      10s timeout, and 401/403/409 toast handling are centralized. Base path `/api/document`
      (the shared axios base already includes the `/api/ua/api` gateway prefix → the gateway alias
      from task 8.4 routes it to `systemadmin-cluster`).
    - _Requirements: 10.1, 11.1, 13.11, 13.12_

    ```typescript
    import api from '../api';
    import {
      IDocumentService, CreateDocumentReq, UpdateDocumentReq, ValidationConfigReq, GetDocumentsParams,
    } from '../interfaces/document.service';
    import {
      DocumentModel, PendingReview, Paginated, ApplicabilityRule, AuditEntry,
    } from '../../models/document.model';

    const BASE = '/api/document';   // composed onto the shared axios baseURL (.../api/ua/api)

    export const real: IDocumentService = {
      async getPaged(params: GetDocumentsParams): Promise<Paginated<DocumentModel>> {
        const { data } = await api.get<Paginated<DocumentModel>>(BASE, { params });
        return data;
      },
      async getById(id) {
        const { data } = await api.get<DocumentModel>(`${BASE}/${id}`);
        return data;
      },
      async getReview(id): Promise<PendingReview> {
        const { data } = await api.get<PendingReview>(`${BASE}/${id}/review`);
        return data;
      },
      async getAudit(id): Promise<AuditEntry[]> {
        const { data } = await api.get<AuditEntry[]>(`${BASE}/${id}/audit`);
        return data;
      },
      async create(req: CreateDocumentReq) {
        const { data } = await api.post<DocumentModel>(BASE, req);
        return data;
      },
      async update(id, req: UpdateDocumentReq) {
        const { data } = await api.put<DocumentModel>(`${BASE}/${id}`, req);
        return data;
      },
      async updateApplicability(id, rules: ApplicabilityRule[]) {
        const { data } = await api.put<DocumentModel>(`${BASE}/${id}/applicability`, { rules });
        return data;
      },
      async updateValidationConfig(id, req: ValidationConfigReq) {
        const { data } = await api.put<DocumentModel>(`${BASE}/${id}/validation-config`, req);
        return data;
      },
      async authorize(id) {
        const { data } = await api.post<DocumentModel>(`${BASE}/${id}/authorize`);
        return data;
      },
      async reject(id, reason) {
        const { data } = await api.post<DocumentModel>(`${BASE}/${id}/reject`, { reason });
        return data;
      },
      async resubmit(id) {
        const { data } = await api.post<DocumentModel>(`${BASE}/${id}/resubmit`);
        return data;
      },
      async inactivate(id) {
        const { data } = await api.post<DocumentModel>(`${BASE}/${id}/inactivate`);
        return data;
      },
      async reactivate(id) {
        const { data } = await api.post<DocumentModel>(`${BASE}/${id}/reactivate`);
        return data;
      },
      async remove(id) {
        await api.delete(`${BASE}/${id}`);
      },
    };
    ```

  - [ ] 10.4 CREATE `src/services/mock/document.service.mock.ts`
    - In-memory mock for `VITE_USE_MOCK_API=true`, consistent behavior (create → appears in list;
      authorize → Active; reject → requires remark + ReturnedToMaker).
    - _Requirements: 10.1, 5.3, 5.6_

    ```typescript
    import {
      IDocumentService, CreateDocumentReq, UpdateDocumentReq, ValidationConfigReq, GetDocumentsParams,
    } from '../interfaces/document.service';
    import {
      DocumentModel, PendingReview, Paginated, ApplicabilityRule, AuditEntry, DocumentStatus,
    } from '../../models/document.model';

    let store: DocumentModel[] = [];
    const audit: AuditEntry[] = [];
    const nowIso = () => new Date().toISOString();
    const uuid = () => (crypto?.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

    const find = (id: string) => {
      const d = store.find((x) => x.id === id);
      if (!d) throw new Error('not found');
      return d;
    };

    export const mock: IDocumentService = {
      async getPaged(params: GetDocumentsParams): Promise<Paginated<DocumentModel>> {
        const page = params.page ?? 1;
        const pageSize = params.pageSize ?? 20;
        let rows = [...store];
        if (params.search) {
          const t = params.search.toLowerCase();
          rows = rows.filter((x) =>
            x.docCode.toLowerCase().includes(t) || x.docName.toLowerCase().includes(t));
        }
        if (params.category) rows = rows.filter((x) => x.docCategory === params.category);
        if (params.status) rows = rows.filter((x) => x.status === params.status);
        rows.sort((a, b) => a.docCode.localeCompare(b.docCode) || a.id.localeCompare(b.id));
        const total = rows.length;
        const data = rows.slice((page - 1) * pageSize, (page - 1) * pageSize + pageSize);
        return { data, total, page, pageSize, totalPages: Math.ceil(total / pageSize) };
      },
      async getById(id) { return { ...find(id) }; },
      async getReview(id): Promise<PendingReview> {
        const d = find(id);
        return { id: d.id, status: d.status, live: { ...d }, pending: { ...d } };
      },
      async getAudit(id): Promise<AuditEntry[]> {
        return audit.filter((a) => a.documentId === id)
          .sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc));
      },
      async create(req: CreateDocumentReq) {
        const d: DocumentModel = {
          id: uuid(), docCode: req.docCode.trim(), docName: req.docName.trim(),
          docShortName: req.docShortName, description: req.description,
          docCategory: req.docCategory, applicableEntity: req.applicableEntity,
          isMandatory: req.isMandatory ?? false, hasExpiry: req.hasExpiry ?? false,
          requiresMasking: req.requiresMasking ?? false, isAadhaarDocument: req.isAadhaarDocument ?? false,
          validationEngine: 'NONE', maxFileSizeMb: req.maxFileSizeMb ?? 5, allowedMimeTypes: ['PDF'],
          status: 'PendingApproval' as DocumentStatus, authorizedVersionNumber: 0,
          applicabilityRules: [], createdAt: nowIso(), updatedAt: nowIso(), makerId: 'mock-maker',
        };
        store.push(d);
        audit.push({ id: uuid(), documentId: d.id, action: 'Create', summary: `Created ${d.docCode}`,
          changedBy: 'mock-maker', changedAtUtc: nowIso() });
        return { ...d };
      },
      async update(id, req: UpdateDocumentReq) {
        const d = find(id);
        Object.assign(d, req, { status: 'PendingModification', updatedAt: nowIso() });
        return { ...d };
      },
      async updateApplicability(id, rules: ApplicabilityRule[]) {
        const d = find(id);
        d.applicabilityRules = rules; d.status = 'PendingModification'; d.updatedAt = nowIso();
        return { ...d };
      },
      async updateValidationConfig(id, req: ValidationConfigReq) {
        const d = find(id);
        Object.assign(d, req, { status: 'PendingModification', updatedAt: nowIso() });
        return { ...d };
      },
      async authorize(id) {
        const d = find(id); d.status = 'Active'; d.checkerId = 'mock-checker'; d.updatedAt = nowIso();
        return { ...d };
      },
      async reject(id, reason) {
        if (!reason || !reason.trim()) throw new Error('remark required');
        const d = find(id); d.status = 'ReturnedToMaker'; d.rejectionReason = reason; d.updatedAt = nowIso();
        return { ...d };
      },
      async resubmit(id) {
        const d = find(id); d.status = 'PendingApproval'; d.updatedAt = nowIso();
        return { ...d };
      },
      async inactivate(id) { const d = find(id); d.status = 'Inactive'; return { ...d }; },
      async reactivate(id) { const d = find(id); d.status = 'Active'; return { ...d }; },
      async remove(id) { store = store.filter((x) => x.id !== id); },
    };
    ```

  - [ ] 10.5 CREATE `src/services/document.service.ts`
    - Toggle module, mirroring `location.service.ts` / `lookup.service.ts`.
    - _Requirements: 10.1_

    ```typescript
    import { real } from './real/document.service.real';
    import { mock } from './mock/document.service.mock';
    import { IDocumentService } from './interfaces/document.service';

    export const documentService: IDocumentService =
      import.meta.env.VITE_USE_MOCK_API === 'true' ? mock : real;

    export type { IDocumentService };
    ```

  - [ ] 10.6 MODIFY `src/services/index.ts` (barrel — only if the repo exports services via a barrel)
    - Add the document service to the services barrel next to the existing `location`/`lookup`
      exports. If the repo has no services barrel, skip this task.
    - _Requirements: 10.1_

    Add alongside the other service exports:

    ```typescript
    export { documentService } from './document.service';
    export type { IDocumentService } from './document.service';
    ```

- [ ] 11. Frontend page + components + route + nav (Finnova-UI/Finnova-UI/src)

  MUI (`@mui/material`, `@mui/x-data-grid`), hooks + context, lookup-backed selects from the
  existing lookup dropdown service, role gating from the auth context. Mirror `LookupMaster.tsx` /
  `LocationMaster.tsx`. The snippets below are complete enough to compile and run; adapt imports to
  the repo's exact auth-context and lookup-dropdown hook names where noted.

  - [ ] 11.1 CREATE `src/pages/DocumentMaster/DocumentFilters.tsx`
    - Search + Doc Category + Status filters (R13.1). Category options come from the lookup dropdown
      service (`DOC_CATEGORY`); status is the fixed lifecycle set.
    - _Requirements: 10.1, 10.2, 13.1, 13.2_

    ```tsx
    import { Box, MenuItem, TextField } from '@mui/material';

    export interface DocumentFiltersValue {
      search: string;
      category: string;
      status: string;
    }

    interface Props {
      value: DocumentFiltersValue;
      categoryOptions: { code: string; label: string }[];   // ACTIVE DOC_CATEGORY lookup values
      onChange: (next: DocumentFiltersValue) => void;
    }

    const STATUSES = ['', 'PendingApproval', 'Active', 'Inactive', 'PendingModification', 'ReturnedToMaker'];

    export default function DocumentFilters({ value, categoryOptions, onChange }: Props) {
      return (
        <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
          <TextField
            label="Search (Doc Code / Doc Name)"
            size="small"
            value={value.search}
            onChange={(e) => onChange({ ...value, search: e.target.value })}
            sx={{ minWidth: 260 }}
          />
          <TextField
            select label="Doc Category" size="small" value={value.category}
            onChange={(e) => onChange({ ...value, category: e.target.value })}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">All</MenuItem>
            {categoryOptions.map((o) => (
              <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>
            ))}
          </TextField>
          <TextField
            select label="Status" size="small" value={value.status}
            onChange={(e) => onChange({ ...value, status: e.target.value })}
            sx={{ minWidth: 180 }}
          >
            {STATUSES.map((s) => (
              <MenuItem key={s || 'all'} value={s}>{s || 'All'}</MenuItem>
            ))}
          </TextField>
        </Box>
      );
    }
    ```

  - [ ] 11.2 CREATE `src/pages/DocumentMaster/DocumentFormDialog.tsx`
    - Create/edit dialog. DocCode read-only when `status === 'Active'` (R13.4). Lookup-backed
      selects for Doc Category / Applicable Entity (R13.2). Field-level required validation blocks
      submit (R13.6).
    - _Requirements: 1.1, 2.3, 9.8, 13.2, 13.3, 13.4, 13.6_

    ```tsx
    import { useState } from 'react';
    import {
      Button, Dialog, DialogActions, DialogContent, DialogTitle,
      FormControlLabel, MenuItem, Switch, TextField,
    } from '@mui/material';
    import { DocumentModel } from '../../models/document.model';
    import { CreateDocumentReq } from '../../services/interfaces/document.service';

    interface LookupOpt { code: string; label: string; }
    interface Props {
      open: boolean;
      editing?: DocumentModel;                 // present => edit mode
      categoryOptions: LookupOpt[];            // ACTIVE DOC_CATEGORY
      applicableEntityOptions: LookupOpt[];    // ACTIVE APPLICABLE_ENTITY
      onClose: () => void;
      onSubmit: (req: CreateDocumentReq) => Promise<void>;
    }

    export default function DocumentFormDialog({
      open, editing, categoryOptions, applicableEntityOptions, onClose, onSubmit,
    }: Props) {
      const [form, setForm] = useState<CreateDocumentReq>({
        docCode: editing?.docCode ?? '',
        docName: editing?.docName ?? '',
        docShortName: editing?.docShortName ?? '',
        description: editing?.description ?? '',
        docCategory: editing?.docCategory ?? '',
        applicableEntity: editing?.applicableEntity ?? '',
        isMandatory: editing?.isMandatory ?? false,
        hasExpiry: editing?.hasExpiry ?? false,
        maxFileSizeMb: editing?.maxFileSizeMb ?? 5,
      });
      const [errors, setErrors] = useState<Record<string, string>>({});

      const codeReadOnly = editing?.status === 'Active';   // R13.4

      const validate = () => {
        const e: Record<string, string> = {};
        if (!form.docCode.trim()) e.docCode = 'Doc Code is required.';
        if (!form.docName.trim()) e.docName = 'Doc Name is required.';
        if (!form.docCategory) e.docCategory = 'Doc Category is required.';
        if (!form.applicableEntity) e.applicableEntity = 'Applicable Entity is required.';
        if ((form.maxFileSizeMb ?? 5) < 1 || (form.maxFileSizeMb ?? 5) > 25)
          e.maxFileSizeMb = 'Max file size must be between 1 and 25 MB';
        setErrors(e);
        return Object.keys(e).length === 0;
      };

      const submit = async () => {
        if (!validate()) return;   // R13.6 block submission on missing required field
        await onSubmit(form);
      };

      return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
          <DialogTitle>{editing ? 'Edit Document' : 'Create Document'}</DialogTitle>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <TextField
              label="Doc Code" value={form.docCode}
              onChange={(e) => setForm({ ...form, docCode: e.target.value })}
              error={!!errors.docCode} helperText={errors.docCode}
              InputProps={{ readOnly: codeReadOnly }} disabled={codeReadOnly}
            />
            <TextField
              label="Doc Name" value={form.docName}
              onChange={(e) => setForm({ ...form, docName: e.target.value })}
              error={!!errors.docName} helperText={errors.docName}
            />
            <TextField
              label="Doc Short Name" value={form.docShortName ?? ''}
              onChange={(e) => setForm({ ...form, docShortName: e.target.value })}
            />
            <TextField
              label="Description" value={form.description ?? ''} multiline minRows={2}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
            />
            <TextField
              select label="Doc Category" value={form.docCategory}
              onChange={(e) => setForm({ ...form, docCategory: e.target.value })}
              error={!!errors.docCategory} helperText={errors.docCategory}
            >
              {categoryOptions.map((o) => <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>)}
            </TextField>
            <TextField
              select label="Applicable Entity" value={form.applicableEntity}
              onChange={(e) => setForm({ ...form, applicableEntity: e.target.value })}
              error={!!errors.applicableEntity} helperText={errors.applicableEntity}
            >
              {applicableEntityOptions.map((o) => <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>)}
            </TextField>
            <TextField
              label="Max File Size (MB)" type="number" value={form.maxFileSizeMb ?? 5}
              onChange={(e) => setForm({ ...form, maxFileSizeMb: Number(e.target.value) })}
              error={!!errors.maxFileSizeMb} helperText={errors.maxFileSizeMb}
            />
            <FormControlLabel
              control={<Switch checked={!!form.isMandatory}
                onChange={(e) => setForm({ ...form, isMandatory: e.target.checked })} />}
              label="Is Mandatory"
            />
            <FormControlLabel
              control={<Switch checked={!!form.hasExpiry}
                onChange={(e) => setForm({ ...form, hasExpiry: e.target.checked })} />}
              label="Has Expiry"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" onClick={submit}>Save</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

  - [ ] 11.3 CREATE `src/pages/DocumentMaster/ApplicabilityRulesForm.tsx`
    - Multi-row Constitution × Entity Role with a per-row mandatory toggle (R4, R13.2). Lookup-backed
      selects; no free text.
    - _Requirements: 4.1, 4.2, 4.4, 13.2_

    ```tsx
    import {
      Box, Button, IconButton, MenuItem, Switch, TextField, Table, TableBody,
      TableCell, TableHead, TableRow,
    } from '@mui/material';
    import DeleteIcon from '@mui/icons-material/Delete';
    import { ApplicabilityRule } from '../../models/document.model';

    interface LookupOpt { code: string; label: string; }
    interface Props {
      rules: ApplicabilityRule[];
      constitutionOptions: LookupOpt[];   // ACTIVE CONSTITUTION
      entityRoleOptions: LookupOpt[];      // ACTIVE APPLICABLE_ENTITY
      onChange: (rules: ApplicabilityRule[]) => void;
    }

    export default function ApplicabilityRulesForm({
      rules, constitutionOptions, entityRoleOptions, onChange,
    }: Props) {
      const update = (i: number, patch: Partial<ApplicabilityRule>) =>
        onChange(rules.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));
      const add = () =>
        onChange([...rules, { constitution: '', entityRole: '', isMandatory: false }]);
      const remove = (i: number) => onChange(rules.filter((_, idx) => idx !== i));

      return (
        <Box>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Constitution</TableCell>
                <TableCell>Entity Role</TableCell>
                <TableCell>Mandatory</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {rules.map((r, i) => (
                <TableRow key={i}>
                  <TableCell>
                    <TextField select size="small" value={r.constitution} fullWidth
                      onChange={(e) => update(i, { constitution: e.target.value })}>
                      {constitutionOptions.map((o) => (
                        <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>
                      ))}
                    </TextField>
                  </TableCell>
                  <TableCell>
                    <TextField select size="small" value={r.entityRole} fullWidth
                      onChange={(e) => update(i, { entityRole: e.target.value })}>
                      {entityRoleOptions.map((o) => (
                        <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>
                      ))}
                    </TextField>
                  </TableCell>
                  <TableCell>
                    <Switch checked={r.isMandatory}
                      onChange={(e) => update(i, { isMandatory: e.target.checked })} />
                  </TableCell>
                  <TableCell>
                    <IconButton onClick={() => remove(i)} aria-label="remove rule">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Button sx={{ mt: 1 }} onClick={add}>Add Rule</Button>
        </Box>
      );
    }
    ```

  - [ ] 11.4 CREATE `src/pages/DocumentMaster/ValidationConfigForm.tsx`
    - Validation Engine select (lookup-backed), format regex, masking toggle + pattern, file rules
      (R9, R13.2).
    - _Requirements: 9.1, 9.3, 9.8, 9.9, 13.2_

    ```tsx
    import {
      Box, Checkbox, FormControlLabel, FormGroup, MenuItem, Switch, TextField,
    } from '@mui/material';
    import { ValidationConfigReq } from '../../services/interfaces/document.service';

    interface LookupOpt { code: string; label: string; }
    interface Props {
      value: ValidationConfigReq;
      validationEngineOptions: LookupOpt[];   // ACTIVE VALIDATION_ENGINE
      onChange: (next: ValidationConfigReq) => void;
    }

    const MIME = ['PDF', 'JPG', 'PNG'];

    export default function ValidationConfigForm({ value, validationEngineOptions, onChange }: Props) {
      const toggleMime = (m: string) =>
        onChange({
          ...value,
          allowedMimeTypes: value.allowedMimeTypes.includes(m)
            ? value.allowedMimeTypes.filter((x) => x !== m)
            : [...value.allowedMimeTypes, m],
        });

      return (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            select label="Validation Engine" value={value.validationEngine}
            onChange={(e) => onChange({ ...value, validationEngine: e.target.value })}
          >
            {validationEngineOptions.map((o) => (
              <MenuItem key={o.code} value={o.code}>{o.label}</MenuItem>
            ))}
          </TextField>
          <TextField
            label="Format Regex" value={value.formatRegex ?? ''}
            onChange={(e) => onChange({ ...value, formatRegex: e.target.value })}
          />
          <FormControlLabel
            control={<Switch checked={value.requiresMasking}
              onChange={(e) => onChange({ ...value, requiresMasking: e.target.checked })} />}
            label="Requires Masking"
          />
          <TextField
            label="Mask Pattern" value={value.maskPattern ?? ''} disabled={!value.requiresMasking}
            helperText={value.requiresMasking ? 'Required when masking is enabled' : ''}
            onChange={(e) => onChange({ ...value, maskPattern: e.target.value })}
          />
          <TextField
            label="Max File Size (MB)" type="number" value={value.maxFileSizeMb}
            onChange={(e) => onChange({ ...value, maxFileSizeMb: Number(e.target.value) })}
          />
          <FormGroup row>
            {MIME.map((m) => (
              <FormControlLabel key={m}
                control={<Checkbox checked={value.allowedMimeTypes.includes(m)}
                  onChange={() => toggleMime(m)} />}
                label={m} />
            ))}
          </FormGroup>
        </Box>
      );
    }
    ```

  - [ ] 11.5 CREATE `src/pages/DocumentMaster/AuthorizeDialog.tsx`
    - Checker dialog: live vs pending side by side (R13.5); Reject requires a non-empty remark
      before submit (R13.7).
    - _Requirements: 5.1, 5.6, 13.5, 13.7_

    ```tsx
    import { useState } from 'react';
    import {
      Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Grid, TextField, Typography,
    } from '@mui/material';
    import { PendingReview, DocumentModel } from '../../models/document.model';

    interface Props {
      open: boolean;
      review?: PendingReview;
      onClose: () => void;
      onAuthorize: () => Promise<void>;
      onReject: (reason: string) => Promise<void>;
    }

    function Side({ title, doc }: { title: string; doc: DocumentModel }) {
      return (
        <Box>
          <Typography variant="subtitle2">{title}</Typography>
          <Typography variant="body2">Name: {doc.docName}</Typography>
          <Typography variant="body2">Category: {doc.docCategory}</Typography>
          <Typography variant="body2">Mandatory: {String(doc.isMandatory)}</Typography>
          <Typography variant="body2">Masking: {String(doc.requiresMasking)}</Typography>
          <Typography variant="body2">Engine: {doc.validationEngine}</Typography>
        </Box>
      );
    }

    export default function AuthorizeDialog({ open, review, onClose, onAuthorize, onReject }: Props) {
      const [reason, setReason] = useState('');
      const [reasonError, setReasonError] = useState('');

      const reject = async () => {
        if (!reason.trim()) { setReasonError('A rejection remark is required.'); return; }  // R13.7
        await onReject(reason.trim());
      };

      return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
          <DialogTitle>Authorize Document</DialogTitle>
          <DialogContent>
            {review && (
              <Grid container spacing={2}>
                <Grid item xs={6}><Side title="Live (current)" doc={review.live} /></Grid>
                <Grid item xs={6}><Side title="Pending (proposed)" doc={review.pending} /></Grid>
              </Grid>
            )}
            <TextField
              sx={{ mt: 2 }} fullWidth label="Rejection remark" value={reason}
              onChange={(e) => { setReason(e.target.value); setReasonError(''); }}
              error={!!reasonError} helperText={reasonError}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Cancel</Button>
            <Button color="error" onClick={reject}>Reject</Button>
            <Button variant="contained" onClick={onAuthorize}>Authorize</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

  - [ ] 11.6 CREATE `src/pages/DocumentMaster/DocumentMaster.tsx`
    - The page: filters + MUI DataGrid (server-side pagination), create/edit/authorize dialogs,
      role-gated controls, and `ERR-DOC-001`/`ERR-DOC-002` message handling. Adapt the auth-context
      and lookup-dropdown hooks to the repo's actual names (noted inline).
    - _Requirements: 10.1, 10.3, 13.1, 13.5, 13.8, 13.9, 13.10_

    ```tsx
    import { useCallback, useEffect, useMemo, useState } from 'react';
    import { Box, Button, Chip, Stack, Typography } from '@mui/material';
    import { DataGrid, GridColDef, GridPaginationModel } from '@mui/x-data-grid';
    import { documentService } from '../../services/document.service';
    import { DocumentModel, PendingReview } from '../../models/document.model';
    import DocumentFilters, { DocumentFiltersValue } from './DocumentFilters';
    import DocumentFormDialog from './DocumentFormDialog';
    import AuthorizeDialog from './AuthorizeDialog';
    // Adapt these to the repo's actual hooks:
    // import { useAuth } from '../../context/AuthContext';           // provides current roles
    // import { useLookupOptions } from '../../hooks/useLookupOptions'; // ACTIVE lookup dropdown values

    export default function DocumentMaster() {
      // const { roles } = useAuth();
      const roles: string[] = [];   // replace with useAuth().roles
      const canMake = roles.includes('DOC_MASTER_MAKER');     // R13.10
      const canCheck = roles.includes('DOC_MASTER_CHECKER');  // R13.10

      // Replace with useLookupOptions('DOC_CATEGORY') etc.
      const categoryOptions = useMemo(() => [] as { code: string; label: string }[], []);
      const applicableEntityOptions = useMemo(() => [] as { code: string; label: string }[], []);

      const [filters, setFilters] = useState<DocumentFiltersValue>({ search: '', category: '', status: '' });
      const [rows, setRows] = useState<DocumentModel[]>([]);
      const [rowCount, setRowCount] = useState(0);
      const [pagination, setPagination] = useState<GridPaginationModel>({ page: 0, pageSize: 20 });
      const [loading, setLoading] = useState(false);
      const [error, setError] = useState('');
      const [formOpen, setFormOpen] = useState(false);
      const [editing, setEditing] = useState<DocumentModel | undefined>();
      const [review, setReview] = useState<PendingReview | undefined>();
      const [authOpen, setAuthOpen] = useState(false);

      const load = useCallback(async () => {
        setLoading(true); setError('');
        try {
          const res = await documentService.getPaged({
            search: filters.search || undefined,
            category: filters.category || undefined,
            status: filters.status || undefined,
            page: pagination.page + 1,
            pageSize: pagination.pageSize,
          });
          setRows(res.data); setRowCount(res.total);
        } catch (e: any) {
          setError(readError(e));   // R13.8/13.9 messages surfaced here
        } finally {
          setLoading(false);
        }
      }, [filters, pagination]);

      useEffect(() => { load(); }, [load]);

      const openReview = async (id: string) => {
        setReview(await documentService.getReview(id));
        setAuthOpen(true);
      };

      const columns: GridColDef<DocumentModel>[] = [
        { field: 'docCode', headerName: 'Doc Code', width: 140 },
        { field: 'docName', headerName: 'Doc Name', flex: 1 },
        { field: 'docCategory', headerName: 'Category', width: 150 },
        {
          field: 'status', headerName: 'Status', width: 170,
          renderCell: (p) => <Chip size="small" label={p.value as string} />,
        },
        {
          field: 'actions', headerName: 'Actions', width: 240, sortable: false,
          renderCell: (p) => (
            <Stack direction="row" spacing={1}>
              {canMake && (
                <Button size="small" onClick={() => { setEditing(p.row); setFormOpen(true); }}>Edit</Button>
              )}
              {canCheck && (p.row.status === 'PendingApproval' || p.row.status === 'PendingModification') && (
                <Button size="small" onClick={() => openReview(p.row.id)}>Review</Button>
              )}
            </Stack>
          ),
        },
      ];

      return (
        <Box sx={{ p: 3 }}>
          <Typography variant="h5" sx={{ mb: 2 }}>Document Master</Typography>
          {error && <Typography color="error" sx={{ mb: 2 }}>{error}</Typography>}
          <DocumentFilters value={filters} categoryOptions={categoryOptions} onChange={setFilters} />
          {canMake && (
            <Button variant="contained" sx={{ mb: 2 }}
              onClick={() => { setEditing(undefined); setFormOpen(true); }}>
              New Document
            </Button>
          )}
          <div style={{ height: 560, width: '100%' }}>
            <DataGrid
              rows={rows} columns={columns} getRowId={(r) => r.id}
              loading={loading} rowCount={rowCount}
              paginationMode="server" pageSizeOptions={[10, 20, 50]}
              paginationModel={pagination} onPaginationModelChange={setPagination}
            />
          </div>

          {formOpen && (
            <DocumentFormDialog
              open={formOpen} editing={editing}
              categoryOptions={categoryOptions} applicableEntityOptions={applicableEntityOptions}
              onClose={() => setFormOpen(false)}
              onSubmit={async (req) => {
                try {
                  if (editing) {
                    await documentService.update(editing.id, {
                      docName: req.docName, docShortName: req.docShortName, description: req.description,
                      docCategory: req.docCategory, applicableEntity: req.applicableEntity,
                      isMandatory: req.isMandatory ?? false, hasExpiry: req.hasExpiry ?? false,
                    });
                  } else {
                    await documentService.create(req);
                  }
                  setFormOpen(false); await load();
                } catch (e: any) { setError(readError(e)); }
              }}
            />
          )}

          {authOpen && (
            <AuthorizeDialog
              open={authOpen} review={review}
              onClose={() => setAuthOpen(false)}
              onAuthorize={async () => {
                try { await documentService.authorize(review!.id); setAuthOpen(false); await load(); }
                catch (e: any) { setError(readError(e)); }   // ERR-DOC-001 surfaces here (R13.8)
              }}
              onReject={async (reason) => {
                try { await documentService.reject(review!.id, reason); setAuthOpen(false); await load(); }
                catch (e: any) { setError(readError(e)); }
              }}
            />
          )}
        </Box>
      );
    }

    // R13.8 / R13.9 — map the backend error codes to their exact messages. The shared axios layer
    // exposes error.response.data.code / .message.
    function readError(e: any): string {
      const code = e?.response?.data?.code;
      if (code === 'ERR-DOC-001') return 'You cannot authorize a record you created or edited';
      if (code === 'ERR-DOC-002') return 'Document is in use and cannot be deleted. You can mark it Inactive';
      return e?.response?.data?.message ?? 'Something went wrong. Please try again.';
    }
    ```

  - [ ] 11.7 MODIFY `src/App.tsx`
    - Add the `/document-master` route next to the existing master routes (e.g. right after the
      Lookup/Location master `<Route>` entries). Adapt to the repo's router style (the snippet
      assumes `react-router-dom` `<Route>` elements as used by the existing masters).
    - _Requirements: 13.1_

    Add the import near the other page imports:

    ```tsx
    import DocumentMaster from './pages/DocumentMaster/DocumentMaster';
    ```

    Add the route alongside the other master routes:

    ```tsx
    <Route path="/document-master" element={<DocumentMaster />} />
    ```

  - [ ] 11.8 MODIFY `src/components/Layout.tsx`
    - Add a nav entry for Document Master next to the existing Lookup/Location master entries.
      Adapt to the repo's nav item shape (the snippet assumes a `{ label, path }` nav list).
    - _Requirements: 13.1_

    Add alongside the other master nav items:

    ```tsx
    { label: 'Document Master', path: '/document-master' },
    ```

- [ ] 12. Frontend tests (Vitest + React Testing Library, jsdom)

  Run with `npm test` (use `vitest --run` for a single pass, not watch mode). No RxJS marble
  tests, no Redux store tests. Representative test files below; add the rest in the same style.

  - [ ]* 12.1 CREATE `src/services/__tests__/document.service.mock.test.ts`
    - Mock-service behavior: create → appears in list; authorize → Active; reject → requires remark
      and sets ReturnedToMaker; paged filtering.
    - _Requirements: 5.3, 5.6, 10.1_

    ```typescript
    import { describe, it, expect, beforeEach } from 'vitest';
    import { mock } from '../mock/document.service.mock';

    const base = {
      docName: 'PAN Card', docCategory: 'KYC', applicableEntity: 'CUSTOMER',
    };

    describe('document mock service', () => {
      beforeEach(async () => {
        // reset by removing any existing rows
        const page = await mock.getPaged({ pageSize: 100 });
        await Promise.all(page.data.map((d) => mock.remove(d.id)));
      });

      it('create then appears in the paged list', async () => {
        const created = await mock.create({ docCode: 'DOC-PAN', ...base });
        expect(created.status).toBe('PendingApproval');
        const page = await mock.getPaged({ search: 'PAN' });
        expect(page.data.some((d) => d.id === created.id)).toBe(true);
      });

      it('authorize moves status to Active', async () => {
        const created = await mock.create({ docCode: 'DOC-A', ...base });
        const authed = await mock.authorize(created.id);
        expect(authed.status).toBe('Active');
      });

      it('reject requires a remark and sets ReturnedToMaker', async () => {
        const created = await mock.create({ docCode: 'DOC-R', ...base });
        await expect(mock.reject(created.id, '   ')).rejects.toBeTruthy();
        const rejected = await mock.reject(created.id, 'Fix the category');
        expect(rejected.status).toBe('ReturnedToMaker');
        expect(rejected.rejectionReason).toBe('Fix the category');
      });
    });
    ```

  - [ ]* 12.2 CREATE `src/services/__tests__/document.service.real.test.ts`
    - Real-service URL/body composition and response mapping, mocking the shared axios instance.
    - _Requirements: 10.1, 13.12_

    ```typescript
    import { describe, it, expect, vi, beforeEach } from 'vitest';

    const get = vi.fn();
    const post = vi.fn();
    const put = vi.fn();
    const del = vi.fn();
    vi.mock('../api', () => ({ default: { get, post, put, delete: del } }));

    // Import after the mock is registered.
    import { real } from '../real/document.service.real';

    describe('document real service', () => {
      beforeEach(() => { get.mockReset(); post.mockReset(); put.mockReset(); del.mockReset(); });

      it('getPaged composes /api/document with params', async () => {
        get.mockResolvedValue({ data: { data: [], total: 0, page: 1, pageSize: 20, totalPages: 0 } });
        await real.getPaged({ search: 'pan', page: 2, pageSize: 10 });
        expect(get).toHaveBeenCalledWith('/api/document',
          { params: { search: 'pan', page: 2, pageSize: 10 } });
      });

      it('reject posts the reason body', async () => {
        post.mockResolvedValue({ data: {} });
        await real.reject('abc', 'bad category');
        expect(post).toHaveBeenCalledWith('/api/document/abc/reject', { reason: 'bad category' });
      });

      it('updateApplicability wraps rules under { rules }', async () => {
        put.mockResolvedValue({ data: {} });
        await real.updateApplicability('abc', [{ constitution: 'Individual', entityRole: 'CUSTOMER', isMandatory: true }]);
        expect(put).toHaveBeenCalledWith('/api/document/abc/applicability',
          { rules: [{ constitution: 'Individual', entityRole: 'CUSTOMER', isMandatory: true }] });
      });
    });
    ```

  - [ ]* 12.3 CREATE `src/pages/DocumentMaster/__tests__/DocumentFormDialog.test.tsx`
    - Create dialog: blocks submit on a missing required field with a field-level message (R13.6);
      DocCode read-only when editing an Active document (R13.4); lookup selects render the provided
      options only (R13.2).
    - _Requirements: 2.3, 13.2, 13.4, 13.6_

    ```tsx
    import { describe, it, expect, vi } from 'vitest';
    import { render, screen, fireEvent } from '@testing-library/react';
    import DocumentFormDialog from '../DocumentFormDialog';

    const cats = [{ code: 'KYC', label: 'KYC' }];
    const ents = [{ code: 'CUSTOMER', label: 'Customer' }];

    describe('DocumentFormDialog', () => {
      it('blocks submit and shows a field-level message when required fields are empty', async () => {
        const onSubmit = vi.fn();
        render(<DocumentFormDialog open editing={undefined}
          categoryOptions={cats} applicableEntityOptions={ents}
          onClose={() => {}} onSubmit={onSubmit} />);
        fireEvent.click(screen.getByRole('button', { name: /save/i }));
        expect(await screen.findByText('Doc Code is required.')).toBeInTheDocument();
        expect(onSubmit).not.toHaveBeenCalled();
      });

      it('renders DocCode read-only when editing an Active document', () => {
        const editing: any = {
          id: '1', docCode: 'DOC-PAN', docName: 'PAN', docCategory: 'KYC',
          applicableEntity: 'CUSTOMER', status: 'Active', maxFileSizeMb: 5,
          isMandatory: false, hasExpiry: false, requiresMasking: false, isAadhaarDocument: false,
          validationEngine: 'NONE', allowedMimeTypes: ['PDF'], authorizedVersionNumber: 1,
          applicabilityRules: [], createdAt: '', updatedAt: '',
        };
        render(<DocumentFormDialog open editing={editing}
          categoryOptions={cats} applicableEntityOptions={ents}
          onClose={() => {}} onSubmit={vi.fn()} />);
        const code = screen.getByLabelText(/Doc Code/i) as HTMLInputElement;
        expect(code).toBeDisabled();
      });
    });
    ```

  - [ ]* 12.4 Remaining frontend tests to add (same style)
    - These complete the design's Frontend test list; add each as a `*.test.tsx` next to the code.
    - _Requirements: 5.1, 5.6, 10.1, 10.9, 13.2, 13.5, 13.7, 13.8, 13.9, 13.10_

    - `DocumentMaster.test.tsx` — grid renders filtered/paginated rows and the empty-state (R10.9);
      role gating hides the New/Edit buttons without `DOC_MASTER_MAKER` and the Review button
      without `DOC_MASTER_CHECKER` (R13.10); an `ERR-DOC-001` on authorize and `ERR-DOC-002` on
      delete surface the exact messages and leave the row unchanged (R13.8, R13.9).
    - `AuthorizeDialog.test.tsx` — live vs pending render side by side (R13.5); Reject blocks submit
      until a non-empty remark is entered (R13.7).
    - `ApplicabilityRulesForm.test.tsx` — Constitution / Entity Role selects reject free text and
      only offer lookup options (R13.2); add/remove row behavior.
    - `ValidationConfigForm.test.tsx` — Mask Pattern disabled until Requires Masking is on (R9.3 UI
      affordance); MIME checkboxes restricted to PDF/JPG/PNG (R9.9).

## Notes

- Tasks marked with `*` are optional (tests) and can be skipped for a faster MVP; core
  implementation tasks are never optional.
- This `tasks.md` is the copy-paste deliverable: the user applies each CREATE/MODIFY block. Do not
  run `dotnet build` / `dotnet test` / `npm run build` / `npm test` as part of producing the spec.
- Backend targets **SQL Server**; property-based tests use **FsCheck.Xunit** (min 100 iterations),
  one test per property, tagged `// Feature: document-master-management, Property {n}: {text}`.
- The migration (task 4.3) is a command note; run it only after tasks 1–5 compile, with the
  SystemAdmin host as the EF startup project.
- Backend reuses the existing host, JWT auth, `ValidationBehavior<,>`, `ExceptionHandlingMiddleware`,
  `AddFinnovaRepository`/`FinnovaDbContext`, `ILookupRepository`, `PaginatedResponse<T>`, and the
  gateway `systemadmin-cluster` + `ua-*-alias` pattern — none of these are re-created.
- Frontend reuses the shared axios instance (Bearer token + 10s timeout + 401/403/409 toasts), the
  lookup dropdown service, and the MUI master-page pattern — **no Redux, no RxJS**. English-only,
  single English display fields, **no RTL / no Arabic**.
- Open assumptions carried from the design (flag for confirmation): A1 token issuance / role
  claims and whether `SystemAdmin` is a super-role; A2 the Lookup Module scope value; A3 the real
  `IDocumentUsageChecker` adapter from Customer Master; A4 single pending change per document.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "2.1", "3.1", "3.2"] },
    { "id": 1, "tasks": ["4.1", "4.2", "5.1", "5.3", "6.1", "6.3"] },
    { "id": 2, "tasks": ["5.2", "5.4", "6.2", "6.4"] },
    { "id": 3, "tasks": ["4.3", "5.5", "7.1"] },
    { "id": 4, "tasks": ["7.2", "7.3", "7.4", "7.5", "7.6", "7.7", "7.8", "7.9", "7.10", "7.11", "7.12", "7.13", "7.14", "7.15"] },
    { "id": 5, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 6, "tasks": ["9.1", "9.2"] },
    { "id": 7, "tasks": ["9.3", "9.4", "9.5", "9.6"] },
    { "id": 8, "tasks": ["10.1", "10.2"] },
    { "id": 9, "tasks": ["10.3", "10.4", "10.5", "10.6"] },
    { "id": 10, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 11, "tasks": ["11.6", "11.7", "11.8"] },
    { "id": 12, "tasks": ["12.1", "12.2", "12.3", "12.4"] }
  ]
}
```
