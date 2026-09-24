# Implementation Plan: Nationality Master Management

**JIRA:** FINNOVA-9 · **Module:** SystemAdmin · **Master:** Nationality

## Overview

This plan implements the Nationality Master feature as an API-only vertical slice inside the
**existing** `Finnova.SystemAdminService` host, mirroring the Lookup Master (FINNOVA-8) layered
CQRS conventions. Work proceeds bottom-up by dependency: domain models → EF configuration &
repositories → CQRS service layer (commands, queries, validators, mappers) → controller +
error-handling middleware extension → gateway alias → tests (property, edge, integration) →
frontend (separate `Finnova-UI` repo). Each step builds on the previous and ends with wiring
into the running host and UI.

The host's JWT authentication, `SystemAdmin` authorization policy, MediatR `ValidationBehavior`
pipeline, `ExceptionHandlingMiddleware`, `AddFinnovaRepository` registration, and the
`PaginatedResponse<T>` contract already exist and are reused, not re-created.

Implementation language: **C#** (.NET) for the backend; **TypeScript/React** for the frontend
(per the design; no language selection needed).

## Tasks

- [x] 1. Add domain models and contracts
  - [x] 1.1 Add domain entities and enum for nationality + audit trail
    - Create `Finnova.Models/Domain/Entities/Nationality.cs` with `Id` (Guid), `Code` (string, canonical max 10), `Name` (single English string, max 100), `IsActive` (bool, default true), `CreatedAt`/`UpdatedAt` (UTC).
    - Create `Finnova.Models/Domain/Entities/NationalityAuditEntry.cs` with `Id`, `NationalityId`, `Action`, nullable `OldName`, `NewName`, `ChangedBy`, `ChangedAtUtc` (UTC); immutable by having no mutating members beyond property setters used at construction.
    - Create `Finnova.Models/Domain/Enums/NationalityAuditAction.cs` enum (`Create = 0`, `Update = 1`).
    - _Requirements: 1.1, 1.2, 3.1, 4.1, 4.2_

  - [x] 1.2 Add nationality contracts (request/response records)
    - Create `Finnova.Models/Contracts/Nationalities/CreateNationalityRequest.cs` = `(string Code, string Name, bool? IsActive)` (nullable IsActive so service defaults to true).
    - Create `UpdateNationalityNameRequest.cs` = `(string Name)` (only Name editable; Code immutable post-create).
    - Create `NationalityResponse.cs` = `(Guid Id, string Code, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt)`.
    - Create `NationalityAuditEntryResponse.cs` = `(Guid Id, Guid NationalityId, string Action, string? OldName, string NewName, string ChangedBy, DateTime ChangedAtUtc)`.
    - Reuse the existing `PaginatedResponse<T>` for paged search (do not create a new paged contract).
    - _Requirements: 1.1, 3.1, 4.4, 5.4_

- [x] 3. Configure persistence for the new entities
  - [x] 3.1 Add EF Core entity configurations
    - Create `Finnova.Repository/Configuration/NationalityConfiguration.cs`: table `nationalities`, key `Id`, `Code` required max 10, `Name` required max 100, `IsActive` required, **unique index on `Code`** (backs case-insensitive uniqueness via default CI collation), index on `Name` (search/order path).
    - Create `Finnova.Repository/Configuration/NationalityAuditEntryConfiguration.cs`: table `nationality_audit_entries`, key `Id`, `NationalityId` required, `Action` required (int), `OldName` nullable max 100, `NewName` required max 100, `ChangedBy` required max 200, `ChangedAtUtc` required, composite index `(NationalityId, ChangedAtUtc)`; no FK to `Nationality` so audit survives independently.
    - Rely on the existing `ApplyConfigurationsFromAssembly` call for auto-discovery.
    - _Requirements: 2.1, 2.2, 4.4, 4.5, 5.10_

  - [x] 3.2 Register DbSets on FinnovaDbContext
    - Add `DbSet<Nationality> Nationalities` and `DbSet<NationalityAuditEntry> NationalityAuditEntries` to `FinnovaDbContext`.
    - _Requirements: 1.1, 4.1_

- [x] 4. Implement the repository layer
  - [x] 4.1 Define repository interfaces
    - Create `INationalityRepository : IRepository<Nationality>` with `GetPagedAsync(string? searchTerm, int page, int pageSize, CancellationToken)` returning `(List<Nationality> Items, int Total)` and `ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken)`.
    - Create `INationalityAuditRepository : IRepository<NationalityAuditEntry>` with `GetByNationalityIdAsync(Guid nationalityId, CancellationToken)` returning `List<NationalityAuditEntry>`.
    - _Requirements: 2.1, 4.4, 4.5, 5.1, 5.10_

  - [x] 4.2 Implement NationalityRepository
    - Mirror `LookupRepository`. `GetPagedAsync`: `AsNoTracking`, apply trimmed case-insensitive `Contains` on `Code` OR `Name` when term is non-blank (blank/whitespace = no filter), count total before paging, order by `Name` asc then `Code` asc, apply `Skip((page-1)*pageSize).Take(pageSize)`. `ExistsByCodeAsync`: trim code, `AnyAsync` on `Code == c` excluding `excludeId` when supplied.
    - _Requirements: 2.1, 2.2, 5.1, 5.2, 5.3, 5.4, 5.9, 5.10, 5.11_

  - [x] 4.3 Implement NationalityAuditRepository
    - `GetByNationalityIdAsync`: `AsNoTracking`, filter by `NationalityId`, order by `ChangedAtUtc` desc then `Id` desc, return list (empty list when none match, never throw).
    - _Requirements: 4.4, 4.5_

  - [x] 4.4 Register repositories in DI
    - Add `AddScoped<INationalityRepository, NationalityRepository>()` and `AddScoped<INationalityAuditRepository, NationalityAuditRepository>()` in `DependencyInjection.AddFinnovaRepository`.
    - _Requirements: 1.1, 4.1_

- [x] 5. Add EF Core migration for the new tables
  - [x] 5.1 Generate the AddNationalityAndAudit migration
    - Run `dotnet ef migrations add AddNationalityAndAudit --project Finnova.Repository --startup-project Finnova.SystemAdminService` to create both tables and the unique `Code` index.
    - Verify the generated migration reflects the configurations from task 3.1 (no seed data required).
    - _Requirements: 1.1, 2.1, 4.1_

- [x] 6. Implement domain exceptions and mapper
  - [x] 6.1 Add nationality domain exceptions
    - Create `NationalityNotFoundException` (thrown when an update targets a missing id).
    - Create a dedicated duplicate-code exception (e.g. `NationalityDuplicateCodeException`) carrying the exact message `"Nationality code must be unique"`, so the middleware can map it to 409 by type (avoiding message-sniffing on generic validation).
    - _Requirements: 2.2, 3.4_

  - [x] 6.2 Implement static ToResponse mappers
    - Create `Finnova.Service/Mappers/NationalityMapper.cs` with `Nationality.ToResponse()`, `IEnumerable<Nationality>.ToResponseList()`, and `NationalityAuditEntry.ToResponse()` preserving every field (Action mapped to its enum name string).
    - _Requirements: 1.1, 4.1, 4.2, 4.4_

  - [x] 6.3 Write property test for mappers
    - **Property 13: Mapper preserves fields**
    - **Validates: Requirements 1.1, 4.1, 4.2, 4.4**

- [x] 7. Implement the create-nationality command
  - [x] 7.1 Implement CreateNationalityCommand, validator, and handler
    - Create `CreateNationalityCommand(string Code, string Name, bool? IsActive, string ActingAdmin) : IRequest<NationalityResponse>` under `Finnova.Service/Nationality`.
    - Validator: `Code` NotEmpty + MaxLength(10); `Name` NotEmpty + MaxLength(100).
    - Handler: trim `Code`; if `ExistsByCodeAsync` true throw the duplicate-code exception (no record, no audit); else `AddAsync` nationality with `IsActive ?? true`, then `AddAsync` a `Create` audit entry (`OldName=null`, `NewName=name`, `ChangedBy=ActingAdmin`, `ChangedAtUtc=UtcNow`) in the same scope; return `entity.ToResponse()`.
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.2, 2.5, 4.2, 4.3_

  - [x] 7.2 Write property test for create round trip
    - **Property 1: Create round trip**
    - **Validates: Requirements 1.1, 1.6**

  - [x] 7.3 Write property test for code uniqueness on create
    - **Property 2: Code uniqueness on create**
    - **Validates: Requirements 1.5, 2.1, 2.2**

  - [x] 7.4 Write property test for IsActive default
    - **Property 3: Create defaults IsActive to true**
    - **Validates: Requirements 1.2**

  - [x] 7.5 Write property test for audit entry on create
    - **Property 6: Audit entry written on create**
    - **Validates: Requirements 4.2**

- [x] 8. Implement the update-name command
  - [x] 8.1 Implement UpdateNationalityNameCommand, validator, and handler
    - Create `UpdateNationalityNameCommand(Guid Id, string Name, string ActingAdmin) : IRequest<NationalityResponse>`.
    - Validator: `Id` NotEmpty; `Name` NotEmpty + MaxLength(100).
    - Handler: `GetByIdAsync` or throw `NationalityNotFoundException` (nothing changed); if submitted `Name` equals current `Name` return existing record as a no-op with no audit; else capture `oldName`, set `Name`, refresh `UpdatedAt=UtcNow`, `UpdateAsync`, then `AddAsync` an `Update` audit entry (`OldName=oldName`, `NewName=newName`); return `entity.ToResponse()`.
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.3_

  - [x] 8.2 Write property test for update name round trip
    - **Property 4: Update name round trip**
    - **Validates: Requirements 3.1**

  - [x] 8.3 Write property test for same-name no-op
    - **Property 5: Same-name update is a no-op with no audit**
    - **Validates: Requirements 3.5, 4.3**

  - [x] 8.4 Write property test for audit entry on update
    - **Property 7: Audit entry written on name update**
    - **Validates: Requirements 4.1**

  - [x] 8.5 Write property test for no audit on rejection
    - **Property 8: No audit entry on rejection**
    - **Validates: Requirements 4.3**

- [x] 9. Implement the query handlers
  - [x] 9.1 Implement GetNationalitiesPagedQuery, validator, and handler
    - Create `GetNationalitiesPagedQuery(string? SearchTerm, int Page = 1, int PageSize = 20) : IRequest<PaginatedResponse<NationalityResponse>>`.
    - Validator: `Page >= 1`; `PageSize` InclusiveBetween(1, 100).
    - Handler: call `GetPagedAsync`, map items via `ToResponseList`, compute `TotalPages = ceil(Total / PageSize)`, return `PaginatedResponse<NationalityResponse>` echoing `Page`/`PageSize`.
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11_

  - [x] 9.2 Implement GetNationalityAuditTrailQuery and handler
    - Create `GetNationalityAuditTrailQuery(Guid NationalityId) : IRequest<List<NationalityAuditEntryResponse>>`.
    - Handler: call `GetByNationalityIdAsync`, map to responses; missing id yields empty list (no error).
    - _Requirements: 4.4, 4.5_

  - [x] 9.3 Write property test for search filter conjunction
    - **Property 10: Search filter conjunction**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.11**

  - [x] 9.4 Write property test for pagination consistency
    - **Property 11: Pagination consistency**
    - **Validates: Requirements 5.4, 5.9**

  - [x] 9.5 Write property test for list ordering
    - **Property 12: List ordering is deterministic**
    - **Validates: Requirements 5.10**

  - [x] 9.6 Write property test for audit trail ordering
    - **Property 9: Audit trail ordering is deterministic**
    - **Validates: Requirements 4.4, 4.5**

- [x] 10. Add in-memory test infrastructure and edge unit tests
  - [x] 10.1 Add nationality test infrastructure
    - Under `Finnova.Tests/Infrastructure`, create `InMemoryNationalityRepository`, `InMemoryNationalityAuditRepository`, `NationalityBuilder`, and `NationalityGenerators` (valid + boundary strings within max 10/100, casing/whitespace variants, blank strings, datasets, and audit-entry sets including empty), mirroring the existing `InMemoryLookupRepository`/`LookupGenerators`.
    - _Requirements: 1.1, 2.1, 4.1, 5.1_

  - [x] 10.2 Write validator boundary and query-default edge unit tests
    - Code length 10 accepted / 11 rejected; Name 100 accepted / 101 rejected; blank Code/Name rejected; Page defaults to 1 and PageSize to 20; Page < 1 rejected; PageSize < 1 or > 100 rejected.
    - _Requirements: 1.3, 1.4, 2.5, 3.2, 3.3, 5.5, 5.6, 5.7, 5.8_

  - [x] 10.3 Write not-found, audit-missing-id, and immutability edge tests
    - Update unknown id throws `NationalityNotFoundException` and `UpdateAsync` is never called; audit read for a missing id returns `[]` with no exception; assert the audit repository exposes only add + immutable reads (no update/delete) and `UpdateNationalityNameRequest` has no `Code` field.
    - _Requirements: 3.4, 4.5, 4.6_

- [x] 11. Wire up the API and middleware
  - [x] 11.1 Implement the controller
    - Create `Finnova.SystemAdminService/Controllers/NationalityController.cs`: `[ApiController]`, `[Route("api/[controller]")]`, every action `[Authorize(Policy = "SystemAdmin")]`.
    - Resolve the acting admin id from `User` claims (`ClaimTypes.NameIdentifier`/`sub`, fallback to `Name`) and pass into commands.
    - Endpoints: `GET api/nationality?search=&page=1&pageSize=20` → 200 `PaginatedResponse<NationalityResponse>`; `POST api/nationality` → 201 via `CreatedAtAction(nameof(GetPaged), new { search = result.Code }, result)`; `PUT api/nationality/{id:guid}` → 200; `GET api/nationality/{id:guid}/audit` → 200 with possibly-empty list.
    - Dispatch all via MediatR so the existing `ValidationBehavior` runs.
    - _Requirements: 1.1, 3.1, 4.4, 4.5, 5.1, 5.4, 6.1, 6.2, 6.3_

  - [x] 11.2 Extend ExceptionHandlingMiddleware with the nationality branch
    - Add cases to the existing `Finnova.SystemAdminService` `ExceptionHandlingMiddleware` switch (do not replace the lookup branch): `NationalityNotFoundException` → 404 `ERR-NAT-404`; duplicate-code exception → 409 `ERR-NAT-409`; nationality validation failure → 400 `ERR-NAT-400`; fallback → 500 `ERR-NAT-500`.
    - Keep the existing `ProblemDetails` shape (`Status`, `Title = code`, `Detail`, `Extensions["code"]`) so the UI keeps reading `message` and `code`.
    - _Requirements: 2.2, 2.3, 3.4, 1.3, 1.4, 3.2, 3.3, 5.7, 5.8_

  - [x] 11.3 Add gateway UI-alias routes for nationality
    - In `Finnova.ApiGateway/appsettings.json`, add `ua-nationality-alias-route` (`/api/ua/api/nationality/{**catch-all}`) and `ua-nationality-alias-root` (`/api/ua/api/nationality`), both to `systemadmin-cluster` with transforms `PathRemovePrefix /api/ua/api` + `PathPrefix /api`, mirroring the existing lookup alias. Keep host separation intact.
    - _Requirements: 6.3_

- [x] 12. Checkpoint - backend build and tests green
  - Run `dotnet build` and `dotnet test`. Ensure all tests pass, ask the user if questions arise.

- [x] 13. Add end-to-end integration tests for the host
  - [x] 13.1 Write authorization integration tests
    - Using `WebApplicationFactory<Program>` (mirror `SystemAdminAppFactory`, EF Core InMemory provider, dev-signed JWTs): assert 401 for missing/expired/invalid token on every endpoint, 403 for a valid non-admin token on every endpoint, and 401-before-403 for an invalid token that also lacks the role.
    - _Requirements: 6.1, 6.2, 6.4_

  - [x] 13.2 Write end-to-end create/list/audit integration test
    - With a valid `SystemAdmin` token: create a nationality (201), list it via paged search (200), and read its audit trail (200), asserting case-insensitive code uniqueness rejection returns 409 `ERR-NAT-409`.
    - _Requirements: 1.1, 4.4, 5.1, 6.3_

- [x] 14. Frontend model and service layer (Finnova-UI repo)
  - **Implemented in the separate `Finnova-UI` repository at `E:\Finnova\Finnova-UI\Finnova-UI` (React 18 + TS + MUI + Vite; hooks + Context; no Redux/RxJS).**
  - [x] 14.1 Add model and service interface
    - Create `src/models/nationality.model.ts` (`Nationality`, `NationalityFormData`, `NationalityUpdateData`, `NationalityAuditEntry`) and `src/services/interfaces/nationality.interface.ts` (`NationalityQueryParams`, `INationalityService` with `getPaged`/`create`/`updateName`/`getAuditTrail`).
    - _Requirements: 1.1, 3.1, 4.4, 5.1_

  - [x] 14.2 Implement real and mock services + toggle
    - Create `src/services/real/nationality.real.ts` using the shared `api` axios instance with `basePath = '/nationality'`, mapping camelCase DTOs to UI models. Create `src/services/mock/nationality.mock.ts` (in-memory, English-only seeds, case-insensitive uniqueness throwing an `ERR-NAT-409`-shaped error, search/order/pagination/audit-append). Create `src/services/nationality.service.ts` selecting via `VITE_USE_MOCK_API` and re-export from `src/services/index.ts`. Mirror `lookup.service.ts`.
    - _Requirements: 1.1, 2.2, 3.1, 4.4, 5.1_

  - [x] 14.3 Write service unit tests (Vitest)
    - Mock service: case-insensitive uniqueness (throws `ERR-NAT-409` shape), search filtering, ordering, pagination, audit append. Real service: DTO↔model mapping and `/nationality` path composition against a mocked `api`.
    - _Requirements: 1.1, 2.2, 4.4, 5.1, 5.10_

- [x] 15. Frontend NationalityMaster page (Finnova-UI repo)
  - **Implemented in the `Finnova-UI` repository.**
  - [x] 15.1 Implement NationalityMaster.tsx
    - Create `src/pages/NationalityMaster.tsx` mirroring `LookupMaster.tsx`: hooks-only state, debounced search `TextField` driving `getPaged`, an `@mui/x-data-grid` grid (Code, Name, IsActive, Updated) with Edit and View-Audit row actions, and MUI dialogs for Add (code + name + active), Edit-name (name only, code read-only), and Audit-trail (newest-first). Wire into the app's routing/navigation as the other master pages are.
    - _Requirements: 1.1, 3.1, 4.4, 5.1_

  - [x] 15.2 Write page/component tests (Vitest + RTL)
    - Mirror `LookupMaster.test.tsx`: render the grid, open Add/Edit/Audit dialogs, submit create/rename, and cover the duplicate-code error path (grid unchanged, toast message read from `error.response.data.message`). Success and error paths both covered.
    - _Requirements: 1.1, 2.2, 3.1, 4.4, 5.1_

- [x] 16. Final checkpoint - all builds and tests green
  - Run `dotnet build` / `dotnet test` in the backend and `npm run build` / `npm test` in `Finnova-UI`. Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core implementation tasks are never optional.
- Each task references specific requirements (and property tests reference a specific correctness property) for traceability.
- Property tests use FsCheck.Xunit with a minimum of 100 iterations; each of the 13 correctness properties is implemented by exactly one property-based test.
- Edge/example behavior and authorization (R6) are covered by plain xUnit facts/theories and `WebApplicationFactory<Program>` integration tests, not property tests.
- The SystemAdmin host, JWT scheme, `SystemAdmin` policy, `ValidationBehavior`, and `PaginatedResponse<T>` already exist and are reused, not re-created.
- Frontend tasks (14, 15) are implemented in the separate `Finnova-UI` repository, not in this backend workspace.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "14.1"] },
    { "id": 1, "tasks": ["1.2", "3.1", "6.1", "14.2"] },
    { "id": 2, "tasks": ["3.2", "4.1", "6.2", "14.3", "15.1"] },
    { "id": 3, "tasks": ["4.2", "4.3", "6.3", "10.1", "15.2"] },
    { "id": 4, "tasks": ["4.4"] },
    { "id": 5, "tasks": ["5.1", "7.1", "8.1", "9.1", "9.2"] },
    { "id": 6, "tasks": ["7.2", "7.3", "7.4", "7.5", "8.2", "8.3", "8.4", "8.5", "9.3", "9.4", "9.5", "9.6", "10.2", "10.3", "11.1"] },
    { "id": 7, "tasks": ["11.2"] },
    { "id": 8, "tasks": ["11.3", "13.1", "13.2"] }
  ]
}
```
