# Review — LOB + Program FK Normalization

LOB and program references across the user-management access model move off free-text string columns onto Guid foreign keys into a new `lines_of_business` master and the existing `programs` table. The access satellites (`user_access_assignments`, `user_branch_associations`, `functional_group_functions`) now carry `LineOfBusinessId`/`ProgramId`, the two reference GET endpoints read from the real tables instead of in-memory catalogs, and the UI dropdowns bind to Ids while displaying names. RoleCode generation is pinned to the server-resolved `programs.ProgramName`, keeping the permission-gating string byte-identical to before.

Watch for: nothing blocking. Two LOW notes below (branch-tree param now carries a Guid against a `string lob` endpoint that ignores it; `LineOfBusinessCatalog` retained only for the role-code-linkage rule). Reviewed against working-tree diffs in both repos plus the recorded `verification.md`; build/test suites were not re-run per the efficiency mandate.

**Verdict**: APPROVED

## High-level view

The schema change is symmetric across all three satellites: drop the `LineOfBusiness`/`ProgramName` string columns and their composite indexes, add Guid FK columns, recreate the composite indexes against the new columns, add single-column FK indexes and `Restrict` foreign keys. The migration seeds three `lines_of_business` rows with deterministic GUIDs and a unique `LOB_Name` index. The satellites hold no production rows, so the drop-and-recreate is non-destructive; the migration was generated but intentionally not applied.

RoleCode parity is the load-bearing concern and it holds: `SaveUserAccess` resolves every `ProgramId` to its `programs.ProgramName` server-side before calling `RoleCodeBuilder`, so a client cannot drift the RoleCode by sending a different program name, and `DisplayName` is used only for the UI label — never for RoleCode. `GetMyPermissions` groups by `ProgramId` but emits the resolved `ProgramName`, keeping the UI gating string unchanged.

The reference APIs read the real tables: `/ref/lines-of-business` returns `LineOfBusinessRefResponse` from `lines_of_business`, and a new `/ref/programs` returns `ProgramRefResponse` from `programs`. The frontend adds a dedicated `lobProgramService` (interface + mock + real + toggle + barrels) pointed at `/user/ref/...`, which the existing `/api/ua/api/user/**` gateway alias already covers, so no new route was needed.

UI contracts match the backend records field-for-field in camelCase: `lineOfBusinessId`, `lineOfBusinessName`, `programId`, `programName`, `displayName`, `sourceLineOfBusinessId`, and the `access` GET query param renamed `lob` -> `lobId` to match `[FromQuery] Guid lobId`.

<details>
<summary>Issues (2)</summary>

1. **Branch-tree param now carries a Guid** (LOW) — `AccessStep` passes `access.lineOfBusinessId` into `getBranchTree(lob)`, which forwards it as the `lob` query string to an endpoint whose handler (`BranchTreeCatalog.Tree()`) ignores the argument entirely. No runtime break (the tree is static), but the param now semantically carries an Id against a name-shaped contract. Rename/retype when the branch tree is wired to a real LOB-scoped source. Out of this ticket's FK scope.
2. **`LineOfBusinessCatalog` retained** (LOW) — the hardcoded catalog is no longer used for the LOV (that now reads the table) but is still the source of truth for the "LOB must be linked to role codes" rule (`HasRoleCodes(lob.LOB_Name)`). The three seeded `LOB_Name`s match the catalog by name, so the rule holds; a future rename of a seeded LOB would silently diverge. Consider moving the linkage flag onto the master row later.

</details>

<details>
<summary>Details</summary>

## FK normalization and the migration

All three satellites follow one pattern: drop the `nvarchar(100)` `LineOfBusiness`/`ProgramName` columns and the composite indexes keyed on them, add the `uniqueidentifier` FK columns, recreate the composite indexes against the new Id columns (`UserAccountId, LineOfBusinessId, RoleCode` etc.), add single-column FK indexes, and `AddForeignKey` with `ReferentialAction.Restrict`. `functional_group_functions.ProgramId` -> `programs`, `user_access_assignments` -> both `lines_of_business` and `programs`, `user_branch_associations.LineOfBusinessId` -> `lines_of_business`; the configurations and snapshot match. `Restrict` blocks deleting an LOB/program that still has access rows rather than cascading those rows away.

The `AddColumn<Guid>` calls use a zero-Guid default — harmless only because the satellites hold no rows; `verification.md` records the data-loss notice and confirms `database update` was not run. `Down()` is a faithful inverse.

## lines_of_business master and seed

The entity uses the ticket-specified shape (`Id`, `LOB_Name`, `LOB_Description`) plus `IsActive`/`CreatedAt`/`UpdatedAt`. `LOB_Name` is required, max-length 100, with a unique index backing the business-line key (SQL Server's default case-insensitive collation backs uniqueness). The seed is deterministic (fixed GUIDs `…-0002-…01/02/03`, fixed timestamp), and the three seeded names match `LineOfBusinessCatalog.WithRoleCodes` exactly — the coupling that keeps the role-code-linkage rule consistent after the LOV moved to the table (see issue 2).

## RoleCode parity

In `SaveUserAccessCommandHandler`, each incoming `AccessRightRow.ProgramId` is resolved against the active-programs lookup to `program.ProgramName`, and `RoleCodeBuilder.Build(roleCenterName, programName)` is called with that server-resolved name — a client that sends a mismatched `programName` in the row cannot influence the stored RoleCode. `DisplayName` feeds only the grid's "Program Description" column (`valueGetter: row.displayName || row.programName`), never RoleCode. `CreateFunctionalGroup` and `GetRoleCenterPrograms` follow the same path: catalog name -> active `programs` row -> `ProgramId` + RoleCode from `program.ProgramName`, skipping any catalog name with no active program row.

`GetMyPermissions` groups by `ProgramId` but emits `nameById[g.Key]` (the resolved `ProgramName`), so the UI gating string is unchanged. The unit tests exercise this: two rows on the same program Id collapse by OR, a row whose `ProgramId` is not in the active registry is omitted, and an active program with no rows is omitted.

## Reference APIs and the retained catalog

`/ref/lines-of-business` returns `LineOfBusinessRefResponse(Id, LobName, LobDescription)` from `GetActiveLinesOfBusinessAsync` (active-only, ordered by name), replacing the previous `LineOfBusinessCatalog`-backed `ReferenceItem` projection. The new `/ref/programs` returns `ProgramRefResponse(Id, ProgramName, DisplayName)` from the active programs. `LineOfBusinessCatalog` survives only as the role-code-linkage oracle in `SaveUserAccess` (`HasRoleCodes(lob.LOB_Name)`) — issue 2.

## UI contract parity and Id plumbing

`getAccess` sends `{ params: { lobId } }` against `[FromQuery] Guid lobId`; `saveAccess` sends `lineOfBusinessId` and `copyProfile.sourceLineOfBusinessId`. The LOB and Source-LOB `Select`s bind `value`/`key` to `l.id` and render `l.lobName`; the access-rows grid and review step resolve the display name by finding the Id in the fetched LOB list, falling back to the Id string if the list has not loaded. `getLinesOfBusiness` was removed from `IUserManagementService` and relocated to `lobProgramService`.

## Test coverage

Backend: the in-memory repo gained a `Lobs` list and the three lookup methods; the authorization theory payload switched to `LineOfBusinessId`; the merger property tests use the new `(programId, programName, displayName)` arity; the `GetMyPermissions` tests were rewritten around program Ids. `verification.md` records 212 targeted tests passing and a green build.

Frontend: `lobProgram.service.test.ts` asserts both new endpoints and their response field names; `userManagement.real.test.ts` asserts the `lobId` query param and the `lineOfBusinessId` save payload. `verification.md` records 6 files / 20 tests passing and a green `npm run build`.

Not tested: build/test suites were not re-run here (efficiency mandate); findings rely on diffs plus recorded evidence. No integration test asserts the DB-level FK `Restrict` behavior end-to-end — acceptable while the migration is unapplied and the satellites are empty.

</details>

<details>
<summary>File map</summary>

Backend (`d:\Projects\Finnova\Finnova-API`, `feature/FINNOVA-10-user-management`, working tree vs HEAD):
- `Finnova.Models/Domain/Entities/LineOfBusiness.cs` — new master entity (Id/LOB_Name/LOB_Description + audit fields).
- `Finnova.Models/Domain/Entities/{UserAccessAssignment,UserBranchAssociation,FunctionalGroupFunction}.cs` — string columns -> Guid FK columns.
- `Finnova.Models/Contracts/UserManagement/MasterReferenceResponses.cs` — new `LineOfBusinessRefResponse` / `ProgramRefResponse`.
- `Finnova.Models/Contracts/UserManagement/UserManagement{Requests,Responses}.cs` — Id fields + `DisplayName`/`LineOfBusinessName`.
- `Finnova.Repository/Configuration/LineOfBusinessConfiguration.cs` — table/unique index/seed.
- `Finnova.Repository/Configuration/{UserAccessAssignment,UserBranchAssociation,FunctionalGroupFunction}Configuration.cs` — FK relationships + reindexing.
- `Finnova.Repository/Context/FinnovaDbContext.cs` — `LinesOfBusiness` DbSet.
- `Finnova.Repository/Interfaces/IUserManagementRepository.cs` + `Repositories/UserManagementRepository.cs` — Id-based signatures + LOB/program lookups.
- `Finnova.Repository/Migrations/20261005025155_AddLineOfBusinessAndFkNormalization.cs` (+ Designer, snapshot) — the FK migration (generated, not applied).
- `Finnova.Service/Auth/Queries/GetMyPermissions/GetMyPermissionsQueryHandler.cs` — group-by-Id, emit resolved name.
- `Finnova.Service/UserManagement/Commands/SaveUserAccess/*`, `Commands/CreateFunctionalGroup/*`, `Queries/GetUserAccess/*`, `Queries/References/*`, `Mappers/UserManagementMapper.cs`, `Internal/RoleCenterCatalog.cs` — Id resolution + server-side RoleCode.
- `Finnova.SystemAdminService/Controllers/UserManagementController.cs` — `lobId` param + new `/ref/programs`.
- `Finnova.Tests/*` — in-memory repo lookups, updated unit/property/integration specs.

Frontend (`D:\Projects\Finnova\Finnova-UI`, `main`, working tree vs HEAD):
- `src/models/userManagement.model.ts` — Id fields, `LineOfBusinessRef`/`ProgramRef`.
- `src/services/interfaces/lobProgram.interface.ts`, `services/{mock,real}/lobProgram.*.ts`, `services/lobProgram.service.ts`, barrels — new ref service.
- `src/services/interfaces/userManagement.interface.ts`, `services/{mock,real}/userManagement.*.ts` — Id signatures, `getLinesOfBusiness` relocated.
- `src/components/userManagement/{AccessStep,AccessRightsGrid,ReviewStep,UserManagementWizard}.tsx`, `hooks/useUserManagementDraft.ts` — Id-bound dropdowns, display-name resolution.
- `src/**/*.test.ts(x)` — updated specs.

Full diffs: `git diff HEAD` in each repo (backend branch `feature/FINNOVA-10-user-management`, frontend `main`).

</details>
