# JvPostingSubmissionService Investigation Report

## Summary answer

The reported Repository/Service reference is a confirmed source-level dependency-direction error. `Finnova.Repository/DependencyInjection.cs:61` registers `JvPostingSubmissionService`, but the concrete type is declared in `Finnova.Service/JVPosting/JvPostingSubmissionService.cs:14`. `Finnova.Repository/Finnova.Repository.csproj` references only `Finnova.Models`, while `Finnova.Service/Finnova.Service.csproj:25` references `Finnova.Repository`. Therefore the Repository project cannot resolve the service type. Adding a `Finnova.Service` reference to Repository would create the reverse of the existing dependency and a circular project dependency, so it is not a valid repair.

The service is already registered in the correct host-owned location at `Finnova.SystemAdminService/Program.cs:69`, and the submission controller consumes it at `Finnova.SystemAdminService/Controllers/JvPostingController.cs:11`. The Repository registration should be removed in a later implementation change; the SystemAdmin registration should remain. No source, spec, or configuration file was edited during this investigation.

A second confirmed test-build issue is present: `Finnova.Tests/JVPosting/JvPostingApiTests.cs:11` calls `TestApplicationFactory.Create()`, but repository-wide source search found no declaration of `TestApplicationFactory`. The existing integration factory is `Finnova.Tests/Integration/SystemAdminAppFactory.cs`, which exposes a `WebApplicationFactory` subclass but does not define that symbol. This is a likely test compilation error independent of the Repository registration.

## Evidence

### 1. Project dependency direction

- `Finnova.Repository/Finnova.Repository.csproj` has one project reference, to `..\\Finnova.Models\\Finnova.Models.csproj`; it has no reference to `Finnova.Service`.
- `Finnova.Service/Finnova.Service.csproj:23-25` references both `Finnova.Models` and `Finnova.Repository`.
- `Finnova.SystemAdminService/Finnova.SystemAdminService.csproj:23-27` references `Finnova.Service`, `Finnova.Models`, and `Finnova.Repository`.
- `Finnova.Backend.slnx` includes all of these projects, so the invalid Repository source is part of the solution compilation graph.

The Repository DI file imports `Finnova.Repository.Context`, `Finnova.Repository.Interfaces`, `Finnova.Repository.Repositories`, EF Core, and `Microsoft.Extensions.DependencyInjection`, but not `Finnova.Service.JVPosting`. Its final registration is:

```csharp
services.AddScoped<ILookupReferencePort, LookupReferenceAdapter>();
services.AddScoped<JvPostingSubmissionService>();
```

The referenced symbol is not declared in the Repository project. It is declared as:

```csharp
namespace Finnova.Service.JVPosting;

public sealed class JvPostingSubmissionService(FinnovaDbContext db)
```

in `Finnova.Service/JVPosting/JvPostingSubmissionService.cs:11-14`.

This confirms the source cause. The precise compiler diagnostic was not captured because build commands were prohibited; the likely diagnostic is an unresolved type/name error for `JvPostingSubmissionService`. Adding a project reference from Repository to Service would invert the existing `Service -> Repository` edge and create an invalid circular dependency, not fix the architecture.

### 2. Correct host registration and consumers

`Finnova.SystemAdminService/Program.cs:69` already contains:

```csharp
builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingSubmissionService>();
```

`Finnova.SystemAdminService/Program.cs:114` separately registers `JvPostingOperationsService`. The SystemAdmin host references the Service project, so these are resolvable from that host.

`Finnova.SystemAdminService/Controllers/JvPostingController.cs:10-18` imports `Finnova.Service.JVPosting`, injects the concrete `JvPostingSubmissionService`, and calls `SubmitAsync` from `POST /api/jv-posting/requests`.

`Finnova.SystemAdminService/Controllers/JvPostingOperationsController.cs:10-18` injects `JvPostingOperationsService` for queue and detail queries. No interface named `IJvPostingSubmissionService`, `IJvPostingOperationsService`, or another JV submission-service abstraction was found. The current design uses concrete service injection, which is not itself a compile error but leaves the service without an interface boundary for isolated unit tests.

`Finnova.Repository/DependencyInjection.cs` is called by the UA, Accounts, and SystemAdmin hosts. A service registration in this extension would therefore be owned by the wrong layer and, if it were made compilable through a reverse reference, would duplicate the SystemAdmin registration. The host registration is the appropriate current location.

### 3. Test reference and missing factory

`Finnova.Tests/JVPosting/JvPostingApiTests.cs:8-16` contains only one test:

```csharp
using var factory = TestApplicationFactory.Create();
using var client = factory.CreateClient();
var response = await client.PostAsync("/api/jv-posting/requests", content: null);
Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
```

A repository-wide search found this call site but no `TestApplicationFactory` class, method, or file. The available host factory is `Finnova.Tests/Integration/SystemAdminAppFactory.cs`, declared as:

```csharp
public class SystemAdminAppFactory
    : WebApplicationFactory<Finnova.SystemAdminService.Controllers.CourtController>
```

It does not expose a static `Create()` method. `Finnova.Tests/Finnova.Tests.csproj` references the SystemAdmin host, Service, Repository, and Models projects and includes the MVC testing package, so a factory-based test is supported, but the current JvPosting test points at an absent helper.

The only current JvPosting test covers unauthenticated access. No unit or integration test was found for successful submission, duplicate-equivalent submission, conflicting request reuse, person/automation actor validation, or persistence of `JvPostingInbox`.

### 4. EF model and persistence references

`Finnova.Repository/Context/FinnovaDbContext.cs:72` exposes:

```csharp
public DbSet<JvPostingInbox> JvPostingInboxes => Set<JvPostingInbox>();
```

The entity is declared in `Finnova.Models/Domain/JvPostingInbox.cs:5-41`, and the status enum is in `Finnova.Models/Domain/Enums/JvPostingStatus.cs:3-12`. The submission service queries and inserts the DbSet directly at `Finnova.Service/JVPosting/JvPostingSubmissionService.cs:25` and `:32`.

Search found no `IEntityTypeConfiguration<JvPostingInbox>`, explicit `Entity<JvPostingInbox>` mapping, `JvPostingInboxConfiguration`, or migration/snapshot entry for this entity. `FinnovaDbContext` calls `ApplyConfigurationsFromAssembly`, so a missing configuration does not prevent convention-based model discovery, but there is no checked-in migration for creating the inbox table. Whether the worker's separate SQL schema is intended to create this table is not established by the current .NET source. This is a persistence/design gap that must be resolved before relying on the submission endpoint against a migrated database.

### 5. Service behavior and design risks

The service implementation at `Finnova.Service/JVPosting/JvPostingSubmissionService.cs` has the following confirmed behaviors:

- It computes the hash at line 23 from request ID, company ID, branch ID, source system, and serialized `request.Data`.
- It checks an existing request by `RequestId` at line 25 and returns it as equivalent when the hash, company ID, and branch ID match.
- It inserts a `JvPostingInbox` with status `Ready` at lines 32-54.
- It uses `DateTime.UtcNow` directly at lines 52-53 rather than an injected clock.
- It validates required fields and initiation mode at lines 60-67.
- For person mode, it requires `InitiatingUserId` and compares it to `ClaimTypes.NameIdentifier` using `Guid.Parse` at line 67.

The following design problems are confirmed by comparison with the declared symbols and behavior:

1. **Caller company/branch scope is not enforced.** The service checks that a person actor matches the name-identifier claim, but it does not query or invoke any company/branch authorization or scope abstraction. The current source therefore does not implement the stated requirement to enforce caller scope.
2. **The idempotency hash does not cover the full immutable envelope.** It omits event type, rule key, schema version, dates, currency, description, initiation mode, initiating user, automation context, and other immutable metadata. Two submissions with the same request ID and the same `Data`/company/branch/source values but different omitted fields can be treated as equivalent.
3. **Malformed actor claims can become an unexpected server error.** `Guid.Parse` is used without `TryParse`; an absent or malformed claim can throw `FormatException`. `[Authorize]` does not itself require a valid `NameIdentifier` claim. The SystemAdmin middleware has no JVE-specific exception branch.
4. **Submission validation and conflict exceptions have no typed API mapping.** The service throws `ArgumentException`, `UnauthorizedAccessException`, and `InvalidOperationException` with JVE message codes. `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` contains no JVE-specific exception types or mappings, so these paths fall through to the generic 500 response instead of a stable 400/403/409 contract.
5. **The service is tightly coupled to EF Core.** It receives `FinnovaDbContext` directly and has no repository or service interface. This is compatible with the current compile graph once registered in the host, but it makes unit testing persistence and idempotency behavior harder and differs from the surrounding repository-interface pattern.
6. **Operations portability is unverified.** `JvPostingOperationsService.cs:14` uses `EF.Functions.DateDiffSecond`, which is SQL Server-specific. The existing `SystemAdminAppFactory` replaces SQL Server with EF Core InMemory. If operations endpoints are tested through that factory, this query may not translate. No such runtime test currently exists.
7. **Recovery endpoints are compile-valid but intentionally unfinished.** `JvPostingOperationsController.cs:20-30` throws `NotImplementedException` for lease recovery, reprocess, and cancel. This is separate from the submission registration error but means those routes are not operational.

These are source-inspection conclusions. No runtime behavior was executed.

### 6. Gateway and authorization wiring

`Finnova.ApiGateway/appsettings.json:322-325` has a `jve-operations-alias-route` matching `/api/systemadmin/jv-posting/{**catch-all}`, targeting `systemadmin-cluster`, and removing `/api/systemadmin`. The catch-all covers both the submission path and operations path and rewrites the gateway request to the SystemAdmin controller routes. No separate gateway route is needed solely to resolve the Repository compile issue.

The submission controller has `[Authorize]` without a named policy. The operations controller uses `JveOperationsRead` and `JveOperationsRecovery`, both registered in `Finnova.SystemAdminService/Program.cs:96-97`. This means authentication is required for submission, but no more specific submission permission or company/branch scope is enforced by the current controller/service code.

## Confirmed findings versus assumptions

### Confirmed from source inspection

- `Finnova.Repository/DependencyInjection.cs:61` references a service-layer concrete type that Repository does not reference or import.
- `Finnova.Service` already references `Finnova.Repository`, making a reverse Repository-to-Service reference architecturally invalid and circular.
- The SystemAdmin host already registers `JvPostingSubmissionService` at `Program.cs:69` and consumes it from `JvPostingController`.
- `JvPostingApiTests.cs` references an undeclared `TestApplicationFactory.Create()` symbol; no declaration was found in the repository.
- `JvPostingInbox` has a DbSet and direct service usage, but no explicit EF configuration or checked-in migration entry was found.
- The submission service does not enforce company/branch scope, hashes only a partial envelope, uses direct wall-clock time, and throws untyped exceptions without JVE middleware mappings.
- The working tree is dirty and contains unrelated/current JV-posting work: modified `Finnova.ApiGateway/appsettings.json`, `Finnova.Repository/Context/FinnovaDbContext.cs`, `Finnova.Repository/DependencyInjection.cs`, and `Finnova.SystemAdminService/Program.cs`; plus untracked JV-posting models, service files, controllers, tests, and the existing `.kiro/specs/resolve-build-and-unit-test-failures/` directory. None of these files were changed by this investigation.

### Assumptions requiring confirmation

- The intended fix is to remove the `JvPostingSubmissionService` registration from `Finnova.Repository/DependencyInjection.cs` and retain registration in `Finnova.SystemAdminService/Program.cs`, rather than introducing a new cross-layer abstraction.
- The existing `SystemAdminAppFactory` should be reused or a correctly named JV-specific factory should be added for the API test. The preferred factory and test-host database setup need confirmation before implementation.
- The worker SQL schema described in `.kiro/specs/jv-posting-engine` is intended to be coordinated with the EF `JvPostingInbox` model. If the Python worker owns the schema exclusively, the .NET migration/configuration plan must instead document that ownership boundary.
- The intended source contract requires full-envelope idempotency and a formal company/branch authorization port, as the existing JV posting spec states. The current implementation does not yet provide either.
- The API should expose stable JVE error codes/statuses through the SystemAdmin middleware. The current source has no typed JVE exception contract, so exact status/code values remain to be confirmed before implementation.
- The current feature is backend-only. No Finnova-UI source or frontend repository was inspected or changed for this investigation.

## Conclusions

The immediate reported build problem is confirmed and localized: Repository DI owns a registration for a type implemented by Service. The proper ownership is the SystemAdmin host, which already has the required Service project reference and registration. The Repository extension should remain responsible for `FinnovaDbContext` and repository/adapter registrations only.

The current JV posting test surface also has a separate confirmed compile issue because its factory symbol is absent. After correcting the registration ownership, the test must be pointed at an existing or newly defined `WebApplicationFactory` without changing production authorization behavior.

The source review also finds design problems beyond the registration: incomplete scope authorization, incomplete idempotency material, generic error handling for expected submission failures, no explicit database migration/configuration evidence, and no service abstraction for unit isolation. These should be resolved or explicitly deferred in the implementation spec rather than silently treated as build-only cleanup.

## Documents-only repair plan for later implementation

1. **Correct dependency ownership.** Modify only `Finnova.Repository/DependencyInjection.cs` to remove the `services.AddScoped<JvPostingSubmissionService>();` line. Do not add `using Finnova.Service...` or a Repository-to-Service project reference. Keep `Finnova.SystemAdminService/Program.cs:69` as the concrete service registration. This is the smallest repair that restores the existing dependency direction.
2. **Repair the test host reference.** Modify `Finnova.Tests/JVPosting/JvPostingApiTests.cs` to use the existing `SystemAdminAppFactory` or create a clearly named JV posting factory in `Finnova.Tests/Integration` that follows the existing `WebApplicationFactory`/EF InMemory pattern. The test must continue to assert 401 for an unauthenticated request and must not bypass authentication. Add focused authenticated submission tests only after the host factory can resolve the SystemAdmin pipeline.
3. **Add submission service unit coverage.** Add tests for required-field rejection, valid person actor, person actor mismatch, automation context requirements, equivalent duplicate request, conflicting duplicate request, and Ready persistence. Use an explicit repository or EF test boundary selected by the implementation design; do not rely on an absent factory or external SQL Server.
4. **Confirm persistence ownership before changing schema.** Decide whether EF migrations or the Python worker SQL migration owns the `JvPostingInbox` table. If .NET owns it, add an explicit EF configuration, constraints/indexes, and a forward migration in the repository’s established migration location. If the worker owns it, document the shared-table contract and ensure the .NET model maps exactly to that schema without creating a competing migration.
5. **Define the service authorization boundary.** Add or reuse a repository/service port for company and branch scope checks, then invoke it from `JvPostingSubmissionService` before persistence. Replace unsafe claim parsing with a typed validation/authorization outcome. Keep actor context immutable and ensure person mode cannot submit for another user.
6. **Make idempotency cover the approved immutable envelope.** Define the canonical serialization/hash input in the contract/design, then include every immutable field required to distinguish requests. Enforce the request-id conflict atomically at the persistence boundary, preserving the first request and returning an equivalent response only for a genuinely equivalent payload/context.
7. **Define typed JVE error handling.** Add the approved JVE exception contract in the existing domain exception location, map validation/conflict/authorization failures in `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`, and add tests for the stable HTTP status and error code. Do not expose raw database or payload details.
8. **Review operations separately.** Before testing `JvPostingOperationsService`, decide whether SQL Server-specific `DateDiffSecond` is acceptable for the production-only query or whether a provider-neutral age calculation is required for the existing InMemory test host. Keep recovery endpoint implementation out of the immediate registration fix unless explicitly included in scope.
9. **Verify only after implementation.** The later implementation workflow may run the project’s documented build and test commands after all source changes are applied. This investigation did not run `dotnet build`, `dotnet test`, restore, migration, or any server command, so current compilation and test-pass status remain unverified.

## Verification not performed

Per the read-only investigation instructions, the following were not run:

- `dotnet build`
- `dotnet test`
- `dotnet restore`
- EF migration commands or database access
- npm/frontend commands
- any server restart or runtime HTTP request

The report is based on source inspection, project-file inspection, repository-wide text search, and `git status`/targeted `git diff` inspection only.
