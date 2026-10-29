# Design Document — Resolve Build Errors and Separate Repository/Service DI

## Overview

This change consolidates the existing application registrations into two explicit library-owned composition methods and makes each web host compose them in a fixed order. `Finnova.Repository.DependencyInjection.AddFinnovaRepository` remains the infrastructure boundary for EF Core and repositories. A new `Finnova.Service.DependencyInjection.AddFinnovaServices` becomes the application boundary for MediatR, validators, pipeline behavior, service implementations, service ports, JWT options consumed by `TokenService`, and blob-storage registration. `Finnova.UAService`, `Finnova.AccountsService`, and `Finnova.SystemAdminService` call both methods from their existing `Program.cs` files and retain host-only concerns.

The design is based on static inspection. The solution file contains the three API hosts, `Finnova.Models`, `Finnova.Repository`, `Finnova.Service`, and tests. All three API hosts already reference both `Finnova.Service` and `Finnova.Repository`, so no host project reference is needed. The repository project references only `Finnova.Models`; the service project references `Finnova.Models` and `Finnova.Repository`, which is the existing direction required by service handlers. Existing tracked evidence in `.agents/tasks/task-screen-permissions/features/FEAT-001.json` and `.agents/tasks/lob-program-fk/verification.md` reports a prior successful build with one pre-existing `CS8604` warning in `Finnova.SystemAdminService/Program.cs`. Static inspection found no tracked compiler error. The tasks therefore fix the confirmed nullable warning and the duplicated/incomplete registration shape without claiming that a build has run.

The implementation language and stack stay fixed: C# on the existing .NET target framework, ASP.NET Core top-level `Program.cs`, MediatR 12, FluentValidation 11, EF Core 10, SQL Server, and the existing test stack. No frontend code is required.

## Confirmed findings and assumptions

### Confirmed by static inspection

- `Finnova.Repository/DependencyInjection.cs` already registers `FinnovaDbContext` and repository interfaces/implementations through `AddFinnovaRepository`.
- The same repository extension contains repository-local reference resolvers and address/reference adapters. They remain repository-owned because they reference repository interfaces and do not reference `Finnova.Service`.
- Each host manually repeats part of the service registration. UA registers MediatR, validators, token service, asset generator, blob storage, and password policy. Accounts registers those items plus the validation pipeline and GL stubs. SystemAdmin registers those items plus the validation pipeline, customer evaluator, document ports, password policy, and asset generator, and it calls MediatR scanning twice.
- The three hosts use the same service assembly even though they choose different marker types. A single assembly scan is sufficient.
- UA and SystemAdmin pass the nullable result of `GetConnectionString("FinnovaConnection")` directly to the non-null `AddFinnovaRepository` parameter. Accounts suppresses the warning with `!` but does not validate the setting. The tracked diagnostics identify the SystemAdmin call as `CS8604` in prior build evidence.
- `AddBlobStorage` is already a service-project extension and has provider selection plus singleton registration. It is reused rather than rewritten.

### Assumptions for confirmation

- All three API hosts should use one common service extension, including registrations that are currently used only by one host. Unused registrations are lazy and reduce drift between composition roots.
- `AddFinnovaServices` should bind `JwtSettings` because `TokenService` is a service-layer implementation. JWT bearer authentication itself stays in each host because issuer/audience/signing-key validation is host middleware configuration.
- The existing development GL and document stubs remain the default registrations. A later host-specific registration may replace a stub.
- A missing connection string is a startup configuration error, not a recoverable request error. The hosts fail with `InvalidOperationException`; the repository extension rejects blank values with `ArgumentException`.
- No HTTP or frontend contract is implied by this architecture task.

## What is reused and what is new

### Reused without behavioral change

| Existing asset | Use in this design |
| --- | --- |
| `Finnova.Repository.DependencyInjection.AddFinnovaRepository` | Registers `FinnovaDbContext`, SQL Server, repository implementations, and repository-local adapters. It gains only a whitespace guard. |
| `Finnova.Service.Storage.DependencyInjection.AddBlobStorage` | Called by the new service extension. Provider selection and singleton lifetime are unchanged. |
| `Finnova.Service` handler and validator assembly | Scanned once by MediatR and FluentValidation. |
| `ValidationBehavior<,>` | Registered as the existing transient pipeline behavior. |
| `TokenService`, `DefaultPasswordPolicy`, `AssetCodeGenerator` | Registered with the current scoped lifetimes. |
| GL and document port interfaces and development implementations | Registered with the current scoped lifetimes. |
| Host JWT, authorization, middleware, controller, health, OpenAPI, and CORS setup | Stays in the web hosts. |
| `Finnova.ApiGateway` and `Finnova-UI` | No changes. Existing paths and wire contracts remain valid. |

### New or modified assets

- **Create** `Finnova.Service/DependencyInjection.cs` with `AddFinnovaServices(IServiceCollection, IConfiguration)`.
- **Modify** `Finnova.Service/Finnova.Service.csproj` to reference `FluentValidation.DependencyInjectionExtensions`, because the registration extension now compiles the validator scan inside the service project.
- **Modify** `Finnova.Repository/DependencyInjection.cs` to reject a null, empty, or whitespace connection string at the repository boundary.
- **Modify** the three host `Program.cs` files to import `Finnova.Service`, call both extensions, remove duplicated application registrations, and validate `FinnovaConnection` before calling the repository extension.
- **Create** a backend DI descriptor test under `Finnova.Tests/Unit` to check that the two registration families expose the intended service types and lifetimes without requiring a live SQL Server.

No entity, contract, EF configuration, migration, controller, gateway, or frontend file is new for this feature.

## Requirements coverage map

| Requirement | Design coverage |
| --- | --- |
| R1 Repository boundary | Existing `Finnova.Repository.DependencyInjection` remains the only repository registration entry point; its new guard has no service dependency. |
| R2 Service entry point | `Finnova.Service/DependencyInjection.cs` owns the full service registration list and calls `AddBlobStorage`. |
| R3 Host composition | UA, Accounts, and SystemAdmin each call repository then service registration once. |
| R4 Nullable configuration diagnostic | Each host uses a null-coalescing startup guard; repository DI rejects blank values. No `!` suppression is used. |
| R5 Duplicate/omitted application registration | Host blocks remove manual MediatR, validator, pipeline, and moved implementation registrations. The service extension scans the assembly once. |
| R6 Stable runtime/wire behavior | Host authentication, authorization, middleware, controllers, routes, contracts, and gateway are unchanged. |
| R7 Optional replacement point | Stub registrations are ordinary scoped descriptors and can be overridden later by a host-specific registration. |
| NFR buildability and layering | Service DI code is compiled in `Finnova.Service`; repository DI contains no service reference; a descriptor test checks the boundary. |
| Frontend compatibility | No UI change; no URL or JSON shape changes. |

## Architecture and call order

The composition order is deliberately consistent across all three hosts:

1. Add controllers and host framework services.
2. Call `AddFinnovaRepository(connectionString)` once. This registers the scoped EF context and repository family.
3. Call `AddFinnovaServices(builder.Configuration)` once. This registers the service family, scans the service assembly, binds service options, and registers storage.
4. Configure host-owned JWT authentication and authorization.
5. Configure host middleware, endpoint mapping, health checks, and OpenAPI as before.

The service extension must not configure `AddAuthentication`, `AddAuthorization`, or middleware. Those APIs need host policy decisions and remain in the hosts. The repository extension must not scan handlers or resolve application implementations. The resulting dependency graph is:

```text
Finnova.Models
      ^
Finnova.Repository  <---- Finnova.Service
      ^                         ^
      +--------- API hosts -----+
```

The arrows represent project references. The API hosts are composition roots and may reference both libraries. `Finnova.Repository` never references `Finnova.Service`.

## Component and interface design

### Repository registration

`AddFinnovaRepository(IServiceCollection services, string connectionString)` keeps its existing public signature. At the start of the method it calls `ArgumentException.ThrowIfNullOrWhiteSpace(connectionString)`, then configures `FinnovaDbContext` with `UseSqlServer(connectionString)` and registers the existing repository descriptors. This gives the repository boundary a deterministic response to empty configuration while the host guard handles a missing key and removes `CS8604`.

The method does not change repository lifetimes. `FinnovaDbContext` remains scoped, all repository interfaces remain scoped, and repository-local adapters remain scoped. No service project type is added to this file.

### Service registration

`AddFinnovaServices(IServiceCollection services, IConfiguration configuration)` validates that `configuration` is non-null, obtains the assembly from the existing Login command marker, and performs one MediatR registration and one validator scan:

- `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(serviceAssembly))`.
- `AddValidatorsFromAssembly(serviceAssembly)`.
- `AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`.

It then registers:

- `services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName))`.
- Scoped `ITokenService` → `TokenService`.
- Scoped `IPasswordPolicy` → `DefaultPasswordPolicy`.
- Scoped `IAssetCodeGenerator` → `AssetCodeGenerator`.
- Scoped `CustomerFieldApplicabilityEvaluator`.
- Scoped GL ports to the seven existing `Stub*` classes.
- Scoped `ILookupValidationPort` → `LookupValidationAdapter`.
- Scoped `IDocumentUsageChecker` → `StubDocumentUsageChecker`.
- `AddBlobStorage(configuration)`, preserving its configured singleton provider.

The extension imports the existing types; it does not create a new service registry abstraction or use reflection beyond the established assembly scan.

### Host call sites

Each host adds `using Finnova.Service;` and replaces the registration blocks that it currently owns with:

```csharp
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'FinnovaConnection' is required.");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddFinnovaServices(builder.Configuration);
```

The old `serviceAssembly` local, direct `AddMediatR`, direct `AddValidatorsFromAssembly`, direct validation pipeline registration, direct service implementation registrations, and direct `AddBlobStorage` call are removed. SystemAdmin's second MediatR registration for `CreateCustomerCommand` is removed because the one complete service assembly scan already includes it.

Host-specific registrations remain in place: Accounts GL authorization policies and middleware, SystemAdmin policies, customer middleware, document policy behavior, and UA/SystemAdmin authentication setup. The `JwtSettings` options binding and application services move to `AddFinnovaServices`; JWT bearer validation still reads the host configuration directly.

## Data models and contracts

There are no data model or HTTP contract changes. No entity, enum, request record, response record, route, gateway transform, or JSON serialization setting is added or modified. The only configuration contract affected is the internal DI method signature:

```csharp
IServiceCollection AddFinnovaServices(
    IServiceCollection services,
    IConfiguration configuration)
```

The existing repository method signature remains unchanged. The frontend continues to call the same gateway paths with the same bearer-token behavior and receives the same responses.

## Error handling

### Missing connection-string key

Condition: `GetConnectionString("FinnovaConnection")` returns null.

Behavior: The host throws `InvalidOperationException` while building the service collection, with a message naming `FinnovaConnection`. This is fatal and non-recoverable for that process start. It is not logged as a request error because no request pipeline exists yet; the ASP.NET host startup infrastructure reports the failure.

### Blank connection-string value

Condition: The key exists but contains an empty or whitespace value.

Behavior: The host passes a non-null string to the repository boundary, where `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentException`. This is fatal startup configuration failure. The guard is intentionally duplicated at the boundary because tests or another composition root may call `AddFinnovaRepository` directly without using a web host guard.

### Missing service configuration

Condition: `Jwt` settings are absent or incomplete.

Behavior: The service extension still binds the existing options object, matching current behavior. JWT bearer middleware remains responsible for token validation and will reject invalid tokens according to the host's existing settings. This design does not invent defaults, log secrets, or change the authentication failure response.

### Dependency resolution failure

Condition: A handler requests a repository or service implementation that is not registered, or a host-specific replacement is malformed.

Behavior: The request fails with the existing ASP.NET Core DI activation failure behavior. The design prevents the known class of failures by registering the full service family once and retaining the repository family in its own extension. No exception middleware mapping is changed; a later user-run integration test identifies any missed descriptor.

### Database/provider failure

Condition: SQL Server cannot be reached or EF Core cannot construct the context.

Behavior: This remains an infrastructure/startup or request failure governed by existing EF Core and host middleware behavior. The DI refactor does not catch, transform, retry, or log database exceptions differently.

## Security

The change does not add an authentication or authorization bypass. JWT bearer configuration stays in each host, including issuer, audience, lifetime, signing-key validation, role claim type, and existing `UseAuthentication` before `UseAuthorization` ordering. `TokenService` remains the same implementation and receives the same `JwtSettings` options, now bound in one service-owned extension.

Service and repository descriptors keep scoped lifetimes for request-bound dependencies. Blob storage keeps its existing singleton provider selection. No credentials or connection strings are copied into source code or logs. The startup error names the configuration key but never includes its value.

The existing development GL and document stubs remain explicit and replaceable. Moving their registrations into a shared extension does not make them production adapters; the code comments and future override point remain necessary before production use.

## Frontend impact and compatibility

**Frontend impact: NONE.** No file under `E:\Finnova\Finnova-UI\Finnova-UI` is changed. This work does not change a route, gateway alias, request body, response body, authentication header, error payload, or host port. The shared UI Axios client therefore remains compatible. Static compatibility verification consists of confirming that the host programs, gateway configuration, and UI service paths are untouched.

## Testability and verification strategy

The new unit test should inspect `ServiceDescriptor` entries rather than instantiate a SQL Server context. It can build a plain `ServiceCollection`, call `AddFinnovaServices` with an empty `ConfigurationBuilder`, and assert the service implementation and lifetimes. A second descriptor assertion can call `AddFinnovaRepository` with a syntactically valid placeholder connection string and inspect `FinnovaDbContext`, repository interfaces, and repository lifetimes without resolving the provider.

Later host-level integration tests may use the existing `WebApplicationFactory` infrastructure to prove composition and override the database as current tests do. They should verify that a host can resolve a representative handler and its repository dependencies. They should not require a real SQL Server for this DI-only change.

The user runs the full backend build and test commands after copying the tasks. This design records no build or test result.

## Design decisions and tradeoffs

### One shared service extension versus host-specific service extensions

The chosen approach is one shared extension because all hosts scan the same service assembly and currently duplicate the same registrations. Host-specific extensions would preserve duplication and make a new handler easy to register in only one host. The tradeoff is that a host receives some lazy descriptors it may not currently use; the consistency benefit is greater, and host-specific replacements remain possible.

### Service assembly scan inside the service extension versus marker passed by each host

The chosen approach uses the existing Login command marker inside the service project. Passing a marker from every host would retain a composition detail that caused the current repetition, and the inspected markers all belong to the same assembly. The tradeoff is that the service extension assumes all handlers stay in `Finnova.Service`, which is already the current project boundary.

### Fail-fast configuration versus null suppression

The chosen approach validates the connection string at both host and repository boundaries. Using `connectionString!` would remove a warning without preventing an invalid startup. Throwing early gives a named configuration failure and protects direct test/composition callers. The tradeoff is that an invalid environment fails sooner, which is the intended behavior for a required database dependency.

### Moving blob storage behind the service extension

The chosen approach treats the existing `AddBlobStorage` extension as part of service-layer composition and calls it from `AddFinnovaServices`. This removes the third repeated service call from each host while preserving provider selection and lifetime. A caller that needs a custom provider can register its replacement afterward.

### No source-level refactor of handlers or repositories

The chosen approach changes only registrations and call sites. It avoids touching business behavior while resolving composition drift. If the later build reports an unrelated compiler error, the error must be added as a separate confirmed finding rather than folded into this refactor without evidence.

## Implementation-ordered task list

1. Create `Finnova.Service/DependencyInjection.cs` with the complete service-layer registration method, including MediatR, FluentValidation, the validation pipeline, existing service implementations, existing ports/stubs, JWT options binding, and `AddBlobStorage`.
2. Modify `Finnova.Service/Finnova.Service.csproj` to add `FluentValidation.DependencyInjectionExtensions` at the existing FluentValidation version so the validator scan compiles in the service project.
3. Modify `Finnova.Repository/DependencyInjection.cs` to add the connection-string guard while preserving all existing repository descriptors and lifetimes.
4. Modify `Finnova.UAService/Program.cs` to import `Finnova.Service`, add the validated repository-plus-service composition block once, and remove the old manual service registrations and service assembly scan.
5. Modify `Finnova.AccountsService/Program.cs` in the same way, retaining Accounts-specific authorization policies, middleware, and GL behavior while removing duplicated service registrations.
6. Modify `Finnova.SystemAdminService/Program.cs` in the same way, removing both old MediatR scan blocks and retaining SystemAdmin-specific policies, middleware, customer evaluator behavior through the shared extension, and document middleware behavior.
7. Create `Finnova.Tests/Unit/DependencyInjectionRegistrationTests.cs` to assert the service and repository descriptors are present with the intended lifetimes without resolving a live database.
8. Perform a static diff review confirming only the four spec files are produced by this workflow, no frontend file changes are requested, no routes/contracts/migrations changed, and the copy-paste commands at the end of `tasks.md` are ready for the user to run later.
