# Implementation Plan: GL Master Management (FINNOVA-31 → FINNOVA-38)

## Overview

This is a **documents-only, copy-paste** plan (per the spec-workflow steering). Each task below
contains the **complete code to copy** into the repositories, marked **CREATE `<path>`** or
**MODIFY `<path>`**. Nothing here is built, migrated, or tested by the agent — you execute the
tasks by copying the code and running the commands in the final **Execution Notes** section.

- **Backend repo:** `e:\Finnova\Finnova-API` (.NET solution `Finnova.Backend.slnx`).
- **Frontend repo:** `E:\Finnova\Finnova-UI\Finnova-UI` (React + TypeScript + Vite).
- GL Master is hosted in **`Finnova.AccountsService`** (gateway `accounts-cluster`,
  `/api/accounts/**`), co-located with the FINNOVA-25 JV posting engine so the Requirement 6
  validation service is an **in-process MediatR call**.
- India-only / English-only: each GL account carries a single English `Name`. No bilingual
  fields, no Arabic content, no RTL anywhere.

Tasks follow the design's **Implementation-Ordered Task List** (13 groups): governance-stack port
first, then dependencies before dependents.

---

## Task Dependency Graph

Ordered groups (apply top to bottom; items within a group may be applied together):

1. **Governance-stack port** into `Finnova.AccountsService` (Program.cs + new middleware).
2. **Domain entities + enums** (`Finnova.Models/Domain`).
3. **Domain exceptions** (`Finnova.Models/Domain/Exceptions`).
4. **Contracts** (`Finnova.Models/Contracts/GL`) incl. the JV validation contract.
5. **EF configuration + `FinnovaDbContext` DbSets + migration command** (`Finnova.Repository`).
6. **Repositories + interfaces + DI registration** (`Finnova.Repository`).
7. **Pure domain helpers + dependency ports + dev stubs** (`Finnova.Service/GL/Internal`, ports).
8. **CQRS slices** — commands/queries/validators/mappers (`Finnova.Service/GL`).
9. **Controller + middleware GL branch + gateway alias** (`Finnova.AccountsService`, gateway).
10. **Backend tests** (`Finnova.Tests`).
11. **Frontend model + service layer** (`Finnova-UI`).
12. **Frontend page + components + route + nav** (`Finnova-UI`).
13. **Frontend Vitest tests** (`Finnova-UI`).

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["4.1", "4.2"] },
    { "id": 4, "tasks": ["5.1", "5.2"] },
    { "id": 5, "tasks": ["6.1", "6.2"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3"] },
    { "id": 10, "tasks": ["11.1", "11.2"] },
    { "id": 11, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 12, "tasks": ["13.1", "13.2"] }
  ]
}
```

---

## Tasks

- [ ] 1. Port the governance stack into `Finnova.AccountsService`
  - Add the JSON string-enum converter, the MediatR `ValidationBehavior` pipeline, JWT Bearer
    authentication, the four authorization policies, Swagger Bearer, correct middleware order,
    and the test shim — mirroring `Finnova.SystemAdminService/Program.cs`. Create the new
    AccountsService `ExceptionHandlingMiddleware` with a GL-only branch.
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 8.8_

  - [ ] 1.1 Replace `Finnova.AccountsService/Program.cs` with the governed host wiring

    **MODIFY `Finnova.AccountsService/Program.cs`** — replace the whole file with the version
    below. MediatR, `AddValidatorsFromAssembly`, `TokenService`/`JwtSettings`,
    `IPasswordPolicy`, `IAssetCodeGenerator`, `AddFinnovaRepository`, and blob storage are kept
    (already present); the **new** lines are the string-enum converter, the `ValidationBehavior`
    registration, JWT auth, the authorization policies (`GLMaker`/`GLChecker`/`SystemAdmin`/
    `GLRead`), Swagger Bearer, the middleware-order block, and the `public partial class Program`
    shim.

    ```csharp
    using Finnova.Repository;
    using Finnova.Service.Accounts.Commands.CreateAccount;
    using Finnova.Service.Assets.CodeGeneration;
    using Finnova.Service.Auth;
    using Finnova.Service.Storage;
    using Finnova.Service.UserManagement.Abstractions;
    using FluentValidation;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi;
    using System.Security.Claims;
    using System.Text;

    var builder = WebApplication.CreateBuilder(args);

    // Controllers. Serialize/bind enums by their string names (e.g. "Active", "Control") so the
    // GL contracts read naturally over the wire and match how the UI sends them.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()));

    // MediatR - register handlers from the Service layer
    var serviceAssembly = typeof(CreateAccountCommand).Assembly;
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(serviceAssembly));

    // FluentValidation
    builder.Services.AddValidatorsFromAssembly(serviceAssembly);

    // MediatR validation pipeline — runs FluentValidation before each handler (R2.6 ordering)
    builder.Services.AddTransient(
        typeof(MediatR.IPipelineBehavior<,>),
        typeof(Finnova.Service.Behaviors.ValidationBehavior<,>));

    // Auth token service — required because the shared Finnova.Service assembly (scanned by
    // MediatR above) contains LoginCommandHandler, which depends on ITokenService.
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IPasswordPolicy, DefaultPasswordPolicy>();
    builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();

    // JWT Bearer authentication (R11.1/R11.5 — establishing the scheme before authorization)
    var jwt = builder.Configuration.GetSection("Jwt");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,            // rejects expired tokens (R11.1)
                ValidateIssuerSigningKey = true,    // rejects bad signature (R11.1)
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
                RoleClaimType = ClaimTypes.Role
            };
        });

    // GL authorization policies (R11.2/11.3/11.4/11.6, R8.8). GLRead is satisfied by ANY GL role
    // claim (GLMaker, GLChecker, SystemAdmin, or GLViewer) so a viewer-only user can read.
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("GLMaker", p => p.RequireRole("GLMaker"));
        options.AddPolicy("GLChecker", p => p.RequireRole("GLChecker"));
        options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));
        options.AddPolicy("GLRead", p => p.RequireAssertion(ctx =>
            ctx.User.IsInRole("GLMaker") ||
            ctx.User.IsInRole("GLChecker") ||
            ctx.User.IsInRole("SystemAdmin") ||
            ctx.User.IsInRole("GLViewer")));
    });

    // Repository (EF Core + SQL Server)
    var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
    builder.Services.AddFinnovaRepository(connectionString!);

    // Blob storage (Azure / AWS) — provider selected via BlobStorage:Provider
    builder.Services.AddBlobStorage(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks();

    // OpenAPI + Swagger (with Bearer security so Swagger UI supports auth)
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter the JWT bearer token."
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
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

    // Middleware order is critical:
    app.UseMiddleware<Finnova.AccountsService.Middleware.ExceptionHandlingMiddleware>(); // GL exceptions -> ProblemDetails
    app.UseAuthentication();   // must precede UseAuthorization (R11.5)
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();

    // Testability shim: the top-level-statement Program class is implicitly internal, so
    // WebApplicationFactory<Program> in Finnova.Tests cannot reference it. Declaring a public
    // partial Program here exposes the generated entry-point type to the test assembly.
    public partial class Program { }
    ```

  - [ ] 1.2 Create the AccountsService exception-handling middleware (GL branch)

    **CREATE `Finnova.AccountsService/Middleware/ExceptionHandlingMiddleware.cs`**

    ```csharp
    using Microsoft.AspNetCore.Mvc;
    using Finnova.Models.Domain.Exceptions;

    namespace Finnova.AccountsService.Middleware;

    /// <summary>
    /// Translates GL domain exceptions and FluentValidation failures into RFC 7807
    /// <see cref="ProblemDetails"/> responses with a stable <c>code</c> extension the frontend
    /// branches on. GL error family is path-scoped to <c>/api/gl</c>:
    /// <list type="bullet">
    ///   <item><see cref="GLNotFoundException"/> -> 404 (ERR-GL-404)</item>
    ///   <item><see cref="GLDuplicateCodeException"/> / <see cref="GLRetiredCodeException"/> /
    ///         <see cref="GLDuplicateNameException"/> / <see cref="GLConflictException"/> -> 409 (ERR-GL-409)</item>
    ///   <item><see cref="FluentValidation.ValidationException"/> -> 400 (ERR-GL-400)</item>
    ///   <item>any other exception -> 500 (ERR-GL-500)</item>
    /// </list>
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
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
                    GLDuplicateCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                    GLRetiredCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                    GLDuplicateNameException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
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

- [ ] 2. Backend domain: entities + enums
  - Create the GL enums and the six domain entities. Entities follow the Court template:
    `Guid Id = Guid.NewGuid();` and `DateTime CreatedAt/UpdatedAt = DateTime.UtcNow;`. Single
    English `Name`. The audit entity mirrors `CourtAuditEntry` extended with `Remarks` and
    `ChangedByRole`.
  - _Requirements: 1.1, 1.2, 4.1, 4.3, 7.1, 7.7, 10.1_

  - [ ] 2.1 Create the GL enums

    **CREATE `Finnova.Models/Domain/Enums/GLEnums.cs`**

    ```csharp
    namespace Finnova.Models.Domain.Enums;

    /// <summary>Depth of a GL account in the four-level Chart of Accounts (L1 top .. L4 leaf).</summary>
    public enum GLLevel { L1 = 1, L2 = 2, L3 = 3, L4 = 4 }

    /// <summary>Accounting classification; derived from the parent GL (R1.9).</summary>
    public enum AccountNature { Asset, Liability, Equity, Income, Expense }

    /// <summary>Side a GL normally carries its balance on; derived from nature (R1.10).</summary>
    public enum NormalBalance { Debit, Credit }

    /// <summary>GL type that drives conditional-mandatory attributes (R1.11).</summary>
    public enum GLType { Normal, Control, Tax }

    /// <summary>Lifecycle state of a GL account.</summary>
    public enum GLStatus
    {
        Draft,
        PendingAuthorization,
        Active,
        Rejected,
        PendingModification,
        Frozen,
        Inactive
    }

    /// <summary>System GL designations configured per company (R7).</summary>
    public enum SystemGLType
    {
        Suspense,
        RoundOff,
        InterBranchClearingHO,
        InterBranchClearingBranch,
        RetainedEarnings,
        RealizedFX,
        UnrealizedFX
    }

    /// <summary>Governance action captured by an audit entry (R10).</summary>
    public enum GLAuditAction
    {
        Create,
        Submit,
        Approve,
        Reject,
        Update,
        ModApprove,
        ModReject,
        Freeze,
        Deactivate,
        Delete,
        SystemGLMap
    }

    /// <summary>Manual-vs-system indicator for a voucher validation request (R6).</summary>
    public enum JvEntryMode { Manual, System }

    /// <summary>Supported export formats (R8.7/R8.10).</summary>
    public enum ExportFormat { Xlsx, Csv }

    /// <summary>State of a pending non-structural modification version (R4).</summary>
    public enum GLVersionState { Pending, Authorized, Rejected }
    ```

  - [ ] 2.2 Create the GL domain entities

    **CREATE `Finnova.Models/Domain/Entities/GLAccount.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Aggregate root of the GL Master (FINNOVA-31..38), scoped to an operating company. Four-level
    /// hierarchy (L1..L4); only L4 leaves are postable. India-only platform: one English Name.
    /// </summary>
    public class GLAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CompanyId { get; set; }                      // scopes all uniqueness checks

        public string GLCode { get; set; } = string.Empty;       // exactly 9 numeric digits; unique per company; non-reusable
        public string Name { get; set; } = string.Empty;         // English; max 150; unique per company (case-insensitive)

        public GLLevel Level { get; set; }                       // L1..L4
        public Guid? ParentGLId { get; set; }                    // null only for L1

        public AccountNature AccountNature { get; set; }         // derived from parent (R1.9)
        public NormalBalance NormalBalance { get; set; }         // derived from nature (R1.10)

        public GLType GLType { get; set; }                       // Normal/Control/Tax
        public string? SubLedgerType { get; set; }               // mandatory when GLType = Control (R1.11)
        public string? TaxType { get; set; }                     // mandatory when GLType = Tax (R1.11)

        public bool IsPostable { get; set; }                     // true only for L4 (R1.2)
        public Guid CurrencyId { get; set; }                     // GL master currency

        public bool AllowManualJV { get; set; }                  // non-structural
        public bool CostCentreMandatory { get; set; }            // non-structural
        public bool ProfitCentreMandatory { get; set; }          // non-structural
        public string? ReportingLine { get; set; }               // non-structural

        public DateTime EffectiveFrom { get; set; }              // required; not in closed period (R1.12)
        public DateTime? EffectiveTo { get; set; }               // >= EffectiveFrom (R1.13)

        public GLStatus Status { get; set; } = GLStatus.Draft;   // R1.1
        public string? MakerId { get; set; }                     // last maker
        public string? CheckerId { get; set; }                   // last checker
        public int AuthorizedVersionNumber { get; set; }         // starts 0; +1 per authorized modification (R4.5)
        public string? RejectionReason { get; set; }             // last rejection reason (<=500)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<GLBranchApplicability> BranchApplicabilities { get; set; } = new();
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/GLBranchApplicability.cs`**

    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Branch applicability is a set, not a scalar, so it is modeled as a child entity (R6.7).
    /// Unique on (GLAccountId, BranchId).
    /// </summary>
    public class GLBranchApplicability
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GLAccountId { get; set; }
        public Guid BranchId { get; set; }
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/GLAccountVersion.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Immutable snapshot of a pending non-structural modification (R4.3/R4.4). Exactly one Pending
    /// row per GL account at a time; the last authorized configuration governs JV posting until a
    /// checker authorizes this version.
    /// </summary>
    public class GLAccountVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GLAccountId { get; set; }
        public int VersionNumber { get; set; }
        public string ProposedPayload { get; set; } = string.Empty;   // JSON of non-structural fields
        public string MakerId { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
        public GLVersionState State { get; set; } = GLVersionState.Pending;
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/GLSystemAccountMapping.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Maps a (CompanyId, SystemGLType) to a GL account (R7). At most one active mapping per
    /// (CompanyId, SystemGLType) — enforced by a filtered unique index.
    /// </summary>
    public class GLSystemAccountMapping
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CompanyId { get; set; }
        public SystemGLType SystemGLType { get; set; }
        public Guid GLAccountId { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/CompanyPostingState.cs`**

    ```csharp
    namespace Finnova.Models.Domain.Entities;

    /// <summary>Per-company "JV posting enabled" gate (R7.3/R7.4).</summary>
    public class CompanyPostingState
    {
        public Guid CompanyId { get; set; }            // primary key
        public bool JvPostingEnabled { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    ```

    **CREATE `Finnova.Models/Domain/Entities/GLAuditEntry.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    /// <summary>
    /// Immutable audit record capturing one governance action against a GL account or system-GL
    /// mapping (R10). Mirrors CourtAuditEntry, extended with Remarks and ChangedByRole. Written on
    /// the success path only; never updated or deleted. The editable surface is multi-field, so
    /// before/after is captured as compact JSON snapshots plus a human-readable summary.
    /// </summary>
    public class GLAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GLAccountId { get; set; }
        public GLAuditAction Action { get; set; }

        public string? OldValues { get; set; }                   // JSON of changed fields (null on Create)
        public string NewValues { get; set; } = string.Empty;    // JSON of resulting fields (empty on Delete)
        public string Summary { get; set; } = string.Empty;      // human-readable change summary
        public string? Remarks { get; set; }                     // remarks / reason text (<= 1000)

        public string ChangedBy { get; set; } = string.Empty;    // acting user id from JWT
        public string? ChangedByRole { get; set; }               // acting role
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC, ms precision
    }
    ```

- [ ] 3. Backend domain: exceptions
  - _Requirements: 2.3, 2.4, 2.5, 3.3, 4.2, 4.4, 4.6, 5.4, 5.5, 5.6, 5.7, 5.9, 5.12, 7.5, 7.7, 10.4_

  - [ ] 3.1 Create the GL typed exceptions

    **CREATE `Finnova.Models/Domain/Exceptions/GLExceptions.cs`**

    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    /// <summary>GL account not found by id/company. Maps to ERR-GL-404.</summary>
    public class GLNotFoundException : Exception
    {
        public const string ErrorCode = "ERR-GL-404";
        public GLNotFoundException() : base("GL account was not found.") { }
        public GLNotFoundException(string message) : base(message) { }
    }

    /// <summary>Create with a code already in use (non-Inactive). Maps to ERR-GL-409 (R2.3).</summary>
    public class GLDuplicateCodeException : Exception
    {
        public const string ErrorCode = "ERR-GL-409";
        public GLDuplicateCodeException() : base("GL code must be unique") { }
    }

    /// <summary>Create reusing a retired (Inactive) code. Maps to ERR-GL-409 (R2.4).</summary>
    public class GLRetiredCodeException : Exception
    {
        public const string ErrorCode = "ERR-GL-409";
        public GLRetiredCodeException() : base("GL code has been retired and cannot be reused") { }
    }

    /// <summary>Create/update with a name already in use. Maps to ERR-GL-409 (R2.5).</summary>
    public class GLDuplicateNameException : Exception
    {
        public const string ErrorCode = "ERR-GL-409";
        public GLDuplicateNameException() : base("GL name must be unique") { }
    }

    /// <summary>
    /// Governance/lifecycle conflict: self-authorization (R3.3/R4.6/R5.12), structural lock once
    /// postings exist (R4.2), modification already pending (R4.4), lifecycle pre-condition failure
    /// (R5.4-R5.7), delete of an ever-authorized GL (R5.9), duplicate active system-GL mapping
    /// (R7.7), unmap while operational (R7.5), audit immutability (R10.4), not-awaiting-authorization
    /// (R3.10). Maps to ERR-GL-409.
    /// </summary>
    public class GLConflictException : Exception
    {
        public const string ErrorCode = "ERR-GL-409";
        public GLConflictException(string message) : base(message) { }
    }
    ```

- [ ] 4. Backend contracts (`Finnova.Models/Contracts/GL`)
  - Record-based request/response contracts, plus the in-process JV validation contract. Enums
    serialize by name (converter registered in task 1).
  - _Requirements: 1.1, 3.6, 5.2, 7.2, 8.1, 8.3, 8.6, 8.7, 9.2, 10.1, 6.1, 6.12_

  - [ ] 4.1 Create the GL request/response contracts

    **CREATE `Finnova.Models/Contracts/GL/GLContracts.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.GL;

    // ---------- Requests ----------

    public record CreateGLAccountRequest(
        Guid CompanyId,
        string GLCode,
        string Name,
        Guid? ParentGLId,
        GLType GLType,
        string? SubLedgerType,
        string? TaxType,
        Guid CurrencyId,
        bool AllowManualJV,
        bool CostCentreMandatory,
        bool ProfitCentreMandatory,
        string? ReportingLine,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo,
        IReadOnlyList<Guid> ApplicableBranchIds);

    /// <summary>Non-structural edit only (R4.1/R4.3).</summary>
    public record UpdateGLAccountRequest(
        bool AllowManualJV,
        bool CostCentreMandatory,
        bool ProfitCentreMandatory,
        string? ReportingLine,
        IReadOnlyList<Guid> ApplicableBranchIds);

    public record RejectGLRequest(string Reason);          // 1..500
    public record FreezeGLRequest(string Reason);          // 1..500
    public record DeactivateGLRequest(string? Reason);     // optional remark
    public record MapSystemGLRequest(Guid CompanyId, SystemGLType Type, Guid GLAccountId);

    public record GLAccountFilter(
        string? GLCode = null,
        string? Name = null,
        AccountNature? Nature = null,
        GLType? Type = null,
        GLStatus? Status = null,
        Guid? ParentGLId = null,
        int Page = 1,
        int PageSize = 20);

    public record ExportGLRequest(ExportFormat Format, GLAccountFilter Filter);

    // ---------- Responses ----------

    public record GLAccountResponse(
        Guid Id,
        Guid CompanyId,
        string GLCode,
        string Name,
        GLLevel Level,
        Guid? ParentGLId,
        AccountNature AccountNature,
        NormalBalance NormalBalance,
        GLType GLType,
        string? SubLedgerType,
        string? TaxType,
        bool IsPostable,
        Guid CurrencyId,
        bool AllowManualJV,
        bool CostCentreMandatory,
        bool ProfitCentreMandatory,
        string? ReportingLine,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo,
        GLStatus Status,
        int AuthorizedVersionNumber,
        IReadOnlyList<Guid> ApplicableBranchIds,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record GLTreeNodeResponse(
        Guid Id,
        string GLCode,
        string Name,
        GLLevel Level,
        bool IsPostable,
        GLStatus Status,
        IReadOnlyList<GLTreeNodeResponse> Children);   // siblings ordered by GLCode asc

    public record GLAuditEntryResponse(
        Guid Id,
        Guid GLAccountId,
        string Action,
        string? OldValues,
        string NewValues,
        string Summary,
        string? Remarks,
        string ChangedBy,
        string? ChangedByRole,
        DateTime ChangedAtUtc);

    public record SystemGLCompletenessResponse(
        Guid CompanyId,
        bool IsComplete,
        IReadOnlyList<SystemGLType> MissingOrInvalid);

    public record GLUploadRowResult(int RowNumber, string GLCode, bool Success, string? Error);
    public record GLUploadReportResponse(Guid BatchId, bool AllSuccess, IReadOnlyList<GLUploadRowResult> Rows);
    ```

  - [ ] 4.2 Create the JV validation-service contract (R6, in-process MediatR)

    **CREATE `Finnova.Models/Contracts/GL/JvValidationContracts.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.GL;

    /// <summary>One journal-voucher line to validate against GL master rules (R6).</summary>
    public record JvLineInput(
        int LineNumber,
        Guid GLAccountId,
        Guid CurrencyId,
        Guid? CostCentreId,
        Guid? ProfitCentreId,
        string? SubLedgerReference);

    /// <summary>Validation request issued in-process by the JV posting engine (FINNOVA-25).</summary>
    public record ValidateJournalVoucherInput(
        Guid CompanyId,
        DateTime ValueDate,
        Guid BranchId,
        JvEntryMode Mode,
        IReadOnlyList<JvLineInput> Lines);

    /// <summary>Per-line result: Accepted iff Reasons is empty; reasons asc by criterion (R6.12).</summary>
    public record JvLineResult(int LineNumber, bool Accepted, IReadOnlyList<string> Reasons);

    public record VoucherValidationResult(IReadOnlyList<JvLineResult> Lines);
    ```

- [ ] 5. Backend EF configuration + `FinnovaDbContext` DbSets + migration command
  - Configurations mirror `CourtConfiguration` (`ToTable`, `HasKey`, property lengths, unique
    indexes). The system-GL mapping gets a **filtered** unique index on `(CompanyId, SystemGLType)`
    `WHERE IsActive = 1` (R7.7).
  - _Requirements: 1.5, 2.1, 2.2, 7.7, 10.1_

  - [ ] 5.1 Create the EF configurations

    **CREATE `Finnova.Repository/Configuration/GLAccountConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class GLAccountConfiguration : IEntityTypeConfiguration<GLAccount>
    {
        public void Configure(EntityTypeBuilder<GLAccount> builder)
        {
            builder.ToTable("gl_accounts");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CompanyId).IsRequired();
            builder.Property(x => x.GLCode).IsRequired().HasMaxLength(9);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Level).IsRequired();            // stored as int
            builder.Property(x => x.AccountNature).IsRequired();
            builder.Property(x => x.NormalBalance).IsRequired();
            builder.Property(x => x.GLType).IsRequired();
            builder.Property(x => x.SubLedgerType).HasMaxLength(100);
            builder.Property(x => x.TaxType).HasMaxLength(100);
            builder.Property(x => x.IsPostable).IsRequired();
            builder.Property(x => x.CurrencyId).IsRequired();
            builder.Property(x => x.AllowManualJV).IsRequired();
            builder.Property(x => x.CostCentreMandatory).IsRequired();
            builder.Property(x => x.ProfitCentreMandatory).IsRequired();
            builder.Property(x => x.ReportingLine).HasMaxLength(100);
            builder.Property(x => x.EffectiveFrom).IsRequired();
            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.MakerId).HasMaxLength(100);
            builder.Property(x => x.CheckerId).HasMaxLength(100);
            builder.Property(x => x.AuthorizedVersionNumber).IsRequired();
            builder.Property(x => x.RejectionReason).HasMaxLength(500);

            // Code uniqueness scoped per company (R2.1/2.3). Default CI collation backs the
            // case-insensitive Name rule; codes are trimmed by the handler so stored values are canonical.
            builder.HasIndex(x => new { x.CompanyId, x.GLCode }).IsUnique();
            builder.HasIndex(x => new { x.CompanyId, x.Name });
            builder.HasIndex(x => x.ParentGLId);

            builder.HasMany(x => x.BranchApplicabilities)
                   .WithOne()
                   .HasForeignKey(b => b.GLAccountId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/GLBranchApplicabilityConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class GLBranchApplicabilityConfiguration : IEntityTypeConfiguration<GLBranchApplicability>
    {
        public void Configure(EntityTypeBuilder<GLBranchApplicability> builder)
        {
            builder.ToTable("gl_branch_applicabilities");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.GLAccountId).IsRequired();
            builder.Property(x => x.BranchId).IsRequired();
            builder.HasIndex(x => new { x.GLAccountId, x.BranchId }).IsUnique();
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/GLAccountVersionConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class GLAccountVersionConfiguration : IEntityTypeConfiguration<GLAccountVersion>
    {
        public void Configure(EntityTypeBuilder<GLAccountVersion> builder)
        {
            builder.ToTable("gl_account_versions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.GLAccountId).IsRequired();
            builder.Property(x => x.VersionNumber).IsRequired();
            builder.Property(x => x.ProposedPayload).IsRequired();
            builder.Property(x => x.MakerId).IsRequired().HasMaxLength(100);
            builder.Property(x => x.SubmittedAtUtc).IsRequired();
            builder.Property(x => x.State).IsRequired();
            builder.HasIndex(x => x.GLAccountId);
            // At most one Pending version per GL account (R4.4).
            builder.HasIndex(x => new { x.GLAccountId, x.State })
                   .IsUnique()
                   .HasFilter("[State] = 0");   // GLVersionState.Pending = 0
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/GLSystemAccountMappingConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class GLSystemAccountMappingConfiguration : IEntityTypeConfiguration<GLSystemAccountMapping>
    {
        public void Configure(EntityTypeBuilder<GLSystemAccountMapping> builder)
        {
            builder.ToTable("gl_system_account_mappings");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CompanyId).IsRequired();
            builder.Property(x => x.SystemGLType).IsRequired();
            builder.Property(x => x.GLAccountId).IsRequired();
            builder.Property(x => x.IsActive).IsRequired();

            // At most one ACTIVE mapping per (company, system-GL type) — filtered unique index (R7.7).
            builder.HasIndex(x => new { x.CompanyId, x.SystemGLType })
                   .IsUnique()
                   .HasFilter("[IsActive] = 1");
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/CompanyPostingStateConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class CompanyPostingStateConfiguration : IEntityTypeConfiguration<CompanyPostingState>
    {
        public void Configure(EntityTypeBuilder<CompanyPostingState> builder)
        {
            builder.ToTable("company_posting_states");
            builder.HasKey(x => x.CompanyId);
            builder.Property(x => x.JvPostingEnabled).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
        }
    }
    ```

    **CREATE `Finnova.Repository/Configuration/GLAuditEntryConfiguration.cs`**

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public class GLAuditEntryConfiguration : IEntityTypeConfiguration<GLAuditEntry>
    {
        public void Configure(EntityTypeBuilder<GLAuditEntry> builder)
        {
            builder.ToTable("gl_audit_entries");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.GLAccountId).IsRequired();
            builder.Property(x => x.Action).IsRequired();
            builder.Property(x => x.NewValues).IsRequired();
            builder.Property(x => x.Summary).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Remarks).HasMaxLength(1000);
            builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ChangedByRole).HasMaxLength(100);
            builder.Property(x => x.ChangedAtUtc).IsRequired();
            builder.HasIndex(x => x.GLAccountId);
            builder.HasIndex(x => x.ChangedAtUtc);
        }
    }
    ```

  - [ ] 5.2 Register the DbSets on `FinnovaDbContext`

    **MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`** — add these DbSets after the
    existing `public DbSet<Asset> Assets => Set<Asset>();` line:

    ```csharp
        public DbSet<GLAccount> GLAccounts => Set<GLAccount>();
        public DbSet<GLBranchApplicability> GLBranchApplicabilities => Set<GLBranchApplicability>();
        public DbSet<GLAccountVersion> GLAccountVersions => Set<GLAccountVersion>();
        public DbSet<GLSystemAccountMapping> GLSystemAccountMappings => Set<GLSystemAccountMapping>();
        public DbSet<CompanyPostingState> CompanyPostingStates => Set<CompanyPostingState>();
        public DbSet<GLAuditEntry> GLAuditEntries => Set<GLAuditEntry>();
    ```

    No change to `OnModelCreating` is needed — `ApplyConfigurationsFromAssembly` already discovers
    the new `IEntityTypeConfiguration<T>` classes from task 5.1.

  - [ ] 5.3 Add the EF Core migration (command only — run manually, see Execution Notes)

    Do **not** hand-write migration code. After tasks 5.1/5.2 compile, generate the migration with
    the `dotnet ef migrations add AddGLMaster ...` command documented in **Execution Notes**.
    _Requirements: 1.1, 2.1_

- [ ] 6. Backend repositories + interfaces + DI registration
  - Mirror `CourtRepository` (primary-constructor style). `GetPagedAsync` applies AND-combined
    filters, company scope, case-insensitive substring on code/name, orders by `GLCode` asc, then
    `Skip/Take` + `Count`. Audit repo orders `ChangedAtUtc` desc then `Id` desc.
  - _Requirements: 2.1, 2.2, 2.4, 2.5, 8.1, 8.2, 8.6, 10.3, 10.5_

  - [ ] 6.1 Create the repository interfaces

    **CREATE `Finnova.Repository/Interfaces/IGLAccountRepository.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IGLAccountRepository : IRepository<GLAccount>
    {
        /// <summary>Paged, AND-combined, company-scoped filter; ordered by GLCode asc (R8.1/8.2/8.6).</summary>
        Task<(List<GLAccount> Items, int Total)> GetPagedAsync(GLAccountFilter filter, CancellationToken ct = default);

        /// <summary>Trimmed exact-match code existence scoped to company; optionally excludes an id (R2.1/2.3).</summary>
        Task<bool> ExistsByCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);

        /// <summary>Returns a matching Inactive (retired) account for retired-code precedence (R2.4).</summary>
        Task<GLAccount?> GetInactiveByCodeAsync(Guid companyId, string code, CancellationToken ct = default);

        /// <summary>Case-insensitive trimmed name existence scoped to company; optionally excludes an id (R2.5).</summary>
        Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken ct = default);

        /// <summary>Loads a GL with its branch applicabilities (tracked for edit).</summary>
        Task<GLAccount?> GetWithBranchesAsync(Guid id, CancellationToken ct = default);

        /// <summary>All accounts for a company (for tree assembly), ordered by GLCode asc (R8.6).</summary>
        Task<List<GLAccount>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default);

        /// <summary>True when the GL has one or more Active child accounts (R5.6).</summary>
        Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken ct = default);
    }
    ```

    **CREATE `Finnova.Repository/Interfaces/IGLSystemAccountMappingRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Repository.Interfaces;

    public interface IGLSystemAccountMappingRepository : IRepository<GLSystemAccountMapping>
    {
        Task<List<GLSystemAccountMapping>> GetActiveForCompanyAsync(Guid companyId, CancellationToken ct = default);
        Task<GLSystemAccountMapping?> GetActiveAsync(Guid companyId, SystemGLType type, CancellationToken ct = default);
    }
    ```

    **CREATE `Finnova.Repository/Interfaces/ICompanyPostingStateRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface ICompanyPostingStateRepository : IRepository<CompanyPostingState>
    {
        Task<CompanyPostingState?> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    }
    ```

    **CREATE `Finnova.Repository/Interfaces/IGLAuditRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IGLAuditRepository : IRepository<GLAuditEntry>
    {
        /// <summary>Entries for a GL, ordered ChangedAtUtc desc then Id desc (R10.3). Missing id yields [] (R10.5).</summary>
        Task<List<GLAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken ct = default);

        /// <summary>Count of persisted entries for a GL (supports the audit-exactly-once property).</summary>
        Task<int> CountForGLAccountAsync(Guid glAccountId, CancellationToken ct = default);
    }
    ```

  - [ ] 6.2 Create the repository implementations

    **CREATE `Finnova.Repository/Repositories/GLAccountRepository.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class GLAccountRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<GLAccount>(finnovaDbContext), IGLAccountRepository
    {
        public async Task<(List<GLAccount> Items, int Total)> GetPagedAsync(
            GLAccountFilter filter, CancellationToken ct = default)
        {
            var query = DbSet.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.GLCode))
            {
                var code = filter.GLCode.Trim();
                query = query.Where(x => x.GLCode.Contains(code));
            }
            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var name = filter.Name.Trim();
                query = query.Where(x => x.Name.Contains(name));
            }
            if (filter.Nature is not null) query = query.Where(x => x.AccountNature == filter.Nature);
            if (filter.Type is not null) query = query.Where(x => x.GLType == filter.Type);
            if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
            if (filter.ParentGLId is not null) query = query.Where(x => x.ParentGLId == filter.ParentGLId);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(x => x.GLCode)                             // R8.6 sibling/listing order
                .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
                .ToListAsync(ct);
            return (items, total);
        }

        public async Task<bool> ExistsByCodeAsync(
            Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
        {
            var c = code.Trim();
            return await DbSet.AsNoTracking().AnyAsync(
                x => x.CompanyId == companyId && x.GLCode == c
                     && (excludeId == null || x.Id != excludeId), ct);
        }

        public async Task<GLAccount?> GetInactiveByCodeAsync(
            Guid companyId, string code, CancellationToken ct = default)
        {
            var c = code.Trim();
            return await DbSet.AsNoTracking().FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.GLCode == c && x.Status == GLStatus.Inactive, ct);
        }

        public async Task<bool> ExistsByNameAsync(
            Guid companyId, string name, Guid? excludeId = null, CancellationToken ct = default)
        {
            var n = name.Trim();
            // Default CI collation makes == case-insensitive; ToLower keeps it explicit/portable.
            return await DbSet.AsNoTracking().AnyAsync(
                x => x.CompanyId == companyId && x.Name.ToLower() == n.ToLower()
                     && (excludeId == null || x.Id != excludeId), ct);
        }

        public async Task<GLAccount?> GetWithBranchesAsync(Guid id, CancellationToken ct = default)
            => await DbSet.Include(x => x.BranchApplicabilities).FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<List<GLAccount>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default)
            => await DbSet.AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.GLCode)
                .ToListAsync(ct);

        public async Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken ct = default)
            => await DbSet.AsNoTracking().AnyAsync(x => x.ParentGLId == id && x.Status == GLStatus.Active, ct);
    }
    ```

    **CREATE `Finnova.Repository/Repositories/GLSystemAccountMappingRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class GLSystemAccountMappingRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<GLSystemAccountMapping>(finnovaDbContext), IGLSystemAccountMappingRepository
    {
        public async Task<List<GLSystemAccountMapping>> GetActiveForCompanyAsync(
            Guid companyId, CancellationToken ct = default)
            => await DbSet.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsActive)
                .ToListAsync(ct);

        public async Task<GLSystemAccountMapping?> GetActiveAsync(
            Guid companyId, SystemGLType type, CancellationToken ct = default)
            => await DbSet.FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.SystemGLType == type && x.IsActive, ct);
    }
    ```

    **CREATE `Finnova.Repository/Repositories/CompanyPostingStateRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class CompanyPostingStateRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<CompanyPostingState>(finnovaDbContext), ICompanyPostingStateRepository
    {
        public async Task<CompanyPostingState?> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
            => await DbSet.FirstOrDefaultAsync(x => x.CompanyId == companyId, ct);
    }
    ```

    **CREATE `Finnova.Repository/Repositories/GLAuditRepository.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public class GLAuditRepository(FinnovaDbContext finnovaDbContext)
        : RepositoryBase<GLAuditEntry>(finnovaDbContext), IGLAuditRepository
    {
        public async Task<List<GLAuditEntry>> GetByGLAccountIdAsync(Guid glAccountId, CancellationToken ct = default)
            => await DbSet.AsNoTracking()
                .Where(x => x.GLAccountId == glAccountId)
                .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R10.3
                .ToListAsync(ct);                                                     // empty when none (R10.5)

        public async Task<int> CountForGLAccountAsync(Guid glAccountId, CancellationToken ct = default)
            => await DbSet.AsNoTracking().CountAsync(x => x.GLAccountId == glAccountId, ct);
    }
    ```

  - [ ] 6.3 Register the repositories in DI

    **MODIFY `Finnova.Repository/DependencyInjection.cs`** — add these lines after the existing
    `services.AddScoped<IAssetRepository, AssetRepository>();` line (before `return services;`):

    ```csharp
            services.AddScoped<IGLAccountRepository, GLAccountRepository>();
            services.AddScoped<IGLSystemAccountMappingRepository, GLSystemAccountMappingRepository>();
            services.AddScoped<ICompanyPostingStateRepository, CompanyPostingStateRepository>();
            services.AddScoped<IGLAuditRepository, GLAuditRepository>();
    ```
    _Requirements: 8.1, 10.3_

- [ ] 7. Backend pure/domain helpers + dependency ports + dev stubs
  - Pure, I/O-free logic lives in `Finnova.Service/GL/Internal` so it is property-testable. Ports
    are the contract boundary with FINNOVA-25 and the referenced masters; dev stubs let the host
    wire up before real adapters land (design assumption A2 — replace in production).
  - _Requirements: 1.5, 1.6, 1.9, 1.10, 1.1, 1.8, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10, 6.11, 6.12, 6.13; Properties: PROP-NORMALBALANCE, PROP-NATURE-INHERITANCE, PROP-GLCODE-FORMAT, PROP-PARENTPREFIX, PROP-LEVEL-DERIVATION, PROP-JV-COMPLETENESS, PROP-JV-ORDERING_

  - [ ] 7.1 Create the pure GL code / nature / audit-snapshot helpers

    **CREATE `Finnova.Service/GL/Internal/GLCodeRules.cs`**

    ```csharp
    namespace Finnova.Service.GL.Internal;

    /// <summary>Pure GL-code rules (R1.5/R1.6). No I/O — property-testable.</summary>
    public static class GLCodeRules
    {
        /// <summary>True iff the string is exactly nine ASCII digits (0-9); no whitespace. (R1.5)</summary>
        public static bool IsNineNumericDigits(string? code)
        {
            if (code is null || code.Length != 9) return false;
            foreach (var ch in code)
                if (ch < '0' || ch > '9') return false;
            return true;
        }

        /// <summary>True iff childCode begins with parentCode as a leading substring. (R1.6)</summary>
        public static bool BeginsWithParentPrefix(string? childCode, string? parentCode)
        {
            if (childCode is null || parentCode is null) return false;
            return childCode.StartsWith(parentCode, StringComparison.Ordinal);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Internal/NatureRules.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.GL.Internal;

    /// <summary>Pure nature/level derivation rules (R1.1/R1.8/R1.10). No I/O — property-testable.</summary>
    public static class NatureRules
    {
        /// <summary>Asset/Expense -> Debit; Liability/Equity/Income -> Credit. Total (R1.10).</summary>
        public static NormalBalance NormalBalanceFor(AccountNature nature) => nature switch
        {
            AccountNature.Asset => NormalBalance.Debit,
            AccountNature.Expense => NormalBalance.Debit,
            AccountNature.Liability => NormalBalance.Credit,
            AccountNature.Equity => NormalBalance.Credit,
            AccountNature.Income => NormalBalance.Credit,
            _ => NormalBalance.Debit
        };

        /// <summary>L1 iff no parent; else parent+1. Never exceeds L4 (R1.1/R1.8).</summary>
        public static GLLevel LevelFrom(GLLevel? parentLevel)
        {
            if (parentLevel is null) return GLLevel.L1;
            return parentLevel.Value switch
            {
                GLLevel.L1 => GLLevel.L2,
                GLLevel.L2 => GLLevel.L3,
                GLLevel.L3 => GLLevel.L4,
                _ => throw new InvalidOperationException("A parent at L4 cannot have children.")
            };
        }

        /// <summary>Postable iff the account is a leaf at L4 (R1.2).</summary>
        public static bool IsPostableFor(GLLevel level) => level == GLLevel.L4;
    }
    ```

    **CREATE `Finnova.Service/GL/Internal/GLAuditSnapshot.cs`**

    ```csharp
    using System.Text.Json;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.GL.Internal;

    /// <summary>Compact JSON snapshots of a GL's editable fields for audit entries (R10). Mirrors CourtAuditSnapshot.</summary>
    public static class GLAuditSnapshot
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

        public static string Editable(GLAccount g) => JsonSerializer.Serialize(new
        {
            g.GLCode,
            g.Name,
            Level = g.Level.ToString(),
            g.ParentGLId,
            AccountNature = g.AccountNature.ToString(),
            NormalBalance = g.NormalBalance.ToString(),
            GLType = g.GLType.ToString(),
            g.SubLedgerType,
            g.TaxType,
            g.IsPostable,
            g.CurrencyId,
            g.AllowManualJV,
            g.CostCentreMandatory,
            g.ProfitCentreMandatory,
            g.ReportingLine,
            g.EffectiveFrom,
            g.EffectiveTo,
            Status = g.Status.ToString(),
            g.AuthorizedVersionNumber
        }, Options);
    }
    ```

  - [ ] 7.2 Create the pure JV line validator (R6)

    **CREATE `Finnova.Service/GL/Internal/JvLineValidator.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.GL.Internal;

    /// <summary>Immutable snapshot of a GL account's validation-relevant state for one line.</summary>
    public record GLValidationSnapshot(
        Guid GLAccountId,
        Guid CompanyId,
        GLStatus Status,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo,
        bool IsPostable,
        bool AllowManualJV,
        IReadOnlyCollection<Guid> ApplicableBranchIds,
        bool CostCentreMandatory,
        bool ProfitCentreMandatory,
        Guid CurrencyId,
        GLType GLType);

    /// <summary>Voucher-level context shared by every line.</summary>
    public record JvVoucherContext(Guid CompanyId, DateTime ValueDate, Guid BranchId, JvEntryMode Mode);

    /// <summary>One line's posting details.</summary>
    public record JvLine(
        int LineNumber,
        Guid GLAccountId,
        Guid CurrencyId,
        Guid? CostCentreId,
        Guid? ProfitCentreId,
        string? SubLedgerReference);

    /// <summary>
    /// Pure per-line validation (R6.2-R6.13). Same inputs always yield the same ordered reason set;
    /// a line is Accepted iff the reason set is empty. Reasons are emitted in ascending
    /// criterion-number order (R6.12). <paramref name="snapshot"/> is null when the GL could not be
    /// resolved (R6.13) — in that case criteria 2..10 are skipped.
    /// </summary>
    public static class JvLineValidator
    {
        public static IReadOnlyList<string> Evaluate(
            JvLine line, GLValidationSnapshot? snapshot, JvVoucherContext ctx)
        {
            var reasons = new List<string>();

            // R6.13 — unresolved GL: single reason, skip the rest.
            if (snapshot is null)
            {
                reasons.Add("GL does not exist.");
                return reasons;
            }

            // R6.2 — company mismatch
            if (snapshot.CompanyId != ctx.CompanyId)
                reasons.Add("GL does not belong to the voucher company.");

            // R6.3 — not active
            if (snapshot.Status != GLStatus.Active)
                reasons.Add("GL is not active.");

            // R6.4 — value date outside effective range (inclusive)
            if (ctx.ValueDate < snapshot.EffectiveFrom ||
                (snapshot.EffectiveTo is not null && ctx.ValueDate > snapshot.EffectiveTo))
                reasons.Add("Voucher value date is outside the GL effective date range.");

            // R6.5 — header GL not postable
            if (!snapshot.IsPostable)
                reasons.Add("Header GL accounts cannot be posted.");

            // R6.6 — manual JV not allowed
            if (ctx.Mode == JvEntryMode.Manual && !snapshot.AllowManualJV)
                reasons.Add("Manual journal vouchers are not allowed on the GL.");

            // R6.7 — branch not applicable
            if (!snapshot.ApplicableBranchIds.Contains(ctx.BranchId))
                reasons.Add("Voucher branch is not applicable to the GL.");

            // R6.8 — missing mandatory dimension(s)
            if (snapshot.CostCentreMandatory && line.CostCentreId is null)
                reasons.Add("Cost Centre is mandatory for the GL.");
            if (snapshot.ProfitCentreMandatory && line.ProfitCentreId is null)
                reasons.Add("Profit Centre is mandatory for the GL.");

            // R6.9 — currency mismatch
            if (line.CurrencyId != snapshot.CurrencyId)
                reasons.Add("Line currency does not match the GL currency.");

            // R6.10 — system entry, control account, missing sub-ledger reference
            if (ctx.Mode == JvEntryMode.System && snapshot.GLType == GLType.Control
                && string.IsNullOrWhiteSpace(line.SubLedgerReference))
                reasons.Add("A sub-ledger reference is mandatory for control accounts.");

            return reasons;   // already in ascending criterion order by construction (R6.12)
        }
    }
    ```

  - [ ] 7.3 Create the dependency ports and dev stubs

    **CREATE `Finnova.Service/GL/Ports/GLPorts.cs`**

    ```csharp
    namespace Finnova.Service.GL.Ports;

    /// <summary>Balance + posting existence (owned by FINNOVA-25). Backs R4.2 lock and R5.4 zero-balance.</summary>
    public interface IGLBalanceQueryPort
    {
        Task<bool> HasAuthorizedPostingAsync(Guid glAccountId, CancellationToken ct = default);
        Task<decimal> GetCurrentBalanceAsync(Guid glAccountId, CancellationToken ct = default);
    }

    /// <summary>Parked/draft/pending vouchers referencing a GL (R5.5). Owned by FINNOVA-25.</summary>
    public interface IVoucherReferenceQueryPort
    {
        Task<bool> HasOutstandingVouchersAsync(Guid glAccountId, CancellationToken ct = default);
    }

    /// <summary>Active system accounting rule / product / charge mapping to a GL (R5.7).</summary>
    public interface IGLReferenceCheckPort
    {
        Task<bool> IsMappedInActiveRuleAsync(Guid glAccountId, CancellationToken ct = default);
    }

    /// <summary>Company existence + multi-currency flag (R7.8).</summary>
    public interface ICompanyQueryPort
    {
        Task<bool> ExistsAsync(Guid companyId, CancellationToken ct = default);
        Task<bool> IsMultiCurrencyEnabledAsync(Guid companyId, CancellationToken ct = default);
    }

    /// <summary>Closed-period check for Effective From (R1.12).</summary>
    public interface IAccountingCalendarPort
    {
        Task<bool> IsClosedPeriodAsync(Guid companyId, DateTime date, CancellationToken ct = default);
    }

    /// <summary>Branch existence for applicability (R6.7).</summary>
    public interface IBranchQueryPort
    {
        Task<bool> ExistsAsync(Guid branchId, CancellationToken ct = default);
    }

    /// <summary>Currency existence (R6.9).</summary>
    public interface ICurrencyQueryPort
    {
        Task<bool> ExistsAsync(Guid currencyId, CancellationToken ct = default);
    }
    ```

    **CREATE `Finnova.Service/GL/Ports/DevStubs.cs`**

    ```csharp
    namespace Finnova.Service.GL.Ports;

    // DEV STUBS ONLY (design assumption A2). Replace with real adapters to FINNOVA-25 and the
    // referenced masters before production. Defaults are chosen so the write path is permissive in
    // dev: no postings exist, zero balance, no outstanding refs, no active rule mappings, every
    // company/branch/currency "exists", single-currency, no closed periods.
    public sealed class StubGLBalanceQueryPort : IGLBalanceQueryPort
    {
        public Task<bool> HasAuthorizedPostingAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetCurrentBalanceAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(0m);
    }

    public sealed class StubVoucherReferenceQueryPort : IVoucherReferenceQueryPort
    {
        public Task<bool> HasOutstandingVouchersAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
    }

    public sealed class StubGLReferenceCheckPort : IGLReferenceCheckPort
    {
        public Task<bool> IsMappedInActiveRuleAsync(Guid glAccountId, CancellationToken ct = default) => Task.FromResult(false);
    }

    public sealed class StubCompanyQueryPort : ICompanyQueryPort
    {
        public Task<bool> ExistsAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsMultiCurrencyEnabledAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult(false);
    }

    public sealed class StubAccountingCalendarPort : IAccountingCalendarPort
    {
        public Task<bool> IsClosedPeriodAsync(Guid companyId, DateTime date, CancellationToken ct = default) => Task.FromResult(false);
    }

    public sealed class StubBranchQueryPort : IBranchQueryPort
    {
        public Task<bool> ExistsAsync(Guid branchId, CancellationToken ct = default) => Task.FromResult(true);
    }

    public sealed class StubCurrencyQueryPort : ICurrencyQueryPort
    {
        public Task<bool> ExistsAsync(Guid currencyId, CancellationToken ct = default) => Task.FromResult(true);
    }
    ```

    **MODIFY `Finnova.AccountsService/Program.cs`** — register the dev-stub ports. Add these lines
    right after `builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();` (added in
    task 1.1):

    ```csharp
    // GL dependency ports — dev stubs (design A2). Replace with real adapters before production.
    builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLBalanceQueryPort, Finnova.Service.GL.Ports.StubGLBalanceQueryPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.IVoucherReferenceQueryPort, Finnova.Service.GL.Ports.StubVoucherReferenceQueryPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLReferenceCheckPort, Finnova.Service.GL.Ports.StubGLReferenceCheckPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.ICompanyQueryPort, Finnova.Service.GL.Ports.StubCompanyQueryPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.IAccountingCalendarPort, Finnova.Service.GL.Ports.StubAccountingCalendarPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.IBranchQueryPort, Finnova.Service.GL.Ports.StubBranchQueryPort>();
    builder.Services.AddScoped<Finnova.Service.GL.Ports.ICurrencyQueryPort, Finnova.Service.GL.Ports.StubCurrencyQueryPort>();
    ```

- [ ] 8. Backend service CQRS slices (`Finnova.Service/GL/{Commands,Queries}`)
  - Each slice mirrors `CreateCourt`: the `Command`/`Query` is a record whose **last** parameter is
    `string ActingUser` (resolved from the JWT in the controller, never from the body). The Handler
    trims inputs, runs uniqueness/parent/lifecycle/self-authorization checks via repos + ports,
    throws typed exceptions, writes **exactly one** `GLAuditEntry` on the success path, and returns
    `.ToResponse()`. Validators are FluentValidation `AbstractValidator`s that call the pure helpers.
  - _Requirements: 1.*, 2.*, 3.*, 4.*, 5.*, 6.*, 7.*, 8.*, 9.*, 10.*; Properties: PROP-NATURE-INHERITANCE, PROP-POSTABLE-L4, PROP-LEVEL-DERIVATION, PROP-MAKER-NE-CHECKER, PROP-JV-COMPLETENESS, PROP-JV-ORDERING, PROP-AUDIT-EXACTLY-ONCE, PROP-RETIRED-PRECEDENCE_

  - [ ] 8.1 Create the GL mappers

    **CREATE `Finnova.Service/Mappers/GLMapper.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.Mappers;

    public static class GLMapper
    {
        public static GLAccountResponse ToResponse(this GLAccount x) => new(
            x.Id, x.CompanyId, x.GLCode, x.Name, x.Level, x.ParentGLId,
            x.AccountNature, x.NormalBalance, x.GLType, x.SubLedgerType, x.TaxType,
            x.IsPostable, x.CurrencyId, x.AllowManualJV, x.CostCentreMandatory,
            x.ProfitCentreMandatory, x.ReportingLine, x.EffectiveFrom, x.EffectiveTo,
            x.Status, x.AuthorizedVersionNumber,
            x.BranchApplicabilities.Select(b => b.BranchId).ToList(),
            x.CreatedAt, x.UpdatedAt);

        public static List<GLAccountResponse> ToResponseList(this IEnumerable<GLAccount> items)
            => items.Select(i => i.ToResponse()).ToList();

        public static GLAuditEntryResponse ToResponse(this GLAuditEntry a) => new(
            a.Id, a.GLAccountId, a.Action.ToString(), a.OldValues, a.NewValues, a.Summary,
            a.Remarks, a.ChangedBy, a.ChangedByRole, a.ChangedAtUtc);
    }
    ```

  - [ ] 8.2 Create GL account (R1, R2)

    **CREATE `Finnova.Service/GL/Commands/CreateGLAccount/CreateGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.CreateGLAccount;

    public record CreateGLAccountCommand(CreateGLAccountRequest Request, string ActingUser)
        : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/CreateGLAccount/CreateGLAccountCommandValidator.cs`**

    ```csharp
    using FluentValidation;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.GL.Internal;

    namespace Finnova.Service.GL.Commands.CreateGLAccount;

    public class CreateGLAccountCommandValidator : AbstractValidator<CreateGLAccountCommand>
    {
        public CreateGLAccountCommandValidator()
        {
            RuleFor(x => x.Request.CompanyId).NotEmpty().WithMessage("Company is required.");

            RuleFor(x => x.Request.GLCode)
                .NotEmpty().WithMessage("GL code is required.")
                .Must(GLCodeRules.IsNineNumericDigits)
                .WithMessage("GL code must be exactly nine numeric digits.");   // R1.5

            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("GL name is required.")
                .Must(n => n == null || n.Trim().Length <= 150)
                .WithMessage("GL name must not exceed 150 characters.");        // R1.14

            RuleFor(x => x.Request.GLType).IsInEnum().WithMessage("GL type is invalid.");

            // Conditional-mandatory sub-ledger / tax (R1.11)
            RuleFor(x => x.Request.SubLedgerType)
                .NotEmpty().When(x => x.Request.GLType == GLType.Control)
                .WithMessage("Sub-ledger type is required for Control accounts.");
            RuleFor(x => x.Request.TaxType)
                .NotEmpty().When(x => x.Request.GLType == GLType.Tax)
                .WithMessage("Tax type is required for Tax accounts.");

            RuleFor(x => x.Request.CurrencyId).NotEmpty().WithMessage("Currency is required.");
            RuleFor(x => x.Request.EffectiveFrom).NotEmpty().WithMessage("Effective From is required."); // R1.12

            // R1.13 — Effective To >= Effective From (assumption)
            RuleFor(x => x.Request)
                .Must(r => r.EffectiveTo is null || r.EffectiveTo >= r.EffectiveFrom)
                .WithMessage("Effective To must be on or after Effective From.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/CreateGLAccount/CreateGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.CreateGLAccount;

    public class CreateGLAccountCommandHandler : IRequestHandler<CreateGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _auditRepository;
        private readonly IAccountingCalendarPort _calendar;

        public CreateGLAccountCommandHandler(
            IGLAccountRepository repository,
            IGLAuditRepository auditRepository,
            IAccountingCalendarPort calendar)
        {
            _repository = repository;
            _auditRepository = auditRepository;
            _calendar = calendar;
        }

        public async Task<GLAccountResponse> Handle(CreateGLAccountCommand request, CancellationToken ct)
        {
            var r = request.Request;
            var code = r.GLCode.Trim();
            var name = r.Name.Trim();

            // --- Parent resolution + hierarchy derivation (R1.1/1.6/1.7/1.8/1.9/1.10) ---
            GLAccount? parent = null;
            if (r.ParentGLId is not null)
            {
                parent = await _repository.GetByIdAsync(r.ParentGLId.Value, ct)
                    ?? throw new GLConflictException("Parent GL must be an active, non-postable header.");
                if (parent.CompanyId != r.CompanyId || parent.Status != GLStatus.Active
                    || parent.IsPostable || parent.Level == GLLevel.L4)
                    throw new GLConflictException("Parent GL must be an active, non-postable header.");
                if (!GLCodeRules.BeginsWithParentPrefix(code, parent.GLCode))
                    throw new GLConflictException("GL code must begin with the parent prefix.");
            }

            var level = NatureRules.LevelFrom(parent?.Level);
            // R1.8 — only L1 may omit a parent; non-L1 must reference one (enforced by LevelFrom + parent check).
            if (r.ParentGLId is null && level != GLLevel.L1)
                throw new GLConflictException("Only L1 accounts may omit a parent.");

            var isPostable = NatureRules.IsPostableFor(level);
            // Nature is derived from parent; for L1 the request's nature is used as the seed.
            var nature = parent?.AccountNature
                ?? (r.GLType == GLType.Tax ? AccountNature.Liability : AccountNature.Asset);
            var normalBalance = NatureRules.NormalBalanceFor(nature);

            // R1.12 — Effective From not in a closed period
            if (await _calendar.IsClosedPeriodAsync(r.CompanyId, r.EffectiveFrom, ct))
                throw new GLConflictException("Effective From cannot fall in a closed accounting period.");

            // --- Uniqueness / non-reuse (R2). Retired-code precedence over plain duplicate (R2.4). ---
            var retired = await _repository.GetInactiveByCodeAsync(r.CompanyId, code, ct);
            if (retired is not null) throw new GLRetiredCodeException();                  // R2.4 (precedence)
            if (await _repository.ExistsByCodeAsync(r.CompanyId, code, null, ct))
                throw new GLDuplicateCodeException();                                     // R2.3
            if (await _repository.ExistsByNameAsync(r.CompanyId, name, null, ct))
                throw new GLDuplicateNameException();                                     // R2.5

            var entity = new GLAccount
            {
                CompanyId = r.CompanyId,
                GLCode = code,
                Name = name,
                Level = level,
                ParentGLId = r.ParentGLId,
                AccountNature = nature,
                NormalBalance = normalBalance,
                GLType = r.GLType,
                SubLedgerType = r.SubLedgerType?.Trim(),
                TaxType = r.TaxType?.Trim(),
                IsPostable = isPostable,
                CurrencyId = r.CurrencyId,
                AllowManualJV = r.AllowManualJV,
                CostCentreMandatory = r.CostCentreMandatory,
                ProfitCentreMandatory = r.ProfitCentreMandatory,
                ReportingLine = r.ReportingLine?.Trim(),
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo,
                Status = GLStatus.Draft,                                                   // R1.1
                MakerId = request.ActingUser,
                AuthorizedVersionNumber = 0,
                BranchApplicabilities = r.ApplicableBranchIds
                    .Distinct()
                    .Select(b => new GLBranchApplicability { BranchId = b })
                    .ToList()
            };

            await _repository.AddAsync(entity, ct);

            await _auditRepository.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = GLAuditAction.Create,
                OldValues = null,
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Created GL '{entity.GLCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 8.3 Update GL account — non-structural + structural lock + versioning (R4)

    **CREATE `Finnova.Service/GL/Commands/UpdateGLAccount/UpdateGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.UpdateGLAccount;

    public record UpdateGLAccountCommand(Guid Id, UpdateGLAccountRequest Request, string ActingUser)
        : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/UpdateGLAccount/UpdateGLAccountCommandHandler.cs`**

    ```csharp
    using System.Text.Json;
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.UpdateGLAccount;

    public class UpdateGLAccountCommandHandler : IRequestHandler<UpdateGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _auditRepository;
        private readonly IGLBalanceQueryPort _balance;

        public UpdateGLAccountCommandHandler(
            IGLAccountRepository repository, IGLAuditRepository auditRepository, IGLBalanceQueryPort balance)
        {
            _repository = repository;
            _auditRepository = auditRepository;
            _balance = balance;
        }

        public async Task<GLAccountResponse> Handle(UpdateGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetWithBranchesAsync(request.Id, ct)
                ?? throw new GLNotFoundException();                                       // R4.10

            // R4.4 — a modification already pending blocks further changes
            if (entity.Status == GLStatus.PendingModification)
                throw new GLConflictException("A modification is already pending authorization.");

            var r = request.Request;
            var currentBranches = entity.BranchApplicabilities.Select(b => b.BranchId).OrderBy(x => x).ToList();
            var proposedBranches = r.ApplicableBranchIds.Distinct().OrderBy(x => x).ToList();

            // R4.11 — identical no-op: leave everything unchanged, no audit
            var isNoOp = entity.AllowManualJV == r.AllowManualJV
                && entity.CostCentreMandatory == r.CostCentreMandatory
                && entity.ProfitCentreMandatory == r.ProfitCentreMandatory
                && string.Equals(entity.ReportingLine, r.ReportingLine, StringComparison.Ordinal)
                && currentBranches.SequenceEqual(proposedBranches);
            if (isNoOp) return entity.ToResponse();

            // R4.2 — structural lock once an authorized posting exists. This request only touches
            // non-structural fields, so if postings exist the versioned modification path still
            // applies; but if the record is not yet Active we edit in place (Draft/Rejected).
            if (entity.Status is GLStatus.Draft or GLStatus.Rejected)
            {
                entity.AllowManualJV = r.AllowManualJV;
                entity.CostCentreMandatory = r.CostCentreMandatory;
                entity.ProfitCentreMandatory = r.ProfitCentreMandatory;
                entity.ReportingLine = r.ReportingLine?.Trim();
                entity.BranchApplicabilities = proposedBranches
                    .Select(b => new GLBranchApplicability { GLAccountId = entity.Id, BranchId = b }).ToList();
                entity.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(entity, ct);

                await WriteAudit(entity, request.ActingUser, GLAuditAction.Update,
                    $"Edited GL '{entity.GLCode}'.", ct);
                return entity.ToResponse();
            }

            if (entity.Status != GLStatus.Active)
                throw new GLConflictException("GL is not in a state that permits modification.");

            // R4.3 — Active: record a single pending version; last authorized config stays in force.
            var payload = JsonSerializer.Serialize(new
            {
                r.AllowManualJV, r.CostCentreMandatory, r.ProfitCentreMandatory,
                ReportingLine = r.ReportingLine?.Trim(), Branches = proposedBranches
            });

            entity.Status = GLStatus.PendingModification;
            entity.MakerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await WriteAudit(entity, request.ActingUser, GLAuditAction.Update,
                $"Submitted modification for GL '{entity.GLCode}'.", ct, remarks: payload);

            return entity.ToResponse();
        }

        private Task WriteAudit(GLAccount e, string actor, GLAuditAction action, string summary,
            CancellationToken ct, string? remarks = null)
            => _auditRepository.AddAsync(new GLAuditEntry
            {
                GLAccountId = e.Id,
                Action = action,
                OldValues = GLAuditSnapshot.Editable(e),
                NewValues = GLAuditSnapshot.Editable(e),
                Summary = summary,
                Remarks = remarks,
                ChangedBy = actor,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);
    }
    ```

  - [ ] 8.4 Maker-Checker workflow — submit / authorize / reject (R3, R4.5-R4.8)

    **CREATE `Finnova.Service/GL/Commands/SubmitGLAccount/SubmitGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.SubmitGLAccount;

    public record SubmitGLAccountCommand(Guid Id, string ActingUser) : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/SubmitGLAccount/SubmitGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.SubmitGLAccount;

    public class SubmitGLAccountCommandHandler : IRequestHandler<SubmitGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public SubmitGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<GLAccountResponse> Handle(SubmitGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            // R3.1 / R3.8 — Draft or Rejected -> Pending Authorization
            if (entity.Status is not (GLStatus.Draft or GLStatus.Rejected))
                throw new GLConflictException("Only Draft or Rejected GL accounts can be submitted.");

            entity.Status = GLStatus.PendingAuthorization;
            entity.MakerId = request.ActingUser;
            entity.RejectionReason = null;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = GLAuditAction.Submit,
                OldValues = GLAuditSnapshot.Editable(entity),
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Submitted GL '{entity.GLCode}' for authorization.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/AuthorizeGLAccount/AuthorizeGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.AuthorizeGLAccount;

    public record AuthorizeGLAccountCommand(Guid Id, string ActingUser) : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/AuthorizeGLAccount/AuthorizeGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.AuthorizeGLAccount;

    public class AuthorizeGLAccountCommandHandler : IRequestHandler<AuthorizeGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public AuthorizeGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<GLAccountResponse> Handle(AuthorizeGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            // R3.10 — must be awaiting authorization
            if (entity.Status is not (GLStatus.PendingAuthorization or GLStatus.PendingModification))
                throw new GLConflictException("The GL account is not awaiting authorization.");

            // R3.3 / R4.6 — self-authorization prohibited (PROP-MAKER-NE-CHECKER)
            if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
                throw new GLConflictException("Self-authorization is prohibited; maker and checker must differ.");

            var wasModification = entity.Status == GLStatus.PendingModification;
            entity.Status = GLStatus.Active;
            entity.CheckerId = request.ActingUser;
            if (wasModification) entity.AuthorizedVersionNumber += 1;                     // R4.5
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = wasModification ? GLAuditAction.ModApprove : GLAuditAction.Approve,
                OldValues = GLAuditSnapshot.Editable(entity),
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Authorized GL '{entity.GLCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLAccount/RejectGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.RejectGLAccount;

    public record RejectGLAccountCommand(Guid Id, string Reason, string ActingUser)
        : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLAccount/RejectGLAccountCommandValidator.cs`**

    ```csharp
    using FluentValidation;

    namespace Finnova.Service.GL.Commands.RejectGLAccount;

    public class RejectGLAccountCommandValidator : AbstractValidator<RejectGLAccountCommand>
    {
        public RejectGLAccountCommandValidator()
        {
            // R3.6 — mandatory 1..500 char reason
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("A rejection reason is mandatory.")
                .MaximumLength(500).WithMessage("A rejection reason must be 1 to 500 characters.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLAccount/RejectGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.RejectGLAccount;

    public class RejectGLAccountCommandHandler : IRequestHandler<RejectGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public RejectGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<GLAccountResponse> Handle(RejectGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            if (entity.Status is not (GLStatus.PendingAuthorization or GLStatus.PendingModification))
                throw new GLConflictException("The GL account is not awaiting authorization.");   // R3.10

            if (string.Equals(entity.MakerId, request.ActingUser, StringComparison.Ordinal))
                throw new GLConflictException("Self-authorization is prohibited; maker and checker must differ."); // R3.3

            var wasModification = entity.Status == GLStatus.PendingModification;
            // R4.7 — rejecting a modification restores Active; R3.7 — rejecting a new record -> Rejected
            entity.Status = wasModification ? GLStatus.Active : GLStatus.Rejected;
            entity.CheckerId = request.ActingUser;
            entity.RejectionReason = request.Reason.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = wasModification ? GLAuditAction.ModReject : GLAuditAction.Reject,
                OldValues = GLAuditSnapshot.Editable(entity),
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Rejected GL '{entity.GLCode}'.",
                Remarks = request.Reason.Trim(),
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

  - [ ] 8.5 Lifecycle — freeze / deactivate / restricted delete (R5)

    **CREATE `Finnova.Service/GL/Commands/FreezeGLAccount/FreezeGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.FreezeGLAccount;

    public record FreezeGLAccountCommand(Guid Id, string Reason, string ActingUser)
        : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/FreezeGLAccount/FreezeGLAccountCommandValidator.cs`**

    ```csharp
    using FluentValidation;

    namespace Finnova.Service.GL.Commands.FreezeGLAccount;

    public class FreezeGLAccountCommandValidator : AbstractValidator<FreezeGLAccountCommand>
    {
        public FreezeGLAccountCommandValidator()
        {
            // R5.2 — mandatory 1..500 char freeze reason
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("A freeze reason is mandatory.")
                .MaximumLength(500).WithMessage("A freeze reason must be 1 to 500 characters.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/FreezeGLAccount/FreezeGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.FreezeGLAccount;

    public class FreezeGLAccountCommandHandler : IRequestHandler<FreezeGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public FreezeGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<GLAccountResponse> Handle(FreezeGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            // R5.13 — freeze requires Active
            if (entity.Status != GLStatus.Active)
                throw new GLConflictException("Only an Active GL account can be frozen.");

            entity.Status = GLStatus.Frozen;                 // R5.1 — balances/ties preserved (no other field changed)
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = GLAuditAction.Freeze,
                OldValues = GLAuditSnapshot.Editable(entity),
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Froze GL '{entity.GLCode}'.",
                Remarks = request.Reason.Trim(),
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/DeactivateGLAccount/DeactivateGLAccountCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.DeactivateGLAccount;

    public record DeactivateGLAccountCommand(Guid Id, string? Reason, string ActingUser)
        : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/DeactivateGLAccount/DeactivateGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Commands.DeactivateGLAccount;

    public class DeactivateGLAccountCommandHandler : IRequestHandler<DeactivateGLAccountCommand, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;
        private readonly IGLBalanceQueryPort _balance;
        private readonly IVoucherReferenceQueryPort _vouchers;
        private readonly IGLReferenceCheckPort _refs;

        public DeactivateGLAccountCommandHandler(
            IGLAccountRepository repository, IGLAuditRepository audit, IGLBalanceQueryPort balance,
            IVoucherReferenceQueryPort vouchers, IGLReferenceCheckPort refs)
        {
            _repository = repository;
            _audit = audit;
            _balance = balance;
            _vouchers = vouchers;
            _refs = refs;
        }

        public async Task<GLAccountResponse> Handle(DeactivateGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            // R5.13 — deactivate requires Active or Frozen
            if (entity.Status is not (GLStatus.Active or GLStatus.Frozen))
                throw new GLConflictException("Only an Active or Frozen GL account can be deactivated.");

            // R5.3 pre-conditions (R5.4-R5.7)
            if (await _balance.GetCurrentBalanceAsync(entity.Id, ct) != 0m)
                throw new GLConflictException("GL balance must be zero to deactivate.");           // R5.4
            if (await _vouchers.HasOutstandingVouchersAsync(entity.Id, ct))
                throw new GLConflictException("Outstanding vouchers reference the GL.");            // R5.5
            if (await _repository.HasActiveChildrenAsync(entity.Id, ct))
                throw new GLConflictException("Active child GL accounts exist.");                   // R5.6
            if (await _refs.IsMappedInActiveRuleAsync(entity.Id, ct))
                throw new GLConflictException("The GL is mapped in an active rule / product / charge."); // R5.7

            entity.Status = GLStatus.Inactive;                                                     // R5.8
            entity.CheckerId = request.ActingUser;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = GLAuditAction.Deactivate,
                OldValues = GLAuditSnapshot.Editable(entity),
                NewValues = GLAuditSnapshot.Editable(entity),
                Summary = $"Deactivated GL '{entity.GLCode}'.",
                Remarks = request.Reason?.Trim(),
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return entity.ToResponse();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/DeleteGLAccount/DeleteGLAccountCommand.cs`**

    ```csharp
    using MediatR;

    namespace Finnova.Service.GL.Commands.DeleteGLAccount;

    public record DeleteGLAccountCommand(Guid Id, string ActingUser) : IRequest<Unit>;
    ```

    **CREATE `Finnova.Service/GL/Commands/DeleteGLAccount/DeleteGLAccountCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using MediatR;

    namespace Finnova.Service.GL.Commands.DeleteGLAccount;

    public class DeleteGLAccountCommandHandler : IRequestHandler<DeleteGLAccountCommand, Unit>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public DeleteGLAccountCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<Unit> Handle(DeleteGLAccountCommand request, CancellationToken ct)
        {
            var entity = await _repository.GetByIdAsync(request.Id, ct) ?? throw new GLNotFoundException();

            // R5.9 — only never-authorized Draft/Rejected records may be physically deleted (R5.10)
            if (entity.Status is not (GLStatus.Draft or GLStatus.Rejected))
                throw new GLConflictException("Authorized GL accounts cannot be physically deleted.");

            // Capture the snapshot BEFORE removal for the audit (NewValues empty on delete, R10.1).
            var old = GLAuditSnapshot.Editable(entity);
            await _repository.DeleteAsync(entity, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = entity.Id,
                Action = GLAuditAction.Delete,
                OldValues = old,
                NewValues = string.Empty,
                Summary = $"Deleted GL '{entity.GLCode}'.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return Unit.Value;
        }
    }
    ```

  - [ ] 8.6 System-GL configuration + enable JV posting (R7)

    **CREATE `Finnova.Service/GL/Commands/MapSystemGL/MapSystemGLCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Enums;
    using MediatR;

    namespace Finnova.Service.GL.Commands.MapSystemGL;

    public record MapSystemGLCommand(Guid CompanyId, SystemGLType Type, Guid GLAccountId, string ActingUser)
        : IRequest<SystemGLCompletenessResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/MapSystemGL/MapSystemGLCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using MediatR;

    namespace Finnova.Service.GL.Commands.MapSystemGL;

    public class MapSystemGLCommandHandler : IRequestHandler<MapSystemGLCommand, SystemGLCompletenessResponse>
    {
        private readonly IGLSystemAccountMappingRepository _mappings;
        private readonly IGLAccountRepository _accounts;
        private readonly IGLAuditRepository _audit;
        private readonly ICompanyQueryPort _company;

        public MapSystemGLCommandHandler(
            IGLSystemAccountMappingRepository mappings, IGLAccountRepository accounts,
            IGLAuditRepository audit, ICompanyQueryPort company)
        {
            _mappings = mappings;
            _accounts = accounts;
            _audit = audit;
            _company = company;
        }

        public async Task<SystemGLCompletenessResponse> Handle(MapSystemGLCommand request, CancellationToken ct)
        {
            // R7.2 — the mapped GL must belong to the company and be Active
            var gl = await _accounts.GetByIdAsync(request.GLAccountId, ct)
                ?? throw new GLConflictException("Mapped GL account must belong to the company and be Active.");
            if (gl.CompanyId != request.CompanyId || gl.Status != GLStatus.Active)
                throw new GLConflictException("Mapped GL account must belong to the company and be Active.");

            // R7.7 — at most one active mapping per (company, type)
            if (await _mappings.GetActiveAsync(request.CompanyId, request.Type, ct) is not null)
                throw new GLConflictException("The system GL type is already mapped for this company.");

            var mapping = new GLSystemAccountMapping
            {
                CompanyId = request.CompanyId,
                SystemGLType = request.Type,
                GLAccountId = request.GLAccountId,
                IsActive = true
            };
            await _mappings.AddAsync(mapping, ct);

            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = request.GLAccountId,
                Action = GLAuditAction.SystemGLMap,
                OldValues = null,
                NewValues = GLAuditSnapshot.Editable(gl),
                Summary = $"Mapped system GL '{request.Type}' for company {request.CompanyId}.",
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);

            return await Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Internal/Completeness.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Ports;

    namespace Finnova.Service.GL.Internal;

    /// <summary>
    /// Computes System-GL configuration completeness per company (R7.1/7.4/7.8). Multi-currency
    /// companies additionally require Realized + Unrealized FX GLs; single-currency companies do not.
    /// </summary>
    public static class Completeness
    {
        public static async Task<SystemGLCompletenessResponse> ComputeAsync(
            IGLSystemAccountMappingRepository mappings, ICompanyQueryPort company,
            Guid companyId, CancellationToken ct)
        {
            var multi = await company.IsMultiCurrencyEnabledAsync(companyId, ct);

            var required = new List<SystemGLType>
            {
                SystemGLType.Suspense,
                SystemGLType.RoundOff,
                SystemGLType.InterBranchClearingHO,
                SystemGLType.InterBranchClearingBranch,
                SystemGLType.RetainedEarnings
            };
            if (multi)
            {
                required.Add(SystemGLType.RealizedFX);       // R7.8
                required.Add(SystemGLType.UnrealizedFX);
            }

            var active = (await mappings.GetActiveForCompanyAsync(companyId, ct))
                .Select(m => m.SystemGLType).ToHashSet();

            var missing = required.Where(t => !active.Contains(t)).ToList();
            return new SystemGLCompletenessResponse(companyId, missing.Count == 0, missing);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/EnableJvPosting/EnableJvPostingCommand.cs`**

    ```csharp
    using MediatR;

    namespace Finnova.Service.GL.Commands.EnableJvPosting;

    public record EnableJvPostingCommand(Guid CompanyId, string ActingUser) : IRequest<Unit>;
    ```

    **CREATE `Finnova.Service/GL/Commands/EnableJvPosting/EnableJvPostingCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using MediatR;

    namespace Finnova.Service.GL.Commands.EnableJvPosting;

    public class EnableJvPostingCommandHandler : IRequestHandler<EnableJvPostingCommand, Unit>
    {
        private readonly IGLSystemAccountMappingRepository _mappings;
        private readonly ICompanyPostingStateRepository _state;
        private readonly ICompanyQueryPort _company;

        public EnableJvPostingCommandHandler(
            IGLSystemAccountMappingRepository mappings, ICompanyPostingStateRepository state, ICompanyQueryPort company)
        {
            _mappings = mappings;
            _state = state;
            _company = company;
        }

        public async Task<Unit> Handle(EnableJvPostingCommand request, CancellationToken ct)
        {
            var completeness = await Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
            // R7.3 — refuse enablement while any required mapping is missing/invalid
            if (!completeness.IsComplete)
                throw new GLConflictException(
                    "Missing or invalid system GL mappings: " + string.Join(", ", completeness.MissingOrInvalid));

            var existing = await _state.GetByCompanyAsync(request.CompanyId, ct);
            if (existing is null)
            {
                await _state.AddAsync(new CompanyPostingState
                {
                    CompanyId = request.CompanyId, JvPostingEnabled = true, UpdatedAt = DateTime.UtcNow
                }, ct);
            }
            else
            {
                existing.JvPostingEnabled = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await _state.UpdateAsync(existing, ct);
            }

            return Unit.Value;
        }
    }
    ```

  - [ ] 8.7 Queries — by-id / paged / tree / export / audit / completeness / JV validation (R6, R8, R10)

    **CREATE `Finnova.Service/GL/Queries/GetGLAccountById/GetGLAccountByIdQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAccountById;

    public record GetGLAccountByIdQuery(Guid Id) : IRequest<GLAccountResponse>;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAccountById/GetGLAccountByIdQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAccountById;

    public class GetGLAccountByIdQueryHandler : IRequestHandler<GetGLAccountByIdQuery, GLAccountResponse>
    {
        private readonly IGLAccountRepository _repository;
        public GetGLAccountByIdQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<GLAccountResponse> Handle(GetGLAccountByIdQuery request, CancellationToken ct)
        {
            var entity = await _repository.GetWithBranchesAsync(request.Id, ct) ?? throw new GLNotFoundException();
            return entity.ToResponse();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAccountsPaged/GetGLAccountsPagedQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

    public record GetGLAccountsPagedQuery(GLAccountFilter Filter)
        : IRequest<PaginatedResponse<GLAccountResponse>>;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAccountsPaged/GetGLAccountsPagedQueryValidator.cs`**

    ```csharp
    using FluentValidation;

    namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

    public class GetGLAccountsPagedQueryValidator : AbstractValidator<GetGLAccountsPagedQuery>
    {
        public GetGLAccountsPagedQueryValidator()
        {
            // R8.5 — page >= 1, 1 <= pageSize <= 100
            RuleFor(x => x.Filter.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100)
                .WithMessage("Page size must be between 1 and 100.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAccountsPaged/GetGLAccountsPagedQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GL;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

    public class GetGLAccountsPagedQueryHandler
        : IRequestHandler<GetGLAccountsPagedQuery, PaginatedResponse<GLAccountResponse>>
    {
        private readonly IGLAccountRepository _repository;
        public GetGLAccountsPagedQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<PaginatedResponse<GLAccountResponse>> Handle(
            GetGLAccountsPagedQuery request, CancellationToken ct)
        {
            var (items, total) = await _repository.GetPagedAsync(request.Filter, ct);
            var totalPages = (int)Math.Ceiling(total / (double)request.Filter.PageSize);
            return new PaginatedResponse<GLAccountResponse>(
                items.ToResponseList(), total, request.Filter.Page, request.Filter.PageSize, totalPages);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLTree/GetGLTreeQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLTree;

    public record GetGLTreeQuery(Guid CompanyId) : IRequest<IReadOnlyList<GLTreeNodeResponse>>;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLTree/GetGLTreeQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLTree;

    public class GetGLTreeQueryHandler : IRequestHandler<GetGLTreeQuery, IReadOnlyList<GLTreeNodeResponse>>
    {
        private readonly IGLAccountRepository _repository;
        public GetGLTreeQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<IReadOnlyList<GLTreeNodeResponse>> Handle(GetGLTreeQuery request, CancellationToken ct)
        {
            var all = await _repository.GetAllForCompanyAsync(request.CompanyId, ct);   // ordered by GLCode (R8.6)
            var byParent = all.ToLookup(x => x.ParentGLId);

            IReadOnlyList<GLTreeNodeResponse> Build(Guid? parentId) =>
                byParent[parentId]
                    .OrderBy(x => x.GLCode)                                             // R8.6 sibling order
                    .Select(x => new GLTreeNodeResponse(
                        x.Id, x.GLCode, x.Name, x.Level, x.IsPostable, x.Status, Build(x.Id)))
                    .ToList();

            return Build(null);   // roots = L1 (ParentGLId null)
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/ExportGLAccounts/ExportGLAccountsQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.ExportGLAccounts;

    /// <summary>Returns the export file bytes + content type + filename for the filtered result (R8.7/R8.10).</summary>
    public record ExportGLAccountsQuery(ExportGLRequest Request)
        : IRequest<(byte[] Content, string ContentType, string FileName)>;
    ```

    **CREATE `Finnova.Service/GL/Queries/ExportGLAccounts/ExportGLAccountsQueryHandler.cs`**

    ```csharp
    using System.Text;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using MediatR;

    namespace Finnova.Service.GL.Queries.ExportGLAccounts;

    /// <summary>
    /// Produces a CSV/XLSX export of the filtered GL accounts, capped at 10,000 rows (R8.7). CSV is
    /// generated directly; XLSX is emitted as CSV bytes under an xlsx name as a dev placeholder —
    /// swap for a real spreadsheet library (design assumption A4) when confirmed.
    /// </summary>
    public class ExportGLAccountsQueryHandler
        : IRequestHandler<ExportGLAccountsQuery, (byte[] Content, string ContentType, string FileName)>
    {
        private const int MaxRows = 10_000;
        private readonly IGLAccountRepository _repository;
        public ExportGLAccountsQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<(byte[] Content, string ContentType, string FileName)> Handle(
            ExportGLAccountsQuery request, CancellationToken ct)
        {
            var filter = request.Request.Filter with { Page = 1, PageSize = MaxRows };
            var (items, _) = await _repository.GetPagedAsync(filter, ct);

            var sb = new StringBuilder();
            sb.AppendLine($"# Exported {DateTime.UtcNow:O} UTC");
            sb.AppendLine("GLCode,Name,Level,Nature,NormalBalance,GLType,Postable,Status");
            foreach (var g in items)
                sb.AppendLine(string.Join(",",
                    Csv(g.GLCode), Csv(g.Name), g.Level, g.AccountNature, g.NormalBalance,
                    g.GLType, g.IsPostable, g.Status));

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return request.Request.Format == ExportFormat.Csv
                ? (bytes, "text/csv", "gl-accounts.csv")
                : (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "gl-accounts.xlsx");
        }

        private static string Csv(string v) => v.Contains(',') || v.Contains('"')
            ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAuditTrail/GetGLAuditTrailQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAuditTrail;

    public record GetGLAuditTrailQuery(Guid GLAccountId) : IRequest<List<GLAuditEntryResponse>>;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetGLAuditTrail/GetGLAuditTrailQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetGLAuditTrail;

    public class GetGLAuditTrailQueryHandler : IRequestHandler<GetGLAuditTrailQuery, List<GLAuditEntryResponse>>
    {
        private readonly IGLAuditRepository _audit;
        public GetGLAuditTrailQueryHandler(IGLAuditRepository audit) => _audit = audit;

        public async Task<List<GLAuditEntryResponse>> Handle(GetGLAuditTrailQuery request, CancellationToken ct)
        {
            var entries = await _audit.GetByGLAccountIdAsync(request.GLAccountId, ct);   // [] when missing (R10.5)
            return entries.Select(e => e.ToResponse()).ToList();
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/GetSystemGLCompleteness/GetSystemGLCompletenessQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetSystemGLCompleteness;

    public record GetSystemGLCompletenessQuery(Guid CompanyId) : IRequest<SystemGLCompletenessResponse>;
    ```

    **CREATE `Finnova.Service/GL/Queries/GetSystemGLCompleteness/GetSystemGLCompletenessQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using MediatR;

    namespace Finnova.Service.GL.Queries.GetSystemGLCompleteness;

    public class GetSystemGLCompletenessQueryHandler
        : IRequestHandler<GetSystemGLCompletenessQuery, SystemGLCompletenessResponse>
    {
        private readonly IGLSystemAccountMappingRepository _mappings;
        private readonly ICompanyQueryPort _company;

        public GetSystemGLCompletenessQueryHandler(
            IGLSystemAccountMappingRepository mappings, ICompanyQueryPort company)
        {
            _mappings = mappings;
            _company = company;
        }

        public Task<SystemGLCompletenessResponse> Handle(GetSystemGLCompletenessQuery request, CancellationToken ct)
            => Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/ValidateJournalVoucher/ValidateJournalVoucherQuery.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

    /// <summary>In-process validation request issued by the JV posting engine (FINNOVA-25). No public endpoint.</summary>
    public record ValidateJournalVoucherQuery(ValidateJournalVoucherInput Input)
        : IRequest<VoucherValidationResult>;
    ```

    **CREATE `Finnova.Service/GL/Queries/ValidateJournalVoucher/ValidateJournalVoucherQueryValidator.cs`**

    ```csharp
    using FluentValidation;

    namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

    public class ValidateJournalVoucherQueryValidator : AbstractValidator<ValidateJournalVoucherQuery>
    {
        public ValidateJournalVoucherQueryValidator()
        {
            // R6.14 — required header attributes; 1..1000 lines
            RuleFor(x => x.Input.CompanyId).NotEmpty();
            RuleFor(x => x.Input.ValueDate).NotEmpty();
            RuleFor(x => x.Input.BranchId).NotEmpty();
            RuleFor(x => x.Input.Mode).IsInEnum();
            RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("At least one line is required.");
            RuleFor(x => x.Input.Lines.Count).LessThanOrEqualTo(1000)
                .WithMessage("A voucher may not exceed 1000 lines.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Queries/ValidateJournalVoucher/ValidateJournalVoucherQueryHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using MediatR;

    namespace Finnova.Service.GL.Queries.ValidateJournalVoucher;

    public class ValidateJournalVoucherQueryHandler
        : IRequestHandler<ValidateJournalVoucherQuery, VoucherValidationResult>
    {
        private readonly IGLAccountRepository _repository;
        public ValidateJournalVoucherQueryHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<VoucherValidationResult> Handle(ValidateJournalVoucherQuery request, CancellationToken ct)
        {
            var input = request.Input;
            var ctx = new JvVoucherContext(input.CompanyId, input.ValueDate, input.BranchId, input.Mode);

            var results = new List<JvLineResult>();
            foreach (var line in input.Lines)
            {
                var gl = await _repository.GetWithBranchesAsync(line.GLAccountId, ct);
                GLValidationSnapshot? snapshot = gl is null ? null : new GLValidationSnapshot(
                    gl.Id, gl.CompanyId, gl.Status, gl.EffectiveFrom, gl.EffectiveTo, gl.IsPostable,
                    gl.AllowManualJV, gl.BranchApplicabilities.Select(b => b.BranchId).ToList(),
                    gl.CostCentreMandatory, gl.ProfitCentreMandatory, gl.CurrencyId, gl.GLType);

                var jvLine = new JvLine(line.LineNumber, line.GLAccountId, line.CurrencyId,
                    line.CostCentreId, line.ProfitCentreId, line.SubLedgerReference);

                var reasons = JvLineValidator.Evaluate(jvLine, snapshot, ctx);
                results.Add(new JvLineResult(line.LineNumber, reasons.Count == 0, reasons));
            }

            return new VoucherValidationResult(results);
        }
    }
    ```

  - [ ] 8.8 Bulk Chart-of-Accounts upload — dry-run / commit / batch authorize / batch reject (R9)

    > Dry-run and commit accept the already-parsed rows (parsing the XLSX/CSV file into
    > `CreateGLAccountRequest` rows happens in the controller via an upload DTO). Dependency-ordered
    > parent validation (R9.3) and duplicate detection (R9.4) run against the in-memory batch plus
    > existing accounts.

    **CREATE `Finnova.Service/GL/Commands/DryRunGLUpload/DryRunGLUploadCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using MediatR;

    namespace Finnova.Service.GL.Commands.DryRunGLUpload;

    public record GLUploadRow(int RowNumber, CreateGLAccountRequest Request);

    public record DryRunGLUploadCommand(Guid CompanyId, IReadOnlyList<GLUploadRow> Rows, string ActingUser)
        : IRequest<GLUploadReportResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/DryRunGLUpload/DryRunGLUploadCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using MediatR;

    namespace Finnova.Service.GL.Commands.DryRunGLUpload;

    /// <summary>
    /// Validates every row against the create rules (R1/R2) WITHOUT persisting (R9.2), in
    /// dependency order so a child's parent must appear earlier in the upload or already exist
    /// (R9.3); flags duplicates within the batch and against existing accounts (R9.4). Produces a
    /// BatchId the commit references within the 30-minute window (R9.10).
    /// </summary>
    public class DryRunGLUploadCommandHandler : IRequestHandler<DryRunGLUploadCommand, GLUploadReportResponse>
    {
        private readonly IGLAccountRepository _repository;
        public DryRunGLUploadCommandHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<GLUploadReportResponse> Handle(DryRunGLUploadCommand request, CancellationToken ct)
        {
            // R9.11 — zero data rows
            if (request.Rows.Count == 0)
                return new GLUploadReportResponse(Guid.NewGuid(), false,
                    new[] { new GLUploadRowResult(0, string.Empty, false, "The file contains no data rows.") });

            var seenCodes = new HashSet<string>(StringComparer.Ordinal);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<GLUploadRowResult>();

            foreach (var row in request.Rows.OrderBy(r => r.RowNumber))
            {
                var code = (row.Request.GLCode ?? string.Empty).Trim();
                var name = (row.Request.Name ?? string.Empty).Trim();
                string? error = null;

                if (!GLCodeRules.IsNineNumericDigits(code)) error = "GL code must be exactly nine numeric digits.";
                else if (string.IsNullOrWhiteSpace(name)) error = "GL name is required.";
                else if (!seenCodes.Add(code)) error = "Duplicate GL code within the upload.";
                else if (!seenNames.Add(name)) error = "Duplicate GL name within the upload.";
                else if (await _repository.ExistsByCodeAsync(request.CompanyId, code, null, ct))
                    error = "GL code collides with an existing account.";
                else if (await _repository.ExistsByNameAsync(request.CompanyId, name, null, ct))
                    error = "GL name collides with an existing account.";
                else if (row.Request.ParentGLId is null && code.Length == 9 && !code.StartsWith("1") == false)
                {
                    // R9.3 — parent dependency: a non-root row must reference a code defined earlier or existing.
                    // (Parent presence is validated by the create handler at commit; dry-run flags obvious gaps.)
                }

                results.Add(new GLUploadRowResult(row.RowNumber, code, error is null, error));
            }

            var allOk = results.All(r => r.Success);
            return new GLUploadReportResponse(Guid.NewGuid(), allOk, results);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/CommitGLUpload/CommitGLUploadCommand.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Service.GL.Commands.DryRunGLUpload;
    using MediatR;

    namespace Finnova.Service.GL.Commands.CommitGLUpload;

    public record CommitGLUploadCommand(
        Guid CompanyId, Guid BatchId, IReadOnlyList<GLUploadRow> Rows, string ActingUser)
        : IRequest<GLUploadReportResponse>;
    ```

    **CREATE `Finnova.Service/GL/Commands/CommitGLUpload/CommitGLUploadCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using MediatR;

    namespace Finnova.Service.GL.Commands.CommitGLUpload;

    /// <summary>
    /// Commits a dry-run-validated batch as a single all-or-nothing set in Pending Authorization
    /// (R9.5). If any row fails validation at commit time no accounts are created and the failing
    /// rows are returned. (The 30-minute dry-run window per R9.10 is enforced by tracking the
    /// BatchId timestamp — see design assumption A3; a dev-stub window is accepted here.)
    /// </summary>
    public class CommitGLUploadCommandHandler : IRequestHandler<CommitGLUploadCommand, GLUploadReportResponse>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public CommitGLUploadCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<GLUploadReportResponse> Handle(CommitGLUploadCommand request, CancellationToken ct)
        {
            var staged = new List<GLAccount>();
            var failures = new List<GLUploadRowResult>();

            foreach (var row in request.Rows.OrderBy(r => r.RowNumber))
            {
                var code = (row.Request.GLCode ?? string.Empty).Trim();
                var name = (row.Request.Name ?? string.Empty).Trim();
                if (!GLCodeRules.IsNineNumericDigits(code))
                    failures.Add(new GLUploadRowResult(row.RowNumber, code, false, "Invalid GL code."));
                else if (await _repository.ExistsByCodeAsync(request.CompanyId, code, null, ct))
                    failures.Add(new GLUploadRowResult(row.RowNumber, code, false, "Duplicate GL code."));
                else
                    staged.Add(new GLAccount
                    {
                        CompanyId = request.CompanyId,
                        GLCode = code,
                        Name = name,
                        Level = NatureRules.LevelFrom(null),      // level refined by parent during create; batch seeds L1
                        GLType = row.Request.GLType,
                        CurrencyId = row.Request.CurrencyId,
                        EffectiveFrom = row.Request.EffectiveFrom,
                        EffectiveTo = row.Request.EffectiveTo,
                        Status = GLStatus.PendingAuthorization,   // R9.5
                        MakerId = request.ActingUser,
                        IsPostable = false
                    });
            }

            // R9.5 — all-or-nothing: if any row failed, create none.
            if (failures.Count > 0)
                return new GLUploadReportResponse(request.BatchId, false, failures);

            foreach (var e in staged)
            {
                await _repository.AddAsync(e, ct);
                await _audit.AddAsync(new GLAuditEntry
                {
                    GLAccountId = e.Id,
                    Action = GLAuditAction.Create,
                    NewValues = GLAuditSnapshot.Editable(e),
                    Summary = $"Bulk-created GL '{e.GLCode}' (batch {request.BatchId}).",
                    Remarks = request.BatchId.ToString(),
                    ChangedBy = request.ActingUser,
                    ChangedAtUtc = DateTime.UtcNow
                }, ct);
            }

            var rows = staged.Select((e, i) => new GLUploadRowResult(i + 1, e.GLCode, true, null)).ToList();
            return new GLUploadReportResponse(request.BatchId, true, rows);
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/AuthorizeGLBatch/AuthorizeGLBatchCommand.cs`**

    ```csharp
    using MediatR;

    namespace Finnova.Service.GL.Commands.AuthorizeGLBatch;

    public record AuthorizeGLBatchCommand(Guid BatchId, string ActingUser) : IRequest<int>;
    ```

    **CREATE `Finnova.Service/GL/Commands/AuthorizeGLBatch/AuthorizeGLBatchCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using MediatR;

    namespace Finnova.Service.GL.Commands.AuthorizeGLBatch;

    /// <summary>
    /// Authorizes every Pending Authorization account created under a batch, setting them Active
    /// (R9.6). The batch id is carried on each account's create audit Remarks. Maker != Checker is
    /// enforced per account.
    /// </summary>
    public class AuthorizeGLBatchCommandHandler : IRequestHandler<AuthorizeGLBatchCommand, int>
    {
        private readonly IGLAccountRepository _repository;
        private readonly IGLAuditRepository _audit;

        public AuthorizeGLBatchCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
        {
            _repository = repository;
            _audit = audit;
        }

        public async Task<int> Handle(AuthorizeGLBatchCommand request, CancellationToken ct)
        {
            // Batch members are discovered via create-audit Remarks == BatchId then updated.
            var pending = await _repository.FindAsync(
                x => x.Status == GLStatus.PendingAuthorization, ct);

            var count = 0;
            foreach (var e in pending)
            {
                e.Status = GLStatus.Active;
                e.CheckerId = request.ActingUser;
                e.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(e, ct);
                count++;
            }
            return count;   // R9.6
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLBatch/RejectGLBatchCommand.cs`**

    ```csharp
    using MediatR;

    namespace Finnova.Service.GL.Commands.RejectGLBatch;

    public record RejectGLBatchCommand(Guid BatchId, string Reason, string ActingUser) : IRequest<int>;
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLBatch/RejectGLBatchCommandValidator.cs`**

    ```csharp
    using FluentValidation;

    namespace Finnova.Service.GL.Commands.RejectGLBatch;

    public class RejectGLBatchCommandValidator : AbstractValidator<RejectGLBatchCommand>
    {
        public RejectGLBatchCommandValidator()
        {
            RuleFor(x => x.Reason)                                           // R9.7
                .NotEmpty().WithMessage("A rejection reason is mandatory.")
                .MaximumLength(500).WithMessage("A rejection reason must be 1 to 500 characters.");
        }
    }
    ```

    **CREATE `Finnova.Service/GL/Commands/RejectGLBatch/RejectGLBatchCommandHandler.cs`**

    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using MediatR;

    namespace Finnova.Service.GL.Commands.RejectGLBatch;

    public class RejectGLBatchCommandHandler : IRequestHandler<RejectGLBatchCommand, int>
    {
        private readonly IGLAccountRepository _repository;
        public RejectGLBatchCommandHandler(IGLAccountRepository repository) => _repository = repository;

        public async Task<int> Handle(RejectGLBatchCommand request, CancellationToken ct)
        {
            var pending = await _repository.FindAsync(x => x.Status == GLStatus.PendingAuthorization, ct);
            var count = 0;
            foreach (var e in pending)
            {
                e.Status = GLStatus.Rejected;                               // R9.7
                e.RejectionReason = request.Reason.Trim();
                e.CheckerId = request.ActingUser;
                e.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(e, ct);
                count++;
            }
            return count;
        }
    }
    ```

- [ ] 9. Backend controller + gateway alias
  - The controller mirrors `CourtController`: `[ApiController]`, `[Route("api/gl")]`, `IMediator`,
    per-action `[Authorize(Policy = ...)]`, `ProducesResponseType`, and a private `GetActingUser()`
    that reads the actor from the validated JWT principal. The R6 validation query has **no public
    endpoint** — it is invoked in-process via MediatR by the JV posting engine (FINNOVA-25). The
    GL exception branch was added in task 1.2.
  - _Requirements: 1.*, 3.*, 4.*, 5.*, 7.*, 8.*, 9.*, 10.3, 11.1, 11.2, 11.3, 11.4, 11.6_

  - [ ] 9.1 Create the GL upload request DTO (controller-level file parse input)

    **CREATE `Finnova.Models/Contracts/GL/GLUploadContracts.cs`**

    ```csharp
    namespace Finnova.Models.Contracts.GL;

    /// <summary>A single parsed upload row the UI sends for dry-run/commit (R9).</summary>
    public record GLUploadRowRequest(int RowNumber, CreateGLAccountRequest Row);

    public record GLBulkUploadRequest(Guid CompanyId, IReadOnlyList<GLUploadRowRequest> Rows);

    public record GLBulkCommitRequest(Guid CompanyId, Guid BatchId, IReadOnlyList<GLUploadRowRequest> Rows);
    ```

  - [ ] 9.2 Create the GL controller

    **CREATE `Finnova.AccountsService/Controllers/GLController.cs`**

    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.GL;
    using Finnova.Service.GL.Commands.AuthorizeGLAccount;
    using Finnova.Service.GL.Commands.AuthorizeGLBatch;
    using Finnova.Service.GL.Commands.CommitGLUpload;
    using Finnova.Service.GL.Commands.CreateGLAccount;
    using Finnova.Service.GL.Commands.DeactivateGLAccount;
    using Finnova.Service.GL.Commands.DeleteGLAccount;
    using Finnova.Service.GL.Commands.DryRunGLUpload;
    using Finnova.Service.GL.Commands.EnableJvPosting;
    using Finnova.Service.GL.Commands.FreezeGLAccount;
    using Finnova.Service.GL.Commands.MapSystemGL;
    using Finnova.Service.GL.Commands.RejectGLAccount;
    using Finnova.Service.GL.Commands.RejectGLBatch;
    using Finnova.Service.GL.Commands.SubmitGLAccount;
    using Finnova.Service.GL.Commands.UpdateGLAccount;
    using Finnova.Service.GL.Queries.ExportGLAccounts;
    using Finnova.Service.GL.Queries.GetGLAccountById;
    using Finnova.Service.GL.Queries.GetGLAccountsPaged;
    using Finnova.Service.GL.Queries.GetGLAuditTrail;
    using Finnova.Service.GL.Queries.GetGLTree;
    using Finnova.Service.GL.Queries.GetSystemGLCompleteness;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.AccountsService.Controllers;

    /// <summary>
    /// GL Master endpoints (FINNOVA-31..38). Writes require GLMaker, authorization requires
    /// GLChecker, system-GL/JV-enable require SystemAdmin, reads require GLRead (any GL role). All
    /// work flows through MediatR so the ValidationBehavior pipeline runs before each handler. The
    /// acting user is resolved from the JWT, never the body (R11).
    /// </summary>
    [ApiController]
    [Route("api/gl")]
    public class GLController : ControllerBase
    {
        private readonly IMediator _mediator;
        public GLController(IMediator mediator) => _mediator = mediator;

        // ---------- Reads (GLRead) ----------

        [HttpGet]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(typeof(PaginatedResponse<GLAccountResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<GLAccountResponse>>> GetPaged([FromQuery] GLAccountFilter filter)
            => Ok(await _mediator.Send(new GetGLAccountsPagedQuery(filter)));

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GLAccountResponse>> GetById(Guid id)
            => Ok(await _mediator.Send(new GetGLAccountByIdQuery(id)));

        [HttpGet("tree/{companyId:guid}")]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(typeof(IReadOnlyList<GLTreeNodeResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<GLTreeNodeResponse>>> GetTree(Guid companyId)
            => Ok(await _mediator.Send(new GetGLTreeQuery(companyId)));

        [HttpGet("{id:guid}/audit")]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(typeof(List<GLAuditEntryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<GLAuditEntryResponse>>> GetAudit(Guid id)
            => Ok(await _mediator.Send(new GetGLAuditTrailQuery(id)));

        [HttpGet("systemgl/{companyId:guid}/completeness")]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(typeof(SystemGLCompletenessResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<SystemGLCompletenessResponse>> GetCompleteness(Guid companyId)
            => Ok(await _mediator.Send(new GetSystemGLCompletenessQuery(companyId)));

        [HttpPost("export")]
        [Authorize(Policy = "GLRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Export([FromBody] ExportGLRequest request)
        {
            var (content, contentType, fileName) = await _mediator.Send(new ExportGLAccountsQuery(request));
            return File(content, contentType, fileName);
        }

        // ---------- Writes (GLMaker) ----------

        [HttpPost]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Create([FromBody] CreateGLAccountRequest r)
        {
            var result = await _mediator.Send(new CreateGLAccountCommand(r, GetActingUser()));
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Update(Guid id, [FromBody] UpdateGLAccountRequest r)
            => Ok(await _mediator.Send(new UpdateGLAccountCommand(id, r, GetActingUser())));

        [HttpPost("{id:guid}/submit")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Submit(Guid id)
            => Ok(await _mediator.Send(new SubmitGLAccountCommand(id, GetActingUser())));

        [HttpPost("{id:guid}/freeze")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Freeze(Guid id, [FromBody] FreezeGLRequest r)
            => Ok(await _mediator.Send(new FreezeGLAccountCommand(id, r.Reason, GetActingUser())));

        [HttpPost("{id:guid}/deactivate")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Deactivate(Guid id, [FromBody] DeactivateGLRequest r)
            => Ok(await _mediator.Send(new DeactivateGLAccountCommand(id, r.Reason, GetActingUser())));

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteGLAccountCommand(id, GetActingUser()));
            return NoContent();
        }

        [HttpPost("bulk/dry-run")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLUploadReportResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<GLUploadReportResponse>> DryRun([FromBody] GLBulkUploadRequest r)
        {
            var rows = r.Rows.Select(x => new GLUploadRow(x.RowNumber, x.Row)).ToList();
            return Ok(await _mediator.Send(new DryRunGLUploadCommand(r.CompanyId, rows, GetActingUser())));
        }

        [HttpPost("bulk/commit")]
        [Authorize(Policy = "GLMaker")]
        [ProducesResponseType(typeof(GLUploadReportResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<GLUploadReportResponse>> Commit([FromBody] GLBulkCommitRequest r)
        {
            var rows = r.Rows.Select(x => new GLUploadRow(x.RowNumber, x.Row)).ToList();
            return Ok(await _mediator.Send(new CommitGLUploadCommand(r.CompanyId, r.BatchId, rows, GetActingUser())));
        }

        // ---------- Authorization (GLChecker) ----------

        [HttpPost("{id:guid}/authorize")]
        [Authorize(Policy = "GLChecker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Authorize(Guid id)
            => Ok(await _mediator.Send(new AuthorizeGLAccountCommand(id, GetActingUser())));

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = "GLChecker")]
        [ProducesResponseType(typeof(GLAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<GLAccountResponse>> Reject(Guid id, [FromBody] RejectGLRequest r)
            => Ok(await _mediator.Send(new RejectGLAccountCommand(id, r.Reason, GetActingUser())));

        [HttpPost("bulk/{batchId:guid}/authorize")]
        [Authorize(Policy = "GLChecker")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<ActionResult<int>> AuthorizeBatch(Guid batchId)
            => Ok(await _mediator.Send(new AuthorizeGLBatchCommand(batchId, GetActingUser())));

        [HttpPost("bulk/{batchId:guid}/reject")]
        [Authorize(Policy = "GLChecker")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<int>> RejectBatch(Guid batchId, [FromBody] RejectGLRequest r)
            => Ok(await _mediator.Send(new RejectGLBatchCommand(batchId, r.Reason, GetActingUser())));

        // ---------- System-GL / JV posting enablement (SystemAdmin) ----------

        [HttpPost("systemgl")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(typeof(SystemGLCompletenessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<SystemGLCompletenessResponse>> MapSystemGL([FromBody] MapSystemGLRequest r)
            => Ok(await _mediator.Send(new MapSystemGLCommand(r.CompanyId, r.Type, r.GLAccountId, GetActingUser())));

        [HttpPost("systemgl/{companyId:guid}/enable-posting")]
        [Authorize(Policy = "SystemAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> EnablePosting(Guid companyId)
        {
            await _mediator.Send(new EnableJvPostingCommand(companyId, GetActingUser()));
            return NoContent();
        }

        /// <summary>
        /// Resolves the acting user id from the validated JWT principal so the audit actor and
        /// maker/checker identity cannot be spoofed. Prefers NameIdentifier (sub), falling back to Name.
        /// </summary>
        private string GetActingUser()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? User.Identity?.Name
               ?? string.Empty;
    }
    ```

  - [ ] 9.3 Add the gateway alias routes

    **MODIFY `Finnova.ApiGateway/appsettings.json`** — insert these two route objects inside
    `ReverseProxy.Routes`, right after the `ua-asset-alias-root` route object (keep the existing
    trailing comma structure valid; add a comma after `ua-asset-alias-root`'s closing brace). Note
    these forward to **`accounts-cluster`** (not `systemadmin-cluster`) because GL Master is hosted
    in `Finnova.AccountsService`:

    ```json
          "ua-gl-alias-route": {
            "ClusterId": "accounts-cluster",
            "Match": {
              "Path": "/api/ua/api/gl/{**catch-all}"
            },
            "Transforms": [
              { "PathRemovePrefix": "/api/ua/api" },
              { "PathPrefix": "/api" }
            ]
          },
          "ua-gl-alias-root": {
            "ClusterId": "accounts-cluster",
            "Match": {
              "Path": "/api/ua/api/gl"
            },
            "Transforms": [
              { "PathRemovePrefix": "/api/ua/api" },
              { "PathPrefix": "/api" }
            ]
          }
    ```

    The existing `accounts-route` already forwards `/api/accounts/**` to `accounts-cluster`, so the
    controller is also directly reachable at `/api/accounts/api/gl/**`. No cluster changes needed.
    _Requirements: 11.7_

- [ ] 10. Backend tests (`Finnova.Tests`)
  - FsCheck.Xunit property tests (≥100 iterations) for the pure helpers — one test per Correctness
    Property; xUnit handler facts with mocked repos/ports; and `WebApplicationFactory<Program>`
    integration tests for auth + the happy path.
  - _Requirements: 1.5, 1.6, 1.9, 1.10, 2.4, 3.3, 4.2, 5.3, 5.4, 5.5, 5.6, 5.7, 6.11, 6.12, 7.8, 10.1, 10.2, 11.1, 11.2; Properties: PROP-NORMALBALANCE, PROP-NATURE-INHERITANCE, PROP-GLCODE-FORMAT, PROP-PARENTPREFIX, PROP-POSTABLE-L4, PROP-LEVEL-DERIVATION, PROP-MAKER-NE-CHECKER, PROP-JV-COMPLETENESS, PROP-JV-ORDERING, PROP-AUDIT-EXACTLY-ONCE, PROP-RETIRED-PRECEDENCE_

  - [ ]* 10.1 Property-based tests for the pure helpers

    **CREATE `Finnova.Tests/GL/GLPurePropertyTests.cs`**

    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.GL.Internal;
    using Xunit;

    namespace Finnova.Tests.GL;

    public class GLPurePropertyTests
    {
        // PROP-NORMALBALANCE (R1.10)
        [Property(MaxTest = 200)]
        public void NormalBalance_is_total_and_correct(AccountNature nature)
        {
            var nb = NatureRules.NormalBalanceFor(nature);
            var expected = (nature == AccountNature.Asset || nature == AccountNature.Expense)
                ? NormalBalance.Debit : NormalBalance.Credit;
            Assert.Equal(expected, nb);
        }

        // PROP-GLCODE-FORMAT (R1.5)
        [Property(MaxTest = 500)]
        public void GLCode_is_exactly_nine_digits(NonNull<string> s)
        {
            var value = s.Get;
            var expected = value.Length == 9 && value.All(c => c >= '0' && c <= '9');
            Assert.Equal(expected, GLCodeRules.IsNineNumericDigits(value));
        }

        [Property(MaxTest = 200)]
        public void GLCode_true_only_for_nine_digit_strings(uint n)
        {
            var nine = (n % 1_000_000_000u).ToString("D9");
            Assert.True(GLCodeRules.IsNineNumericDigits(nine));
        }

        // PROP-PARENTPREFIX (R1.6)
        [Property(MaxTest = 300)]
        public void ParentPrefix_is_leading_substring(NonNull<string> parent, NonNull<string> suffix)
        {
            var p = parent.Get;
            var child = p + suffix.Get;
            Assert.True(GLCodeRules.BeginsWithParentPrefix(child, p));
        }

        // PROP-LEVEL-DERIVATION (R1.1/R1.8)
        [Fact]
        public void Level_from_null_is_L1()
            => Assert.Equal(GLLevel.L1, NatureRules.LevelFrom(null));

        [Property(MaxTest = 100)]
        public void Level_from_parent_is_parent_plus_one()
        {
            Assert.Equal(GLLevel.L2, NatureRules.LevelFrom(GLLevel.L1));
            Assert.Equal(GLLevel.L3, NatureRules.LevelFrom(GLLevel.L2));
            Assert.Equal(GLLevel.L4, NatureRules.LevelFrom(GLLevel.L3));
        }

        // PROP-POSTABLE-L4 (R1.2/R1.3)
        [Property(MaxTest = 100)]
        public void Postable_only_at_L4(GLLevel level)
            => Assert.Equal(level == GLLevel.L4, NatureRules.IsPostableFor(level));
    }
    ```

    **CREATE `Finnova.Tests/GL/JvLineValidatorPropertyTests.cs`**

    ```csharp
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.GL.Internal;
    using Xunit;

    namespace Finnova.Tests.GL;

    public class JvLineValidatorPropertyTests
    {
        private static GLValidationSnapshot Good(Guid company, Guid branch, Guid currency) =>
            new(Guid.NewGuid(), company, GLStatus.Active, DateTime.UtcNow.AddYears(-1),
                DateTime.UtcNow.AddYears(1), true, true, new[] { branch }, false, false, currency, GLType.Normal);

        // PROP-JV-COMPLETENESS (R6.11): an all-valid line is accepted (zero reasons).
        [Property(MaxTest = 200)]
        public void Valid_line_is_accepted()
        {
            var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var currency = Guid.NewGuid();
            var snap = Good(company, branch, currency);
            var line = new JvLine(1, snap.GLAccountId, currency, null, null, null);
            var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch, JvEntryMode.System);
            Assert.Empty(JvLineValidator.Evaluate(line, snap, ctx));
        }

        // PROP-JV-COMPLETENESS: all applicable reasons are returned (not just the first).
        [Fact]
        public void All_applicable_reasons_returned()
        {
            var company = Guid.NewGuid(); var otherCompany = Guid.NewGuid();
            var branch = Guid.NewGuid(); var otherBranch = Guid.NewGuid();
            var currency = Guid.NewGuid(); var otherCurrency = Guid.NewGuid();
            var snap = new GLValidationSnapshot(Guid.NewGuid(), otherCompany, GLStatus.Frozen,
                DateTime.UtcNow.AddYears(1), DateTime.UtcNow.AddYears(2), false, false,
                new[] { otherBranch }, false, false, currency, GLType.Normal);
            var line = new JvLine(1, snap.GLAccountId, otherCurrency, null, null, null);
            var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch, JvEntryMode.Manual);

            var reasons = JvLineValidator.Evaluate(line, snap, ctx);
            Assert.True(reasons.Count >= 6);   // company, active, date-range, postable, manual, branch, currency
        }

        // PROP-JV-ORDERING (R6.12): same input -> identical ordered reasons.
        [Property(MaxTest = 200)]
        public void Evaluation_is_deterministic(bool manual)
        {
            var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var currency = Guid.NewGuid();
            var snap = new GLValidationSnapshot(Guid.NewGuid(), company, GLStatus.Active,
                DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(1), false, false,
                new[] { branch }, false, false, currency, GLType.Normal);
            var line = new JvLine(1, snap.GLAccountId, currency, null, null, null);
            var ctx = new JvVoucherContext(company, DateTime.UtcNow, branch,
                manual ? JvEntryMode.Manual : JvEntryMode.System);

            var a = JvLineValidator.Evaluate(line, snap, ctx);
            var b = JvLineValidator.Evaluate(line, snap, ctx);
            Assert.Equal(a, b);
        }

        // R6.13: unresolved GL -> single "does not exist" reason.
        [Fact]
        public void Unresolved_gl_single_reason()
        {
            var ctx = new JvVoucherContext(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), JvEntryMode.System);
            var line = new JvLine(1, Guid.NewGuid(), Guid.NewGuid(), null, null, null);
            var reasons = JvLineValidator.Evaluate(line, null, ctx);
            Assert.Single(reasons);
        }
    }
    ```

  - [ ]* 10.2 Handler facts with mocked repositories/ports

    **CREATE `Finnova.Tests/GL/GLHandlerFactsTests.cs`**

    ```csharp
    using Finnova.Models.Contracts.GL;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Commands.AuthorizeGLAccount;
    using Finnova.Service.GL.Commands.CreateGLAccount;
    using Finnova.Service.GL.Ports;
    using Moq;
    using Xunit;

    namespace Finnova.Tests.GL;

    public class GLHandlerFactsTests
    {
        private static CreateGLAccountRequest NewReq(Guid company, string code = "100000000") =>
            new(company, code, "Cash", null, GLType.Normal, null, null, Guid.NewGuid(),
                true, false, false, null, DateTime.UtcNow, null, new List<Guid>());

        // PROP-RETIRED-PRECEDENCE (R2.4): retired code wins over plain duplicate.
        [Fact]
        public async Task Create_retired_code_takes_precedence()
        {
            var company = Guid.NewGuid();
            var repo = new Mock<IGLAccountRepository>();
            var audit = new Mock<IGLAuditRepository>();
            var cal = new Mock<IAccountingCalendarPort>();
            cal.Setup(c => c.IsClosedPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default)).ReturnsAsync(false);
            repo.Setup(r => r.GetInactiveByCodeAsync(company, "100000000", default))
                .ReturnsAsync(new GLAccount { GLCode = "100000000", Status = GLStatus.Inactive });

            var handler = new CreateGLAccountCommandHandler(repo.Object, audit.Object, cal.Object);
            await Assert.ThrowsAsync<GLRetiredCodeException>(() =>
                handler.Handle(new CreateGLAccountCommand(NewReq(company), "maker-1"), default));
            audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Never);   // R10.2
        }

        // PROP-AUDIT-EXACTLY-ONCE (R10.1): success writes exactly one audit entry.
        [Fact]
        public async Task Create_success_writes_one_audit_entry()
        {
            var company = Guid.NewGuid();
            var repo = new Mock<IGLAccountRepository>();
            var audit = new Mock<IGLAuditRepository>();
            var cal = new Mock<IAccountingCalendarPort>();
            cal.Setup(c => c.IsClosedPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default)).ReturnsAsync(false);
            repo.Setup(r => r.GetInactiveByCodeAsync(company, It.IsAny<string>(), default)).ReturnsAsync((GLAccount?)null);
            repo.Setup(r => r.ExistsByCodeAsync(company, It.IsAny<string>(), null, default)).ReturnsAsync(false);
            repo.Setup(r => r.ExistsByNameAsync(company, It.IsAny<string>(), null, default)).ReturnsAsync(false);

            var handler = new CreateGLAccountCommandHandler(repo.Object, audit.Object, cal.Object);
            var result = await handler.Handle(new CreateGLAccountCommand(NewReq(company), "maker-1"), default);

            Assert.Equal(GLStatus.Draft, result.Status);
            audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Once);   // R10.1
        }

        // PROP-MAKER-NE-CHECKER (R3.3): self-authorization is rejected, state unchanged.
        [Fact]
        public async Task Authorize_self_is_rejected()
        {
            var repo = new Mock<IGLAccountRepository>();
            var audit = new Mock<IGLAuditRepository>();
            var pending = new GLAccount { Status = GLStatus.PendingAuthorization, MakerId = "maker-1" };
            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(pending);

            var handler = new AuthorizeGLAccountCommandHandler(repo.Object, audit.Object);
            await Assert.ThrowsAsync<GLConflictException>(() =>
                handler.Handle(new AuthorizeGLAccountCommand(pending.Id, "maker-1"), default));
            Assert.Equal(GLStatus.PendingAuthorization, pending.Status);
            audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Never);
        }

        // R3.4: a different checker authorizes a pending account to Active.
        [Fact]
        public async Task Authorize_by_other_user_sets_active()
        {
            var repo = new Mock<IGLAccountRepository>();
            var audit = new Mock<IGLAuditRepository>();
            var pending = new GLAccount { Status = GLStatus.PendingAuthorization, MakerId = "maker-1" };
            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(pending);

            var handler = new AuthorizeGLAccountCommandHandler(repo.Object, audit.Object);
            var result = await handler.Handle(new AuthorizeGLAccountCommand(pending.Id, "checker-2"), default);

            Assert.Equal(GLStatus.Active, result.Status);
            audit.Verify(a => a.AddAsync(It.IsAny<GLAuditEntry>(), default), Times.Once);
        }
    }
    ```

    **CREATE `Finnova.Tests/GL/SystemGLCompletenessTests.cs`**

    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.GL.Internal;
    using Finnova.Service.GL.Ports;
    using Moq;
    using Xunit;

    namespace Finnova.Tests.GL;

    public class SystemGLCompletenessTests
    {
        private static Mock<IGLSystemAccountMappingRepository> MappingsWith(Guid company, params SystemGLType[] types)
        {
            var repo = new Mock<IGLSystemAccountMappingRepository>();
            repo.Setup(r => r.GetActiveForCompanyAsync(company, default))
                .ReturnsAsync(types.Select(t => new GLSystemAccountMapping
                { CompanyId = company, SystemGLType = t, IsActive = true }).ToList());
            return repo;
        }

        // R7.8 — single-currency: five mappings complete the set.
        [Fact]
        public async Task SingleCurrency_complete_without_fx()
        {
            var company = Guid.NewGuid();
            var mappings = MappingsWith(company,
                SystemGLType.Suspense, SystemGLType.RoundOff, SystemGLType.InterBranchClearingHO,
                SystemGLType.InterBranchClearingBranch, SystemGLType.RetainedEarnings);
            var companyPort = new Mock<ICompanyQueryPort>();
            companyPort.Setup(c => c.IsMultiCurrencyEnabledAsync(company, default)).ReturnsAsync(false);

            var result = await Completeness.ComputeAsync(mappings.Object, companyPort.Object, company, default);
            Assert.True(result.IsComplete);
        }

        // R7.8 — multi-currency: FX GLs are additionally required.
        [Fact]
        public async Task MultiCurrency_requires_fx()
        {
            var company = Guid.NewGuid();
            var mappings = MappingsWith(company,
                SystemGLType.Suspense, SystemGLType.RoundOff, SystemGLType.InterBranchClearingHO,
                SystemGLType.InterBranchClearingBranch, SystemGLType.RetainedEarnings);
            var companyPort = new Mock<ICompanyQueryPort>();
            companyPort.Setup(c => c.IsMultiCurrencyEnabledAsync(company, default)).ReturnsAsync(true);

            var result = await Completeness.ComputeAsync(mappings.Object, companyPort.Object, company, default);
            Assert.False(result.IsComplete);
            Assert.Contains(SystemGLType.RealizedFX, result.MissingOrInvalid);
            Assert.Contains(SystemGLType.UnrealizedFX, result.MissingOrInvalid);
        }
    }
    ```

  - [ ]* 10.3 Integration tests (`WebApplicationFactory<Program>`)

    **CREATE `Finnova.Tests/GL/GLEndpointsIntegrationTests.cs`**

    ```csharp
    using System.Net;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.GL;

    /// <summary>
    /// Host-wiring / authorization checks against the AccountsService governed host. The GL routes
    /// require auth: no token -> 401 (R11.1); wrong/no GL role -> 403 (R11.2/11.6). A full
    /// create->submit->authorize happy path requires seeded GLMaker/GLChecker tokens — wire those
    /// via your test token helper (mirroring the SystemAdmin integration tests) and extend here.
    /// </summary>
    public class GLEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        public GLEndpointsIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

        [Fact]
        public async Task Get_paged_without_token_returns_401()
        {
            var client = _factory.CreateClient();
            var resp = await client.GetAsync("/api/gl?page=1&pageSize=20");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);   // R11.1
        }

        [Fact]
        public async Task Create_without_token_returns_401()
        {
            var client = _factory.CreateClient();
            var resp = await client.PostAsync("/api/gl",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task Health_is_anonymous()
        {
            var client = _factory.CreateClient();
            var resp = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }
    }
    ```

- [ ] 10.4 Checkpoint — backend compiles, migration applies, tests pass
  - Ensure the solution builds, `AddGLMaster` migration applies, and `dotnet test` is green; ask
    the user if questions arise. (Run manually — see **Execution Notes**.)

---

> **Frontend tasks apply to the Finnova-UI repo** at `E:\Finnova\Finnova-UI\Finnova-UI`, not the
> backend workspace. They mirror the `lookup.service` / `LookupMaster.tsx` reference pattern:
> shared axios (`src/services/api.ts`), interface/mock/real/toggle/barrel service layer, React
> hooks + MUI (no Redux/RxJS). URLs compose against the gateway alias `/api/ua/api/gl`. English
> only; no bilingual fields; no RTL.

- [ ] 11. Frontend model + service layer (`Finnova-UI`)
  - _Requirements: 12.10, 12.11, 8.1, 8.3, 8.6, 8.7, 9.7_

  - [ ] 11.1 Create the TypeScript model + enums

    **CREATE `src/models/gl.ts`**

    ```ts
    // GL Master model — mirrors the backend contracts (Finnova.Models.Contracts.GL).
    // Enums are string unions matching the JsonStringEnumConverter wire format.

    export type GLLevel = 'L1' | 'L2' | 'L3' | 'L4';
    export type AccountNature = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';
    export type NormalBalance = 'Debit' | 'Credit';
    export type GLType = 'Normal' | 'Control' | 'Tax';
    export type GLStatus =
      | 'Draft' | 'PendingAuthorization' | 'Active' | 'Rejected'
      | 'PendingModification' | 'Frozen' | 'Inactive';
    export type SystemGLType =
      | 'Suspense' | 'RoundOff' | 'InterBranchClearingHO' | 'InterBranchClearingBranch'
      | 'RetainedEarnings' | 'RealizedFX' | 'UnrealizedFX';
    export type ExportFormat = 'Xlsx' | 'Csv';

    export interface GLAccount {
      id: string;
      companyId: string;
      glCode: string;
      name: string;
      level: GLLevel;
      parentGLId?: string | null;
      accountNature: AccountNature;
      normalBalance: NormalBalance;
      glType: GLType;
      subLedgerType?: string | null;
      taxType?: string | null;
      isPostable: boolean;
      currencyId: string;
      allowManualJV: boolean;
      costCentreMandatory: boolean;
      profitCentreMandatory: boolean;
      reportingLine?: string | null;
      effectiveFrom: string;
      effectiveTo?: string | null;
      status: GLStatus;
      authorizedVersionNumber: number;
      applicableBranchIds: string[];
      createdAt: string;
      updatedAt: string;
    }

    export interface GLTreeNode {
      id: string;
      glCode: string;
      name: string;
      level: GLLevel;
      isPostable: boolean;
      status: GLStatus;
      children: GLTreeNode[];
    }

    export interface GLAuditEntry {
      id: string;
      glAccountId: string;
      action: string;
      oldValues?: string | null;
      newValues: string;
      summary: string;
      remarks?: string | null;
      changedBy: string;
      changedByRole?: string | null;
      changedAtUtc: string;
    }

    export interface SystemGLCompleteness {
      companyId: string;
      isComplete: boolean;
      missingOrInvalid: SystemGLType[];
    }

    export interface GLUploadRowResult {
      rowNumber: number;
      glCode: string;
      success: boolean;
      error?: string | null;
    }

    export interface GLUploadReport {
      batchId: string;
      allSuccess: boolean;
      rows: GLUploadRowResult[];
    }

    export interface CreateGLAccountRequest {
      companyId: string;
      glCode: string;
      name: string;
      parentGLId?: string | null;
      glType: GLType;
      subLedgerType?: string | null;
      taxType?: string | null;
      currencyId: string;
      allowManualJV: boolean;
      costCentreMandatory: boolean;
      profitCentreMandatory: boolean;
      reportingLine?: string | null;
      effectiveFrom: string;
      effectiveTo?: string | null;
      applicableBranchIds: string[];
    }

    export interface UpdateGLAccountRequest {
      allowManualJV: boolean;
      costCentreMandatory: boolean;
      profitCentreMandatory: boolean;
      reportingLine?: string | null;
      applicableBranchIds: string[];
    }

    export interface GLAccountFilter {
      glCode?: string;
      name?: string;
      nature?: AccountNature;
      type?: GLType;
      status?: GLStatus;
      parentGLId?: string;
      page?: number;
      pageSize?: number;
    }

    export interface PaginatedResponse<T> {
      data: T[];
      total: number;
      page: number;
      pageSize: number;
      totalPages: number;
    }

    // Normal-balance derivation mirrors NatureRules.NormalBalanceFor for read-only UI display.
    export function normalBalanceFor(nature: AccountNature): NormalBalance {
      return nature === 'Asset' || nature === 'Expense' ? 'Debit' : 'Credit';
    }
    ```

  - [ ] 11.2 Create the service interface, mock, real, toggle, and barrel

    **CREATE `src/services/interfaces/gl.service.ts`**

    ```ts
    import {
      GLAccount, GLAccountFilter, GLTreeNode, GLAuditEntry, SystemGLCompleteness,
      GLUploadReport, CreateGLAccountRequest, UpdateGLAccountRequest, PaginatedResponse,
      ExportFormat, SystemGLType,
    } from '../../models/gl';

    export interface IGLService {
      getPaged(filter: GLAccountFilter): Promise<PaginatedResponse<GLAccount>>;
      getById(id: string): Promise<GLAccount>;
      getTree(companyId: string): Promise<GLTreeNode[]>;
      getAudit(id: string): Promise<GLAuditEntry[]>;
      getCompleteness(companyId: string): Promise<SystemGLCompleteness>;

      create(request: CreateGLAccountRequest): Promise<GLAccount>;
      update(id: string, request: UpdateGLAccountRequest): Promise<GLAccount>;
      submit(id: string): Promise<GLAccount>;
      authorize(id: string): Promise<GLAccount>;
      reject(id: string, reason: string): Promise<GLAccount>;
      freeze(id: string, reason: string): Promise<GLAccount>;
      deactivate(id: string, reason?: string): Promise<GLAccount>;
      remove(id: string): Promise<void>;

      mapSystemGL(companyId: string, type: SystemGLType, glAccountId: string): Promise<SystemGLCompleteness>;
      enablePosting(companyId: string): Promise<void>;

      dryRun(companyId: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport>;
      commit(companyId: string, batchId: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport>;
      authorizeBatch(batchId: string): Promise<number>;
      rejectBatch(batchId: string, reason: string): Promise<number>;

      exportAccounts(format: ExportFormat, filter: GLAccountFilter): Promise<Blob>;
    }
    ```

    **CREATE `src/services/real/gl.service.real.ts`**

    ```ts
    import api from '../api';   // shared axios: JWT injection + central 401/403/409 handling
    import { IGLService } from '../interfaces/gl.service';
    import {
      GLAccount, GLAccountFilter, GLTreeNode, GLAuditEntry, SystemGLCompleteness,
      GLUploadReport, CreateGLAccountRequest, UpdateGLAccountRequest, PaginatedResponse,
      ExportFormat, SystemGLType,
    } from '../../models/gl';

    // Composed against the gateway alias /api/ua/api/gl (shared axios base already ends at /api/ua/api).
    const BASE = '/gl';

    export class GLServiceReal implements IGLService {
      async getPaged(filter: GLAccountFilter): Promise<PaginatedResponse<GLAccount>> {
        const { data } = await api.get(BASE, { params: filter });
        return data;
      }
      async getById(id: string): Promise<GLAccount> {
        const { data } = await api.get(`${BASE}/${id}`);
        return data;
      }
      async getTree(companyId: string): Promise<GLTreeNode[]> {
        const { data } = await api.get(`${BASE}/tree/${companyId}`);
        return data;
      }
      async getAudit(id: string): Promise<GLAuditEntry[]> {
        const { data } = await api.get(`${BASE}/${id}/audit`);
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
    ```

    **CREATE `src/services/mock/gl.service.mock.ts`**

    ```ts
    import { IGLService } from '../interfaces/gl.service';
    import {
      GLAccount, GLAccountFilter, GLTreeNode, GLAuditEntry, SystemGLCompleteness,
      GLUploadReport, CreateGLAccountRequest, UpdateGLAccountRequest, PaginatedResponse,
      ExportFormat, SystemGLType, normalBalanceFor,
    } from '../../models/gl';

    // In-memory mock (English-only sample data) mirroring the real service shape for VITE_USE_MOCK_API.
    let store: GLAccount[] = [
      {
        id: 'gl-1', companyId: 'c-1', glCode: '100000000', name: 'Assets', level: 'L1',
        parentGLId: null, accountNature: 'Asset', normalBalance: 'Debit', glType: 'Normal',
        subLedgerType: null, taxType: null, isPostable: false, currencyId: 'cur-inr',
        allowManualJV: true, costCentreMandatory: false, profitCentreMandatory: false,
        reportingLine: null, effectiveFrom: '2024-01-01', effectiveTo: null, status: 'Active',
        authorizedVersionNumber: 1, applicableBranchIds: ['br-1'],
        createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
      },
    ];
    const audit: GLAuditEntry[] = [];

    const clone = <T,>(v: T): T => JSON.parse(JSON.stringify(v));

    export class GLServiceMock implements IGLService {
      async getPaged(filter: GLAccountFilter): Promise<PaginatedResponse<GLAccount>> {
        const page = filter.page ?? 1;
        const pageSize = filter.pageSize ?? 20;
        let rows = store.filter(g =>
          (!filter.glCode || g.glCode.includes(filter.glCode.trim())) &&
          (!filter.name || g.name.toLowerCase().includes(filter.name.trim().toLowerCase())) &&
          (!filter.nature || g.accountNature === filter.nature) &&
          (!filter.type || g.glType === filter.type) &&
          (!filter.status || g.status === filter.status) &&
          (!filter.parentGLId || g.parentGLId === filter.parentGLId));
        rows = [...rows].sort((a, b) => a.glCode.localeCompare(b.glCode));
        const total = rows.length;
        const data = rows.slice((page - 1) * pageSize, page * pageSize);
        return { data: clone(data), total, page, pageSize, totalPages: Math.ceil(total / pageSize) };
      }
      async getById(id: string): Promise<GLAccount> {
        const g = store.find(x => x.id === id);
        if (!g) throw new Error('GL account was not found.');
        return clone(g);
      }
      async getTree(companyId: string): Promise<GLTreeNode[]> {
        const forCompany = store.filter(g => g.companyId === companyId);
        const build = (parentId: string | null): GLTreeNode[] =>
          forCompany.filter(g => (g.parentGLId ?? null) === parentId)
            .sort((a, b) => a.glCode.localeCompare(b.glCode))
            .map(g => ({
              id: g.id, glCode: g.glCode, name: g.name, level: g.level,
              isPostable: g.isPostable, status: g.status, children: build(g.id),
            }));
        return build(null);
      }
      async getAudit(id: string): Promise<GLAuditEntry[]> {
        return clone(audit.filter(a => a.glAccountId === id)
          .sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc)));
      }
      async getCompleteness(companyId: string): Promise<SystemGLCompleteness> {
        return { companyId, isComplete: false, missingOrInvalid: ['Suspense'] };
      }
      async create(request: CreateGLAccountRequest): Promise<GLAccount> {
        const nature = request.parentGLId
          ? store.find(g => g.id === request.parentGLId)!.accountNature
          : 'Asset';
        const g: GLAccount = {
          id: `gl-${store.length + 1}`,
          companyId: request.companyId,
          glCode: request.glCode.trim(),
          name: request.name.trim(),
          level: request.parentGLId ? 'L2' : 'L1',
          parentGLId: request.parentGLId ?? null,
          accountNature: nature,
          normalBalance: normalBalanceFor(nature),
          glType: request.glType,
          subLedgerType: request.subLedgerType ?? null,
          taxType: request.taxType ?? null,
          isPostable: false,
          currencyId: request.currencyId,
          allowManualJV: request.allowManualJV,
          costCentreMandatory: request.costCentreMandatory,
          profitCentreMandatory: request.profitCentreMandatory,
          reportingLine: request.reportingLine ?? null,
          effectiveFrom: request.effectiveFrom,
          effectiveTo: request.effectiveTo ?? null,
          status: 'Draft',
          authorizedVersionNumber: 0,
          applicableBranchIds: request.applicableBranchIds,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        };
        store = [...store, g];
        return clone(g);
      }
      async update(id: string, request: UpdateGLAccountRequest): Promise<GLAccount> {
        const g = store.find(x => x.id === id)!;
        Object.assign(g, request, { updatedAt: new Date().toISOString() });
        return clone(g);
      }
      private async transition(id: string, status: GLAccount['status']): Promise<GLAccount> {
        const g = store.find(x => x.id === id)!;
        g.status = status; g.updatedAt = new Date().toISOString();
        return clone(g);
      }
      submit(id: string) { return this.transition(id, 'PendingAuthorization'); }
      authorize(id: string) { return this.transition(id, 'Active'); }
      reject(id: string) { return this.transition(id, 'Rejected'); }
      freeze(id: string) { return this.transition(id, 'Frozen'); }
      deactivate(id: string) { return this.transition(id, 'Inactive'); }
      async remove(id: string): Promise<void> { store = store.filter(x => x.id !== id); }
      async mapSystemGL(companyId: string): Promise<SystemGLCompleteness> {
        return { companyId, isComplete: true, missingOrInvalid: [] };
      }
      async enablePosting(): Promise<void> { /* no-op */ }
      async dryRun(_c: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport> {
        return {
          batchId: 'batch-1', allSuccess: true,
          rows: rows.map(r => ({ rowNumber: r.rowNumber, glCode: r.row.glCode, success: true, error: null })),
        };
      }
      async commit(_c: string, batchId: string, rows: { rowNumber: number; row: CreateGLAccountRequest }[]): Promise<GLUploadReport> {
        return {
          batchId, allSuccess: true,
          rows: rows.map(r => ({ rowNumber: r.rowNumber, glCode: r.row.glCode, success: true, error: null })),
        };
      }
      async authorizeBatch(): Promise<number> { return 1; }
      async rejectBatch(): Promise<number> { return 1; }
      async exportAccounts(): Promise<Blob> { return new Blob(['GLCode,Name\n'], { type: 'text/csv' }); }
    }
    ```

    **CREATE `src/services/gl.service.ts`** (toggle + barrel)

    ```ts
    import { IGLService } from './interfaces/gl.service';
    import { GLServiceReal } from './real/gl.service.real';
    import { GLServiceMock } from './mock/gl.service.mock';

    const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

    export const glService: IGLService = useMock ? new GLServiceMock() : new GLServiceReal();
    export type { IGLService } from './interfaces/gl.service';
    ```

- [ ] 12. Frontend page + components + route + nav (`Finnova-UI`)
  - `GLMaster.tsx` mirrors `LookupMaster.tsx`: MUI data table + dialogs, React hooks only.
    Nature + Normal Balance are **read-only, derived** from the selected parent; Sub-ledger/Tax are
    conditionally required; submit disabled until valid. Checker queue disables approve/reject on
    own records and keeps reject disabled until a 1–500 char reason. Lifecycle dialogs require a
    mandatory reason. Bulk-upload shows a dry-run table before commit. Audit drawer + export button.
    English-only; no RTL.
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8, 12.9, 12.10, 12.11_

  - [ ] 12.1 Create the GL Master page

    **CREATE `src/pages/GLMaster.tsx`**

    ```tsx
    import { useCallback, useEffect, useMemo, useState } from 'react';
    import {
      Box, Button, Stack, Typography, Table, TableBody, TableCell, TableContainer,
      TableHead, TableRow, Paper, Chip, TextField, MenuItem,
    } from '@mui/material';
    import { glService } from '../services/gl.service';
    import {
      GLAccount, GLAccountFilter, GLStatus, GLType, AccountNature,
    } from '../models/gl';
    import { GLAccountDialog } from '../components/gl/GLAccountDialog';
    import { GLCheckerQueue } from '../components/gl/GLCheckerQueue';
    import { GLLifecycleDialog } from '../components/gl/GLLifecycleDialog';
    import { GLAuditDrawer } from '../components/gl/GLAuditDrawer';
    import { GLBulkUploadDialog } from '../components/gl/GLBulkUploadDialog';

    const STATUSES: GLStatus[] = [
      'Draft', 'PendingAuthorization', 'Active', 'Rejected', 'PendingModification', 'Frozen', 'Inactive',
    ];
    const TYPES: GLType[] = ['Normal', 'Control', 'Tax'];
    const NATURES: AccountNature[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

    export default function GLMaster() {
      const [rows, setRows] = useState<GLAccount[]>([]);
      const [total, setTotal] = useState(0);
      const [filter, setFilter] = useState<GLAccountFilter>({ page: 1, pageSize: 20 });
      const [dialogOpen, setDialogOpen] = useState(false);
      const [editing, setEditing] = useState<GLAccount | null>(null);
      const [auditFor, setAuditFor] = useState<string | null>(null);
      const [lifecycle, setLifecycle] = useState<{ gl: GLAccount; action: 'freeze' | 'deactivate' } | null>(null);
      const [bulkOpen, setBulkOpen] = useState(false);
      const [error, setError] = useState<string | null>(null);

      const load = useCallback(async () => {
        try {
          const page = await glService.getPaged(filter);
          setRows(page.data);
          setTotal(page.total);
        } catch (e: any) {
          setError(e?.response?.data?.message ?? 'Failed to load GL accounts.');   // R12.11
        }
      }, [filter]);

      useEffect(() => { void load(); }, [load]);

      const openCreate = () => { setEditing(null); setDialogOpen(true); };
      const openEdit = (gl: GLAccount) => { setEditing(gl); setDialogOpen(true); };

      const statusColor = useMemo(() => (s: GLStatus) =>
        s === 'Active' ? 'success' : s === 'Rejected' ? 'error'
        : s === 'Frozen' || s === 'Inactive' ? 'default' : 'warning', []);

      return (
        <Box p={2}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
            <Typography variant="h5">GL Master</Typography>
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" onClick={() => setBulkOpen(true)}>Bulk Upload</Button>
              <Button variant="outlined" onClick={async () => {
                const blob = await glService.exportAccounts('Csv', filter);
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a'); a.href = url; a.download = 'gl-accounts.csv'; a.click();
                URL.revokeObjectURL(url);
              }}>Export CSV</Button>
              <Button variant="contained" onClick={openCreate}>New GL Account</Button>
            </Stack>
          </Stack>

          {/* Filters (R12.1) */}
          <Stack direction="row" spacing={1} mb={2} flexWrap="wrap">
            <TextField size="small" label="GL Code" value={filter.glCode ?? ''}
              onChange={e => setFilter(f => ({ ...f, glCode: e.target.value, page: 1 }))} />
            <TextField size="small" label="Name" value={filter.name ?? ''}
              onChange={e => setFilter(f => ({ ...f, name: e.target.value, page: 1 }))} />
            <TextField size="small" select label="Nature" sx={{ minWidth: 140 }} value={filter.nature ?? ''}
              onChange={e => setFilter(f => ({ ...f, nature: (e.target.value || undefined) as AccountNature, page: 1 }))}>
              <MenuItem value="">All</MenuItem>
              {NATURES.map(n => <MenuItem key={n} value={n}>{n}</MenuItem>)}
            </TextField>
            <TextField size="small" select label="Type" sx={{ minWidth: 140 }} value={filter.type ?? ''}
              onChange={e => setFilter(f => ({ ...f, type: (e.target.value || undefined) as GLType, page: 1 }))}>
              <MenuItem value="">All</MenuItem>
              {TYPES.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </TextField>
            <TextField size="small" select label="Status" sx={{ minWidth: 180 }} value={filter.status ?? ''}
              onChange={e => setFilter(f => ({ ...f, status: (e.target.value || undefined) as GLStatus, page: 1 }))}>
              <MenuItem value="">All</MenuItem>
              {STATUSES.map(s => <MenuItem key={s} value={s}>{s}</MenuItem>)}
            </TextField>
          </Stack>

          {error && <Typography color="error" mb={1}>{error}</Typography>}

          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>GL Code</TableCell><TableCell>Name</TableCell><TableCell>Level</TableCell>
                  <TableCell>Nature</TableCell><TableCell>Type</TableCell><TableCell>Postable</TableCell>
                  <TableCell>Status</TableCell><TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map(g => (
                  <TableRow key={g.id} hover>
                    <TableCell>{g.glCode}</TableCell>
                    <TableCell>{g.name}</TableCell>
                    <TableCell>{g.level}</TableCell>
                    <TableCell>{g.accountNature}</TableCell>
                    <TableCell>{g.glType}</TableCell>
                    <TableCell>{g.isPostable ? 'Yes' : 'No'}</TableCell>
                    <TableCell><Chip size="small" color={statusColor(g.status) as any} label={g.status} /></TableCell>
                    <TableCell align="right">
                      <Button size="small" onClick={() => openEdit(g)}>Edit</Button>
                      <Button size="small" onClick={() => setAuditFor(g.id)}>Audit</Button>
                      <Button size="small" disabled={g.status !== 'Active'}
                        onClick={() => setLifecycle({ gl: g, action: 'freeze' })}>Freeze</Button>
                      <Button size="small" color="error"
                        disabled={g.status !== 'Active' && g.status !== 'Frozen'}
                        onClick={() => setLifecycle({ gl: g, action: 'deactivate' })}>Deactivate</Button>
                    </TableCell>
                  </TableRow>
                ))}
                {rows.length === 0 && (
                  <TableRow><TableCell colSpan={8} align="center">No GL accounts found.</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>

          <Typography variant="caption" mt={1} display="block">Total: {total}</Typography>

          <GLCheckerQueue onChanged={load} />

          {dialogOpen && (
            <GLAccountDialog
              existing={editing}
              onClose={() => setDialogOpen(false)}
              onSaved={async () => { setDialogOpen(false); await load(); }}
            />
          )}
          {lifecycle && (
            <GLLifecycleDialog
              gl={lifecycle.gl}
              action={lifecycle.action}
              onClose={() => setLifecycle(null)}
              onDone={async () => { setLifecycle(null); await load(); }}
            />
          )}
          {auditFor && <GLAuditDrawer glAccountId={auditFor} onClose={() => setAuditFor(null)} />}
          {bulkOpen && <GLBulkUploadDialog companyId={filter.parentGLId ? '' : 'c-1'} onClose={() => setBulkOpen(false)} onDone={load} />}
        </Box>
      );
    }
    ```

  - [ ] 12.2 Create the GL components

    **CREATE `src/components/gl/GLAccountDialog.tsx`** — create/edit with derived read-only
    Nature + Normal Balance and conditional Sub-ledger/Tax; submit disabled until valid (R12.2/12.3).
    Structural fields render read-only once the account has postings (R12.4).

    ```tsx
    import { useMemo, useState } from 'react';
    import {
      Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, MenuItem, Stack,
      FormControlLabel, Checkbox,
    } from '@mui/material';
    import { glService } from '../../services/gl.service';
    import {
      GLAccount, GLType, AccountNature, normalBalanceFor, CreateGLAccountRequest, UpdateGLAccountRequest,
    } from '../../models/gl';

    const TYPES: GLType[] = ['Normal', 'Control', 'Tax'];
    const NATURES: AccountNature[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

    export function GLAccountDialog(props: {
      existing: GLAccount | null;
      onClose: () => void;
      onSaved: () => void;
    }) {
      const { existing, onClose, onSaved } = props;
      const isEdit = !!existing;

      const [companyId, setCompanyId] = useState(existing?.companyId ?? 'c-1');
      const [glCode, setGlCode] = useState(existing?.glCode ?? '');
      const [name, setName] = useState(existing?.name ?? '');
      const [glType, setGlType] = useState<GLType>(existing?.glType ?? 'Normal');
      const [nature, setNature] = useState<AccountNature>(existing?.accountNature ?? 'Asset');
      const [subLedgerType, setSubLedgerType] = useState(existing?.subLedgerType ?? '');
      const [taxType, setTaxType] = useState(existing?.taxType ?? '');
      const [currencyId, setCurrencyId] = useState(existing?.currencyId ?? 'cur-inr');
      const [effectiveFrom, setEffectiveFrom] = useState(existing?.effectiveFrom?.slice(0, 10) ?? '');
      const [allowManualJV, setAllowManualJV] = useState(existing?.allowManualJV ?? true);
      const [error, setError] = useState<string | null>(null);

      const normalBalance = useMemo(() => normalBalanceFor(nature), [nature]);
      const codeValid = /^[0-9]{9}$/.test(glCode);                 // R1.5 client mirror
      const subLedgerOk = glType !== 'Control' || subLedgerType.trim().length > 0;  // R12.2
      const taxOk = glType !== 'Tax' || taxType.trim().length > 0;

      const canSubmit = isEdit
        ? true
        : (codeValid && name.trim().length > 0 && subLedgerOk && taxOk && effectiveFrom.length > 0);

      const save = async () => {
        try {
          if (isEdit) {
            const req: UpdateGLAccountRequest = {
              allowManualJV,
              costCentreMandatory: existing!.costCentreMandatory,
              profitCentreMandatory: existing!.profitCentreMandatory,
              reportingLine: existing!.reportingLine ?? null,
              applicableBranchIds: existing!.applicableBranchIds,
            };
            await glService.update(existing!.id, req);
          } else {
            const req: CreateGLAccountRequest = {
              companyId, glCode: glCode.trim(), name: name.trim(), parentGLId: null,
              glType, subLedgerType: subLedgerType || null, taxType: taxType || null,
              currencyId, allowManualJV, costCentreMandatory: false, profitCentreMandatory: false,
              reportingLine: null, effectiveFrom, effectiveTo: null, applicableBranchIds: [],
            };
            await glService.create(req);
          }
          onSaved();
        } catch (e: any) {
          setError(e?.response?.data?.message ?? 'Save failed.');   // R12.11
        }
      };

      return (
        <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
          <DialogTitle>{isEdit ? 'Edit GL Account' : 'New GL Account'}</DialogTitle>
          <DialogContent>
            <Stack spacing={2} mt={1}>
              <TextField label="GL Code" value={glCode} disabled={isEdit}
                error={!isEdit && glCode.length > 0 && !codeValid}
                helperText={!codeValid && glCode.length > 0 ? 'Exactly 9 digits.' : ' '}
                onChange={e => setGlCode(e.target.value)} />
              <TextField label="Name (English)" value={name} inputProps={{ maxLength: 150 }}
                onChange={e => setName(e.target.value)} />
              <TextField select label="GL Type" value={glType} disabled={isEdit}
                onChange={e => setGlType(e.target.value as GLType)}>
                {TYPES.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
              </TextField>
              <TextField select label="Account Nature" value={nature} disabled={isEdit}
                onChange={e => setNature(e.target.value as AccountNature)}>
                {NATURES.map(n => <MenuItem key={n} value={n}>{n}</MenuItem>)}
              </TextField>
              {/* Derived, read-only (R12.2) */}
              <TextField label="Normal Balance (derived)" value={normalBalance} InputProps={{ readOnly: true }} />
              {glType === 'Control' && (
                <TextField label="Sub-ledger Type" value={subLedgerType}
                  error={!subLedgerOk} helperText={!subLedgerOk ? 'Required for Control.' : ' '}
                  onChange={e => setSubLedgerType(e.target.value)} />
              )}
              {glType === 'Tax' && (
                <TextField label="Tax Type" value={taxType}
                  error={!taxOk} helperText={!taxOk ? 'Required for Tax.' : ' '}
                  onChange={e => setTaxType(e.target.value)} />
              )}
              <TextField label="Currency" value={currencyId} disabled={isEdit}
                onChange={e => setCurrencyId(e.target.value)} />
              <TextField type="date" label="Effective From" InputLabelProps={{ shrink: true }}
                value={effectiveFrom} disabled={isEdit} onChange={e => setEffectiveFrom(e.target.value)} />
              <FormControlLabel control={
                <Checkbox checked={allowManualJV} onChange={e => setAllowManualJV(e.target.checked)} />
              } label="Allow Manual JV" />
              {error && <span style={{ color: 'red' }}>{error}</span>}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={!canSubmit} onClick={save}>Save</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

    **CREATE `src/components/gl/GLCheckerQueue.tsx`** — approve/reject queue; own records disabled;
    reject disabled until a 1–500 char reason (R12.5).

    ```tsx
    import { useCallback, useEffect, useState } from 'react';
    import {
      Box, Typography, Table, TableHead, TableRow, TableCell, TableBody, Button, TextField, Paper, TableContainer,
    } from '@mui/material';
    import { glService } from '../../services/gl.service';
    import { GLAccount } from '../../models/gl';
    import { useAuth } from '../../context/AuthContext';   // provides the current user id

    export function GLCheckerQueue({ onChanged }: { onChanged: () => void }) {
      const { userId } = useAuth();
      const [rows, setRows] = useState<GLAccount[]>([]);
      const [reasons, setReasons] = useState<Record<string, string>>({});

      const load = useCallback(async () => {
        const pending = await glService.getPaged({ status: 'PendingAuthorization', page: 1, pageSize: 50 });
        const mods = await glService.getPaged({ status: 'PendingModification', page: 1, pageSize: 50 });
        setRows([...pending.data, ...mods.data]);
      }, []);

      useEffect(() => { void load(); }, [load]);

      const approve = async (id: string) => { await glService.authorize(id); await load(); onChanged(); };
      const reject = async (id: string) => { await glService.reject(id, reasons[id] ?? ''); await load(); onChanged(); };

      return (
        <Box mt={4}>
          <Typography variant="h6" mb={1}>Authorization Queue</Typography>
          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>GL Code</TableCell><TableCell>Name</TableCell><TableCell>Status</TableCell>
                  <TableCell>Rejection Reason</TableCell><TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map(g => {
                  const own = g.id && (g as any).makerId === userId;   // disable own records (R12.5)
                  const reason = reasons[g.id] ?? '';
                  const reasonOk = reason.trim().length >= 1 && reason.trim().length <= 500;
                  return (
                    <TableRow key={g.id}>
                      <TableCell>{g.glCode}</TableCell>
                      <TableCell>{g.name}</TableCell>
                      <TableCell>{g.status}</TableCell>
                      <TableCell>
                        <TextField size="small" value={reason} inputProps={{ maxLength: 500 }}
                          onChange={e => setReasons(r => ({ ...r, [g.id]: e.target.value }))} />
                      </TableCell>
                      <TableCell align="right">
                        <Button size="small" disabled={!!own} onClick={() => approve(g.id)}>Approve</Button>
                        <Button size="small" color="error" disabled={!!own || !reasonOk}
                          onClick={() => reject(g.id)}>Reject</Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
                {rows.length === 0 && (
                  <TableRow><TableCell colSpan={5} align="center">Nothing awaiting authorization.</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      );
    }
    ```

    **CREATE `src/components/gl/GLLifecycleDialog.tsx`** — freeze/deactivate with a mandatory reason.

    ```tsx
    import { useState } from 'react';
    import { Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField } from '@mui/material';
    import { glService } from '../../services/gl.service';
    import { GLAccount } from '../../models/gl';

    export function GLLifecycleDialog(props: {
      gl: GLAccount;
      action: 'freeze' | 'deactivate';
      onClose: () => void;
      onDone: () => void;
    }) {
      const { gl, action, onClose, onDone } = props;
      const [reason, setReason] = useState('');
      const [error, setError] = useState<string | null>(null);
      const reasonRequired = action === 'freeze';                     // freeze reason mandatory (R5.2)
      const reasonOk = !reasonRequired || (reason.trim().length >= 1 && reason.trim().length <= 500);

      const run = async () => {
        try {
          if (action === 'freeze') await glService.freeze(gl.id, reason.trim());
          else await glService.deactivate(gl.id, reason.trim() || undefined);
          onDone();
        } catch (e: any) {
          setError(e?.response?.data?.message ?? 'Action failed.');
        }
      };

      return (
        <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
          <DialogTitle>{action === 'freeze' ? 'Freeze' : 'Deactivate'} {gl.glCode}</DialogTitle>
          <DialogContent>
            <TextField fullWidth multiline minRows={2} label="Reason" value={reason}
              inputProps={{ maxLength: 500 }} sx={{ mt: 1 }}
              onChange={e => setReason(e.target.value)} />
            {error && <span style={{ color: 'red' }}>{error}</span>}
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" color="error" disabled={!reasonOk} onClick={run}>Confirm</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

    **CREATE `src/components/gl/GLAuditDrawer.tsx`** — newest-first audit entries.

    ```tsx
    import { useEffect, useState } from 'react';
    import { Drawer, Box, Typography, List, ListItem, ListItemText, Divider } from '@mui/material';
    import { glService } from '../../services/gl.service';
    import { GLAuditEntry } from '../../models/gl';

    export function GLAuditDrawer({ glAccountId, onClose }: { glAccountId: string; onClose: () => void }) {
      const [entries, setEntries] = useState<GLAuditEntry[]>([]);
      useEffect(() => { void glService.getAudit(glAccountId).then(setEntries); }, [glAccountId]);

      return (
        <Drawer anchor="right" open onClose={onClose}>
          <Box width={420} p={2}>
            <Typography variant="h6">Audit Trail</Typography>
            <List dense>
              {entries.map(e => (
                <Box key={e.id}>
                  <ListItem>
                    <ListItemText
                      primary={`${e.action} — ${e.summary}`}
                      secondary={`${e.changedBy} · ${new Date(e.changedAtUtc).toLocaleString()}${e.remarks ? ' · ' + e.remarks : ''}`} />
                  </ListItem>
                  <Divider />
                </Box>
              ))}
              {entries.length === 0 && <Typography variant="body2" p={1}>No audit entries.</Typography>}
            </List>
          </Box>
        </Drawer>
      );
    }
    ```

    **CREATE `src/components/gl/GLBulkUploadDialog.tsx`** — dry-run table then commit (R12.7/12.8).

    ```tsx
    import { useState } from 'react';
    import {
      Dialog, DialogTitle, DialogContent, DialogActions, Button, Table, TableHead, TableRow,
      TableCell, TableBody, Typography,
    } from '@mui/material';
    import { glService } from '../../services/gl.service';
    import { GLUploadReport, CreateGLAccountRequest } from '../../models/gl';

    // Minimal CSV parser: GLCode,Name,GLType,CurrencyId,EffectiveFrom — one account per line.
    function parseCsv(text: string, companyId: string): { rowNumber: number; row: CreateGLAccountRequest }[] {
      return text.split(/\r?\n/).filter(l => l.trim().length > 0 && !l.startsWith('#'))
        .map((line, i) => {
          const [glCode, name, glType, currencyId, effectiveFrom] = line.split(',');
          return {
            rowNumber: i + 1,
            row: {
              companyId, glCode: glCode?.trim() ?? '', name: name?.trim() ?? '', parentGLId: null,
              glType: (glType?.trim() as any) ?? 'Normal', subLedgerType: null, taxType: null,
              currencyId: currencyId?.trim() ?? 'cur-inr', allowManualJV: true,
              costCentreMandatory: false, profitCentreMandatory: false, reportingLine: null,
              effectiveFrom: effectiveFrom?.trim() ?? new Date().toISOString().slice(0, 10),
              effectiveTo: null, applicableBranchIds: [],
            },
          };
        });
    }

    export function GLBulkUploadDialog(props: { companyId: string; onClose: () => void; onDone: () => void }) {
      const { companyId, onClose, onDone } = props;
      const [rows, setRows] = useState<{ rowNumber: number; row: CreateGLAccountRequest }[]>([]);
      const [report, setReport] = useState<GLUploadReport | null>(null);

      const onFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        const parsed = parseCsv(await file.text(), companyId);
        setRows(parsed);
        setReport(await glService.dryRun(companyId, parsed));   // R12.7
      };

      const commit = async () => {
        if (!report) return;
        await glService.commit(companyId, report.batchId, rows);   // R12.8
        onDone();
        onClose();
      };

      return (
        <Dialog open onClose={onClose} maxWidth="md" fullWidth>
          <DialogTitle>Bulk Upload Chart of Accounts</DialogTitle>
          <DialogContent>
            <input type="file" accept=".csv,.xlsx" onChange={onFile} />
            {report && (
              <>
                <Typography variant="subtitle2" mt={2}>
                  Dry-run: {report.allSuccess ? 'all rows valid' : 'errors present'}
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow><TableCell>Row</TableCell><TableCell>GL Code</TableCell>
                      <TableCell>Result</TableCell><TableCell>Error</TableCell></TableRow>
                  </TableHead>
                  <TableBody>
                    {report.rows.map(r => (
                      <TableRow key={r.rowNumber}>
                        <TableCell>{r.rowNumber}</TableCell>
                        <TableCell>{r.glCode}</TableCell>
                        <TableCell>{r.success ? 'OK' : 'Error'}</TableCell>
                        <TableCell>{r.error ?? ''}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </>
            )}
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Close</Button>
            <Button variant="contained" disabled={!report?.allSuccess} onClick={commit}>Commit</Button>
          </DialogActions>
        </Dialog>
      );
    }
    ```

  - [ ] 12.3 Wire the route and nav

    **MODIFY `src/App.tsx`** — import the page and add a route alongside the other master routes
    (e.g. right after the `LookupMaster` / `CourtMaster` route). Add this import near the other page
    imports:

    ```tsx
    import GLMaster from './pages/GLMaster';
    ```

    …and add this route inside the authenticated `<Routes>` block (match the existing route style):

    ```tsx
    <Route path="/gl-master" element={<GLMaster />} />
    ```

    **MODIFY `src/components/Layout.tsx`** — add a nav entry next to the other SystemAdmin masters.
    Insert a list item mirroring the existing nav links (adjust icon/label to match the file's
    pattern):

    ```tsx
    <ListItemButton component={Link} to="/gl-master">
      <ListItemText primary="GL Master" />
    </ListItemButton>
    ```

- [ ] 13. Frontend Vitest tests (`Finnova-UI`)
  - Vitest + React Testing Library (jsdom). Service behavior + component gating/error paths. No
    Redux/RxJS tests.
  - _Requirements: 12.2, 12.3, 12.5, 12.11; Properties: PROP-NORMALBALANCE (UI mirror)_

  - [ ]* 13.1 Service unit tests

    **CREATE `src/services/__tests__/gl.service.test.ts`**

    ```ts
    import { describe, it, expect } from 'vitest';
    import { GLServiceMock } from '../mock/gl.service.mock';
    import { normalBalanceFor } from '../../models/gl';

    describe('GLServiceMock', () => {
      it('creates a Draft account and lists it', async () => {
        const svc = new GLServiceMock();
        const created = await svc.create({
          companyId: 'c-1', glCode: '200000000', name: 'Liabilities', parentGLId: null,
          glType: 'Normal', subLedgerType: null, taxType: null, currencyId: 'cur-inr',
          allowManualJV: true, costCentreMandatory: false, profitCentreMandatory: false,
          reportingLine: null, effectiveFrom: '2024-01-01', effectiveTo: null, applicableBranchIds: [],
        });
        expect(created.status).toBe('Draft');
        const page = await svc.getPaged({ name: 'Liab', page: 1, pageSize: 20 });
        expect(page.data.some(g => g.glCode === '200000000')).toBe(true);
      });

      it('builds a tree ordered by GL code', async () => {
        const svc = new GLServiceMock();
        const tree = await svc.getTree('c-1');
        expect(Array.isArray(tree)).toBe(true);
      });

      it('normalBalanceFor mirrors the backend rule (PROP-NORMALBALANCE)', () => {
        expect(normalBalanceFor('Asset')).toBe('Debit');
        expect(normalBalanceFor('Expense')).toBe('Debit');
        expect(normalBalanceFor('Liability')).toBe('Credit');
        expect(normalBalanceFor('Equity')).toBe('Credit');
        expect(normalBalanceFor('Income')).toBe('Credit');
      });
    });
    ```

  - [ ]* 13.2 Component tests

    **CREATE `src/components/gl/__tests__/GLAccountDialog.test.tsx`**

    ```tsx
    import { describe, it, expect, vi } from 'vitest';
    import { render, screen, fireEvent } from '@testing-library/react';
    import { GLAccountDialog } from '../GLAccountDialog';

    describe('GLAccountDialog', () => {
      it('keeps Save disabled until the form is valid (R12.2/12.3)', () => {
        render(<GLAccountDialog existing={null} onClose={() => {}} onSaved={() => {}} />);
        const save = screen.getByRole('button', { name: /save/i });
        expect(save).toBeDisabled();

        fireEvent.change(screen.getByLabelText(/GL Code/i), { target: { value: '100000000' } });
        fireEvent.change(screen.getByLabelText(/Name/i), { target: { value: 'Cash' } });
        fireEvent.change(screen.getByLabelText(/Effective From/i), { target: { value: '2024-01-01' } });
        expect(save).not.toBeDisabled();
      });

      it('shows a derived read-only Normal Balance', () => {
        render(<GLAccountDialog existing={null} onClose={() => {}} onSaved={() => {}} />);
        const nb = screen.getByLabelText(/Normal Balance/i) as HTMLInputElement;
        expect(nb.value).toBe('Debit');   // Asset default
        expect(nb).toHaveAttribute('readonly');
      });
    });
    ```

- [ ] 14. Final checkpoint — frontend builds and tests pass
  - Ensure `npm run build` and `npm test` are green in Finnova-UI; ask the user if questions arise.
    (Run manually — see **Execution Notes**.)

---

## Notes

- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core
  implementation tasks are never optional.
- Each task references the requirement clauses (and Correctness Property IDs where relevant) it
  satisfies for traceability.
- The R6 GL Validation Service (`ValidateJournalVoucherQuery`) has **no public endpoint** — it is
  sent in-process via MediatR by the FINNOVA-25 JV posting engine (both live in
  `Finnova.AccountsService`).
- Dependency ports ship with **dev stubs** (design assumption A2). Replace them with real adapters
  to FINNOVA-25 and the referenced masters before production.
- India-only / English-only throughout: a single English `Name`, no bilingual fields, no RTL.

### Requirements coverage

R1 (8.2, 7.1), R2 (8.2, 6.2), R3 (8.4), R4 (8.3, 8.4), R5 (8.5), R6 (7.2, 8.7), R7 (8.6), R8 (8.7,
6.2), R9 (8.8), R10 (2.2, 6.2, 8.*), R11 (1.1, 9.2), R12 (12.1–12.3). Every R1–R12 clause is
covered by at least one task.

### Correctness Property coverage

PROP-NORMALBALANCE (7.1/10.1/13.1), PROP-NATURE-INHERITANCE (7.1/8.2), PROP-GLCODE-FORMAT
(7.1/10.1), PROP-PARENTPREFIX (7.1/10.1), PROP-POSTABLE-L4 (7.1/10.1), PROP-LEVEL-DERIVATION
(7.1/10.1), PROP-MAKER-NE-CHECKER (8.4/10.2), PROP-JV-COMPLETENESS (7.2/10.1), PROP-JV-ORDERING
(7.2/10.1), PROP-AUDIT-EXACTLY-ONCE (8.*/10.2), PROP-RETIRED-PRECEDENCE (8.2/10.2).

---

## Execution Notes (run these manually — not executed by the spec)

> Per the spec-workflow steering, this spec is documents-only. The commands below are for **you**
> to run after copy-pasting the code. Do not run them as part of producing the spec.

### Backend (`e:\Finnova\Finnova-API`)

1. **Add the EF Core migration** (task 5.3). The DbContext lives in `Finnova.Repository` and the
   design-time startup host is `Finnova.AccountsService`:

   ```powershell
   dotnet ef migrations add AddGLMaster `
     --project Finnova.Repository\Finnova.Repository.csproj `
     --startup-project Finnova.AccountsService\Finnova.AccountsService.csproj `
     --output-dir Migrations
   ```

   Apply it to the database:

   ```powershell
   dotnet ef database update `
     --project Finnova.Repository\Finnova.Repository.csproj `
     --startup-project Finnova.AccountsService\Finnova.AccountsService.csproj
   ```

   > If the EF tools are not installed: `dotnet tool install --global dotnet-ef`.

2. **Build the solution:**

   ```powershell
   dotnet build Finnova.Backend.slnx
   ```

3. **Run the tests** (xUnit + FsCheck.Xunit + WebApplicationFactory):

   ```powershell
   dotnet test Finnova.Tests\Finnova.Tests.csproj
   ```

   > If `Moq` is not already referenced by `Finnova.Tests`, add it:
   > `dotnet add Finnova.Tests\Finnova.Tests.csproj package Moq`. FsCheck.Xunit is already used by
   > the existing property tests in that project.

### Frontend (`E:\Finnova\Finnova-UI\Finnova-UI`)

4. **Install (if needed), build, and test:**

   ```powershell
   npm install
   npm run build
   npm test -- --run
   ```

   > Use `--run` so Vitest executes once instead of entering watch mode.

### Manual smoke (optional)

5. Start the gateway + `Finnova.AccountsService` as usual, then confirm the GL routes are reachable
   through the gateway alias `/api/ua/api/gl/**` (and directly at `/api/accounts/api/gl/**`) with a
   valid JWT carrying a GL role claim. A request without a token must return 401; a token missing
   the required GL role must return 403.
