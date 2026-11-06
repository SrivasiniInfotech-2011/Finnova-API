# Design Document — Resolve Build and Unit-Test Failures

## Overview

This is a bounded repair of the existing .NET backend, not a new feature. The design keeps the current layered architecture, API routes, domain contracts, authentication policies, gateway routes, and test strategy. It brings the solution and test project back to compile-time compatibility, preserves the eight behavioral repairs already present in the current tree, and restores exact exception-type coverage in `IdentityVerificationProperties.InvalidRecordShapeIsRejected`.

The backend uses .NET 10, ASP.NET Core Web API hosts, MediatR, FluentValidation, EF Core, xUnit, FsCheck.Xunit, and `WebApplicationFactory` integration tests. The system has separate Accounts and SystemAdmin hosts behind the YARP gateway. The repair does not merge hosts or introduce a new abstraction. The frontend is outside the repair boundary because the API contracts remain unchanged.

The historical build report stated that the solution and test project compiled after the build pass. The historical test-repair report stated that all 555 tests passed after a rebuild with a process-scoped `FINNOVA_TEST_JWT_KEY`. Those are reported historical results, not commands run for this design. The implementation must verify the current checkout.

## Reused-vs-New

### Reused

Almost everything is reused. The repair retains:

- `Finnova.Backend.slnx` and its existing project graph.
- The current .NET 10 target frameworks and existing package families.
- `Finnova.Models` domain entities, exceptions, enums, value objects, and identity-verification contracts.
- `Finnova.Repository` EF Core context, generic repositories, repository interfaces, and identity/application adapters.
- `Finnova.Service` CQRS handlers, validators, application reference ports, identity-verification workflow, provider executor, and document validator.
- `Finnova.SystemAdminService` JWT Bearer authentication, the `SystemAdmin` policy, controller routes, identity workflow controller, middleware, and public `Program` testability shim.
- `Finnova.AccountsService` Application Processing controller, middleware, `ApplicationProcessor` policy, and public `Program` shim.
- `Finnova.Tests` xUnit/FsCheck tests, deterministic identity test factory, application-processing test factory, and integration test approach.
- Existing gateway aliases and clusters in `Finnova.ApiGateway/appsettings.json`. The current file contains the identity-verification and application-processing aliases needed by the existing tests and does not need a new route for this repair.

### New or changed by this repair

The only mandatory new behavior is precision in one existing property test: replace broad exception acceptance with an exact runtime-type check. Other implementation steps are conditional repairs: apply a listed build correction only if it is missing from the current checkout, and preserve the current behavioral corrections rather than reimplementing them.

No new domain model, API endpoint, database object, gateway cluster, frontend component, or localization asset is designed here.

## Current State Assessment

The on-disk inspection establishes the following:

- `Finnova.Tests/Finnova.Tests.csproj` currently aligns `xunit`, `xunit.core` through the xUnit dependency graph, and `xunit.extensibility.core` at 2.9.2. The test project references both API hosts, the repository/service/model projects, MVC testing, EF InMemory, FsCheck, and JWT token support.
- `Finnova.Service/Finnova.Service.csproj` currently has `FrameworkReference Include="Microsoft.AspNetCore.App"` and references `Finnova.Repository`.
- Application reference contracts currently live in `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`; service validation consumes them through `Finnova.Repository.Interfaces`, while the service-local file retains only `IApplicationReferenceValidator`.
- `IdentityVerificationExceptions.cs` assigns the inner exception in the constructor and has the collection constructor used by current callers.
- `IdentityVerificationController` declares comparison routes as `multipart/form-data`, normalizes a missing submitted value to an empty string, and leaves domain validation responsible for rejecting required input.
- `DocumentInputValidator` requires the content type to be accepted, the filename extension to agree with the content type, and the signature to match.
- SystemAdmin middleware emits identity error `code` and `correlationId`; Accounts middleware maps `ERR-APP-404` to HTTP 404.
- `ApplicationProcessingApiFactory` removes both `DbContextOptions<FinnovaDbContext>` and the provider-specific `IDbContextOptionsConfiguration<FinnovaDbContext>` before registering the InMemory provider.
- The three workflow result slots are nullable in `VerificationWorkflowResponse`, and the integration test checks an unrun address result as a JSON null value.
- `AmortizationCalculatorProperties.Deterministic` compares scalar schedule values and `ScheduleLineValue` sequence equality by value. The identity property checks generated adapter statuses and the neutral-contract property includes `ActorReference` while excluding provider/secret/token names.
- `InvalidRecordShapeIsRejected` remains weakened: `catch (InvalidOperationException)` sets a boolean. This is the one confirmed mandatory source correction.
- The working tree has unrelated JV-posting modifications and untracked files. The design preserves them.

## Requirements-Coverage Map

| Requirement | Design element that satisfies it |
| --- | --- |
| U1, E1 | Existing project graph plus conditional project/package/source repairs in the build-compatibility task. Validation uses solution and test-project compilation commands run by the implementer. |
| U2 | `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs` remains the shared boundary; adapters implement those interfaces and service validators consume them without a repository-to-service reference. |
| U3, E3, W5 | Explicit `Exception?` capture in `InvalidRecordShapeIsRejected`, followed by `ex.GetType() == typeof(InvalidOperationException)`. |
| U4 | Test changes remain assertion-preserving and targeted. No tests are removed, skipped, or changed to broad exception assertions. |
| E2, W1 | Existing test-host prerequisite reads `FINNOVA_TEST_JWT_KEY`; implementer supplies it only in the test process and runs the full suite. |
| E4, W2 | Existing multipart endpoint declarations and multipart authorization fixtures are retained; authentication and authorization remain in the host pipeline before controller execution. |
| E5, W3 | `DocumentInputValidator` keeps the accepted MIME, extension, and signature checks before storage/provider work. |
| E6, W4 | Existing Accounts and SystemAdmin middleware mappings retain stable status, code, message/detail, and correlation fields. |
| S1 | `VerificationWorkflowResponse` nullable result slots and the full-workflow integration assertion that checks JSON `null`. |
| S2 | `RecordingIdentifierAdapter` returns the generated outcome and the property compares each result with its generated status and call count. |
| S3 | `ScheduleResult` scalar fields and the `Installments` value sequence are compared rather than object references. |
| S4 | `ProviderNeutralComparisonInput` public contract reflection includes `ActorReference` and rejects provider-, secret-, and token-named fields. |
| C1 | Ordered implementation and verification tasks at the end of this document. |
| C2 | No UI source change; a final UI compatibility verification only if the UI repository is accessible. |

## Architecture and Repair Boundaries

The repair follows the existing request paths.

```text
Build graph:
Finnova.Backend.slnx
  -> Models -> Repository -> Service -> Accounts/SystemAdmin hosts -> Tests

Identity request:
Test client -> host authentication/authorization -> controller
  -> MediatR/handler or workflow -> validator/provider/storage/repository
  -> host ExceptionHandlingMiddleware -> ProblemDetails

Application request:
Test client -> Accounts authentication/authorization -> controller
  -> application handler/repository -> Accounts ExceptionHandlingMiddleware
  -> ProblemDetails

Gateway request:
UI/client -> existing YARP alias or host route -> unchanged API host
```

The dependency repair is made at the project boundary rather than by adding reverse references. The service can consume repository contracts because the contracts belong to the repository-facing assembly. The repository does not depend on service code. The test project uses explicit host types or controller-based `WebApplicationFactory` targets to avoid ambiguity between the two public `Program` shims.

The runtime repair is made at the narrowest owner of each invariant:

- Request media type is owned by endpoint metadata and test fixtures.
- Required comparison input is owned by controller normalization plus existing domain validation.
- Extension/MIME/signature agreement is owned by `DocumentInputValidator` before storage/provider calls.
- Stable error mapping is owned by the host middleware that knows the host's error family.
- EF provider selection is owned by the integration factory, which removes inherited SQL Server options before adding InMemory.
- Value determinism is owned by the test assertion because the calculator already returns a value-oriented schedule result.
- Exception exactness is owned by the property test because it verifies the invariant's contract.
- JWT validation remains owned by ASP.NET Core authentication and the existing role policies.

## Data Models, Contracts, and Interfaces

### Build and project contracts

The solution contract is the set of projects in `Finnova.Backend.slnx`. The test project contract is `Finnova.Tests/Finnova.Tests.csproj`. The repair keeps the current target framework and package compatibility. The relevant package relationship is that `xunit`, `xunit.core`, and `xunit.extensibility.core` must resolve to compatible 2.9.2 versions. The ASP.NET Core framework reference supplies the HTTP and hosting abstractions used by `Finnova.Service` without adding a redundant options package.

The application reference boundary is:

```csharp
namespace Finnova.Repository.Interfaces;

public interface ILineOfBusinessReferencePort { ... }
public interface ICustomerReferencePort { ... }
public interface IBranchReferencePort { ... }
public interface ILookupReferencePort { ... }
```

`ApplicationReferenceValidator` consumes these interfaces, and repository adapters implement them over `FinnovaDbContext` and `ILookupRepository`. The repair does not change method meanings or introduce a service-to-repository reverse dependency.

### Identity contracts

`IdentityVerificationController.ComparisonForm` is the multipart binding model. `SubmittedValue` is nullable at the HTTP boundary, and `UploadAndSend` maps a missing value to `string.Empty`; the existing command/domain validation then rejects it. `DocumentFile`, `ExtractedValuesJson`, `MappingProfile`, and `SessionId` remain as current form fields.

`ProviderNeutralComparisonInput` is a record with these current public fields:

- `OperationType`
- `SubmittedValue`
- `ProtectedDocumentReferenceId`
- `ProtectedDocumentStream`
- `ExtractedValues`
- `CorrelationId`
- `MappingProfile`
- `ActorReference`

The property test must reflect this current contract and reject public names containing `Provider`, `Secret`, or `Token`. It must not demand a removed field or add a provider-specific field to make the test pass.

`VerificationWorkflowResponse` contains nullable `PanIdentifierResult`, `PanNameResult`, `AadhaarIdentifierResult`, and `AadhaarAddressResult`, plus `PartialFailures`. A partial request therefore serializes an unrun result slot as JSON `null` under the current serializer configuration. The integration test checks that explicit contract.

`ValidationRecord` is checked by `IdentityVerificationInvariants.EnsureResultDomain`. Identifier operations require `ValidationStatus` and no `ComparisonOutcome`; comparison operations require `ComparisonOutcome` and no `ValidationStatus`. A malformed identifier record must throw exactly `InvalidOperationException` for the property to pass.

### Application-processing contracts

`ApplicationNotFoundException` exposes `ErrorCode == "ERR-APP-404"`. Accounts middleware maps that error to `StatusCodes.Status404NotFound`, while other application errors retain their existing status family. `ApplicationProcessingApiFactory` supplies JWT settings and the `ApplicationProcessor` policy in test configuration and replaces SQL Server EF options with the InMemory provider.

### Amortization contracts

`AmortizationCalculator.Calculate` returns `ScheduleResult`, containing scalar values such as `PeriodicInterestRate`, `EmiAmount`, and total amounts, plus `IReadOnlyList<ScheduleLineValue> Installments`. The deterministic property compares each scalar and the sequence's value equality. It does not change the calculator or the financial formulas.

## Components and Interfaces

### Project and package configuration

`Finnova.Tests/Finnova.Tests.csproj` owns test package alignment and project references. `Finnova.Service/Finnova.Service.csproj` owns the ASP.NET Core framework reference. The solution file owns project inclusion. Changes are conditional: if the current file already contains the required repair, no edit is made.

### Domain exceptions and workflow interfaces

`Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs` owns exception construction and error codes. Constructor-dependent values are assigned in constructors, not member initializers that cannot access constructor parameters.

`ILegacyVerificationWorkflowCoordinator` remains distinct from the active `IVerificationWorkflowCoordinator`. The active interface is declared with `ExecuteAsync(VerificationWorkflowRequest, CancellationToken)` and is implemented by `VerificationWorkflowCoordinator`; `Finnova.SystemAdminService` registers and injects the active interface. The legacy interface is retained only where existing compatibility requires it and must not create a duplicate type declaration.

`IProviderCallExecutor.ExecuteAsync<T>` owns the call signature used by workflow code. Callers pass the function, operation type, correlation ID, and cancellation token in that order. The executor retains retry, timeout, cancellation, safe exception classification, and logging behavior.

### Application reference ports and validation

`ApplicationReferenceValidator` runs the five active-reference checks through the repository interfaces and throws the existing application reference exception for an invalid reference. `ApplicationValidation` owns FluentValidation rules for create/update/query commands. The update validator expresses rules directly rather than using the removed FluentValidation `Transform` API.

### Identity controller and document validation

`IdentityVerificationController` owns multipart request binding and calls `DocumentInputValidator` before protected storage. `DocumentInputValidator` rejects size, accepted content type, extension/MIME mismatch, and signature mismatch. It does not infer format from bytes while ignoring the filename or declared MIME type.

The controller's `[Authorize(Policy = "SystemAdmin")]` and `[Consumes("multipart/form-data")]` metadata remain unchanged. Authorization tests must provide multipart content, but production authentication and authorization are not bypassed.

### Host middleware and gateway

`Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` owns identity error mappings and correlation properties. Identity exceptions retain their typed status/error-code mappings; shared validation errors use the identity path family. `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs` owns the `ApplicationProcessingException` mapping, including `ERR-APP-404` to 404.

`Finnova.ApiGateway/appsettings.json` currently contains routes for the SystemAdmin host, the identity-verification UI alias, and the application-processing Accounts alias. Since this repair does not change route shapes, the gateway file is verification-only. If a current checkout lacks one of the already-consumed aliases, the implementer must stop and reconcile that discrepancy before changing routing; this spec does not authorize collapsing hosts or inventing a new prefix.

### Test factories and tests

`IdentityVerificationAppFactory` replaces provider/storage/repository registrations with deterministic in-memory implementations. `ApplicationProcessingApiFactory` removes both generic DbContext options and provider-specific options configuration before adding the named InMemory database. The factories preserve JWT configuration from `FINNOVA_TEST_JWT_KEY` and fail clearly when it is absent.

The affected test files are:

- `Finnova.Tests/Unit/IdentityVerificationProperties.cs`
- `Finnova.Tests/Unit/IdentityVerificationExamples.cs`
- `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`
- `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingIntegrationTests.cs`
- `Finnova.Tests/Finnova.Tests.csproj`

The property tests retain `MaxTest = 100` or an equivalent minimum of 100 generated cases. Integration tests use `WebApplicationFactory` to exercise host wiring, authorization, middleware, controller binding, and stable response contracts.

## Error Handling

### Compile and restore failures

A package version conflict, missing framework reference, invalid project reference, stale method signature, duplicate type declaration, unsupported FluentValidation API, or malformed source expression is a fatal implementation error. The implementer corrects the owning project or source boundary and reruns the relevant build command. The repair must not solve these errors by suppressing warnings, excluding source files, or removing tests. Historical restore/build results are evidence only and must not be restated as commands run by this workflow.

### Test prerequisite failures

If `FINNOVA_TEST_JWT_KEY` is absent or blank, the test host throws its explicit prerequisite `InvalidOperationException` during setup. This is fatal for JWT-dependent integration tests and recoverable only by supplying a process-scoped key. The key must not be logged, committed, placed in a runsettings file, or copied into application configuration. A missing key must not be handled by disabling JWT validation.

### Identity request failures

- Missing or invalid bearer credentials are handled by ASP.NET Core authentication and return 401. Authenticated non-SystemAdmin users return 403 through the existing policy. These are expected, recoverable caller outcomes and are asserted by integration tests.
- Missing multipart submitted text reaches the existing validation boundary as an empty string and returns the existing identity validation response. It must not be converted into an unsupported-media-type response by sending the wrong fixture media type.
- A document size violation returns the existing 413 identity code. An unsupported content type, extension/MIME mismatch, or signature mismatch returns the existing 415 identity code. These are recoverable input errors, logged only through existing middleware/application behavior, and must occur before provider/storage work.
- Identity validation exceptions are mapped by SystemAdmin middleware to HTTP 400 with the identity error code and the request correlation identifier. The middleware writes no sensitive identifier values to public error details.
- Provider timeout/unavailability behavior remains the existing typed 503 path. Cancellation requested by the caller remains cancellation rather than being converted into a successful result.

### Application-processing failures

An unknown application ID raises `ApplicationNotFoundException`, which is a recoverable caller-visible not-found outcome. Accounts middleware returns 404 and `ERR-APP-404`. It must not fall through to the generic 500 branch. Other application exceptions retain their current codes and status mapping.

### Assertion failures

`InvalidRecordShapeIsRejected` captures the exception as `Exception?`. If no exception is thrown, the property fails. If an exception is thrown, it succeeds only when `exception.GetType() == typeof(InvalidOperationException)`. Any subtype, unrelated exception, or null result is a test failure. This is intentionally stricter than `Assert.IsType`-style assignability and prevents future weakening of the domain invariant.

## Security

The repair preserves JWT Bearer validation for issuer, audience, lifetime, and signing key, plus the existing `SystemAdmin` and `ApplicationProcessor` role policies. It makes no endpoint anonymous and does not accept a caller-supplied role or actor to bypass authorization.

`FINNOVA_TEST_JWT_KEY` is an ephemeral test prerequisite. The implementer supplies it process-scoped, uses it to create test tokens, and lets the process exit. No secret is committed or written to code, configuration, runsettings, reports, logs, or the spec. The design does not prescribe a literal key value.

The identity test fixtures use deterministic provider adapters and in-memory storage. They must not call external providers or require real credentials. The provider-neutral contract remains free of provider secrets, tokens, storage URLs, and provider implementation types. Existing masking and privacy-safe exception messages remain unchanged.

The API gateway forwards the existing authorization header through the existing aliases. No route rewrite is changed, and host separation remains intact.

## Design Decisions and Tradeoffs

1. **Correct the assertion instead of changing the invariant.** The production invariant already throws `InvalidOperationException` for an invalid record shape. Exact runtime-type assertion protects the intended contract without changing production behavior. An explicit check is chosen over a framework-specific typed FsCheck helper because it is clear and compatible with the current test package.
2. **Repair dependency direction instead of adding a reverse project reference.** Moving application reference ports to `Finnova.Repository.Interfaces` lets adapters and services share contracts without making the repository depend on service implementation details. This is the smallest architectural correction.
3. **Use multipart fixtures rather than weakening endpoint metadata.** The controller explicitly consumes multipart form data. Updating tests to match the HTTP contract tests authorization and binding in the correct order. Removing `Consumes` would make the endpoint less precise and would hide client errors.
4. **Keep extension, MIME, and signature checks conjunctive.** A valid PDF header does not make a `.png` file with `application/pdf` acceptable. Requiring all three signals to agree prevents format ambiguity at the trust boundary.
5. **Keep host-specific middleware mappings.** The Accounts host owns application error mapping and the SystemAdmin host owns identity error mapping. A shared generic branch would risk converting known errors to the wrong code family.
6. **Compare amortization results by value.** The calculator returns independently allocated schedule lines. Sequence reference equality is not a valid determinism test. Comparing all scalar summaries and line values proves the intended result without changing production types.
7. **Use the current nullable workflow contract.** A nullable result slot is a deliberate representation of an operation that did not run in partial mode. The test checks explicit JSON null, preserving serialization coverage.
8. **No frontend change.** API routes and response contracts remain unchanged. A UI source edit would add risk without addressing a backend compile or test failure. The tradeoff is that UI verification is deferred to an environment where the UI repository is accessible.
9. **Preserve unrelated worktree changes.** The current JV-posting changes are outside this repair. Avoiding reset/cleanup protects user work and keeps the repair reversible.

## Testability and Verification Strategy

Pure or mostly pure logic is verified with FsCheck properties at a minimum of 100 generated cases: record-domain invariants, generated adapter outcomes, amortization determinism, and provider-neutral contract shape. Example tests cover document formats and exact error codes.

Integration tests are required for host wiring and boundaries: missing/invalid/incorrect-role JWT behavior, multipart routing, workflow JSON null slots, middleware `code`/correlation fields, application 404 mapping, EF InMemory host setup, and gateway route expectations. `WebApplicationFactory` factories keep provider and storage dependencies deterministic.

The implementer should verify in this order: compile the backend solution, compile the test project if needed, run focused affected tests with the process-scoped JWT key, then run the complete test project with the same process-scoped key. A UI build/contract check is separate and is performed only in the UI repository. No validation command in this design is evidence that this workflow executed it.

## Frontend Boundary

The frontend repository is expected at `E:\Finnova\Finnova-UI\Finnova-UI`, but it was not readable from the current environment. The repair design therefore treats the UI as unchanged and records that boundary as an assumption rather than inventing UI edits. Existing UI consumers should continue to use the same gateway aliases and response contracts. If a later UI inspection finds a genuine contract break, it must be raised as a separate scope decision; it is not silently folded into this backend repair.

## Implementation-Ordered Task List

The tasks document for this repair must follow this exact dependency order:

1. **Inspect and isolate the baseline.** Read the four prior task reports/reviews and current files; record the current branch and preserve unrelated JV-posting working-tree changes. Do not reset or clean the repository.
2. **Repair project/package compatibility.** Verify and, only if absent, apply the xUnit 2.9.2 alignment in `Finnova.Tests/Finnova.Tests.csproj`, the `Microsoft.AspNetCore.App` framework reference in `Finnova.Service/Finnova.Service.csproj`, and the existing project/reference set in `Finnova.Backend.slnx` and the test project.
3. **Repair domain and contract compilation.** Verify and, only if absent, apply constructor fixes in `Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs`, the switch-expression syntax correction in the amortization source, the duplicate workflow-interface resolution in `Finnova.Service/IdentityVerification/Workflow`, and the current provider-executor signatures/imports.
4. **Repair dependency direction and application validation.** Verify the shared ports in `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`, adapter implementations under `Finnova.Repository/Repositories`, service consumers under `Finnova.Service/Applications`, DI registration, direct FluentValidation rules, and current test API signatures. Apply only missing corrections.
5. **Verify host and gateway wiring.** Check `Finnova.SystemAdminService/Program.cs`, `Finnova.AccountsService/Program.cs`, both middleware files, and `Finnova.ApiGateway/appsettings.json`. Preserve authentication order, role policies, public testability shims, existing identity/application aliases, and stable error mappings. Add no new route unless an existing required alias is demonstrably absent.
6. **Preserve or restore production behavioral repairs.** Verify `DocumentInputValidator.cs` enforces extension/MIME/signature agreement, `IdentityVerificationController.cs` retains multipart metadata and empty-value normalization, and Accounts middleware maps `ERR-APP-404` to 404. Do not weaken validation or authorization.
7. **Preserve or restore integration-host repairs.** Verify `IdentityVerificationApiTests.cs` uses multipart fixtures and current nullable workflow JSON expectations. Verify `ApplicationProcessingApiFactory.cs` removes inherited SQL/provider options before registering InMemory and `ApplicationProcessingIntegrationTests.cs` asserts the stable 404 code.
8. **Preserve or restore focused property/example repairs.** Verify value-based amortization determinism, generated identity statuses, provider-neutral reflection, document mismatch examples, and the minimum 100 FsCheck iterations in `AmortizationCalculatorProperties.cs`, `IdentityVerificationProperties.cs`, and `IdentityVerificationExamples.cs`.
9. **Apply the mandatory exact-type correction last among source/test edits.** Modify `Finnova.Tests/Unit/IdentityVerificationProperties.cs` so `InvalidRecordShapeIsRejected` captures the exception and checks `ex.GetType() == typeof(InvalidOperationException)`, or use the supported typed assertion. Keep the invalid record input and the property test; do not accept derived types.
10. **Run implementer backend verification.** With `FINNOVA_TEST_JWT_KEY` supplied only for the process, build the solution and test project, run focused affected tests, and run the complete `Finnova.Tests` suite. Label these as implementer commands and report failures without claiming this workflow ran them.
11. **Run the frontend boundary verification if available.** In `E:\Finnova\Finnova-UI\Finnova-UI`, verify the unchanged UI against the unchanged API contracts using the repository's established verification command. Do not modify UI source; stop and request scope confirmation if a genuine API contract break is found.
