# Implementation Plan: Customer Master, Customer References, and Mind Mappings

**Feature:** `customer-master-reference-mind-mappings`  
**Repositories:** `e:\Finnova\Finnova-API` and `E:\Finnova\Finnova-UI\Finnova-UI`  
**Implementation languages:** C#/.NET 10 backend; TypeScript/React 18 frontend  
**Status:** Contract-resolution gated; downstream code tasks are intentionally blocked until Task 0 is complete.

## Overview

This handoff implements the requirements and design as a dependency-ordered vertical slice across the existing SystemAdmin backend and Finnova UI. Customer Master, Customer References, and Mind Mappings remain separate aggregates, contracts, services, audit scopes, and UI flows. Location remains an external authoritative reference. Applicable Aadhaar/PAN validation is performed through an `IDocumentVerifierClient` adapter before a Customer create/update commits, and only the approved Verification Outcome is retained.

The source requirements and design explicitly do **not** contain the FINNOVA-51..55 field lists and acceptance criteria or the Finnova-Document-Verifier wire contract. Therefore, this file does not invent Customer fields, controlled enum members, verifier status values, retry/timeout/idempotency values, route tokens, gateway aliases, or error-code values. Tasks marked `BLOCKED` contain a non-executable handoff template or an exact repository anchor plus an insertion contract; they are not safe to copy-paste until Task 0 resolves the named values. Do not replace unresolved tokens with guesses.

The existing `.config.kiro`, `requirements.md`, and `design.md` are not modified by this task. No backend/frontend source is modified by this task phase. The commands in the validation and migration sections are user-run instructions only.

## Prerequisites and dependency gate

1. Read `.kiro/specs/customer-master-reference-mind-mappings/requirements.md` and `design.md` before applying any task.
2. Complete Task 0 and record the approved values in the implementation work item or an approved contract artifact before copying any blocked code.
3. Resolve every `<RESOLVE:...>` token before applying the corresponding code block. A token left unresolved is a hard stop, not a value to infer.
4. Preserve the existing architecture: `Finnova.SystemAdminService`, MediatR, FluentValidation, `Finnova.Repository.Context.FinnovaDbContext`, `AddFinnovaRepository`, `ExceptionHandlingMiddleware`, YARP, shared UI `api`, and `VITE_USE_MOCK_API`.
5. Apply backend tasks in order. Frontend tasks may begin after the final public contracts and gateway paths are resolved, but their service and model code depends on the backend contract tasks.
6. Use English-only India-specific labels/fixtures and normal left-to-right MUI layouts. Do not add translation scaffolding, bilingual fields, Arabic/non-English values, RTL handling, Redux, RxJS, or an AI agent.

## Tasks

- [ ] 0. Contract-resolution gate (required before implementation)

- [ ] 0.1 GATE — Resolve FINNOVA-51..55 and Finnova-Document-Verifier blockers
  - Obtain and approve the complete FINNOVA-51, FINNOVA-52, FINNOVA-53, FINNOVA-54, and FINNOVA-55 acceptance criteria and field definitions.
  - Record the Customer field list, nullability, lengths, formats, defaults, searchable fields, ordering, PII/sensitivity classification, immutable fields, business key, normalization, uniqueness scope, lifecycle states, concurrency behavior, deactivation/delete policy, and authorization matrix.
  - Record the Customer Reference endpoint cardinality, directed/symmetric rule, reference-type vocabulary, self-reference rule, duplicate key, inactive-endpoint policy, editable fields, and lifecycle/status vocabulary.
  - Record the Mind Mapping tuple cardinality, optional/required Location policy, relation/status vocabularies, confidence semantics/range, provenance and evidence limits, duplicate/supersession policy, contract version, and administrator review/edit workflow.
  - Inspect and record the existing Finnova-Document-Verifier endpoint, HTTP method, request/response schema, authentication mechanism, status vocabulary, valid/matched semantics, verifier reference, timeout, cancellation, retry/backoff, idempotency/deduplication, safe public error mapping, logging/redaction rules, and whether Aadhaar, PAN, or both are mandatory.
  - Record the final backend route prefixes, UI-composed gateway prefixes, middleware error-code family, pagination defaults/limits, authorization policies, and whether the UI uses separate pages or an approved tab/detail flow.
  - Confirm that no future Customer Information AI Agent, model call, embedding, inference, ranking, retrieval, or agent orchestration is part of this implementation.
  - **Blocked until:** all values above are approved. No implementation task below is safely copy-pasteable before this gate.
  - **Handoff record template (not executable):**
    ```text
    FINNOVA-51..55 contract version: <RESOLVE:approved story contract/version>
    Customer fields and rules: <RESOLVE:approved field table>
    Customer business key/normalization/uniqueness: <RESOLVE:approved rule>
    Customer lifecycle/delete policy: <RESOLVE:approved rule>
    Reference endpoint/type/duplicate policy: <RESOLVE:approved rule>
    Mind Mapping tuple/relation/status/version policy: <RESOLVE:approved rule>
    Verifier endpoint/request/response/auth/status policy: <RESOLVE:approved verifier contract>
    Verifier timeout/cancellation/retry/idempotency policy: <RESOLVE:approved dependency policy>
    Public route prefixes and gateway aliases: <RESOLVE:approved route table>
    Error codes and authorization policies: <RESOLVE:approved platform codes/policies>
    UI navigation and approved editable controls: <RESOLVE:approved UI flow>
    ```
  - **Requirements:** 1.1–1.4, 2.1–2.14, 3.1–3.9, 5.1–5.15, 7.1–7.15, 8.1–8.12, 10.1–10.19, 11.1–11.21, 12.1–12.20, 13.1–13.12, 14.1–14.12.

- [ ] 1. Backend domain entities and enums

- [ ] 1.1 BLOCKED — CREATE `Finnova.Models/Domain/Entities/Customer.cs`
  - Create the Customer aggregate using only the approved FINNOVA-51..55 fields from Task 0. Include the immutable stable identifier, approved lifecycle representation, UTC audit timestamps, and only the approved Verification Outcome storage shape.
  - Do not add guessed fields, raw Aadhaar/PAN values, document images, authentication tokens, raw verifier responses, copied Location names, relationship navigation collections as API contracts, or unbounded PII.
  - **Copy-paste status:** blocked; the complete class cannot be written without the approved field table.
    ```csharp
    // BLOCKED: replace this entire template only after Task 0 supplies the approved fields.
    // CREATE Finnova.Models/Domain/Entities/Customer.cs
    namespace Finnova.Models.Domain.Entities;

    public sealed class Customer
    {
        public Guid Id { get; private set; }

        // <RESOLVE:insert only FINNOVA-51..55-approved Customer properties>
        // <RESOLVE:insert approved lifecycle properties>
        // <RESOLVE:insert approved Verification Outcome value object/properties only>

        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }
    }
    ```
  - **Requirements:** 1.1–1.5, 2.1–2.14, 3.1–3.9, 10.9–10.12, 11.12–11.18, 14.8–14.12.

- [ ] 1.2 BLOCKED — CREATE `Finnova.Models/Domain/Entities/CustomerReference.cs`, `MindMapping.cs`, and audit entities
  - Create separate `CustomerReference` and `MindMapping` entities. Use stable IDs and validated foreign-key identifiers; do not merge them into one generic relationship entity.
  - Add `CustomerAuditEntry`, `CustomerReferenceAuditEntry`, and `MindMappingAuditEntry` using the approved immutable audit payload and retention-compatible foreign-key behavior.
  - Keep `CustomerReference` target cardinality and `MindMapping` Location optionality exactly as resolved in Task 0.
  - **Copy-paste status:** blocked; endpoint columns, controlled values, audit payloads, and deletion behavior are unresolved.
    ```csharp
    // BLOCKED: CREATE only after Task 0 resolves endpoint cardinality and audit fields.
    namespace Finnova.Models.Domain.Entities;

    public sealed class CustomerReference
    {
        public Guid Id { get; private set; }
        public Guid SourceCustomerId { get; private set; }
        // <RESOLVE:approved ReferenceType representation>
        // <RESOLVE:approved exactly-one-target representation>
        // <RESOLVE:approved lifecycle/provenance fields>
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }
    }

    public sealed class MindMapping
    {
        public Guid Id { get; private set; }
        public Guid CustomerId { get; private set; }
        public Guid RelatedCustomerId { get; private set; }
        // <RESOLVE:approved nullable/required LocationId>
        // <RESOLVE:approved RelationType, Status, provenance, confidence, evidence, ContractVersion>
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }
    }

    // <RESOLVE:create the three approved immutable audit entities without speculative fields>
    ```
  - **Requirements:** 5.1–5.15, 6.1–6.11, 7.1–7.15, 8.1–8.12, 9.1–9.14.

- [ ] 1.3 BLOCKED — CREATE `Finnova.Models/Domain/Enums/*` for approved controlled values
  - Add only the enum/value-object members confirmed in Task 0: Customer lifecycle, Reference Type/lifecycle, Mind Mapping relation/status, endpoint kind, verification status/failure kind, and audit actions as applicable.
  - Use the existing JSON enum convention only after confirming the platform representation. Do not add examples such as `Active`, `Approved`, `Rejected`, or `Valid` unless those exact members are approved.
  - **Copy-paste status:** blocked; controlled enum members are expressly unavailable.
    ```csharp
    // BLOCKED: no enum member may be invented here.
    // CREATE Finnova.Models/Domain/Enums/<ResolvedEnumName>.cs
    namespace Finnova.Models.Domain.Enums;

    public enum <RESOLVE:approved enum name>
    {
        // <RESOLVE:insert exact approved members and numeric/wire contract>
    }
    ```
  - **Requirements:** 1.1, 3.7–3.9, 5.9–5.15, 7.5–7.10, 9.1–9.6, 14.3–14.12.

- [ ] 2. Backend domain exceptions

- [ ] 2.1 BLOCKED — CREATE `Finnova.Models/Domain/Exceptions/*Customer*`, `*Reference*`, `*MindMapping*`, and verifier exceptions
  - Create typed exceptions for approved not-found, duplicate/conflict, immutable/lifecycle, endpoint/cardinality, verification rejection/mismatch, verifier dependency failure, and contract/configuration failure cases.
  - Each exception must carry only safe English detail and stable error-code constants resolved in Task 0; never include raw verifier payloads, documents, tokens, full document numbers, or unbounded PII.
  - **Copy-paste status:** blocked; exception taxonomy and error-code family are unresolved.
    ```csharp
    // BLOCKED: create one file per approved exception after Task 0.
    namespace Finnova.Models.Domain.Exceptions;

    public sealed class <RESOLVE:approvedExceptionName> : Exception
    {
        public const string ErrorCode = "<RESOLVE:approved-safe-error-code>";

        public <RESOLVE:approvedExceptionName>(string message)
            : base(message)
        {
        }
    }
    ```
  - **Requirements:** 2.7–2.8, 3.3, 3.8–3.9, 5.7–5.10, 7.11–7.12, 8.7–8.8, 10.1–10.19, 11.7–11.9, 14.4–14.9.

- [ ] 3. Backend request/response contracts

- [ ] 3.1 BLOCKED — CREATE `Finnova.Models/Contracts/Customers/*`
  - Create explicit Customer create/update/query/response/audit/Verification Outcome records from the approved story contract. Reuse `PaginatedResponse<T>` for lists.
  - Do not expose entity types, navigation graphs, raw document content, verifier credentials, raw verifier responses, unnecessary full document numbers, or fields outside the caller/purpose scope.
  - Customer create/update contracts must carry only the approved document input shape; handlers map it to the internal verifier request and do not echo it by default.
  - **Copy-paste status:** blocked; field names and public document-input shape are unresolved.
    ```csharp
    // BLOCKED: replace with the exact approved records after Task 0.
    namespace Finnova.Models.Contracts.Customers;

    public sealed record CreateCustomerRequest(
        // <RESOLVE:approved FINNOVA-51..55 create fields>
        // <RESOLVE:approved Aadhaar/PAN input fields, if any>
    );

    public sealed record UpdateCustomerRequest(
        // <RESOLVE:approved mutable update fields>
        // <RESOLVE:approved document-change/revalidation fields, if any>
    );

    public sealed record CustomerResponse(
        Guid Id
        // <RESOLVE:approved response fields and safe Verification Outcome>
    );

    public sealed record CustomerQuery(
        // <RESOLVE:approved search/filter/order/page fields>
    );
    ```
  - **Requirements:** 1.1–1.5, 2.1–2.14, 4.1–4.12, 10.9–10.19, 11.1–11.9, 11.12–11.21.

- [ ] 3.2 BLOCKED — CREATE `Finnova.Models/Contracts/CustomerReferences/*`, `MindMappings/*`, and `Audits/*`
  - Create separate request/response/query/audit contracts for References and Mind Mappings. Include stable endpoint IDs, approved display projections only, lifecycle/status/provenance, UTC timestamps, and Mind Mapping contract version.
  - Use nullable/discriminated endpoint fields only as approved. Do not permit incompatible Customer/Location endpoint combinations through the public contract.
  - **Copy-paste status:** blocked; endpoint and controlled-value contracts are unresolved.
    ```csharp
    // BLOCKED: create exact records after Task 0 resolves endpoint and mapping contracts.
    namespace Finnova.Models.Contracts.CustomerReferences;

    public sealed record CreateCustomerReferenceRequest(
        Guid SourceCustomerId
        // <RESOLVE:approved ReferenceType and exactly-one target shape>
    );

    public sealed record CustomerReferenceResponse(
        Guid Id,
        Guid SourceCustomerId
        // <RESOLVE:approved endpoint IDs, authorized display fields, lifecycle, provenance, timestamps>
    );
    ```
    ```csharp
    namespace Finnova.Models.Contracts.MindMappings;

    public sealed record CreateMindMappingRequest(
        Guid CustomerId,
        Guid RelatedCustomerId
        // <RESOLVE:approved LocationId, relation/status/provenance/confidence/evidence fields>
    );

    public sealed record MindMappingResponse(
        Guid Id,
        Guid CustomerId,
        Guid RelatedCustomerId
        // <RESOLVE:approved versioned safe response fields>
    );
    ```
  - **Requirements:** 5.1–5.15, 6.1–6.11, 7.1–7.15, 8.1–8.12, 9.1–9.14, 11.1–11.18.

- [ ] 4. EF configurations, DbSets, and migration command

- [ ] 4.1 BLOCKED — CREATE `Finnova.Repository/Configuration/Customer*Configuration.cs`, `CustomerReference*Configuration.cs`, `MindMapping*Configuration.cs`, and audit configurations
  - Configure table names, required/nullable columns, lengths, enum conversions, indexes, foreign keys, check constraints, concurrency behavior, and approved uniqueness keys from Task 0.
  - Ensure Location is referenced through the approved boundary and not duplicated as authoritative name/hierarchy fields. If a local FK is approved, configure only the resolved `LocationId` relationship.
  - Configure audit rows for immutable history and the approved retention/deletion behavior. Do not add speculative unique indexes.
  - **Copy-paste status:** blocked; EF schema cannot be safely generated without the approved fields and keys.
    ```csharp
    // BLOCKED: one complete IEntityTypeConfiguration<T> per resolved entity after Task 0.
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Configuration;

    public sealed class <RESOLVE:Entity>Configuration : IEntityTypeConfiguration<<RESOLVE:Entity>>
    {
        public void Configure(EntityTypeBuilder<<RESOLVE:Entity>> builder)
        {
            builder.ToTable("<RESOLVE:approved table name>");
            builder.HasKey(x => x.Id);
            // <RESOLVE:configure only approved properties, indexes, FKs, constraints>
        }
    }
    ```
  - **Requirements:** 2.2, 3.1–3.6, 5.2, 5.5, 5.10, 7.2, 7.3, 7.11–7.14, 9.9–9.12, 11.12–11.15.

- [ ] 4.2 BLOCKED — MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`
  - Use the exact existing anchor below. Insert only the resolved DbSets after the existing feature DbSets and before `OnModelCreating`; retain `ApplyConfigurationsFromAssembly` unchanged.
  - **Copy-paste status:** insertion is blocked until entity names and DbSet names are approved.
    ```csharp
    public DbSet<Asset> Assets => Set<Asset>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinnovaDbContext).Assembly);
    }
    ```
    Insert immediately before `protected override`:
    ```csharp
    public DbSet<<RESOLVE:CustomerEntity>> <RESOLVE:CustomersDbSet> => Set<<RESOLVE:CustomerEntity>>();
    public DbSet<<RESOLVE:CustomerReferenceEntity>> <RESOLVE:CustomerReferencesDbSet> => Set<<RESOLVE:CustomerReferenceEntity>>();
    public DbSet<<RESOLVE:MindMappingEntity>> <RESOLVE:MindMappingsDbSet> => Set<<RESOLVE:MindMappingEntity>>();
    // <RESOLVE:add approved audit/verification DbSets if they are separate entities>
    ```
  - **Requirements:** 9.1–9.10, 11.1, 11.12–11.15.

- [ ] 4.3 CREATE `Finnova.Repository/Migrations/<resolved-migration-name>.cs` — migration handoff through the user-run command
  - After Tasks 4.1–4.2 are complete, review the generated migration for approved columns, constraints, indexes, foreign keys, and no seed PII/secrets/non-English values.
  - Do not apply the migration in this workflow. The implementation owner runs the command only after the contract gate and code review.
  - **User-run command template:**
    ```powershell
    dotnet ef migrations add <RESOLVE:approved migration name> --project Finnova.Repository --startup-project Finnova.SystemAdminService
    ```
  - **Blocked until:** Task 0 and Tasks 4.1–4.2 are complete. Never replace the migration name or schema with guesses.
  - **Requirements:** 3.1–3.6, 5.2, 5.5, 7.2–7.3, 9.9–9.12, 11.1, 11.12–11.15.

- [ ] 5. Repositories, interfaces, and DI

- [ ] 5.1 BLOCKED — CREATE `Finnova.Repository/Interfaces/I*Customer*.cs`, `I*Reference*.cs`, `I*MindMapping*.cs`, `I*Audit*.cs`, and verifier/location boundary interfaces
  - Define feature-specific read/write interfaces for approved queries, normalized endpoint resolution, uniqueness/concurrency checks, lifecycle operations, audit reads, and transaction-aware mutations.
  - Keep Location behind the existing authoritative repository/service boundary. Do not introduce direct database access to an unrelated Location store.
  - Define a transaction/unit-of-work abstraction because the existing generic `RepositoryBase<T>` auto-saves each mutation and cannot safely compose business row plus audit row atomically.
  - **Copy-paste status:** blocked; method signatures depend on the resolved contracts and repository conventions.
    ```csharp
    // BLOCKED: complete after Task 0 and inspection of the approved Location boundary.
    namespace Finnova.Repository.Interfaces;

    public interface <RESOLVE:CustomerRepositoryInterface>
    {
        // <RESOLVE:approved reads, writes, uniqueness and concurrency methods>
    }

    public interface <RESOLVE:FeatureUnitOfWorkInterface>
    {
        // <RESOLVE:tracked mutation and atomic commit/rollback contract>
    }
    ```
  - **Requirements:** 3.1–3.6, 4.1–4.12, 5.1–5.15, 6.1–6.11, 7.1–7.12, 8.1–8.12, 9.1–9.14, 11.12–11.15.

- [ ] 5.2 BLOCKED — CREATE `Finnova.Repository/Repositories/*Customer*.cs`, `*Reference*.cs`, `*MindMapping*.cs`, and audit/unit-of-work implementations
  - Implement `AsNoTracking` read projections, approved search/filter semantics, deterministic ordering with stable-ID tie-breakers, endpoint validation, active/history filtering, and no navigation-graph serialization.
  - Implement tracked writes through the feature unit-of-work and ensure exactly one audit row is committed with each successful approved mutation.
  - Translate database uniqueness/concurrency failures into typed safe domain exceptions without exposing SQL details.
  - **Copy-paste status:** blocked; implementation depends on resolved entity fields, keys, query semantics, and transaction behavior.
    ```csharp
    // BLOCKED: complete repository implementation after Task 0.
    using Microsoft.EntityFrameworkCore;
    using Finnova.Repository.Context;

    namespace Finnova.Repository.Repositories;

    internal sealed class <RESOLVE:CustomerRepository> : <RESOLVE:CustomerRepositoryInterface>
    {
        private readonly FinnovaDbContext _db;

        public <RESOLVE:CustomerRepository>(FinnovaDbContext db) => _db = db;

        // <RESOLVE:implement only approved projection/query/write members>
        // Never return raw verifier payloads or EF navigation graphs.
    }
    ```
  - **Requirements:** 3.1–3.6, 4.1–4.12, 5.1–5.15, 6.1–6.11, 7.1–7.15, 8.1–8.12, 9.1–9.14, 10.9–10.12.

- [ ] 5.3 BLOCKED — MODIFY `Finnova.Repository/DependencyInjection.cs`
  - Use the exact existing registration anchor below. Insert the resolved repository, unit-of-work, Location boundary, and verifier adapter registrations without changing existing registrations.
  - **Copy-paste status:** blocked until interface/class names and verifier configuration are resolved.
    ```csharp
        services.AddScoped<IAssetRepository, AssetRepository>();
        return services;
    }
    ```
    Insert immediately before `return services;`:
    ```csharp
        services.AddScoped<<RESOLVE:CustomerRepositoryInterface>, <RESOLVE:CustomerRepository>>();
        services.AddScoped<<RESOLVE:CustomerReferenceRepositoryInterface>, <RESOLVE:CustomerReferenceRepository>>();
        services.AddScoped<<RESOLVE:MindMappingRepositoryInterface>, <RESOLVE:MindMappingRepository>>();
        services.AddScoped<<RESOLVE:FeatureUnitOfWorkInterface>, <RESOLVE:FeatureUnitOfWork>>();
        services.AddScoped<<RESOLVE:DocumentVerifierClientInterface>, <RESOLVE:DocumentVerifierClient>>();
    ```
  - **Requirements:** 2.12–2.14, 5.1–5.6, 7.1–7.10, 9.1–9.10, 11.16–11.21, 14.1–14.7.

- [ ] 6. Pure/domain helpers and verifier safety

- [ ] 6.1 CREATE `Finnova.Service/<FeatureNamespace>/EndpointNormalization.cs`
  - After Task 0 resolves endpoint cardinality and normalization, implement a pure helper that preserves authoritative IDs, rejects incompatible combinations, rejects/permits self-reference exactly as approved, and never creates copied names as endpoint data.
  - Keep this helper free of EF, HTTP, filesystem, clock, and logging dependencies.
  - **Copy-paste status:** blocked only for the unresolved endpoint rules; the pure-helper shape is safe to use after resolution.
    ```csharp
    // BLOCKED TEMPLATE — replace tokens with approved names/rules before copying.
    namespace Finnova.Service.<RESOLVE:FeatureNamespace>;

    public static class EndpointNormalization
    {
        public static <RESOLVE:NormalizedEndpointType> Normalize(
            <RESOLVE:raw endpoint input>,
            <RESOLVE:normalization options>)
        {
            // <RESOLVE:validate approved cardinality, IDs, self-reference and active policy>
            // Return IDs only; never copy display names into authoritative endpoint fields.
            throw new NotImplementedException();
        }
    }
    ```
  - **Requirements:** 5.7–5.10, 6.3–6.5, 7.1–7.4, 11.2–11.4, 11.14–11.15.

- [ ] 6.2 CREATE `Finnova.Service/<FeatureNamespace>/ProjectionRedaction.cs`
  - Implement a pure allow-list projection layer for Customer, Reference, Mind Mapping, and Verification Outcome responses. Reduced authorization/purpose scopes must never add fields.
  - Redact raw documents, tokens, raw verifier payloads, unnecessary full document numbers, and unbounded PII from responses, errors, logs, provenance, and audit snapshots.
  - **Copy-paste status:** blocked until the approved field sensitivity/purpose matrix exists.
    ```csharp
    // BLOCKED TEMPLATE — this must be an allow-list, not a deny-list.
    namespace Finnova.Service.<RESOLVE:FeatureNamespace>;

    public static class ProjectionRedaction
    {
        public static <RESOLVE:ResponseType> Project(
            <RESOLVE:SourceType> source,
            <RESOLVE:AuthorizedPurposeScope> scope)
        {
            // <RESOLVE:return only fields explicitly allowed for scope>
            // <RESOLVE:omit/mask sensitive endpoint fields as approved>
            throw new NotImplementedException();
        }
    }
    ```
  - **Requirements:** 8.5–8.8, 10.9–10.19, 11.9, 11.12–11.18, 14.8–14.9.

- [ ] 6.3 CREATE `Finnova.Service/IdentityVerification/SafeVerifierResultMapper.cs` and timeout/error classifier
  - Implement `IDocumentVerifierClient` boundary mapping from the resolved external response into a safe internal result. Map only approved status, verifier reference, masked/non-sensitive metadata, timestamps, and approved failure categories.
  - Implement the resolved timeout/cancellation/retry/idempotency classification. Permanent invalid/rejected/mismatch results must not retry; eligible dependency failures return a safe retryable result. Never pass through external response bodies.
  - **Copy-paste status:** blocked until the verifier wire contract and dependency policy are supplied.
    ```csharp
    // BLOCKED TEMPLATE — external status names and retry policy must be resolved first.
    namespace Finnova.Service.IdentityVerification;

    public interface IDocumentVerifierClient
    {
        Task<DocumentVerificationResult> VerifyAsync(
            DocumentVerificationRequest request,
            CancellationToken cancellationToken);
    }

    public sealed record DocumentVerificationResult(
        <RESOLVE:approved safe outcome fields only>
    );

    public static class SafeVerifierResultMapper
    {
        public static DocumentVerificationResult Map(<RESOLVE:external result>)
        {
            // <RESOLVE:map only approved safe outcome fields>
            // <RESOLVE:unknown external statuses fail closed>
            throw new NotImplementedException();
        }

        public static <RESOLVE:SafeFailure> ClassifyFailure(Exception exception)
        {
            // <RESOLVE:apply approved timeout/cancellation/retry/idempotency policy>
            throw new NotImplementedException();
        }
    }
    ```
  - **Requirements:** 2.12–2.14, 10.16–10.19, 11.16–11.21, 14.1–14.12.

- [ ] 6.4 CREATE `Finnova.Service/<FeatureNamespace>/FeatureTransactionCoordinator.cs`
  - Implement the transaction boundary that writes one business mutation and exactly one immutable audit entry atomically. Roll back on verifier failure, validation/conflict, audit failure, concurrency failure, or commit failure; return no successful response for an uncommitted change.
  - Preserve the existing generic repository behavior for unrelated features; do not change it globally just to support this feature.
  - **Copy-paste status:** blocked until the approved unit-of-work interface and audit entities exist.
    ```csharp
    // BLOCKED TEMPLATE — use the resolved repository/context abstractions.
    namespace Finnova.Service.<RESOLVE:FeatureNamespace>;

    public sealed class <RESOLVE:FeatureTransactionCoordinator>
    {
        public async Task<TResponse> ExecuteAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            // <RESOLVE:begin transaction>
            // <RESOLVE:execute tracked business + exactly one audit write>
            // <RESOLVE:save once, commit once; rollback on every failure>
            throw new NotImplementedException();
        }
    }
    ```
  - **Requirements:** 2.2–2.6, 3.3–3.6, 5.2, 5.5, 7.2, 7.11–7.13, 9.1–9.12, 14.3–14.7.

- [ ] 7. Backend CQRS commands, queries, validators, and mappers

- [ ] 7.1 BLOCKED — CREATE `Finnova.Service/Customers/*`
  - Implement Customer create/update/lifecycle commands, validators, handlers, and explicit mappers using the approved story contract.
  - Validate request shape before persistence. Resolve approved identity-document applicability; if verification is required or supplied, call the verifier adapter before beginning the successful write transaction. On invalid/rejected/mismatched/ambiguous/timeout/unavailable verifier results, preserve the existing Customer or create no new Customer and return the safe approved error.
  - Apply approved uniqueness and concurrency rules; preserve existing records on conflict; produce exactly one audit entry on each successful approved mutation.
  - **Copy-paste status:** blocked; no Customer handler can be safely authored without fields, lifecycle, verifier policy, and error codes.
    ```csharp
    // BLOCKED TEMPLATE — CREATE command/validator/handler files after Task 0.
    namespace Finnova.Service.Customers;

    public sealed record <RESOLVE:CreateCustomerCommand>(
        <RESOLVE:approved command fields>) : IRequest<<RESOLVE:CustomerResponse>>;

    public sealed class <RESOLVE:CreateCustomerCommandValidator>
        : AbstractValidator<<RESOLVE:CreateCustomerCommand>>
    {
        public <RESOLVE:CreateCustomerCommandValidator>()
        {
            // <RESOLVE:approved required/format/length/cross-field rules>
        }
    }

    public sealed class <RESOLVE:CreateCustomerHandler>
        : IRequestHandler<<RESOLVE:CreateCustomerCommand>, <RESOLVE:CustomerResponse>>
    {
        // <RESOLVE:inject repositories, verifier, transaction coordinator and clock>
        // <RESOLVE:verify applicable Aadhaar/PAN before persistence>
        // <RESOLVE:write Customer + exactly one audit atomically>
    }
    ```
  - **Requirements:** 1.1–1.5, 2.1–2.14, 3.1–3.9, 9.1–9.10, 14.1–14.12.

- [ ] 7.2 BLOCKED — CREATE `Finnova.Service/CustomerReferences/*`
  - Implement create/update/lifecycle commands, validators, handlers, and mappers for Customer-to-Customer and Customer-to-Location References as separate flows.
  - Validate source/target existence through the approved boundaries, exact target cardinality, reference type, direction/self-reference/duplicate rules, active-endpoint policy, and immutable endpoint IDs. Write one audit entry atomically for successful changes.
  - **Copy-paste status:** blocked; endpoint and controlled-value rules are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — CREATE after Task 0 resolves Reference contract.
    namespace Finnova.Service.CustomerReferences;

    public sealed record <RESOLVE:CreateCustomerReferenceCommand>(
        <RESOLVE:approved reference fields>) : IRequest<<RESOLVE:CustomerReferenceResponse>>;

    public sealed class <RESOLVE:CreateCustomerReferenceValidator>
        : AbstractValidator<<RESOLVE:CreateCustomerReferenceCommand>>
    {
        public <RESOLVE:CreateCustomerReferenceValidator>()
        {
            // <RESOLVE:approved endpoint cardinality/type/direction/active rules>
        }
    }
    ```
  - **Requirements:** 5.1–5.15, 6.1–6.11, 9.3–9.4, 10.13, 11.2–11.4, 11.14–11.15.

- [ ] 7.3 BLOCKED — CREATE `Finnova.Service/MindMappings/*`
  - Implement create/update/status/supersession commands only if approved, validators, handlers, version-aware mappers, and safe read projections.
  - Validate normalized Customer/Related Customer/Location identifiers, relation/status values, provenance/evidence bounds, confidence semantics, duplicate/supersession rules, and contract version. Do not infer relationships, calculate scores, invoke models, create embeddings, or expose AI controls.
  - **Copy-paste status:** blocked; mapping semantics and edit workflow are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — CREATE after Task 0 resolves Mind Mapping contract.
    namespace Finnova.Service.MindMappings;

    public sealed record <RESOLVE:CreateMindMappingCommand>(
        <RESOLVE:approved mapping fields>) : IRequest<<RESOLVE:MindMappingResponse>>;

    public sealed class <RESOLVE:CreateMindMappingValidator>
        : AbstractValidator<<RESOLVE:CreateMindMappingCommand>>
    {
        public <RESOLVE:CreateMindMappingValidator>()
        {
            // <RESOLVE:approved endpoint/relation/status/confidence/evidence rules>
        }
    }
    ```
  - **Requirements:** 7.1–7.15, 8.1–8.12, 9.5–9.6, 10.13, 11.2–11.5, 13.1–13.12.

- [ ] 7.4 BLOCKED — CREATE `Finnova.Service/<FeatureNamespace>/Queries/*`
  - Implement paginated Customer, Reference, Mind Mapping, and approved audit queries with deterministic ordering, stable-ID tie-breakers, active/history filtering, and approved search semantics.
  - Resolve endpoint display fields only in authorized projections. Preserve endpoint identifiers and authorized lifecycle state for inactive endpoints. Do not reveal protected values through counts, ordering, errors, or free-text metadata.
  - Do not create an export/bulk endpoint unless Task 0 explicitly approves it and supplies purpose, field-minimization, row-limit, and audit rules.
  - **Copy-paste status:** blocked; query fields, ordering, pagination limits, and authorization scopes are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — CREATE query records/handlers after Task 0.
    namespace Finnova.Service.<RESOLVE:FeatureNamespace>.Queries;

    public sealed record <RESOLVE:ListQuery>(
        <RESOLVE:approved filters>,
        int Page = <RESOLVE:approved default page>,
        int PageSize = <RESOLVE:approved default page size>)
        : IRequest<PaginatedResponse<<RESOLVE:ResponseType>>>;
    ```
  - **Requirements:** 4.1–4.12, 6.1–6.11, 8.1–8.12, 9.13–9.14, 10.3–10.15, 11.1–11.15.

- [ ] 7.5 BLOCKED — CREATE `Finnova.Service/Mappers/*`
  - Implement explicit entity/projection-to-contract mappers for Customer, Reference, Mind Mapping, Verification Outcome, and audit records. Use allow-list authorization/purpose contexts and version-aware Mind Mapping mapping.
  - Never serialize EF navigation graphs or raw external verifier objects. Ensure every mapper preserves stable IDs and UTC timestamps and omits unapproved PII.
  - **Copy-paste status:** blocked; public field and version contracts are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — CREATE one mapper per approved contract family.
    namespace Finnova.Service.Mappers;

    public static class <RESOLVE:Feature>Mappings
    {
        public static <RESOLVE:ResponseType> ToResponse(
            <RESOLVE:ProjectionType> source,
            <RESOLVE:AuthorizedPurposeScope> scope)
        {
            // <RESOLVE:allow-list mapping only>
            throw new NotImplementedException();
        }
    }
    ```
  - **Requirements:** 2.3, 2.6, 4.4, 6.3–6.5, 7.3–7.9, 8.1–8.10, 10.9–10.12, 11.1–11.13.

- [ ] 8. Controllers, middleware, and gateway aliases

- [ ] 8.1 BLOCKED — CREATE `Finnova.SystemAdminService/Controllers/CustomerController.cs`, `CustomerReferenceController.cs`, and `MindMappingController.cs`
  - Add authenticated SystemAdmin endpoints using the approved final route tokens and policies. Controllers must translate HTTP to MediatR requests, pass cancellation tokens, and return only approved contracts/status codes.
  - Keep management and read policies as separate seams even if both initially resolve to `SystemAdmin`. Resolve acting identity from the established claims convention without putting tokens or document data into commands/logs.
  - Do not add a public verifier endpoint, database endpoint, export endpoint, or AI endpoint unless explicitly approved in Task 0.
  - **Copy-paste status:** blocked; route tokens, policies, operation set, and response statuses are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — CREATE one controller per approved capability.
    [ApiController]
    [Route("<RESOLVE:approved route prefix>")]
    [Authorize(Policy = "<RESOLVE:approved policy>")]
    public sealed class <RESOLVE:ControllerName>Controller : ControllerBase
    {
        private readonly IMediator _mediator;

        public <RESOLVE:ControllerName>Controller(IMediator mediator) => _mediator = mediator;

        // <RESOLVE:add only approved actions; dispatch all through MediatR>
    }
    ```
  - **Requirements:** 4.10–4.12, 5.1–5.6, 6.1–6.11, 7.1–7.15, 8.1–8.12, 10.1–10.8, 11.1–11.11.

- [ ] 8.2 BLOCKED — MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`
  - Preserve all existing branches and add typed branches for the resolved Customer, Reference, Mind Mapping, and verifier exceptions. Select validation/fallback code families by the approved final request path without message sniffing.
  - Return the existing RFC 7807 `ProblemDetails` shape with safe English detail and stable `Extensions["code"]`. Never include raw verifier payloads, document data, tokens, SQL details, or unnecessary PII.
  - **Existing anchor:**
    ```csharp
                AssetValidationException => (400, "ERR-AST-400", ex.Message),
                // ---- shared: validation failures and unhandled fallback (path-scoped code) ----
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationAssetCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackAssetCode, "Unexpected error.")
    ```
    Insert the resolved typed cases before the shared branch and add path-family variables using the same `StartsWithSegments` pattern only after Task 0 supplies paths/codes:
    ```csharp
                <RESOLVE:CustomerNotFoundException> => (StatusCodes.Status404NotFound, "<RESOLVE:error code>", ex.Message),
                <RESOLVE:ReferenceConflictException> => (StatusCodes.Status409Conflict, "<RESOLVE:error code>", ex.Message),
                <RESOLVE:MindMappingValidationException> => (StatusCodes.Status400BadRequest, "<RESOLVE:error code>", ex.Message),
                <RESOLVE:VerifierDependencyException> => (StatusCodes.Status503ServiceUnavailable, "<RESOLVE:error code>", "<RESOLVE:safe retry message>"),
    ```
  - **Requirements:** 2.7–2.8, 3.3, 5.7–5.10, 7.11–7.12, 8.7–8.8, 10.1–10.5, 10.17–10.19, 11.7–11.9, 14.4–14.9.

- [ ] 8.3 BLOCKED — MODIFY `Finnova.ApiGateway/appsettings.json`
  - Add the approved UI alias route(s) to `systemadmin-cluster` using the established remove-prefix/add-prefix transform. Keep the direct `/api/systemadmin/**` route and host separation intact.
  - Use the exact existing nationality alias shape as the pattern; do not invent feature route tokens.
  - **Existing anchor:**
    ```json
      "ua-nationality-alias-root": {
        "ClusterId": "systemadmin-cluster",
        "Match": {
          "Path": "/api/ua/api/nationality"
        },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
    ```
    Insert the resolved Customer/Reference/Mind Mapping route entries adjacent to the other SystemAdmin aliases:
    ```json
      "<RESOLVE:feature-alias-route-name>": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/ua/api/<RESOLVE:approved-prefix>/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "<RESOLVE:feature-alias-root-name>": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/ua/api/<RESOLVE:approved-prefix>" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
    ```
  - **Requirements:** 10.1–10.5, 11.10–11.11, 12.13–12.14.

- [ ] 9. Backend tests

- [ ] 9.1 BLOCKED — CREATE `Finnova.Tests/Unit/<Feature>/*` and `Finnova.Tests/Infrastructure/<Feature>/*`
  - Add xUnit example/edge tests for validators, handlers, no-write/no-audit rejection, duplicate/not-found/lifecycle/endpoint/cardinality cases, transaction rollback, safe error mapping, verifier success/failure/timeout/cancellation/no-document/single-document flows, contract-version reads, and sensitive-field absence.
  - Add one FsCheck.Xunit property test per design property, with a minimum of 100 iterations:
    - Property 1: relationship endpoint normalization preserves the authoritative tuple (Requirements 5.7).
    - Property 2: safe verifier mapping never leaks sensitive external data (Requirements 2.13; also 10, 11, and 14 redaction clauses).
    - Property 3: audit ordering is deterministic and newest-first (Requirements 9.13–9.14).
    - Property 4: approved projection minimization is monotonic (Requirements 8.5 and 10.9–10.11).
  - Generators must use pure in-memory values only and must include adversarial document, credential, header, raw-response, and PII strings for Property 2. Do not call SQL Server, the verifier, gateway, filesystem, or network from PBTs.
  - **Copy-paste status:** blocked; generators and assertions require resolved types and fields.
    ```csharp
    // BLOCKED TEMPLATE — one property test per design property, minimum 100 runs.
    [Property(MaxTest = 100)]
    public Property Property_<RESOLVE:number>_<RESOLVE:name>(<RESOLVE:generated input>)
    {
        // <RESOLVE:assert the exact design property using pure helper output>
        throw new NotImplementedException();
    }
    ```
  - **Requirements:** 5.7, 8.5, 9.13–9.14, 10.9–10.12, 14.8–14.9.

- [ ] 9.2 BLOCKED — CREATE `Finnova.Tests/Integration/<Feature>/*`
  - Add `WebApplicationFactory<Program>` tests for missing/invalid JWT → 401, valid non-admin → 403, invalid token before role evaluation, approved admin route wiring, ProblemDetails/error codes, field-level errors, Customer verifier rollback, Reference/Mind Mapping flows, audit history, inactive endpoints, and Location boundary behavior.
  - Replace external verifier calls with a deterministic test adapter or local HTTP stub; never contact production verifier. Prove the gateway alias configuration where the test harness supports YARP, otherwise add a focused route-configuration test.
  - **Copy-paste status:** blocked; final routes, policies, contracts, and verifier fixture are unresolved.
    ```csharp
    // BLOCKED TEMPLATE — complete after Task 0 and host wiring.
    public sealed class <RESOLVE:Feature>AuthorizationTests
        : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task Missing_token_returns_unauthorized()
        {
            // <RESOLVE:call approved endpoint and assert 401/no protected data>
        }
    }
    ```
  - **Requirements:** 6.1–6.11, 8.7–8.8, 10.1–10.8, 11.7–11.11, 14.3–14.9.

- [ ] 10. Frontend models, service interfaces, real/mock/toggle services, and barrels

- [ ] 10.1 BLOCKED — CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models/customer*.model.ts`, `src/services/interfaces/customer*.interface.ts`
  - Create TypeScript models for the approved Customer, Reference, Mind Mapping, audit, query, form, and safe Verification Outcome contracts. Keep Customer References and Mind Mappings distinct.
  - Include only approved fields and endpoint/display projections. Do not create fields for raw documents, tokens, raw verifier responses, unnecessary full document numbers, AI inference, embeddings, ranking, retrieval, or agent controls.
  - **Copy-paste status:** blocked; API field names and controlled values are unresolved.
    ```typescript
    // BLOCKED: replace with the resolved API contracts after Task 0.
    export interface Customer {
      id: string;
      // <RESOLVE:approved Customer response fields only>
    }

    export interface CustomerReference {
      id: string;
      sourceCustomerId: string;
      // <RESOLVE:approved endpoint/type/lifecycle/provenance fields>
    }

    export interface MindMapping {
      id: string;
      customerId: string;
      relatedCustomerId: string;
      // <RESOLVE:approved Location/relation/status/version fields>
    }
    ```
  - **Requirements:** 4.4, 5.3–5.6, 6.3–6.11, 7.3–7.9, 8.1–8.10, 11.1–11.18, 12.1–12.8.

- [ ] 10.2 BLOCKED — CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src/services/real/*`, `src/services/mock/*`, feature toggle modules, and MODIFY service barrels
  - Implement real services with the shared `src/services/api.ts` only. Compose paths from the approved gateway prefix; do not create an axios client or hard-code a host port.
  - Implement mock services with deterministic English-only India-specific non-real-Pii fixtures and the same success/error shapes as real services. Select implementations through `import.meta.env.VITE_USE_MOCK_API`.
  - Update `src/services/interfaces/index.ts` and `src/services/index.ts` with only the resolved exports. Preserve existing exports.
  - **Copy-paste status:** blocked; endpoint paths, DTO fields, and error codes are unresolved.
    ```typescript
    // BLOCKED TEMPLATE — mirror existing location/lookup service files after Task 0.
    import api from '../api';
    import type { <RESOLVE:Model>, <RESOLVE:ServiceInterface> } from '../interfaces';

    const basePath = '<RESOLVE:approved gateway-relative path>';

    export const <RESOLVE:realService>: <RESOLVE:ServiceInterface> = {
      async <RESOLVE:method>(<RESOLVE:args>) {
        const response = await api.<RESOLVE:httpMethod>(`${basePath}<RESOLVE:suffix>`, <RESOLVE:payload>);
        return <RESOLVE:mapResponse>(response.data);
      }
    };
    ```
    ```typescript
    // BLOCKED TEMPLATE — deterministic mock with no real PII/non-English values.
    import type { <RESOLVE:ServiceInterface> } from '../interfaces';

    export const <RESOLVE:mockService>: <RESOLVE:ServiceInterface> = {
      // <RESOLVE:implement parity with real-service success/error contracts>
    };
    ```
  - **Requirements:** 12.9–12.20, 11.10, 11.13, 14.18–14.20.

- [ ] 11. Frontend pages, components, routes, and navigation

- [ ] 11.1 BLOCKED — CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src/pages/<ResolvedCustomerPages>.tsx` and feature components
  - Mirror the existing MUI master-page patterns such as `LocationMaster.tsx` and `LookupMaster.tsx`, using hooks and service interfaces only.
  - Provide the approved Customer fields and operations, controlled Customer/Location selection for References, and only approved relation/status/provenance/confidence controls for Mind Mappings.
  - Implement explicit loading, empty, success, validation-error, unauthorized/forbidden, timeout, retry, and safe unsaved-input-preservation states. Refresh from service responses after successful saves rather than relying on optimistic state.
  - Verification failures must show only the approved safe English status/message, preserve safe input, and never show raw verifier details or mark the Customer verified before successful validation.
  - Do not add AI inference, model, embedding, relationship suggestion, ranking, Customer Information retrieval, or agent controls.
  - **Copy-paste status:** blocked; final fields, controls, routes, and page flow are unresolved.
    ```tsx
    // BLOCKED TEMPLATE — complete after Task 0 using existing MUI master-page structure.
    import { useEffect, useState } from 'react';
    import { Alert, CircularProgress, Box } from '@mui/material';

    export default function <RESOLVE:CustomerPageName>() {
      const [loading, setLoading] = useState(true);
      const [error, setError] = useState<string | null>(null);

      useEffect(() => {
        // <RESOLVE:load approved feature data through the selected service>
      }, []);

      if (loading) return <CircularProgress aria-label="Loading" />;
      if (error) return <Alert severity="error">{error}</Alert>;

      return <Box>{/* <RESOLVE:approved MUI grid/dialog/form only> */}</Box>;
    }
    ```
  - **Requirements:** 12.1–12.20, 10.9–10.19, 13.1–13.12.

- [ ] 11.2 BLOCKED — MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src/App.tsx` and `src/components/Layout.tsx` (or the exact existing route/nav files)
  - Add approved protected routes and navigation entries only after Task 0 resolves route names, permission/program names, and page flow. Preserve existing providers, authentication, authorization, and navigation.
  - Use the existing route and navigation anchors discovered in the UI repository; do not guess file structure from the backend repository.
  - **Copy-paste status:** blocked; route and navigation tokens are unresolved and the UI repository is separate from this workspace.
    ```tsx
    // MODIFY the exact existing route list at the resolved insertion location.
    <Route path="<RESOLVE:approved-ui-path>" element={<<RESOLVE:PageComponent> />} />
    ```
    ```tsx
    // MODIFY the exact existing navigation item list at the resolved insertion location.
    { path: '<RESOLVE:approved-ui-path>', label: '<RESOLVE:English label>' },
    ```
  - **Requirements:** 10.3–10.8, 12.1, 12.4–12.8, 12.13–12.17.

- [ ] 12. Frontend Vitest/RTL tests and user-run validation

- [ ] 12.1 BLOCKED — CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src/**/<feature>.test.ts(x)`
  - Add Vitest + React Testing Library tests for service path/payload/response mapping, mock/real parity, `VITE_USE_MOCK_API` selection, approved fields/validation, controlled selectors, loading/empty/success/error/401/403/conflict/timeout/retry states, safe verification failure/retry, audit ordering, no AI controls, English-only text, and no RTL behavior.
  - **Copy-paste status:** blocked; final component names, contracts, paths, and error codes are unresolved.
    ```typescript
    // BLOCKED TEMPLATE — complete after Task 0 and page/service implementation.
    import { render, screen } from '@testing-library/react';
    import { describe, expect, it } from 'vitest';
    import <RESOLVE:PageComponent> from '<RESOLVE:page import>';

    describe('<RESOLVE:feature> page', () => {
      it('renders the approved loading and success states', async () => {
        render(<<RESOLVE:PageComponent> />);
        expect(screen.getByText('<RESOLVE:approved English label>')).toBeInTheDocument();
      });
    });
    ```
  - **Requirements:** 12.2–12.20.

- [ ] 12.2 VALIDATION HANDOFF — user-run commands only; do not execute in this workflow
  - Backend, after all code tasks and migration review:
    ```powershell
    dotnet build Finnova.Backend.slnx
    dotnet test Finnova.Backend.slnx --no-restore
    ```
  - Frontend, from `E:\Finnova\Finnova-UI\Finnova-UI`, after UI tasks:
    ```powershell
    npm run build
    npm test -- --run
    ```
  - Migration review/application is a deployment-owner action after schema review; do not apply it automatically. Confirm rollback strategy, backup/restore, retention/legal-hold implications, and verifier secret configuration before deployment.
  - **Requirements:** 9.1–9.14, 10.1–10.19, 11.1–11.21, 12.1–12.20, 14.1–14.12.

## Rollback, transaction, security, privacy, and deployment notes

- **Transaction/rollback:** Customer, Reference, and Mind Mapping business mutations and their required single audit entries must commit atomically. Verifier rejection, mismatch, timeout, cancellation, unavailable dependency, validation, conflict, concurrency, audit, or commit failure must leave no new Customer and must preserve the existing Customer on update. No ordinary change audit entry is written for rejected operations.
- **Privacy:** Use allow-list projections. Never persist or return raw Aadhaar/PAN images/documents, authentication tokens, raw verifier response bodies, unnecessary full document numbers, or unbounded PII. Redact those values from logs, telemetry, errors, provenance, and audit snapshots. Do not leak protected existence through counts, ordering, conflict text, or free-text evidence.
- **Authorization:** Unauthenticated callers receive 401 before protected evaluation. Authenticated callers without the approved policy receive 403 and no protected data. Management and read policies remain separate seams. Verify access for each referenced Customer and Location before returning display fields.
- **Location boundary:** Store only approved Location identifiers. Resolve names/status through the authoritative Location repository/service boundary. Do not create, update, duplicate, infer, or directly database-join an unrelated Location master.
- **AI boundary:** Do not implement Customer Information retrieval, model calls, embeddings, inference, ranking, explanations, training, vector search, RAG, or agent orchestration. Mind Mappings are persisted inputs only.
- **Migration/deployment:** Generate migrations only after the contract gate. Review indexes, FKs, check constraints, audit retention, legal hold, and rollback/data-loss implications. Supply verifier endpoint/auth/timeout/retry/idempotency settings through approved configuration/secret management; never source-control credentials or log them.
- **Localization:** All UI labels, statuses, errors, mock fixtures, and tests are English-only and left-to-right. No bilingual or RTL implementation is permitted.

## Notes

- Task 0 is a hard prerequisite. A coding agent must stop rather than inventing a missing field, enum member, route token, error code, verifier status, timeout, retry, or idempotency value.
- Tasks marked `BLOCKED` are intentionally not falsely presented as executable. Their fenced blocks are contract-resolution templates or exact existing anchors with insertion locations. After Task 0, replace every `<RESOLVE:...>` token with approved values and expand each template into a complete file/replacement before copying it.
- Testing tasks include the four design correctness properties. FsCheck runs must use at least 100 iterations. Example/edge tests and `WebApplicationFactory<Program>` integration tests remain complementary.
- No task in this document authorizes changes outside the two repositories named at the top. During this spec phase, only this `tasks.md` file is created.
- Checkpoints are represented by the validation handoff and the dependency graph; builds/tests are user-run only.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["0.1"] },
    { "id": 1, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "3.1", "3.2"] },
    { "id": 3, "tasks": ["4.1", "4.2"] },
    { "id": 4, "tasks": ["4.3", "5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3"] },
    { "id": 6, "tasks": ["6.1", "6.2", "6.3", "6.4"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5"] },
    { "id": 8, "tasks": ["8.1", "8.2", "8.3"] },
    { "id": 9, "tasks": ["9.1", "9.2"] },
    { "id": 10, "tasks": ["10.1", "10.2"] },
    { "id": 11, "tasks": ["11.1", "11.2"] },
    { "id": 12, "tasks": ["12.1", "12.2"] }
  ]
}
```
