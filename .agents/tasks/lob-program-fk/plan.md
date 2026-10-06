# Implementation Plan — LOB + Program FK Normalization (two repos)

Backend repo: `d:\Projects\Finnova\Finnova-API` (.NET solution `Finnova.Backend.slnx`, net10.0),
branch `feature/FINNOVA-10-user-management`. Work DIRECTLY on the branch. Do NOT commit, do NOT run
`dotnet ef database update`.

Frontend repo: `D:\Projects\Finnova\Finnova-UI` (React 18 + TS + MUI + Vite, Vitest + RTL),
branch `main`. Work DIRECTLY on the branch. Do NOT commit.

Scope: FULL FK normalization (not validation-only). Replace the string `LineOfBusiness` and
`ProgramName` columns on three satellite tables with `Guid` FKs to the `lines_of_business` (new) and
`programs` (existing) master tables, carry Ids through the handlers/contracts/UI, and expose two GET
master endpoints. One migration, not applied.

---

## Key findings from exploration (decisions grounded in code)

- **`programs` master already exists.** `ScreenProgram` entity (`Finnova.Models/Domain/Entities/ScreenProgram.cs`)
  → table `programs`, EF config `ScreenProgramConfiguration.cs`, 9 `HasData` rows with deterministic GUIDs
  `00000000-0000-0000-0001-0000000000NN` (NN=01..09) and `2024-01-01T00:00:00Z`. `ProgramName` is the stable
  unique key (e.g. `LookupMaster`); `DisplayName` is the label. REUSE as-is; do NOT recreate.

- **`RoleCodeBuilder.Build(roleCenterName, programName)` returns `(roleCenterName + programName).ToUpperInvariant()`.**
  It is fed **`ProgramName`** today (NOT `DisplayName`) — confirmed in
  `ReferenceQueryHandlers.GetRoleCenterProgramsQueryHandler` and `CreateFunctionalGroupCommandHandler`.
  After normalization, `RoleCode` MUST stay byte-identical, so wherever a row is built from a `ProgramId`,
  the handler MUST resolve `ProgramId → ScreenProgram.ProgramName` and feed THAT (not DisplayName) to
  `RoleCodeBuilder`. Property P2 (`UserManagementHelperProperties.RoleCode_IsUppercaseConcatenation_AndDeterministic`)
  pins this and must keep passing — do NOT touch `RoleCodeBuilder`.

- **CRITICAL catalog/master mismatch.** The program names flowing through access/functional-group today come
  from `RoleCenterCatalog` (`Finnova.Service/UserManagement/Internal/RoleCenterCatalog.cs`): `"Company Master"`,
  `"User Master"`, `"Lookup Master"`, `"Asset Master"`, `"Entity Master"`, `"Application Entry"`. **None of these
  equal a `programs.ProgramName` value** (`LookupMaster`, `NationalityMaster`, …). If `ProgramId` becomes a hard FK
  to `programs` while the catalog still emits those strings, nothing resolves. **Decision (recorded, see Assumptions):**
  repoint `RoleCenterCatalog` so each Role Center maps to a set of **real `programs.ProgramName` keys**, and have
  `GetRoleCenterProgramsQuery` resolve those names → `programs` rows → return `ProgramId` + `DisplayName`. This is the
  only coherent full-normalization path given "RoleCode must stay identical" + "`GetRoleCenterPrograms` returns Id".
  The satellite tables hold NO rows (no `HasData`, verified), so there is no stored data to reconcile.

- **`GetMyPermissions` is an Auth flow that joins on `ProgramName` and MUST be repointed.**
  `GetMyPermissionsQueryHandler` (`Finnova.Service/Auth/Queries/GetMyPermissions/`) collapses a user's
  `UserAccessAssignment` rows by `ProgramName` and inner-joins against `GetActiveProgramsAsync()`
  (`ScreenProgram.ProgramName`). After normalization the rows carry `ProgramId`; the handler must group/join by
  `ProgramId` and still emit `ScreenPermissionResponse.ProgramName` = the resolved `ScreenProgram.ProgramName`
  (the UI's `useScreenPermissions` gates on that string — it must stay identical). This is the "all Auth-related
  APIs" concern from the original request.

- **No real rows in the three satellites.** `grep HasData` across `Configuration/*` shows seeds only for Location,
  LookupValue, ScreenProgram, UserAccount. `user_access_assignments`, `user_branch_associations`,
  `functional_group_functions` have none ⇒ the migration can DROP the old string columns and ADD non-null FK
  columns with no backfill.

- **Current satellite indexes** (from `FinnovaDbContextModelSnapshot.cs`, must be recreated against the new columns):
  - `user_access_assignments`: `(UserAccountId, LineOfBusiness, RoleCode)` and `(UserGroupId, LineOfBusiness, RoleCode)`
    → become `(UserAccountId, LineOfBusinessId, RoleCode)` / `(UserGroupId, LineOfBusinessId, RoleCode)`. Keep the
    `CK_user_access_assignments_owner` check constraint.
  - `user_branch_associations`: `(UserAccountId, LineOfBusiness)` / `(UserGroupId, LineOfBusiness)` →
    `(UserAccountId, LineOfBusinessId)` / `(UserGroupId, LineOfBusinessId)`. Keep its owner check constraint.
  - `functional_group_functions`: unique `(FunctionalGroupId, RoleCode)` — RoleCode is retained, so this index is
    unchanged by the Program FK, but add the new `ProgramId` FK index.

- **EF conventions:** `IEntityTypeConfiguration<T>` in `Finnova.Repository/Configuration/`, `ToTable`,
  `HasIndex(...).IsUnique()`, `HasData` with deterministic GUIDs + `new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc)`.
  `FinnovaDbContext` uses `ApplyConfigurationsFromAssembly` (only a `DbSet` needs adding). EF startup project is
  `Finnova.SystemAdminService`. Migration command pattern (used by every prior spec):
  `dotnet ef migrations add <Name> --project Finnova.Repository --startup-project Finnova.SystemAdminService`.

- **Masters mirror pattern:** other masters (`EntityMaster`, `Court`, `Nationality`) have business columns +
  `IsActive`, `CreatedAt`, `UpdatedAt`. `LineOfBusiness` matches that shape.

- **Ref endpoints already route.** `UserManagementController` is `[Route("api/user")]` on the SystemAdmin host; the
  two new GETs live under `api/user/ref/*` alongside the existing ones. The gateway already aliases
  `/api/ua/api/user/**` → `systemadmin-cluster` (`ua-user-alias-route`), and the UI axios base is
  `http://localhost:5000/api/ua/api` with service `basePath = '/user'`. **No new gateway route is required.**

- **UI service pattern:** `interfaces/<f>.interface.ts` + `mock/<f>.mock.ts` + `real/<f>.real.ts` +
  `<f>.service.ts` toggling on `import.meta.env.VITE_USE_MOCK_API === 'true'`, barrels `services/index.ts`,
  `services/interfaces/index.ts`, `models/index.ts`. `useScreenPermissions` / `PermissionsContext` already exist and
  must stay intact. Build = `npm run build` (`tsc -b && vite build`); test = `npm test` (= `vitest run`), filter with
  `npm test -- --run <path>`.

- **Backend test harness:** `Finnova.Tests` references `Finnova.SystemAdminService` only. xUnit + Moq + FsCheck.Xunit
  are available. `GetMyPermissionsQueryHandlerTests`, `UserManagementHandlerTests`,
  `UserManagementHelperProperties`, and `UserManagementAuthorizationTests` (integration, PUT access body sends
  `LineOfBusiness`/`Rows`/`BranchCodes`) all touch the changed surface and must be updated to the Id shapes.

---

## PINNED WIRE SHAPES (System.Text.Json camelCase; the frontend step MUST match these exactly)

Access/user-management contracts after normalization:

```jsonc
// AccessRightRow  (request rows AND response rows)
{
  "roleCode": "SYSTEM ADMINLOOKUPMASTER",   // unchanged: RoleCenterName + ProgramName, uppercased
  "roleCenterName": "System Admin",
  "programId": "00000000-0000-0000-0001-000000000001",  // NEW: FK to programs.Id
  "programName": "LookupMaster",            // RETAINED for display/RoleCode parity (stable key)
  "displayName": "Lookup Master",           // NEW: programs.DisplayName, so UI renders a label w/o lookup
  "canAdd": false, "canModify": false, "canQuery": false, "canDelete": false
}

// SaveUserAccessRequest  (PUT /api/user/{id}/access body)
{
  "lineOfBusinessId": "GUID",               // was "lineOfBusiness" (string)
  "rows": [ AccessRightRow, ... ],
  "branchCodes": ["ALL"],                   // unchanged
  "copyProfile": {                          // Create mode only; null otherwise
    "sourceUserCode": "ASHA1",
    "sourceLineOfBusinessId": "GUID"        // was "sourceLineOfBusiness" (string)
  }
}

// UserAccessResponse  (GET/PUT /api/user/{id}/access response)
{
  "lineOfBusinessId": "GUID",               // was "lineOfBusiness"
  "lineOfBusinessName": "Retail Lending",   // NEW: label so UI needn't re-lookup
  "rows": [ AccessRightRow, ... ],
  "branchCodes": ["ALL"]
}

// GetUserAccess query param: GET /api/user/{id}/access?lobId=GUID   (was ?lob=<string>)

// FunctionalGroupFunctionResponse
{ "programId": "GUID", "programName": "LookupMaster", "roleCode": "..." }  // programName retained
```

Two NEW ref endpoints on the SystemAdmin host (`UserManagementController`, `[Route("api/user")]`):

```jsonc
// GET /api/user/ref/lines-of-business   -> active LOBs (REPLACES the hardcoded-catalog data source)
[ { "id": "GUID", "lobName": "Retail Lending", "lobDescription": "..." } ]

// GET /api/user/ref/programs            -> active programs (NEW)
[ { "id": "GUID", "programName": "LookupMaster", "displayName": "Lookup Master" } ]
```

Note: these two use dedicated response records (NOT the generic `ReferenceItemResponse(Code,Label)`), per the
task's field spec. `role-centers/{name}/programs` now returns the enriched `AccessRightRow` above (with
`programId`/`displayName`).

---

## BACKEND (direct in `d:\Projects\Finnova\Finnova-API`)

- [ ] 1. Create the `LineOfBusiness` domain entity.
      Fields: `Guid Id` (PK), `string LOB_Name`, `string LOB_Description`, `bool IsActive`, `DateTime CreatedAt`,
      `DateTime UpdatedAt` (mirror `EntityMaster`/`Court`). India-only, single English display field.
      Files: `Finnova.Models\Domain\Entities\LineOfBusiness.cs`
      Verify: `dotnet build Finnova.Models/Finnova.Models.csproj` succeeds.

- [ ] 2. Add `LineOfBusinessId`/`ProgramId` FK fields to the three satellite entities; drop the string fields.
      - `UserAccessAssignment`: remove `LineOfBusiness` and `ProgramName` strings; add `Guid LineOfBusinessId`
        and `Guid ProgramId`. KEEP `RoleCenterName` and `RoleCode`.
      - `UserBranchAssociation`: remove `LineOfBusiness`; add `Guid LineOfBusinessId`.
      - `FunctionalGroupFunction`: remove `ProgramName`; add `Guid ProgramId`. KEEP `RoleCode`.
      Files: `Finnova.Models\Domain\Entities\UserAccessAssignment.cs`, `UserBranchAssociation.cs`,
      `FunctionalGroupFunction.cs`
      Verify: `dotnet build Finnova.Models/Finnova.Models.csproj` succeeds (Service/Repository will break until
      their tasks land — expected; the solution build is gated at task 13).

- [ ] 3. Update the UserManagement contracts to the pinned Id shapes.
      In `Finnova.Models\Contracts\UserManagement\UserManagementRequests.cs`: `AccessRightRow` gains `Guid ProgramId`
      + `string DisplayName` and KEEPS `ProgramName`; `CopyProfileRequest.SourceLineOfBusiness` → `Guid SourceLineOfBusinessId`;
      `SaveUserAccessRequest.LineOfBusiness` → `Guid LineOfBusinessId`.
      In `UserManagementResponses.cs`: `UserAccessResponse.LineOfBusiness` → `Guid LineOfBusinessId` + add
      `string LineOfBusinessName`; `FunctionalGroupFunctionResponse(string ProgramName, string RoleCode)` →
      `(Guid ProgramId, string ProgramName, string RoleCode)`. Add two new response records in a new file
      `Finnova.Models\Contracts\UserManagement\MasterReferenceResponses.cs`:
      `record LineOfBusinessRefResponse(Guid Id, string LobName, string LobDescription);` and
      `record ProgramRefResponse(Guid Id, string ProgramName, string DisplayName);`.
      Files: the two contract files above + the new file.
      Verify: `dotnet build Finnova.Models/Finnova.Models.csproj` succeeds.

- [ ] 4. Create `LineOfBusinessConfiguration` + register the DbSet.
      `ToTable("lines_of_business")`, `HasKey(Id)`, `Property(LOB_Name).IsRequired().HasMaxLength(100)`,
      `Property(LOB_Description).HasMaxLength(400)`, `Property(IsActive).IsRequired()`,
      `HasIndex(LOB_Name).IsUnique()`. `HasData` seeding the three catalog LOBs
      (`Retail Lending`, `Corporate Lending`, `Leasing` — from `LineOfBusinessCatalog.WithRoleCodes`) with
      deterministic GUIDs `00000000-0000-0000-0002-0000000000NN` (NN=01..03) and
      `new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc)`, all `IsActive=true`, with short English descriptions.
      Add `public DbSet<LineOfBusiness> LinesOfBusiness => Set<LineOfBusiness>();` to `FinnovaDbContext`.
      Files: `Finnova.Repository\Configuration\LineOfBusinessConfiguration.cs`,
      `Finnova.Repository\Context\FinnovaDbContext.cs` (DbSet only)
      Verify: `dotnet build Finnova.Repository/Finnova.Repository.csproj` fails only on the satellite configs
      (fixed next); `LineOfBusinessConfiguration` itself compiles.

- [ ] 5. Update the three satellite EF configs for the FK columns, FKs, indexes, and `OnDelete(Restrict)`.
      - `UserAccessAssignmentConfiguration`: drop the `LineOfBusiness`/`ProgramName` string properties; add
        `Property(LineOfBusinessId).IsRequired()`, `Property(ProgramId).IsRequired()`; add
        `HasOne<LineOfBusiness>().WithMany().HasForeignKey(x=>x.LineOfBusinessId).OnDelete(DeleteBehavior.Restrict)`
        and `HasOne<ScreenProgram>().WithMany().HasForeignKey(x=>x.ProgramId).OnDelete(DeleteBehavior.Restrict)`;
        replace the two indexes with `(UserAccountId, LineOfBusinessId, RoleCode)` and
        `(UserGroupId, LineOfBusinessId, RoleCode)`; keep the owner check constraint.
      - `UserBranchAssociationConfiguration`: drop `LineOfBusiness`; add `LineOfBusinessId` + FK (Restrict);
        indexes `(UserAccountId, LineOfBusinessId)` / `(UserGroupId, LineOfBusinessId)`; keep owner check.
      - `FunctionalGroupFunctionConfiguration`: drop `ProgramName`; add `ProgramId` + FK to `ScreenProgram`
        (Restrict); keep the unique `(FunctionalGroupId, RoleCode)` index.
      Files: the three `*Configuration.cs`
      Verify: `dotnet build Finnova.Repository/Finnova.Repository.csproj` — Repository compiles (the repo methods in
      task 6 are additive; existing string filters are fixed in task 6).

- [ ] 6. Update `IUserManagementRepository` + `UserManagementRepository` to filter by Id and expose master lookups.
      - Change `ReplaceAccessAsync(Guid owner, string lob, …)` and
        `GetAccessAsync(Guid owner, string lob)` signatures to take `Guid lobId` and filter on `LineOfBusinessId`.
      - Add `Task<List<LineOfBusiness>> GetActiveLinesOfBusinessAsync(CancellationToken)` and
        `Task<LineOfBusiness?> GetLineOfBusinessByIdAsync(Guid id, CancellationToken)` and
        `Task<ScreenProgram?> GetProgramByIdAsync(Guid id, CancellationToken)` (reuse `GetActiveProgramsAsync` for the
        programs list). `GetAccessAssignmentsByUserAsync` is unchanged in signature.
      Files: `Finnova.Repository\Interfaces\IUserManagementRepository.cs`,
      `Finnova.Repository\Repositories\UserManagementRepository.cs`
      Verify: `dotnet build Finnova.Repository/Finnova.Repository.csproj` succeeds.

- [ ] 7. Repoint `RoleCenterCatalog` to real `programs.ProgramName` keys (resolves the catalog/master mismatch).
      Map each Role Center to a set of EXISTING `programs.ProgramName` values, e.g.
      `"System Admin" → ["LookupMaster","NationalityMaster","UserManagement"]`,
      `"Origination" → ["EntityMaster","LocationMaster","Organization"]` (pick from the 9 seeded keys; keep two
      role centers). This keeps `RoleCode = RoleCenterName + ProgramName` deterministic AND makes every emitted
      program name resolvable to a `programs` row. Record the exact mapping chosen in the FunctionalGroup/Access
      handlers' comments. (`LineOfBusinessCatalog` is retained ONLY for the `HasRoleCodes` validation rule — see
      task 9; its `ActiveLinesOfBusiness` list is no longer the ref data source.)
      Files: `Finnova.Service\UserManagement\Internal\RoleCenterCatalog.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` (will still fail on handlers until tasks 8–10;
      this file itself compiles).

- [ ] 8. Rework the reference query handlers + add the two master ref queries.
      - `GetRoleCenterProgramsQueryHandler`: for each program NAME from `RoleCenterCatalog`, resolve the matching
        active `ScreenProgram` (via `GetActiveProgramsAsync`/by name) and emit the enriched `AccessRightRow`
        (`ProgramId`, `ProgramName`, `DisplayName`, `RoleCode = RoleCodeBuilder.Build(roleCenter, ProgramName)`).
      - Replace `GetAccessibleLinesOfBusinessQueryHandler` to read `GetActiveLinesOfBusinessAsync()` and return
        `List<LineOfBusinessRefResponse>` (id/lobName/lobDescription) — REPLACES the hardcoded catalog source.
      - Add `GetProgramsRefQuery() : IRequest<List<ProgramRefResponse>>` + handler returning active programs
        (`GetActiveProgramsAsync`) mapped to `ProgramRefResponse(Id, ProgramName, DisplayName)`.
      Update the query records (`ReferenceQueries.cs`): change `GetAccessibleLinesOfBusinessQuery`'s result type to
      `List<LineOfBusinessRefResponse>`; add `GetProgramsRefQuery`.
      Files: `Finnova.Service\UserManagement\Queries\References\ReferenceQueries.cs`, `ReferenceQueryHandlers.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` (gated with tasks 9–10).

- [ ] 9. Rework `SaveUserAccess` (command + handler) for Ids.
      - `SaveUserAccessCommand`: `LineOfBusiness`(string) → `Guid LineOfBusinessId`; `CopyProfile` carries
        `SourceLineOfBusinessId`.
      - Handler: resolve the LOB via `GetLineOfBusinessByIdAsync` (404/validation if missing); keep the
        `LineOfBusinessCatalog.HasRoleCodes(lob.LOB_Name)` rule (preserve the "LOB must be linked to role codes"
        validation against the LOB NAME, per locked decision 2). Build `UserAccessAssignment` rows with
        `LineOfBusinessId = lob.Id`, `ProgramId = row.ProgramId`, `RoleCenterName`, and
        `RoleCode = RoleCodeBuilder.Build(row.RoleCenterName, row.ProgramName)` (ProgramName comes in on the request
        row; if trusting client is unwanted, resolve `ProgramId → ScreenProgram.ProgramName` server-side and use that —
        PREFER server-side resolution so RoleCode cannot drift). CopyProfile source read uses `SourceLineOfBusinessId`.
        Return `UserAccessResponse(lob.Id, lob.LOB_Name, rows, branches)`.
      - `SaveUserAccessCommandValidator`: `RuleFor(x => x.LineOfBusinessId).NotEmpty()`; keep BranchCodes + per-row
        RoleCenterName rules.
      Files: `Finnova.Service\UserManagement\Commands\SaveUserAccess\SaveUserAccessCommand.cs`,
      `SaveUserAccessCommandHandler.cs`, `SaveUserAccessCommandValidator.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` (gated with task 10/11).

- [ ] 10. Rework `GetUserAccess` (query + handler) and `CreateFunctionalGroup` handler + mapper for Ids.
      - `GetUserAccessQuery`: `LineOfBusiness`(string) → `Guid LineOfBusinessId`. Handler: `GetAccessAsync(id, lobId)`;
        resolve each row's `ProgramId → ScreenProgram` (ProgramName + DisplayName) and build enriched `AccessRightRow`s;
        resolve the LOB name; return `UserAccessResponse(lobId, lobName, rows, branchCodes)`.
      - `CreateFunctionalGroupCommandHandler`: build `FunctionalGroupFunction` with `ProgramId` (resolve each catalog
        program NAME → active `ScreenProgram.Id`) and `RoleCode = RoleCodeBuilder.Build(roleCenter, ProgramName)`.
      - `UserManagementMapper.ToResponse(FunctionalGroup)`: emit `FunctionalGroupFunctionResponse(f.ProgramId,
        <resolved ProgramName>, f.RoleCode)`. Since `FunctionalGroupFunction` no longer stores `ProgramName`, resolve
        it (pass the active-programs lookup into the mapper, or map in the handler) — PREFER resolving in the handler
        where the repo is available and passing names into the mapper.
      Files: `Finnova.Service\UserManagement\Queries\GetUserAccess\GetUserAccessQuery.cs`, `GetUserAccessQueryHandler.cs`,
      `Finnova.Service\UserManagement\Commands\CreateFunctionalGroup\CreateFunctionalGroupCommandHandler.cs`,
      `Finnova.Service\UserManagement\Mappers\UserManagementMapper.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` (gated with task 11).

- [ ] 11. Repoint the `GetMyPermissions` Auth handler to join by `ProgramId`.
      Group the user's `UserAccessAssignment` rows by `ProgramId` (OR the four flags), inner-join against
      active `ScreenProgram` by `Id`, and emit `ScreenPermissionResponse` using the resolved
      `ScreenProgram.ProgramName` (so the UI gating string is byte-identical to today). Admin bypass still returns
      every active program's `ProgramName` with all four flags true. Do NOT change `ScreenPermissionResponse` /
      `MyPermissionsResponse` shapes.
      Files: `Finnova.Service\Auth\Queries\GetMyPermissions\GetMyPermissionsQueryHandler.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` succeeds.

- [ ] 12. Add the two ref endpoints to `UserManagementController` and repoint `GetAccess`'s query param.
      - Change `GetAccess` to `[FromQuery] Guid lobId` and send `GetUserAccessQuery(id, lobId)`.
      - Change `SaveAccess` to pass `r.LineOfBusinessId` and `r.CopyProfile` (with `SourceLineOfBusinessId`).
      - `GET ref/lines-of-business` now returns `List<LineOfBusinessRefResponse>` (handler already replaced).
      - Add `GET ref/programs` → `List<ProgramRefResponse>` via `GetProgramsRefQuery`.
      No gateway change (already aliased). The SystemAdmin host's `ExceptionHandlingMiddleware` already maps
      `UserValidationException`/`UserNotFoundException`; no new branch needed unless a new exception type is added
      (none is).
      Files: `Finnova.SystemAdminService\Controllers\UserManagementController.cs`
      Verify: `dotnet build Finnova.Backend.slnx` succeeds.

- [ ] 13. Update backend tests to the Id shapes (unit + property + integration + in-memory repo).
      - `Finnova.Tests\Infrastructure\InMemoryUserManagementRepository.cs`: match new `ReplaceAccessAsync`/
        `GetAccessAsync` signatures (filter by `LineOfBusinessId`); add `GetActiveLinesOfBusinessAsync`,
        `GetLineOfBusinessByIdAsync`, `GetProgramByIdAsync`; add a `List<LineOfBusiness> Lobs` backing list.
      - `GetMyPermissionsQueryHandlerTests.cs`: build `UserAccessAssignment` rows with `ProgramId` matching the
        seeded `ScreenProgram.Id`; assert the emitted `ProgramName` is unchanged (`LookupMaster`, etc.).
      - `UserManagementAuthorizationTests.cs`: the "PUT access" body → `{ LineOfBusinessId = <guid>, Rows = [],
        BranchCodes = ["ALL"] }` (authorization assertions unchanged; it only checks 401/403).
      - Any `SaveUserAccess`/`GetUserAccess`/`CreateFunctionalGroup` handler/property tests that referenced the string
        columns → switch to Ids. `RoleCodeBuilder` property P2 stays as-is (do NOT edit).
      Files: the test files above (and any other test referencing `.LineOfBusiness`/`.ProgramName` on these entities).
      Verify (ONE targeted slice, per efficiency mandate):
      `dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~UserManagement|FullyQualifiedName~GetMyPermissions|FullyQualifiedName~Auth|FullyQualifiedName~RoleCode"`
      — all selected tests pass. (Run the backend build `dotnet build Finnova.Backend.slnx` once here too.)

- [ ] 14. Generate the EF migration (do NOT apply).
      `dotnet ef migrations add AddLineOfBusinessAndFkNormalization --project Finnova.Repository --startup-project Finnova.SystemAdminService`
      Review the generated `Up()`: `CreateTable("lines_of_business")` + unique `LOB_Name` index + 3 `InsertData`
      seed rows; `DropIndex` of the old string indexes; `DropColumn` `LineOfBusiness`/`ProgramName` on the three
      tables; `AddColumn` `LineOfBusinessId`/`ProgramId`; `CreateIndex` the new FK + composite indexes;
      `AddForeignKey` with `ReferentialAction.Restrict`. Confirm `FinnovaDbContextModelSnapshot.cs` updated.
      Do NOT run `dotnet ef database update` (the orchestrator applies after review).
      Files: new `Finnova.Repository\Migrations\<ts>_AddLineOfBusinessAndFkNormalization.cs` (+ Designer),
      updated `FinnovaDbContextModelSnapshot.cs`
      Verify: `dotnet build Finnova.Backend.slnx` succeeds and the migration file exists with the expected `Up()`.

---

## FRONTEND (direct in `D:\Projects\Finnova\Finnova-UI`)

- [ ] 15. Add LOB + Program master models and extend the userManagement model to Ids.
      In `src\models\userManagement.model.ts`:
      - `AccessRightRow`: add `programId: string` and `displayName: string`; KEEP `programName`.
      - `CopyProfileRequest`: `sourceLineOfBusiness` → `sourceLineOfBusinessId: string`.
      - `SaveUserAccessData`: `lineOfBusiness` → `lineOfBusinessId: string`.
      - `UserAccess`: `lineOfBusiness` → `lineOfBusinessId: string`; add `lineOfBusinessName: string`.
      - `FunctionalGroupFunction`: add `programId: string` (keep `programName`, `roleCode`).
      - Add `interface LineOfBusinessRef { id: string; lobName: string; lobDescription: string; }` and
        `interface ProgramRef { id: string; programName: string; displayName: string; }`.
      Files: `src\models\userManagement.model.ts` (models barrel already re-exports it).
      Verify: `npm run build` fails only where consumers still use old fields (fixed in 16–20); the model file type-checks.

- [ ] 16. Add LOB + Programs master services (interface/mock/real/toggle) and update barrels.
      Mirror `lookup.service.ts`. New feature `lobProgram` (or two features — ONE service module is fine):
      - `src\services\interfaces\lobProgram.interface.ts`:
        `interface ILobProgramService { getLinesOfBusiness(): Promise<LineOfBusinessRef[]>;
         getPrograms(): Promise<ProgramRef[]>; }`
      - `src\services\real\lobProgram.real.ts`: `api.get<LineOfBusinessRef[]>('/user/ref/lines-of-business')` and
        `api.get<ProgramRef[]>('/user/ref/programs')` (shared axios; base already has the gateway prefix).
      - `src\services\mock\lobProgram.mock.ts`: three LOBs (`Retail Lending`/`Corporate Lending`/`Leasing` with ids +
        descriptions) and the 9 programs (ids = the backend seed GUIDs, programName/displayName as seeded).
      - `src\services\lobProgram.service.ts`: toggle on `VITE_USE_MOCK_API`.
      - Barrels: add `export { lobProgramService } from './lobProgram.service';` to `services\index.ts` and
        `export type { ILobProgramService } from './lobProgram.interface';` to `services\interfaces\index.ts`.
      Files: the five files above + the two barrels.
      Verify: `npm run build` (gated with later tasks).

- [ ] 17. Repoint the userManagement service LOB ref + types to the new shapes (interface/real/mock).
      - `interfaces\userManagement.interface.ts`: `getAccess(id, lobId)`; `getLinesOfBusiness()` may be removed from
        this interface in favor of `lobProgramService` (PREFER: keep AccessStep calling `lobProgramService` for LOBs
        and `getRoleCenterPrograms` for program rows). Keep `getRoleCenters`/`getRoleCenterPrograms`/`getBranchTree`.
      - `real\userManagement.real.ts`: `getAccess(id, lobId)` → `params: { lobId }`; `saveAccess` body already passes
        `d` (now Id-shaped). `getRoleCenterPrograms` return type is the enriched `AccessRightRow`.
      - `mock\userManagement.mock.ts`: update `roleCenters` map to the real program-name keys chosen in backend task 7;
        `roleCode()` unchanged; `getRoleCenterPrograms` returns rows with `programId`/`displayName` (ids from the
        programs seed); `saveAccess`/`getAccess` key on `lineOfBusinessId`; the `HasRoleCodes`-equivalent check uses the
        LOB id→name. Keep `getLinesOfBusiness` returning the LOB refs if retained, else delete it.
      Files: `src\services\interfaces\userManagement.interface.ts`, `src\services\real\userManagement.real.ts`,
      `src\services\mock\userManagement.mock.ts`
      Verify: `npm run build` (gated).

- [ ] 18. Update the Access wizard slice to carry Ids (draft + context + AccessStep + grids).
      - `src\hooks\useUserManagementDraft.ts` `DraftAccess`: `lineOfBusiness` → `lineOfBusinessId`,
        `copySourceLineOfBusiness` → `copySourceLineOfBusinessId` (keep `roleCenter`, `rows`, `branchCodes`,
        `copyProfileEnabled`, `copySourceUserCode`).
      - `AccessStep.tsx`: load LOBs via `lobProgramService.getLinesOfBusiness()` (bind `value=lob.id`,
        label `lob.lobName`); the LOB `Select` writes `lineOfBusinessId`; branch-tree effect keys on
        `access.lineOfBusinessId`; `getRoleCenterPrograms` rows already carry `programId`/`displayName`; the
        "LOB / Role Code / Program" grid's program column renders `displayName` (fallback `programName`); copy-source
        LOB select binds ids.
      - `AccessRightsGrid.tsx`: program column header stays "Program Description" but render `displayName`
        (fallback `programName`); `getRowId` stays `roleCode`.
      - `ReviewStep.tsx`: show the LOB name (resolve `lineOfBusinessId` → loaded LOB label, or show the id) and leave
        rows count as-is.
      Files: `src\hooks\useUserManagementDraft.ts`, `src\components\userManagement\WizardContext.tsx`
      (only if it names the renamed fields — it spreads generically, so likely untouched),
      `AccessStep.tsx`, `AccessRightsGrid.tsx`, `ReviewStep.tsx`
      Verify: `npm run build` (gated).

- [ ] 19. Update the save payload composition in the wizard to Ids.
      `UserManagementWizard.tsx` `handleSubmit`: `saveAccess` body → `{ lineOfBusinessId: access.lineOfBusinessId ?? '',
      rows: access.rows ?? [], branchCodes: access.branchCodes ?? [], copyProfile: enabled && sourceUser ?
      { sourceUserCode, sourceLineOfBusinessId: access.copySourceLineOfBusinessId ?? '' } : null }`.
      Keep `useScreenPermissions('UserManagement')` gating (`canSave`) exactly as-is.
      Files: `src\components\userManagement\UserManagementWizard.tsx`
      Verify: `npm run build` succeeds (whole app type-checks).

- [ ] 20. Update the frontend tests touched by the shape change (do not expand scope).
      - `src\services\real\userManagement.real.test.ts`: the "puts access" expectation → body with
        `lineOfBusinessId` (a guid) and the enriched rows; add/adjust the `getAccess` path assertion to
        `params: { lobId }`.
      - `src\components\userManagement\AccessStep.test.tsx`: the service mock now returns LOB refs via
        `lobProgramService` (mock that module) and `getRoleCenterPrograms` rows with `programId`/`displayName`; keep the
        Copy-Profile visibility assertions.
      - `src\services\mock\userManagement.mock.test.ts` and `AccessRightsGrid.test.tsx` / `UserManagementWizard.test.tsx`:
        adjust any references to `lineOfBusiness`/`programName`-only rows to the Id shapes.
      - Add a small `src\services\lobProgram.service.test.ts` (mock axios; assert the two ref paths).
      Files: the test files above.
      Verify (ONCE, per efficiency mandate): `npm run build` is green, then
      `npm test -- --run src/services/real/userManagement.real.test.ts src/components/userManagement/AccessStep.test.tsx src/services/lobProgram.service.test.ts src/services/mock/userManagement.mock.test.ts`
      — all pass. (A single full `npm test -- --run` is acceptable as the one final confirmation if the filtered run
      is green; do NOT loop full runs.)

---

## Dependencies / ordering notes

- Strict backend order: 1 (LOB entity) → 2 (entity FK fields) → 3 (contracts) → 4 (LOB config+DbSet) →
  5 (satellite configs) → 6 (repo) → 7 (catalog) → 8 (ref handlers) → 9/10 (command/query/mapper) → 11 (auth) →
  12 (controller) → 13 (tests) → 14 (migration). Tasks 1–11 may leave intermediate projects red; the solution build
  is first fully green at task 12, so defer the solution-wide build/test to tasks 12–14.
- Frontend depends on the PINNED WIRE SHAPES above (independent of backend compilation, but must match it). Order:
  15 (models) → 16 (LOB/Programs service) → 17 (UM service) → 18 (wizard slice) → 19 (save payload) → 20 (tests).
- Verify ONCE each side (efficiency mandate): backend build once + the one targeted test filter (task 13 + 14);
  frontend `npm run build` once + one filtered `npm test -- --run` (task 20). No repeated full suites.

## Assumptions (recorded for confirmation — silent decisions made explicit)

1. **RoleCenterCatalog is repointed to real `programs.ProgramName` keys** (task 7). The current catalog program names
   (`Company Master`, `User Master`, `Asset Master`, …) are sample data with no matching `programs` row, so a hard
   `ProgramId` FK cannot resolve them. Because the three satellite tables hold NO rows, there is no stored data to
   migrate; aligning the catalog to the seeded program keys is the only full-normalization path that keeps `RoleCode`
   deterministic. If instead new `programs` rows should be seeded for those exact catalog names, say so and task 4/7
   change accordingly.
2. **LOB seed = the three `LineOfBusinessCatalog.WithRoleCodes` entries** (`Retail Lending`, `Corporate Lending`,
   `Leasing`), with short English descriptions, deterministic GUIDs `…-0002-0000000000NN`. The
   "LOB must be linked to role codes" rule is preserved by validating the resolved LOB NAME against
   `LineOfBusinessCatalog.HasRoleCodes` (kept for that purpose only).
3. **RoleCode parity is enforced server-side**: `SaveUserAccess` resolves `ProgramId → ScreenProgram.ProgramName` and
   feeds that to `RoleCodeBuilder` (rather than trusting a client-sent `programName`), so RoleCode cannot drift.
4. **No new API-gateway route**: the two ref GETs live under `/api/user/ref/*` on the already-aliased SystemAdmin host.
5. **`getLinesOfBusiness` moves to the new `lobProgramService`** on the UI; the userManagement service keeps the
   access/role-center/branch calls. (If keeping it on `userManagementService` is preferred, only task 16/17 wiring
   shifts; the wire shapes are unchanged.)

## Commands summary

- Backend build: `dotnet build Finnova.Backend.slnx`
- Backend targeted tests: `dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~UserManagement|FullyQualifiedName~GetMyPermissions|FullyQualifiedName~Auth|FullyQualifiedName~RoleCode"`
- Backend migration (NO apply): `dotnet ef migrations add AddLineOfBusinessAndFkNormalization --project Finnova.Repository --startup-project Finnova.SystemAdminService`
- UI build: `npm run build`  (in `D:\Projects\Finnova\Finnova-UI`)
- UI tests (filtered, once): `npm test -- --run <touched specs>`

NOTHING is committed in either repo and the migration is NOT applied; both are left to the orchestrator/user.
