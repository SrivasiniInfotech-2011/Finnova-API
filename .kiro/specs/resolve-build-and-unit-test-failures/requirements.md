# Requirements Document

## Introduction

This repair restores the build and test health of the Finnova backend .NET solution and its `Finnova.Tests` project. The scope is the backend repository at `d:\Projects\Finnova\Finnova-API`, including the solution `Finnova.Backend.slnx`, the API hosts, shared domain/service/repository projects, and the existing unit and integration tests.

The prior build-repair report historically reported successful restore and compilation of the solution and test project, but it also reported 9 failing tests when `FINNOVA_TEST_JWT_KEY` was supplied in the test process. A later historical test-repair report stated that those 9 behavioral failures were addressed and reported 555 passing tests after a rebuild. This document does not claim that this workflow ran any build, restore, or test command. The implementer must independently verify the current tree.

The current on-disk inspection confirms that the main behavioral repairs are present in the relevant files. It also confirms that `Finnova.Tests/Unit/IdentityVerificationProperties.cs` still uses a broad `catch (InvalidOperationException)` assertion in `InvalidRecordShapeIsRejected`. That assertion accepts derived exception types and is the headline correction required by this repair.

Finnova is an India-only, English-only product. This repair introduces no user-facing data, bilingual fields, Arabic or other non-English sample values, or RTL behavior.

## Glossary

- **Backend solution**: `Finnova.Backend.slnx` and all projects listed by that solution.
- **Test project**: `Finnova.Tests/Finnova.Tests.csproj`, containing xUnit, FsCheck, unit, property-based, and integration tests.
- **Build repair**: The dependency, project-reference, source-compatibility, and API/signature corrections required for the solution and test project to compile.
- **Behavioral test repair**: A targeted correction to production behavior, test fixtures, test assertions, or test-host wiring that resolves one of the nine historically reported failures without suppressing the test.
- **Nine reported failures**: The historically reported failures covering multipart authorization fixtures, identity middleware error properties, nullable workflow results, amortization value equality, generated identity statuses, provider-neutral contract reflection, application 404 mapping, and document extension/MIME agreement.
- **Exact-type assertion**: An assertion that succeeds only when the thrown exception runtime type is exactly `InvalidOperationException`, not a subtype.
- **Process-scoped JWT key**: The `FINNOVA_TEST_JWT_KEY` value supplied only to the process running the test command. It is not written to source, configuration, repository files, or committed history.
- **Stable error contract**: The existing HTTP status and `ProblemDetails` properties, including the applicable error `code`, message/detail, and identity middleware correlation identifier.
- **Provider-neutral contract**: `ProviderNeutralComparisonInput` and its public members. It may contain operation and request-context data but must not expose provider-, secret-, or token-specific fields.

## Dependencies and Assumptions

- **Assumption A1, repair baseline:** The current tree retains the previously reported build repairs: xUnit package alignment, the `Microsoft.AspNetCore.App` framework reference, repository-owned application reference ports, exception constructor fixes, the switch-expression comma correction, duplicate workflow-interface resolution, stale method/import corrections, FluentValidation compatibility changes, and test API/signature updates. If any of these changes are absent in the implementer's checkout, they are part of this repair rather than a new feature.
- **Assumption A2, current behavioral baseline:** The current tree retains the later pass's eight behavioral corrections: multipart authorization fixtures, identity middleware properties, nullable workflow result assertions, amortization value equality, generated identity statuses, provider-neutral reflection expectations, application 404 middleware mapping, and extension/MIME agreement. The implementer must preserve or restore these corrections while applying the exact-type assertion.
- **Assumption A3, exact assertion choice:** The repair will use an explicit exception capture followed by `ex.GetType() == typeof(InvalidOperationException)` for `InvalidRecordShapeIsRejected`. The original FsCheck typed-throw assertion remains an acceptable equivalent only if it is demonstrably supported by the current package versions, but the implementation sequence chooses the explicit check to avoid package-API ambiguity.
- **Assumption A4, behavior preservation:** No API route, authentication policy, domain rule, or production behavior will be changed except where required by one of the nine reported failures or by the build corrections listed in A1. Existing contracts and stable error codes remain authoritative.
- **Assumption A5, JWT test prerequisite:** The complete `Finnova.Tests` run requires `FINNOVA_TEST_JWT_KEY` to be supplied process-scoped. The implementer will use an ephemeral development/test value suitable for the current JWT validation configuration and will not persist it. Missing-key failure is an environment/setup failure, not a reason to weaken authentication or skip tests.
- **Assumption A6, warnings:** Historical reports mention existing nullable/preview warnings after successful builds. The repair targets build errors and test failures; it will not suppress warnings, relax analyzers, or expand scope to unrelated warning cleanup unless a warning is proven to cause a current build failure.
- **Assumption A7, unrelated working-tree changes:** The currently modified and untracked JV-posting files are unrelated to this repair and must remain untouched. The implementer will not reset, delete, or overwrite them.
- **Assumption A8, frontend boundary:** This is a backend build/test repair. No Finnova-UI source change is required because the repair preserves API contracts and the relevant UI repository path was not available for on-disk reading in this environment. The UI remains unchanged; a boundary verification is required only if the UI repository is available to the implementer.
- **Assumption A9, UI verification scope:** UI verification means checking that the unchanged React/TypeScript application still builds against the unchanged API contracts. It does not mean adding UI code, changing routes, adding localization, or changing the shared HTTP client.
- **Assumption A10, suite identity:** The historical report used 555 tests as the suite size. The acceptance criterion is that the complete current `Finnova.Tests` suite passes, not that it must retain that exact count if unrelated tests have legitimately changed.

## Functional Requirements

### Ubiquitous Requirements

**Requirement U1: Solution compilation**

THE backend repair SHALL preserve a dependency graph in which every project in `Finnova.Backend.slnx`, including `Finnova.Tests`, compiles for its configured target framework without compiler errors or restore-time package version conflicts.

**Requirement U2: Contract and dependency direction**

THE repair SHALL keep persistence-facing application reference ports in the repository-facing contracts assembly so that repository adapters do not reference the service project, while service consumers reference the shared repository contracts through the existing project direction.

**Requirement U3: Assertion precision**

THE `InvalidRecordShapeIsRejected` property SHALL pass only when `IdentityVerificationInvariants.EnsureResultDomain` throws an exception whose runtime type is exactly `InvalidOperationException`; a derived exception SHALL fail the property.

**Requirement U4: Test integrity**

THE repair SHALL retain meaningful assertions for all affected behaviors and SHALL NOT resolve a failure by deleting a test, skipping it, broadening an expected exception type, suppressing an error, weakening authentication, or accepting an invalid contract.

### Event-driven Requirements

**Requirement E1: Build and test execution**

WHEN the implementer compiles `Finnova.Backend.slnx` and `Finnova.Tests/Finnova.Tests.csproj`, THE projects SHALL compile with the repaired package versions, framework references, contracts, exception constructors, service APIs, and host registrations.

**Requirement E2: Complete test execution with prerequisite**

WHEN the implementer supplies `FINNOVA_TEST_JWT_KEY` process-scoped and runs the complete `Finnova.Tests` suite, THE suite SHALL report zero failed tests and zero skipped tests, subject to the current suite contents.

**Requirement E3: Invalid identity record shape**

WHEN the property test generates a `ValidationRecord` containing a comparison outcome in an identifier-validation result domain, THE test SHALL invoke `EnsureResultDomain` and SHALL verify the exact `InvalidOperationException` runtime type.

**Requirement E4: Multipart authorization fixture**

WHEN an identity-verification authorization integration test exercises an endpoint declared for `multipart/form-data`, THE fixture SHALL send multipart content so authentication and authorization are evaluated before request model binding; it SHALL not turn an unsupported-media-type response into a false authorization result.

**Requirement E5: Document format mismatch**

WHEN document validation receives a supported content type whose filename extension does not agree with that content type, THE validator SHALL reject the request with the existing identity document-format error rather than accepting bytes that match a different format.

**Requirement E6: Application not-found mapping**

WHEN an authenticated Application Processor requests an unknown application identifier, THE Accounts host SHALL return HTTP 404 and preserve the stable `ERR-APP-404` code rather than converting the domain not-found exception to HTTP 500.

### State-driven Requirements

**Requirement S1: Full workflow partial state**

WHILE a full identity-verification workflow is in partial-input mode and an operation has not run, THE response SHALL retain the addressable result property with JSON `null`, and the integration assertion SHALL verify that contract rather than requiring the property to be absent or populated.

**Requirement S2: Independent generated outcomes**

WHILE the identity property generates success, failure, or unavailable adapter outcomes, THE property SHALL assert the status produced by each generated adapter and SHALL still verify one call per adapter and independent result instances.

**Requirement S3: Deterministic amortization state**

WHILE the amortization calculator is called twice with identical terms, THE deterministic property SHALL compare scalar result values and the installment sequence by value, not rely on reference equality for independently generated installment objects.

**Requirement S4: Provider-neutral input state**

WHILE the provider-neutral comparison input contract is inspected, THE property SHALL match the current public contract, including `ActorReference` when present, and SHALL reject any public member whose name exposes provider, secret, or token-specific data.

### Unwanted-event Requirements

**Requirement W1: Missing test key**

IF `FINNOVA_TEST_JWT_KEY` is absent or blank when a JWT-dependent integration test host starts, THEN the test host SHALL fail clearly with a prerequisite error; the repair SHALL NOT add a committed secret, use a hard-coded signing key, disable JWT validation, or remove authorization tests.

**Requirement W2: Invalid or unauthorized request**

IF an identity or application request is missing a token, has an invalid token, or has the wrong role, THEN the existing authentication and authorization pipeline SHALL continue to return its established 401 or 403 response; a multipart fixture correction SHALL NOT weaken those checks.

**Requirement W3: Invalid document request**

IF a document exceeds the configured size limit, has an unsupported media type, has an extension/MIME mismatch, or has an invalid signature, THEN validation SHALL stop before provider or persistence work and SHALL return the existing identity document error family.

**Requirement W4: Middleware property loss**

IF an identity-verification validation exception reaches `ExceptionHandlingMiddleware`, THEN the response SHALL contain the stable identity `code` and a non-empty correlation identifier. If an Application Processing not-found exception reaches Accounts middleware, it SHALL retain `ERR-APP-404` and HTTP 404.

**Requirement W5: Broad exception acceptance**

IF `EnsureResultDomain` throws a subtype of `InvalidOperationException`, THEN `InvalidRecordShapeIsRejected` SHALL fail rather than treating the subtype as an acceptable exact result.

### Optional-feature Requirements

No optional feature is introduced by this repair. The UI boundary verification in Assumption A9 is a verification activity, not an optional UI feature or a source change.

### Complex Requirements

**Requirement C1: Complete repair verification**

WHEN the implementer applies the dependency and source compatibility repairs, preserves the eight confirmed behavioral corrections, restores the exact exception assertion, and supplies the process-scoped JWT key, THE implementer SHALL verify the solution build, test-project build, focused affected tests, and complete `Finnova.Tests` suite in dependency order. Historical command results SHALL be recorded as historical reports; commands run by the implementer SHALL be identified as implementer verification.

**Requirement C2: Backend/frontend boundary**

WHERE the repair leaves API routes, response shapes, authentication behavior, and gateway routing unchanged, THE UI repository SHALL receive no source modification, and the implementer SHALL perform only the agreed UI build/contract boundary verification when that repository is accessible.

## Acceptance Criteria

1. The final repair contains exactly the two spec documents requested for this workflow under `.kiro/specs/resolve-build-and-unit-test-failures/`; no API or UI source file is modified by this workflow.
2. `Finnova.Tests/Unit/IdentityVerificationProperties.cs` no longer treats a derived `InvalidOperationException` as equivalent to the exact expected type. The property either uses the supported typed FsCheck assertion or explicitly checks `ex.GetType() == typeof(InvalidOperationException)`.
3. The implementation retains the current xUnit version alignment, ASP.NET Core framework reference, repository-owned application reference ports, exception constructor fixes, workflow-interface resolution, stale API/import corrections, FluentValidation compatibility correction, and corresponding test signature updates needed for compilation.
4. Multipart identity authorization fixtures use `MultipartFormDataContent` for multipart endpoints, while the controller still validates missing values through the existing identity validation path.
5. Identity middleware tests can observe the stable `code` and non-empty correlation identifier; application-processing unknown-ID integration tests receive HTTP 404 with `ERR-APP-404`; and no generic 500 mapping is used for that not-found case.
6. The full-workflow partial-input test checks an addressable result property whose JSON value is null, the amortization deterministic test compares values and installment contents, the generated identifier property checks generated statuses, and the provider-neutral reflection test matches the current neutral contract while excluding provider/secret/token fields.
7. The document validator rejects the `.png`/`application/pdf` mismatch and other extension/MIME/signature mismatches without invoking provider or persistence behavior.
8. With `FINNOVA_TEST_JWT_KEY` supplied process-scoped, the implementer reports a complete current `Finnova.Tests` run with zero failed and zero skipped tests. This outcome must be attributed to the implementer's command or to a cited historical report; this workflow itself runs no test.
9. The repair does not commit or persist any JWT key, weaken JWT validation, alter role policies, or remove unauthorized/invalid-token coverage.
10. If the UI repository is available, the implementer verifies that its unchanged source remains compatible with the unchanged backend contracts. No UI source change is required unless a concrete contract break is discovered, in which case the assumption must be revisited before scope changes.

## Out of Scope

- Running `dotnet build`, `dotnet test`, `dotnet restore`, npm commands, or any build/test/restore command in this workflow.
- Implementing a new API feature, changing database schema, changing API routes, changing gateway clusters, or changing frontend behavior.
- Refactoring unrelated JV-posting changes currently present in the working tree.
- Fixing historical nullable, preview, or other non-failing warnings unless they become current build errors.
- Adding localization, bilingual fields, Arabic or other non-English values, RTL support, Redux, RxJS, or a new UI service layer.
- Persisting any JWT test key in source, configuration, logs, documentation containing secrets, environment profiles, or Git history.
