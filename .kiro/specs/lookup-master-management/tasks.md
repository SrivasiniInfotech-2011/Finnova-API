# Implementation Plan: Lookup Master Management (FINNOVA-8)

## Overview

This plan converts the Lookup Master Management design into an incremental, test-driven set of
coding tasks that follow the existing Finnova platform conventions exactly: `Finnova.Models`
(entities + record contracts + domain exceptions), `Finnova.Repository` (EF Core, generic
`IRepository<T>` + `RepositoryBase<T>`, per-entity `IEntityTypeConfiguration<T>`, `AddFinnovaRepository`
DI), `Finnova.Service` (MediatR CQRS feature slices with FluentValidation validators and static
`.ToResponse()` mappers), a new host `Finnova.SystemAdminService` (controller + JWT + policies +
middleware), the `Finnova.ApiGateway` YARP reverse proxy, and the React + Redux + RxJS frontend.

The tasks build bottom-up (model -> repository -> service -> cross-cutting -> host -> gateway),
add tests alongside the code they cover, and end by wiring everything together and verifying the
build and test suite. Backend uses **C#/.NET on SQL Server** (design correction #1); property-based
tests use **FsCheck.Xunit**. Frontend uses **TypeScript + React + Redux Toolkit + redux-observable
(RxJS)** with React Testing Library.

Dependency notes carried from the design:
- **SQL Server**, not PostgreSQL (design correction #1) — no Npgsql.
- **No JWT scheme exists yet** — this feature establishes it in the new host (design correction #2, R7.6).
- **No SystemAdmin host exists** — this feature adds `Finnova.SystemAdminService` (design correction #3).
- **Token issuance (login) does not exist** in `Finnova.UAService`. Any task that needs a real token
  is written to use a **dev-signed JWT** so the SystemAdmin host stays testable without the UA login
  endpoint (design Security & Authentication Flow, A2).

## Tasks

- [x] 1. Backend domain model and contracts (Finnova.Models)
  - [x] 1.1 Add `LookupValue` entity
    - Create `Finnova.Models/Domain/Entities/LookupValue.cs` mirroring the `Location` entity style
    - Properties: `Id` (Guid, default `Guid.NewGuid()`), `Module`, `LookupType`, `Code`, `Value`,
      `DisplayOrder` (int), `IsActive` (bool, default true), `IsSystemLocked` (bool),
      `CreatedAt`/`UpdatedAt` (UtcNow)
    - _Requirements: 1.1, 2.1, 2.4, 3.5_ (design: Data Models — `LookupValue` entity)

  - [x] 1.2 Add lookup contract records
    - Create `Finnova.Models/Contracts/Lookups/` with `CreateLookupValueRequest`
      (IsActive nullable), `UpdateLookupValueRequest`, `LookupValueResponse` (includes
      `IsSystemLocked`), `LookupDropdownItemResponse` (Code, Label)
    - Confirm reuse of the existing platform `PaginatedResponse<T>` for listing
    - _Requirements: 2.1, 3.5, 4.1, 5.1, 6.4_ (design: Contracts (records))

  - [x] 1.3 Add domain exceptions
    - Create `Finnova.Models/Domain/Exceptions/` with `LookupLockedException`
      (const `ErrorCode = "ERR-LKP-005"`, message "System-defined lookup codes cannot be altered."),
      `LookupNotFoundException(Guid id)`, `LookupInUseException`
    - _Requirements: 3.1, 3.2, 3.4, 5.4, 5.6_ (design: Shared error model — ERR-LKP-005)

- [x] 2. Repository layer (Finnova.Repository)
  - [x] 2.1 Add `LookupValueConfiguration` and register DbSet
    - Create `Finnova.Repository/Configuration/LookupValueConfiguration.cs` implementing
      `IEntityTypeConfiguration<LookupValue>`: table `lookup_values`, `HasKey`, `HasMaxLength`
      (Module 50, LookupType 100, Code 50, Value 200), required flags
    - Unique index on `(Module, LookupType, Code)`; composite query index on
      `(Module, LookupType, IsActive, DisplayOrder)`
    - `HasData` seed (single English `Value` column): system-locked `SYS_TXN_TYPE` rows
      (DEBIT="Debit", CREDIT="Credit", `locked: true`) and `Origination`/`MARITAL_STATUS` rows
      (SIN="Single", MAR="Married", DIV="Divorced") with fixed seed Ids and seed date
    - Add `DbSet<LookupValue> LookupValues => Set<LookupValue>();` to `FinnovaDbContext`
      (relies on existing `ApplyConfigurationsFromAssembly`)
    - _Requirements: 1.1, 2.8, 3.1, 5.3_ (design: EF configuration `LookupValueConfiguration`)

  - [x] 2.2 Add `ILookupRepository` interface
    - Create `Finnova.Repository/Interfaces/ILookupRepository.cs` extending `IRepository<LookupValue>`
    - Methods: `GetPagedAsync(module?, lookupType?, isActive?, page, pageSize, ct)`,
      `GetActiveByModuleAndTypeAsync(module, lookupType, ct)`,
      `ExistsByCodeAsync(module, lookupType, code, excludeId?, ct)`,
      `ScopeExistsAsync(module, lookupType, ct)`, `GetByModuleTypeCodeAsync(module, lookupType, code, ct)`
    - _Requirements: 1.2, 1.4, 1.5, 2.8, 4.6, 5.3, 6.1, 6.2, 6.5_ (design: Repository Layer — `ILookupRepository`)

  - [x] 2.3 Implement `LookupRepository` and register in DI
    - Create `Finnova.Repository/Repositories/LookupRepository.cs` extending `RepositoryBase<LookupValue>`
    - `GetPagedAsync`: apply case-sensitive/ordinal Module + LookupType + optional IsActive filters,
      count before paging, order by `DisplayOrder` then `Id`, `Skip/Take`, return `(items, total)`
    - `GetActiveByModuleAndTypeAsync`: active-only, order by `DisplayOrder` then `Value`
    - `ExistsByCodeAsync` (with `excludeId`), `ScopeExistsAsync`, `GetByModuleTypeCodeAsync`
    - Register `services.AddScoped<ILookupRepository, LookupRepository>();` in `AddFinnovaRepository`
      (`Finnova.Repository/DependencyInjection.cs`)
    - _Requirements: 1.2, 1.4, 1.5, 4.2, 4.3, 4.5, 4.6, 6.1, 6.2_ (design: Repository Layer — `LookupRepository`)

  - [x] 2.4 Add EF Core migration for `lookup_values` (SQL Server)
    - Generate migration `AddLookupValues` (`--project Finnova.Repository --startup-project Finnova.SystemAdminService`)
      producing `nvarchar` columns, the unique index, the composite query index, and the `HasData` seed rows
    - Note: run after the SystemAdmin host project exists (task 5.1)
    - _Requirements: 1.1, 2.8, 5.3_ (design: EF Core migration note)

- [x] 3. Service layer — mapper and cross-cutting pipeline (Finnova.Service)
  - [x] 3.1 Add `LookupMapper`
    - Create `Finnova.Service/Mappers/LookupMapper.cs` mirroring `LocationMapper`:
      `ToResponse(this LookupValue)` (preserves `IsSystemLocked` + all scalars),
      `ToResponseList(this IEnumerable<LookupValue>)`, `ToDropdownItem(this LookupValue)`
      (Code, Value)
    - _Requirements: 3.5, 6.4_ (design: `LookupMapper`)

  - [x]* 3.2 Write property test for the mapper
    - **Property 12: Mapper preserves fields (system-lock flag and dropdown label)**
    - **Validates: Requirements 3.5, 6.4**
    - FsCheck.Xunit, min 100 iterations, tagged `// Feature: lookup-master-management, Property 12: ...`

  - [x] 3.3 Add `ValidationBehavior<TRequest,TResponse>` MediatR pipeline
    - Create `Finnova.Service/Behaviors/ValidationBehavior.cs` implementing `IPipelineBehavior<,>`:
      run all `IValidator<TRequest>`, aggregate failures, throw `FluentValidation.ValidationException`
      before the handler when any failure exists
    - _Requirements: 1.7, 2.3, 2.5, 2.7, 2.8, 2.9, 5.3, 5.7_ (design: ValidationBehavior)

- [x] 4. Service layer — CQRS commands and queries (Finnova.Service/Lookup)
  - [x] 4.1 Implement `CreateLookupValue` command + handler
    - Create `Finnova.Service/Lookup/Commands/CreateLookupValue/` with `CreateLookupValueCommand`
      (`IRequest<LookupValueResponse>`) and handler
    - Handler: `ScopeExistsAsync` guard (unknown reference -> `ValidationException`),
      `ExistsByCodeAsync` guard (duplicate -> `ValidationException`), build entity with
      `IsActive ?? true`, `AddAsync`, return `.ToResponse()`
    - _Requirements: 2.1, 2.2, 2.4, 2.6, 2.8, 2.9_ (design: Command `CreateLookupValue`)

  - [x] 4.2 Implement `CreateLookupValueCommandValidator`
    - Rules: Module/LookupType/Code/Value required with max lengths (50/100/50/200),
      `DisplayOrder` `InclusiveBetween(0, 9999)`
    - _Requirements: 2.3, 2.5, 2.7_ (design: CreateLookupValueCommandValidator)

  - [x]* 4.3 Write property tests for create-path uniqueness and round trip
    - **Property 2: Lookup Code uniqueness within Module + Lookup Type scope** — **Validates: Requirements 2.8, 5.3**
    - **Property 6: Create-then-dropdown round trip** — **Validates: Requirements 2.2, 2.4, 2.6, 6.3**
    - Two separate FsCheck.Xunit tests, min 100 iterations each, tagged with their property numbers

  - [x]* 4.4 Write unit tests for create validator and handler (TC-LKP-01)
    - Arrange-Act-Assert with mocked `ILookupRepository`: missing required fields (R2.3), length
      overflow (R2.7), DisplayOrder out of range (R2.5), unknown scope (R2.9), duplicate code (R2.8),
      default IsActive (R2.4), and the explicit TC-LKP-01 `WID`/`Widowed`/`4`/`true` example (R2.2)
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.7, 2.8, 2.9_

  - [x] 4.5 Implement `UpdateLookupValue` command + handler + validator
    - `Finnova.Service/Lookup/Commands/UpdateLookupValue/`: command (`IRequest<LookupValueResponse>`),
      handler (`GetByIdAsync` else `LookupNotFoundException`; detect rename by ordinal Code compare;
      system-locked rename -> `LookupLockedException`; uniqueness on rename via `ExistsByCodeAsync`
      with `excludeId`; apply editable fields; `UpdatedAt = UtcNow`; `UpdateAsync`; return `.ToResponse()`)
    - Validator: Id/Code/Value required, lengths 50/200, `DisplayOrder` 0..9999
    - _Requirements: 3.2, 3.3, 3.4, 5.1, 5.3, 5.4, 5.5, 5.7_ (design: Command `UpdateLookupValue`)

  - [x]* 4.6 Write property tests for update behavior
    - **Property 7: Update round trip for editable fields** — **Validates: Requirements 5.1**
    - **Property 8: System-locked values cannot be deleted or renamed (atomic protection)** — **Validates: Requirements 3.1, 3.2, 3.4, 5.5**
    - **Property 9: System-locked values still permit non-identity edits** — **Validates: Requirements 3.3**
    - Three separate FsCheck.Xunit tests, min 100 iterations each, tagged with their property numbers

  - [x]* 4.7 Write unit tests for update validator and handler
    - Mocked `ILookupRepository`: not-found (R5.4), duplicate on rename (R5.3), locked-rename ->
      ERR-LKP-005 (R3.2/3.4), locked non-identity edit succeeds (R3.3), field length/range validation (R5.7)
    - _Requirements: 3.2, 3.3, 3.4, 5.1, 5.3, 5.4, 5.7_

  - [x] 4.8 Implement `DeleteLookupValue` command + handler
    - `Finnova.Service/Lookup/Commands/DeleteLookupValue/`: command (`IRequest<bool>`), handler
      (`GetByIdAsync` else `LookupNotFoundException`; `IsSystemLocked` -> `LookupLockedException`;
      in-use guard hook for `LookupInUseException`; `DeleteAsync`)
    - _Requirements: 3.1, 5.2, 5.4, 5.6_ (design: Command `DeleteLookupValue`)

  - [x]* 4.9 Write property test and unit tests for delete
    - **Property 10: Deleting a non-locked value removes it from all queries** — **Validates: Requirements 5.2**
      (single FsCheck.Xunit test, min 100 iterations, tagged Property 10)
    - Unit tests (mocked repo, incl. mocked reference checker): system-locked delete -> ERR-LKP-005
      TC-LKP-02 `SYS_TXN_TYPE` example (R3.1), not-found (R5.4), in-use guard (R5.6)
    - _Requirements: 3.1, 5.2, 5.4, 5.6_

  - [x] 4.10 Implement `GetLookupValuesPaged` query + handler + validator
    - `Finnova.Service/Lookup/Queries/GetLookupValuesPaged/`: query (defaults Page 1, PageSize 20),
      handler (`GetPagedAsync`, compute `TotalPages = ceil(total/pageSize)`, return
      `PaginatedResponse<LookupValueResponse>`), validator (Page >= 1, PageSize 1..200,
      Module/LookupType max lengths when provided)
    - _Requirements: 1.2, 1.4, 1.5, 1.6, 1.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_ (design: Query `GetLookupValuesPaged`)

  - [x]* 4.11 Write property tests for filtering, ordering, and pagination
    - **Property 1: Filter results satisfy the provided-filter conjunction** — **Validates: Requirements 1.2, 1.4, 1.5, 4.2, 4.3**
    - **Property 3: List ordering is DisplayOrder ascending with Id tie-break** — **Validates: Requirements 4.6**
    - **Property 11: Pagination consistency** — **Validates: Requirements 4.1, 4.5**
    - Three separate FsCheck.Xunit tests, min 100 iterations each, tagged with their property numbers

  - [x] 4.12 Implement `GetLookupDropdownItems` query + handler
    - `Finnova.Service/Lookup/Queries/GetLookupDropdownItems/`: query, handler (`ScopeExistsAsync`
      guard -> `ValidationException` for unknown scope; `GetActiveByModuleAndTypeAsync`;
      `.ToDropdownItem()`; empty list allowed for defined-but-empty scope)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_ (design: Query `GetLookupDropdownItems`)

  - [x]* 4.13 Write property tests and unit tests for dropdown query
    - **Property 4: Dropdown ordering is DisplayOrder asc with Value tie-break, deterministic** — **Validates: Requirements 6.2**
    - **Property 5: Dropdown returns only active values** — **Validates: Requirements 6.1**
      (two separate FsCheck.Xunit tests, min 100 iterations each, tagged with their property numbers)
    - Unit tests (mocked repo): unknown scope -> validation error (R6.5), defined-but-empty scope ->
      empty result not error (R6.6)
    - _Requirements: 6.1, 6.2, 6.5, 6.6_

- [x] 5. New host `Finnova.SystemAdminService`
  - [x] 5.1 Create the host project and add to solution
    - Create `Finnova.SystemAdminService` ASP.NET Core Web API project mirroring the existing host
      pattern (`AccountsService`/`UAService`); reference `Finnova.Service`, `Finnova.Repository`,
      `Finnova.Models`; add to `Finnova.Backend.slnx`; add `Jwt` config (Issuer/Audience/SigningKey)
      and `DefaultConnection` to `appsettings.json`
    - _Requirements: 7.6_ (design correction #3, Host auth wiring)

  - [x] 5.2 Add `ExceptionHandlingMiddleware`
    - Create `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` mapping
      `LookupLockedException` -> 409 (`ERR-LKP-005`), `LookupNotFoundException` -> 404,
      `LookupInUseException` -> 409, `ValidationException` -> 400, fallback -> 500, each written as
      RFC 7807 `ProblemDetails` with a `code` extension
    - _Requirements: 3.1, 5.4, 5.6, 1.7, 2.3, 2.5, 2.7, 2.8, 2.9, 5.3, 5.7_ (design: Exception-to-status mapping)

  - [x] 5.3 Configure `Program.cs` (DI, MediatR, validation, JWT, policy, middleware order)
    - `AddControllers`, `AddMediatR(RegisterServicesFromAssembly(serviceAssembly))`,
      `AddValidatorsFromAssembly(serviceAssembly)`, register `ValidationBehavior<,>` as transient
      `IPipelineBehavior<,>`
    - `AddAuthentication(JwtBearer)` + `AddJwtBearer` (ValidateIssuer/Audience/Lifetime/SigningKey,
      `RoleClaimType = ClaimTypes.Role`), `AddAuthorization` with `"SystemAdmin"` policy `RequireRole("SystemAdmin")`
    - `AddFinnovaRepository(connectionString)` (SQL Server), `AddHealthChecks`, OpenAPI/Swagger with
      Bearer security definition
    - Middleware order: `UseMiddleware<ExceptionHandlingMiddleware>()` -> `UseAuthentication` ->
      `UseAuthorization` -> `MapControllers` -> `MapHealthChecks("/health")`
    - _Requirements: 7.1, 7.2, 7.3, 7.6_ (design: Host auth wiring, Program.cs)

  - [x] 5.4 Implement `LookupController`
    - Create `Finnova.SystemAdminService/Controllers/LookupController.cs` injecting `IMediator`:
      GET `/api/lookup` (`[Authorize(Policy="SystemAdmin")]`, paged), GET `/api/lookup/dropdown`
      (`[Authorize]`, any authenticated), POST `/api/lookup` (`SystemAdmin`, `CreatedAtAction`),
      PUT `/api/lookup/{id:guid}` (`SystemAdmin`), DELETE `/api/lookup/{id:guid}` (`SystemAdmin`, 204)
    - Swagger `[ProducesResponseType]` annotations per the endpoint summary table
    - _Requirements: 2.1, 4.1, 5.1, 6.1, 7.2, 7.3, 7.4, 7.5_ (design: API Layer — `LookupController`)

- [x] 6. Gateway wiring (Finnova.ApiGateway)
  - [x] 6.1 Add SystemAdmin route + cluster and ensure auth passthrough + CORS
    - Add `systemadmin-route` (`Match.Path = /api/systemadmin/{**catch-all}`, `PathRemovePrefix`
      transform) and `systemadmin-cluster` (destination pointing at the SystemAdmin host) to
      `Finnova.ApiGateway/appsettings.json`, mirroring the existing `ua`/`accounts` routes
    - Ensure the gateway CORS policy allows `http://localhost:3000`, the `Authorization` header,
      and GET/POST/PUT/DELETE (YARP forwards `Authorization` by default)
    - _Requirements: 7.1, 7.2, 7.3, 8.10_ (design: Gateway passthrough + CORS)

- [x] 7. Checkpoint - backend build and tests
  - Ensure the solution builds, the `AddLookupValues` migration applies, and all backend unit and
    property tests pass. Ask the user if questions arise.

- [x] 8. Backend integration tests (WebApplicationFactory)
  - [x]* 8.1 Authorization integration tests with dev-signed JWTs
    - `WebApplicationFactory<Program>` for the SystemAdmin host; use dev-signed tokens (UA login
      does not exist yet): 401 for missing/expired/tampered token (R7.1, R7.5), 403 for authenticated
      non-admin (R7.2), 2xx for SystemAdmin (R7.3), authenticated non-admin allowed on dropdown (R7.4)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x]* 8.2 Host smoke test and end-to-end create-then-list against SQL Server
    - Smoke: host boots with JWT scheme + `SystemAdmin` policy registered (R7.6)
    - E2E: against SQL Server (test container or local), create a value then list it to validate the
      migration, unique index, and DisplayOrder/Id ordering
    - _Requirements: 2.2, 4.6, 7.6_

- [x] 9. Frontend Redux slice and epics (React + Redux + RxJS)
  - [x] 9.1 Implement `lookupsSlice`
    - Create `lookupsSlice.ts`: `LookupItem` (single `value` field, not valueEn/valueAr)/`LookupFilters`/`Status`/`LookupsState` types,
      `initialState`, reducers `setFilters`, `loadLookups`/`Success`/`Failure`,
      `createLookup`/`Success`/`Failure`, `updateLookup`/`Success`/`Failure`,
      `deleteLookup`/`Success`/`Failure` (append on create, replace on update, remove on delete,
      leave items unchanged on failure)
    - _Requirements: 8.5, 8.8, 8.9_ (design: Redux slice)

  - [ ]* 9.2 Write Redux state-transition tests
    - Assert each action transitions `status`/`error`/`items`: loading -> succeeded/failed,
      append on createSuccess, replace on updateSuccess, remove on deleteSuccess, error set on failures
    - _Requirements: 8.5, 8.8, 8.9_

  - [x] 9.3 Implement `lookupEpics` and wire the root epic
    - Create `lookupEpics.ts`: `loadLookupsEpic` (`switchMap`, `takeUntil` to cancel stale),
      `createLookupEpic`/`updateLookupEpic`/`deleteLookupEpic` (`mergeMap`), all attaching
      `Authorization: Bearer <token>` from a selector, `timeout(10000)`, and a shared `mapError`
      that maps `ERR-LKP-005` to the exact message and TimeoutError to a retry message; register
      in the root epic and add the reducer to the store
    - _Requirements: 8.4, 8.8, 8.9, 8.10_ (design: RxJS epics)

  - [ ]* 9.4 Write RxJS marble/epic tests
    - `TestScheduler`/fake timers: load emits success on 200 and failure on error; create/update/delete
      map results; `timeout(10000)` produces a retry-able failure past 10s (R8.4); every epic attaches
      `Authorization: Bearer` (R8.10); `ERR-LKP-005` maps to the exact message (R8.8)
    - _Requirements: 8.4, 8.8, 8.10_

- [x] 10. Frontend components (React)
  - [x] 10.1 Implement `ModuleTypeFilter` and `LookupMasterPage` shell
    - `ModuleTypeFilter`: Module select + Lookup Type select disabled until a Module is chosen,
      dispatch `setFilters` + `loadLookups` on selection; `LookupMasterPage` composes filter, grid,
      and error banner and connects to the store
    - _Requirements: 8.1, 8.2_ (design: Component tree)

  - [x] 10.2 Implement `LookupGrid`, `LookupRow`/`EditableRow`, `AddRowForm`, `ErrorBanner`
    - `LookupGrid` renders rows (single Value column) ordered by DisplayOrder then Code with an
      empty-state (R8.3); `EditableRow` disables rename/delete when `isSystemLocked` (R8.7);
      `AddRowForm` blocks submission with field-level messages on missing Code/Value (R8.6) then
      dispatches `createLookup` and refreshes on success (R8.5); `ErrorBanner` shows the exact
      `ERR-LKP-005` message vs a general error (R8.8, R8.9) and a retry prompt on timeout (R8.4)
    - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9_ (design: Grid save flow)

  - [ ]* 10.3 Write React Testing Library tests (TC-LKP-01 and TC-LKP-02)
    - TC-LKP-01 (R8.5): select Module + Type, add row (WID / Widowed / 4 / active), Save with
      mocked create epic, assert the value appears after refresh
    - TC-LKP-02 (R8.7, R8.8): system-locked row has rename/delete disabled; simulated 409 `ERR-LKP-005`
      shows the exact message and leaves the row unchanged
    - Additional: Type disabled until Module selected (R8.1), empty-state (R8.3), field-level message
      with no dispatch (R8.6), general error banner (R8.9)
    - _Requirements: 8.1, 8.3, 8.5, 8.6, 8.7, 8.8, 8.9_

- [x] 11. Final checkpoint - wire together and verify
  - Confirm the frontend store registers the `lookups` reducer and root epic, the gateway route
    reaches the SystemAdmin host, and the full build plus backend and frontend test suites pass.
    Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional (tests) and can be skipped for a faster MVP; core implementation
  tasks are never optional.
- Backend targets **SQL Server** (design correction #1); property-based tests use **FsCheck.Xunit**
  with a mocked `ILookupRepository` (min 100 iterations each), one test per property.
- Each property test is tagged `// Feature: lookup-master-management, Property {n}: {property text}`.
- **JWT token issuance dependency:** `Finnova.UAService` has no login endpoint (A2), so integration
  and any token-dependent tasks use **dev-signed JWTs**; the SystemAdmin host stays fully testable
  without the UA login endpoint.
- Task 2.4 (migration) is authored after task 5.1 because `dotnet ef` needs the host as startup project.
- Checkpoints (tasks 7 and 11) provide incremental validation of build and tests.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "3.1", "3.3"] },
    { "id": 2, "tasks": ["2.3", "3.2"] },
    { "id": 3, "tasks": ["4.1", "4.5", "4.8", "4.10", "4.12"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.6", "4.9", "4.11", "4.13"] },
    { "id": 5, "tasks": ["4.4", "4.7", "5.1"] },
    { "id": 6, "tasks": ["2.4", "5.2", "5.4"] },
    { "id": 7, "tasks": ["5.3", "6.1"] },
    { "id": 8, "tasks": ["8.1", "8.2"] },
    { "id": 9, "tasks": ["9.1", "10.1"] },
    { "id": 10, "tasks": ["9.2", "9.3", "10.2"] },
    { "id": 11, "tasks": ["9.4", "10.3"] }
  ]
}
```
