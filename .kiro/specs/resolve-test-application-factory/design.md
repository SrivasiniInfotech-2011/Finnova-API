# Design Document — Resolve TestApplicationFactory

## Overview

The existing JvPosting API integration test references `TestApplicationFactory.Create()`, but the repository contains only `Finnova.Tests.Integration.SystemAdminAppFactory`. The design adds one thin static adapter in the test project and imports its namespace into `JvPostingApiTests`. The adapter creates the existing SystemAdmin factory, so the test boots the actual `Finnova.SystemAdminService` host without duplicating configuration.

The selected stack remains the existing .NET 10 test stack: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, ASP.NET Core `WebApplicationFactory`, EF Core InMemory, JWT Bearer authentication, and the existing SystemAdmin middleware pipeline. The production API and the React/TypeScript/Vite Finnova-UI application are unchanged. This is a backend test-host symbol repair, not a feature or API-contract change.

The primary proof remains the existing test: an unauthenticated `POST /api/jv-posting/requests` returns 401. The InMemory provider is used only to let the host compose without SQL Server. Authentication still uses the real JWT Bearer handler, and authorization still runs before the controller action.

## What is reused vs what is new

### Reused

- `Finnova.Tests/Integration/SystemAdminAppFactory.cs` remains the sole host configuration implementation.
- `SystemAdminAppFactory` continues to derive from `WebApplicationFactory<Finnova.SystemAdminService.Controllers.CourtController>`.
- Its `ConfigureWebHost` method continues to select the `Testing` environment, remove SQL Server EF registrations, add a uniquely named EF Core InMemory database, ensure the database exists, and seed its deterministic English-only test rows.
- `Finnova.SystemAdminService/Program.cs` remains the host entry point with its public `Program` shim, JWT Bearer registration, authorization policies, exception middleware, `UseAuthentication`, `UseAuthorization`, and controller mapping.
- `Finnova.SystemAdminService/Controllers/JvPostingController.cs` remains the owner of `POST /api/jv-posting/requests` with controller-level `[Authorize]`.
- The existing `Submission_requires_authentication` test, including its null request content and 401 assertion, remains unchanged apart from the namespace import needed to resolve the helper.
- Existing package and project references in `Finnova.Tests.csproj` are sufficient. No dependency-file change is designed.

### New

- `Finnova.Tests/Integration/TestApplicationFactory.cs` contains a public static `TestApplicationFactory` with one method:

  ```csharp
  public static SystemAdminAppFactory Create() => new();
  ```

  Returning `SystemAdminAppFactory` is intentional. It is already a `WebApplicationFactory` and already exposes `CreateClient()`, so the call site can retain `using var factory` and `factory.CreateClient()` without an additional wrapper lifetime or cast.
- `using Finnova.Tests.Integration;` is added to `Finnova.Tests/JVPosting/JvPostingApiTests.cs` so the test's existing unqualified `TestApplicationFactory` reference resolves to the new helper.

No entity, DTO, database model, migration, production service, controller, gateway route, or UI artifact is new.

## Requirements-coverage map

| Requirement | Design coverage |
| --- | --- |
| R1 | The new public static helper in `Finnova.Tests.Integration` exposes `Create()` and returns a disposable `SystemAdminAppFactory`. |
| R2 | `Create()` delegates directly to `new SystemAdminAppFactory()`; no host setup is copied. |
| R3 | Only test-project helper and namespace-import source changes are specified; production API, contracts, auth, middleware, and UI remain untouched. |
| R4 | The existing test call resolves to the helper, which constructs the existing WebApplicationFactory and supports `CreateClient()`. |
| R5 | The existing route call and `HttpStatusCode.Unauthorized` assertion are retained verbatim. |
| R6 | Delegation invokes `SystemAdminAppFactory.ConfigureWebHost`, which selects `Testing` and installs the existing InMemory provider. |
| R7 | The SystemAdmin host's existing middleware order remains `ExceptionHandlingMiddleware`, authentication, authorization, then endpoint mapping. |
| R8 | The test host does not depend on a live SQL Server because the reused factory removes SQL Server EF registrations and adds InMemory. |
| R9 | No test authentication handler, fake identity, policy override, or middleware change is added. |
| R10 | The task list names only the new helper and the required using change; unrelated JvPosting files are excluded. |
| R11 | The helper returns the same SystemAdmin factory that a future token-based test could reuse, without adding that optional test now. |
| R12 | The combined helper, namespace import, existing host setup, and unchanged 401 test resolve the symbol and preserve runtime behavior. |

## Test-host / data model / contracts

### Test-host composition

`TestApplicationFactory.Create()` returns `new SystemAdminAppFactory()`. The returned object is disposed by the existing `using var factory` statement in `JvPostingApiTests`. Calling `CreateClient()` uses the standard ASP.NET Core in-process test server provided by the inherited `WebApplicationFactory`.

The existing factory configures the host with `builder.UseEnvironment("Testing")`. During service configuration it removes the production `FinnovaDbContext` and EF provider registrations, then adds `FinnovaDbContext` with a unique EF Core InMemory database name. It calls `EnsureCreated()` and applies its deterministic seed. This startup arrangement is retained rather than reimplemented in `TestApplicationFactory`.

The SystemAdmin `Program.cs` public partial `Program` shim remains available for other factory-based tests, but the selected adapter continues to wrap `SystemAdminAppFactory`, whose existing controller marker already identifies the SystemAdmin host. No new host entry point is introduced.

### Data model

No data model changes are required. The test factory continues to use the existing `FinnovaDbContext` model and the existing InMemory database instance configured by `SystemAdminAppFactory`. The seed data is not part of the JvPosting 401 assertion, but keeping the existing seed makes the shared factory safe for other SystemAdmin integration tests.

The 401-only path requires no database schema change or migration. Authentication and authorization execute before the protected controller action, so an unauthenticated request is rejected before `JvPostingSubmissionService` or any database query is invoked. InMemory is still required because the host composes repository services during startup and must not attempt a live SQL Server connection while the test server starts.

### HTTP contract

The route remains:

- **Method:** `POST`
- **Path:** `/api/jv-posting/requests`
- **Owner:** `Finnova.SystemAdminService.Controllers.JvPostingController`
- **Protection:** controller-level `[Authorize]`
- **Unauthenticated result:** HTTP 401 Unauthorized

The request body, response contract, status codes for authenticated callers, and error payloads are not changed. The test sends `content: null`; because authentication and authorization precede endpoint execution, the unauthenticated call is rejected before request processing reaches the action. No API gateway alias is involved in this in-process host test, and no gateway change is needed.

## Component/interface descriptions

### `TestApplicationFactory`

- **Path:** `Finnova.Tests/Integration/TestApplicationFactory.cs`
- **Namespace:** `Finnova.Tests.Integration`
- **Kind:** public static class
- **Interface:** `public static SystemAdminAppFactory Create()`
- **Behavior:** returns a fresh `SystemAdminAppFactory` instance on every call.
- **Lifetime:** the caller owns and disposes the returned factory. The existing test's `using var` provides that lifetime.
- **Dependencies:** only the existing `SystemAdminAppFactory`; no service locator, production reference, configuration override, or test authentication service.

The helper is deliberately static because the call site already expects a static `Create()` method. It is deliberately not a subclass or second `WebApplicationFactory`, because either choice would duplicate or obscure the existing host setup.

### `SystemAdminAppFactory`

This existing class remains the only component responsible for test-host configuration. It owns environment selection, EF descriptor removal, InMemory registration, database creation, and seed data. `TestApplicationFactory` has no authority to alter those concerns.

### `JvPostingApiTests`

The test receives only a namespace import. Its request construction and assertion remain unchanged. The test therefore verifies both compile-time symbol resolution and the runtime authentication boundary without adding an alternate path.

## Error handling

- **Missing helper symbol:** Before the repair, compilation fails because `TestApplicationFactory` cannot be resolved. Adding the helper and the Integration namespace import fixes this compile-time failure. There is no runtime fallback for a missing type; this is a fatal compile error addressed by the two specified source changes.
- **Factory construction or host startup failure:** If SystemAdmin host composition fails because a required service, configuration value, or existing factory setup is invalid, `Create()` propagates the startup exception to the test. This is fatal for the test and is not hidden by the adapter. The existing test host logs according to the ASP.NET Core test host configuration; the adapter adds no logging or exception translation.
- **Unavailable SQL Server:** The existing factory handles this test-host condition by removing the SQL Server EF registrations and using InMemory. This is recoverable at the composition level and requires no production database connection. The adapter must not bypass or replace that configuration.
- **Missing or invalid authentication:** A request with no bearer token is rejected by the real JWT authentication/authorization pipeline with 401. This is an expected, recoverable HTTP result asserted by the test, not an exception for `ExceptionHandlingMiddleware` to translate.
- **Forbidden authenticated request:** An authenticated token lacking the required authorization claim or role would remain subject to the existing SystemAdmin policies and would receive the host's normal 403 behavior. The repair does not alter this path.
- **Controller or service failure after authorization:** Such a failure is outside the 401 scenario. If reached by a separately authenticated test, the existing SystemAdmin exception middleware retains responsibility for its configured exception mappings. `TestApplicationFactory` does not catch, rewrite, or mask controller exceptions.
- **Unexpected test failure:** xUnit reports the failure to the caller. No retry, swallowed exception, or fallback factory is introduced.

## Security

The security boundary is preserved exactly. `TestApplicationFactory` does not register a fake authentication scheme, inject a principal, disable authorization, replace JWT validation, or reorder middleware. The real SystemAdmin host still runs JWT Bearer authentication followed by authorization. With no bearer token, the protected `JvPostingController` endpoint returns 401 and the controller action does not execute.

The EF Core InMemory provider changes persistence infrastructure only for the test host. It does not change authentication, token validation, authorization policies, claims, or the route's access decision. No credentials, signing keys, or tokens are added to the helper. The existing test remains an unauthenticated test and must not be made to pass by supplying an identity.

Finnova's product constraints remain unchanged: this backend test-host repair adds no user-facing values, no bilingual or non-English content, and no RTL handling. The Finnova-UI application is not modified.

## Design decisions/tradeoffs

### Option A — thin static adapter, selected

Add `Finnova.Tests.Integration.TestApplicationFactory` with `Create()` returning `new SystemAdminAppFactory()`, and import the Integration namespace in the test. This is the selected design because it resolves the missing symbol while keeping the current test call and behavior intact. It adds one small, clearly named test helper and delegates all host behavior to the existing factory.

### Option B — modify the test to instantiate `SystemAdminAppFactory`, not selected

Change the test to use `new SystemAdminAppFactory()` directly. This would add no new type, but it would rewrite the test's established factory abstraction and require a test-file change beyond a namespace import. It also leaves any other tests or future callers that expect `TestApplicationFactory.Create()` without a compatible symbol. Option B is technically viable but has a larger behavioral surface for the requested repair.

### Reuse instead of a parallel factory

The existing SystemAdmin factory already solves the difficult host-startup problem, including EF provider replacement and deterministic seed setup. Reimplementing that setup in a second factory risks provider conflicts, divergent middleware/configuration behavior, and duplicated maintenance. A one-line delegation keeps one source of truth.

### No reverse project reference

The helper belongs in `Finnova.Tests`, which already references `Finnova.SystemAdminService`. Adding a reference from a production project back to the test project would invert the project dependency graph and contaminate production composition. No such reference is needed.

### No database migration or schema change

The test's assertion is about authentication. It is rejected before the controller accesses data, and the existing test factory's InMemory model is enough for host startup. A migration would add risk and cost without contributing to the 401 proof.

### No extra authenticated test in the core repair

An authenticated-path test could verify service execution, but it would require token construction and would broaden the scope toward JvPosting service behavior. The existing unauthenticated test directly proves the requested factory wiring and security boundary, so the authenticated path remains optional follow-up work.

## Implementation-ordered task list

1. **Create the compatibility helper.** Create `Finnova.Tests/Integration/TestApplicationFactory.cs` with the complete static adapter shown in `tasks.md`. The method must return a fresh `SystemAdminAppFactory` and must not duplicate host configuration.
2. **Resolve the test namespace.** Modify `Finnova.Tests/JVPosting/JvPostingApiTests.cs` by adding `using Finnova.Tests.Integration;` with the existing using directives. Keep the test body, endpoint, null content, and 401 assertion unchanged.
3. **User-run validation after applying the tasks.** The user may compile the test project and run the existing test suite to verify the symbol and runtime 401 behavior. This authoring pass ran no build or test command and claims no build or test result.
