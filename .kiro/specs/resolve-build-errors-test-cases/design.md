# Design Document — Resolve Build Errors and Test Cases

## Overview

This is a verification-first repair of the existing Finnova platform, not a new feature. The design keeps the current .NET layered architecture, the three API hosts, the YARP gateway, the React/MUI frontend, and the existing xUnit/FsCheck and Vitest test strategies. The implementation first records the current state when artifacts do not already establish a failure, then repairs backend compilation, backend tests, frontend compilation, and frontend tests in dependency order. It finishes with separate backend and frontend verification and a report that distinguishes current results from historical evidence.

The backend uses the existing .NET target framework, ASP.NET Core Web API hosts, MediatR, FluentValidation, EF Core, xUnit, FsCheck.Xunit, and `WebApplicationFactory`. The frontend uses React 18, TypeScript, Vite, Material UI, React hooks/context, the shared axios instance, Vitest, React Testing Library, and jsdom. No Redux, RxJS, new transport client, new endpoint, or localization capability is designed.

The findings establish several historical backend build and test categories but do not establish the current status of the dirty worktree. The frontend checkout was unreachable during investigation. Consequently, the design treats historical repairs as candidates to verify, treats the exact-type assertion and two current source-level issues as confirmed review items, and requires a baseline for any additional failure before editing it.

## Reused versus New Work

### Reused

The repair reuses the following existing assets without changing their responsibilities:

- `Finnova.Backend.slnx` and its current project graph.
- `Finnova.Models` entities, enums, exceptions, identity-verification contracts, and financial models.
- `Finnova.Repository` EF Core context, configuration, repositories, repository interfaces, and repository-owned application reference ports.
- `Finnova.Service` CQRS handlers, validators, MediatR pipeline, identity-verification workflow, document validation, customer rules, and service registrations.
- `Finnova.UAService`, `Finnova.AccountsService`, and `Finnova.SystemAdminService` host authentication, authorization policies, controllers, middleware, and testability `Program` shims.
- `Finnova.ApiGateway` YARP clusters, aliases, forwarded authorization header, and host separation.
- `Finnova.Tests` xUnit/FsCheck property and example tests, integration factories, EF Core InMemory setup, JWT test setup, and current test package family.
- The frontend's actual `package.json` scripts and existing Vite, TypeScript, Vitest, MUI, hooks, context, shared axios, mock/real service toggle, and test conventions after the checkout is reachable.

### New or changed work

The precise change set is conditional on the baseline. The expected mandatory test-quality change is:

- `Finnova.Tests/Unit/IdentityVerificationProperties.cs`: replace the broad `catch (InvalidOperationException)` in `InvalidRecordShapeIsRejected` with an exact runtime-type assertion while retaining the property and its generated input.

The expected conditional backend changes restore only missing historical repairs, if a current baseline proves they are missing:

- `Finnova.Tests/Finnova.Tests.csproj`: package-version alignment, especially the xUnit 2.9.2 family.
- `Finnova.Service/Finnova.Service.csproj`: the ASP.NET Core framework reference required by service code.
- `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`, repository adapters, and service consumers: the existing repository-owned dependency boundary.
- Identity-verification exception, workflow, validator, controller, and middleware files where the historical compiler or behavioral repair is absent.
- `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`, `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`, and the relevant controllers/validators where a confirmed current behavior does not satisfy the established error contract.
- Affected backend test fixtures and assertions only where they accurately reflect the existing API contract.

No frontend file is selected in advance because `E:\Finnova\Finnova-UI\Finnova-UI` was unreachable. If a current frontend diagnostic is found, the implementer selects the smallest existing file or test fixture that owns that diagnostic and records the path and reason before editing. A genuine API contract mismatch requires scope confirmation before either repository is changed.

## Requirements Coverage Map

| Requirement | Design coverage |
| --- | --- |
| R1 Baseline capture | Baseline protocol, current-versus-historical evidence rules, and first implementation task. |
| R2 Backend buildability | Project/package compatibility, dependency-direction boundary, source/API compatibility checks, and backend build verification. |
| R3 Backend behavioral tests | Affected test/host/production boundaries for multipart input, middleware properties, nullable workflow results, amortization values, identity outcomes, neutral contracts, application 404, and document format validation. |
| R4 Exact exception coverage | `InvalidRecordShapeIsRejected` exact runtime-type check and retained FsCheck generation. |
| R5 Security and stable failures | Existing JWT prerequisite, host policies, middleware order, document validation boundary, error codes, and no-auth-bypass rule. |
| R6 Frontend build/test boundary | Inspect actual UI configuration, reuse existing stack/scripts, verify build and Vitest, and record blocked status if unreachable. |
| R7 Platform boundaries | Conditional, minimal file edits; unchanged routes/contracts; preservation of dirty unrelated work. |
| R8 Final verification | Ordered backend/frontend verification, current-result reporting, zero-failure/zero-skip completion rule, and secret handling. |

## Architecture and Repair Boundaries

The repair does not change request flow. The intended dependency shape remains:

```text
Finnova.Models
      ^
Finnova.Repository  <---- Finnova.Service
      ^                         ^
      +--------- API hosts -----+
                                 ^
                         Finnova.Tests

Finnova-UI -> shared axios -> existing YARP alias/host route -> backend host
```

The project boundary repair is made at the owning assembly, not by adding reverse references. Repository adapters consume repository-owned reference ports. Service handlers consume those ports. API hosts compose repository and service registrations. Tests reference the hosts and libraries through the existing project references.

The runtime repair is made at the narrowest owner of each invariant:

- Project/package compatibility belongs to the project file or source API that owns the incompatibility.
- Repository/service dependency direction belongs to the repository interface boundary and project references.
- Multipart request shape belongs jointly to endpoint metadata and the integration fixture.
- Extension/MIME/signature agreement belongs to `DocumentInputValidator` before provider or persistence calls.
- Stable identity and application error mapping belongs to the corresponding host middleware.
- EF provider replacement belongs to the relevant test factory, not production registration.
- Amortization value equality and exception exactness belong to the tests because they describe assertion semantics.
- Frontend compilation belongs to the actual UI TypeScript/Vite configuration and owning component/service type, but only after a current diagnostic is captured.

No change is made merely because a historical report mentions a file. The current baseline and source review decide whether the change is needed.

## Data Models and Contracts

### Domain and persistence state

**State changes: N/A.** This repair adds no entity, enum, database table, migration, seed record, or persistence contract. The `JvPostingInbox` migration ownership remains unresolved and is explicitly outside this repair unless separately confirmed.

Existing entities and EF configurations remain the source of truth. If a current build fails because an existing EF configuration is missing a key or a test factory leaves incompatible provider options registered, the fix stays within that existing configuration/factory boundary and does not introduce schema work.

### Backend project and package contracts

The build contract is the current project graph in `Finnova.Backend.slnx`, the configured target framework, nullable settings, package versions, and project references. The test contract includes `Finnova.Tests.csproj`, its runsettings behavior, and the references to both API hosts, Service, Repository, and Models.

The following source-level contracts are preserved or restored where a current baseline requires them:

- `Finnova.Repository.Interfaces.ApplicationReferencePorts` remains the shared location for repository-facing application reference interfaces. The repository does not reference `Finnova.Service`.
- `Microsoft.AspNetCore.App` remains available to `Finnova.Service` for its HTTP/hosting abstractions.
- `IVerificationWorkflowCoordinator` and any legacy compatibility interface remain distinct, with one active contract and no duplicate declaration.
- Provider executor calls retain their current function, operation, correlation, and cancellation argument semantics.
- Existing identity exception constructors remain compatible with current callers and preserve their error data.
- FluentValidation rules use APIs supported by the configured package version.

### Identity-verification contracts

The current multipart controller contract remains authoritative. `IdentityVerificationController` continues to declare `multipart/form-data`, bind the established form fields, and normalize a missing submitted value so domain validation can reject it through the intended validation path. Tests must send `MultipartFormDataContent` for these routes.

`VerificationWorkflowResponse` retains nullable result slots. A result for an operation that did not run is represented as JSON `null` at its addressable property, not as an absent property or fabricated success/failure result.

`ProviderNeutralComparisonInput` retains its current public shape, including `ActorReference` when present. It must not acquire public members whose names or values expose provider, secret, or token-specific data. The reflection property test checks the actual contract rather than hard-coding a stale field list.

`ValidationRecord` retains the domain rule that identifier operations carry validation status and comparison operations carry comparison outcome. A malformed identifier record is expected to throw exactly `InvalidOperationException`; this is the only confirmed mandatory test assertion correction.

### Application-processing and document contracts

`ApplicationNotFoundException` retains error code `ERR-APP-404`. Accounts middleware maps it to HTTP 404 rather than the generic 500 branch. The existing application integration test remains the end-to-end contract check.

`DocumentInputValidator` retains conjunctive validation of size, accepted media type, filename extension agreement, and content signature. A `.png` filename paired with `application/pdf` is invalid even if the uploaded bytes match a different format. The validator must reject before external provider or persistence work.

### Frontend contracts

**Frontend API/type changes: none are designed in advance.** The API routes, JSON shapes, error contracts, and authentication behavior remain unchanged. After the frontend checkout is accessible, its current TypeScript models, service interfaces, mock and real implementations, toggle module, page/component props, and tests are inspected. A type or test change is allowed only to fix a captured current diagnostic while preserving the existing backend contract.

The UI continues to use one English display value per concept, existing camelCase API serialization, the shared axios instance at `src/services/api.ts`, and the established mock/real service selection through `VITE_USE_MOCK_API`.

## Component and Interface Changes

### Backend build and composition files

- `Finnova.Tests/Finnova.Tests.csproj`: verify package alignment and project references. Do not add a package solely to silence an error without identifying the API that requires it.
- `Finnova.Service/Finnova.Service.csproj`: verify the ASP.NET Core framework reference and existing project dependencies.
- `Finnova.Backend.slnx`: verify all expected projects are included. Modify only if the baseline proves a project inclusion defect.
- `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`, repository adapters, and service consumers: preserve the one-way dependency boundary and compile-time interface signatures.
- `Finnova.Repository/DependencyInjection.cs` and host `Program.cs` files: verify registrations and host composition only if the baseline identifies missing or duplicate registrations. The ticket does not authorize a general DI refactor.

### Backend production behavior and hosts

- `Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs`: correct constructor/API incompatibilities only when confirmed.
- Identity workflow files under `Finnova.Service/IdentityVerification/Workflow`: retain one active workflow coordinator contract and current method signatures.
- `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs`: retain extension/MIME/signature validation ordering.
- `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs`: retain multipart metadata and missing-value normalization.
- `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`: retain identity error code and correlation properties.
- `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`: map `ERR-APP-404` to 404.
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs`: ensure the test factory removes inherited SQL/provider options before registering InMemory, if a current integration baseline proves that wiring is failing.
- `Finnova.ApiGateway/appsettings.json`: verification-only under this design. No route change is expected because the repair preserves public paths and host placement.

### Backend test files

The affected tests are reviewed in their existing locations, including:

- `Finnova.Tests/Unit/IdentityVerificationProperties.cs`
- `Finnova.Tests/Unit/IdentityVerificationExamples.cs`
- `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`
- `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingIntegrationTests.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs`
- `Finnova.Tests/Integration/TestApplicationFactory.cs`
- `Finnova.Tests/JVPosting/JvPostingApiTests.cs`

The exact exception property is changed narrowly. Other tests are changed only when a baseline confirms a failure and the revised assertion matches the established production contract. Property-based tests retain at least 100 generated cases for relevant pure logic.

### Frontend files and integration points

The frontend repository must first be inspected. The likely integration points are the existing `package.json` scripts, `tsconfig`, Vite config, Vitest setup, feature service interfaces, real/mock services, model barrels, and component/page tests. The implementation must not create an ad hoc axios client or replace hooks/context with Redux/RxJS. If no current frontend diagnostic exists, no UI source file is modified.

## Error Handling

### Baseline and unavailable environment

If a required repository, package state, script, or environment value cannot be accessed, the implementation records the path and failure to access it as a blocked prerequisite. This is recoverable by restoring access or supplying the missing prerequisite, but it is not treated as a passing result. No source change is made to disguise an unavailable environment.

### Restore and compiler failures

A package conflict, missing framework reference, invalid project reference, duplicate type, stale method signature, unsupported library API, malformed expression, or missing symbol is a fatal implementation failure for the owning build target. The implementer changes the owning project/source boundary, reruns the smallest relevant build, and then reruns the full backend build. The repair must not exclude files, suppress errors, remove tests, or reverse dependency direction.

If a diagnostic appears only in the dirty Loan Account/Application Account or service-auth work, the implementer preserves the work and records the scope conflict. It is recoverable only after the owner confirms whether that work belongs to this ticket.

### Backend test setup failures

If `FINNOVA_TEST_JWT_KEY` is absent or blank, JWT-dependent test setup fails clearly and is recoverable by supplying an ephemeral process-scoped key. The key is never written to source, runsettings, logs, reports, configuration, or Git history. The implementation must not disable JWT validation or skip protected tests.

If a test host cannot construct its EF InMemory model, the failure is fatal to that host's integration tests. The implementer fixes test-factory provider-option replacement or the confirmed EF configuration issue while leaving production SQL configuration unchanged. A missing `JvPostingInbox` key or migration is handled as a scope decision if it requires schema ownership, not as an automatic migration change.

### Identity and document failures

- Missing, invalid, or wrong-role credentials retain the existing 401/403 behavior. Multipart fixture corrections are recoverable test-input repairs; they do not alter controller authorization.
- A missing submitted value remains a validation error through the existing controller/domain path. Sending the wrong media type must not be used to produce a convenient authorization result.
- Size, unsupported content type, extension/MIME mismatch, and signature mismatch are recoverable caller-input failures. They are rejected before provider or persistence work and retain existing status/code families.
- Identity validation middleware failures retain the existing stable `code` and non-empty correlation identifier. Loss of either is a production/middleware defect, not a reason to weaken the test.

### Application-processing failures

An unknown application identifier is a recoverable caller-visible not-found outcome. `ApplicationNotFoundException` must reach Accounts middleware as HTTP 404 with `ERR-APP-404`. Any 500 response is a failed repair unless a captured current contract proves the exception is not the expected not-found case.

### Assertion failures

`InvalidRecordShapeIsRejected` captures the thrown exception as `Exception?`. No exception fails the property. An exception succeeds only when `exception.GetType() == typeof(InvalidOperationException)`. A subtype, unrelated exception, or message-only match fails. This protects exact domain behavior without changing production code.

### Frontend build and test failures

A TypeScript/Vite compile error is fatal to the frontend build target and is fixed in the owning type, import, configuration, or component after the baseline identifies it. A Vitest failure is fixed in the owning UI behavior, service mock/real implementation, or assertion. A frontend test is not skipped or made less strict to hide a failure. Network errors in real-service tests use the repository's established mocking strategy and shared axios instance; no new HTTP client is introduced.

## Security

The repair preserves JWT Bearer validation, issuer/audience/lifetime/signature checks, role claim mapping, SystemAdmin and ApplicationProcessor policies, and authentication-before-authorization ordering. No endpoint becomes anonymous and no test token can choose its own role or actor outside the existing fixture contract.

`FINNOVA_TEST_JWT_KEY` is an ephemeral process prerequisite. It must not be hard-coded, committed, persisted in runsettings, copied into reports, or logged. If the configured test runner fails to honor the existing runsettings value, the implementation records the observed behavior and supplies the key process-scoped rather than weakening the host.

Identity fixtures use deterministic adapters and in-memory storage. They must not call external identity providers or require real credentials. Provider-neutral public contracts contain no provider secrets, access tokens, token-specific fields, or sensitive storage details.

The shared frontend axios instance remains responsible for JWT injection from `localStorage('finnova_token')` and central 401/403/409/timeout notifications. The repair does not add a client that could omit or duplicate authorization behavior. UI test fixtures use non-sensitive English-only values.

No log, error detail, or test report should contain a connection string, JWT key, bearer token, document contents, or provider secret. Existing correlation identifiers may remain in responses where they are part of the established contract.

## Design Decisions and Tradeoffs

1. **Capture the baseline before fixing unknown failures.** Static artifacts are valuable but cannot prove the current dirty worktree. A baseline prevents speculative edits and makes each repair attributable. The tradeoff is an extra verification step, chosen because correctness and preservation of user work matter more than speed.
2. **Fix the owning code or the test, never weaken the test.** A failing test may expose a production defect, an inaccurate fixture, or a stale expectation. The chosen approach corrects the narrowest owner and retains meaningful assertions. Tests are not deleted, skipped, converted to broad exception acceptance, or changed to accept invalid contracts.
3. **Use exact exception semantics.** The current broad catch allows derived exception types and is confirmed as a test-quality defect. Explicit runtime-type equality is chosen over assignable-type assertions because the domain contract requires the exact type.
4. **Preserve historical fixes conditionally.** Historical reports identify likely causes, but applying every reported patch blindly could clobber later work. The implementation verifies each candidate against current source and baseline output, then applies only missing or regressed changes.
5. **Keep dependency direction instead of adding a reverse reference.** Repository adapters share ports through the repository-facing boundary. Adding a repository-to-service reference would make the build pass at the cost of a layered architecture defect.
6. **Use multipart fixtures rather than relaxing endpoint metadata.** The identity controller consumes multipart data. Updating the fixture tests the real HTTP contract; removing `[Consumes]` would hide malformed clients and distort authorization results.
7. **Preserve conjunctive document validation.** Extension, declared MIME type, and file signature each provide a distinct trust-boundary check. Accepting any one signal alone would make the mismatch test green while weakening input validation.
8. **Keep host-specific error mappings.** Accounts owns application processing errors and SystemAdmin owns identity errors. A shared generic mapping would risk turning known errors into the wrong status or code family.
9. **Do not infer frontend changes from an inaccessible checkout.** The UI is included in verification scope, but no UI file is changed without a current diagnostic. This avoids inventing a type fix against an unknown revision. The tradeoff is that frontend verification may remain blocked until the repository is reachable.
10. **Do not fold unresolved product decisions into a build repair.** The customer clear-default invariant and `JvPostingInbox` migration ownership require owner decisions. Silently changing them would expand scope and could corrupt behavior or schema ownership.

## Testability and Verification Strategy

Pure and deterministic backend logic remains covered by FsCheck properties with at least 100 iterations, including amortization value determinism, identity record-domain rules, generated adapter outcomes, and provider-neutral contract shape. xUnit example tests cover extension/MIME mismatch and other explicit edge cases.

Backend integration tests use `WebApplicationFactory` to exercise host startup, JWT authentication, authorization, multipart binding, middleware mappings, EF InMemory setup, stable JSON contracts, and the application 404 path. The test factories remain deterministic and do not require external providers or a production database.

Frontend verification uses the actual UI repository configuration. The build checks TypeScript and Vite integration. Vitest/React Testing Library checks service behavior, mock/real selection where configured, rendering, user interactions, and success/error paths already covered by the repository. Tests continue to run under jsdom and use existing MUI/hook patterns.

The verification order is: capture baseline, repair backend build, repair backend tests, repair frontend build, repair frontend tests, then run final backend and frontend verification. Each result records the command, working directory, exit code, and relevant output. No command in this design is evidence that this authoring workflow executed it.

## Final Implementation-Ordered Task List

The future `tasks.md` must use the following order. Each task must contain the complete copy-paste implementation for its specified file changes, but this design intentionally does not inline that code.

1. **Capture baseline first.** Record repository status without destructive cleanup; inspect the findings and current source; verify backend and frontend paths; capture backend solution/test and frontend build/test output when the current failure is not already determined from artifacts; record missing JWT-key or frontend-access prerequisites.
2. **Apply backend build fixes.** Verify and conditionally repair package alignment in `Finnova.Tests/Finnova.Tests.csproj`, the ASP.NET Core framework reference in `Finnova.Service/Finnova.Service.csproj`, solution/project references, repository-owned application ports, identity exception constructors, amortization syntax, workflow interfaces, stale imports/signatures, FluentValidation APIs, and host/test `Program` ambiguity. Preserve unrelated dirty work and do not add migrations.
3. **Apply backend test fixes.** Verify and conditionally restore multipart integration fixtures, middleware code/correlation assertions, nullable workflow-result assertions, amortization value comparisons, generated identity status checks, provider-neutral contract reflection, application 404 middleware behavior, document extension/MIME validation, EF InMemory test-factory wiring, and the exact-type assertion in `Finnova.Tests/Unit/IdentityVerificationProperties.cs`. Keep tests meaningful and retain at least 100 property iterations.
4. **Apply frontend build fixes.** After the UI checkout is reachable, inspect `package.json`, TypeScript, Vite, and import/type configuration; capture any current diagnostic; fix only the owning UI configuration, model, service, component, or test type while preserving React hooks, MUI, shared axios, mock/real toggle, English-only values, and existing API contracts.
5. **Apply frontend test fixes.** Inspect the actual Vitest setup and current failures; repair the owning service mock/real behavior, component/page interaction, or assertion; retain React Testing Library/jsdom coverage, avoid Redux/RxJS and ad hoc axios, and do not skip or weaken tests. If the UI remains unreachable, document the block instead of making speculative changes.
6. **Perform final verification.** Supply `FINNOVA_TEST_JWT_KEY` only process-scoped; build the backend solution and test project, run focused and complete backend tests, run the actual frontend build and Vitest scripts when available, verify zero failures and zero skips, review the diff for preserved unrelated work and unchanged contracts, and report current results separately from historical reports. Stop on unresolved failures or scope decisions rather than declaring the ticket complete.
