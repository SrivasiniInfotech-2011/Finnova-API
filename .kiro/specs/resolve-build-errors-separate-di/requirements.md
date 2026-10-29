# Requirements Document

## Introduction

This specification addresses the backend request: **“resolve the build errors and implement the separate DI registrations for Repo and Service Layer.”** The work is a composition-root and dependency-registration correction for the existing Finnova .NET solution. It does not add a business capability, change an HTTP contract, change the database schema, or add UI behavior.

Static inspection covers `Finnova.Backend.slnx`, the three API-host `Program.cs` files, the existing `Finnova.Repository.DependencyInjection` method, the service-layer registration calls, project references, and the tracked diagnostic evidence under `.agents/tasks`. No build, test, migration, or frontend command is part of this authoring pass. The tracked evidence records a previous successful build with one pre-existing nullable warning, `CS8604`, at `Finnova.SystemAdminService/Program.cs`; no tracked compiler artifact inspected here records a compiler error. Therefore, the confirmed problem is duplicated and incomplete composition-root wiring plus the confirmed nullable warning. Any additional compiler error that appears when the user later builds must be treated as a new diagnostic and resolved against the same layer boundary rather than assumed here.

The solution remains split into the existing backend projects: `Finnova.Repository` owns EF Core and repository registrations, `Finnova.Service` owns MediatR handlers, validators, service implementations, and service ports, and each API host composes both registrations. The API gateway and the React frontend remain unchanged because this request does not alter routes, JSON contracts, authentication semantics, or user-facing behavior.

## Summary

- Add one service-layer DI extension in `Finnova.Service` and reuse the existing repository-layer DI extension in `Finnova.Repository`.
- Move the repeated MediatR, FluentValidation, pipeline, token, password-policy, asset-generator, customer-evaluator, GL-port, and document-port registrations into the service extension.
- Make `Finnova.UAService`, `Finnova.AccountsService`, and `Finnova.SystemAdminService` call the repository extension and service extension once each, while retaining host-owned authentication, authorization, middleware, controllers, health checks, and OpenAPI setup.
- Replace nullable connection-string flow with an explicit startup guard, resolving the confirmed `CS8604` warning without suppressing it.
- Preserve all existing backend API behavior and record frontend impact as **NONE**.

## Glossary

- **DI**: Dependency injection, the .NET service-container mechanism used to construct handlers and services.
- **Repository layer**: `Finnova.Repository`; owns `FinnovaDbContext`, EF Core configuration, repository interfaces, repository implementations, and repository-local adapters.
- **Service layer**: `Finnova.Service`; owns MediatR requests and handlers, validators, pipeline behaviors, service implementations, and service ports/adapters.
- **Host**: One of `Finnova.UAService`, `Finnova.AccountsService`, or `Finnova.SystemAdminService`, each of which provides a web application composition root in `Program.cs`.
- **Composition root**: The host startup code that combines infrastructure and application registrations and configures host concerns such as JWT authentication and authorization.
- **Repository DI extension**: The existing `Finnova.Repository.DependencyInjection.AddFinnovaRepository(IServiceCollection, string)` method.
- **Service DI extension**: The new `Finnova.Service.DependencyInjection.AddFinnovaServices(IServiceCollection, IConfiguration)` method.
- **Service assembly**: The `Finnova.Service` assembly containing the MediatR handlers and FluentValidation validators. Existing marker types show that all hosts scan this assembly.
- **CS8604**: The nullable-reference warning for passing a possibly null connection string to `AddFinnovaRepository`; tracked build evidence identifies it as pre-existing in `Finnova.SystemAdminService/Program.cs`.
- **Frontend**: `E:\Finnova\Finnova-UI\Finnova-UI`, the React 18 + TypeScript + MUI + Vite application. This feature has no frontend change.

## Dependencies and Assumptions

### Dependencies

- `Finnova.Backend.slnx` remains the solution entry point.
- The three API hosts already reference both `Finnova.Service` and `Finnova.Repository`; no new host project reference is expected.
- `Finnova.Service` already references `Finnova.Models` and `Finnova.Repository`, and it already owns the `MediatR`, `FluentValidation`, and options packages needed by its implementations. The dependency-injection extension package is added to `Finnova.Service` so `AddValidatorsFromAssembly` is compiled where it is used.
- The existing `Finnova.Service.Storage.DependencyInjection.AddBlobStorage` method remains the provider-selection implementation and is called from the service-layer extension.
- Host JWT authentication, authorization policies, exception middleware, controller discovery, gateway routes, and configuration files remain host concerns.

### Assumptions for confirmation

- **A1 — Scope of “build errors”:** Static inspection cannot prove a current compiler error without running the prohibited build. The only tracked compiler diagnostic found is the pre-existing `CS8604` warning; the implementation therefore fixes that warning and the observable DI duplication/incompleteness, while later user-run diagnostics remain authoritative for any unrelated errors.
- **A2 — Shared service registration:** Every API host may call the same `AddFinnovaServices` method. Registering service implementations that a particular host does not currently invoke is acceptable because registrations are lazy and keeps all hosts on one service-layer composition contract.
- **A3 — MediatR scan scope:** All hosts should scan the complete `Finnova.Service` assembly once. The existing marker types from Login, CreateAccount, Lookup, and CreateCustomer all resolve to the same service assembly; the second MediatR registration in `Finnova.SystemAdminService` is redundant and is removed.
- **A4 — Storage ownership:** Blob-storage registration is treated as service-layer infrastructure and is moved behind `AddFinnovaServices`; the existing provider selection and singleton lifetimes do not change.
- **A5 — Configuration failure:** A missing `FinnovaConnection` is a fatal startup configuration error. A null value is rejected by the host with `InvalidOperationException`; an empty or whitespace value is rejected by the repository extension with `ArgumentException`. No request should reach a host with an invalid database connection configuration.
- **A6 — No contract change:** Separate registrations do not change controllers, routes, DTOs, serialization, authorization policies, or gateway aliases. If the user intended an API or UI behavior change in addition to DI cleanup, that is a separate ticket.
- **A7 — India and localization context:** Finnova remains India-market and English-only. This work adds no display values, seed data, bilingual fields, RTL handling, or localization scaffolding.

## Functional Requirements

### Requirement 1: Preserve the repository-layer boundary (Ubiquitous)

THE repository layer SHALL register `FinnovaDbContext`, EF Core SQL Server configuration, repository interfaces and implementations, and repository-local adapters through `AddFinnovaRepository`.

THE repository layer SHALL NOT reference or register concrete types from `Finnova.Service`, and it SHALL NOT register MediatR handlers, FluentValidation validators, service ports, token services, or host authentication services.

### Requirement 2: Provide one service-layer registration entry point (Ubiquitous)

THE service layer SHALL expose `AddFinnovaServices(IServiceCollection services, IConfiguration configuration)` in a source file owned by `Finnova.Service`.

THE service extension SHALL register the following existing service-layer concerns with their current lifetimes and behavior:

- MediatR handlers from the `Finnova.Service` assembly.
- FluentValidation validators from the same assembly.
- `ValidationBehavior<,>` as the existing transient MediatR pipeline behavior.
- `JwtSettings` from the existing `Jwt` configuration section, `ITokenService` → `TokenService`, and `IPasswordPolicy` → `DefaultPasswordPolicy`.
- `IAssetCodeGenerator` → `AssetCodeGenerator`.
- `CustomerFieldApplicabilityEvaluator`.
- The existing development GL port interfaces to their existing stub implementations.
- The existing document lookup validation and document usage port interfaces to their existing implementations.
- Existing blob-storage options and provider-selected singleton registration through `AddBlobStorage`.

THE service extension SHALL reuse existing classes and interfaces rather than create parallel implementations or alternate service lifetimes.

### Requirement 3: Compose both layers in every backend API host (Event-driven)

WHEN `Finnova.UAService`, `Finnova.AccountsService`, or `Finnova.SystemAdminService` builds its service collection, THE host SHALL call `AddFinnovaRepository` exactly once with a validated `FinnovaConnection` value and SHALL call `AddFinnovaServices` exactly once with the host configuration.

THE host SHALL retain ownership of JWT authentication and authorization, host-specific exception middleware, controller setup, OpenAPI, health checks, and host-specific authorization policies.

### Requirement 4: Resolve the confirmed nullable configuration diagnostic (Unwanted-event)

IF `FinnovaConnection` is absent, THEN the host SHALL fail during startup with an `InvalidOperationException` that identifies the required connection-string setting, rather than passing null into the repository registration.

IF `FinnovaConnection` is empty or whitespace-only, THEN `AddFinnovaRepository` SHALL reject it with an argument/configuration exception before the EF Core context is used.

THE implementation SHALL not use the null-forgiving operator merely to suppress the existing `CS8604` warning.

### Requirement 5: Avoid duplicate or omitted application registrations (State-driven)

WHILE a host uses the new service extension, MediatR handlers and validators SHALL be registered once from the service assembly, the validation pipeline SHALL be registered once, and the manual service registrations moved into the extension SHALL not remain as duplicate host registrations.

WHILE a host uses both extensions, a handler that depends on a repository interface and a service-layer dependency SHALL be constructible from the same scoped request container. The repository and service registrations SHALL therefore remain compatible with the existing scoped `FinnovaDbContext` lifetime.

### Requirement 6: Keep runtime and wire behavior stable (Complex)

WHEN the refactored hosts process existing requests, THE solution SHALL preserve existing routes, response contracts, JWT validation, authorization policies, middleware mappings, blob-storage provider selection, and repository behavior. The refactor SHALL change registration location and composition only, except for the explicit fail-fast handling of an invalid connection string.

### Requirement 7: Support a single opt-in composition operation (Optional-feature)

WHERE a future host or test composition root opts into `AddFinnovaServices`, THE extension SHALL provide the complete service-layer registration set without requiring that caller to know individual handler, validator, or service implementation registrations.

A caller MAY continue to register a host-specific replacement after `AddFinnovaServices`; the later registration remains the deliberate override point for a real adapter replacing a development stub. The extension SHALL not lock those types against replacement.

## Non-Functional Requirements

- **Buildability:** The changes SHALL compile under the target framework and package versions already used by the solution. This specification makes no claim that a build has been run.
- **Layering:** Repository code SHALL remain independent of service code. Hosts may reference both layers as composition roots.
- **Diagnosability:** Invalid required configuration SHALL fail early with a named setting. No null-forgiving suppression SHALL hide the connection-string problem.
- **Lifetime safety:** `FinnovaDbContext` and repository/service request dependencies SHALL remain scoped; blob storage retains its existing singleton lifetime.
- **Security preservation:** No JWT validation setting, role claim type, authorization policy, token payload, or middleware ordering is changed by the DI refactor.
- **Product constraints:** The change SHALL introduce no non-English data, bilingual fields, RTL handling, or localization code.

## Acceptance Criteria

1. `Finnova.Service/DependencyInjection.cs` exists and exposes `AddFinnovaServices(IServiceCollection, IConfiguration)` using the existing service implementations and assemblies listed in Requirement 2.
2. `Finnova.Repository/DependencyInjection.cs` remains the repository composition boundary, and no repository source file references the `Finnova.Service` namespace.
3. Each of the UA, Accounts, and SystemAdmin `Program.cs` files calls `AddFinnovaRepository` once and `AddFinnovaServices` once.
4. Each host no longer manually registers the MediatR scan, FluentValidation scan, validation pipeline, token service, asset generator, password policy, or registrations moved into the service extension. SystemAdmin no longer scans the service assembly twice.
5. A missing connection string fails at startup before EF Core is resolved with an error identifying `FinnovaConnection`; an empty or whitespace value is rejected by the repository extension.
6. The nullable connection-string calls no longer produce the tracked `CS8604` warning when the user later compiles. This criterion is verified by the user’s later build; no build result is asserted by this specification.
7. A DI registration test or equivalent static review confirms the service descriptors use the existing intended lifetimes and that both repository and service dependency families are present.
8. No backend HTTP route, request/response contract, gateway route, database entity, EF configuration, or migration changes are introduced.
9. Frontend impact is **NONE**. The UI continues to use the same gateway origin and existing APIs; no file under `E:\Finnova\Finnova-UI\Finnova-UI` is changed.
10. Later user-run validation includes the backend solution build and test commands listed at the end of `tasks.md`; this authoring pass runs neither command.

## Out of Scope

- Implementing or changing business features, CQRS handlers, repositories, entities, contracts, controllers, or exception mappings.
- Adding or applying EF Core migrations, changing the database schema, or changing seed data.
- Changing JWT configuration, authorization policy semantics, token claims, CORS, gateway routing, or middleware behavior.
- Replacing development GL or document stubs with production adapters.
- Adding frontend models, services, pages, routes, navigation, tests, localization, or RTL support.
- Diagnosing compiler errors that are not present in tracked evidence. Such diagnostics should be added to a later revision after the user supplies the build output.
- Running `dotnet build`, `dotnet test`, `dotnet ef`, `npm run build`, `npm test`, or any other build, test, migration, deployment, or database command during document authoring.
