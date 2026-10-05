# Implementation Plan: Drawee Bank & Challan Rules Master Management

**JIRA:** FINNOVA-16 · **Module:** SystemAdmin · **Master:** Drawee Bank & Challan Rules

## Overview

This is a **copy-paste** implementation plan. Each task carries the actual code to paste, one
task per step, in dependency order (domain → exceptions → contracts → EF config + DbSets +
migration → repositories + DI → mappers → CQRS slice → controller + middleware + gateway alias →
tests → frontend). Blocks are marked **CREATE `<path>`** (full file) or **MODIFY `<path>`** (exact
snippet + where it goes). Backend is **C#** (.NET); frontend is **TypeScript/React** — no language
selection needed (the design uses concrete code, not pseudocode).

The host's JWT authentication, `SystemAdmin` authorization policy, MediatR `ValidationBehavior`
pipeline, `ExceptionHandlingMiddleware`, `AddFinnovaRepository` registration, generic
`IRepository<T>` / `RepositoryBase<T>`, `ApplyConfigurationsFromAssembly` discovery, and the
`PaginatedResponse<T>` contract already exist and are **reused, not re-created**. India-only /
English-only throughout: a single `BankName` / `PlaceName`, six-digit Indian PINs, India sample
data (`HDFC` / `HDFC Bank Ltd`, `Mumbai` / `Pune`, PIN `400001` / `411001`). No bilingual/Arabic
fields, no RTL.

## Tasks

- [ ] 1. Add domain entities, enum, and exceptions
  - [ ] 1.1 Add the five domain entities and the audit-action enum

    **CREATE `Finnova.Models/Domain/Entities/DraweeBank.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Aggregate root: a drawee bank reference record. India-only platform: one English BankName.
    /// Owns its DraweeBranch collection and an optional RestrictionDetail (R1, R3, R4).
    /// </summary>
    public class DraweeBank
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string BankCode { get; set; } = string.Empty;   // max 20, unique case-insensitive (R1, R2)
        public string BankName { get; set; } = string.Empty;   // max 150, single English name (R1, R5)

        public bool IsActive { get; set; } = true;             // default true (R1.2)

        // Owned children — persisted/loaded with the aggregate.
        public List<DraweeBranch> Branches { get; set; } = new();   // R3
        public RestrictionDetail? Restriction { get; set; }         // optional (R4.4)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/DraweeBranch.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// A branch/place under a drawee bank. Place Code is unique within the owning bank (R3.3).
    /// EndDate null => open-ended effective period (R3.5). PostalCode is a six-digit Indian PIN (R3.6).
    /// </summary>
    public class DraweeBranch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DraweeBankId { get; set; }                  // owner FK (R3.1)

        public string PlaceCode { get; set; } = string.Empty;   // max 20, unique within bank (R3.2, R3.3)
        public string PlaceName { get; set; } = string.Empty;   // max 150, single English name (R3.2)
        public string Address { get; set; } = string.Empty;     // max 300 (R3.1)
        public string PostalCode { get; set; } = string.Empty;  // six-digit numeric PIN (R3.6)

        public DateTime StartDate { get; set; }                 // R3.1
        public DateTime? EndDate { get; set; }                  // null => open-ended (R3.4, R3.5)
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/RestrictionDetail.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Per-bank clearing-day / effective-period constraint. Optional: a bank may have none (R4.4).
    /// ClearingDays in [0, 365] (R4.2, R4.5). EndDate on or after StartDate (R4.3).
    /// </summary>
    public class RestrictionDetail
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DraweeBankId { get; set; }                  // owner FK (R4.1)

        public int ClearingDays { get; set; }                   // >= 0 and <= 365 (R4.2, R4.5)
        public DateTime StartDate { get; set; }                 // R4.1
        public DateTime EndDate { get; set; }                   // on/after StartDate (R4.3)
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/ChallanRule.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// A standalone challan rule associated with a drawee bank by DraweeBankId. NOT part of the
    /// DraweeBank aggregate: it is never navigated from, loaded with, or returned on the bank.
    /// Rule Code is unique within the owning bank (R6.4). Managed only through its own endpoints.
    /// </summary>
    public class ChallanRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DraweeBankId { get; set; }                       // owning bank (R6.1, R6.7)

        public string RuleCode { get; set; } = string.Empty;         // max 50, unique within bank (R6.3, R6.4)
        public string FormatPattern { get; set; } = string.Empty;    // max 500, parseable (R6.3, R6.8)
        public string ValidationExpression { get; set; } = string.Empty; // max 500, parseable (R6.3, R6.8)
        public string RoutingTarget { get; set; } = string.Empty;    // max 200 (R6.3)

        public bool IsActive { get; set; } = true;                   // default true (R6.2)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/DraweeBankAuditEntry.cs`**
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Immutable audit record capturing one create/update/delete on a drawee bank (or a challan rule
    /// under it) (R7). Written within the same unit of work as the change. Never updated or deleted
    /// (R7.6). No FK to DraweeBank so entries survive independently and a missing-id read returns []
    /// without error (R7.5).
    /// </summary>
    public class DraweeBankAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DraweeBankId { get; set; }                   // affected aggregate (R7.1/7.2)
        public DraweeBankAuditAction Action { get; set; }        // Create | Update | Delete (R7.1/7.2)

        public string? BeforeSnapshot { get; set; }              // prior state (JSON); null on Create
        public string? AfterSnapshot { get; set; }               // new state (JSON); null on Delete

        public string ChangedBy { get; set; } = string.Empty;    // acting admin id from JWT (R7.1/7.2)
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC timestamp (R7.1/7.2)
    }
    ```

    **CREATE `Finnova.Models/Domain/Enums/DraweeBankAuditAction.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    /// <summary>Discriminates the mutation an audit entry records (R7.1, R7.2).</summary>
    public enum DraweeBankAuditAction
    {
        Create = 0,
        Update = 1,
        Delete = 2
    }
    ```
    - _Requirements: 1.1, 1.2, 3.1, 3.5, 4.1, 4.4, 6.1, 6.2, 7.1, 7.2_

  - [ ] 1.2 Add the drawee-bank domain exceptions

    **CREATE `Finnova.Models/Domain/Exceptions/DraweeBankNotFoundException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when an update, a challan-rule create, or a challan-rule update targets a drawee bank
    /// that does not exist (R5.4, R6.7). Maps to ERR-DRB-404.
    /// </summary>
    public class DraweeBankNotFoundException : Exception
    {
        public const string ErrorCode = "ERR-DRB-404";

        public DraweeBankNotFoundException(Guid id)
            : base($"Drawee bank '{id}' was not found.")
        {
        }
    }
    ```

    **CREATE `Finnova.Models/Domain/Exceptions/DraweeBankDuplicateCodeException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when a drawee bank is created (or a different bank is updated) with a Bank Code that
    /// already exists (case-insensitive, trimmed) (R2.2, R2.3). Maps to ERR-DRB-409 by type.
    /// </summary>
    public class DraweeBankDuplicateCodeException : Exception
    {
        public const string ErrorCode = "ERR-DRB-409";

        public DraweeBankDuplicateCodeException()
            : base("Bank code must be unique")
        {
        }
    }
    ```

    **CREATE `Finnova.Models/Domain/Exceptions/DraweeBranchDuplicatePlaceCodeException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when two or more submitted branches share a Place Code (trimmed, case-insensitive)
    /// within the same drawee bank (R3.3). Maps to ERR-DRB-409 by type.
    /// </summary>
    public class DraweeBranchDuplicatePlaceCodeException : Exception
    {
        public const string ErrorCode = "ERR-DRB-409";

        public DraweeBranchDuplicatePlaceCodeException()
            : base("Place code must be unique within the bank")
        {
        }
    }
    ```

    **CREATE `Finnova.Models/Domain/Exceptions/ChallanRuleNotFoundException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when a challan-rule update targets a rule id that does not exist (R6.6).
    /// Maps to ERR-DRB-404.
    /// </summary>
    public class ChallanRuleNotFoundException : Exception
    {
        public const string ErrorCode = "ERR-DRB-404";

        public ChallanRuleNotFoundException(Guid id)
            : base($"Challan rule '{id}' was not found.")
        {
        }
    }
    ```

    **CREATE `Finnova.Models/Domain/Exceptions/ChallanRuleDuplicateCodeException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown when a challan-rule create/update uses a Rule Code that already exists under the same
    /// drawee bank (trimmed, case-insensitive) (R6.4). Maps to ERR-DRB-409 by type.
    /// </summary>
    public class ChallanRuleDuplicateCodeException : Exception
    {
        public const string ErrorCode = "ERR-DRB-409";

        public ChallanRuleDuplicateCodeException()
            : base("Rule code must be unique within the bank")
        {
        }
    }
    ```

    **CREATE `Finnova.Models/Domain/Exceptions/DraweeBankValidationException.cs`**
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>
    /// Thrown for drawee-bank domain validation failures surfaced outside FluentValidation
    /// (e.g. an unparseable challan Format Pattern / Validation Expression) (R6.8). Maps to
    /// ERR-DRB-400 by type.
    /// </summary>
    public class DraweeBankValidationException : Exception
    {
        public const string ErrorCode = "ERR-DRB-400";

        public DraweeBankValidationException(string message)
            : base(message)
        {
        }
    }
    ```
    - _Requirements: 2.2, 2.3, 3.3, 5.4, 6.4, 6.6, 6.7, 6.8_

- [ ] 2. Add the contracts (request/response records)
  - [ ] 2.1 Add request contracts

    **CREATE `Finnova.Models/Contracts/DraweeBanks/DraweeBankRequests.cs`**
    ```csharp
    namespace Finnova.Models.Contracts.DraweeBanks;

    // ---- Child submitted inside a create/update ----

    // Dates ISO-8601; EndDate nullable (open-ended branch, R3.5).
    public record DraweeBranchRequest(
        string PlaceCode,
        string PlaceName,
        string Address,
        string PostalCode,
        DateTime StartDate,
        DateTime? EndDate);

    // Optional restriction submitted inside a create/update.
    public record RestrictionDetailRequest(
        int ClearingDays,
        DateTime StartDate,
        DateTime EndDate);

    // ---- Create ----

    // POST /draweebank — IsActive nullable so the service defaults it to true (R1.2).
    // Challan rules are NOT created inline with the bank; they are managed only via their own route.
    public record CreateDraweeBankRequest(
        string BankCode,
        string BankName,
        bool? IsActive,
        IReadOnlyList<DraweeBranchRequest> Branches,       // zero or more (R1.1)
        RestrictionDetailRequest? Restriction);            // optional (R4.4)

    // ---- Update ----

    // PUT /draweebank/{id} — BankCode immutable post-create. Branches/restriction are the full
    // desired set; omitted branches are removed (R3.7), null restriction clears it (R4.4).
    public record UpdateDraweeBankRequest(
        string BankName,
        IReadOnlyList<DraweeBranchRequest> Branches,
        RestrictionDetailRequest? Restriction);

    // ---- Challan rule sub-routes ----

    public record CreateChallanRuleRequest(
        string RuleCode,
        string FormatPattern,
        string ValidationExpression,
        string RoutingTarget,
        bool? IsActive);

    // RuleCode immutable on update (mirrors BankCode); only mutable fields accepted (R6.5).
    public record UpdateChallanRuleRequest(
        string FormatPattern,
        string ValidationExpression,
        string RoutingTarget,
        bool IsActive);
    ```
    - _Requirements: 1.1, 1.2, 3.1, 4.1, 4.4, 5.1, 6.1, 6.5_

  - [ ] 2.2 Add response contracts

    **CREATE `Finnova.Models/Contracts/DraweeBanks/DraweeBankResponses.cs`**
    ```csharp
    namespace Finnova.Models.Contracts.DraweeBanks;

    public record DraweeBranchResponse(
        Guid Id,
        Guid DraweeBankId,
        string PlaceCode,
        string PlaceName,
        string Address,
        string PostalCode,
        DateTime StartDate,
        DateTime? EndDate);

    public record RestrictionDetailResponse(
        Guid Id,
        Guid DraweeBankId,
        int ClearingDays,
        DateTime StartDate,
        DateTime EndDate);

    public record ChallanRuleResponse(
        Guid Id,
        Guid DraweeBankId,
        string RuleCode,
        string FormatPattern,
        string ValidationExpression,
        string RoutingTarget,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    // Admin-facing response (R1.1 returns ids + resolved IsActive). Includes the owned children only.
    // Challan rules are decoupled and NOT part of this response; they are read via GET /{id}/challan-rules.
    public record DraweeBankResponse(
        Guid Id,
        string BankCode,
        string BankName,
        bool IsActive,
        IReadOnlyList<DraweeBranchResponse> Branches,
        RestrictionDetailResponse? Restriction,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    // One audit row (R7.4).
    public record DraweeBankAuditEntryResponse(
        Guid Id,
        Guid DraweeBankId,
        string Action,                 // "Create" | "Update" | "Delete"
        string? BeforeSnapshot,
        string? AfterSnapshot,
        string ChangedBy,
        DateTime ChangedAtUtc);
    ```
    - Reuse the existing `PaginatedResponse<T>` for paged search (do not create a new paged contract).
    - _Requirements: 1.1, 7.1, 7.2, 7.4, 8.4_

- [ ] 3. Configure persistence for the new entities
  - [ ] 3.1 Add the five EF Core entity configurations (auto-discovered by `ApplyConfigurationsFromAssembly`)

    **CREATE `Finnova.Repository/Configuration/DraweeBankConfiguration.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class DraweeBankConfiguration : IEntityTypeConfiguration<DraweeBank>
    {
        public void Configure(EntityTypeBuilder<DraweeBank> builder)
        {
            builder.ToTable("drawee_banks");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.BankCode).IsRequired().HasMaxLength(20);
            builder.Property(x => x.BankName).IsRequired().HasMaxLength(150);
            builder.Property(x => x.IsActive).IsRequired();

            // Case-insensitive uniqueness of BankCode (R2). SQL Server default CI collation backs the
            // index; codes are trimmed by the handler so stored values are canonical.
            builder.HasIndex(x => x.BankCode).IsUnique();
            builder.HasIndex(x => x.BankName);                       // search/order path (R8.10)

            // Owned branches + optional restriction (cascade so update-removal deletes the branch, R3.7).
            builder.HasMany(x => x.Branches)
                   .WithOne()
                   .HasForeignKey(b => b.DraweeBankId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Restriction)
                   .WithOne()
                   .HasForeignKey<RestrictionDetail>(r => r.DraweeBankId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/DraweeBranchConfiguration.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class DraweeBranchConfiguration : IEntityTypeConfiguration<DraweeBranch>
    {
        public void Configure(EntityTypeBuilder<DraweeBranch> builder)
        {
            builder.ToTable("drawee_branches");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PlaceCode).IsRequired().HasMaxLength(20);
            builder.Property(x => x.PlaceName).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.PostalCode).IsRequired().HasMaxLength(6);   // six-digit PIN (R3.6)
            builder.Property(x => x.StartDate).IsRequired();
            builder.Property(x => x.EndDate);                                   // nullable (R3.5)

            // Place Code unique within the owning bank (R3.3).
            builder.HasIndex(x => new { x.DraweeBankId, x.PlaceCode }).IsUnique();
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/RestrictionDetailConfiguration.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class RestrictionDetailConfiguration : IEntityTypeConfiguration<RestrictionDetail>
    {
        public void Configure(EntityTypeBuilder<RestrictionDetail> builder)
        {
            builder.ToTable("restriction_details");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClearingDays).IsRequired();
            builder.Property(x => x.StartDate).IsRequired();
            builder.Property(x => x.EndDate).IsRequired();

            builder.HasIndex(x => x.DraweeBankId).IsUnique();   // at most one restriction per bank
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/ChallanRuleConfiguration.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class ChallanRuleConfiguration : IEntityTypeConfiguration<ChallanRule>
    {
        public void Configure(EntityTypeBuilder<ChallanRule> builder)
        {
            builder.ToTable("challan_rules");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DraweeBankId).IsRequired();
            builder.Property(x => x.RuleCode).IsRequired().HasMaxLength(50);
            builder.Property(x => x.FormatPattern).IsRequired().HasMaxLength(500);
            builder.Property(x => x.ValidationExpression).IsRequired().HasMaxLength(500);
            builder.Property(x => x.RoutingTarget).IsRequired().HasMaxLength(200);
            builder.Property(x => x.IsActive).IsRequired();

            // Rule Code unique within the owning bank (R6.4).
            builder.HasIndex(x => new { x.DraweeBankId, x.RuleCode }).IsUnique();
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/DraweeBankAuditEntryConfiguration.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class DraweeBankAuditEntryConfiguration : IEntityTypeConfiguration<DraweeBankAuditEntry>
    {
        public void Configure(EntityTypeBuilder<DraweeBankAuditEntry> builder)
        {
            builder.ToTable("drawee_bank_audit_entries");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DraweeBankId).IsRequired();
            builder.Property(x => x.Action).IsRequired();            // stored as int
            builder.Property(x => x.BeforeSnapshot);                 // nullable nvarchar(max)
            builder.Property(x => x.AfterSnapshot);                  // nullable nvarchar(max)
            builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
            builder.Property(x => x.ChangedAtUtc).IsRequired();

            // Read path: entries for a bank ordered by time desc then id desc (R7.4).
            builder.HasIndex(x => new { x.DraweeBankId, x.ChangedAtUtc });
            // No FK to DraweeBank so entries survive independently and a missing-id read is [] (R7.5).
        }
    }
    ```
    - _Requirements: 2.1, 3.3, 3.5, 3.6, 3.7, 4.1, 6.4, 7.4, 7.5, 8.10_

  - [ ] 3.2 Register the five DbSets on `FinnovaDbContext`

    **MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`** — add these five properties alongside the existing `DbSet<T>` declarations (e.g. right after `public DbSet<LineOfBusiness> LinesOfBusiness => Set<LineOfBusiness>();`):
    ```csharp
    public DbSet<DraweeBank> DraweeBanks => Set<DraweeBank>();
    public DbSet<DraweeBranch> DraweeBranches => Set<DraweeBranch>();
    public DbSet<RestrictionDetail> RestrictionDetails => Set<RestrictionDetail>();
    public DbSet<ChallanRule> ChallanRules => Set<ChallanRule>();
    public DbSet<DraweeBankAuditEntry> DraweeBankAuditEntries => Set<DraweeBankAuditEntry>();
    ```
    - _Requirements: 1.1, 3.1, 4.1, 6.1, 7.1_

- [ ] 4. Add the EF Core migration for the new tables
  - [ ] 4.1 Generate the `AddDraweeBankChallanRulesAndAudit` migration
    - Run (from the solution root):
    ```
    dotnet ef migrations add AddDraweeBankChallanRulesAndAudit --project Finnova.Repository --startup-project Finnova.SystemAdminService
    ```
    - Verify the generated migration creates all five tables (`drawee_banks`, `drawee_branches`, `restriction_details`, `challan_rules`, `drawee_bank_audit_entries`), the unique indexes on `BankCode`, `(DraweeBankId, PlaceCode)`, `(DraweeBankId, RuleCode)`, and `RestrictionDetail.DraweeBankId`, and the `(DraweeBankId, ChangedAtUtc)` audit index — matching task 3.1. No seed data is required (any smoke seed must be India-only / English-only).
    - _Requirements: 1.1, 2.1, 3.3, 4.1, 6.4, 7.1_

- [ ] 5. Implement the repository layer
  - [ ] 5.1 Define the repository interfaces

    **CREATE `Finnova.Repository/Interfaces/IDraweeBankRepository.cs`**
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IDraweeBankRepository : IRepository<DraweeBank>
    {
        /// <summary>Search + page (R8). Term filters BankCode OR BankName (substring, case-insensitive);
        /// null/blank term = no filter. Ordered by BankName asc then BankCode asc (R8.10). Returns page
        /// + total. Includes owned branches/restriction for the page items (challan rules are not loaded).</summary>
        Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
            string? searchTerm, int page, int pageSize, CancellationToken ct = default);

        /// <summary>Load the bank aggregate (bank + branches + restriction) by id, or null if absent
        /// (R5, R6.7). Challan rules are not part of the aggregate and are not loaded here.</summary>
        Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Case-insensitive, trimmed uniqueness check on BankCode (R2). excludeId supports
        /// self-exclusion on update (R2.4).</summary>
        Task<bool> ExistsByBankCodeAsync(string bankCode, Guid? excludeId = null, CancellationToken ct = default);

        /// <summary>Standalone challan-rule helpers (R6). List rules for a bank, load/insert/update a
        /// rule, and check per-bank rule-code uniqueness (trimmed, case-insensitive; excludeId for
        /// self-exclusion on update). Rules are accessed only through these helpers, never with the bank.</summary>
        Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid draweeBankId, CancellationToken ct = default);
        Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default);
        Task<bool> ChallanRuleCodeExistsAsync(Guid draweeBankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default);
        Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default);
        Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default);
    }
    ```

    **CREATE `Finnova.Repository/Interfaces/IDraweeBankAuditRepository.cs`**
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IDraweeBankAuditRepository : IRepository<DraweeBankAuditEntry>
    {
        /// <summary>Audit entries for a drawee bank, ordered ChangedAtUtc desc then Id desc (R7.4).
        /// Missing id returns an empty list, never an error (R7.5).</summary>
        Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(Guid draweeBankId, CancellationToken ct = default);
    }
    ```
    - _Requirements: 2.1, 2.4, 5.1, 6.4, 6.7, 7.4, 7.5, 8.1, 8.10_

  - [ ] 5.2 Implement `DraweeBankRepository`

    **CREATE `Finnova.Repository/Repositories/DraweeBankRepository.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class DraweeBankRepository : RepositoryBase<DraweeBank>, IDraweeBankRepository
    {
        public DraweeBankRepository(FinnovaDbContext context) : base(context) { }

        public async Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
            string? searchTerm, int page, int pageSize, CancellationToken ct = default)
        {
            var query = DbSet
                .AsNoTracking()
                .Include(x => x.Branches)
                .Include(x => x.Restriction)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                // EF Core translates Contains to SQL LIKE; default CI collation => case-insensitive (R8.1).
                query = query.Where(x => x.BankCode.Contains(term) || x.BankName.Contains(term));
            }

            var total = await query.CountAsync(ct);                       // total before paging (R8.4)

            var items = await query
                .OrderBy(x => x.BankName).ThenBy(x => x.BankCode)         // deterministic order (R8.10)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);

            // Challan rules are decoupled from the aggregate — not loaded or attached to page items.
            return (items, total);
        }

        public async Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default)
        {
            // Bank aggregate = bank + owned branches + optional restriction. Challan rules are NOT
            // part of the aggregate and are read separately via GetChallanRulesByBankIdAsync.
            return await DbSet
                .Include(x => x.Branches)
                .Include(x => x.Restriction)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<bool> ExistsByBankCodeAsync(
            string bankCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = bankCode.Trim();
            return await DbSet.AsNoTracking().AnyAsync(
                x => x.BankCode == c && (excludeId == null || x.Id != excludeId), ct);   // CI via collation (R2.1)
        }

        public async Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid draweeBankId, CancellationToken ct = default)
            => await Context.Set<ChallanRule>().AsNoTracking()
                .Where(r => r.DraweeBankId == draweeBankId)
                .OrderBy(r => r.RuleCode)                 // deterministic order for the read endpoint
                .ToListAsync(ct);

        public async Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
            => await Context.Set<ChallanRule>().FirstOrDefaultAsync(r => r.Id == ruleId, ct);

        public async Task<bool> ChallanRuleCodeExistsAsync(
            Guid draweeBankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var code = ruleCode.Trim();
            return await Context.Set<ChallanRule>().AsNoTracking().AnyAsync(
                r => r.DraweeBankId == draweeBankId && r.RuleCode == code
                     && (excludeId == null || r.Id != excludeId), ct);   // CI via collation (R6.4)
        }

        public async Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
        {
            await Context.Set<ChallanRule>().AddAsync(rule, ct);
            await Context.SaveChangesAsync(ct);
        }

        public async Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
        {
            Context.Set<ChallanRule>().Update(rule);
            await Context.SaveChangesAsync(ct);
        }
    }
    ```
    - _Requirements: 2.1, 2.4, 5.1, 6.4, 6.7, 8.1, 8.2, 8.3, 8.4, 8.9, 8.10, 8.11_

  - [ ] 5.3 Implement `DraweeBankAuditRepository`

    **CREATE `Finnova.Repository/Repositories/DraweeBankAuditRepository.cs`**
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class DraweeBankAuditRepository : RepositoryBase<DraweeBankAuditEntry>, IDraweeBankAuditRepository
    {
        public DraweeBankAuditRepository(FinnovaDbContext context) : base(context) { }

        public async Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(
            Guid draweeBankId, CancellationToken ct = default)
        {
            return await DbSet.AsNoTracking()
                .Where(x => x.DraweeBankId == draweeBankId)
                .OrderByDescending(x => x.ChangedAtUtc)   // newest first (R7.4)
                .ThenByDescending(x => x.Id)              // deterministic tie-break (R7.4)
                .ToListAsync(ct);                         // empty list when none match (R7.5)
        }
    }
    ```
    - _Requirements: 7.4, 7.5_

  - [ ] 5.4 Register both repositories in DI

    **MODIFY `Finnova.Repository/DependencyInjection.cs`** — add these two lines inside `AddFinnovaRepository`, alongside the existing `AddScoped<...>` registrations (e.g. after the `IUserManagementRepository` line, before `return services;`):
    ```csharp
    services.AddScoped<IDraweeBankRepository, DraweeBankRepository>();
    services.AddScoped<IDraweeBankAuditRepository, DraweeBankAuditRepository>();
    ```
    - _Requirements: 1.1, 7.1_

- [ ] 6. Implement the `.ToResponse()` mappers and the challan-expression helper
  - [ ] 6.1 Add `DraweeBankMapper`

    **CREATE `Finnova.Service/Mappers/DraweeBankMapper.cs`**
    ```csharp
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.Mappers;

    public static class DraweeBankMapper
    {
        public static DraweeBranchResponse ToResponse(this DraweeBranch b) =>
            new(b.Id, b.DraweeBankId, b.PlaceCode, b.PlaceName, b.Address, b.PostalCode, b.StartDate, b.EndDate);

        public static RestrictionDetailResponse ToResponse(this RestrictionDetail r) =>
            new(r.Id, r.DraweeBankId, r.ClearingDays, r.StartDate, r.EndDate);

        // Standalone rule mapper used by the challan-rule endpoints (read/create/update).
        public static ChallanRuleResponse ToResponse(this ChallanRule c) =>
            new(c.Id, c.DraweeBankId, c.RuleCode, c.FormatPattern, c.ValidationExpression, c.RoutingTarget,
                c.IsActive, c.CreatedAt, c.UpdatedAt);

        // Bank response carries owned children only — challan rules are decoupled and never mapped here.
        public static DraweeBankResponse ToResponse(this DraweeBank x) =>
            new(x.Id, x.BankCode, x.BankName, x.IsActive,
                x.Branches.Select(b => b.ToResponse()).ToList(),
                x.Restriction?.ToResponse(),
                x.CreatedAt, x.UpdatedAt);

        public static List<DraweeBankResponse> ToResponseList(this IEnumerable<DraweeBank> items)
            => items.Select(i => i.ToResponse()).ToList();

        public static DraweeBankAuditEntryResponse ToResponse(this DraweeBankAuditEntry a) =>
            new(a.Id, a.DraweeBankId, a.Action.ToString(), a.BeforeSnapshot, a.AfterSnapshot,
                a.ChangedBy, a.ChangedAtUtc);
    }
    ```
    - _Requirements: 1.1, 7.1, 7.2, 7.4_

  - [ ] 6.2 Add the challan-expression parse helper (placeholder grammar, R6.8)

    **CREATE `Finnova.Service/DraweeBank/ChallanExpression.cs`**
    ```csharp
    namespace Finnova.Service.DraweeBank;

    /// <summary>
    /// Placeholder parser for challan Format Pattern / Validation Expression (R6.8). The ticket does
    /// not define a grammar; until one is agreed this enforces a minimal "parseable" rule: non-blank,
    /// within the length bound, and balanced brackets/parentheses. Centralised so the real grammar can
    /// replace this one method without touching the validators.
    /// </summary>
    public static class ChallanExpression
    {
        public static bool IsParseable(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;

            var depthParen = 0;
            var depthBracket = 0;
            foreach (var ch in expression)
            {
                switch (ch)
                {
                    case '(': depthParen++; break;
                    case ')': if (--depthParen < 0) return false; break;
                    case '[': depthBracket++; break;
                    case ']': if (--depthBracket < 0) return false; break;
                }
            }
            return depthParen == 0 && depthBracket == 0;
        }
    }
    ```
    - _Requirements: 6.8_

- [ ] 7. Implement the CQRS service slice
  - [ ] 7.1 Add the shared child-request validators (branch / restriction / rule)

    **CREATE `Finnova.Service/DraweeBank/Validators/DraweeBranchRequestValidator.cs`**
    ```csharp
    using FluentValidation;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Validators;

    /// <summary>Validates a single submitted branch (R3.2 required, R3.6 six-digit PIN,
    /// R3.4 EndDate >= StartDate when present).</summary>
    public class DraweeBranchRequestValidator : AbstractValidator<DraweeBranchRequest>
    {
        public DraweeBranchRequestValidator()
        {
            RuleFor(x => x.PlaceCode)
                .NotEmpty().WithMessage("Place Code is required.")
                .MaximumLength(20).WithMessage("Place Code must not exceed 20 characters.");

            RuleFor(x => x.PlaceName)
                .NotEmpty().WithMessage("Place Name is required.")
                .MaximumLength(150).WithMessage("Place Name must not exceed 150 characters.");

            RuleFor(x => x.Address)
                .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

            // Six-digit Indian PIN (R3.6).
            RuleFor(x => x.PostalCode)
                .Matches(@"^\d{6}$").WithMessage("Postal Code must be a six-digit PIN.");

            // EndDate on/after StartDate when present (R3.4). Null EndDate = open-ended (R3.5).
            RuleFor(x => x)
                .Must(b => b.EndDate == null || b.EndDate.Value >= b.StartDate)
                .WithMessage("Branch End Date must be on or after Start Date.");
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Validators/RestrictionDetailRequestValidator.cs`**
    ```csharp
    using FluentValidation;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Validators;

    /// <summary>Validates a submitted restriction (R4.2 ClearingDays >= 0, R4.5 <= 365,
    /// R4.3 EndDate >= StartDate).</summary>
    public class RestrictionDetailRequestValidator : AbstractValidator<RestrictionDetailRequest>
    {
        public RestrictionDetailRequestValidator()
        {
            RuleFor(x => x.ClearingDays)
                .InclusiveBetween(0, 365).WithMessage("Clearing Days must be between 0 and 365.");

            RuleFor(x => x)
                .Must(r => r.EndDate >= r.StartDate)
                .WithMessage("Restriction End Date must be on or after Start Date.");
        }
    }
    ```

    > **Note:** No inline `ChallanRuleRequestValidator` is needed. Challan rules are decoupled from the
    > bank aggregate and are never submitted inline on create — they are validated by the standalone
    > `CreateChallanRuleCommandValidator` / `UpdateChallanRuleCommandValidator` (tasks 7.16/7.17).
    - _Requirements: 3.2, 3.4, 3.5, 3.6, 4.2, 4.3, 4.5_

  - [ ] 7.2 Add the shared aggregate-assembly helpers (snapshot + place-code uniqueness)

    **CREATE `Finnova.Service/DraweeBank/DraweeBankSnapshot.cs`**
    ```csharp
    using System.Text.Json;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.DraweeBank;

    /// <summary>
    /// Serialises the relevant state of a drawee bank aggregate (or a challan rule) to a compact JSON
    /// projection stored in the audit before/after snapshots (R7.1, R7.2). One entry per
    /// create/modify of the aggregate (design decision 8).
    /// </summary>
    public static class DraweeBankSnapshot
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

        public static string Of(DraweeBank bank) => JsonSerializer.Serialize(new
        {
            bank.Id,
            bank.BankCode,
            bank.BankName,
            bank.IsActive,
            Branches = bank.Branches.Select(b => new
            {
                b.PlaceCode, b.PlaceName, b.Address, b.PostalCode, b.StartDate, b.EndDate
            }),
            Restriction = bank.Restriction == null ? null : new
            {
                bank.Restriction.ClearingDays, bank.Restriction.StartDate, bank.Restriction.EndDate
            }
        }, Options);

        public static string Of(ChallanRule rule) => JsonSerializer.Serialize(new
        {
            rule.Id, rule.DraweeBankId, rule.RuleCode, rule.FormatPattern,
            rule.ValidationExpression, rule.RoutingTarget, rule.IsActive
        }, Options);
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/DraweeBankAssembler.cs`**
    ```csharp
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;

    namespace Finnova.Service.DraweeBank;

    /// <summary>
    /// Shared aggregate helpers used by the create and update handlers: in-collection place-code
    /// uniqueness (R3.3) and mapping branch/restriction requests to owned entities.
    /// </summary>
    public static class DraweeBankAssembler
    {
        /// <summary>Rejects a submitted branch set that contains two place codes equal after trimming
        /// and case-insensitive comparison (R3.3).</summary>
        public static void EnsureUniquePlaceCodes(IReadOnlyList<DraweeBranchRequest> branches)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in branches)
            {
                if (!seen.Add(b.PlaceCode.Trim()))
                    throw new DraweeBranchDuplicatePlaceCodeException();
            }
        }

        public static List<DraweeBranch> ToBranchEntities(Guid bankId, IReadOnlyList<DraweeBranchRequest> branches)
            => branches.Select(b => new DraweeBranch
            {
                DraweeBankId = bankId,
                PlaceCode = b.PlaceCode.Trim(),
                PlaceName = b.PlaceName,
                Address = b.Address,
                PostalCode = b.PostalCode,
                StartDate = b.StartDate,
                EndDate = b.EndDate
            }).ToList();

        public static RestrictionDetail? ToRestrictionEntity(Guid bankId, RestrictionDetailRequest? r)
            => r == null ? null : new RestrictionDetail
            {
                DraweeBankId = bankId,
                ClearingDays = r.ClearingDays,
                StartDate = r.StartDate,
                EndDate = r.EndDate
            };
    }
    ```
    - _Requirements: 3.3, 4.4, 7.1, 7.2_

  - [ ] 7.3 Implement `CreateDraweeBankCommand`, validator, and handler

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateDraweeBank/CreateDraweeBankCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

    public record CreateDraweeBankCommand(
        string BankCode,
        string BankName,
        bool? IsActive,
        IReadOnlyList<DraweeBranchRequest> Branches,
        RestrictionDetailRequest? Restriction,
        string ActingAdmin
    ) : IRequest<DraweeBankResponse>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateDraweeBank/CreateDraweeBankCommandValidator.cs`**
    ```csharp
    using FluentValidation;
    using Finnova.Service.DraweeBank.Validators;

    namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

    public class CreateDraweeBankCommandValidator : AbstractValidator<CreateDraweeBankCommand>
    {
        public CreateDraweeBankCommandValidator()
        {
            // R1.3, R2.5 — BankCode required; R1.4, R2.5 — max 20.
            RuleFor(x => x.BankCode)
                .NotEmpty().WithMessage("Bank Code is required.")
                .MaximumLength(20).WithMessage("Bank Code must not exceed 20 characters.");

            // R1.3 — BankName required; R1.4 — max 150.
            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage("Bank Name is required.")
                .MaximumLength(150).WithMessage("Bank Name must not exceed 150 characters.");

            // Each branch / restriction validated per its own rules (R3, R4). No challan rules are
            // accepted inline — they are managed through their own route.
            RuleForEach(x => x.Branches).SetValidator(new DraweeBranchRequestValidator());
            When(x => x.Restriction != null, () =>
                RuleFor(x => x.Restriction!).SetValidator(new RestrictionDetailRequestValidator()));
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateDraweeBank/CreateDraweeBankCommandHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

    public class CreateDraweeBankCommandHandler : IRequestHandler<CreateDraweeBankCommand, DraweeBankResponse>
    {
        private readonly IDraweeBankRepository _repository;
        private readonly IDraweeBankAuditRepository _auditRepository;

        public CreateDraweeBankCommandHandler(
            IDraweeBankRepository repository,
            IDraweeBankAuditRepository auditRepository)
        {
            _repository = repository;
            _auditRepository = auditRepository;
        }

        public async Task<DraweeBankResponse> Handle(CreateDraweeBankCommand request, CancellationToken ct)
        {
            var code = request.BankCode.Trim();

            // R3.3 — reject duplicate place codes within the submitted set before any persistence.
            DraweeBankAssembler.EnsureUniquePlaceCodes(request.Branches);

            // R2.1/R2.2 — case-insensitive, trimmed uniqueness. Rejecting here persists nothing
            // and writes no audit entry (R7.3).
            if (await _repository.ExistsByBankCodeAsync(code, null, ct))
                throw new DraweeBankDuplicateCodeException();

            var bank = new DraweeBank
            {
                BankCode = code,
                BankName = request.BankName,
                IsActive = request.IsActive ?? true,          // R1.2 default true
            };
            bank.Branches = DraweeBankAssembler.ToBranchEntities(bank.Id, request.Branches);   // R3.1
            bank.Restriction = DraweeBankAssembler.ToRestrictionEntity(bank.Id, request.Restriction); // R4.1/R4.4

            // R1.1/R1.7 — persist the whole aggregate in one unit of work (all-or-nothing).
            // Challan rules are decoupled: they are NOT created here, only later via CreateChallanRuleCommand.
            await _repository.AddAsync(bank, ct);

            // R7.2 — exactly one Create audit entry; BeforeSnapshot null, AfterSnapshot = created state.
            await _auditRepository.AddAsync(new DraweeBankAuditEntry
            {
                DraweeBankId = bank.Id,
                Action = DraweeBankAuditAction.Create,
                BeforeSnapshot = null,
                AfterSnapshot = DraweeBankSnapshot.Of(bank),
                ChangedBy = request.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return bank.ToResponse();
        }
    }
    ```
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.7, 1.8, 2.1, 2.2, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 7.2_

  - [ ]* 7.4 Write property test: create aggregate round trip
    - **Property 1: Create aggregate round trip**
    - **Validates: Requirements 1.1, 1.6, 1.7, 3.1, 4.1**

  - [ ]* 7.5 Write property test: bank code uniqueness on create and update
    - **Property 2: Bank code uniqueness on create and update**
    - **Validates: Requirements 1.5, 2.1, 2.2, 2.3, 2.4**

  - [ ]* 7.6 Write property test: create defaults IsActive to true
    - **Property 3: Create defaults IsActive to true**
    - **Validates: Requirements 1.2, 6.2**

  - [ ]* 7.7 Write property test: branch place-code uniqueness within a bank
    - **Property 4: Branch place-code uniqueness within a bank**
    - **Validates: Requirements 3.3**

  - [ ]* 7.8 Write property test: branch date-range and PIN validity
    - **Property 5: Branch date-range and PIN validity**
    - **Validates: Requirements 3.4, 3.5, 3.6**

  - [ ]* 7.9 Write property test: restriction clearing-days and date-range bounds
    - **Property 6: Restriction clearing-days and date-range bounds**
    - **Validates: Requirements 4.2, 4.3, 4.4, 4.5**

  - [ ]* 7.10 Write property test: audit entry written on create
    - **Property 11: Audit entry written on create and update** (create arm)
    - **Validates: Requirements 7.1, 7.2**

  - [ ] 7.11 Implement `UpdateDraweeBankCommand`, validator, and handler

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateDraweeBank/UpdateDraweeBankCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

    /// <summary>Updates a drawee bank's Name, branch set, and restriction (BankCode immutable).
    /// Branches/restriction are the full desired set; omitted branches are removed (R3.7).</summary>
    public record UpdateDraweeBankCommand(
        Guid Id,
        string BankName,
        IReadOnlyList<DraweeBranchRequest> Branches,
        RestrictionDetailRequest? Restriction,
        string ActingAdmin
    ) : IRequest<DraweeBankResponse>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateDraweeBank/UpdateDraweeBankCommandValidator.cs`**
    ```csharp
    using FluentValidation;
    using Finnova.Service.DraweeBank.Validators;

    namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

    public class UpdateDraweeBankCommandValidator : AbstractValidator<UpdateDraweeBankCommand>
    {
        public UpdateDraweeBankCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();

            // R5.2 — BankName required; R5.3 — max 150.
            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage("Bank Name is required.")
                .MaximumLength(150).WithMessage("Bank Name must not exceed 150 characters.");

            RuleForEach(x => x.Branches).SetValidator(new DraweeBranchRequestValidator());
            When(x => x.Restriction != null, () =>
                RuleFor(x => x.Restriction!).SetValidator(new RestrictionDetailRequestValidator()));
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateDraweeBank/UpdateDraweeBankCommandHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

    public class UpdateDraweeBankCommandHandler : IRequestHandler<UpdateDraweeBankCommand, DraweeBankResponse>
    {
        private readonly IDraweeBankRepository _repository;
        private readonly IDraweeBankAuditRepository _auditRepository;

        public UpdateDraweeBankCommandHandler(
            IDraweeBankRepository repository,
            IDraweeBankAuditRepository auditRepository)
        {
            _repository = repository;
            _auditRepository = auditRepository;
        }

        public async Task<DraweeBankResponse> Handle(UpdateDraweeBankCommand request, CancellationToken ct)
        {
            // R3.3 — reject duplicate place codes before touching the store.
            DraweeBankAssembler.EnsureUniquePlaceCodes(request.Branches);

            // R5.4 — unknown id -> not found; nothing changed, no audit.
            var bank = await _repository.GetAggregateByIdAsync(request.Id, ct)
                ?? throw new DraweeBankNotFoundException(request.Id);

            var before = DraweeBankSnapshot.Of(bank);

            // R5.5 — if the submitted name, branch set, and restriction all equal the current values,
            // treat the update as a successful no-op with no audit entry (R7.3).
            var desired = BuildDesired(request);
            var after = DraweeBankSnapshot.Of(desired);
            if (string.Equals(before, after, StringComparison.Ordinal))
                return bank.ToResponse();

            // Apply the diff: new name, replace branch set (removal cascades, R3.7), set/clear restriction.
            bank.BankName = request.BankName;
            bank.Branches = DraweeBankAssembler.ToBranchEntities(bank.Id, request.Branches);
            bank.Restriction = DraweeBankAssembler.ToRestrictionEntity(bank.Id, request.Restriction);
            bank.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(bank, ct);

            // R7.1 — exactly one Update audit entry with before/after snapshots.
            await _auditRepository.AddAsync(new DraweeBankAuditEntry
            {
                DraweeBankId = bank.Id,
                Action = DraweeBankAuditAction.Update,
                BeforeSnapshot = before,
                AfterSnapshot = DraweeBankSnapshot.Of(bank),
                ChangedBy = request.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return bank.ToResponse();
        }

        // Projects the submitted request into a transient aggregate for the no-op snapshot compare.
        private static DraweeBank BuildDesired(UpdateDraweeBankCommand request)
        {
            var tmp = new DraweeBank { BankName = request.BankName };
            tmp.Branches = DraweeBankAssembler.ToBranchEntities(Guid.Empty, request.Branches);
            tmp.Restriction = DraweeBankAssembler.ToRestrictionEntity(Guid.Empty, request.Restriction);
            return tmp;
        }
    }
    ```
    - Note: the no-op compare normalises on branch/restriction values (not ids/dates generated on save); the snapshot projects only the business fields, so a semantically identical submission is detected as a no-op (R5.5).
    - _Requirements: 3.3, 3.7, 5.1, 5.2, 5.3, 5.4, 5.5, 7.1, 7.3_

  - [ ]* 7.12 Write property test: update aggregate round trip
    - **Property 7: Update aggregate round trip**
    - **Validates: Requirements 3.7, 5.1**

  - [ ]* 7.13 Write property test: same-state update is a no-op with no audit
    - **Property 8: Same-state update is a no-op with no audit**
    - **Validates: Requirements 5.5, 7.3**

  - [ ]* 7.14 Write property test: audit entry written on update
    - **Property 11: Audit entry written on create and update** (update arm)
    - **Validates: Requirements 7.1, 7.2**

  - [ ]* 7.15 Write property test: no audit entry on rejection
    - **Property 12: No audit entry on rejection**
    - **Validates: Requirements 7.3**

  - [ ] 7.16 Implement `CreateChallanRuleCommand`, validator, and handler

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateChallanRule/CreateChallanRuleCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

    public record CreateChallanRuleCommand(
        Guid DraweeBankId,
        string RuleCode,
        string FormatPattern,
        string ValidationExpression,
        string RoutingTarget,
        bool? IsActive,
        string ActingAdmin
    ) : IRequest<ChallanRuleResponse>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateChallanRule/CreateChallanRuleCommandValidator.cs`**
    ```csharp
    using FluentValidation;

    namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

    public class CreateChallanRuleCommandValidator : AbstractValidator<CreateChallanRuleCommand>
    {
        public CreateChallanRuleCommandValidator()
        {
            RuleFor(x => x.DraweeBankId).NotEmpty();

            RuleFor(x => x.RuleCode)
                .NotEmpty().WithMessage("Rule Code is required.")
                .MaximumLength(50).WithMessage("Rule Code must not exceed 50 characters.");

            RuleFor(x => x.FormatPattern)
                .NotEmpty().WithMessage("Format Pattern is required.")
                .MaximumLength(500).WithMessage("Format Pattern must not exceed 500 characters.")
                .Must(ChallanExpression.IsParseable).WithMessage("Format Pattern is not a valid expression.");

            RuleFor(x => x.ValidationExpression)
                .NotEmpty().WithMessage("Validation Expression is required.")
                .MaximumLength(500).WithMessage("Validation Expression must not exceed 500 characters.")
                .Must(ChallanExpression.IsParseable).WithMessage("Validation Expression is not a valid expression.");

            RuleFor(x => x.RoutingTarget)
                .NotEmpty().WithMessage("Routing Target is required.")
                .MaximumLength(200).WithMessage("Routing Target must not exceed 200 characters.");
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/CreateChallanRule/CreateChallanRuleCommandHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

    public class CreateChallanRuleCommandHandler : IRequestHandler<CreateChallanRuleCommand, ChallanRuleResponse>
    {
        private readonly IDraweeBankRepository _repository;
        private readonly IDraweeBankAuditRepository _auditRepository;

        public CreateChallanRuleCommandHandler(
            IDraweeBankRepository repository,
            IDraweeBankAuditRepository auditRepository)
        {
            _repository = repository;
            _auditRepository = auditRepository;
        }

        public async Task<ChallanRuleResponse> Handle(CreateChallanRuleCommand request, CancellationToken ct)
        {
            // R6.7 — the owning bank must exist.
            var bank = await _repository.GetByIdAsync(request.DraweeBankId, ct)
                ?? throw new DraweeBankNotFoundException(request.DraweeBankId);

            var code = request.RuleCode.Trim();

            // R6.4 — per-bank rule-code uniqueness (trimmed, case-insensitive).
            if (await _repository.ChallanRuleCodeExistsAsync(bank.Id, code, null, ct))
                throw new ChallanRuleDuplicateCodeException();

            var rule = new ChallanRule
            {
                DraweeBankId = bank.Id,
                RuleCode = code,
                FormatPattern = request.FormatPattern,
                ValidationExpression = request.ValidationExpression,
                RoutingTarget = request.RoutingTarget,
                IsActive = request.IsActive ?? true,     // R6.2 default true
            };
            await _repository.AddChallanRuleAsync(rule, ct);   // R6.1

            // R7.2 — Create audit entry on the owning bank.
            await _auditRepository.AddAsync(new DraweeBankAuditEntry
            {
                DraweeBankId = bank.Id,
                Action = DraweeBankAuditAction.Create,
                BeforeSnapshot = null,
                AfterSnapshot = DraweeBankSnapshot.Of(rule),
                ChangedBy = request.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return rule.ToResponse();
        }
    }
    ```
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.7, 6.8, 7.2_

  - [ ] 7.17 Implement `UpdateChallanRuleCommand`, validator, and handler

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateChallanRule/UpdateChallanRuleCommand.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

    /// <summary>Updates a challan rule's mutable fields (RuleCode immutable). ActingAdmin from JWT.</summary>
    public record UpdateChallanRuleCommand(
        Guid DraweeBankId,
        Guid RuleId,
        string FormatPattern,
        string ValidationExpression,
        string RoutingTarget,
        bool IsActive,
        string ActingAdmin
    ) : IRequest<ChallanRuleResponse>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateChallanRule/UpdateChallanRuleCommandValidator.cs`**
    ```csharp
    using FluentValidation;

    namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

    public class UpdateChallanRuleCommandValidator : AbstractValidator<UpdateChallanRuleCommand>
    {
        public UpdateChallanRuleCommandValidator()
        {
            RuleFor(x => x.DraweeBankId).NotEmpty();
            RuleFor(x => x.RuleId).NotEmpty();

            RuleFor(x => x.FormatPattern)
                .NotEmpty().WithMessage("Format Pattern is required.")
                .MaximumLength(500).WithMessage("Format Pattern must not exceed 500 characters.")
                .Must(ChallanExpression.IsParseable).WithMessage("Format Pattern is not a valid expression.");

            RuleFor(x => x.ValidationExpression)
                .NotEmpty().WithMessage("Validation Expression is required.")
                .MaximumLength(500).WithMessage("Validation Expression must not exceed 500 characters.")
                .Must(ChallanExpression.IsParseable).WithMessage("Validation Expression is not a valid expression.");

            RuleFor(x => x.RoutingTarget)
                .NotEmpty().WithMessage("Routing Target is required.")
                .MaximumLength(200).WithMessage("Routing Target must not exceed 200 characters.");
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Commands/UpdateChallanRule/UpdateChallanRuleCommandHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

    public class UpdateChallanRuleCommandHandler : IRequestHandler<UpdateChallanRuleCommand, ChallanRuleResponse>
    {
        private readonly IDraweeBankRepository _repository;
        private readonly IDraweeBankAuditRepository _auditRepository;

        public UpdateChallanRuleCommandHandler(
            IDraweeBankRepository repository,
            IDraweeBankAuditRepository auditRepository)
        {
            _repository = repository;
            _auditRepository = auditRepository;
        }

        public async Task<ChallanRuleResponse> Handle(UpdateChallanRuleCommand request, CancellationToken ct)
        {
            // R6.6 — unknown rule id -> not found; nothing changed.
            var rule = await _repository.GetChallanRuleByIdAsync(request.RuleId, ct)
                ?? throw new ChallanRuleNotFoundException(request.RuleId);

            var before = DraweeBankSnapshot.Of(rule);

            // R6.5 — apply mutable fields and refresh the timestamp.
            rule.FormatPattern = request.FormatPattern;
            rule.ValidationExpression = request.ValidationExpression;
            rule.RoutingTarget = request.RoutingTarget;
            rule.IsActive = request.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateChallanRuleAsync(rule, ct);

            // R7.1 — Update audit entry on the owning bank.
            await _auditRepository.AddAsync(new DraweeBankAuditEntry
            {
                DraweeBankId = rule.DraweeBankId,
                Action = DraweeBankAuditAction.Update,
                BeforeSnapshot = before,
                AfterSnapshot = DraweeBankSnapshot.Of(rule),
                ChangedBy = request.ActingAdmin,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);

            return rule.ToResponse();
        }
    }
    ```
    - _Requirements: 6.3, 6.5, 6.6, 6.8, 7.1_

  - [ ]* 7.18 Write property test: challan rule code uniqueness within a bank
    - **Property 9: Challan rule code uniqueness within a bank**
    - **Validates: Requirements 6.4**

  - [ ]* 7.19 Write property test: challan rule field validity
    - **Property 10: Challan rule field validity**
    - **Validates: Requirements 6.3, 6.8**

  - [ ] 7.20 Implement `GetDraweeBanksPagedQuery`, validator, and handler

    **CREATE `Finnova.Service/DraweeBank/Queries/GetDraweeBanksPaged/GetDraweeBanksPagedQuery.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

    /// <summary>Paged, filtered admin listing (R8). Term filters BankCode OR BankName (substring,
    /// case-insensitive); blank term = no filter (R8.1, R8.2). Defaults page 1, size 20 (R8.5, R8.6).</summary>
    public record GetDraweeBanksPagedQuery(
        string? SearchTerm,
        int Page = 1,
        int PageSize = 20
    ) : IRequest<PaginatedResponse<DraweeBankResponse>>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Queries/GetDraweeBanksPaged/GetDraweeBanksPagedQueryValidator.cs`**
    ```csharp
    using FluentValidation;

    namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

    /// <summary>Validates paging bounds (R8.7, R8.8). A blank/whitespace search term is left
    /// unconstrained and treated as "no filter" by the repository (R8.2).</summary>
    public class GetDraweeBanksPagedQueryValidator : AbstractValidator<GetDraweeBanksPagedQuery>
    {
        public GetDraweeBanksPagedQueryValidator()
        {
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }
    ```

    **CREATE `Finnova.Service/DraweeBank/Queries/GetDraweeBanksPaged/GetDraweeBanksPagedQueryHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

    public class GetDraweeBanksPagedQueryHandler
        : IRequestHandler<GetDraweeBanksPagedQuery, PaginatedResponse<DraweeBankResponse>>
    {
        private readonly IDraweeBankRepository _repository;

        public GetDraweeBanksPagedQueryHandler(IDraweeBankRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaginatedResponse<DraweeBankResponse>> Handle(
            GetDraweeBanksPagedQuery request, CancellationToken ct)
        {
            var (items, total) = await _repository.GetPagedAsync(
                request.SearchTerm, request.Page, request.PageSize, ct);   // R8.1-R8.3, R8.10

            var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);   // R8.9

            return new PaginatedResponse<DraweeBankResponse>(
                items.ToResponseList(), total, request.Page, request.PageSize, totalPages);   // R8.4, R8.11
        }
    }
    ```
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11_

  - [ ] 7.21 Implement `GetDraweeBankAuditTrailQuery` and handler

    **CREATE `Finnova.Service/DraweeBank/Queries/GetDraweeBankAuditTrail/GetDraweeBankAuditTrailQuery.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;

    /// <summary>Reads the full audit trail for one drawee bank, newest first (R7.4). A missing id
    /// yields an empty list rather than an error (R7.5).</summary>
    public record GetDraweeBankAuditTrailQuery(
        Guid DraweeBankId
    ) : IRequest<List<DraweeBankAuditEntryResponse>>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Queries/GetDraweeBankAuditTrail/GetDraweeBankAuditTrailQueryHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;

    public class GetDraweeBankAuditTrailQueryHandler
        : IRequestHandler<GetDraweeBankAuditTrailQuery, List<DraweeBankAuditEntryResponse>>
    {
        private readonly IDraweeBankAuditRepository _auditRepository;

        public GetDraweeBankAuditTrailQueryHandler(IDraweeBankAuditRepository auditRepository)
        {
            _auditRepository = auditRepository;
        }

        public async Task<List<DraweeBankAuditEntryResponse>> Handle(
            GetDraweeBankAuditTrailQuery request, CancellationToken ct)
        {
            // Ordered newest-first; empty list for an unknown id, never an error (R7.4, R7.5).
            var entries = await _auditRepository.GetByDraweeBankIdAsync(request.DraweeBankId, ct);
            return entries.Select(e => e.ToResponse()).ToList();
        }
    }
    ```
    - _Requirements: 7.4, 7.5_

  - [ ]* 7.22 Write property test: audit trail ordering is deterministic
    - **Property 13: Audit trail ordering is deterministic**
    - **Validates: Requirements 7.4, 7.5**

  - [ ]* 7.23 Write property test: search filter conjunction
    - **Property 14: Search filter conjunction**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.11**

  - [ ]* 7.24 Write property test: pagination consistency
    - **Property 15: Pagination consistency**
    - **Validates: Requirements 8.4, 8.9**

  - [ ]* 7.25 Write property test: list ordering is deterministic
    - **Property 16: List ordering is deterministic**
    - **Validates: Requirements 8.10**

  - [ ]* 7.26 Write property test: mapper preserves fields
    - **Property 17: Mapper preserves fields**
    - **Validates: Requirements 1.1, 7.1, 7.2, 7.4**

  - [ ] 7.27 Implement `GetChallanRulesByBankIdQuery` and handler

    Backs the decoupled read endpoint so a bank's challan rules are retrievable on their own,
    independent of the bank response (R6).

    **CREATE `Finnova.Service/DraweeBank/Queries/GetChallanRulesByBankId/GetChallanRulesByBankIdQuery.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

    /// <summary>Reads the challan rules configured under one drawee bank (R6). Rules are decoupled
    /// from the bank aggregate and read only through this query / its endpoint.</summary>
    public record GetChallanRulesByBankIdQuery(
        Guid DraweeBankId
    ) : IRequest<List<ChallanRuleResponse>>;
    ```

    **CREATE `Finnova.Service/DraweeBank/Queries/GetChallanRulesByBankId/GetChallanRulesByBankIdQueryHandler.cs`**
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

    public class GetChallanRulesByBankIdQueryHandler
        : IRequestHandler<GetChallanRulesByBankIdQuery, List<ChallanRuleResponse>>
    {
        private readonly IDraweeBankRepository _repository;

        public GetChallanRulesByBankIdQueryHandler(IDraweeBankRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ChallanRuleResponse>> Handle(
            GetChallanRulesByBankIdQuery request, CancellationToken ct)
        {
            // Standalone read — never loaded with the bank. Missing bank id yields an empty list.
            var rules = await _repository.GetChallanRulesByBankIdAsync(request.DraweeBankId, ct);
            return rules.Select(r => r.ToResponse()).ToList();
        }
    }
    ```
    - _Requirements: 6.1_

- [ ] 8. Wire up the API, middleware, and gateway
  - [ ] 8.1 Implement the controller

    **CREATE `Finnova.SystemAdminService/Controllers/DraweeBankController.cs`**
    ```csharp
    using System.Security.Claims;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.DraweeBanks;
    using Finnova.Service.DraweeBank.Commands.CreateDraweeBank;
    using Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;
    using Finnova.Service.DraweeBank.Commands.CreateChallanRule;
    using Finnova.Service.DraweeBank.Commands.UpdateChallanRule;
    using Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;
    using Finnova.Service.DraweeBank.Queries.GetDraweeBankAuditTrail;
    using Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

    namespace Finnova.SystemAdminService.Controllers;

    /// <summary>
    /// Drawee Bank &amp; Challan Rules Master endpoints (FINNOVA-16). Every action is SystemAdmin-only
    /// (R9.1-R9.3, read included). All work flows through MediatR so the existing ValidationBehavior
    /// pipeline runs before each handler.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DraweeBankController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DraweeBankController(IMediator mediator) => _mediator = mediator;

        /// <summary>Paged, filtered admin list (R8). SystemAdmin only.</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(PaginatedResponse<DraweeBankResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<DraweeBankResponse>>> GetPaged(
            [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await _mediator.Send(new GetDraweeBanksPagedQuery(search, page, pageSize)));

        /// <summary>Create a drawee bank aggregate (R1-R4). SystemAdmin only.</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(DraweeBankResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DraweeBankResponse>> Create([FromBody] CreateDraweeBankRequest r)
        {
            var result = await _mediator.Send(new CreateDraweeBankCommand(
                r.BankCode, r.BankName, r.IsActive, r.Branches, r.Restriction, GetActingAdmin()));
            return CreatedAtAction(nameof(GetPaged), new { search = result.BankCode }, result);
        }

        /// <summary>Update a drawee bank's name, branches, and restriction (R5, R3, R4). 404 when missing.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(DraweeBankResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DraweeBankResponse>> Update(Guid id, [FromBody] UpdateDraweeBankRequest r)
            => Ok(await _mediator.Send(new UpdateDraweeBankCommand(
                id, r.BankName, r.Branches, r.Restriction, GetActingAdmin())));

        /// <summary>Read a bank's challan rules (R6). Decoupled from the bank response. SystemAdmin only.
        /// Always 200 with a possibly-empty list.</summary>
        [HttpGet("{id:guid}/challan-rules")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(List<ChallanRuleResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ChallanRuleResponse>>> GetChallanRules(Guid id)
            => Ok(await _mediator.Send(new GetChallanRulesByBankIdQuery(id)));

        /// <summary>Create a challan rule under a bank (R6.1-R6.4, R6.7). SystemAdmin only.</summary>
        [HttpPost("{id:guid}/challan-rules")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(ChallanRuleResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ChallanRuleResponse>> CreateChallanRule(
            Guid id, [FromBody] CreateChallanRuleRequest r)
        {
            var result = await _mediator.Send(new CreateChallanRuleCommand(
                id, r.RuleCode, r.FormatPattern, r.ValidationExpression, r.RoutingTarget, r.IsActive, GetActingAdmin()));
            return CreatedAtAction(nameof(GetPaged), new { search = result.RuleCode }, result);
        }

        /// <summary>Update a challan rule (R6.5, R6.6). 404 when the rule id is missing.</summary>
        [HttpPut("{id:guid}/challan-rules/{ruleId:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(ChallanRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ChallanRuleResponse>> UpdateChallanRule(
            Guid id, Guid ruleId, [FromBody] UpdateChallanRuleRequest r)
            => Ok(await _mediator.Send(new UpdateChallanRuleCommand(
                id, ruleId, r.FormatPattern, r.ValidationExpression, r.RoutingTarget, r.IsActive, GetActingAdmin())));

        /// <summary>Audit trail for a bank, newest first (R7.4). Missing id yields [] (R7.5).</summary>
        [HttpGet("{id:guid}/audit")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(List<DraweeBankAuditEntryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DraweeBankAuditEntryResponse>>> GetAuditTrail(Guid id)
            => Ok(await _mediator.Send(new GetDraweeBankAuditTrailQuery(id)));

        /// <summary>
        /// Resolves the acting administrator id from the validated JWT principal so the audit actor
        /// cannot be spoofed by the request body. Prefers NameIdentifier (sub), falls back to Name.
        /// </summary>
        private string GetActingAdmin()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? User.Identity?.Name
               ?? string.Empty;
    }
    ```
    - _Requirements: 1.1, 3.1, 4.1, 5.1, 6.1, 6.5, 7.4, 7.5, 8.1, 8.4, 9.1, 9.2, 9.3_

  - [ ] 8.2 Extend `ExceptionHandlingMiddleware` with the typed drawee-bank branch

    **MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`** — do NOT replace existing branches. (a) Chain the path-scoped shared codes onto the existing ladder, right after the `fallbackUserCode` lines:
    ```csharp
    var isDraweeBank = ctx.Request.Path.StartsWithSegments("/api/draweebank",
        StringComparison.OrdinalIgnoreCase);
    var validationDraweeBankCode = isDraweeBank ? "ERR-DRB-400" : validationUserCode;
    var fallbackDraweeBankCode   = isDraweeBank ? "ERR-DRB-500" : fallbackUserCode;
    ```

    (b) Add the typed arms to the `switch`, immediately before the shared `FluentValidation.ValidationException` arm:
    ```csharp
    // ---- new Drawee Bank branch (typed, no message sniffing) ----
    DraweeBankNotFoundException             => (404, "ERR-DRB-404", ex.Message),
    ChallanRuleNotFoundException            => (404, "ERR-DRB-404", ex.Message),
    DraweeBankDuplicateCodeException        => (409, "ERR-DRB-409", ex.Message),
    DraweeBranchDuplicatePlaceCodeException => (409, "ERR-DRB-409", ex.Message),
    ChallanRuleDuplicateCodeException       => (409, "ERR-DRB-409", ex.Message),
    DraweeBankValidationException           => (400, "ERR-DRB-400", ex.Message),
    ```

    (c) Switch the two shared arms to the drawee-bank-aware codes (replace the existing `validationUserCode`/`fallbackUserCode` in those two arms):
    ```csharp
    FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationDraweeBankCode, v.Message),
    _ => (StatusCodes.Status500InternalServerError, fallbackDraweeBankCode, "Unexpected error.")
    ```
    - The `ProblemDetails` shape (`Status`, `Title = code`, `Detail`, `Extensions["code"] = code`) is unchanged.
    - _Requirements: 2.2, 2.3, 3.3, 5.4, 6.4, 6.6, 6.7, 6.8, 1.3, 1.4, 3.2, 3.4, 3.6, 4.2, 4.3, 4.5, 5.2, 5.3, 6.3, 8.7, 8.8_

  - [ ] 8.3 Add the gateway UI-alias routes for draweebank

    **MODIFY `Finnova.ApiGateway/appsettings.json`** — add this route pair inside `ReverseProxy.Routes` (mirrors the `ua-nationality-alias-*` pair; place it alongside the other `ua-*-alias-*` entries):
    ```jsonc
    "ua-draweebank-alias-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/draweebank/{**catch-all}"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    "ua-draweebank-alias-root": {
      "ClusterId": "systemadmin-cluster",
      "Match": {
        "Path": "/api/ua/api/draweebank"
      },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    ```
    - Keep host separation intact; the direct `/api/systemadmin/**` route also reaches the controller.
    - _Requirements: 9.3_

- [ ] 9. Checkpoint - backend build and tests green
  - Run `dotnet build` and `dotnet test`. Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Add in-memory test infrastructure, property tests, and edge/integration tests
  - [ ] 10.1 Add the in-memory repositories and builders/generators

    **CREATE `Finnova.Tests/Infrastructure/InMemoryDraweeBankRepository.cs`**
    ```csharp
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Tests.Infrastructure;

    /// <summary>
    /// Hand-written in-memory <see cref="IDraweeBankRepository"/> mirroring the real EF-backed repo so
    /// property tests exercise the actual handlers cheaply. CI semantics via OrdinalIgnoreCase match
    /// SQL Server's default collation. Read paths clone so callers cannot mutate stored state.
    /// </summary>
    public sealed class InMemoryDraweeBankRepository : IDraweeBankRepository
    {
        private readonly List<DraweeBank> _store = new();
        private readonly List<ChallanRule> _rules = new();

        public InMemoryDraweeBankRepository() { }

        public InMemoryDraweeBankRepository(IEnumerable<DraweeBank> seed)
        {
            foreach (var b in seed) _store.Add(Clone(b));
        }

        public IReadOnlyList<DraweeBank> Snapshot() => _store.Select(Clone).ToList();
        public IReadOnlyList<ChallanRule> RuleSnapshot() => _rules.Select(Clone).ToList();
        public int Count => _store.Count;

        private static DraweeBranch Clone(DraweeBranch b) => new()
        {
            Id = b.Id, DraweeBankId = b.DraweeBankId, PlaceCode = b.PlaceCode, PlaceName = b.PlaceName,
            Address = b.Address, PostalCode = b.PostalCode, StartDate = b.StartDate, EndDate = b.EndDate
        };
        private static RestrictionDetail? Clone(RestrictionDetail? r) => r == null ? null : new()
        {
            Id = r.Id, DraweeBankId = r.DraweeBankId, ClearingDays = r.ClearingDays,
            StartDate = r.StartDate, EndDate = r.EndDate
        };
        private static ChallanRule Clone(ChallanRule c) => new()
        {
            Id = c.Id, DraweeBankId = c.DraweeBankId, RuleCode = c.RuleCode, FormatPattern = c.FormatPattern,
            ValidationExpression = c.ValidationExpression, RoutingTarget = c.RoutingTarget,
            IsActive = c.IsActive, CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
        };
        private DraweeBank Clone(DraweeBank x) => new()
        {
            Id = x.Id, BankCode = x.BankCode, BankName = x.BankName, IsActive = x.IsActive,
            Branches = x.Branches.Select(Clone).ToList(),
            Restriction = Clone(x.Restriction),
            // Challan rules are decoupled — stored in the separate _rules list, not on the bank.
            CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
        };

        // ---- IRepository<DraweeBank> ----
        public Task<DraweeBank?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var f = _store.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(f is null ? null : Clone(f));
        }
        public Task<List<DraweeBank>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());
        public Task<List<DraweeBank>> FindAsync(Expression<Func<DraweeBank, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());
        public Task AddAsync(DraweeBank entity, CancellationToken ct = default)
        {
            _store.Add(Clone(entity));
            return Task.CompletedTask;
        }
        public Task UpdateAsync(DraweeBank entity, CancellationToken ct = default)
        {
            var idx = _store.FindIndex(x => x.Id == entity.Id);
            if (idx >= 0) _store[idx] = Clone(entity);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(DraweeBank entity, CancellationToken ct = default)
        {
            _store.RemoveAll(x => x.Id == entity.Id);
            return Task.CompletedTask;
        }
        public Task<int> CountAsync(Expression<Func<DraweeBank, bool>>? p = null, CancellationToken ct = default)
            => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));

        // ---- IDraweeBankRepository ----
        public Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
            string? searchTerm, int page, int pageSize, CancellationToken ct = default)
        {
            IEnumerable<DraweeBank> query = _store;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(x =>
                    x.BankCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.BankName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            var all = query.ToList();
            var total = all.Count;
            var items = all
                .OrderBy(x => x.BankName, StringComparer.Ordinal)
                .ThenBy(x => x.BankCode, StringComparer.Ordinal)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(Clone).ToList();
            return Task.FromResult((items, total));
        }

        public Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default)
            => GetByIdAsync(id, ct);

        public Task<bool> ExistsByBankCodeAsync(string bankCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = bankCode.Trim();
            return Task.FromResult(_store.Any(x =>
                string.Equals(x.BankCode, c, StringComparison.OrdinalIgnoreCase) &&
                (excludeId == null || x.Id != excludeId)));
        }

        public Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid bankId, CancellationToken ct = default)
            => Task.FromResult(_rules.Where(r => r.DraweeBankId == bankId).Select(Clone).ToList());
        public Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
        {
            var f = _rules.FirstOrDefault(r => r.Id == ruleId);
            return Task.FromResult(f is null ? null : Clone(f));
        }
        public Task<bool> ChallanRuleCodeExistsAsync(Guid bankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default)
        {
            var code = ruleCode.Trim();
            return Task.FromResult(_rules.Any(r =>
                r.DraweeBankId == bankId &&
                string.Equals(r.RuleCode, code, StringComparison.OrdinalIgnoreCase) &&
                (excludeId == null || r.Id != excludeId)));
        }
        public Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
        {
            _rules.Add(Clone(rule));
            return Task.CompletedTask;
        }
        public Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
        {
            var idx = _rules.FindIndex(r => r.Id == rule.Id);
            if (idx >= 0) _rules[idx] = Clone(rule);
            return Task.CompletedTask;
        }
    }
    ```

    **CREATE `Finnova.Tests/Infrastructure/InMemoryDraweeBankAuditRepository.cs`**
    ```csharp
    using System.Linq.Expressions;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Tests.Infrastructure;

    /// <summary>In-memory audit repo mirroring the real one: newest-first by ChangedAtUtc then Id,
    /// empty list for an unknown id, add + immutable reads only (R7.4, R7.5, R7.6).</summary>
    public sealed class InMemoryDraweeBankAuditRepository : IDraweeBankAuditRepository
    {
        private readonly List<DraweeBankAuditEntry> _store = new();

        public IReadOnlyList<DraweeBankAuditEntry> Snapshot() => _store.Select(Clone).ToList();
        public int Count => _store.Count;

        private static DraweeBankAuditEntry Clone(DraweeBankAuditEntry a) => new()
        {
            Id = a.Id, DraweeBankId = a.DraweeBankId, Action = a.Action,
            BeforeSnapshot = a.BeforeSnapshot, AfterSnapshot = a.AfterSnapshot,
            ChangedBy = a.ChangedBy, ChangedAtUtc = a.ChangedAtUtc
        };

        public Task<DraweeBankAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var f = _store.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(f is null ? null : Clone(f));
        }
        public Task<List<DraweeBankAuditEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_store.Select(Clone).ToList());
        public Task<List<DraweeBankAuditEntry>> FindAsync(Expression<Func<DraweeBankAuditEntry, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_store.Where(p.Compile()).Select(Clone).ToList());
        public Task AddAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
        {
            _store.Add(Clone(entity));
            return Task.CompletedTask;
        }
        // Immutable: Update/Delete are intentionally not meaningful for audit entries (R7.6).
        public Task UpdateAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
            => throw new InvalidOperationException("Audit entries are immutable.");
        public Task DeleteAsync(DraweeBankAuditEntry entity, CancellationToken ct = default)
            => throw new InvalidOperationException("Audit entries are immutable.");
        public Task<int> CountAsync(Expression<Func<DraweeBankAuditEntry, bool>>? p = null, CancellationToken ct = default)
            => Task.FromResult(p is null ? _store.Count : _store.Count(p.Compile()));

        public Task<List<DraweeBankAuditEntry>> GetByDraweeBankIdAsync(Guid bankId, CancellationToken ct = default)
            => Task.FromResult(_store
                .Where(x => x.DraweeBankId == bankId)
                .OrderByDescending(x => x.ChangedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(Clone).ToList());
    }
    ```

    **CREATE `Finnova.Tests/Infrastructure/DraweeBankBuilder.cs`**
    ```csharp
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Tests.Infrastructure;

    /// <summary>Fluent builder for valid India-only create/update requests used by example tests.</summary>
    public sealed class DraweeBankBuilder
    {
        private string _code = "HDFC";
        private string _name = "HDFC Bank Ltd";
        private bool? _isActive = null;
        private readonly List<DraweeBranchRequest> _branches = new();
        private RestrictionDetailRequest? _restriction;

        public DraweeBankBuilder WithCode(string code) { _code = code; return this; }
        public DraweeBankBuilder WithName(string name) { _name = name; return this; }
        public DraweeBankBuilder WithIsActive(bool? v) { _isActive = v; return this; }

        public DraweeBankBuilder WithBranch(string placeCode = "MUM", string placeName = "Mumbai",
            string address = "Fort", string pin = "400001", DateTime? start = null, DateTime? end = null)
        {
            _branches.Add(new DraweeBranchRequest(placeCode, placeName, address, pin,
                start ?? new DateTime(2024, 1, 1), end));
            return this;
        }

        public DraweeBankBuilder WithRestriction(int clearingDays = 2,
            DateTime? start = null, DateTime? end = null)
        {
            _restriction = new RestrictionDetailRequest(clearingDays,
                start ?? new DateTime(2024, 1, 1), end ?? new DateTime(2024, 12, 31));
            return this;
        }

        public CreateDraweeBankRequest BuildCreate()
            => new(_code, _name, _isActive, _branches, _restriction);

        public UpdateDraweeBankRequest BuildUpdate()
            => new(_name, _branches, _restriction);

        // Standalone challan-rule request for the dedicated rule route (rules are NOT part of the bank
        // aggregate; they are created via CreateChallanRuleCommand, not with the bank).
        public static CreateChallanRuleRequest BuildRule(string ruleCode = "R1", string format = "AA[0-9]{6}",
            string expr = "len(v)==8", string routing = "CLEARING-A", bool? active = null)
            => new(ruleCode, format, expr, routing, active);
    }
    ```

    **CREATE `Finnova.Tests/Infrastructure/DraweeBankGenerators.cs`**
    ```csharp
    using FsCheck;
    using Finnova.Models.Contracts.DraweeBanks;

    namespace Finnova.Tests.Infrastructure;

    /// <summary>
    /// FsCheck generators for drawee-bank property tests. Produces valid + edge strings within the
    /// max lengths (20/150/20/150/300/50/500/500/200), six-digit and malformed PINs, casing/whitespace
    /// code variants, branch sets with/without duplicate place codes, restrictions with in-range and
    /// out-of-range clearing days and valid/invalid date ranges, datasets, and audit-entry sets
    /// (including empty). English-only / India-appropriate values throughout.
    /// </summary>
    public static class DraweeBankGenerators
    {
        public static Arbitrary<string> ValidBankCode() =>
            Gen.Elements("HDFC", "ICICI", "SBI", "AXIS", "KOTAK", "PNB", "BOB", "CANARA")
               .ToArbitrary();

        public static Arbitrary<string> ValidBankName() =>
            Gen.Elements("HDFC Bank Ltd", "ICICI Bank Ltd", "State Bank of India",
                         "Axis Bank Ltd", "Kotak Mahindra Bank").ToArbitrary();

        public static Arbitrary<string> SixDigitPin() =>
            Gen.Elements("400001", "411001", "560001", "110001", "600001").ToArbitrary();

        public static Arbitrary<string> MalformedPin() =>
            Gen.Elements("40001", "4000011", "4000AB", "", "   ").ToArbitrary();

        // Casing/whitespace variants of a code for uniqueness tests (R2.1, R6.4).
        public static Gen<string> CaseWhitespaceVariant(string code) =>
            Gen.Elements(code.ToUpperInvariant(), code.ToLowerInvariant(),
                         $"  {code}  ", $"{code} ", $" {code}");

        public static Arbitrary<DraweeBranchRequest> ValidBranch() =>
            (from pc in Gen.Elements("MUM", "PUN", "DEL", "BLR", "CHN")
             from pin in SixDigitPin().Generator
             select new DraweeBranchRequest(pc, "Mumbai", "Fort", pin,
                 new DateTime(2024, 1, 1), null)).ToArbitrary();
    }
    ```
    - _Requirements: 1.1, 2.1, 3.1, 3.3, 3.6, 4.1, 6.4, 7.4, 8.1_

  - [ ]* 10.2 Write validator-boundary and query-default edge unit tests
    - `BankCode` 20 accepted / 21 rejected; `BankName` 150 accepted / 151 rejected; blank BankCode/BankName rejected (R1.3, R1.4).
    - PIN `400001` accepted; `40001` / `4000011` / `4000AB` rejected (R3.6).
    - `ClearingDays` 0 and 365 accepted; `-1` and `366` rejected (R4.2, R4.5); restriction `EndDate < StartDate` rejected (R4.3); branch `EndDate < StartDate` rejected (R3.4); null branch EndDate persisted open-ended (R3.5).
    - Rule-field length bounds 50/500/500/200 (R6.3); unparseable pattern/expression rejected (R6.8).
    - Query defaults `Page == 1`, `PageSize == 20` (R8.5, R8.6); `Page < 1`, `PageSize < 1` / `> 100` rejected (R8.7, R8.8).
    - _Requirements: 1.3, 1.4, 3.4, 3.5, 3.6, 4.2, 4.3, 4.5, 6.3, 6.8, 8.5, 8.6, 8.7, 8.8_

  - [ ]* 10.3 Write not-found, atomic-failure, audit-missing-id, and immutability edge tests
    - Update unknown id → `DraweeBankNotFoundException`, `UpdateAsync` never called (R5.4).
    - Rule update unknown id → `ChallanRuleNotFoundException` (R6.6); rule create on unknown bank → `DraweeBankNotFoundException` (R6.7).
    - A create whose branch validation fails persists nothing and writes no audit (R1.7, R1.8, R7.3).
    - Audit read for a missing id returns `[]` with no exception (R7.5); `InMemoryDraweeBankAuditRepository.Update/Delete` throw (audit immutability, R7.6).
    - _Requirements: 1.7, 1.8, 5.4, 6.6, 6.7, 7.3, 7.5, 7.6_

  - [ ]* 10.4 Write authorization integration tests (`WebApplicationFactory<Program>`)
    - Mirror `SystemAdminAppFactory` (EF Core InMemory provider, dev-signed JWTs). Assert 401 for missing/expired/invalid token on every endpoint (R9.1); 403 for a valid non-admin token on every endpoint (R9.2); 401-before-403 for an invalid token that also lacks the role (R9.4).
    - _Requirements: 9.1, 9.2, 9.4_

  - [ ]* 10.5 Write end-to-end create/list/challan-rule/audit integration test
    - With a valid `SystemAdmin` token: create a bank (`HDFC` / `HDFC Bank Ltd` with a `Mumbai`/`400001` branch) → 201; list via paged search → 200; add a challan rule → 201; read the audit trail → 200. Assert a case-insensitive duplicate bank-code create returns 409 `ERR-DRB-409` (R9.3, R2.2, R7.4).
    - _Requirements: 1.1, 2.2, 6.1, 7.4, 8.1, 9.3_

- [ ] 11. Frontend model and service layer (Finnova-UI repo)
  - **Implemented in the separate `Finnova-UI` repository at `E:\Finnova\Finnova-UI\Finnova-UI` (React 18 + TS + MUI + Vite; hooks + Context; no Redux/RxJS). Use the distinct `draweeBank` / `DraweeBank` namespace everywhere so nothing collides with existing masters.**
  - [ ] 11.1 Add the model and barrel export

    **CREATE `src/models/draweeBank.model.ts`**
    ```typescript
    export interface DraweeBranch {
      id: string;
      draweeBankId: string;
      placeCode: string;
      placeName: string;
      address: string;
      postalCode: string;
      startDate: string;
      endDate: string | null; // null => open-ended
    }

    export interface RestrictionDetail {
      id: string;
      draweeBankId: string;
      clearingDays: number;
      startDate: string;
      endDate: string;
    }

    export interface ChallanRule {
      id: string;
      draweeBankId: string;
      ruleCode: string;
      formatPattern: string;
      validationExpression: string;
      routingTarget: string;
      isActive: boolean;
      createdAt: string;
      updatedAt: string;
    }

    // Bank does NOT carry challan rules; the standalone ChallanRule type above is loaded separately.
    export interface DraweeBank {
      id: string;
      bankCode: string;
      bankName: string;
      isActive: boolean;
      branches: DraweeBranch[];
      restriction: RestrictionDetail | null;
      createdAt: string;
      updatedAt: string;
    }

    export interface DraweeBankAuditEntry {
      id: string;
      draweeBankId: string;
      action: 'Create' | 'Update' | 'Delete';
      beforeSnapshot: string | null;
      afterSnapshot: string | null;
      changedBy: string;
      changedAtUtc: string;
    }

    // ---- Form payloads (dialogs) ----

    export interface DraweeBranchFormData {
      placeCode: string;
      placeName: string;
      address: string;
      postalCode: string;
      startDate: string;
      endDate: string | null;
    }

    export interface RestrictionDetailFormData {
      clearingDays: number;
      startDate: string;
      endDate: string;
    }

    export interface ChallanRuleFormData {
      ruleCode: string;
      formatPattern: string;
      validationExpression: string;
      routingTarget: string;
      isActive: boolean;
    }

    // Bank form carries no challan rules; rules are created/edited via the standalone rule dialog.
    export interface DraweeBankFormData {
      bankCode: string; // immutable on edit
      bankName: string;
      isActive: boolean;
      branches: DraweeBranchFormData[];
      restriction: RestrictionDetailFormData | null;
    }

    export interface DraweeBankUpdateData {
      bankName: string;
      branches: DraweeBranchFormData[];
      restriction: RestrictionDetailFormData | null;
    }
    ```

    **MODIFY `src/models/index.ts`** — add the barrel re-export alongside the other model exports:
    ```typescript
    export * from './draweeBank.model';
    ```
    - _Requirements: 1.1, 3.1, 4.1, 5.1, 6.1, 7.4, 8.1_

  - [ ] 11.2 Add the service interface and barrel export

    **CREATE `src/services/interfaces/draweeBank.interface.ts`**
    ```typescript
    import type { PaginatedResponse } from '../../models/api.model';
    import type {
      DraweeBank,
      DraweeBankFormData,
      DraweeBankUpdateData,
      ChallanRule,
      ChallanRuleFormData,
      DraweeBankAuditEntry,
    } from '../../models/draweeBank.model';

    export interface DraweeBankQueryParams {
      search?: string;
      page?: number;
      pageSize?: number;
    }

    export interface IDraweeBankService {
      getPaged(params: DraweeBankQueryParams): Promise<PaginatedResponse<DraweeBank>>;
      create(data: DraweeBankFormData): Promise<DraweeBank>; // no challan rules carried
      update(id: string, data: DraweeBankUpdateData): Promise<DraweeBank>; // no challan rules carried
      getChallanRules(bankId: string): Promise<ChallanRule[]>; // read a bank's rules on their own
      createChallanRule(bankId: string, data: ChallanRuleFormData): Promise<ChallanRule>;
      updateChallanRule(
        bankId: string,
        ruleId: string,
        data: Omit<ChallanRuleFormData, 'ruleCode'>,
      ): Promise<ChallanRule>;
      getAuditTrail(id: string): Promise<DraweeBankAuditEntry[]>;
    }
    ```

    **MODIFY `src/services/interfaces/index.ts`** — add:
    ```typescript
    export * from './draweeBank.interface';
    ```
    - _Requirements: 1.1, 5.1, 6.1, 6.5, 7.4, 8.1_

  - [ ] 11.3 Implement the real service

    **CREATE `src/services/real/draweeBank.real.ts`**
    ```typescript
    import { api } from '../api';
    import type { PaginatedResponse } from '../../models/api.model';
    import type {
      DraweeBank,
      DraweeBankFormData,
      DraweeBankUpdateData,
      ChallanRule,
      ChallanRuleFormData,
      DraweeBankAuditEntry,
    } from '../../models/draweeBank.model';
    import type {
      IDraweeBankService,
      DraweeBankQueryParams,
    } from '../interfaces/draweeBank.interface';

    // The gateway alias (/api/ua/api/draweebank -> systemadmin-cluster -> /api/draweebank) handles
    // routing; the UI adds no gateway config and composes a service-relative base path.
    const basePath = '/draweebank';

    class DraweeBankRealService implements IDraweeBankService {
      async getPaged(params: DraweeBankQueryParams): Promise<PaginatedResponse<DraweeBank>> {
        const { data } = await api.get<PaginatedResponse<DraweeBank>>(basePath, {
          params: {
            search: params.search || undefined,
            page: params.page ?? 1,
            pageSize: params.pageSize ?? 20,
          },
        });
        return data;
      }

      async create(form: DraweeBankFormData): Promise<DraweeBank> {
        // Bank create carries no challan rules — they are added via createChallanRule.
        const { data } = await api.post<DraweeBank>(basePath, {
          bankCode: form.bankCode,
          bankName: form.bankName,
          isActive: form.isActive,
          branches: form.branches,
          restriction: form.restriction,
        });
        return data;
      }

      async update(id: string, form: DraweeBankUpdateData): Promise<DraweeBank> {
        // Bank update carries no challan rules.
        const { data } = await api.put<DraweeBank>(`${basePath}/${id}`, {
          bankName: form.bankName,
          branches: form.branches,
          restriction: form.restriction,
        });
        return data;
      }

      async getChallanRules(bankId: string): Promise<ChallanRule[]> {
        const { data } = await api.get<ChallanRule[]>(`${basePath}/${bankId}/challan-rules`);
        return data;
      }

      async createChallanRule(bankId: string, form: ChallanRuleFormData): Promise<ChallanRule> {
        const { data } = await api.post<ChallanRule>(`${basePath}/${bankId}/challan-rules`, form);
        return data;
      }

      async updateChallanRule(
        bankId: string,
        ruleId: string,
        form: Omit<ChallanRuleFormData, 'ruleCode'>,
      ): Promise<ChallanRule> {
        const { data } = await api.put<ChallanRule>(
          `${basePath}/${bankId}/challan-rules/${ruleId}`,
          form,
        );
        return data;
      }

      async getAuditTrail(id: string): Promise<DraweeBankAuditEntry[]> {
        const { data } = await api.get<DraweeBankAuditEntry[]>(`${basePath}/${id}/audit`);
        return data;
      }
    }

    export const draweeBankRealService = new DraweeBankRealService();
    ```
    - _Requirements: 1.1, 5.1, 6.1, 6.5, 7.4, 8.1, 8.4_

  - [ ] 11.4 Implement the mock service

    **CREATE `src/services/mock/draweeBank.mock.ts`**
    ```typescript
    import type { PaginatedResponse } from '../../models/api.model';
    import type {
      DraweeBank,
      DraweeBankFormData,
      DraweeBankUpdateData,
      ChallanRule,
      ChallanRuleFormData,
      DraweeBankAuditEntry,
    } from '../../models/draweeBank.model';
    import type {
      IDraweeBankService,
      DraweeBankQueryParams,
    } from '../interfaces/draweeBank.interface';

    // Backend-shaped error so mock + real behave identically through the shared axios interceptor.
    function apiError(status: number, code: string, message: string): never {
      const err = new Error(message) as Error & { response?: unknown };
      err.response = { status, data: { code, message } };
      throw err;
    }

    const nowIso = () => new Date().toISOString();
    const newId = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`);
    const PIN = /^\d{6}$/;

    class DraweeBankMockService implements IDraweeBankService {
      // India-only / English-only seed. Banks do NOT carry challan rules.
      private banks: DraweeBank[] = [
        {
          id: newId(),
          bankCode: 'HDFC',
          bankName: 'HDFC Bank Ltd',
          isActive: true,
          branches: [
            {
              id: newId(),
              draweeBankId: '',
              placeCode: 'MUM',
              placeName: 'Mumbai',
              address: 'Fort',
              postalCode: '400001',
              startDate: '2024-01-01',
              endDate: null,
            },
          ],
          restriction: { id: newId(), draweeBankId: '', clearingDays: 2, startDate: '2024-01-01', endDate: '2024-12-31' },
          createdAt: nowIso(),
          updatedAt: nowIso(),
        },
      ];
      // Challan rules are decoupled: a separate in-memory store keyed by bank id.
      private rules: ChallanRule[] = [];
      private audit: DraweeBankAuditEntry[] = [];

      private validateBranches(branches: DraweeBankFormData['branches']): void {
        const seen = new Set<string>();
        for (const b of branches) {
          if (!b.placeCode.trim() || !b.placeName.trim())
            apiError(400, 'ERR-DRB-400', 'Place Code and Place Name are required.');
          if (!PIN.test(b.postalCode))
            apiError(400, 'ERR-DRB-400', 'Postal Code must be a six-digit PIN.');
          if (b.endDate && b.endDate < b.startDate)
            apiError(400, 'ERR-DRB-400', 'Branch End Date must be on or after Start Date.');
          const key = b.placeCode.trim().toLowerCase();
          if (seen.has(key)) apiError(409, 'ERR-DRB-409', 'Place code must be unique within the bank');
          seen.add(key);
        }
      }

      private validateRestriction(r: DraweeBankFormData['restriction']): void {
        if (!r) return;
        if (r.clearingDays < 0 || r.clearingDays > 365)
          apiError(400, 'ERR-DRB-400', 'Clearing Days must be between 0 and 365.');
        if (r.endDate < r.startDate)
          apiError(400, 'ERR-DRB-400', 'Restriction End Date must be on or after Start Date.');
      }

      async getPaged(params: DraweeBankQueryParams): Promise<PaginatedResponse<DraweeBank>> {
        const page = params.page ?? 1;
        const pageSize = params.pageSize ?? 20;
        const term = (params.search ?? '').trim().toLowerCase();

        let rows = this.banks;
        if (term) {
          rows = rows.filter(
            (b) =>
              b.bankCode.toLowerCase().includes(term) ||
              b.bankName.toLowerCase().includes(term),
          );
        }
        const sorted = [...rows].sort(
          (a, b) => a.bankName.localeCompare(b.bankName) || a.bankCode.localeCompare(b.bankCode),
        );
        const total = sorted.length;
        const data = sorted.slice((page - 1) * pageSize, (page - 1) * pageSize + pageSize);
        return { data, total, page, pageSize, totalPages: Math.ceil(total / pageSize) };
      }

      async create(form: DraweeBankFormData): Promise<DraweeBank> {
        if (!form.bankCode.trim() || !form.bankName.trim())
          apiError(400, 'ERR-DRB-400', 'Bank Code and Bank Name are required.');
        this.validateBranches(form.branches);
        this.validateRestriction(form.restriction);

        const code = form.bankCode.trim();
        if (this.banks.some((b) => b.bankCode.toLowerCase() === code.toLowerCase()))
          apiError(409, 'ERR-DRB-409', 'Bank code must be unique');

        const id = newId();
        const bank: DraweeBank = {
          id,
          bankCode: code,
          bankName: form.bankName,
          isActive: form.isActive,
          branches: form.branches.map((b) => ({ ...b, id: newId(), draweeBankId: id })),
          restriction: form.restriction ? { ...form.restriction, id: newId(), draweeBankId: id } : null,
          // Bank create does not touch challan rules — they live in the separate rules store.
          createdAt: nowIso(),
          updatedAt: nowIso(),
        };
        this.banks.push(bank);
        this.audit.push({
          id: newId(),
          draweeBankId: id,
          action: 'Create',
          beforeSnapshot: null,
          afterSnapshot: JSON.stringify({ bankCode: code, bankName: form.bankName }),
          changedBy: 'mock-admin',
          changedAtUtc: nowIso(),
        });
        return bank;
      }

      async update(id: string, form: DraweeBankUpdateData): Promise<DraweeBank> {
        const bank = this.banks.find((b) => b.id === id);
        if (!bank) apiError(404, 'ERR-DRB-404', `Drawee bank '${id}' was not found.`);
        if (!form.bankName.trim()) apiError(400, 'ERR-DRB-400', 'Bank Name is required.');
        this.validateBranches(form.branches);
        this.validateRestriction(form.restriction);

        const before = JSON.stringify({ bankName: bank!.bankName });
        bank!.bankName = form.bankName;
        bank!.branches = form.branches.map((b) => ({ ...b, id: newId(), draweeBankId: id }));
        bank!.restriction = form.restriction ? { ...form.restriction, id: newId(), draweeBankId: id } : null;
        const after = JSON.stringify({ bankName: bank!.bankName });

        if (before !== after) {
          bank!.updatedAt = nowIso();
          this.audit.push({
            id: newId(),
            draweeBankId: id,
            action: 'Update',
            beforeSnapshot: before,
            afterSnapshot: after,
            changedBy: 'mock-admin',
            changedAtUtc: nowIso(),
          });
        }
        return bank!;
      }

      async getChallanRules(bankId: string): Promise<ChallanRule[]> {
        // Rules are read on their own, not off the bank object.
        return this.rules
          .filter((r) => r.draweeBankId === bankId)
          .sort((a, b) => a.ruleCode.localeCompare(b.ruleCode));
      }

      async createChallanRule(bankId: string, form: ChallanRuleFormData): Promise<ChallanRule> {
        const bank = this.banks.find((b) => b.id === bankId);
        if (!bank) apiError(404, 'ERR-DRB-404', `Drawee bank '${bankId}' was not found.`);
        const code = form.ruleCode.trim();
        if (!code || !form.formatPattern.trim() || !form.validationExpression.trim() || !form.routingTarget.trim())
          apiError(400, 'ERR-DRB-400', 'All challan rule fields are required.');
        if (this.rules.some((r) => r.draweeBankId === bankId && r.ruleCode.toLowerCase() === code.toLowerCase()))
          apiError(409, 'ERR-DRB-409', 'Rule code must be unique within the bank');

        const rule: ChallanRule = {
          id: newId(),
          draweeBankId: bankId,
          ruleCode: code,
          formatPattern: form.formatPattern,
          validationExpression: form.validationExpression,
          routingTarget: form.routingTarget,
          isActive: form.isActive,
          createdAt: nowIso(),
          updatedAt: nowIso(),
        };
        this.rules.push(rule);
        this.audit.push({
          id: newId(),
          draweeBankId: bankId,
          action: 'Create',
          beforeSnapshot: null,
          afterSnapshot: JSON.stringify({ ruleCode: code }),
          changedBy: 'mock-admin',
          changedAtUtc: nowIso(),
        });
        return rule;
      }

      async updateChallanRule(
        bankId: string,
        ruleId: string,
        form: Omit<ChallanRuleFormData, 'ruleCode'>,
      ): Promise<ChallanRule> {
        const rule = this.rules.find((r) => r.draweeBankId === bankId && r.id === ruleId);
        if (!rule) apiError(404, 'ERR-DRB-404', `Challan rule '${ruleId}' was not found.`);
        if (!form.formatPattern.trim() || !form.validationExpression.trim() || !form.routingTarget.trim())
          apiError(400, 'ERR-DRB-400', 'All challan rule fields are required.');
        rule!.formatPattern = form.formatPattern;
        rule!.validationExpression = form.validationExpression;
        rule!.routingTarget = form.routingTarget;
        rule!.isActive = form.isActive;
        rule!.updatedAt = nowIso();
        this.audit.push({
          id: newId(),
          draweeBankId: bankId,
          action: 'Update',
          beforeSnapshot: JSON.stringify({ ruleId }),
          afterSnapshot: JSON.stringify({ routingTarget: form.routingTarget }),
          changedBy: 'mock-admin',
          changedAtUtc: nowIso(),
        });
        return rule!;
      }

      async getAuditTrail(id: string): Promise<DraweeBankAuditEntry[]> {
        return this.audit
          .filter((a) => a.draweeBankId === id)
          .sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc) || b.id.localeCompare(a.id));
      }
    }

    export const draweeBankMockService = new DraweeBankMockService();
    ```
    - _Requirements: 1.1, 2.2, 3.3, 3.6, 4.2, 4.3, 5.1, 5.5, 6.1, 6.4, 7.3, 7.4, 8.1, 8.10_

  - [ ] 11.5 Add the toggle and services barrel export

    **CREATE `src/services/draweeBank.service.ts`**
    ```typescript
    import { draweeBankMockService } from './mock/draweeBank.mock';
    import { draweeBankRealService } from './real/draweeBank.real';
    import type { IDraweeBankService } from './interfaces/draweeBank.interface';

    const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

    export const draweeBankService: IDraweeBankService = useMock
      ? draweeBankMockService
      : draweeBankRealService;
    ```

    **MODIFY `src/services/index.ts`** — add:
    ```typescript
    export * from './draweeBank.service';
    ```
    - _Requirements: 1.1, 5.1, 8.1_

  - [ ]* 11.6 Write service unit tests (Vitest)
    - Mock service: case-insensitive bank-code / place-code / rule-code uniqueness (throws the `ERR-DRB-409`-shaped error), six-digit PIN and clearing-days(0–365) validation (400 `ERR-DRB-409`-shaped), substring search, `BankName asc → BankCode asc` ordering, pagination, and audit append (never on a rejected op or a no-op). Assert challan rules are **not** on the returned bank object and are retrieved only via `getChallanRules(bankId)` from the separate per-bank store; bank create/update do not create or return rules. Real service: DTO↔model mapping and path composition for `/draweebank`, `/draweebank/{id}`, `GET /draweebank/{id}/challan-rules`, `POST/PUT /draweebank/{id}/challan-rules[/{ruleId}]`, and `/draweebank/{id}/audit` against a mocked `api`.
    - _Requirements: 1.1, 2.2, 3.3, 3.6, 4.2, 5.1, 6.4, 7.4, 8.1, 8.10_

- [ ] 12. Frontend DraweeBankMaster page, components, route, and nav (Finnova-UI repo)
  - **Implemented in the `Finnova-UI` repository. Mirror `NationalityMaster.tsx` / `LookupMaster.tsx`: hooks only, debounced search, server-side `@mui/x-data-grid`, leave the list unchanged on error so the shared axios interceptor's toast is the only feedback.**
  - [ ] 12.1 Implement the bank list grid component

    **CREATE `src/components/draweeBank/DraweeBankGrid.tsx`**
    ```tsx
    import { Box, IconButton, Tooltip } from '@mui/material';
    import EditIcon from '@mui/icons-material/Edit';
    import HistoryIcon from '@mui/icons-material/History';
    import RuleIcon from '@mui/icons-material/Rule';
    import { DataGrid, type GridColDef } from '@mui/x-data-grid';
    import type { DraweeBank } from '../../models/draweeBank.model';

    interface Props {
      rows: DraweeBank[];
      rowCount: number;
      loading: boolean;
      page: number;
      pageSize: number;
      onPageChange: (page: number, pageSize: number) => void;
      onEdit: (bank: DraweeBank) => void;
      onRules: (bank: DraweeBank) => void;
      onAudit: (bank: DraweeBank) => void;
    }

    export default function DraweeBankGrid({
      rows, rowCount, loading, page, pageSize, onPageChange, onEdit, onRules, onAudit,
    }: Props) {
      const columns: GridColDef<DraweeBank>[] = [
        { field: 'bankCode', headerName: 'Bank Code', width: 140 },
        { field: 'bankName', headerName: 'Bank Name', flex: 1, minWidth: 220 },
        {
          field: 'isActive', headerName: 'Active', width: 100,
          valueFormatter: (value) => (value ? 'Yes' : 'No'),
        },
        {
          field: 'updatedAt', headerName: 'Updated', width: 180,
          valueFormatter: (value) => (value ? new Date(value as string).toLocaleString() : ''),
        },
        {
          field: 'actions', headerName: 'Actions', width: 150, sortable: false, filterable: false,
          renderCell: (params) => (
            <Box>
              <Tooltip title="Edit">
                <IconButton size="small" onClick={() => onEdit(params.row)} aria-label="edit">
                  <EditIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Challan Rules">
                <IconButton size="small" onClick={() => onRules(params.row)} aria-label="rules">
                  <RuleIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Audit">
                <IconButton size="small" onClick={() => onAudit(params.row)} aria-label="audit">
                  <HistoryIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </Box>
          ),
        },
      ];

      return (
        <DataGrid
          rows={rows}
          columns={columns}
          getRowId={(r) => r.id}
          loading={loading}
          rowCount={rowCount}
          paginationMode="server"
          paginationModel={{ page: page - 1, pageSize }}
          pageSizeOptions={[10, 20, 50, 100]}
          onPaginationModelChange={(m) => onPageChange(m.page + 1, m.pageSize)}
          disableRowSelectionOnClick
          autoHeight
        />
      );
    }
    ```
    - _Requirements: 8.1, 8.4, 8.10_

  - [ ] 12.2 Implement the branches sub-grid and restriction panel

    **CREATE `src/components/draweeBank/DraweeBranchSubGrid.tsx`**
    ```tsx
    import { Box, Button, IconButton, Stack, TextField, Typography } from '@mui/material';
    import DeleteIcon from '@mui/icons-material/Delete';
    import AddIcon from '@mui/icons-material/Add';
    import type { DraweeBranchFormData } from '../../models/draweeBank.model';

    interface Props {
      branches: DraweeBranchFormData[];
      onChange: (branches: DraweeBranchFormData[]) => void;
    }

    const emptyBranch: DraweeBranchFormData = {
      placeCode: '', placeName: '', address: '', postalCode: '', startDate: '', endDate: null,
    };

    export default function DraweeBranchSubGrid({ branches, onChange }: Props) {
      const update = (i: number, patch: Partial<DraweeBranchFormData>) =>
        onChange(branches.map((b, idx) => (idx === i ? { ...b, ...patch } : b)));
      const remove = (i: number) => onChange(branches.filter((_, idx) => idx !== i));
      const add = () => onChange([...branches, { ...emptyBranch }]);

      return (
        <Box>
          <Typography variant="subtitle1" gutterBottom>List of Branches</Typography>
          <Stack spacing={1}>
            {branches.map((b, i) => (
              <Stack key={i} direction="row" spacing={1} alignItems="center">
                <TextField label="Place Code" size="small" value={b.placeCode}
                  onChange={(e) => update(i, { placeCode: e.target.value })} />
                <TextField label="Place Name" size="small" value={b.placeName}
                  onChange={(e) => update(i, { placeName: e.target.value })} />
                <TextField label="Address" size="small" value={b.address}
                  onChange={(e) => update(i, { address: e.target.value })} />
                <TextField label="PIN" size="small" value={b.postalCode}
                  inputProps={{ maxLength: 6 }}
                  onChange={(e) => update(i, { postalCode: e.target.value })} />
                <TextField label="Start" type="date" size="small" InputLabelProps={{ shrink: true }}
                  value={b.startDate} onChange={(e) => update(i, { startDate: e.target.value })} />
                <TextField label="End" type="date" size="small" InputLabelProps={{ shrink: true }}
                  value={b.endDate ?? ''} onChange={(e) => update(i, { endDate: e.target.value || null })} />
                <IconButton aria-label="remove branch" onClick={() => remove(i)}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
          </Stack>
          <Button startIcon={<AddIcon />} onClick={add} sx={{ mt: 1 }}>Add Branch</Button>
        </Box>
      );
    }
    ```

    **CREATE `src/components/draweeBank/RestrictionPanel.tsx`**
    ```tsx
    import { Box, FormControlLabel, Stack, Switch, TextField, Typography } from '@mui/material';
    import type { RestrictionDetailFormData } from '../../models/draweeBank.model';

    interface Props {
      restriction: RestrictionDetailFormData | null;
      onChange: (restriction: RestrictionDetailFormData | null) => void;
    }

    const empty: RestrictionDetailFormData = { clearingDays: 0, startDate: '', endDate: '' };

    export default function RestrictionPanel({ restriction, onChange }: Props) {
      const enabled = restriction !== null;
      const patch = (p: Partial<RestrictionDetailFormData>) =>
        onChange({ ...(restriction ?? empty), ...p });

      return (
        <Box>
          <Typography variant="subtitle1" gutterBottom>Restriction Details</Typography>
          <FormControlLabel
            control={<Switch checked={enabled}
              onChange={(e) => onChange(e.target.checked ? { ...empty } : null)} />}
            label="Has restriction"
          />
          {enabled && (
            <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
              <TextField label="Clearing Days" type="number" size="small"
                inputProps={{ min: 0, max: 365 }}
                value={restriction!.clearingDays}
                onChange={(e) => patch({ clearingDays: Number(e.target.value) })} />
              <TextField label="Start" type="date" size="small" InputLabelProps={{ shrink: true }}
                value={restriction!.startDate} onChange={(e) => patch({ startDate: e.target.value })} />
              <TextField label="End" type="date" size="small" InputLabelProps={{ shrink: true }}
                value={restriction!.endDate} onChange={(e) => patch({ endDate: e.target.value })} />
            </Stack>
          )}
        </Box>
      );
    }
    ```
    - _Requirements: 3.1, 3.5, 3.7, 4.1, 4.4_

  - [ ] 12.3 Implement the Add/Edit dialogs

    **CREATE `src/components/draweeBank/DraweeBankAddDialog.tsx`**
    ```tsx
    import { useState } from 'react';
    import {
      Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider,
      FormControlLabel, Stack, Switch, TextField,
    } from '@mui/material';
    import DraweeBranchSubGrid from './DraweeBranchSubGrid';
    import RestrictionPanel from './RestrictionPanel';
    import type { DraweeBankFormData } from '../../models/draweeBank.model';

    interface Props {
      open: boolean;
      onClose: () => void;
      onSave: (data: DraweeBankFormData) => Promise<void>;
    }

    const emptyForm: DraweeBankFormData = {
      bankCode: '', bankName: '', isActive: true, branches: [], restriction: null,
    };

    export default function DraweeBankAddDialog({ open, onClose, onSave }: Props) {
      const [form, setForm] = useState<DraweeBankFormData>(emptyForm);
      const [saving, setSaving] = useState(false);

      const clear = () => setForm(emptyForm);
      const handleSave = async () => {
        setSaving(true);
        try {
          await onSave(form);   // list refresh + close handled by the page on success
          clear();
        } finally {
          setSaving(false);
        }
      };

      return (
        <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
          <DialogTitle>Add Drawee Bank</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Stack direction="row" spacing={2}>
                <TextField label="Bank Code" value={form.bankCode}
                  inputProps={{ maxLength: 20 }}
                  onChange={(e) => setForm({ ...form, bankCode: e.target.value })} />
                <TextField label="Bank Name" fullWidth value={form.bankName}
                  inputProps={{ maxLength: 150 }}
                  onChange={(e) => setForm({ ...form, bankName: e.target.value })} />
                <FormControlLabel
                  control={<Switch checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />}
                  label="Active" />
              </Stack>
              <Divider />
              <DraweeBranchSubGrid branches={form.branches}
                onChange={(branches) => setForm({ ...form, branches })} />
              <Divider />
              <RestrictionPanel restriction={form.restriction}
                onChange={(restriction) => setForm({ ...form, restriction })} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={clear}>Clear</Button>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={saving} onClick={handleSave}>Save</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

    **CREATE `src/components/draweeBank/DraweeBankEditDialog.tsx`**
    ```tsx
    import { useEffect, useState } from 'react';
    import {
      Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, Stack, TextField,
    } from '@mui/material';
    import DraweeBranchSubGrid from './DraweeBranchSubGrid';
    import RestrictionPanel from './RestrictionPanel';
    import type { DraweeBank, DraweeBankUpdateData } from '../../models/draweeBank.model';

    interface Props {
      open: boolean;
      bank: DraweeBank | null;
      onClose: () => void;
      onSave: (id: string, data: DraweeBankUpdateData) => Promise<void>;
    }

    export default function DraweeBankEditDialog({ open, bank, onClose, onSave }: Props) {
      const [form, setForm] = useState<DraweeBankUpdateData>({ bankName: '', branches: [], restriction: null });
      const [saving, setSaving] = useState(false);

      useEffect(() => {
        if (bank) {
          setForm({
            bankName: bank.bankName,
            branches: bank.branches.map((b) => ({
              placeCode: b.placeCode, placeName: b.placeName, address: b.address,
              postalCode: b.postalCode, startDate: b.startDate, endDate: b.endDate,
            })),
            restriction: bank.restriction
              ? { clearingDays: bank.restriction.clearingDays, startDate: bank.restriction.startDate, endDate: bank.restriction.endDate }
              : null,
          });
        }
      }, [bank]);

      const handleSave = async () => {
        if (!bank) return;
        setSaving(true);
        try {
          await onSave(bank.id, form);
        } finally {
          setSaving(false);
        }
      };

      return (
        <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
          <DialogTitle>Edit Drawee Bank</DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Stack direction="row" spacing={2}>
                {/* Bank Code is immutable on edit */}
                <TextField label="Bank Code" value={bank?.bankCode ?? ''} InputProps={{ readOnly: true }} disabled />
                <TextField label="Bank Name" fullWidth value={form.bankName}
                  inputProps={{ maxLength: 150 }}
                  onChange={(e) => setForm({ ...form, bankName: e.target.value })} />
              </Stack>
              <Divider />
              <DraweeBranchSubGrid branches={form.branches}
                onChange={(branches) => setForm({ ...form, branches })} />
              <Divider />
              <RestrictionPanel restriction={form.restriction}
                onChange={(restriction) => setForm({ ...form, restriction })} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={saving} onClick={handleSave}>Save</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```
    - _Requirements: 1.1, 3.1, 3.7, 4.1, 4.4, 5.1, 5.5_

  - [ ] 12.4 Implement the Challan Rules and Audit dialogs

    **CREATE `src/components/draweeBank/ChallanRuleDialog.tsx`**
    ```tsx
    import { useEffect, useState } from 'react';
    import {
      Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel,
      List, ListItem, ListItemText, Stack, Switch, TextField, Typography,
    } from '@mui/material';
    import { draweeBankService } from '../../services/draweeBank.service';
    import type { DraweeBank, ChallanRule, ChallanRuleFormData } from '../../models/draweeBank.model';

    interface Props {
      open: boolean;
      bank: DraweeBank | null;
      onClose: () => void;
      onCreate: (bankId: string, data: ChallanRuleFormData) => Promise<void>;
    }

    const emptyRule: ChallanRuleFormData = {
      ruleCode: '', formatPattern: '', validationExpression: '', routingTarget: '', isActive: true,
    };

    export default function ChallanRuleDialog({ open, bank, onClose, onCreate }: Props) {
      const [form, setForm] = useState<ChallanRuleFormData>(emptyRule);
      const [saving, setSaving] = useState(false);
      // Rules are decoupled from the bank object — loaded on their own via getChallanRules.
      const [rules, setRules] = useState<ChallanRule[]>([]);

      const loadRules = async () => {
        if (!bank) return;
        try {
          setRules(await draweeBankService.getChallanRules(bank.id));
        } finally {
          // On error the shared interceptor toasts; leave the list unchanged.
        }
      };

      // (Re)load the bank's rules whenever the dialog opens for a bank.
      useEffect(() => {
        if (open && bank) {
          setForm(emptyRule);
          void loadRules();
        }
      }, [open, bank]);

      const handleCreate = async () => {
        if (!bank) return;
        setSaving(true);
        try {
          await onCreate(bank.id, form);
          setForm(emptyRule);
          await loadRules();   // refresh from the standalone rules endpoint
        } finally {
          setSaving(false);
        }
      };

      return (
        <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
          <DialogTitle>Challan Rules — {bank?.bankName}</DialogTitle>
          <DialogContent>
            <Typography variant="subtitle2" sx={{ mt: 1 }}>Existing rules</Typography>
            <List dense>
              {rules.map((r) => (
                <ListItem key={r.id}>
                  <ListItemText primary={`${r.ruleCode} → ${r.routingTarget}`}
                    secondary={`${r.formatPattern} | ${r.validationExpression} | ${r.isActive ? 'Active' : 'Inactive'}`} />
                </ListItem>
              ))}
            </List>
            <Typography variant="subtitle2">Add rule</Typography>
            <Stack spacing={1} sx={{ mt: 1 }}>
              <TextField label="Rule Code" value={form.ruleCode} inputProps={{ maxLength: 50 }}
                onChange={(e) => setForm({ ...form, ruleCode: e.target.value })} />
              <TextField label="Format Pattern" value={form.formatPattern} inputProps={{ maxLength: 500 }}
                onChange={(e) => setForm({ ...form, formatPattern: e.target.value })} />
              <TextField label="Validation Expression" value={form.validationExpression} inputProps={{ maxLength: 500 }}
                onChange={(e) => setForm({ ...form, validationExpression: e.target.value })} />
              <TextField label="Routing Target" value={form.routingTarget} inputProps={{ maxLength: 200 }}
                onChange={(e) => setForm({ ...form, routingTarget: e.target.value })} />
              <FormControlLabel
                control={<Switch checked={form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />}
                label="Active" />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Close</Button>
            <Button variant="contained" disabled={saving} onClick={handleCreate}>Add Rule</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

    **CREATE `src/components/draweeBank/DraweeBankAuditDialog.tsx`**
    ```tsx
    import {
      Dialog, DialogContent, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow, Button, DialogActions,
    } from '@mui/material';
    import type { DraweeBankAuditEntry } from '../../models/draweeBank.model';

    interface Props {
      open: boolean;
      entries: DraweeBankAuditEntry[];
      onClose: () => void;
    }

    export default function DraweeBankAuditDialog({ open, entries, onClose }: Props) {
      return (
        <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
          <DialogTitle>Audit Trail</DialogTitle>
          <DialogContent>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Action</TableCell>
                  <TableCell>Before</TableCell>
                  <TableCell>After</TableCell>
                  <TableCell>Changed By</TableCell>
                  <TableCell>When (UTC)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {entries.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell>{e.action}</TableCell>
                    <TableCell>{e.beforeSnapshot ?? '—'}</TableCell>
                    <TableCell>{e.afterSnapshot ?? '—'}</TableCell>
                    <TableCell>{e.changedBy}</TableCell>
                    <TableCell>{new Date(e.changedAtUtc).toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Close</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```
    - _Requirements: 6.1, 6.5, 7.4, 7.5_

  - [ ] 12.5 Implement the page

    **CREATE `src/pages/DraweeBankMaster.tsx`**
    ```tsx
    import { useCallback, useEffect, useRef, useState } from 'react';
    import { Box, Button, Stack, TextField, Typography } from '@mui/material';
    import AddIcon from '@mui/icons-material/Add';
    import { draweeBankService } from '../services/draweeBank.service';
    import type {
      DraweeBank, DraweeBankFormData, DraweeBankUpdateData, ChallanRuleFormData, DraweeBankAuditEntry,
    } from '../models/draweeBank.model';
    import DraweeBankGrid from '../components/draweeBank/DraweeBankGrid';
    import DraweeBankAddDialog from '../components/draweeBank/DraweeBankAddDialog';
    import DraweeBankEditDialog from '../components/draweeBank/DraweeBankEditDialog';
    import ChallanRuleDialog from '../components/draweeBank/ChallanRuleDialog';
    import DraweeBankAuditDialog from '../components/draweeBank/DraweeBankAuditDialog';

    export default function DraweeBankMaster() {
      const [rows, setRows] = useState<DraweeBank[]>([]);
      const [rowCount, setRowCount] = useState(0);
      const [loading, setLoading] = useState(false);
      const [search, setSearch] = useState('');
      const [page, setPage] = useState(1);
      const [pageSize, setPageSize] = useState(20);

      const [addOpen, setAddOpen] = useState(false);
      const [editBank, setEditBank] = useState<DraweeBank | null>(null);
      const [rulesBank, setRulesBank] = useState<DraweeBank | null>(null);
      const [auditEntries, setAuditEntries] = useState<DraweeBankAuditEntry[] | null>(null);

      const debounceRef = useRef<ReturnType<typeof setTimeout>>();

      const load = useCallback(async (s: string, p: number, ps: number) => {
        setLoading(true);
        try {
          const res = await draweeBankService.getPaged({ search: s, page: p, pageSize: ps });
          setRows(res.data);
          setRowCount(res.total);
        } finally {
          // On error the shared interceptor toasts; leave the list unchanged.
          setLoading(false);
        }
      }, []);

      useEffect(() => { load(search, page, pageSize); }, [page, pageSize, load]);

      // Debounced search.
      useEffect(() => {
        if (debounceRef.current) clearTimeout(debounceRef.current);
        debounceRef.current = setTimeout(() => {
          setPage(1);
          load(search, 1, pageSize);
        }, 300);
        return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
      }, [search, pageSize, load]);

      const handleCreate = async (data: DraweeBankFormData) => {
        await draweeBankService.create(data);
        setAddOpen(false);
        await load(search, page, pageSize);
      };
      const handleUpdate = async (id: string, data: DraweeBankUpdateData) => {
        await draweeBankService.update(id, data);
        setEditBank(null);
        await load(search, page, pageSize);
      };
      const handleCreateRule = async (bankId: string, data: ChallanRuleFormData) => {
        // Rules are decoupled from the bank; the dialog reloads them via getChallanRules on success.
        await draweeBankService.createChallanRule(bankId, data);
      };
      const openAudit = async (bank: DraweeBank) => {
        const entries = await draweeBankService.getAuditTrail(bank.id);
        setAuditEntries(entries);
      };

      return (
        <Box sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>Drawee Bank Master</Typography>
          <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
            <TextField label="Search by code or name" size="small" value={search}
              onChange={(e) => setSearch(e.target.value)} sx={{ width: 320 }} />
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>
              Add Drawee Bank
            </Button>
          </Stack>

          <DraweeBankGrid
            rows={rows}
            rowCount={rowCount}
            loading={loading}
            page={page}
            pageSize={pageSize}
            onPageChange={(p, ps) => { setPage(p); setPageSize(ps); }}
            onEdit={setEditBank}
            onRules={setRulesBank}
            onAudit={openAudit}
          />

          <DraweeBankAddDialog open={addOpen} onClose={() => setAddOpen(false)} onSave={handleCreate} />
          <DraweeBankEditDialog open={editBank !== null} bank={editBank}
            onClose={() => setEditBank(null)} onSave={handleUpdate} />
          <ChallanRuleDialog open={rulesBank !== null} bank={rulesBank}
            onClose={() => setRulesBank(null)} onCreate={handleCreateRule} />
          <DraweeBankAuditDialog open={auditEntries !== null} entries={auditEntries ?? []}
            onClose={() => setAuditEntries(null)} />
        </Box>
      );
    }
    ```
    - _Requirements: 1.1, 3.1, 4.1, 5.1, 6.1, 7.4, 8.1, 8.2, 8.3_

  - [ ] 12.6 Wire the route and navigation

    **MODIFY `src/App.tsx`** — add the import and the route inside the existing `ProtectedRoute`/`Layout` group (alongside `/nationalities`):
    ```tsx
    import DraweeBankMaster from './pages/DraweeBankMaster';
    ```
    ```tsx
    <Route path="/drawee-banks" element={<DraweeBankMaster />} />
    ```

    **MODIFY `src/components/Layout.tsx`** — add the icon import and a nav item to the **Administration** `menuGroups` entry:
    ```tsx
    import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
    ```
    ```tsx
    { text: 'Drawee Bank Master', icon: <AccountBalanceIcon />, path: '/drawee-banks' },
    ```
    - _Requirements: 8.1, 9.3_

  - [ ]* 12.7 Write page/component tests (Vitest + RTL)
    - Mirror `NationalityMaster.test.tsx`: render the grid; open Add/Edit/Challan-rule/Audit dialogs; submit create (with branches + restriction) and update; add/remove branches in the sub-grid; configure a challan rule. Assert the Add/Edit bank dialogs contain **no** challan-rule inputs, and that opening the Challan-rule dialog lists rules fetched via `getChallanRules(bankId)` (not read off the bank object) and refreshes that list after a create. Surface error paths (duplicate bank/place/rule code 409, bad PIN / out-of-range clearing-days 400) asserting the list is unchanged on error and the toast message is read from `error.response.data.message`.
    - _Requirements: 1.1, 2.2, 3.3, 3.6, 4.2, 5.1, 5.5, 6.1, 6.4, 7.4, 8.1_

- [ ] 13. Final checkpoint - all builds and tests green
  - Run `dotnet build` / `dotnet test` in the backend and `npm run build` / `npm test` in `Finnova-UI`. Ensure all tests pass, ask the user if questions arise.

## Notes

- This is a **copy-paste** tasks file: every CREATE block holds the complete file contents and every
  MODIFY block holds the exact snippet plus where it goes. Apply tasks top-to-bottom in order.
- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core
  implementation tasks are never optional. Top-level tasks are never marked optional.
- Each task references the specific requirements it satisfies; each property test references its
  correctness property number from the design, for traceability.
- Property tests use FsCheck.Xunit (`[Property(MaxTest = 200)]`, min 100 iterations); each of the 17
  correctness properties is implemented by exactly one property-based test, tagged
  `// Feature: drawee-bank-challan-rules-master, Property {n}: {property text}`, over the in-memory
  repositories from task 10.1.
- Edge/example behavior and authorization (R9) are covered by plain xUnit facts/theories and
  `WebApplicationFactory<Program>` integration tests, not property tests.
- The SystemAdmin host, JWT scheme, `SystemAdmin` policy, `ValidationBehavior`, generic
  `IRepository<T>`/`RepositoryBase<T>`, `ApplyConfigurationsFromAssembly`, and `PaginatedResponse<T>`
  already exist and are reused, not re-created.
- Frontend tasks (11, 12) are implemented in the separate `Finnova-UI` repository at
  `E:\Finnova\Finnova-UI\Finnova-UI`, not in this backend workspace. India-only / English-only
  throughout (single `BankName`/`PlaceName`, six-digit Indian PINs, `HDFC` / `Mumbai` / `400001`
  sample data; no bilingual/Arabic fields, no RTL).
- The challan Format Pattern / Validation Expression "parseable" rule (R6.8) uses a placeholder
  grammar (`ChallanExpression.IsParseable`); replace that single method when a grammar is agreed.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "11.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "2.2", "11.2"] },
    { "id": 2, "tasks": ["3.1", "6.1", "6.2", "11.3", "11.4"] },
    { "id": 3, "tasks": ["3.2", "5.1", "11.5"] },
    { "id": 4, "tasks": ["4.1", "5.2", "5.3", "11.6"] },
    { "id": 5, "tasks": ["5.4", "7.1", "7.2", "12.1", "12.2"] },
    { "id": 6, "tasks": ["7.3", "7.11", "7.16", "7.17", "7.20", "7.21", "7.27", "10.1", "12.3", "12.4"] },
    { "id": 7, "tasks": ["7.4", "7.5", "7.6", "7.7", "7.8", "7.9", "7.10", "7.12", "7.13", "7.14", "7.15", "7.18", "7.19", "7.22", "7.23", "7.24", "7.25", "7.26", "8.1", "12.5"] },
    { "id": 8, "tasks": ["8.2", "10.2", "10.3", "12.6", "12.7"] },
    { "id": 9, "tasks": ["8.3", "10.4", "10.5"] }
  ]
}
```
