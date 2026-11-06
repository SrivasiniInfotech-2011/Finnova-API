# Requirements Document

## Introduction

This specification resolves the backend test-host compilation error caused by `Finnova.Tests/JVPosting/JvPostingApiTests.cs` calling `TestApplicationFactory.Create()` when no such symbol exists in the repository. The repair covers only the missing test factory and the namespace import required to resolve it. It does not change the JvPosting API, the production `JvPostingSubmissionService` registration, the authentication model, or the existing test assertion.

The test must continue to boot the existing `Finnova.SystemAdminService` in process through `SystemAdminAppFactory`. The existing factory replaces SQL Server with EF Core InMemory for test startup, while the real SystemAdmin JWT Bearer authentication and authorization middleware remains active. The primary behavior under test is that an unauthenticated `POST /api/jv-posting/requests` receives HTTP 401 Unauthorized before the controller or database-dependent service executes.

This is a backend test-host compilation issue. The Finnova-UI repository (`E:\Finnova\Finnova-UI\Finnova-UI`) is outside the change boundary because the route and its 401 contract do not change. Finnova remains India-only and English-only; this repair introduces no display data, bilingual fields, non-English content, or RTL behavior.

## Glossary

- **TestApplicationFactory**: The new static test helper exposing the `Create()` method expected by `JvPostingApiTests`.
- **SystemAdminAppFactory**: The existing `WebApplicationFactory` implementation in `Finnova.Tests/Integration/SystemAdminAppFactory.cs` that boots the SystemAdmin host with EF Core InMemory.
- **SystemAdmin host**: The `Finnova.SystemAdminService` ASP.NET Core host that owns `JvPostingController` and the `/api/jv-posting/requests` route.
- **WebApplicationFactory**: The ASP.NET Core test-host facility used to run an application in process and create an `HttpClient`.
- **JvPosting route**: `POST /api/jv-posting/requests`, declared by `JvPostingController` and protected by `[Authorize]`.
- **Real authentication pipeline**: The SystemAdmin host's configured JWT Bearer handler followed by authorization middleware, without a test authentication handler or authentication bypass.
- **InMemory EF provider**: The EF Core InMemory database provider already used by `SystemAdminAppFactory` to avoid a SQL Server dependency during test-host startup.
- **401 contract**: An unauthenticated request to the protected JvPosting route returns `HttpStatusCode.Unauthorized`.

## Dependencies/Assumptions

### Confirmed dependencies and findings

- `JvPostingApiTests.cs` contains the `Submission_requires_authentication` test, calls `TestApplicationFactory.Create()`, creates a client, posts null content to `/api/jv-posting/requests`, and asserts 401.
- `SystemAdminAppFactory` already derives from `WebApplicationFactory<Finnova.SystemAdminService.Controllers.CourtController>`, configures the `Testing` environment, replaces EF registrations with a uniquely named InMemory database, creates the database, and seeds deterministic English-only test data.
- `Finnova.SystemAdminService/Program.cs` exposes `public partial class Program { }`, configures JWT Bearer authentication and authorization, registers `JvPostingSubmissionService`, and orders exception middleware before authentication and authorization.
- `JvPostingController` owns `POST /api/jv-posting/requests` and applies `[Authorize]` at controller level.
- `Finnova.Tests.csproj` already references the SystemAdmin service, `Microsoft.AspNetCore.Mvc.Testing`, and `Microsoft.EntityFrameworkCore.InMemory`; no package or project-reference change is required.
- No `TestApplicationFactory` type exists elsewhere in the repository.

### Assumptions for confirmation

- **A1 — Preferred repair:** Add a thin static `Finnova.Tests.Integration.TestApplicationFactory` whose `Create()` method returns a new `SystemAdminAppFactory`; the existing JvPosting test remains behaviorally unchanged.
- **A2 — Namespace import:** Add `using Finnova.Tests.Integration;` to `JvPostingApiTests.cs` because the test currently has no import for the Integration namespace.
- **A3 — InMemory startup:** The existing EF Core InMemory replacement is acceptable and required for this 401-only test host to start without SQL Server. It does not weaken authentication because JWT Bearer processing is independent of the EF provider.
- **A4 — Authentication proof:** The existing 401 assertion is the required primary proof. No authenticated-path test is needed to resolve the missing symbol; an authenticated-path test remains optional follow-up work.
- **A5 — No factory duplication:** `TestApplicationFactory` delegates to `SystemAdminAppFactory` and does not duplicate its host configuration, seed logic, or EF replacement.
- **A6 — No reverse reference:** No reference from `Finnova.SystemAdminService` or another production project back to `Finnova.Tests` is permitted. The helper remains in the test project.
- **A7 — Working-tree ownership:** Existing modified or untracked JvPosting-related files belong to other work. The implementation must not overwrite, reset, delete, or otherwise touch them; only the explicitly listed test helper and test using are in scope.
- **A8 — No contract change:** The JvPosting route, request/response contracts, authorization behavior, and UI-facing API contract remain unchanged. No Finnova-UI file needs modification.
- **A9 — Validation ownership:** The user will run the build and test validation after applying the tasks. This authoring pass will not run build, test, restore, package-manager, database, or server commands.

## Requirements

### Ubiquitous

**R1.** THE test project SHALL provide a resolvable `Finnova.Tests.Integration.TestApplicationFactory.Create()` method that returns a disposable SystemAdmin `WebApplicationFactory` instance compatible with `factory.CreateClient()`.

**R2.** THE repair SHALL reuse the existing `SystemAdminAppFactory` implementation rather than duplicate its host configuration, service registrations, EF replacement, or seed data.

**R3.** THE repair SHALL leave the production API, JvPosting contracts, JWT configuration, authorization policy, middleware order, and Finnova-UI code unchanged.

### Event-driven

**R4.** WHEN `JvPostingApiTests.Submission_requires_authentication` calls `TestApplicationFactory.Create()`, THE test host SHALL boot the existing `Finnova.SystemAdminService` through `SystemAdminAppFactory` and SHALL allow the test to create an `HttpClient`.

**R5.** WHEN the test client sends an unauthenticated `POST /api/jv-posting/requests` with null content, THE SystemAdmin host SHALL return HTTP 401 Unauthorized, preserving the existing test assertion.

### State-driven

**R6.** WHILE the factory configures the test host, THE host SHALL use the existing `Testing` environment and EF Core InMemory database replacement so startup does not require a live SQL Server instance.

**R7.** WHILE the request passes through the host, authentication SHALL execute before authorization and authorization SHALL execute before the controller action; the request SHALL not reach `JvPostingSubmissionService` for an unauthenticated caller.

### Unwanted-event

**R8.** IF the source tree contains no production database or SQL Server available to the test host, THEN the factory SHALL still start using the existing InMemory configuration and SHALL not fail solely because SQL Server is unavailable.

**R9.** IF the unauthenticated JvPosting request is processed, THEN the repair SHALL not add a test authentication handler, synthetic identity, authorization bypass, or other change that could turn the expected 401 into an authenticated response.

**R10.** IF unrelated modified or untracked JvPosting files exist in the working tree, THEN the repair SHALL not overwrite, reset, delete, or modify those files.

### Optional-feature

**R11.** WHERE a future test needs to exercise an authenticated JvPosting path, it MAY reuse the same SystemAdmin factory and add a separately scoped token-based test; such a test is optional and is not required for this repair.

### Complex

**R12.** WHEN the test project compiles and executes the existing integration test, THE solution SHALL resolve the missing factory symbol, boot the configured SystemAdmin host without a production database, preserve the real JWT Bearer and authorization pipeline, and produce 401 for the unauthenticated route request without changing the route contract.

### Acceptance criteria

1. `Finnova.Tests/Integration/TestApplicationFactory.cs` defines a public static `TestApplicationFactory` in namespace `Finnova.Tests.Integration`.
2. `TestApplicationFactory.Create()` returns a new `SystemAdminAppFactory` instance, and the returned type supports the existing `using var factory` and `factory.CreateClient()` calls.
3. `JvPostingApiTests.cs` imports `Finnova.Tests.Integration` without changing the test method's request or 401 assertion.
4. No package reference, project reference, reverse production-to-test reference, or duplicate host factory configuration is added.
5. The factory path reaches the existing `SystemAdminAppFactory.ConfigureWebHost`, including `Testing` environment selection and EF Core InMemory replacement.
6. The unauthenticated request continues to target exactly `POST /api/jv-posting/requests` and continues to assert `HttpStatusCode.Unauthorized`.
7. The design and tasks explicitly preserve the real JWT Bearer authentication and authorization pipeline and do not introduce a test authentication bypass.
8. No source file under `Finnova-UI` is changed, and no API gateway or API contract change is specified.
9. The implementation does not touch unrelated modified or untracked JvPosting files.
10. User-run build and test validation may be performed after applying the tasks; this specification claims no build or test result.

## Out of Scope

- Fixing or refactoring `JvPostingSubmissionService`, its repository registrations, or unrelated DI failures except for relying on the already registered service during host composition.
- Changing `JvPostingController`, its route, `[Authorize]` attribute, request model, response model, or production service registration.
- Adding a test authentication handler, bypassing JWT validation, weakening authorization, or changing middleware order.
- Adding database migrations, changing the production schema, or replacing the existing test factory's seed data.
- Adding an authenticated-path test unless separately requested.
- Changing `Finnova-UI`, API gateway routing, localization, bilingual fields, non-English seed data, or RTL handling.
- Running build, test, restore, npm, database, server, or migration commands while authoring this spec.
