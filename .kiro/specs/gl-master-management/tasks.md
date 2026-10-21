# Implementation Plan: GL Master Management

**JIRA:** FINNOVA (GL Master) · **Module:** SystemAdmin · **Master:** GL Master / General Ledger Account Master

## Overview

This plan implements the GL Master feature as a vertical slice inside the **existing**
`Finnova.SystemAdminService` host, mirroring the Nationality Master and Lookup Master layered
CQRS conventions. Work proceeds bottom-up by dependency: domain entities + enums → domain
exceptions → contracts → EF configuration + DbContext + migration → repositories + DI → mapper →
CQRS service layer (commands, queries, validators) → controller + error-handling middleware
extension + gateway alias → tests (property, edge, integration) → frontend (separate `Finnova-UI`
repo: model → service → page → tests). Each step builds on the previous and ends with wiring into
the running host and UI.

The host's JWT authentication, `SystemAdmin` authorization policy, MediatR `ValidationBehavior`
pipeline, `ExceptionHandlingMiddleware`, `AddFinnovaRepository` registration, global
`JsonStringEnumConverter`, and the `PaginatedResponse<T>` contract already exist and are reused,
not re-created.

Implementation language: **C#** (.NET) for the backend; **TypeScript/React** for the frontend
(per the design; no language selection needed).

> **Documents-only convention:** This tasks file is the deliverable. Each task carries the
> complete code to **copy and paste** into the real source tree, marked **CREATE** or **MODIFY**.
> For MODIFY tasks the exact snippet and its anchor are shown. Do not run `dotnet build` /
> `dotnet test` / `npm run build` / `npm test` as part of producing this spec — the user applies
> the code and runs the builds.

## Tasks

- [ ] 1. Add domain entities and enums
  - [ ] 1.1 Add GL enums

    **CREATE `Finnova.Models/Domain/Enums/GLAccountType.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    /// <summary>Financial classification of a GL account (R1.11). Immutable after create (R3.2).</summary>
    public enum GLAccountType
        {
        Asset = 0,
        Liability = 1,
        Income = 2,
        Expense = 3
            }
        });
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    ```

    ```csharp

    {
            }
            catch (Exception ex)
            {
                var isGl = ctx.Request.Path.StartsWithSegments("/api/gl", StringComparison.OrdinalIgnoreCase);
                var validationCode = isGl ? "ERR-GL-400" : "ERR-GL-400";
                var fallbackCode = isGl ? "ERR-GL-500" : "ERR-GL-500";

                var (status, code, detail) = ex switch
                {
                    // ---- GL branch (typed, no message sniffing) ----
                    GLNotFoundException => (StatusCodes.Status404NotFound, "ERR-GL-404", ex.Message),
    **CREATE `Finnova.Models/Domain/Enums/GLAccountStatus.cs`**
                    GLDuplicateCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
    namespace Finnova.Models.Domain.Enums;
                    GLRetiredCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
    /// <summary>Lifecycle state of a GL account (R1.2). Defaults to Active.</summary>
    public enum GLAccountStatus
                    GLDuplicateNameException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
        Active = 0,
        Inactive = 1
                    GLConflictException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),

                    // ---- shared: validation failures and unhandled fallback ----
                    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationCode, v.Message),
                    _ => (StatusCodes.Status500InternalServerError, fallbackCode, "Unexpected error.")
                };

                var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
                problem.Extensions["code"] = code;
                ctx.Response.StatusCode = status;
                await ctx.Response.WriteAsJsonAsync(problem);
            }
        }
    }
    ```

    ```csharp
    namespace Finnova.Models.Domain.Enums;

    {
    }
    **CREATE `Finnova.Models/Domain/Enums/GLAccountAuditAction.cs`**
    /// <summary>Discriminates the mutation an audit entry records (R9.1, R9.2).</summary>
    public enum GLAccountAuditAction
        Create = 0,
        Update = 1
    ```
    - _Requirements: 1.2, 1.11, 3.2, 9.1, 9.2_


    **CREATE `Finnova.Models/Domain/Entities/GLAccount.cs`**
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// </summary>
    public class GLAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();






        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```

    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// </summary>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
    ```
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7, 1.8, 3.2, 5.1, 5.2_


    **CREATE `Finnova.Models/Domain/Entities/GLAccountAuditEntry.cs`**
    ```csharp
  - [ ] 1.2 Add the GLAccount entity and its SL-link child entity
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// </summary>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
    /// A single General Ledger account in the chart of accounts (R1). India-only platform:
    /// one English GLName. GLCode and AccountType are immutable after create (R3).
    ```

    ```csharp
    namespace Finnova.Models.Domain.Entities;

        public string GLCode { get; set; } = string.Empty;          // max 20, unique CI, alphanumeric, not "000" (R2)
        public string GLName { get; set; } = string.Empty;          // max 100, single English name (R1, R4)
        public GLAccountType AccountType { get; set; }              // stored int; immutable (R1.11, R3.2)
        public string PostingGroup { get; set; } = string.Empty;    // max 50 (R1, R4.5)
        public string Currency { get; set; } = "INR";             // max 3, default INR (R1.6)
        public GLAccountStatus Status { get; set; } = GLAccountStatus.Active; // default Active (R1.2)
        public bool AllowManualJournal { get; set; } = true;        // default true (R1.4)
        public bool AllowSystemJournal { get; set; } = true;        // default true (R1.5)

        public string CompanyCode { get; set; } = string.Empty;     // from request context (R1.7)
        public string UserBranch { get; set; } = string.Empty;      // from request context (R1.7)

        public DateTime EffectiveStartDate { get; set; }            // required (R5.1)
        public DateTime? EffectiveEndDate { get; set; }             // optional (R5.2)

        // Linked SL accounts modeled as a child collection (R1.8, R4.6).
        public List<GLAccountSLLink> LinkedSLAccounts { get; set; } = new();

    **CREATE `Finnova.Models/Domain/Entities/GLAccountSLLink.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// One sub-ledger (SL) account linked to a GL account (R1.8). Stored as provided; no
    /// cross-catalog validation against an external SL catalog (SL dependency assumption).
    /// </summary>
    public class GLAccountSLLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GLAccountId { get; set; }                       // FK/index back to the owning GL account
        public string SLAccountCode { get; set; } = string.Empty;   // max 50
    }
    /// <summary>
    /// </summary>
    {
  - [ ] 1.3 Add the GL audit entry and posting-group mapping entities
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```
    - _Requirements: 6.1, 6.2, 9.1, 9.2, 9.5, 9.7, 9.8_


    /// Immutable audit record capturing one change to a GL account (R9). Written on create and on
    /// update within the same unit of work. Never updated or deleted (R9.7, R9.8).
    **CREATE `Finnova.Models/Domain/Exceptions/GLAccountNotFoundException.cs`**
    public class GLAccountAuditEntry
    ```csharp


        public Guid GLAccountId { get; set; }                       // affected GL account (R9.1/9.2)
        public GLAccountAuditAction Action { get; set; }            // Create | Update (R9.1/9.2)

        /// <summary>Serialized before/after of changed fields: {"field":{"old":...,"new":...}}.
        /// On Create, captures the resulting field values (old = null). (R9.1, R9.2)</summary>
        public string ChangesJson { get; set; } = string.Empty;     // nvarchar(max)

        /// <summary>Short human-readable summary of what changed (e.g. "Status: Active -> Inactive").</summary>
        public string Summary { get; set; } = string.Empty;         // max 1000

        public string ChangedBy { get; set; } = string.Empty;       // acting admin id from JWT (R9.1/9.2)
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC, ms resolution (R9.1/9.2)
    {
    }
    ```
    **CREATE `Finnova.Models/Domain/Entities/PostingGroupMapping.cs`**

    ```csharp

    /// <summary>
    /// </summary>
    {
    /// Resolves a required posting group to exactly one GL account (R6). At most one mapping per
    /// posting group (unique PostingGroup). GLAccountId is a reference without a DB FK constraint
    /// so the mapping store is independent of GL row lifecycle; existence/active checks are done
    /// in the handler at set-time (R6.4, R6.5).

    public class PostingGroupMapping
    }
    ```
        public string PostingGroup { get; set; } = string.Empty;    // max 50, unique (R6.1)
        public Guid GLAccountId { get; set; }                       // mapped GL account (R6.2)

    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    {

- [ ] 2. Add GL domain exceptions
  - [ ] 2.1 Add the five GL domain exceptions
    {
        public const string ErrorCode = "ERR-GL-409";
        public GLDuplicateCodeException() : base("GL code must be unique") { }
    namespace Finnova.Models.Domain.Exceptions;
    }
    /// <summary>
    /// Thrown when an update or postability query targets a GL account that does not exist
    /// (R4.7, R7.4). Maps to error code ERR-GL-404.
    /// </summary>
    public class GLAccountNotFoundException : Exception
    ```
        public GLAccountNotFoundException(Guid id)
            : base($"GL account '{id}' was not found.")
        {

    {

    {
    **CREATE `Finnova.Models/Domain/Exceptions/GLAccountDuplicateCodeException.cs`**
        public const string ErrorCode = "ERR-GL-409";
    namespace Finnova.Models.Domain.Exceptions;
        public GLDuplicateNameException() : base("GL name must be unique") { }
    }
    ```
    /// Thrown when a GL create is attempted with a code that already exists (case-insensitive,
    /// trimmed) (R2.6). Carries the user-facing message the UI displays and lets the middleware
    /// map it to 409 (ERR-GL-409) by type, avoiding message-sniffing on generic validation.

    public class GLAccountDuplicateCodeException : Exception
    **CREATE `Finnova.Models/Domain/Exceptions/GLValidationException.cs`**
        /// <summary>Stable error code returned to clients (R2.6).</summary>
        public const string ErrorCode = "ERR-GL-409";
    ```csharp
        public GLAccountDuplicateCodeException()
            : base("GL Code already exists")
        {
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// </summary>
    **CREATE `Finnova.Models/Domain/Exceptions/GLAccountImmutableFieldException.cs`**
    {
    }
    ```
    /// <summary>
    /// Thrown when an update attempts to change an immutable financial attribute — GL Code or
    /// Account Type (R3.1, R3.2). Mapped to 409 Conflict (ERR-GL-409): the request is
    /// well-formed but conflicts with the fixed-after-create invariant.
    /// </summary>
    public class GLAccountImmutableFieldException : Exception
    - _Requirements: 2.6, 3.1, 3.2, 4.7, 6.3, 6.4, 6.5, 7.4_
        /// <summary>Stable error code returned to clients (R3.1, R3.2).</summary>
        public const string ErrorCode = "ERR-GL-409";

        public GLAccountImmutableFieldException(string fieldName)
            : base($"{fieldName} is immutable and cannot be changed.")

    ```csharp
    **CREATE `Finnova.Models/Domain/Exceptions/PostingGroupMappingValidationException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when a set-mapping request is invalid: unknown posting group (R6.3), missing mapped
    /// GL account (R6.4), or an inactive mapped GL account (R6.5). Maps to 400 (ERR-GL-400).
    /// </summary>
    public class PostingGroupMappingValidationException : Exception
        /// <summary>Stable error code returned to clients (R6.3, R6.4, R6.5).</summary>
        public const string ErrorCode = "ERR-GL-400";
        public PostingGroupMappingValidationException(string message)
            : base(message)
    using Finnova.Models.Domain.Enums;

    /// General GL domain validation failure not expressed by a more specific exception.
    /// Maps to 400 (ERR-GL-400).

    public class GLValidationException : Exception
    public record CreateGLAccountRequest(
        /// <summary>Stable error code returned to clients.</summary>
        public const string ErrorCode = "ERR-GL-400";

        public GLValidationException(string message)
            : base(message)
        {
        string GLCode,




- [ ] 3. Add GL contracts (request/response records)
  - [ ] 3.1 Add the GL request/response records

    **CREATE `Finnova.Models/Contracts/GLAccounts/CreateGLAccountRequest.cs`**

    /// <summary>Admin-facing GL account response; AccountType/Status serialize as names.</summary>
    public record GLAccountResponse(
    namespace Finnova.Models.Contracts.GLAccounts;
        Guid Id,
    /// <summary>Create payload. Optional fields are nullable so the service applies defaults.</summary>
        string GLCode,
        DateTime CreatedAt,
        DateTime UpdatedAt);
        string GLName,
        GLAccountType AccountType,
        string PostingGroup,
        string? Currency,                       // defaults to INR (R1.6)
        GLAccountStatus? Status,                // defaults to Active (R1.2)
        bool? AllowManualJournal,               // defaults to true (R1.4)
        bool? AllowSystemJournal,               // defaults to true (R1.5)
        DateTime EffectiveStartDate,            // required (R5.1)
        DateTime? EffectiveEndDate,             // optional (R5.2)
        List<string>? LinkedSLAccounts);        // defaults to empty (R1.8)
    ```
    ```
    **CREATE `Finnova.Models/Contracts/GLAccounts/UpdateGLAccountRequest.cs`**
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.GLAccounts;
    /// <summary>Update payload. Only non-financial attributes are editable (R4). AccountType and
    /// GLCode are carried only for the equality/immutability check (R3.1-R3.5); they are NOT updated.</summary>
    public record UpdateGLAccountRequest(
        string? GLCode,                         // optional; blank/omitted = unchanged (R3.5)
        GLAccountType? AccountType,             // optional; equality check only (R3.2, R3.4)
        string GLName,                          // editable, required (R4.2, R4.3)
        GLAccountStatus Status,                 // editable (R4.4)
        string PostingGroup,                    // editable, required (R4.5)
        bool AllowManualJournal,                // editable (R4.1)
        bool AllowSystemJournal,                // editable (R4.1)
        DateTime EffectiveStartDate,            // editable (R4.1, R5)
        DateTime? EffectiveEndDate,             // editable (R4.1, R5)
        List<string>? LinkedSLAccounts);        // editable; <=100 entries (R4.6)
    ```

    **CREATE `Finnova.Models/Contracts/GLAccounts/GLAccountResponse.cs`**
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.GLAccounts;
        Guid Id,
        Guid GLAccountId,
        string Summary,
        string ChangedBy,
        DateTime ChangedAtUtc);
    ```
        string GLName,
        GLAccountType AccountType,
        string PostingGroup,
        string Currency,
        GLAccountStatus Status,
        bool AllowManualJournal,
        bool AllowSystemJournal,
        string CompanyCode,
        string UserBranch,
        DateTime EffectiveStartDate,
        DateTime? EffectiveEndDate,
        List<string> LinkedSLAccounts,

    ```csharp
    using Finnova.Models.Domain.Enums;

    **CREATE `Finnova.Models/Contracts/GLAccounts/GLAccountAuditEntryResponse.cs`**
    ```csharp
    namespace Finnova.Models.Contracts.GLAccounts;

    /// <summary>One audit row (R9.5).</summary>
    public record GLAccountAuditEntryResponse(
        Guid GLAccountId,

        string Action,                          // "Create" | "Update"
        string ChangesJson,


    ```



    **CREATE `Finnova.Repository/Configuration/GLAccountConfiguration.cs`**
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    public class GLAccountConfiguration : IEntityTypeConfiguration<GLAccount>
    **CREATE `Finnova.Models/Contracts/GLAccounts/GLAccountPostabilityResponse.cs`**
    {
        public void Configure(EntityTypeBuilder<GLAccount> builder)
    namespace Finnova.Models.Contracts.GLAccounts;

    /// <summary>Derived postability at a date (R7).</summary>
    public record GLAccountPostabilityResponse(
        {
        string GLCode,
        bool Postable,
        DateTime Date,
        GLAccountStatus Status,
        DateTime EffectiveStartDate,
        DateTime? EffectiveEndDate);
    ```
            builder.ToTable("gl_accounts");
    **CREATE `Finnova.Models/Contracts/GLAccounts/PostingGroupMappingResponse.cs`**
    ```csharp
    namespace Finnova.Models.Contracts.GLAccounts;
            builder.HasKey(x => x.Id);
    /// <summary>One required posting group and its (optional) mapped GL account (R6.6).</summary>
    public record PostingGroupMappingResponse(
        string PostingGroup,
        Guid? GLAccountId,                      // null when not yet mapped (R6.6)
        string? GLCode,
        string? GLName,
        bool IsMapped);
    ```

    **CREATE `Finnova.Models/Contracts/GLAccounts/SetPostingGroupMappingRequest.cs`**
    ```csharp
    namespace Finnova.Models.Contracts.GLAccounts;

    /// <summary>Body for setting one posting-group-to-GL mapping (R6.2).</summary>
    public record SetPostingGroupMappingRequest(Guid GLAccountId);


    > Reuse the existing `PaginatedResponse<T>` for the paged search (do not create a new paged
    > contract): `PaginatedResponse<GLAccountResponse>(List<GLAccountResponse> Data, int Total,
    > int Page, int PageSize, int TotalPages)`.
    - _Requirements: 1.1, 4.1, 6.6, 7.1, 8.6, 9.5_
                   .WithOne()
- [ ] 4. Configure persistence for the new entities
  - [ ] 4.1 Add EF Core entity configurations for GLAccount and SL links
        }
    }
    ```

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    {
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.GLAccountId);
        }
    }
            builder.Property(x => x.GLCode).IsRequired().HasMaxLength(20);
            builder.Property(x => x.GLName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.AccountType).IsRequired();                 // stored as int
            builder.Property(x => x.PostingGroup).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            builder.Property(x => x.Status).IsRequired();                      // stored as int
            builder.Property(x => x.AllowManualJournal).IsRequired();
            builder.Property(x => x.AllowSystemJournal).IsRequired();
            builder.Property(x => x.CompanyCode).HasMaxLength(20);
            builder.Property(x => x.UserBranch).HasMaxLength(20);
            builder.Property(x => x.EffectiveStartDate).IsRequired();
    ```
            // Uniqueness of GLCode (R2.1/2.6). SQL Server default collation (CI) backs the
            // case-insensitive rule. Codes are trimmed by the handler before persistence.
            builder.HasIndex(x => x.GLCode).IsUnique();
            // Query path: filter by (AccountType, Status), search/order by GLName/GLCode (R8).
            builder.HasIndex(x => x.GLName);
            builder.HasIndex(x => new { x.AccountType, x.Status });
            // Owned/related child collection (R1.8, R4.6).
            builder.HasMany(x => x.LinkedSLAccounts)
                   .WithOne()
                   .HasForeignKey(l => l.GLAccountId);
    - _Requirements: 1.8, 2.1, 2.6, 4.6, 8.1, 8.2, 8.13_


    **CREATE `Finnova.Repository/Configuration/GLAccountAuditEntryConfiguration.cs`**
    **CREATE `Finnova.Repository/Configuration/GLAccountSLLinkConfiguration.cs`**
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    {
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.GLAccountId).IsRequired();
        }
    }
    ```

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    {
        {
            builder.HasKey(x => x.Id);
        }
    public class GLAccountSLLinkConfiguration : IEntityTypeConfiguration<GLAccountSLLink>
    }
        public void Configure(EntityTypeBuilder<GLAccountSLLink> builder)
    ```
            builder.ToTable("gl_account_sl_links");

            builder.Property(x => x.SLAccountCode).IsRequired().HasMaxLength(50);


    **MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`** — add these three lines immediately
    **after** the existing `public DbSet<Asset> Assets => Set<Asset>();` line (the last DbSet, just
    before `protected override void OnModelCreating`):
    ```csharp
        public DbSet<GLAccount> GLAccounts => Set<GLAccount>();
    ```

  - [ ] 4.2 Add EF Core entity configurations for audit entries and posting-group mappings



    **CREATE `Finnova.Repository/Interfaces/IGLAccountRepository.cs`**
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Repository.Interfaces;
    public class GLAccountAuditEntryConfiguration : IEntityTypeConfiguration<GLAccountAuditEntry>

        public void Configure(EntityTypeBuilder<GLAccountAuditEntry> builder)
    public interface IGLAccountRepository : IRepository<GLAccount>
            builder.ToTable("gl_account_audit_entries");
    {


    }
            builder.Property(x => x.Action).IsRequired();                     // stored as int
            builder.Property(x => x.ChangesJson).IsRequired();                // nvarchar(max) (no HasMaxLength)
            builder.Property(x => x.Summary).HasMaxLength(1000);
            builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
            builder.Property(x => x.ChangedAtUtc).IsRequired();
            // Read path: entries for a GL account ordered by time desc then id desc (R9.5).
            // No FK to gl_accounts, so audit survives independently; "missing id -> empty" (R9.6).
            builder.HasIndex(x => new { x.GLAccountId, x.ChangedAtUtc });
    ```

    ```csharp
    **CREATE `Finnova.Repository/Configuration/PostingGroupMappingConfiguration.cs`**
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    {
    }
    ```

    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    {

    }
    ```
    - _Requirements: 2.1, 6.2, 6.6, 8.1, 8.2, 8.3, 8.13, 9.5, 9.6_
    public class PostingGroupMappingConfiguration : IEntityTypeConfiguration<PostingGroupMapping>

        public void Configure(EntityTypeBuilder<PostingGroupMapping> builder)

            builder.ToTable("posting_group_mappings");
    **CREATE `Finnova.Repository/Repositories/GLAccountRepository.cs`**
            builder.Property(x => x.PostingGroup).IsRequired().HasMaxLength(50);
            builder.HasIndex(x => x.PostingGroup).IsUnique();                 // at most one mapping per group (R6.1)
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    > These four configurations are auto-discovered by the existing
    > `ApplyConfigurationsFromAssembly(typeof(FinnovaDbContext).Assembly)` call in
    > `FinnovaDbContext.OnModelCreating` — no manual registration is needed.
    - _Requirements: 6.1, 9.5, 9.6, 9.7, 9.8_
    using Finnova.Repository.Interfaces;
  - [ ] 4.3 Register the new DbSets on FinnovaDbContext

    namespace Finnova.Repository.Repositories;

    {
        public GLAccountRepository(FinnovaDbContext context) : base(context) { }

    public DbSet<GLAccountAuditEntry> GLAccountAuditEntries => Set<GLAccountAuditEntry>();
    public DbSet<PostingGroupMapping> PostingGroupMappings => Set<PostingGroupMapping>();
        public async Task<(List<GLAccount> Items, int Total)> GetPagedAsync(
    (`GLAccountSLLink` is reachable via the `GLAccount.LinkedSLAccounts` navigation; a dedicated
    DbSet is optional and not added.)
    - _Requirements: 1.1, 6.1, 9.1_
        {
  - [ ] 4.4 Generate the AddGLMaster migration (command only — the user runs it)

    **Run (not part of this documents-only spec):**
    ```
    dotnet ef migrations add AddGLMaster --project Finnova.Repository --startup-project Finnova.SystemAdminService
    ```
    Verify the generated migration creates `gl_accounts`, `gl_account_sl_links`,
    `gl_account_audit_entries`, and `posting_group_mappings` with the unique `GLCode` and
    `PostingGroup` indexes from tasks 4.1–4.2. Apply with `dotnet ef database update`. No seed data
    is required; any smoke seed must be English-only (e.g. `GL1001` / `"Cash in Hand"`).
    - _Requirements: 1.1, 6.1, 9.1_
            {
- [ ] 5. Implement the repository layer
  - [ ] 5.1 Define the repository interfaces
            }

            var total = await query.CountAsync(ct);
            var items = await query
            return (items, total);
        }

        {
            return await DbSet.AsNoTracking().AnyAsync(
        }
        /// <summary>Search + filter + page (R8). Term filters GLCode OR GLName (substring, CI);
        /// null/blank term = no filter. Optional AccountType/Status equality filters. Ordered by
        /// GLCode asc (CI, deterministic). Returns page + total (pre-paging).</summary>
        Task<(List<GLAccount> Items, int Total)> GetPagedAsync(
            string? searchTerm, GLAccountType? accountType, GLAccountStatus? status,
            int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>Case-insensitive, trimmed uniqueness check on GLCode (R2). excludeId supports
        /// self-exclusion for update scenarios.</summary>
        Task<bool> ExistsByGLCodeAsync(string glCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
        {
        /// <summary>Load a GL account with its LinkedSLAccounts included (for update/response).</summary>
        Task<GLAccount?> GetWithLinksAsync(Guid id, CancellationToken cancellationToken = default);
        }
    }
    ```
    **CREATE `Finnova.Repository/Interfaces/IGLAccountAuditRepository.cs`**
    - _Requirements: 2.1, 2.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.12, 8.13_


    **CREATE `Finnova.Repository/Repositories/GLAccountAuditRepository.cs`**
    ```csharp
    public interface IGLAccountAuditRepository : IRepository<GLAccountAuditEntry>
    using Microsoft.EntityFrameworkCore;
        /// <summary>Audit entries for a GL account, ordered ChangedAtUtc desc then Id desc (R9.5).
        /// Missing id returns an empty list, never an error (R9.6).</summary>
        Task<List<GLAccountAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken cancellationToken = default);
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    **CREATE `Finnova.Repository/Interfaces/IPostingGroupMappingRepository.cs`**

    namespace Finnova.Repository.Repositories;

    public interface IPostingGroupMappingRepository : IRepository<PostingGroupMapping>
    {
        /// <summary>All stored posting-group mappings (R6.6).</summary>
        Task<List<PostingGroupMapping>> GetAllMappingsAsync(CancellationToken cancellationToken = default);
        /// <summary>The mapping for one posting group, or null if unmapped (R6.2 overwrite path).</summary>
        Task<PostingGroupMapping?> GetByPostingGroupAsync(string postingGroup, CancellationToken cancellationToken = default);
    }
    ```
  - [ ] 5.2 Implement GLAccountRepository

    **CREATE `Finnova.Repository/Repositories/GLAccountRepository.cs`**
    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    public class GLAccountRepository : RepositoryBase<GLAccount>, IGLAccountRepository
    {

    {
            string? searchTerm, GLAccountType? accountType, GLAccountStatus? status,
            int page, int pageSize, CancellationToken cancellationToken = default)
    }
            var query = DbSet.AsNoTracking().Include(x => x.LinkedSLAccounts).AsQueryable();
    ```
            if (!string.IsNullOrWhiteSpace(searchTerm))
    - _Requirements: 9.5, 9.6_
                var term = searchTerm.Trim();
                // EF Core translates Contains to SQL LIKE; default CI collation makes it CI (R8.1, R8.5).
                query = query.Where(x => x.GLCode.Contains(term) || x.GLName.Contains(term));

            if (accountType.HasValue) query = query.Where(x => x.AccountType == accountType.Value); // R8.2
            if (status.HasValue)      query = query.Where(x => x.Status == status.Value);           // R8.3

            var total = await query.CountAsync(cancellationToken);           // total before paging (R8.6, R8.12)

    **CREATE `Finnova.Repository/Repositories/PostingGroupMappingRepository.cs`**
    ```csharp
                .OrderBy(x => x.GLCode)                                       // deterministic CI order (R8.13)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
        public async Task<bool> ExistsByGLCodeAsync(
            string glCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    using Finnova.Repository.Interfaces;
            var c = glCode.Trim();

                x => x.GLCode == c && (excludeId == null || x.Id != excludeId), cancellationToken); // CI via collation (R2.1)
    namespace Finnova.Repository.Repositories;

        public async Task<GLAccount?> GetWithLinksAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.LinkedSLAccounts)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);


    }
    ```
    - _Requirements: 6.2, 6.6_


    **MODIFY `Finnova.Repository/DependencyInjection.cs`** — add these three lines immediately
    **after** the existing `services.AddScoped<IAssetRepository, AssetRepository>();` line (just
    before `return services;`):
    ```csharp
    ```
  - [ ] 5.3 Implement GLAccountAuditRepository


    **CREATE `Finnova.Service/GLAccount/GLPostingGroups.cs`**
    ```csharp

    /// <summary>
    /// </summary>
    {
    {

    }
    ```
    public class GLAccountAuditRepository : RepositoryBase<GLAccountAuditEntry>, IGLAccountAuditRepository

        public GLAccountAuditRepository(FinnovaDbContext context) : base(context) { }

        public async Task<List<GLAccountAuditEntry>> GetByGLAccountIdAsync(
            Guid glAccountId, CancellationToken cancellationToken = default)
    **CREATE `Finnova.Service/Mappers/GLAccountMapper.cs`**
            return await DbSet.AsNoTracking()
                .Where(x => x.GLAccountId == glAccountId)
                .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R9.5
                .ToListAsync(cancellationToken);                                       // empty list when none (R9.6)
        }
    ```csharp
    using Finnova.Models.Domain.Entities;

  - [ ] 5.4 Implement PostingGroupMappingRepository
    // Alias the entity so it is unambiguous against the Finnova.Service.GLAccount namespace.
    using GLAccountEntity = Finnova.Models.Domain.Entities.GLAccount;

    namespace Finnova.Service.Mappers;

    {

            => items.Select(i => i.ToResponse()).ToList();

    public class PostingGroupMappingRepository : RepositoryBase<PostingGroupMapping>, IPostingGroupMappingRepository
    }
        public PostingGroupMappingRepository(FinnovaDbContext context) : base(context) { }
    ```
        public async Task<List<PostingGroupMapping>> GetAllMappingsAsync(CancellationToken cancellationToken = default)
            => await DbSet.AsNoTracking().ToListAsync(cancellationToken);
    - _Requirements: 1.1, 4.1, 9.1, 9.2, 9.5_
        public async Task<PostingGroupMapping?> GetByPostingGroupAsync(
            string postingGroup, CancellationToken cancellationToken = default)
            => await DbSet.FirstOrDefaultAsync(x => x.PostingGroup == postingGroup, cancellationToken);


  - [ ] 5.5 Register the three repositories in DI

    services.AddScoped<IGLAccountRepository, GLAccountRepository>();
    services.AddScoped<IGLAccountAuditRepository, GLAccountAuditRepository>();
    services.AddScoped<IPostingGroupMappingRepository, PostingGroupMappingRepository>();
    ```
    - _Requirements: 1.1, 6.1, 9.1_

- [ ] 6. Implement the GL mapper and the shared required-posting-groups constant
  - [ ] 6.1 Add the required-posting-groups constant

    namespace Finnova.Service.GLAccount;

    /// <summary>
    /// The required posting groups that may be resolved to a GL account (R6.1, R6.3). Shared by
    /// the set-mapping command/validator and the read-mappings query so both use one source.
    /// </summary>
    public static class GLPostingGroups
    {
        public static readonly IReadOnlyList<string> Required = new[]
        {
            "Customer",
            "Dealer",
            "Insurance",
            "Unearned Income",
            "Lease Receivable"
        };

        public static bool IsRequired(string postingGroup) =>
            Required.Any(g => string.Equals(g, postingGroup?.Trim(), StringComparison.Ordinal));
    }
    ```
    - _Requirements: 6.1, 6.3, 6.6_

  - [ ] 6.2 Implement the static ToResponse mappers
    **CREATE `Finnova.Service/GLAccount/Commands/CreateGLAccount/CreateGLAccountCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;
    using Finnova.Models.Contracts.GLAccounts;


    ```

    ```csharp
    using System.Text.RegularExpressions;
    public static class GLAccountMapper
    using FluentValidation;
        /// <summary>Maps a GL account to the admin-facing response, preserving every field (R1.1).
        /// AccountType/Status pass through (serialized by name); SL links project to List&lt;string&gt;.</summary>
        public static GLAccountResponse ToResponse(this GLAccountEntity x) => new(
            x.Id,
            x.GLCode,
            x.GLName,
            x.AccountType,
            x.PostingGroup,
            x.Currency,
            x.Status,
            x.AllowManualJournal,
            x.AllowSystemJournal,
            x.CompanyCode,
            x.UserBranch,
            x.EffectiveStartDate,
            x.EffectiveEndDate,
            x.LinkedSLAccounts.Select(l => l.SLAccountCode).ToList(),
            x.CreatedAt,
            x.UpdatedAt);

        /// <summary>Maps a flat list of GL accounts to a flat list of responses.</summary>
        public static List<GLAccountResponse> ToResponseList(this IEnumerable<GLAccountEntity> items)

    public class CreateGLAccountCommandValidator : AbstractValidator<CreateGLAccountCommand>
        /// <summary>Maps a single audit entry to its response, projecting Action to its name (R9.5).</summary>
        public static GLAccountAuditEntryResponse ToResponse(this GLAccountAuditEntry a) => new(
            a.Id,
            a.GLAccountId,
            a.Action.ToString(),
            a.ChangesJson,
            a.Summary,
            a.ChangedBy,
            a.ChangedAtUtc);
    {
        private static readonly Regex Alphanumeric = new("^[A-Za-z0-9]+$", RegexOptions.Compiled);

        public CreateGLAccountCommandValidator()
  - [ ]* 6.3 Write property test for the mapper
    - **Property 21: Mapper preserves fields**
    - **Validates: Requirements 1.1, 4.1, 9.1, 9.2, 9.5**
    - Full test code is provided in task 9.3 (grouped with the property-test suite so all FsCheck
      infrastructure lives together).
        {
- [ ] 7. Implement the CQRS service slice
  - [ ] 7.1 Implement CreateGLAccountCommand, validator, and handler
            RuleFor(x => x.Request.GLCode)





    namespace Finnova.Service.GLAccount.Commands.CreateGLAccount;
            RuleFor(x => x.Request)
    /// <summary>Create a GL account. CompanyCode/UserBranch/ActingAdmin are resolved from the
    /// validated principal in the controller, not from the request body (R1.7, R9.1).</summary>
    public record CreateGLAccountCommand(
        CreateGLAccountRequest Request,
        string CompanyCode,
        string UserBranch,
        string ActingAdmin) : IRequest<GLAccountResponse>;
        }
    }
    **CREATE `Finnova.Service/GLAccount/Commands/CreateGLAccount/CreateGLAccountCommandValidator.cs`**
    ```

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    namespace Finnova.Service.GLAccount.Commands.CreateGLAccount;
    using Finnova.Service.Mappers;


    public class CreateGLAccountCommandHandler : IRequestHandler<CreateGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;

            // R1.9/R2.2 required; R2.5 max 20; R2.3 alphanumeric; R2.4 not reserved "000".
        public CreateGLAccountCommandHandler(
                .NotEmpty().WithMessage("GL Code is required.")
                .MaximumLength(20).WithMessage("GL Code must not exceed 20 characters.")
                .Must(c => !string.IsNullOrWhiteSpace(c) && Alphanumeric.IsMatch(c.Trim()))
                    .WithMessage("GL Code must be alphanumeric.")
                .Must(c => c == null || c.Trim() != "000")
                    .WithMessage("GL Code '000' is reserved.");
            IGLAccountRepository repository,
            // R1.9/R1.10 — Name required, max 100.
            RuleFor(x => x.Request.GLName)
                .NotEmpty().WithMessage("GL Name is required.")
                .MaximumLength(100).WithMessage("GL Name must not exceed 100 characters.");
        {
            // R1.9/R1.10 — Posting Group required, max 50.
            RuleFor(x => x.Request.PostingGroup)
                .NotEmpty().WithMessage("Posting Group is required.")
                .MaximumLength(50).WithMessage("Posting Group must not exceed 50 characters.");
            // R1.11 — Account Type must be a defined enum.
            RuleFor(x => x.Request.AccountType)
                .IsInEnum().WithMessage("Account Type is invalid.");
            _repository = repository;
            // R1.3 — Status, when provided, must be a defined enum.
            RuleFor(x => x.Request.Status!.Value)
                .IsInEnum().WithMessage("Status is invalid.")
                .When(x => x.Request.Status.HasValue);
            _auditRepository = auditRepository;
            // R5.1 — Effective Start Date required.
            RuleFor(x => x.Request.EffectiveStartDate)
                .NotEmpty().WithMessage("Effective Start Date is required.");

            // R5.3/R5.4 — end null, or end >= start (equal allowed).
                .Must(r => r.EffectiveEndDate == null || r.EffectiveEndDate.Value.Date >= r.EffectiveStartDate.Date)
                .WithMessage("Effective End Date must be on or after the Effective Start Date.");

            // R4.6-style guard also on create — at most 100 linked SL accounts.
            RuleFor(x => x.Request.LinkedSLAccounts)
                .Must(l => l == null || l.Count <= 100)
                .WithMessage("Linked SL Accounts must not exceed 100 entries.");
        }

        {
            var code = r.GLCode.Trim();
    **CREATE `Finnova.Service/GLAccount/Commands/CreateGLAccount/CreateGLAccountCommandHandler.cs`**

    using System.Text.Json;
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;

            {
                GLCode = code,
            };


    using GLAccountEntity = Finnova.Models.Domain.Entities.GLAccount;
    using GLAccountSLLink = Finnova.Models.Domain.Entities.GLAccountSLLink;
    using GLAccountAuditEntry = Finnova.Models.Domain.Entities.GLAccountAuditEntry;
            {
    namespace Finnova.Service.GLAccount.Commands.CreateGLAccount;
                GLCode = new { old = (string?)null, @new = entity.GLCode },
                GLName = new { old = (string?)null, @new = entity.GLName },
                AccountType = new { old = (string?)null, @new = entity.AccountType.ToString() },
                PostingGroup = new { old = (string?)null, @new = entity.PostingGroup },
        private readonly IGLAccountAuditRepository _auditRepository;
                Currency = new { old = (string?)null, @new = entity.Currency },
                Status = new { old = (string?)null, @new = entity.Status.ToString() },
                AllowManualJournal = new { old = (bool?)null, @new = entity.AllowManualJournal },
            IGLAccountAuditRepository auditRepository)
                AllowSystemJournal = new { old = (bool?)null, @new = entity.AllowSystemJournal },
                EffectiveStartDate = new { old = (DateTime?)null, @new = entity.EffectiveStartDate },
                EffectiveEndDate = new { old = (DateTime?)null, @new = entity.EffectiveEndDate },
                LinkedSLAccounts = new { old = (object?)null, @new = entity.LinkedSLAccounts.Select(l => l.SLAccountCode).ToList() },
            };
            var audit = new GLAccountAuditEntry
        public async Task<GLAccountResponse> Handle(CreateGLAccountCommand command, CancellationToken cancellationToken)
            {
            var r = command.Request;
                GLAccountId = entity.Id,

            return entity.ToResponse();
            // R2.6 — case-insensitive, trimmed uniqueness. Rejecting here writes no record and
            // no audit entry (R9.4).
            if (await _repository.ExistsByGLCodeAsync(code, null, cancellationToken))
                throw new GLAccountDuplicateCodeException();
        }
            var entity = new GLAccountEntity
    }
    ```
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 1.13, 2.2, 2.3, 2.4, 2.5, 2.6, 5.1, 5.3, 5.4, 9.1, 9.4_
                GLName = r.GLName,
                AccountType = r.AccountType,
                PostingGroup = r.PostingGroup,
                Currency = string.IsNullOrWhiteSpace(r.Currency) ? "INR" : r.Currency!.Trim(), // R1.6
                Status = r.Status ?? GLAccountStatus.Active,                                     // R1.2
                AllowManualJournal = r.AllowManualJournal ?? true,                              // R1.4
                AllowSystemJournal = r.AllowSystemJournal ?? true,                              // R1.5
                CompanyCode = command.CompanyCode,                                              // R1.7
                UserBranch = command.UserBranch,                                                // R1.7
                EffectiveStartDate = r.EffectiveStartDate,
                EffectiveEndDate = r.EffectiveEndDate,
                LinkedSLAccounts = (r.LinkedSLAccounts ?? new List<string>())                   // R1.8
                    .Select(sl => new GLAccountSLLink { SLAccountCode = sl })
                    .ToList(),


            await _repository.AddAsync(entity, cancellationToken);   // R1.1 persisted, R1.13 queryable
            // R9.1 — exactly one Create audit entry (resulting field values, old = null).
            var changes = new





  - [ ] 7.8 Implement UpdateGLAccountCommand, validator, and handler

    **CREATE `Finnova.Service/GLAccount/Commands/UpdateGLAccount/UpdateGLAccountCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;




                Action = GLAccountAuditAction.Create,
                ChangesJson = JsonSerializer.Serialize(changes),
                Summary = $"Created GL account {entity.GLCode} ({entity.GLName}).",
                ChangedBy = command.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            };
            await _auditRepository.AddAsync(audit, cancellationToken);

            {





  - [ ]* 7.2 Write property test for create round trip
    - **Property 1: Create round trip**
    - **Validates: Requirements 1.1, 1.13**
        }
  - [ ]* 7.3 Write property test for create defaults
    - **Property 2: Create applies defaults**
    - **Validates: Requirements 1.2, 1.4, 1.5, 1.6, 1.8**
    }
  - [ ]* 7.4 Write property test for GL Code uniqueness on create
    - **Property 3: GL Code uniqueness on create**
    - **Validates: Requirements 2.1, 2.6**
    ```
  - [ ]* 7.5 Write property test for GL Code format accept/reject
    - **Property 4: GL Code format accept/reject**
    - **Validates: Requirements 2.3, 2.4**

  - [ ]* 7.6 Write property test for required-field validation
    - **Property 5: Required-field validation rejects with no change**
    - **Validates: Requirements 1.9, 2.2, 4.2, 4.5**
    ```csharp
  - [ ]* 7.7 Write property test for audit entry on create
    - **Property 17: Audit entry written on create**
    - **Validates: Requirements 9.1**
    - All property-test code bodies are provided together in task 9.3.
    using MediatR;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    namespace Finnova.Service.GLAccount.Commands.UpdateGLAccount;

    /// <summary>Update the non-financial attributes of a GL account (R4). GLCode/AccountType in
    /// the request are used only for the immutability equality check (R3).</summary>
    public record UpdateGLAccountCommand(
        Guid Id,
        UpdateGLAccountRequest Request,
        string ActingAdmin) : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GLAccount/Commands/UpdateGLAccount/UpdateGLAccountCommandValidator.cs`**
    ```csharp
    using FluentValidation;
    {
    namespace Finnova.Service.GLAccount.Commands.UpdateGLAccount;
        private readonly IGLAccountRepository _repository;
    public class UpdateGLAccountCommandValidator : AbstractValidator<UpdateGLAccountCommand>

        public UpdateGLAccountCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
        {
            // R4.2/R4.3 — Name required, max 100.
            RuleFor(x => x.Request.GLName)
                .NotEmpty().WithMessage("GL Name is required.")
                .MaximumLength(100).WithMessage("GL Name must not exceed 100 characters.");
            _repository = repository;
            // R4.5 — Posting Group required, max 50.
            RuleFor(x => x.Request.PostingGroup)
                .NotEmpty().WithMessage("Posting Group is required.")
                .MaximumLength(50).WithMessage("Posting Group must not exceed 50 characters.");
        }
            // R4.4 — Status must be a defined enum.
            RuleFor(x => x.Request.Status)
                .IsInEnum().WithMessage("Status is invalid.");

            // R5.1 — Effective Start Date required; R5.3/R5.4 — end null or end >= start.
            RuleFor(x => x.Request.EffectiveStartDate)
                .NotEmpty().WithMessage("Effective Start Date is required.");
            RuleFor(x => x.Request)
                .Must(r => r.EffectiveEndDate == null || r.EffectiveEndDate.Value.Date >= r.EffectiveStartDate.Date)
                .WithMessage("Effective End Date must be on or after the Effective Start Date.");
        {
            // R4.6 — at most 100 linked SL accounts.
            RuleFor(x => x.Request.LinkedSLAccounts)
                .Must(l => l == null || l.Count <= 100)
                .WithMessage("Linked SL Accounts must not exceed 100 entries.");







    {
        }


            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            {
                GLAccountId = entity.Id,
    **CREATE `Finnova.Service/GLAccount/Commands/UpdateGLAccount/UpdateGLAccountCommandHandler.cs`**

    using System.Text.Json;
            return entity.ToResponse();
    using Finnova.Models.Contracts.GLAccounts;
        }
    }
    ```
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 5.1, 5.3, 5.4, 9.2, 9.4_

    using GLAccountSLLink = Finnova.Models.Domain.Entities.GLAccountSLLink;
    using GLAccountAuditEntry = Finnova.Models.Domain.Entities.GLAccountAuditEntry;

    namespace Finnova.Service.GLAccount.Commands.UpdateGLAccount;
  - [ ]* 7.10 Write property test for update round trip
    public class UpdateGLAccountCommandHandler : IRequestHandler<UpdateGLAccountCommand, GLAccountResponse>
    - **Property 7: Update round trip**
    - **Validates: Requirements 4.1**
        private readonly IGLAccountAuditRepository _auditRepository;

        public UpdateGLAccountCommandHandler(
            IGLAccountRepository repository,
            IGLAccountAuditRepository auditRepository)
  - [ ]* 7.11 Write property test for identical no-op update
    - **Property 8: Identical update is a no-op with no audit**
            _auditRepository = auditRepository;
    - **Validates: Requirements 4.8, 9.4**
        public async Task<GLAccountResponse> Handle(UpdateGLAccountCommand command, CancellationToken cancellationToken)

            var r = command.Request;
  - [ ]* 7.12 Write property test for effective date range validity
            var entity = await _repository.GetWithLinksAsync(command.Id, cancellationToken)
                ?? throw new GLAccountNotFoundException(command.Id);   // R4.7
    - **Property 9: Effective date range validity**
            // R3.1/R3.5 — immutability of GL Code (blank/omitted = unchanged; else trim + CI compare).
            if (!string.IsNullOrWhiteSpace(r.GLCode) &&
                !string.Equals(r.GLCode.Trim(), entity.GLCode, StringComparison.OrdinalIgnoreCase))
                throw new GLAccountImmutableFieldException("GL Code");
    - **Validates: Requirements 5.2, 5.3, 5.4**
            // R3.2/R3.4 — immutability of Account Type (omitted/equal = unchanged).
            if (r.AccountType.HasValue && r.AccountType.Value != entity.AccountType)
                throw new GLAccountImmutableFieldException("Account Type");

            var newLinks = (r.LinkedSLAccounts ?? new List<string>());
            var currentLinks = entity.LinkedSLAccounts.Select(l => l.SLAccountCode).ToList();
  - [ ]* 7.13 Write property test for audit entry on update
            // R4.8 — if every non-financial value equals current, this is a no-op (no audit).
            var unchanged =
                entity.GLName == r.GLName &&
                entity.Status == r.Status &&
                entity.PostingGroup == r.PostingGroup &&
                entity.AllowManualJournal == r.AllowManualJournal &&
                entity.AllowSystemJournal == r.AllowSystemJournal &&
                entity.EffectiveStartDate == r.EffectiveStartDate &&
                entity.EffectiveEndDate == r.EffectiveEndDate &&
                currentLinks.SequenceEqual(newLinks);
    - **Property 18: Audit entry written on update**
            if (unchanged)
                return entity.ToResponse();
    - **Validates: Requirements 9.2**
            // Capture before-values for only the changed fields (R9.2).
            var changed = new Dictionary<string, object?>();
            void Track(string field, object? oldVal, object? newVal)

                changed[field] = new { old = oldVal, @new = newVal };
  - [ ]* 7.14 Write property test for no audit on rejection
    - **Property 19: No audit entry on rejection**
            if (entity.GLName != r.GLName) Track("GLName", entity.GLName, r.GLName);
            if (entity.Status != r.Status) Track("Status", entity.Status.ToString(), r.Status.ToString());
            if (entity.PostingGroup != r.PostingGroup) Track("PostingGroup", entity.PostingGroup, r.PostingGroup);
            if (entity.AllowManualJournal != r.AllowManualJournal) Track("AllowManualJournal", entity.AllowManualJournal, r.AllowManualJournal);
            if (entity.AllowSystemJournal != r.AllowSystemJournal) Track("AllowSystemJournal", entity.AllowSystemJournal, r.AllowSystemJournal);
            if (entity.EffectiveStartDate != r.EffectiveStartDate) Track("EffectiveStartDate", entity.EffectiveStartDate, r.EffectiveStartDate);
            if (entity.EffectiveEndDate != r.EffectiveEndDate) Track("EffectiveEndDate", entity.EffectiveEndDate, r.EffectiveEndDate);
            if (!currentLinks.SequenceEqual(newLinks)) Track("LinkedSLAccounts", currentLinks, newLinks);
    - **Validates: Requirements 9.4**
            // Apply non-financial changes (R4.1).
            entity.GLName = r.GLName;
            entity.Status = r.Status;
            entity.PostingGroup = r.PostingGroup;
            entity.AllowManualJournal = r.AllowManualJournal;
            entity.AllowSystemJournal = r.AllowSystemJournal;
            entity.EffectiveStartDate = r.EffectiveStartDate;
            entity.EffectiveEndDate = r.EffectiveEndDate;
            entity.LinkedSLAccounts = newLinks.Select(sl => new GLAccountSLLink
            {
                GLAccountId = entity.Id,
                SLAccountCode = sl
            }).ToList();
    - Property-test code bodies are provided together in task 9.3.

  - [ ] 7.15 Implement SetPostingGroupMappingCommand, validator, and handler
            await _repository.UpdateAsync(entity, cancellationToken);

            // R9.2 — exactly one Update audit entry capturing {field:{old,new}} for changed fields.
            var audit = new GLAccountAuditEntry

    **CREATE `Finnova.Service/GLAccount/Commands/SetPostingGroupMapping/SetPostingGroupMappingCommand.cs`**
                Action = GLAccountAuditAction.Update,
                ChangesJson = JsonSerializer.Serialize(changed),
                Summary = $"Updated GL account {entity.GLCode}: {string.Join(", ", changed.Keys)}.",
                ChangedBy = command.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            };
            await _auditRepository.AddAsync(audit, cancellationToken);
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;


    ```

  - [ ]* 7.9 Write property test for immutability of GL Code and Account Type
    - **Property 6: Immutability of GL Code and Account Type**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    ```csharp
    using FluentValidation;


    {
        {
        }
    }
    ```

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;


    {

        {
        }

        {




            {

        }
    namespace Finnova.Service.GLAccount.Commands.SetPostingGroupMapping;
    }
    /// <summary>Set/overwrite the GL account mapped to a required posting group (R6.2).</summary>
    public record SetPostingGroupMappingCommand(
        string PostingGroup,
        Guid GLAccountId,
        string ActingAdmin) : IRequest<PostingGroupMappingResponse>;
    ```
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
    **CREATE `Finnova.Service/GLAccount/Commands/SetPostingGroupMapping/SetPostingGroupMappingCommandValidator.cs`**



    namespace Finnova.Service.GLAccount.Commands.SetPostingGroupMapping;
    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountsPaged/GetGLAccountsPagedQuery.cs`**
    public class SetPostingGroupMappingCommandValidator : AbstractValidator<SetPostingGroupMappingCommand>
    ```csharp
        public SetPostingGroupMappingCommandValidator()
            // R6.3 — posting group must be one of the required groups.
            RuleFor(x => x.PostingGroup)
                .NotEmpty().WithMessage("Posting Group is required.")
                .Must(GLPostingGroups.IsRequired)
                .WithMessage("Posting Group is not a required posting group.");

            RuleFor(x => x.GLAccountId)
                .NotEmpty().WithMessage("GL Account Id is required.");
        }
    using MediatR;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GLAccounts;
    **CREATE `Finnova.Service/GLAccount/Commands/SetPostingGroupMapping/SetPostingGroupMappingCommandHandler.cs`**
    using Finnova.Models.Domain.Enums;
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;


    ```
    using PostingGroupMappingEntity = Finnova.Models.Domain.Entities.PostingGroupMapping;

    namespace Finnova.Service.GLAccount.Commands.SetPostingGroupMapping;
    ```csharp
    public class SetPostingGroupMappingCommandHandler
        : IRequestHandler<SetPostingGroupMappingCommand, PostingGroupMappingResponse>
    using FluentValidation;
        private readonly IPostingGroupMappingRepository _mappingRepository;
        private readonly IGLAccountRepository _glRepository;

        public SetPostingGroupMappingCommandHandler(
            IPostingGroupMappingRepository mappingRepository,
            IGLAccountRepository glRepository)

            _mappingRepository = mappingRepository;
            _glRepository = glRepository;
    {
        {
        public async Task<PostingGroupMappingResponse> Handle(
            SetPostingGroupMappingCommand command, CancellationToken cancellationToken)
        }
            var group = command.PostingGroup.Trim();
    }
            // R6.3 — required-group guard (defense in depth beyond the validator).
            if (!GLPostingGroups.IsRequired(group))
                throw new PostingGroupMappingValidationException(
                    $"Posting Group '{group}' is not a required posting group.");
    ```
            // R6.4 — mapped GL account must exist.
            var gl = await _glRepository.GetByIdAsync(command.GLAccountId, cancellationToken)
                ?? throw new PostingGroupMappingValidationException("The mapped GL account was not found.");

            // R6.5 — inactive GL account cannot be mapped.
            if (gl.Status == GLAccountStatus.Inactive)
                throw new PostingGroupMappingValidationException("An inactive GL account cannot be mapped.");
    ```csharp
            // R6.1/R6.2 — upsert: overwrite any prior mapping for this group.
            var existing = await _mappingRepository.GetByPostingGroupAsync(group, cancellationToken);
            if (existing is null)
    using Finnova.Repository.Interfaces;
                var mapping = new PostingGroupMappingEntity
                {
                    PostingGroup = group,
                    GLAccountId = gl.Id,
                };
                await _mappingRepository.AddAsync(mapping, cancellationToken);
            }
            else
            {
                existing.GLAccountId = gl.Id;
                existing.UpdatedAt = DateTime.UtcNow;
                await _mappingRepository.UpdateAsync(existing, cancellationToken);
            }
    using Finnova.Service.Mappers;
            return new PostingGroupMappingResponse(group, gl.Id, gl.GLCode, gl.GLName, true);


    {
        private readonly IGLAccountRepository _repository;
  - [ ]* 7.16 Write property tests for posting-group mapping
    - **Property 11: Posting-group mapping overwrite and uniqueness**
    - **Property 12: Invalid posting-group mapping is rejected with no change**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

  - [ ] 7.17 Implement GetGLAccountsPagedQuery, validator, and handler


        {


        }
    }
    ```
    > Confirm the `PaginatedResponse<T>` constructor order against the existing record in
    namespace Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;
    > `Finnova.Models/Contracts/Common/PaginatedResponse.cs` and adjust the argument order if the
    public record GetGLAccountsPagedQuery(
        string? SearchTerm,
        GLAccountType? AccountType,
        GLAccountStatus? Status,
        int Page = 1,
        int PageSize = 20) : IRequest<PaginatedResponse<GLAccountResponse>>;
    > repo differs. (Design states `(Data, Total, Page, PageSize, TotalPages)`.)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 8.12, 8.13, 8.14, 8.15_
    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountsPaged/GetGLAccountsPagedQueryValidator.cs`**

  - [ ]* 7.18 Write property tests for search, pagination, and ordering
    namespace Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;
    - **Property 14: Search filter conjunction**
    public class GetGLAccountsPagedQueryValidator : AbstractValidator<GetGLAccountsPagedQuery>
    - **Property 15: Pagination consistency**
        public GetGLAccountsPagedQueryValidator()
    - **Property 16: List ordering is deterministic**
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
                .WithMessage("Page number must be 1 or greater.");                       // R8.9
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
                .WithMessage("Page size must be between 1 and 100.");                     // R8.10
            RuleFor(x => x.SearchTerm!)
                .Must(s => s.Trim().Length <= 100)
                .When(x => x.SearchTerm != null)
                .WithMessage("Search term must not exceed 100 characters.");             // R8.11
            RuleFor(x => x.AccountType!.Value).IsInEnum()
                .When(x => x.AccountType.HasValue)
                .WithMessage("Account Type filter is invalid.");                         // R8.15
            RuleFor(x => x.Status!.Value).IsInEnum()
                .When(x => x.Status.HasValue)
                .WithMessage("Status filter is invalid.");                               // R8.15
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.12, 8.13, 8.14**


    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountsPaged/GetGLAccountsPagedQueryHandler.cs`**
    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountPostability/GetGLAccountPostabilityQuery.cs`**
    using MediatR;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GLAccounts;
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;

    namespace Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;

    public class GetGLAccountsPagedQueryHandler
        : IRequestHandler<GetGLAccountsPagedQuery, PaginatedResponse<GLAccountResponse>>
    ```

    ```csharp

        public GetGLAccountsPagedQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<PaginatedResponse<GLAccountResponse>> Handle(
            GetGLAccountsPagedQuery query, CancellationToken cancellationToken)
        {
            var (items, total) = await _repository.GetPagedAsync(
                query.SearchTerm, query.AccountType, query.Status, query.Page, query.PageSize, cancellationToken);
            {
            var totalPages = query.PageSize > 0
                ? (int)Math.Ceiling(total / (double)query.PageSize)
                : 0;
        }
            return new PaginatedResponse<GLAccountResponse>(
                items.ToResponseList(), total, query.Page, query.PageSize, totalPages);
    }
    ```

    ```csharp
    using MediatR;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;


    {
        private readonly IGLAccountRepository _repository;


  - [ ] 7.19 Implement GetGLAccountPostabilityQuery, validator, and handler
        {


        }
    }
    ```
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_
    namespace Finnova.Service.GLAccount.Queries.GetGLAccountPostability;

    public record GetGLAccountPostabilityQuery(
        Guid Id,
        DateTime Date) : IRequest<GLAccountPostabilityResponse>;


    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountPostability/GetGLAccountPostabilityQueryValidator.cs`**
    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountAuditTrail/GetGLAccountAuditTrailQuery.cs`**
    using FluentValidation;
    ```csharp
    namespace Finnova.Service.GLAccount.Queries.GetGLAccountPostability;
    using MediatR;
    public class GetGLAccountPostabilityQueryValidator : AbstractValidator<GetGLAccountPostabilityQuery>
        public GetGLAccountPostabilityQueryValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
            // R7.5 — date required and a valid calendar date. A bound DateTime cannot be an
            // invalid calendar date; a missing/empty query value fails model binding or default(DateTime).
            RuleFor(x => x.Date).NotEmpty().WithMessage("A valid date is required.");
        }
    }
    ```

    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountPostability/GetGLAccountPostabilityQueryHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Contracts.GLAccounts;


    ```

    namespace Finnova.Service.GLAccount.Queries.GetGLAccountPostability;
    ```csharp
    public class GetGLAccountPostabilityQueryHandler
        : IRequestHandler<GetGLAccountPostabilityQuery, GLAccountPostabilityResponse>
    using Finnova.Repository.Interfaces;


        {
        public GetGLAccountPostabilityQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<GLAccountPostabilityResponse> Handle(
            GetGLAccountPostabilityQuery query, CancellationToken cancellationToken)

            var gl = await _repository.GetByIdAsync(query.Id, cancellationToken)
                ?? throw new GLAccountNotFoundException(query.Id);   // R7.4
            {
            // R7.1/R7.2/R7.3 — postable iff Active and date within [start, end].
            var postable =
                gl.Status == GLAccountStatus.Active &&
                query.Date.Date >= gl.EffectiveStartDate.Date &&
                (gl.EffectiveEndDate == null || query.Date.Date <= gl.EffectiveEndDate.Value.Date);
        }
            return new GLAccountPostabilityResponse(
                gl.Id, gl.GLCode, postable, query.Date, gl.Status, gl.EffectiveStartDate, gl.EffectiveEndDate);
    }
    ```



  - [ ]* 7.20 Write property test for postability truth table
    - **Property 10: Postability truth table**
    - **Validates: Requirements 7.1, 7.2, 7.3**
    **CREATE `Finnova.Service/GLAccount/Queries/GetPostingGroupMappings/GetPostingGroupMappingsQuery.cs`**
  - [ ] 7.21 Implement GetGLAccountAuditTrailQuery and handler
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;
    namespace Finnova.Service.GLAccount.Queries.GetGLAccountAuditTrail;

    public record GetGLAccountAuditTrailQuery(Guid GLAccountId)
        : IRequest<List<GLAccountAuditEntryResponse>>;
    ```

    **CREATE `Finnova.Service/GLAccount/Queries/GetGLAccountAuditTrail/GetGLAccountAuditTrailQueryHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.GLAccount.Queries.GetGLAccountAuditTrail;

    public class GetGLAccountAuditTrailQueryHandler
        : IRequestHandler<GetGLAccountAuditTrailQuery, List<GLAccountAuditEntryResponse>>
    {
        private readonly IGLAccountAuditRepository _auditRepository;

        public GetGLAccountAuditTrailQueryHandler(IGLAccountAuditRepository auditRepository)
            => _auditRepository = auditRepository;

        public async Task<List<GLAccountAuditEntryResponse>> Handle(
            GetGLAccountAuditTrailQuery query, CancellationToken cancellationToken)

            var entries = await _auditRepository.GetByGLAccountIdAsync(query.GLAccountId, cancellationToken);
            return entries.Select(e => e.ToResponse()).ToList();   // missing id -> [] (R9.6)
    ```

    ```csharp
    - _Requirements: 9.5, 9.6_
    using Finnova.Repository.Interfaces;
  - [ ]* 7.22 Write property test for audit trail ordering
    - **Property 20: Audit trail ordering is deterministic**
    - **Validates: Requirements 9.5, 9.6**

  - [ ] 7.23 Implement GetPostingGroupMappingsQuery and handler

    {

        {
        }

    namespace Finnova.Service.GLAccount.Queries.GetPostingGroupMappings;
        {
    public record GetPostingGroupMappingsQuery() : IRequest<List<PostingGroupMappingResponse>>;

            {
    **CREATE `Finnova.Service/GLAccount/Queries/GetPostingGroupMappings/GetPostingGroupMappingsQueryHandler.cs`**
                {
    using MediatR;
    using Finnova.Models.Contracts.GLAccounts;
            }
            else
            {
            }
        }
    namespace Finnova.Service.GLAccount.Queries.GetPostingGroupMappings;
    public class GetPostingGroupMappingsQueryHandler
        : IRequestHandler<GetPostingGroupMappingsQuery, List<PostingGroupMappingResponse>>
    }
        private readonly IPostingGroupMappingRepository _mappingRepository;
        private readonly IGLAccountRepository _glRepository;
    ```
        public GetPostingGroupMappingsQueryHandler(
            IPostingGroupMappingRepository mappingRepository,
            IGLAccountRepository glRepository)

            _mappingRepository = mappingRepository;
            _glRepository = glRepository;


        public async Task<List<PostingGroupMappingResponse>> Handle(
            GetPostingGroupMappingsQuery query, CancellationToken cancellationToken)
    **CREATE `Finnova.SystemAdminService/Controllers/GLAccountController.cs`**
            var stored = await _mappingRepository.GetAllMappingsAsync(cancellationToken);
            var byGroup = stored.ToDictionary(m => m.PostingGroup, StringComparer.Ordinal);
    ```csharp
            var result = new List<PostingGroupMappingResponse>();
            foreach (var group in GLPostingGroups.Required)   // every required group exactly once (R6.6)
    using MediatR;
                if (byGroup.TryGetValue(group, out var mapping))
    using Microsoft.AspNetCore.Authorization;
                    var gl = await _glRepository.GetByIdAsync(mapping.GLAccountId, cancellationToken);
                    result.Add(new PostingGroupMappingResponse(
                        group, gl?.Id, gl?.GLCode, gl?.GLName, gl != null));
    using Microsoft.AspNetCore.Mvc;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GLAccounts;
                    result.Add(new PostingGroupMappingResponse(group, null, null, null, false));
                }
    using Finnova.Models.Domain.Enums;
            return result;
    using Finnova.Service.GLAccount.Commands.CreateGLAccount;
    using Finnova.Service.GLAccount.Commands.SetPostingGroupMapping;
    using Finnova.Service.GLAccount.Commands.UpdateGLAccount;
    - _Requirements: 6.6_
    using Finnova.Service.GLAccount.Queries.GetGLAccountAuditTrail;
  - [ ]* 7.24 Write property test for mapping read completeness
    - **Property 13: Posting-group mapping read completeness**
    - **Validates: Requirements 6.6**
    - Property-test code bodies are provided together in task 9.3.
    using Finnova.Service.GLAccount.Queries.GetGLAccountPostability;
- [ ] 8. Wire up the API, middleware, and gateway
  - [ ] 8.1 Implement the GLAccountController
    using Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;
    using Finnova.Service.GLAccount.Queries.GetPostingGroupMappings;

    using System.Security.Claims;

    {

        {
    }
    ```








    namespace Finnova.SystemAdminService.Controllers;
    }
    /// <summary>
    /// GL Master endpoints (FINNOVA GL Master). Every action is SystemAdmin-only
    /// (R10.1-R10.3, read included). All work flows through MediatR so the existing
    /// ValidationBehavior pipeline runs before each handler.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GLAccountController : ControllerBase
    ```
        private readonly IMediator _mediator;
        public GLAccountController(IMediator mediator) => _mediator = mediator;

        /// <summary>Paged, filtered admin list (R8). SystemAdmin only.</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(PaginatedResponse<GLAccountResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<GLAccountResponse>>> GetPaged(
            [FromQuery] string? search,
            [FromQuery] GLAccountType? accountType,
            [FromQuery] GLAccountStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetGLAccountsPagedQuery(search, accountType, status, page, pageSize)));
    > `Create` returns `201` via `CreatedAtAction(nameof(GetPaged), new { search = result.GLCode }, result)`.
        /// <summary>Create a GL account (R1, R2, R5). SystemAdmin only.</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Create([FromBody] CreateGLAccountRequest r)
    > `GetAuditTrail` always returns `200` with a possibly-empty list. Confirm the exact claim
            var result = await _mediator.Send(new CreateGLAccountCommand(
                r, GetCompanyCode(), GetUserBranch(), GetActingAdmin()));
            return CreatedAtAction(nameof(GetPaged), new { search = result.GLCode }, result);
    > names for company/branch against the host's JWT issuance; adjust `GetCompanyCode`/`GetUserBranch`
    > if the token uses different claim keys.
    - _Requirements: 1.1, 1.7, 3.1, 4.1, 6.2, 6.6, 7.1, 8.1, 9.5, 9.6, 10.1, 10.2, 10.3_
        /// <summary>Update non-financial attributes (R3, R4, R5). SystemAdmin only. 404 when missing.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Update(Guid id, [FromBody] UpdateGLAccountRequest r)
            => Ok(await _mediator.Send(new UpdateGLAccountCommand(id, r, GetActingAdmin())));

        /// <summary>Postability of a GL account at a date (R7). SystemAdmin only. 404 when missing.</summary>
        [HttpGet("{id:guid}/postability")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(GLAccountPostabilityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GLAccountPostabilityResponse>> GetPostability(
            Guid id, [FromQuery] DateTime date)
            => Ok(await _mediator.Send(new GetGLAccountPostabilityQuery(id, date)));
  - [ ] 8.2 Extend ExceptionHandlingMiddleware with the GL branch
        /// <summary>Audit trail, newest first (R9.5). Missing id yields [] (R9.6). SystemAdmin only.</summary>
        [HttpGet("{id:guid}/audit")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(List<GLAccountAuditEntryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<GLAccountAuditEntryResponse>>> GetAuditTrail(Guid id)
            => Ok(await _mediator.Send(new GetGLAccountAuditTrailQuery(id)));

        /// <summary>All required posting-group-to-GL mappings (R6.6). SystemAdmin only.</summary>
        [HttpGet("posting-group-mappings")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(List<PostingGroupMappingResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<PostingGroupMappingResponse>>> GetPostingGroupMappings()
            => Ok(await _mediator.Send(new GetPostingGroupMappingsQuery()));

        /// <summary>Set/overwrite the mapping for one required posting group (R6.2-R6.5). SystemAdmin only.</summary>
        [HttpPut("posting-group-mappings/{postingGroup}")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(PostingGroupMappingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PostingGroupMappingResponse>> SetPostingGroupMapping(
            string postingGroup, [FromBody] SetPostingGroupMappingRequest r)
            => Ok(await _mediator.Send(new SetPostingGroupMappingCommand(postingGroup, r.GLAccountId, GetActingAdmin())));
    Edit (a) — path-scoped code chain. Immediately **after** the existing two lines:
        /// <summary>
        /// Resolves the acting administrator id from the validated JWT principal so the audit
        /// actor cannot be spoofed by the request body (R1.7, R9.1). Prefers NameIdentifier (sub),
        /// falls back to Name.
        /// </summary>
        private string GetActingAdmin()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? User.Identity?.Name
               ?? string.Empty;
    ```csharp
        /// <summary>Company context from claims, not request body (R1.7).</summary>
        private string GetCompanyCode()
            => User.FindFirstValue("company_code")
               ?? User.FindFirstValue("companyCode")
               ?? string.Empty;
    var isAsset = ctx.Request.Path.StartsWithSegments("/api/asset", StringComparison.OrdinalIgnoreCase);
        /// <summary>Branch context from claims, not request body (R1.7).</summary>
        private string GetUserBranch()
            => User.FindFirstValue("user_branch")
               ?? User.FindFirstValue("branch")
               ?? string.Empty;
    var validationAssetCode = isAsset ? "ERR-AST-400" : validationUserCode;
    var fallbackAssetCode = isAsset ? "ERR-AST-500" : fallbackUserCode;
    ```
    add these three lines:
    ```csharp
    ```

    ```csharp
    ```
    ```csharp
    **MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`**

    ```


    ```
    > Add a trailing comma after the preceding route object as needed to keep the JSON valid. Host
    > separation is preserved — GL stays on `systemadmin-cluster`.
    - _Requirements: 10.3_
    var isGl = ctx.Request.Path.StartsWithSegments("/api/glaccount", StringComparison.OrdinalIgnoreCase);
    var validationGlCode = isGl ? "ERR-GL-400" : validationAssetCode;
    var fallbackGlCode = isGl ? "ERR-GL-500" : fallbackAssetCode;


    Edit (b) — typed switch arms + shared tail. **Replace** the existing shared-tail two lines:
    **CREATE `Finnova.Tests/Infrastructure/InMemoryGLAccountRepository.cs`**
    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationAssetCode, v.Message),
    _ => (StatusCodes.Status500InternalServerError, fallbackAssetCode, "Unexpected error.")
    ```csharp
    with the GL typed arms **followed by** the shared tail now resolving via `validationGlCode`/`fallbackGlCode`:
    using Finnova.Models.Domain.Enums;
    // ---- new GL Master branch (typed, no message sniffing) ----
    GLAccountNotFoundException => (404, "ERR-GL-404", ex.Message),
    GLAccountDuplicateCodeException => (409, GLAccountDuplicateCodeException.ErrorCode, ex.Message),
    GLAccountImmutableFieldException => (409, GLAccountImmutableFieldException.ErrorCode, ex.Message),
    PostingGroupMappingValidationException => (400, "ERR-GL-400", ex.Message),
    GLValidationException => (400, "ERR-GL-400", ex.Message),
    using Finnova.Repository.Interfaces;
    // ---- shared: validation failures and unhandled fallback (path-scoped code, incl. GL) ----
    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationGlCode, v.Message),
    _ => (StatusCodes.Status500InternalServerError, fallbackGlCode, "Unexpected error.")

    > `using Finnova.Models.Domain.Exceptions;` is already present at the top of the file, so the
    > new exception types resolve without a further using. Do not remove any existing switch arms.
    - _Requirements: 2.6, 3.1, 3.2, 4.7, 6.3, 6.4, 6.5, 7.4_

  - [ ] 8.3 Add the GL gateway UI-alias routes
    /// <summary>
    **MODIFY `Finnova.ApiGateway/appsettings.json`** — inside `ReverseProxy.Routes`, add these two
    objects alongside the other `ua-*-alias` routes (e.g. right after `ua-asset-alias-root`):
    ```jsonc
    "ua-glaccount-alias-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/glaccount/{**catch-all}"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    "ua-glaccount-alias-root": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/glaccount"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    /// </summary>
    {

        {

- [ ] 9. Backend tests (property, edge, integration) in `Finnova.Tests`
  - [ ] 9.1 Add in-memory repositories for GL


    }
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAuditTrail/GetGLAuditTrailQuery.cs`**

    namespace Finnova.Tests.Infrastructure;
    ```csharp
    using Finnova.Models.Contracts.GL;
    /// Hand-written in-memory <see cref="IGLAccountRepository"/> mirroring the EF-backed repo:
    /// trimmed, case-insensitive GLCode uniqueness; substring (CI) search on GLCode/GLName;
    /// AccountType/Status equality filters; GLCode-asc ordering; total before paging.
    /// Read paths clone so callers cannot mutate stored state.
    using MediatR;
    public sealed class InMemoryGLAccountRepository : IGLAccountRepository

        private readonly List<GLAccount> _store = new();
    namespace Finnova.Service.GL.Queries.GetGLAuditTrail;
        public InMemoryGLAccountRepository() { }
        public InMemoryGLAccountRepository(IEnumerable<GLAccount> seed)

            foreach (var g in seed) _store.Add(Clone(g));
        }
    public record GetGLAuditTrailQuery(Guid GLAccountId) : IRequest<List<GLAuditEntryResponse>>;
        public IReadOnlyList<GLAccount> Snapshot() => _store.Select(Clone).ToList();
        public int Count => _store.Count;
    ```
        private static GLAccount Clone(GLAccount x) => new()
        {
            Id = x.Id,
            GLCode = x.GLCode,
            GLName = x.GLName,
            AccountType = x.AccountType,
            PostingGroup = x.PostingGroup,
            Currency = x.Currency,
            Status = x.Status,
            AllowManualJournal = x.AllowManualJournal,
            AllowSystemJournal = x.AllowSystemJournal,
            CompanyCode = x.CompanyCode,
            UserBranch = x.UserBranch,
            EffectiveStartDate = x.EffectiveStartDate,
            EffectiveEndDate = x.EffectiveEndDate,
            LinkedSLAccounts = x.LinkedSLAccounts
                .Select(l => new GLAccountSLLink { Id = l.Id, GLAccountId = l.GLAccountId, SLAccountCode = l.SLAccountCode })
                .ToList(),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
        };

        public Task<GLAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var f = _store.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(f is null ? null : Clone(f));
    **CREATE `Finnova.Service/GL/Queries/GetGLAuditTrail/GetGLAuditTrailQueryHandler.cs`**



    {

        {
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetSystemGLCompleteness/GetSystemGLCompletenessQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;
        public Task<List<GLAccount>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());

        public Task<List<GLAccount>> FindAsync(Expression<Func<GLAccount, bool>> predicate, CancellationToken ct = default)
            => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());
    namespace Finnova.Service.GL.Queries.GetSystemGLCompleteness;
        public Task AddAsync(GLAccount entity, CancellationToken ct = default)

            _store.Add(Clone(entity));
            return Task.CompletedTask;
        }
    public record GetSystemGLCompletenessQuery(Guid CompanyId) : IRequest<SystemGLCompletenessResponse>;
        public Task UpdateAsync(GLAccount entity, CancellationToken ct = default)
    ```
            var idx = _store.FindIndex(x => x.Id == entity.Id);
            if (idx >= 0) _store[idx] = Clone(entity);
            return Task.CompletedTask;

    **CREATE `Finnova.Service/GL/Queries/GetSystemGLCompleteness/GetSystemGLCompletenessQueryHandler.cs`**



    {
        {
        }
            if (accountType.HasValue) q = q.Where(x => x.AccountType == accountType.Value);
            if (status.HasValue) q = q.Where(x => x.Status == status.Value);

    }
    ```

        public Task DeleteAsync(GLAccount entity, CancellationToken ct = default)
        {
            _store.RemoveAll(x => x.Id == entity.Id);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(Expression<Func<GLAccount, bool>>? predicate = null, CancellationToken ct = default)
            => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));
    {
        public Task<(List<GLAccount> Items, int Total)> GetPagedAsync(
            string? searchTerm, GLAccountType? accountType, GLAccountStatus? status,
            int page, int pageSize, CancellationToken ct = default)
            IEnumerable<GLAccount> q = _store;
            if (!string.IsNullOrWhiteSpace(searchTerm))
        }
                var term = searchTerm.Trim();
                q = q.Where(x =>
                    x.GLCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.GLName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
    ```

    ```csharp
            var all = q.ToList();
            var total = all.Count;
            var items = all
                .OrderBy(x => x.GLCode, StringComparer.OrdinalIgnoreCase)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Clone)
                .ToList();
            return Task.FromResult((items, total));
    using Finnova.Repository.Interfaces;


        public Task<bool> ExistsByGLCodeAsync(string glCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = glCode.Trim();
            var exists = _store.Any(x =>
                string.Equals(x.GLCode, c, StringComparison.OrdinalIgnoreCase) &&
                (excludeId == null || x.Id != excludeId));
            return Task.FromResult(exists);
        }
    {
        public Task<GLAccount?> GetWithLinksAsync(Guid id, CancellationToken ct = default)

            var f = _store.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(f is null ? null : Clone(f));

            {


    **CREATE `Finnova.Tests/Infrastructure/InMemoryGLAccountAuditRepository.cs`**

    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;

    {


    namespace Finnova.Tests.Infrastructure;

    public sealed class InMemoryGLAccountAuditRepository : IGLAccountAuditRepository

        private readonly List<GLAccountAuditEntry> _store = new();
                {
        public IReadOnlyList<GLAccountAuditEntry> Snapshot() => _store.Select(Clone).ToList();
        public int Count => _store.Count;
                }
        private static GLAccountAuditEntry Clone(GLAccountAuditEntry x) => new()
            Id = x.Id,
            GLAccountId = x.GLAccountId,
            Action = x.Action,
            ChangesJson = x.ChangesJson,
            Summary = x.Summary,
            ChangedBy = x.ChangedBy,
            ChangedAtUtc = x.ChangedAtUtc,
        };

        public Task<GLAccountAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.FirstOrDefault(x => x.Id == id) is { } f ? Clone(f) : null);
                results.Add(new GLUploadRowResult(row.RowNumber, code, error is null, error));
        public Task<List<GLAccountAuditEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());
            }
        public Task<List<GLAccountAuditEntry>> FindAsync(Expression<Func<GLAccountAuditEntry, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());

        public Task AddAsync(GLAccountAuditEntry e, CancellationToken ct = default)
            var allOk = results.All(r => r.Success);
            _store.Add(Clone(e));
            return Task.CompletedTask;
        }
            return new GLUploadReportResponse(Guid.NewGuid(), allOk, results);
        public Task UpdateAsync(GLAccountAuditEntry e, CancellationToken ct = default)
            => throw new InvalidOperationException("Audit entries are immutable (R9.7).");
        }
        public Task DeleteAsync(GLAccountAuditEntry e, CancellationToken ct = default)
            => throw new InvalidOperationException("Audit entries are immutable (R9.8).");
    }
        public Task<int> CountAsync(Expression<Func<GLAccountAuditEntry, bool>>? p = null, CancellationToken ct = default)
            => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));
    ```
        public Task<List<GLAccountAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken ct = default)

            var items = _store
                .Where(x => x.GLAccountId == glAccountId)
                .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)
                .Select(Clone)
                .ToList();
            return Task.FromResult(items);
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;


    {


        {

            {
    **CREATE `Finnova.Tests/Infrastructure/InMemoryPostingGroupMappingRepository.cs`**
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
            }

            {
            }

    namespace Finnova.Tests.Infrastructure;
        }
    public sealed class InMemoryPostingGroupMappingRepository : IPostingGroupMappingRepository
        private readonly List<PostingGroupMapping> _store = new();

        public IReadOnlyList<PostingGroupMapping> Snapshot() => _store.Select(Clone).ToList();

        private static PostingGroupMapping Clone(PostingGroupMapping x) => new()

            Id = x.Id,
            PostingGroup = x.PostingGroup,
            GLAccountId = x.GLAccountId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
        };
    ```
        public Task<PostingGroupMapping?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.FirstOrDefault(x => x.Id == id) is { } f ? Clone(f) : null);

        public Task<List<PostingGroupMapping>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());

        public Task<List<PostingGroupMapping>> FindAsync(Expression<Func<PostingGroupMapping, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());

        public Task AddAsync(PostingGroupMapping e, CancellationToken ct = default)
    - _Requirements: 1.1, 6.1, 6.6, 8.1, 9.5, 9.6, 9.7, 9.8_
            _store.Add(Clone(e));
            return Task.CompletedTask;


        public Task UpdateAsync(PostingGroupMapping e, CancellationToken ct = default)
    **CREATE `Finnova.Tests/Infrastructure/GLAccountBuilder.cs`**
            var idx = _store.FindIndex(x => x.Id == e.Id);
            if (idx >= 0) _store[idx] = Clone(e);
            return Task.CompletedTask;
    ```csharp
    using Finnova.Models.Contracts.GLAccounts;
        public Task DeleteAsync(PostingGroupMapping e, CancellationToken ct = default)
        {
            _store.RemoveAll(x => x.Id == e.Id);
            return Task.CompletedTask;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;


        public Task<int> CountAsync(Expression<Func<PostingGroupMapping, bool>>? p = null, CancellationToken ct = default)
            => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));
    {
        public Task<List<PostingGroupMapping>> GetAllMappingsAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());
        public Task<PostingGroupMapping?> GetByPostingGroupAsync(string postingGroup, CancellationToken ct = default)
            => Task.FromResult(_store.FirstOrDefault(x => x.PostingGroup == postingGroup) is { } f ? Clone(f) : null);
    }
    ```


  - [ ] 9.2 Add builders and FsCheck generators
        {

            }
            return count;   // R9.6
        }
    }
    ```

    ```csharp
    namespace Finnova.Tests.Infrastructure;

    /// <summary>Fluent builders for GL entities and create/update requests in tests.</summary>
    public sealed class GLAccountBuilder

        private string _code = "GL1001";
        private string _name = "Cash in Hand";
        private GLAccountType _type = GLAccountType.Asset;
        private string _group = "Customer";
        private string _currency = "INR";
        private GLAccountStatus _status = GLAccountStatus.Active;
        private bool _manual = true, _system = true;
        private DateTime _start = new(2024, 1, 1);
        private DateTime? _end = null;
        private List<string> _links = new();

        public GLAccountBuilder WithCode(string c) { _code = c; return this; }
        public GLAccountBuilder WithName(string n) { _name = n; return this; }
        public GLAccountBuilder WithType(GLAccountType t) { _type = t; return this; }
        public GLAccountBuilder WithGroup(string g) { _group = g; return this; }
        public GLAccountBuilder WithStatus(GLAccountStatus s) { _status = s; return this; }
        public GLAccountBuilder WithDates(DateTime start, DateTime? end) { _start = start; _end = end; return this; }
        public GLAccountBuilder WithLinks(params string[] links) { _links = links.ToList(); return this; }

        public GLAccount BuildEntity() => new()

            GLCode = _code, GLName = _name, AccountType = _type, PostingGroup = _group,
            Currency = _currency, Status = _status, AllowManualJournal = _manual,
            AllowSystemJournal = _system, EffectiveStartDate = _start, EffectiveEndDate = _end,
            LinkedSLAccounts = _links.Select(l => new GLAccountSLLink { SLAccountCode = l }).ToList(),
        };

        public CreateGLAccountRequest BuildCreateRequest() => new(
            _code, _name, _type, _group, _currency, _status, _manual, _system, _start, _end, _links);

        public UpdateGLAccountRequest BuildUpdateRequest() => new(
            _code, _type, _name, _status, _group, _manual, _system, _start, _end, _links);
    {
        {
        }
    }
    ```
    - _Requirements: 1.1, 2.1, 2.3, 2.4, 4.1, 5.3, 8.1_
    **CREATE `Finnova.Tests/Infrastructure/GLAccountGenerators.cs`**

    using FsCheck;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Tests.Infrastructure;
    **CREATE `Finnova.Tests/GLAccount/GLAccountPropertyTests.cs`**
    /// <summary>FsCheck generators for GL inputs: valid alphanumeric codes, symbol-injected and
    /// reserved codes, in-bound names/groups, date pairs (equal/inverted), and casing/whitespace
    /// variants for uniqueness tests.</summary>
    public static class GLAccountGenerators
    {
        /// <summary>Valid alphanumeric GL code, 1..20 chars, never "000".</summary>
        public static Arbitrary<string> ValidGLCode() =>
            Gen.Choose(1, 20)
               .SelectMany(len => Gen.ArrayOf(len, AlphanumericChar()))
               .Select(cs => new string(cs))
               .Where(s => s.Trim().Length >= 1 && s.Trim() != "000")
               .ToArbitrary();
    ```csharp
        /// <summary>A code guaranteed to contain at least one non-alphanumeric character.</summary>
        public static Arbitrary<string> InvalidFormatGLCode() =>
            Gen.Elements("GL-1", "A/01", "GL 01", "GL@1", "A.B", "GL#", "(x)")
               .ToArbitrary();
    using System.Text.Json;
        /// <summary>Non-empty name 1..100 chars.</summary>
        public static Arbitrary<string> ValidName() =>
            Arb.Default.String().Filter(s => !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= 100);
    using FsCheck;
        private static Gen<char> AlphanumericChar() =>
            Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray());
    using FsCheck.Xunit;
        /// <summary>A casing/whitespace variant of a code (for uniqueness tests).</summary>
        public static string CasingWhitespaceVariant(string code, int seed)
    using Xunit;
            var trimmed = code.Trim();
            return (seed % 3) switch
    using Finnova.Models.Contracts.GLAccounts;
                0 => trimmed.ToUpperInvariant(),
                1 => trimmed.ToLowerInvariant(),
                _ => "  " + trimmed + "  ",
            };
    using Finnova.Models.Domain.Enums;

  - [ ]* 9.3 Write the property test suite (Properties 1–21)

    {

        {
            }
            return count;
        }

    {
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.GLAccount;
    using Finnova.Service.GLAccount.Commands.CreateGLAccount;
    using Finnova.Service.GLAccount.Commands.SetPostingGroupMapping;
    using Finnova.Service.GLAccount.Commands.UpdateGLAccount;
    using Finnova.Service.GLAccount.Queries.GetGLAccountPostability;
    using Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;
    using Finnova.Service.GLAccount.Queries.GetPostingGroupMappings;
    using Finnova.Service.Mappers;
    using Finnova.Tests.Infrastructure;
    using GLAccountEntity = Finnova.Models.Domain.Entities.GLAccount;

    namespace Finnova.Tests.GLAccount;

    /// <summary>
    /// Property-based tests for the GL service layer (min 100 iterations). Each correctness
    /// property from design.md is one randomized test over the in-memory repositories.
    /// </summary>
    public class GLAccountPropertyTests
        }
        private const int Iters = 200;

        private static (CreateGLAccountCommandHandler H, InMemoryGLAccountRepository R, InMemoryGLAccountAuditRepository A) NewCreate()
        {
            var repo = new InMemoryGLAccountRepository();
            var audit = new InMemoryGLAccountAuditRepository();
            return (new CreateGLAccountCommandHandler(repo, audit), repo, audit);


        }

        {
        }
        // Feature: gl-master-management, Property 1: Create round trip
        [Property(MaxTest = Iters)]
        public async Task Property1_CreateRoundTrip()

            var (h, repo, _) = NewCreate();
            var req = new GLAccountBuilder().WithCode("GL" + Guid.NewGuid().ToString("N")[..6]).BuildCreateRequest();
            var created = await h.Handle(new CreateGLAccountCommand(req, "C1", "B1", "admin"), default);
        {
            var query = new GetGLAccountsPagedQueryHandler(repo);
            var page = await query.Handle(new GetGLAccountsPagedQuery(created.GLCode, null, null, 1, 50), default);

            Assert.Contains(page.Data, x => x.Id == created.Id && x.GLCode == req.GLCode.Trim()
                && x.GLName == req.GLName && x.AccountType == req.AccountType && x.PostingGroup == req.PostingGroup);
            Assert.NotEqual(Guid.Empty, created.Id);


        // Feature: gl-master-management, Property 2: Create applies defaults
        [Property(MaxTest = Iters)]
        public async Task Property2_CreateDefaults()

            var (h, repo, _) = NewCreate();
            var req = new CreateGLAccountRequest("GLD" + Guid.NewGuid().ToString("N")[..5],
                "Defaults", GLAccountType.Asset, "Customer",
                Currency: null, Status: null, AllowManualJournal: null, AllowSystemJournal: null,
                EffectiveStartDate: new DateTime(2024, 1, 1), EffectiveEndDate: null, LinkedSLAccounts: null);

            var created = await h.Handle(new CreateGLAccountCommand(req, "C1", "B1", "admin"), default);
        {
            Assert.Equal(GLAccountStatus.Active, created.Status);
            Assert.True(created.AllowManualJournal);
            Assert.True(created.AllowSystemJournal);
            Assert.Equal("INR", created.Currency);
            Assert.Empty(created.LinkedSLAccounts);

    }
        // Feature: gl-master-management, Property 3: GL Code uniqueness on create
        [Property(MaxTest = Iters)]
        public async Task Property3_CodeUniqueness(int seed)
    ```
            var (h, repo, _) = NewCreate();
            var baseCode = "GLU" + Math.Abs(seed % 100000);
            await h.Handle(new CreateGLAccountCommand(
                new GLAccountBuilder().WithCode(baseCode).BuildCreateRequest(), "C", "B", "admin"), default);

            var variant = GLAccountGenerators.CasingWhitespaceVariant(baseCode, Math.Abs(seed));
            var before = repo.Count;
            await Assert.ThrowsAsync<GLAccountDuplicateCodeException>(() =>
                h.Handle(new CreateGLAccountCommand(
                    new GLAccountBuilder().WithCode(variant).BuildCreateRequest(), "C", "B", "admin"), default));
            Assert.Equal(before, repo.Count);

          }
        // Feature: gl-master-management, Property 10: Postability truth table
        [Property(MaxTest = Iters)]
        public async Task Property10_Postability()
    ```
            var repo = new InMemoryGLAccountRepository();
            var entity = new GLAccountBuilder()
                .WithStatus(GLAccountStatus.Active)
                .WithDates(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31))
                .BuildEntity();
            await repo.AddAsync(entity, default);

            var h = new GetGLAccountPostabilityQueryHandler(repo);

            var inRange = await h.Handle(new GetGLAccountPostabilityQuery(entity.Id, new DateTime(2024, 6, 1)), default);
            Assert.True(inRange.Postable);

            var before = await h.Handle(new GetGLAccountPostabilityQuery(entity.Id, new DateTime(2023, 12, 31)), default);
            Assert.False(before.Postable);
    **CREATE `Finnova.Tests/GLAccount/GLAccountEdgeTests.cs`**
            var after = await h.Handle(new GetGLAccountPostabilityQuery(entity.Id, new DateTime(2025, 1, 1)), default);
            Assert.False(after.Postable);
        }
    ```csharp
        // Feature: gl-master-management, Property 21: Mapper preserves fields
        [Property(MaxTest = Iters)]
        public void Property21_MapperPreservesFields()
    using Finnova.Models.Domain.Enums;
            var entity = new GLAccountBuilder().WithLinks("SL1", "SL2").BuildEntity();
            var r = entity.ToResponse();

            Assert.Equal(entity.Id, r.Id);
            Assert.Equal(entity.GLCode, r.GLCode);
            Assert.Equal(entity.GLName, r.GLName);
            Assert.Equal(entity.AccountType, r.AccountType);
            Assert.Equal(entity.PostingGroup, r.PostingGroup);
            Assert.Equal(entity.Currency, r.Currency);
            Assert.Equal(entity.Status, r.Status);
            Assert.Equal(entity.AllowManualJournal, r.AllowManualJournal);
            Assert.Equal(entity.AllowSystemJournal, r.AllowSystemJournal);
            Assert.Equal(entity.EffectiveStartDate, r.EffectiveStartDate);
            Assert.Equal(entity.EffectiveEndDate, r.EffectiveEndDate);
            Assert.Equal(new List<string> { "SL1", "SL2" }, r.LinkedSLAccounts);

    {
        {
        // Remaining properties (4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20) follow the
        // same shape — one randomized [Property(MaxTest = 200)] test each over the in-memory repos
        // and the real handlers/validators. Each is tagged:
        //   // Feature: gl-master-management, Property {n}: {property text}
        // and asserts the design's correctness property:
        //   P4  code format accept/reject (validator): alphanumeric non-"000" accept; symbol/"000" reject
        //   P5  required-field validation: blank required field -> validation error, store unchanged
        //   P6  immutability: differing code/type -> GLAccountImmutableFieldException; blank/equal unchanged
        //   P7  update round trip: changed values persisted; UpdatedAt not earlier
        //   P8  identical update -> no-op, audit count unchanged
        //   P9  date range: end null / end>=start accept; end<start reject
        //   P11 mapping overwrite: exactly one mapping, latest account wins
        //   P12 invalid mapping: bad group / missing / inactive -> PostingGroupMappingValidationException, unchanged
        //   P13 mapping read: every required group once, correct IsMapped
        //   P14 search conjunction: term+type+status; blank/omitted = no filter; no match -> Total 0
        //   P15 pagination: Total/TotalPages/window/beyond-last-empty
        //   P16 ordering: GLCode CI asc, deterministic
        //   P17 audit on create: exactly one Create entry with correct fields
        //   P18 audit on update: exactly one Update entry with before/after of changed fields
        //   P19 no audit on rejection: audit count unchanged after a rejected create/update
        //   P20 audit ordering: ChangedAtUtc desc then Id desc; empty set for unknown id
        }

    - **Properties 1–21** (one randomized test each, FsCheck.Xunit, `MaxTest = 200`).
    - **Validates: Requirements 1.1, 1.2, 1.4, 1.5, 1.6, 1.8, 1.9, 1.13, 2.1, 2.2, 2.3, 2.4, 2.6, 3.1–3.5, 4.1, 4.2, 4.5, 4.8, 5.2, 5.3, 5.4, 6.1–6.6, 7.1, 7.2, 7.3, 8.1–8.6, 8.12, 8.13, 8.14, 9.1, 9.2, 9.4, 9.5, 9.6**
        {
    > The `*`-marked property sub-tasks under tasks 6, 7, and the ones listed above all live in this
    > single `GLAccountPropertyTests.cs` file (one test per property); apply this file once.
        }
  - [ ]* 9.4 Write edge/boundary and not-found unit tests

        {
        }
    using Xunit;
    using FluentValidation.TestHelper;
    using Finnova.Models.Contracts.GLAccounts;

    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.GLAccount.Commands.CreateGLAccount;
    using Finnova.Service.GLAccount.Commands.UpdateGLAccount;
    using Finnova.Service.GLAccount.Queries.GetGLAccountAuditTrail;
    using Finnova.Service.GLAccount.Queries.GetGLAccountPostability;
    using Finnova.Service.GLAccount.Queries.GetGLAccountsPaged;
    using Finnova.Tests.Infrastructure;
        {
    namespace Finnova.Tests.GLAccount;
        }
    public class GLAccountEdgeTests

        private static CreateGLAccountCommand Create(CreateGLAccountRequest r) =>
            new(r, "C1", "B1", "admin");

        // R2.5 — GLCode 20 accept / 21 reject.
        [Theory]
        [InlineData(20, false)]
        [InlineData(21, true)]
        public void GLCode_Length_Boundary(int len, bool expectError)
        [Fact]
            var v = new CreateGLAccountCommandValidator();
            var req = new GLAccountBuilder().WithCode(new string('A', len)).BuildCreateRequest();
            var result = v.TestValidate(Create(req));
            if (expectError) result.ShouldHaveValidationErrorFor(x => x.Request.GLCode);
            else result.ShouldNotHaveValidationErrorFor(x => x.Request.GLCode);
        {
    }
        // R2.4 — reserved "000" rejected.
        [Fact]
        public void GLCode_Reserved_000_Rejected()
    ```
            var v = new CreateGLAccountCommandValidator();
            var req = new GLAccountBuilder().WithCode("000").BuildCreateRequest();
            v.TestValidate(Create(req)).ShouldHaveValidationErrorFor(x => x.Request.GLCode);

    **CREATE `Finnova.Tests/GL/JvLineValidatorPropertyTests.cs`**
        // R1.10 — GLName 100 accept / 101 reject.
        [Theory]
        [InlineData(100, false)]
        [InlineData(101, true)]
        public void GLName_Length_Boundary(int len, bool expectError)

            var v = new CreateGLAccountCommandValidator();
            var req = new GLAccountBuilder().WithName(new string('x', len)).BuildCreateRequest();
            var result = v.TestValidate(Create(req));
            if (expectError) result.ShouldHaveValidationErrorFor(x => x.Request.GLName);
            else result.ShouldNotHaveValidationErrorFor(x => x.Request.GLName);
    ```csharp
    using FsCheck.Xunit;
        // R4.6 — LinkedSLAccounts 100 accept / 101 reject (update).
        [Theory]
        [InlineData(100, false)]
        [InlineData(101, true)]
        public void LinkedSL_Count_Boundary(int count, bool expectError)
    using Finnova.Models.Domain.Enums;
            var v = new UpdateGLAccountCommandValidator();
            var links = Enumerable.Range(0, count).Select(i => "SL" + i).ToList();
            var req = new UpdateGLAccountRequest(null, null, "Name", GLAccountStatus.Active, "Customer",
                true, true, new DateTime(2024, 1, 1), null, links);
            var result = v.TestValidate(new UpdateGLAccountCommand(Guid.NewGuid(), req, "admin"));
            if (expectError) result.ShouldHaveValidationErrorFor(x => x.Request.LinkedSLAccounts);
            else result.ShouldNotHaveValidationErrorFor(x => x.Request.LinkedSLAccounts);
    using Finnova.Service.GL.Internal;
    using Xunit;
        // R8.7/R8.8 — query defaults Page=1, PageSize=20.

        public void Query_Defaults()
    namespace Finnova.Tests.GL;
            var q = new GetGLAccountsPagedQuery(null, null, null);
            Assert.Equal(1, q.Page);
            Assert.Equal(20, q.PageSize);

    {

        {
        }

        [Fact]
        {
        }

        {
        }

        // R8.9/R8.10/R8.11 — page/pageSize/search bounds.
        [Theory]
        [InlineData(0, 20, false)]   // page < 1 -> error
        [InlineData(1, 0, false)]    // pageSize < 1 -> error
        [InlineData(1, 101, false)]  // pageSize > 100 -> error
        [InlineData(1, 20, true)]    // valid
        public void Query_Bounds(int page, int pageSize, bool valid)
        [Fact]
            var v = new GetGLAccountsPagedQueryValidator();
            var result = v.TestValidate(new GetGLAccountsPagedQuery(null, null, null, page, pageSize));
            Assert.Equal(valid, result.IsValid);
        }
        {
        // R4.7 — update unknown id throws NotFound; UpdateAsync never reached.
        [Fact]
        public async Task Update_UnknownId_Throws_NotFound()
        }
            var repo = new InMemoryGLAccountRepository();
            var audit = new InMemoryGLAccountAuditRepository();
            var h = new UpdateGLAccountCommandHandler(repo, audit);
            var req = new GLAccountBuilder().BuildUpdateRequest();
            await Assert.ThrowsAsync<GLAccountNotFoundException>(() =>
                h.Handle(new UpdateGLAccountCommand(Guid.NewGuid(), req, "admin"), default));
            Assert.Equal(0, audit.Count);
    }
    ```
        // R7.4 — postability unknown id throws NotFound.

        public async Task Postability_UnknownId_Throws_NotFound()

            var repo = new InMemoryGLAccountRepository();
            var h = new GetGLAccountPostabilityQueryHandler(repo);
            await Assert.ThrowsAsync<GLAccountNotFoundException>(() =>
                h.Handle(new GetGLAccountPostabilityQuery(Guid.NewGuid(), new DateTime(2024, 1, 1)), default));
    **CREATE `Finnova.Tests/Integration/GLAccountEndpointsTests.cs`**
        // R9.6 — audit read for a missing id returns [].
        [Fact]
        public async Task AuditTrail_MissingId_ReturnsEmpty()
    ```csharp
            var audit = new InMemoryGLAccountAuditRepository();
            var h = new GetGLAccountAuditTrailQueryHandler(audit);
            var result = await h.Handle(new GetGLAccountAuditTrailQuery(Guid.NewGuid()), default);
            Assert.Empty(result);
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
        // R9.7/R9.8 — audit repository exposes no mutate path (immutability).
    using Finnova.Repository.Interfaces;
        public async Task Audit_Is_Immutable()
    using Finnova.Service.GL.Commands.AuthorizeGLAccount;
            var audit = new InMemoryGLAccountAuditRepository();
            var entry = new Finnova.Models.Domain.Entities.GLAccountAuditEntry { GLAccountId = Guid.NewGuid() };
            await audit.AddAsync(entry, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => audit.UpdateAsync(entry, default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => audit.DeleteAsync(entry, default));
    using Finnova.Service.GL.Commands.CreateGLAccount;
    using Finnova.Service.GL.Ports;
    using Moq;
    - _Requirements: 1.3, 1.10, 1.11, 1.12, 2.4, 2.5, 4.3, 4.4, 4.6, 4.7, 5.1, 7.4, 7.5, 8.7, 8.8, 8.9, 8.10, 8.11, 8.15, 9.6, 9.7, 9.8_
    using Xunit;
  - [ ]* 9.5 Write WebApplicationFactory integration tests


    {
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using Xunit;
    using Finnova.Models.Contracts.GLAccounts;

        {

        }

        [Fact]
        {
        }

    namespace Finnova.Tests.Integration;
        [Fact]
    /// <summary>
    /// End-to-end tests for the GL endpoints over the SystemAdmin host (EF Core InMemory + dev
    /// JWTs). Mirrors the Nationality/Lookup integration suites. Assumes a JWT helper exists on the
    /// factory (as used by the other SystemAdmin integration tests): adjust token minting to match
    /// the project's TestTokens/JwtTestHelper.
    /// </summary>
    public class GLAccountEndpointsTests : IClassFixture<SystemAdminAppFactory>
        {
        private readonly SystemAdminAppFactory _factory;
        public GLAccountEndpointsTests(SystemAdminAppFactory factory) => _factory = factory;

        private HttpClient Client() => _factory.CreateClient();

        // --- helpers: mint tokens the same way the existing SystemAdmin tests do ---
        private static string AdminToken() => TestTokens.ForRole("SystemAdmin");   // reuse existing helper
        private static string NonAdminToken() => TestTokens.ForRole("User");
        private const string ExpiredOrInvalidToken = "not-a-real-jwt";
        }
        private HttpClient Authed(string token)

            var c = Client();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return c;
        }
        [Fact]
        // R10.1 / R10.2 / R10.4 — auth matrix on every endpoint.
        [Theory]
        [InlineData("GET", "/api/glaccount")]
        [InlineData("POST", "/api/glaccount")]
        [InlineData("PUT", "/api/glaccount/00000000-0000-0000-0000-000000000001")]
        [InlineData("GET", "/api/glaccount/00000000-0000-0000-0000-000000000001/postability?date=2024-01-01")]
        [InlineData("GET", "/api/glaccount/00000000-0000-0000-0000-000000000001/audit")]
        [InlineData("GET", "/api/glaccount/posting-group-mappings")]
        public async Task Endpoints_Require_Authentication(string method, string path)
        {
            var resp = await Client().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);   // 401, no token (R10.1)
        {



        public async Task Endpoints_Forbid_NonAdmin()

            var resp = await Authed(NonAdminToken()).GetAsync("/api/glaccount");
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);       // 403 (R10.2)
        }

        [Fact]
        {
        public async Task InvalidToken_LackingRole_Returns401_Before403()

            var resp = await Authed(ExpiredOrInvalidToken).GetAsync("/api/glaccount");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);    // 401 before 403 (R10.4)

        }
        // R1/R8/R9/R7 — create -> list -> audit -> postability happy path.
    }
        public async Task Create_List_Audit_Postability_HappyPath()
    ```
            var client = Authed(AdminToken());
            var req = new CreateGLAccountRequest("GLIT" + Guid.NewGuid().ToString("N")[..5],
                "Cash in Hand", GLAccountType.Asset, "Customer", null, null, null, null,
                new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), null);
    > Swap `TestTokens.ForRole(...)` for the project's actual dev-JWT helper used by the existing
            var create = await client.PostAsJsonAsync("/api/glaccount", req);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<GLAccountResponse>();
            Assert.NotNull(created);
    > SystemAdmin integration tests (e.g. the same helper the Nationality integration tests use).
            var list = await client.GetAsync($"/api/glaccount?search={created!.GLCode}");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    - _Requirements: 1.1, 2.6, 7.1, 8.1, 9.1, 9.5, 10.1, 10.2, 10.3, 10.4_
            var audit = await client.GetAsync($"/api/glaccount/{created.Id}/audit");
            Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
            var entries = await audit.Content.ReadFromJsonAsync<List<GLAccountAuditEntryResponse>>();
            Assert.Single(entries!);
            Assert.Equal("Create", entries![0].Action);

            var post = await client.GetAsync($"/api/glaccount/{created.Id}/postability?date=2024-06-01");
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var postability = await post.Content.ReadFromJsonAsync<GLAccountPostabilityResponse>();
            Assert.True(postability!.Postable);
  - [ ] 9.6 Checkpoint — backend build and tests green (user runs)
    - Run `dotnet build` and `dotnet test`. Ensure all tests pass; ask the user if questions arise.
        // R2.6 — duplicate code -> 409 ERR-GL-409.
    - _Requirements: all backend_
        public async Task Duplicate_Code_Returns_409()

            var client = Authed(AdminToken());
            var code = "GLDUP" + Guid.NewGuid().ToString("N")[..5];
            var req = new CreateGLAccountRequest(code, "First", GLAccountType.Asset, "Customer",
                null, null, null, null, new DateTime(2024, 1, 1), null, null);

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/glaccount", req)).StatusCode);

            var dup = await client.PostAsJsonAsync("/api/glaccount",
                req with { GLCode = code.ToLowerInvariant() });   // casing variant
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
            var problem = await dup.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.Equal("ERR-GL-409", problem!["code"]?.ToString());



        }

        }

        }

    export interface PostingGroupMapping {
      postingGroup: string;
- [ ] 10. Frontend model and service layer (Finnova-UI repo)
  - **Implemented in the separate `Finnova-UI` repository at `E:\Finnova\Finnova-UI\Finnova-UI`
    (React 18 + TS + MUI + Vite; hooks + Context; no Redux/RxJS). Paths below are relative to that
    repo's `src/`.**
      glAccountId: string | null;
  - [ ] 10.1 Add the GL model
      glCode: string | null;
    **CREATE `src/models/glaccount.model.ts`**
    ```typescript
    export type GLAccountType = 'Asset' | 'Liability' | 'Income' | 'Expense';
    export type GLAccountStatus = 'Active' | 'Inactive';
      glName: string | null;
    export interface GLAccount {
      id: string;
      glCode: string;
      glName: string;
      accountType: GLAccountType;
      postingGroup: string;
      currency: string;
      status: GLAccountStatus;
      allowManualJournal: boolean;
      allowSystemJournal: boolean;
      companyCode: string;
      userBranch: string;
      effectiveStartDate: string;        // ISO date
      effectiveEndDate: string | null;
      linkedSLAccounts: string[];
      createdAt: string;
      updatedAt: string;
    }
      isMapped: boolean;
    export interface GLAccountFormData {
      glCode: string;
      glName: string;
      accountType: GLAccountType;
      postingGroup: string;
      currency?: string;
      status?: GLAccountStatus;
      allowManualJournal?: boolean;
      allowSystemJournal?: boolean;
      effectiveStartDate: string;
      effectiveEndDate?: string | null;
      linkedSLAccounts?: string[];
    }
    }
    export interface GLAccountUpdateData {
      glCode?: string;                 // carried for immutability equality check only
      accountType?: GLAccountType;     // carried for immutability equality check only
      glName: string;
      status: GLAccountStatus;
      postingGroup: string;
      allowManualJournal: boolean;
      allowSystemJournal: boolean;
      effectiveStartDate: string;
      effectiveEndDate?: string | null;
      linkedSLAccounts?: string[];
    ```
    export interface GLAccountAuditEntry {
      id: string;
      glAccountId: string;
      action: 'Create' | 'Update';
      changesJson: string;
      summary: string;
      changedBy: string;
      changedAtUtc: string;
    }

    export interface GLAccountPostability {
      glAccountId: string;
      glCode: string;
      postable: boolean;
      date: string;
      status: GLAccountStatus;
      effectiveStartDate: string;
      effectiveEndDate: string | null;








      id: string;
      glCode: string;
      createdAt: string;
    **MODIFY `src/models/index.ts`** — add this re-export line (alongside the other model exports):
    ```typescript
    export * from './glaccount.model';
    ```
    - _Requirements: 1.1, 4.1, 6.6, 7.1, 9.5_
      updatedAt: string;
  - [ ] 10.2 Add the GL service interface
    }
    **CREATE `src/services/interfaces/glaccount.interface.ts`**
    ```typescript
    import type {
      GLAccount,
      GLAccountFormData,
      GLAccountUpdateData,
      GLAccountAuditEntry,
      GLAccountPostability,
      PostingGroupMapping,
      GLAccountType,
      GLAccountStatus,
      PaginatedResponse,
    } from '../../models';

    export interface GLAccountQueryParams {
      search?: string;
      accountType?: GLAccountType;
      status?: GLAccountStatus;
      page?: number;
      pageSize?: number;
    }
    }
    export interface IGLAccountService {
      getPaged(params: GLAccountQueryParams): Promise<PaginatedResponse<GLAccount>>;
      create(data: GLAccountFormData): Promise<GLAccount>;
      update(id: string, data: GLAccountUpdateData): Promise<GLAccount>;
      getPostability(id: string, date: string): Promise<GLAccountPostability>;
      getAuditTrail(id: string): Promise<GLAccountAuditEntry[]>;
      getPostingGroupMappings(): Promise<PostingGroupMapping[]>;
      setPostingGroupMapping(postingGroup: string, glAccountId: string): Promise<PostingGroupMapping>;
    }
    ```

    **MODIFY `src/services/interfaces/index.ts`** — add this re-export line:
    ```typescript
    export type { IGLAccountService, GLAccountQueryParams } from './glaccount.interface';
    ```
    - _Requirements: 1.1, 4.1, 6.2, 6.6, 7.1, 8.1, 9.5_
    }
  - [ ] 10.3 Implement the real GL service

    **CREATE `src/services/real/glaccount.real.ts`**
    ```typescript
    import api from '../api';
    import type {
      GLAccount,
      GLAccountFormData,
      GLAccountUpdateData,
      GLAccountAuditEntry,
      GLAccountPostability,
      PostingGroupMapping,
      GLAccountType,
      GLAccountStatus,
      PaginatedResponse,
    } from '../../models';
    import type { IGLAccountService, GLAccountQueryParams } from '../interfaces';
    }
    interface GLAccountApiResponse {

    }

      glName: string;
      accountType: GLAccountType;
      postingGroup: string;
      currency: string;
      status: GLAccountStatus;
      allowManualJournal: boolean;
      allowSystemJournal: boolean;
      companyCode: string;
      userBranch: string;
      effectiveStartDate: string;
      effectiveEndDate: string | null;
      linkedSLAccounts: string[];
    }

    }

    function toGLAccount(dto: GLAccountApiResponse): GLAccount {
      return {
        id: dto.id,
        glCode: dto.glCode,
        glName: dto.glName,
        accountType: dto.accountType,
        postingGroup: dto.postingGroup,
        currency: dto.currency,
        status: dto.status,
        allowManualJournal: dto.allowManualJournal,
        allowSystemJournal: dto.allowSystemJournal,
        companyCode: dto.companyCode,
        userBranch: dto.userBranch,
        effectiveStartDate: dto.effectiveStartDate,
        effectiveEndDate: dto.effectiveEndDate,
        linkedSLAccounts: dto.linkedSLAccounts ?? [],
        createdAt: dto.createdAt,
        updatedAt: dto.updatedAt,
      };
    }

    /**
     * Real API implementation - calls SystemAdminService via the gateway. The shared `api`
     * instance injects the Bearer token and surfaces error.response.data.message via toast.
     * The gateway aliases /api/ua/api/glaccount/** to the SystemAdmin cluster, so the
     * service-relative base path is simply '/glaccount'.
     */
    class GLAccountRealService implements IGLAccountService {
      private readonly basePath = '/glaccount';

      async getPaged(params: GLAccountQueryParams): Promise<PaginatedResponse<GLAccount>> {
        const response = await api.get<PaginatedResponse<GLAccountApiResponse>>(this.basePath, {
          params: {
            search: params.search,
            accountType: params.accountType,
            status: params.status,
            page: params.page,
            pageSize: params.pageSize,
          },
        });
        const body = response.data;
        return {
          data: body.data.map(toGLAccount),
          total: body.total,
          page: body.page,
          pageSize: body.pageSize,
          totalPages: body.totalPages,
        };
    }

      async create(data: GLAccountFormData): Promise<GLAccount> {
        const response = await api.post<GLAccountApiResponse>(this.basePath, {
          glCode: data.glCode,
          glName: data.glName,
          accountType: data.accountType,
          postingGroup: data.postingGroup,
          currency: data.currency,
          status: data.status,
          allowManualJournal: data.allowManualJournal,
          allowSystemJournal: data.allowSystemJournal,
          effectiveStartDate: data.effectiveStartDate,
          effectiveEndDate: data.effectiveEndDate,
          linkedSLAccounts: data.linkedSLAccounts,
        });
        return toGLAccount(response.data);
    export interface PaginatedResponse<T> {
      async update(id: string, data: GLAccountUpdateData): Promise<GLAccount> {
        const response = await api.put<GLAccountApiResponse>(`${this.basePath}/${id}`, {
          glCode: data.glCode,
          accountType: data.accountType,
          glName: data.glName,
          status: data.status,
          postingGroup: data.postingGroup,
          allowManualJournal: data.allowManualJournal,
          allowSystemJournal: data.allowSystemJournal,
          effectiveStartDate: data.effectiveStartDate,
          effectiveEndDate: data.effectiveEndDate,
          linkedSLAccounts: data.linkedSLAccounts,
        });
        return toGLAccount(response.data);
      data: T[];
      async getPostability(id: string, date: string): Promise<GLAccountPostability> {
        const response = await api.get<GLAccountPostability>(
          `${this.basePath}/${id}/postability`, { params: { date } });
        return response.data;
      }
      total: number;
      async getAuditTrail(id: string): Promise<GLAccountAuditEntry[]> {
        const response = await api.get<GLAccountAuditEntry[]>(`${this.basePath}/${id}/audit`);
        return response.data;
      page: number;
      pageSize: number;
      async getPostingGroupMappings(): Promise<PostingGroupMapping[]> {
        const response = await api.get<PostingGroupMapping[]>(`${this.basePath}/posting-group-mappings`);
        return response.data;
      totalPages: number;
    }
      async setPostingGroupMapping(postingGroup: string, glAccountId: string): Promise<PostingGroupMapping> {
        const response = await api.put<PostingGroupMapping>(
          `${this.basePath}/posting-group-mappings/${encodeURIComponent(postingGroup)}`,
          { glAccountId });
        return response.data;

    ```
    - _Requirements: 1.1, 4.1, 6.2, 6.6, 7.1, 8.1, 9.5_







    export const glAccountRealService = new GLAccountRealService();

  - [ ] 10.4 Implement the mock GL service
    }
    **CREATE `src/services/mock/glaccount.mock.ts`**
    ```typescript
    import type {
      GLAccount,
      GLAccountFormData,
      GLAccountUpdateData,
      GLAccountAuditEntry,
      GLAccountPostability,
      PostingGroupMapping,
      PaginatedResponse,
    } from '../../models';
    import type { IGLAccountService, GLAccountQueryParams } from '../interfaces';
    ```
    const REQUIRED_GROUPS = ['Customer', 'Dealer', 'Insurance', 'Unearned Income', 'Lease Receivable'];
    const ALPHANUMERIC = /^[A-Za-z0-9]+$/;

    /** Shape mirroring the backend ProblemDetails so the UI's axios interceptor reads it the same way. */
    function glError(status: number, code: string, message: string) {
      return { response: { status, data: { code, message } } };
    }

    function nowIso(): string { return new Date().toISOString(); }

    class GLAccountMockService implements IGLAccountService {
      private store: GLAccount[] = [];
      private audit: GLAccountAuditEntry[] = [];
      private mappings: Record<string, string> = {};   // postingGroup -> glAccountId

      constructor() {
        // English-only seeds (India-only platform).
        this.seed('GL1001', 'Cash in Hand', 'Asset', 'Customer');
        this.seed('GL2001', 'Vendor Payable', 'Liability', 'Dealer');
      }
      }
      private seed(glCode: string, glName: string, accountType: GLAccount['accountType'], postingGroup: string) {
        const gl: GLAccount = {
          id: crypto.randomUUID(),
          glCode, glName, accountType, postingGroup,
          currency: 'INR', status: 'Active',
          allowManualJournal: true, allowSystemJournal: true,
          companyCode: 'C1', userBranch: 'B1',
          effectiveStartDate: '2024-01-01', effectiveEndDate: null,
          linkedSLAccounts: [], createdAt: nowIso(), updatedAt: nowIso(),
        };
        this.store.push(gl);
        this.audit.push({
          id: crypto.randomUUID(), glAccountId: gl.id, action: 'Create',
          changesJson: JSON.stringify({ GLCode: { old: null, new: glCode } }),
          summary: `Created GL account ${glCode}.`, changedBy: 'seed', changedAtUtc: nowIso(),
        });
      }
      async getTree(companyId: string): Promise<GLTreeNode[]> {
        const { data } = await api.get(`${BASE}/tree/${companyId}`);
      private existsByCode(code: string, excludeId?: string): boolean {
        const c = code.trim().toLowerCase();
        return this.store.some(x => x.glCode.toLowerCase() === c && x.id !== excludeId);
      }
        return data;
      async getPaged(params: GLAccountQueryParams): Promise<PaginatedResponse<GLAccount>> {
        const page = params.page ?? 1;
        const pageSize = params.pageSize ?? 20;
        let items = [...this.store];
      }
        const term = params.search?.trim().toLowerCase();
        if (term) items = items.filter(x =>
          x.glCode.toLowerCase().includes(term) || x.glName.toLowerCase().includes(term));
        if (params.accountType) items = items.filter(x => x.accountType === params.accountType);
        if (params.status) items = items.filter(x => x.status === params.status);
      async getAudit(id: string): Promise<GLAuditEntry[]> {
        items.sort((a, b) => a.glCode.toLowerCase().localeCompare(b.glCode.toLowerCase()));
        const total = items.length;
        const slice = items.slice((page - 1) * pageSize, (page - 1) * pageSize + pageSize);
        return { data: slice, total, page, pageSize, totalPages: Math.ceil(total / pageSize) };
        const { data } = await api.get(`${BASE}/${id}/audit`);

      async create(data: GLAccountFormData): Promise<GLAccount> {
        const code = data.glCode.trim();
        if (!ALPHANUMERIC.test(code)) throw glError(400, 'ERR-GL-400', 'GL Code must be alphanumeric.');
        if (code === '000') throw glError(400, 'ERR-GL-400', "GL Code '000' is reserved.");
        if (this.existsByCode(code)) throw glError(409, 'ERR-GL-409', 'GL Code already exists');
        if (data.effectiveEndDate && data.effectiveEndDate < data.effectiveStartDate)
          throw glError(400, 'ERR-GL-400', 'Effective End Date must be on or after the Effective Start Date.');

        const gl: GLAccount = {
          id: crypto.randomUUID(),
          glCode: code, glName: data.glName, accountType: data.accountType, postingGroup: data.postingGroup,
          currency: data.currency?.trim() || 'INR',
          status: data.status ?? 'Active',
          allowManualJournal: data.allowManualJournal ?? true,
          allowSystemJournal: data.allowSystemJournal ?? true,
          companyCode: 'C1', userBranch: 'B1',
          effectiveStartDate: data.effectiveStartDate,
          effectiveEndDate: data.effectiveEndDate ?? null,
          linkedSLAccounts: data.linkedSLAccounts ?? [],
          createdAt: nowIso(), updatedAt: nowIso(),
        };
        this.store.push(gl);
        this.audit.push({
          id: crypto.randomUUID(), glAccountId: gl.id, action: 'Create',
          changesJson: JSON.stringify({ GLCode: { old: null, new: gl.glCode } }),
          summary: `Created GL account ${gl.glCode}.`, changedBy: 'mock', changedAtUtc: nowIso(),
        });
        return { ...gl };
        return data;
      }
      async getCompleteness(companyId: string): Promise<SystemGLCompleteness> {
        const { data } = await api.get(`${BASE}/systemgl/${companyId}/completeness`);
        return data;
      }
      async create(request: CreateGLAccountRequest): Promise<GLAccount> {
        const { data } = await api.post(BASE, request);
        return data;
      }
      async update(id: string, request: UpdateGLAccountRequest): Promise<GLAccount> {
        const { data } = await api.put(`${BASE}/${id}`, request);
        return data;
      }
      async submit(id: string): Promise<GLAccount> {
        const { data } = await api.post(`${BASE}/${id}/submit`);
        return data;
      }
      async authorize(id: string): Promise<GLAccount> {
        const { data } = await api.post(`${BASE}/${id}/authorize`);
        return data;
      }
      async reject(id: string, reason: string): Promise<GLAccount> {
        const { data } = await api.post(`${BASE}/${id}/reject`, { reason });
        return data;
      }
      async freeze(id: string, reason: string): Promise<GLAccount> {
        const { data } = await api.post(`${BASE}/${id}/freeze`, { reason });
        return data;
      }
      async deactivate(id: string, reason?: string): Promise<GLAccount> {
        const { data } = await api.post(`${BASE}/${id}/deactivate`, { reason });
        return data;
      }
      async remove(id: string): Promise<void> {
        await api.delete(`${BASE}/${id}`);
      }
      async mapSystemGL(companyId: string, type: SystemGLType, glAccountId: string): Promise<SystemGLCompleteness> {
        const { data } = await api.post(`${BASE}/systemgl`, { companyId, type, glAccountId });
        return data;
      }
      async enablePosting(companyId: string): Promise<void> {
        await api.post(`${BASE}/systemgl/${companyId}/enable-posting`);
      }
      async dryRun(companyId: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport> {
        const { data } = await api.post(`${BASE}/bulk/dry-run`, { companyId, rows });
        return data;
      }
      async commit(companyId: string, batchId: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport> {
        const { data } = await api.post(`${BASE}/bulk/commit`, { companyId, batchId, rows });
        return data;
      }
      async authorizeBatch(batchId: string): Promise<number> {
        const { data } = await api.post(`${BASE}/bulk/${batchId}/authorize`);
        return data;
      }
      async rejectBatch(batchId: string, reason: string): Promise<number> {
        const { data } = await api.post(`${BASE}/bulk/${batchId}/reject`, { reason });
        return data;
      }
      async exportAccounts(format: ExportFormat, filter: GLAccountFilter): Promise<Blob> {
        const { data } = await api.post(`${BASE}/export`, { format, filter }, { responseType: 'blob' });
        return data;
      }





      }
      async update(id: string, data: GLAccountUpdateData): Promise<GLAccount> {
        const gl = this.store.find(x => x.id === id);
        if (!gl) throw glError(404, 'ERR-GL-404', `GL account '${id}' was not found.`);
        };
        if (data.glCode && data.glCode.trim().toLowerCase() !== gl.glCode.toLowerCase())
          throw glError(409, 'ERR-GL-409', 'GL Code is immutable and cannot be changed.');
        if (data.accountType && data.accountType !== gl.accountType)
          throw glError(409, 'ERR-GL-409', 'Account Type is immutable and cannot be changed.');
        if (data.effectiveEndDate && data.effectiveEndDate < data.effectiveStartDate)
          throw glError(400, 'ERR-GL-400', 'Effective End Date must be on or after the Effective Start Date.');
      }
        const newLinks = data.linkedSLAccounts ?? [];
        const unchanged =
          gl.glName === data.glName && gl.status === data.status && gl.postingGroup === data.postingGroup &&
          gl.allowManualJournal === data.allowManualJournal && gl.allowSystemJournal === data.allowSystemJournal &&
          gl.effectiveStartDate === data.effectiveStartDate && (gl.effectiveEndDate ?? null) === (data.effectiveEndDate ?? null) &&
          JSON.stringify(gl.linkedSLAccounts) === JSON.stringify(newLinks);
        if (unchanged) return { ...gl };
      }
        gl.glName = data.glName;
        gl.status = data.status;
        gl.postingGroup = data.postingGroup;
        gl.allowManualJournal = data.allowManualJournal;
        gl.allowSystemJournal = data.allowSystemJournal;
        gl.effectiveStartDate = data.effectiveStartDate;
        gl.effectiveEndDate = data.effectiveEndDate ?? null;
        gl.linkedSLAccounts = newLinks;
        gl.updatedAt = nowIso();
        return {
        this.audit.push({
          id: crypto.randomUUID(), glAccountId: gl.id, action: 'Update',
          changesJson: JSON.stringify({ GLName: { new: gl.glName } }),
          summary: `Updated GL account ${gl.glCode}.`, changedBy: 'mock', changedAtUtc: nowIso(),
        });
        return { ...gl };
        };

      async getPostability(id: string, date: string): Promise<GLAccountPostability> {
        const gl = this.store.find(x => x.id === id);
        if (!gl) throw glError(404, 'ERR-GL-404', `GL account '${id}' was not found.`);
        const postable = gl.status === 'Active' &&
          date >= gl.effectiveStartDate &&
          (gl.effectiveEndDate === null || date <= gl.effectiveEndDate);
        return {
          glAccountId: gl.id, glCode: gl.glCode, postable, date, status: gl.status,
          effectiveStartDate: gl.effectiveStartDate, effectiveEndDate: gl.effectiveEndDate,
        });
      }
      }
    }

      async getAuditTrail(id: string): Promise<GLAccountAuditEntry[]> {
        return this.audit
          .filter(a => a.glAccountId === id)
          .sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc));


      async getPostingGroupMappings(): Promise<PostingGroupMapping[]> {
        return REQUIRED_GROUPS.map(group => {
          const glId = this.mappings[group];
          const gl = glId ? this.store.find(x => x.id === glId) : undefined;
    export const glAccountMockService = new GLAccountMockService();
            postingGroup: group,
            glAccountId: gl?.id ?? null,
            glCode: gl?.glCode ?? null,
            glName: gl?.glName ?? null,
            isMapped: !!gl,
    ```
    - _Requirements: 1.1, 2.3, 2.4, 2.6, 3.1, 3.2, 4.8, 5.3, 6.2, 6.3, 6.4, 6.5, 6.6, 7.1, 7.2, 7.3, 8.1, 8.5, 8.13, 9.5, 9.6_


      async setPostingGroupMapping(postingGroup: string, glAccountId: string): Promise<PostingGroupMapping> {
        if (!REQUIRED_GROUPS.includes(postingGroup))
          throw glError(400, 'ERR-GL-400', `Posting Group '${postingGroup}' is not a required posting group.`);
        const gl = this.store.find(x => x.id === glAccountId);
        if (!gl) throw glError(400, 'ERR-GL-400', 'The mapped GL account was not found.');
        if (gl.status === 'Inactive') throw glError(400, 'ERR-GL-400', 'An inactive GL account cannot be mapped.');
        this.mappings[postingGroup] = gl.id;   // overwrite
        return { postingGroup, glAccountId: gl.id, glCode: gl.glCode, glName: gl.glName, isMapped: true };


    /**
     * Toggle between mock and real API via VITE_USE_MOCK_API.
     * Set VITE_USE_MOCK_API=true to use mock data; otherwise the real backend via the gateway.
     */
    const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

    ```

  - [ ] 10.5 Add the toggle module and barrel re-export

    **CREATE `src/services/glaccount.service.ts`**
    ```typescript
    import type { IGLAccountService } from './interfaces';
    import { glAccountMockService } from './mock/glaccount.mock';
    import { glAccountRealService } from './real/glaccount.real';







    export const glAccountService: IGLAccountService = useMock
      ? glAccountMockService
      : glAccountRealService;

    ```
    **MODIFY `src/services/index.ts`** — add this re-export line (alongside the other service exports):
    ```typescript
    export { glAccountService } from './glaccount.service';
    ```
    - _Requirements: 1.1, 8.1_
    - _Requirements: 1.1, 2.3, 2.4, 2.6, 3.1, 3.2, 6.2, 6.3, 7.1, 7.2, 7.3, 8.1, 8.13_
  - [ ]* 10.6 Write service unit tests (Vitest)

    **CREATE `src/services/__tests__/glaccount.service.test.ts`**
    ```typescript
    import { describe, it, expect, vi, beforeEach } from 'vitest';
    import { glAccountMockService } from '../mock/glaccount.mock';

    describe('glAccountMockService', () => {
      it('rejects a duplicate code (case-insensitive) with an ERR-GL-409 shape', async () => {
        await glAccountMockService.create({
          glCode: 'GLX1', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        });
        await expect(glAccountMockService.create({
          glCode: 'glx1', glName: 'B', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        })).rejects.toMatchObject({ response: { status: 409, data: { code: 'ERR-GL-409' } } });
      });
    ```tsx
      it('rejects a non-alphanumeric or reserved 000 code', async () => {
        await expect(glAccountMockService.create({
          glCode: 'GL-1', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        })).rejects.toMatchObject({ response: { data: { code: 'ERR-GL-400' } } });
        await expect(glAccountMockService.create({
          glCode: '000', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        })).rejects.toMatchObject({ response: { data: { code: 'ERR-GL-400' } } });
      });
    import {
      it('enforces GL Code immutability on update', async () => {
        const gl = await glAccountMockService.create({
          glCode: 'GLIMM1', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        });
        await expect(glAccountMockService.update(gl.id, {
          glCode: 'DIFFERENT', glName: 'A', status: 'Active', postingGroup: 'Customer',
          allowManualJournal: true, allowSystemJournal: true, effectiveStartDate: '2024-01-01',
        })).rejects.toMatchObject({ response: { status: 409, data: { code: 'ERR-GL-409' } } });
      });
    } from '@mui/material';
      it('computes postability over the effective range', async () => {
        const gl = await glAccountMockService.create({
          glCode: 'GLPOST1', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01', effectiveEndDate: '2024-12-31',
        });
        expect((await glAccountMockService.getPostability(gl.id, '2024-06-01')).postable).toBe(true);
        expect((await glAccountMockService.getPostability(gl.id, '2023-12-31')).postable).toBe(false);
        expect((await glAccountMockService.getPostability(gl.id, '2025-01-01')).postable).toBe(false);
      });

      it('overwrites a posting-group mapping and rejects an invalid group', async () => {
        const gl = await glAccountMockService.create({
          glCode: 'GLMAP1', glName: 'A', accountType: 'Asset', postingGroup: 'Customer',
          effectiveStartDate: '2024-01-01',
        });
        const mapped = await glAccountMockService.setPostingGroupMapping('Customer', gl.id);
        expect(mapped.isMapped).toBe(true);
        await expect(glAccountMockService.setPostingGroupMapping('NotAGroup', gl.id))
          .rejects.toMatchObject({ response: { data: { code: 'ERR-GL-400' } } });
      });
    });

    describe('glAccountRealService (DTO/path composition)', () => {
      beforeEach(() => vi.resetModules());

      it('composes GET /glaccount with query params and maps the DTO', async () => {
        vi.doMock('../api', () => ({
          default: {
            get: vi.fn().mockResolvedValue({
              data: { data: [{ id: '1', glCode: 'GL1', glName: 'N', accountType: 'Asset',
                postingGroup: 'Customer', currency: 'INR', status: 'Active',
                allowManualJournal: true, allowSystemJournal: true, companyCode: 'C', userBranch: 'B',
                effectiveStartDate: '2024-01-01', effectiveEndDate: null, linkedSLAccounts: [],
                createdAt: 'x', updatedAt: 'y' }], total: 1, page: 1, pageSize: 20, totalPages: 1 },
            }),
          },
        }));
        const api = (await import('../api')).default as unknown as { get: ReturnType<typeof vi.fn> };
        const { glAccountRealService } = await import('../real/glaccount.real');
        const result = await glAccountRealService.getPaged({ search: 'GL', page: 1, pageSize: 20 });
        expect(api.get).toHaveBeenCalledWith('/glaccount', expect.objectContaining({
          params: expect.objectContaining({ search: 'GL', page: 1, pageSize: 20 }),
        }));
        expect(result.data[0].glCode).toBe('GL1');
      });
    });



- [ ] 11. Frontend GLAccountMaster page, route, and nav (Finnova-UI repo)
  - [ ] 11.1 Implement the GLAccountMaster page
        try {
    **CREATE `src/pages/GLAccountMaster.tsx`**
          }
    import { useCallback, useEffect, useMemo, useState } from 'react';

      Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
      FormControl, FormControlLabel, InputAdornment, InputLabel, MenuItem, Select, Stack, Switch,
      TextField, Typography,

    import { DataGrid, type GridColDef } from '@mui/x-data-grid';
    import AddIcon from '@mui/icons-material/Add';
    import AccountBalanceWalletIcon from '@mui/icons-material/AccountBalanceWallet';
    import SearchIcon from '@mui/icons-material/Search';
    import type {
      GLAccount, GLAccountFormData, GLAccountUpdateData, GLAccountAuditEntry,
      GLAccountPostability, PostingGroupMapping, GLAccountType, GLAccountStatus,
    } from '../models';
    import { glAccountService } from '../services';

    const DEFAULT_PAGE_SIZE = 20;
    const SEARCH_DEBOUNCE_MS = 300;
    const ACCOUNT_TYPES: GLAccountType[] = ['Asset', 'Liability', 'Income', 'Expense'];
    const STATUSES: GLAccountStatus[] = ['Active', 'Inactive'];

    const emptyCreate: GLAccountFormData = {
      glCode: '', glName: '', accountType: 'Asset', postingGroup: '',
      currency: 'INR', status: 'Active', allowManualJournal: true, allowSystemJournal: true,
      effectiveStartDate: new Date().toISOString().slice(0, 10), effectiveEndDate: null, linkedSLAccounts: [],
    };

    export default function GLAccountMaster() {
      const [rows, setRows] = useState<GLAccount[]>([]);
      const [loading, setLoading] = useState(false);
      const [searchTerm, setSearchTerm] = useState('');
      const [debouncedSearch, setDebouncedSearch] = useState('');
      const [accountTypeFilter, setAccountTypeFilter] = useState<GLAccountType | ''>('');
      const [statusFilter, setStatusFilter] = useState<GLAccountStatus | ''>('');
      }, []);
      const [addOpen, setAddOpen] = useState(false);
      const [addForm, setAddForm] = useState<GLAccountFormData>(emptyCreate);
      const [editTarget, setEditTarget] = useState<GLAccount | null>(null);
      const [auditTarget, setAuditTarget] = useState<GLAccount | null>(null);
      const [auditEntries, setAuditEntries] = useState<GLAccountAuditEntry[]>([]);
      const [postTarget, setPostTarget] = useState<GLAccount | null>(null);
      const [postDate, setPostDate] = useState(new Date().toISOString().slice(0, 10));
      const [postResult, setPostResult] = useState<GLAccountPostability | null>(null);
      const [mappingOpen, setMappingOpen] = useState(false);
      const [mappings, setMappings] = useState<PostingGroupMapping[]>([]);

      useEffect(() => {
        const h = setTimeout(() => setDebouncedSearch(searchTerm), SEARCH_DEBOUNCE_MS);
        return () => clearTimeout(h);
      }, [searchTerm]);

      const fetchRows = useCallback(async () => {
        setLoading(true);

          const result = await glAccountService.getPaged({
            search: debouncedSearch.trim() || undefined,
            accountType: accountTypeFilter || undefined,
            status: statusFilter || undefined,
            page: 1, pageSize: DEFAULT_PAGE_SIZE,
          });
          setRows(result.data);
        } catch {
          // Error toast surfaced by the shared axios interceptor; leave the grid unchanged.
        } finally {
          setLoading(false);
      return (
      }, [debouncedSearch, accountTypeFilter, statusFilter]);
        </Box>
      useEffect(() => { fetchRows(); }, [fetchRows]);

      const openAudit = useCallback(async (row: GLAccount) => {
        setAuditTarget(row);
        try { setAuditEntries(await glAccountService.getAuditTrail(row.id)); }
        catch { setAuditEntries([]); }
      }, []);

      const openPostability = useCallback((row: GLAccount) => {
        setPostTarget(row); setPostResult(null);
      }, []);

      const runPostability = useCallback(async () => {
        if (!postTarget) return;
        try { setPostResult(await glAccountService.getPostability(postTarget.id, postDate)); }
        catch { /* interceptor toasts */ }
      }, [postTarget, postDate]);

      const openMappings = useCallback(async () => {
        setMappingOpen(true);
        try { setMappings(await glAccountService.getPostingGroupMappings()); }
        catch { setMappings([]); }
      }, []);
          <DialogContent>
      const setMapping = useCallback(async (group: string, glAccountId: string) => {
        try {
          await glAccountService.setPostingGroupMapping(group, glAccountId);
          setMappings(await glAccountService.getPostingGroupMappings());
        } catch { /* interceptor toasts */ }
          </DialogContent>
          <DialogActions>
      const submitCreate = useCallback(async () => {
        try {
          await glAccountService.create(addForm);
          setAddOpen(false); setAddForm(emptyCreate); fetchRows();
        } catch { /* interceptor toasts; dialog stays open */ }
      }, [addForm, fetchRows]);

      const submitEdit = useCallback(async (data: GLAccountUpdateData) => {
        if (!editTarget) return;
        try { await glAccountService.update(editTarget.id, data); setEditTarget(null); fetchRows(); }
        catch { /* interceptor toasts; dialog stays open */ }
      }, [editTarget, fetchRows]);
          </DialogActions>
      const columns = useMemo<GridColDef<GLAccount>[]>(() => [
        { field: 'glCode', headerName: 'GL Code', width: 120 },
        { field: 'glName', headerName: 'GL Name', flex: 1, minWidth: 160 },
        { field: 'accountType', headerName: 'Type', width: 110 },
        { field: 'postingGroup', headerName: 'Posting Group', width: 150 },
        {
          field: 'status', headerName: 'Status', width: 110,
          renderCell: (p) => <Chip size="small" label={p.value}
            color={p.value === 'Active' ? 'success' : 'default'} />,
        },
        { field: 'effectiveStartDate', headerName: 'Effective From', width: 130 },
        {
          field: 'effectiveEndDate', headerName: 'Effective To', width: 130,
          valueGetter: (v) => v ?? '—',
        },
        {
          field: 'actions', headerName: 'Actions', width: 300, sortable: false,
          renderCell: (p) => (
            <Stack direction="row" spacing={1}>
              <Button size="small" onClick={() => setEditTarget(p.row)}>Edit</Button>
              <Button size="small" onClick={() => openAudit(p.row)}>Audit</Button>
              <Button size="small" onClick={() => openPostability(p.row)}>Postability</Button>
            </Stack>
          ),
        },
      ], [openAudit, openPostability]);
        </Dialog>
      );
        <Box>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 3 }}>
            <Stack direction="row" spacing={1.5} alignItems="center">
              <AccountBalanceWalletIcon color="primary" fontSize="large" />
              <Box>
                <Typography variant="h5" fontWeight={700}>GL Master</Typography>
                <Typography variant="body2" color="text.secondary">
                  Manage General Ledger accounts and posting-group mappings
                </Typography>
    }
            </Stack>
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" onClick={openMappings}>Posting-Group Mappings</Button>
              <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>Add</Button>
            </Stack>
          </Stack>
    ```
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField
              placeholder="Search by GL code or name..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              size="small" fullWidth
              InputProps={{ startAdornment: (<InputAdornment position="start"><SearchIcon color="action" /></InputAdornment>) }}
            />
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel>Account Type</InputLabel>
              <Select label="Account Type" value={accountTypeFilter}
                onChange={(e) => setAccountTypeFilter(e.target.value as GLAccountType | '')}>
                <MenuItem value="">All</MenuItem>
                {ACCOUNT_TYPES.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 140 }}>
              <InputLabel>Status</InputLabel>
              <Select label="Status" value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as GLAccountStatus | '')}>
                <MenuItem value="">All</MenuItem>
                {STATUSES.map(s => <MenuItem key={s} value={s}>{s}</MenuItem>)}
              </Select>
            </FormControl>
          </Stack>

          <Card>
            <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
              <DataGrid autoHeight rows={rows} columns={columns} loading={loading}
                getRowId={(r) => r.id} disableRowSelectionOnClick pageSizeOptions={[20]} />
            </CardContent>
          </Card>

          {/* Add dialog */}
          <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="sm" fullWidth>
            <DialogTitle>Add GL Account</DialogTitle>

              <Stack spacing={2} sx={{ mt: 1 }}>
                <TextField label="GL Code" value={addForm.glCode}
                  onChange={(e) => setAddForm({ ...addForm, glCode: e.target.value })} fullWidth />
                <TextField label="GL Name" value={addForm.glName}
                  onChange={(e) => setAddForm({ ...addForm, glName: e.target.value })} fullWidth />
                <FormControl fullWidth>
                  <InputLabel>Account Type</InputLabel>
                  <Select label="Account Type" value={addForm.accountType}
                    onChange={(e) => setAddForm({ ...addForm, accountType: e.target.value as GLAccountType })}>
                    {ACCOUNT_TYPES.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
                  </Select>
                </FormControl>
                <TextField label="Posting Group" value={addForm.postingGroup}
                  onChange={(e) => setAddForm({ ...addForm, postingGroup: e.target.value })} fullWidth />
                <TextField label="Currency" value={addForm.currency}
                  onChange={(e) => setAddForm({ ...addForm, currency: e.target.value })} fullWidth />
                <FormControl fullWidth>
                  <InputLabel>Status</InputLabel>
                  <Select label="Status" value={addForm.status}
                    onChange={(e) => setAddForm({ ...addForm, status: e.target.value as GLAccountStatus })}>
                    {STATUSES.map(s => <MenuItem key={s} value={s}>{s}</MenuItem>)}
                  </Select>
                </FormControl>
                <FormControlLabel control={<Switch checked={!!addForm.allowManualJournal}
                  onChange={(e) => setAddForm({ ...addForm, allowManualJournal: e.target.checked })} />}
                  label="Allow Manual Journal" />
                <FormControlLabel control={<Switch checked={!!addForm.allowSystemJournal}
                  onChange={(e) => setAddForm({ ...addForm, allowSystemJournal: e.target.checked })} />}
                  label="Allow System Journal" />
                <TextField type="date" label="Effective Start Date" InputLabelProps={{ shrink: true }}
                  value={addForm.effectiveStartDate}
                  onChange={(e) => setAddForm({ ...addForm, effectiveStartDate: e.target.value })} fullWidth />
                <TextField type="date" label="Effective End Date" InputLabelProps={{ shrink: true }}
                  value={addForm.effectiveEndDate ?? ''}
                  onChange={(e) => setAddForm({ ...addForm, effectiveEndDate: e.target.value || null })} fullWidth />
                <TextField label="Linked SL Accounts (comma-separated)"
                  value={(addForm.linkedSLAccounts ?? []).join(', ')}
                  onChange={(e) => setAddForm({ ...addForm,
                    linkedSLAccounts: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })}
                  fullWidth />
              </Stack>

              ))}
              <Button onClick={() => setAddOpen(false)}>Cancel</Button>
              <Button variant="contained" onClick={submitCreate}>Create</Button>
          </Box>
        </Drawer>
      );
    }

          {/* Edit dialog — GL Code + Account Type read-only (immutable) */}
          <GLEditDialog target={editTarget} onClose={() => setEditTarget(null)} onSubmit={submitEdit} />

          {/* Audit dialog */}
          <Dialog open={!!auditTarget} onClose={() => setAuditTarget(null)} maxWidth="md" fullWidth>
            <DialogTitle>Audit Trail — {auditTarget?.glCode}</DialogTitle>
            <DialogContent>
              {auditEntries.length === 0
                ? <Typography variant="body2" color="text.secondary">No audit entries.</Typography>
                : <Stack spacing={1.5} sx={{ mt: 1 }}>
                    {auditEntries.map(e => (
                      <Card key={e.id} variant="outlined">
                        <CardContent>
                          <Stack direction="row" justifyContent="space-between">
                            <Chip size="small" label={e.action}
                              color={e.action === 'Create' ? 'success' : 'info'} />
                            <Typography variant="caption">{e.changedAtUtc} · {e.changedBy}</Typography>
                          </Stack>
                          <Typography variant="body2" sx={{ mt: 1 }}>{e.summary}</Typography>
                          <Typography variant="caption" component="pre"
                            sx={{ whiteSpace: 'pre-wrap', mt: 0.5 }}>{e.changesJson}</Typography>
                        </CardContent>
                      </Card>
                    ))}
                  </Stack>}
            </DialogContent>
            <DialogActions><Button onClick={() => setAuditTarget(null)}>Close</Button></DialogActions>
          </Dialog>
        });
          {/* Postability dialog */}
          <Dialog open={!!postTarget} onClose={() => setPostTarget(null)} maxWidth="xs" fullWidth>
            <DialogTitle>Postability — {postTarget?.glCode}</DialogTitle>
            <DialogContent>
              <Stack spacing={2} sx={{ mt: 1 }}>
                <TextField type="date" label="Date" InputLabelProps={{ shrink: true }}
                  value={postDate} onChange={(e) => setPostDate(e.target.value)} fullWidth />
                {postResult && (
                  <Chip label={postResult.postable ? 'Postable' : 'Not postable'}
                    color={postResult.postable ? 'success' : 'error'} />
                )}
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setPostTarget(null)}>Close</Button>
              <Button variant="contained" onClick={runPostability}>Check</Button>
            </DialogActions>
          </Dialog>
        } else {
          {/* Posting-group mapping dialog */}
          <Dialog open={mappingOpen} onClose={() => setMappingOpen(false)} maxWidth="sm" fullWidth>
            <DialogTitle>Posting-Group Mappings</DialogTitle>
            <DialogContent>
              <Stack spacing={2} sx={{ mt: 1 }}>
                {mappings.map(m => (
                  <Stack key={m.postingGroup} direction="row" spacing={2} alignItems="center">
                    <Typography sx={{ minWidth: 150 }}>{m.postingGroup}</Typography>
                    <FormControl size="small" fullWidth>
                      <InputLabel>GL Account</InputLabel>
                      <Select label="GL Account" value={m.glAccountId ?? ''}
                        onChange={(e) => setMapping(m.postingGroup, e.target.value as string)}>
                        {rows.filter(r => r.status === 'Active').map(r => (
                          <MenuItem key={r.id} value={r.id}>{r.glCode} — {r.glName}</MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                    <Chip size="small" label={m.isMapped ? 'Mapped' : 'Unmapped'}
                      color={m.isMapped ? 'success' : 'default'} />
                  </Stack>
              </Stack>
            </DialogContent>
            <DialogActions><Button onClick={() => setMappingOpen(false)}>Close</Button></DialogActions>
          </Dialog>
          setForm(null);
    }


      return (
          <DialogContent>
          </DialogContent>
          <DialogActions>
    /** Edit dialog: GL Code and Account Type are shown read-only (immutable, R3). */
    function GLEditDialog(props: {
      target: GLAccount | null;
      onClose: () => void;
      onSubmit: (data: GLAccountUpdateData) => void;
    }) {
      const { target, onClose, onSubmit } = props;
      const [form, setForm] = useState<GLAccountUpdateData | null>(null);
          </DialogActions>
      useEffect(() => {
        if (target) {
          setForm({
            glCode: target.glCode, accountType: target.accountType,
            glName: target.glName, status: target.status, postingGroup: target.postingGroup,
            allowManualJournal: target.allowManualJournal, allowSystemJournal: target.allowSystemJournal,
            effectiveStartDate: target.effectiveStartDate, effectiveEndDate: target.effectiveEndDate,
            linkedSLAccounts: target.linkedSLAccounts,
        </Dialog>
      );
    }
    ```
      }, [target]);

      if (!target || !form) return null;

    **MODIFY `src/App.tsx`** — add the import alongside the other page imports (e.g. after
        <Dialog open={!!target} onClose={onClose} maxWidth="sm" fullWidth>
          <DialogTitle>Edit GL Account — {target.glCode}</DialogTitle>
    `import EntityMaster from './pages/EntityMaster';`):
            <Stack spacing={2} sx={{ mt: 1 }}>
              <TextField label="GL Code" value={form.glCode ?? ''} disabled fullWidth />
              <TextField label="Account Type" value={form.accountType ?? ''} disabled fullWidth />
              <TextField label="GL Name" value={form.glName}
                onChange={(e) => setForm({ ...form, glName: e.target.value })} fullWidth />
              <TextField label="Posting Group" value={form.postingGroup}
                onChange={(e) => setForm({ ...form, postingGroup: e.target.value })} fullWidth />
              <FormControl fullWidth>
                <InputLabel>Status</InputLabel>
                <Select label="Status" value={form.status}
                  onChange={(e) => setForm({ ...form, status: e.target.value as GLAccountStatus })}>
                  {STATUSES.map(s => <MenuItem key={s} value={s}>{s}</MenuItem>)}
                </Select>
              </FormControl>
              <FormControlLabel control={<Switch checked={form.allowManualJournal}
                onChange={(e) => setForm({ ...form, allowManualJournal: e.target.checked })} />}
                label="Allow Manual Journal" />
              <FormControlLabel control={<Switch checked={form.allowSystemJournal}
                onChange={(e) => setForm({ ...form, allowSystemJournal: e.target.checked })} />}
                label="Allow System Journal" />
              <TextField type="date" label="Effective Start Date" InputLabelProps={{ shrink: true }}
                value={form.effectiveStartDate}
                onChange={(e) => setForm({ ...form, effectiveStartDate: e.target.value })} fullWidth />
              <TextField type="date" label="Effective End Date" InputLabelProps={{ shrink: true }}
                value={form.effectiveEndDate ?? ''}
                onChange={(e) => setForm({ ...form, effectiveEndDate: e.target.value || null })} fullWidth />
              <TextField label="Linked SL Accounts (comma-separated)"
                value={(form.linkedSLAccounts ?? []).join(', ')}
                onChange={(e) => setForm({ ...form,
                  linkedSLAccounts: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })}
                fullWidth />
            </Stack>
    ```tsx
    import GLAccountMaster from './pages/GLAccountMaster';
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" onClick={() => onSubmit(form)}>Save</Button>
    ```
    and add the route inside the protected `<Route element={<Layout />}>` block (e.g. after
    `<Route path="/entities" element={<EntityMaster />} />`):
    ```tsx
    ```
    - _Requirements: 1.1, 3.1, 3.2, 4.1, 6.2, 6.6, 7.1, 8.1, 8.2, 8.3, 9.5_
    - _Requirements: 1.1, 8.1_
  - [ ] 11.2 Add the route in App.tsx


    **MODIFY `src/components/Layout.tsx`** — in the `Administration` group's `items` array, add this
    menu item (e.g. after the `Entity Master` entry). It reuses the already-imported
    `AccountBalanceIcon`:
    ```tsx
    ```
    > Mirror how the other master pages register their route/nav; adjust the exact anchor if the
    > repo has moved these entries. (If a distinct icon is preferred, import one such as
    <Route path="/gl-accounts" element={<GLAccountMaster />} />
    > `AccountBalanceWalletIcon` at the top of the file and use it here.)
    - _Requirements: 1.1, 8.1_

  - [ ] 11.3 Add the nav entry in Layout.tsx

    **CREATE `src/pages/__tests__/GLAccountMaster.test.tsx`**
    ```tsx


    { text: 'GL Master', icon: <AccountBalanceIcon />, path: '/gl-accounts' },



      });

      });
- [ ] 12. Frontend Vitest page/component tests (Finnova-UI repo)
  - [ ]* 12.1 Write GLAccountMaster page tests

    });
    ```
    import { describe, it, expect, vi, beforeEach } from 'vitest';
    import { render, screen, fireEvent, waitFor } from '@testing-library/react';
    import GLAccountMaster from '../GLAccountMaster';
    import type { GLAccount } from '../../models';

    // Mock the service module so the page talks to a controllable fake.
    const sample: GLAccount = {
      id: 'id-1', glCode: 'GL1001', glName: 'Cash in Hand', accountType: 'Asset',
      postingGroup: 'Customer', currency: 'INR', status: 'Active',
      allowManualJournal: true, allowSystemJournal: true, companyCode: 'C1', userBranch: 'B1',
      effectiveStartDate: '2024-01-01', effectiveEndDate: null, linkedSLAccounts: [],
      createdAt: 'x', updatedAt: 'y',
    };
  - [ ]* 13.2 Component tests
    const svc = {
      getPaged: vi.fn().mockResolvedValue({ data: [sample], total: 1, page: 1, pageSize: 20, totalPages: 1 }),
      create: vi.fn().mockResolvedValue(sample),
      update: vi.fn().mockResolvedValue(sample),
      getPostability: vi.fn().mockResolvedValue({
        glAccountId: 'id-1', glCode: 'GL1001', postable: true, date: '2024-06-01',
        status: 'Active', effectiveStartDate: '2024-01-01', effectiveEndDate: null }),
      getAuditTrail: vi.fn().mockResolvedValue([{
        id: 'a1', glAccountId: 'id-1', action: 'Create', changesJson: '{}',
        summary: 'Created GL account GL1001.', changedBy: 'admin', changedAtUtc: '2024-01-01T00:00:00Z' }]),
      getPostingGroupMappings: vi.fn().mockResolvedValue([
        { postingGroup: 'Customer', glAccountId: null, glCode: null, glName: null, isMapped: false }]),
      setPostingGroupMapping: vi.fn().mockResolvedValue({
        postingGroup: 'Customer', glAccountId: 'id-1', glCode: 'GL1001', glName: 'Cash in Hand', isMapped: true }),
    };

    vi.mock('../../services', () => ({ glAccountService: svc }));
    **CREATE `src/components/gl/__tests__/GLAccountDialog.test.tsx`**
    describe('GLAccountMaster', () => {
      beforeEach(() => vi.clearAllMocks());

      it('renders the grid with fetched GL accounts', async () => {
        render(<GLAccountMaster />);
        expect(await screen.findByText('GL1001')).toBeInTheDocument();
        expect(screen.getByText('Cash in Hand')).toBeInTheDocument();
    ```tsx
      it('opens the Add dialog and submits a create', async () => {
        render(<GLAccountMaster />);
        fireEvent.click(screen.getByRole('button', { name: /add/i }));
        fireEvent.change(screen.getByLabelText(/GL Code/i), { target: { value: 'GL3001' } });
        fireEvent.change(screen.getByLabelText(/GL Name/i), { target: { value: 'New Account' } });
        fireEvent.change(screen.getByLabelText(/Posting Group/i), { target: { value: 'Customer' } });
        fireEvent.click(screen.getByRole('button', { name: /create/i }));
        await waitFor(() => expect(svc.create).toHaveBeenCalled());
    import { describe, it, expect, vi } from 'vitest';
    import { render, screen, fireEvent } from '@testing-library/react';
      it('surfaces a duplicate-code error and leaves the grid unchanged', async () => {
        svc.create.mockRejectedValueOnce({ response: { status: 409, data: { code: 'ERR-GL-409', message: 'GL Code already exists' } } });
        render(<GLAccountMaster />);
        fireEvent.click(screen.getByRole('button', { name: /add/i }));
        fireEvent.change(screen.getByLabelText(/GL Code/i), { target: { value: 'GL1001' } });
        fireEvent.change(screen.getByLabelText(/GL Name/i), { target: { value: 'dup' } });
        fireEvent.change(screen.getByLabelText(/Posting Group/i), { target: { value: 'Customer' } });
        fireEvent.click(screen.getByRole('button', { name: /create/i }));
        await waitFor(() => expect(svc.create).toHaveBeenCalled());
        // The shared axios interceptor reads error.response.data.message; the grid still shows GL1001.
        expect(await screen.findByText('GL1001')).toBeInTheDocument();
    import { GLAccountDialog } from '../GLAccountDialog';


      });

      });
    });
    ```


## Notes

      it('opens the Audit dialog and lists entries', async () => {
        render(<GLAccountMaster />);
        await screen.findByText('GL1001');
        fireEvent.click(screen.getByRole('button', { name: /audit/i }));
        await waitFor(() => expect(svc.getAuditTrail).toHaveBeenCalledWith('id-1'));
        expect(await screen.findByText(/Created GL account GL1001/)).toBeInTheDocument();
      });
- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core
      it('opens the Postability dialog and checks a date', async () => {
        render(<GLAccountMaster />);
        await screen.findByText('GL1001');
        fireEvent.click(screen.getByRole('button', { name: /postability/i }));
        fireEvent.click(screen.getByRole('button', { name: /check/i }));
        await waitFor(() => expect(svc.getPostability).toHaveBeenCalled());
        expect(await screen.findByText(/Postable/)).toBeInTheDocument();
  implementation tasks are never optional.

      it('opens the mappings dialog and sets a mapping', async () => {
        render(<GLAccountMaster />);
        await screen.findByText('GL1001');
        fireEvent.click(screen.getByRole('button', { name: /posting-group mappings/i }));
        await waitFor(() => expect(svc.getPostingGroupMappings).toHaveBeenCalled());
        expect(await screen.findByText('Customer')).toBeInTheDocument();

   ```

    > The GL service unit tests (mock + real) are task 10.6. This task covers the page/component
    > behavior only.
    - _Requirements: 1.1, 2.6, 3.1, 6.2, 6.6, 7.1, 8.1, 9.5_
   > Use `--run` so Vitest executes once instead of entering watch mode.
  - [ ] 12.2 Final checkpoint — frontend build and tests green (user runs)
    - In `Finnova-UI`, run `npm run build` and `npm test`. Ensure all tests pass; ask the user if
      questions arise.
    - _Requirements: all frontend_

### Manual smoke (optional)

5. Start the gateway + `Finnova.AccountsService` as usual, then confirm the GL routes are reachable
   through the gateway alias `/api/ua/api/gl/**` (and directly at `/api/accounts/api/gl/**`) with a
- Each task references specific requirements (and property-test sub-tasks reference a specific
  correctness property from design.md) for traceability.
- Property tests use FsCheck.Xunit with a minimum of 100 iterations (`MaxTest = 200`); each of the
  21 correctness properties is implemented by exactly one property-based test, all in
  `Finnova.Tests/GLAccount/GLAccountPropertyTests.cs`.
- Edge/example behavior (length/enum/boundary, not-found, audit-missing-id, audit immutability) and
  authorization (R10) are covered by plain xUnit facts/theories and `WebApplicationFactory<Program>`
  integration tests, not property tests.
- The SystemAdmin host, JWT scheme, `SystemAdmin` policy, MediatR `ValidationBehavior`, global
  `JsonStringEnumConverter`, and `PaginatedResponse<T>` already exist and are reused, not re-created.
- Immutability violations (GL Code / Account Type) map to **409 Conflict** (`ERR-GL-409`), matching
  the duplicate-code case; this can be switched to 400 in one middleware line if the UI prefers.
- The GL audit trail stores a single `ChangesJson` (`{field:{old,new}}`) per entry rather than a
  column-per-field or a child table, so a GL update that changes many non-financial fields records
  one row.
- Linked SL accounts are a child table (`gl_account_sl_links`); links are stored/returned as
  provided and are not validated against an external SL catalog (SL dependency assumption).
- Frontend tasks (10, 11, 12) are implemented in the separate `Finnova-UI` repository at
  `E:\Finnova\Finnova-UI\Finnova-UI`, not in this backend workspace.
- **Documents-only convention:** this file is the deliverable; the user copies each code block into
  the real source tree and runs the builds/migrations/tests. No source files are created in
  `Finnova-API`/`Finnova-UI` and no `dotnet`/`npm` commands are run as part of producing this spec.
   valid JWT carrying a GL role claim. A request without a token must return 401; a token missing
## Task Dependency Graph
   the required GL role must return 403.
```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "10.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.1", "10.2"] },
    { "id": 2, "tasks": ["3.1", "4.1", "4.2", "6.1", "10.3", "10.4"] },
    { "id": 3, "tasks": ["4.3", "5.1", "6.2", "10.5"] },
    { "id": 4, "tasks": ["4.4", "5.2", "5.3", "5.4", "6.3", "10.6", "11.1"] },
    { "id": 5, "tasks": ["5.5", "7.1", "7.8", "7.15", "7.17", "7.19", "7.21", "7.23", "11.2", "11.3"] },
    { "id": 6, "tasks": ["7.2", "7.3", "7.4", "7.5", "7.6", "7.7", "7.9", "7.10", "7.11", "7.12", "7.13", "7.14", "7.16", "7.18", "7.20", "7.22", "7.24", "8.1", "9.1", "12.1"] },
    { "id": 7, "tasks": ["8.2", "8.3", "9.2"] },
    { "id": 8, "tasks": ["9.3", "9.4", "9.5"] }
  ]
}
