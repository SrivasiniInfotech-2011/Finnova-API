# Verification — LOB + Program FK Normalization

First-iteration full implementation. Both repos verified ONCE at the end per the user efficiency
mandate (no repeated full suites). Nothing committed; the migration was generated but NOT applied.

## Backend (`d:\Projects\Finnova\Finnova-API`, branch `feature/FINNOVA-10-user-management`)

| Command | Result |
| --- | --- |
| `dotnet build Finnova.Backend.slnx` | **PASS** — Build succeeded, 0 errors, 1 pre-existing warning (CS8604 in `Finnova.SystemAdminService/Program.cs`, unrelated to this change). |
| `dotnet ef migrations add AddLineOfBusinessAndFkNormalization --project Finnova.Repository/Finnova.Repository.csproj --startup-project Finnova.SystemAdminService/Finnova.SystemAdminService.csproj` | **PASS** — scaffolded `20261005025155_AddLineOfBusinessAndFkNormalization.cs` (+ Designer) and updated `FinnovaDbContextModelSnapshot.cs`. The expected data-loss notice was emitted (old string columns are dropped — satellites hold no rows). **`dotnet ef database update` was NOT run.** |
| `dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~UserManagement\|FullyQualifiedName~GetMyPermissions\|FullyQualifiedName~Auth\|FullyQualifiedName~RoleCode"` | **PASS** — Failed: 0, Passed: 212, Skipped: 0. |

Migration `Up()` confirmed by inspection: creates `lines_of_business` (unique `LOB_Name` index +
3 seed rows `…-0002-…01/02/03`), drops the old `LineOfBusiness`/`ProgramName` string columns and
their indexes on the three satellites, adds `LineOfBusinessId`/`ProgramId` Guid columns, recreates
the composite indexes against the new columns, adds the FK + single-column indexes, and adds the
three `AddForeignKey` calls with `ReferentialAction.Restrict`.

No running host locked the bin DLLs; no MSB3027 encountered.

## Frontend (`D:\Projects\Finnova\Finnova-UI`, branch `main`)

| Command | Result |
| --- | --- |
| `npm run build` (`tsc -b && vite build`) | **PASS** — type-check clean, production build emitted. Only the pre-existing >500 kB chunk-size advisory was printed (not an error). |
| `npm test -- --run src/services/real/userManagement.real.test.ts src/components/userManagement/AccessStep.test.tsx src/components/userManagement/AccessRightsGrid.test.tsx src/components/userManagement/UserManagementWizard.test.tsx src/services/lobProgram.service.test.ts src/services/mock/userManagement.mock.test.ts` | **PASS** — 6 files, 20 tests passed. |

## Scope notes for the reviewer

- `RoleCenterCatalog` was repointed to real `programs.ProgramName` keys (System Admin →
  LookupMaster/NationalityMaster/UserManagement; Origination →
  EntityMaster/LocationMaster/Organization) per plan Assumption 1, so every emitted program resolves
  to a seeded `programs` row and `RoleCode` stays deterministic.
- `RoleCode` parity is enforced server-side: `SaveUserAccess` resolves `ProgramId → ProgramName`
  from the active-programs lookup before calling `RoleCodeBuilder` (plan Assumption 3). `RoleCodeBuilder`
  itself was not touched; property P2 still passes.
- `GetMyPermissions` now groups/joins by `ProgramId` and emits the resolved `ScreenProgram.ProgramName`,
  keeping the UI gating string identical. `useScreenPermissions` gating is untouched.
- `getLinesOfBusiness` moved to the new `lobProgramService` (plan Assumption 5); the `/user/ref/programs`
  GET is new. No API-gateway route was added (the `/api/ua/api/user/**` alias already covers both).
