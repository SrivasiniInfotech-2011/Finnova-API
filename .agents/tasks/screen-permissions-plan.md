# Implementation Plan — Per-Screen (Per-Program) Permission Gating (two repos)

Backend repo: `d:\Projects\Finnova\Finnova-API` (.NET solution `Finnova.Backend.slnx`, net10.0).
UI repo: `D:\Projects\Finnova\Finnova-UI` (React 18 + TS + MUI + Vite, Vitest + RTL).

## Working-tree / branch constraints (do NOT violate)

- Backend work is DIRECT in `d:\Projects\Finnova\Finnova-API` on branch
  `feature/FINNOVA-10-user-management`. There is a just-finished user-identity refactor present as
  UNCOMMITTED changes (user_accounts is the single user+login table; `UserAccount.UserName`/`Role`;
  auth repointed; migration `20261004161229_UnifyUserIdentity` is untracked). Build on top of that
  state. Do NOT create a backend worktree (a worktree would not contain the uncommitted refactor).
- UI work is on its current branch `main` (it has its own uncommitted/staged user-management work).
  Work directly on that branch.
- Do NOT commit, rebase, or merge in either repo. Committing is left to the user. Do NOT run
  `dotnet ef database update`.

## Key findings from exploration (decisions grounded in code)

- **UA host lacks JWT validation.** `Finnova.UAService/Program.cs` registers MediatR, FluentValidation,
  `JwtSettings`, and `ITokenService` (it *issues* tokens) but has NO `AddAuthentication`/`AddJwtBearer`
  and NO `UseAuthentication()`/`UseAuthorization()`. `SystemAdminService/Program.cs` has the full
  JwtBearer setup (reads config section `Jwt`: Issuer/Audience/SigningKey, `RoleClaimType = ClaimTypes.Role`)
  plus a `SystemAdmin` policy. **Decision: the plan MUST add JwtBearer auth to UA**, mirroring
  SystemAdminService. UA `appsettings.json` already has the matching `Jwt` section, so no config change
  is needed. `TokenService` already emits `ClaimTypes.NameIdentifier = user.Id` and a second
  `ClaimTypes.Role = "SystemAdmin"` for Admin users, so `[Authorize]` + `User.FindFirstValue(ClaimTypes.NameIdentifier)`
  will resolve.
- **Permissions model already exists.** `UserAccessAssignment` (Finnova.Models/Domain/Entities) has
  `UserAccountId`, `UserGroupId`, `LineOfBusiness`, `RoleCenterName`, `ProgramName`, `RoleCode`,
  `CanAdd/CanModify/CanQuery/CanDelete`. `IUserManagementRepository` has `GetUserWithAccessAsync(Guid)`
  (Includes AccessAssignments) and `GetByUserNameAsync`. There may be multiple rows per ProgramName
  (different LOB/RoleCenter), so the handler must collapse by OR-ing flags.
- **No program/screen registry exists today.** New entity required.
- **Service layer uses MediatR Commands; no `Queries/` folder yet.** `LoginCommandHandler` is the
  pattern to mirror (handler takes `IUserManagementRepository`). We add a query under
  `Finnova.Service/Auth/Queries/GetMyPermissions/`.
- **EF config pattern**: `IEntityTypeConfiguration<T>` in `Finnova.Repository/Configuration/`,
  `ToTable(...)`, `HasIndex(...).IsUnique()`, `HasData(...)` with deterministic GUIDs + a fixed
  `new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc)`. DbContext uses `ApplyConfigurationsFromAssembly`,
  so only a `DbSet` needs adding. EF startup project is `Finnova.SystemAdminService`.
- **Test harness caveat.** `Finnova.Tests` references ONLY `Finnova.SystemAdminService` as the host, so
  `WebApplicationFactory<Program>` boots the SystemAdmin host. The new endpoint lives on the **UA** host
  (`AuthController` in `Finnova.UAService`). Both hosts declare `public partial class Program` in the
  global namespace, so simply adding a `Finnova.UAService` project reference makes `WebApplicationFactory<Program>`
  ambiguous (two identical `Program` types) and will not compile cleanly. **Decision: cover the endpoint
  logic with a MediatR handler unit test (collapse/OR + Admin bypass) as the REQUIRED test**, which needs
  no host. A full `WebApplicationFactory` integration test against the UA host is a documented OPTIONAL
  follow-up (would require a dedicated UA test host assembly or renaming one host's `Program`); do not block
  on it.
- **UI patterns**: shared axios `src/services/api.ts` (baseURL `VITE_API_BASE_URL||'/api'`, injects Bearer,
  central 401/403). Service feature = `interfaces/<f>.interface.ts` + `mock/<f>.mock.ts` +
  `real/<f>.real.ts` + `<f>.service.ts` toggling on `import.meta.env.VITE_USE_MOCK_API`, with barrels
  `services/index.ts`, `services/interfaces/index.ts`, `models/index.ts`. Provider tree is in
  `src/App.tsx` (`ThemeProvider > AuthProvider > NotificationProvider > BrowserRouter`), NOT main.tsx.
  Auth state via `useAuth()` (`src/context/AuthContext.tsx`). Master pages own the Add button
  (`disabled={!canQuery}` today) and render a grid component that owns Edit/Delete icon buttons (see
  `LookupGrid.tsx`, disabled by `isSystemLocked`). Forms own the Save button (`LookupForm.tsx`).
  Vitest config in `vite.config.ts` (jsdom, globals, setup `src/test/setup.ts`); scripts: `npm test`
  (= `vitest run`) and `npm run build` (= `tsc -b && vite build`).
- **Cross-repo contract**: backend uses System.Text.Json default camelCase, so JSON is
  `{ isAdmin, programs: [{ programName, canAdd, canModify, canQuery, canDelete }] }`. UI model field
  names MUST match exactly.
- **Login contract mismatch**: UI `LoginRequest` sends `{ email, password }` but backend now expects
  `{ userName, password }`. **Decision: fix the UI** (small, and login is otherwise broken end-to-end).

---

## BACKEND (direct in `d:\Projects\Finnova\Finnova-API`, branch `feature/FINNOVA-10-user-management`)

- [ ] 1. Create the `ScreenProgram` entity (the screen/program master).
      Named `ScreenProgram` (NOT `Program`, which clashes with the hosts' `public partial class Program`).
      Fields: `Guid Id`, `string ProgramName` (unique screen key), `string DisplayName`, `string? Module`,
      `bool IsActive`, `DateTime CreatedAt`, `DateTime UpdatedAt`.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\Entities\ScreenProgram.cs`
      Verify: `dotnet build Finnova.Models/Finnova.Models.csproj` succeeds.

- [ ] 2. Create EF config `ScreenProgramConfiguration` and register the DbSet.
      `ToTable("programs")`, `HasKey(Id)`, `Property(ProgramName).IsRequired().HasMaxLength(100)`,
      `Property(DisplayName).IsRequired().HasMaxLength(150)`, `Property(Module).HasMaxLength(100)`,
      `HasIndex(ProgramName).IsUnique()`. `HasData` seed of the nine screens below with DETERMINISTIC
      GUIDs `00000000-0000-0000-0001-0000000000NN` (NN = 01..09) and fixed date
      `new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc)` for CreatedAt/UpdatedAt, all `IsActive = true`:
      LookupMaster / "Lookup Master" / "SystemAdmin";
      NationalityMaster / "Nationality Master" / "SystemAdmin";
      OrganizationHierarchy / "Organization Hierarchy" / "SystemAdmin";
      DocumentNumberControl / "Document Number Control" / "SystemAdmin";
      CourtMaster / "Court Master" / "SystemAdmin";
      EntityMaster / "Entity Master" / "SystemAdmin";
      UserManagement / "User Management" / "SystemAdmin";
      LocationMaster / "Location Master" / "SystemAdmin";
      Organization / "Organization" / "SystemAdmin".
      Add `public DbSet<ScreenProgram> ScreenPrograms => Set<ScreenProgram>();` to `FinnovaDbContext`.
      Files: `Finnova.Repository\Configuration\ScreenProgramConfiguration.cs`,
      `Finnova.Repository\Context\FinnovaDbContext.cs` (add DbSet only; it already uses
      `ApplyConfigurationsFromAssembly`).
      Verify: `dotnet build Finnova.Repository/Finnova.Repository.csproj` succeeds.

- [ ] 3. Add the permissions response contracts.
      `record ScreenPermissionResponse(string ProgramName, bool CanAdd, bool CanModify, bool CanQuery, bool CanDelete);`
      and `record MyPermissionsResponse(bool IsAdmin, IReadOnlyList<ScreenPermissionResponse> Programs);`
      Files: `Finnova.Models\Contracts\Auth\ScreenPermissionResponse.cs` (both records may live in one file
      or split; keep namespace `Finnova.Models.Contracts.Auth`).
      Verify: `dotnet build Finnova.Models/Finnova.Models.csproj` succeeds.

- [ ] 4. Add a repository method to load a user's access rows and the active program registry.
      Add to `IUserManagementRepository` + `UserManagementRepository`:
      `Task<List<UserAccessAssignment>> GetAccessAssignmentsByUserAsync(Guid userId, CancellationToken ct = default)`
      (`Context.UserAccessAssignments.AsNoTracking().Where(x => x.UserAccountId == userId)`), and
      `Task<List<ScreenProgram>> GetActiveProgramsAsync(CancellationToken ct = default)`
      (`Context.ScreenPrograms.AsNoTracking().Where(x => x.IsActive)`).
      Files: `Finnova.Repository\Interfaces\IUserManagementRepository.cs`,
      `Finnova.Repository\Repositories\UserManagementRepository.cs`
      Verify: `dotnet build Finnova.Repository/Finnova.Repository.csproj` succeeds.

- [ ] 5. Add the MediatR query + handler for the current user's permissions.
      `GetMyPermissionsQuery(Guid UserId, bool IsAdmin) : IRequest<MyPermissionsResponse>` and its handler.
      Handler logic: if `IsAdmin` → return `MyPermissionsResponse(true, <every active program with all four
      flags true>)` from `GetActiveProgramsAsync`. Else: load `GetAccessAssignmentsByUserAsync(UserId)`,
      group rows by `ProgramName`, OR the four flags across each group (granted if ANY row grants it),
      then INNER-JOIN against active program names (`GetActiveProgramsAsync`) so only registered programs
      are returned and programs with no rows are OMITTED. Mirror `LoginCommandHandler` DI style
      (inject `IUserManagementRepository`).
      Files: `Finnova.Service\Auth\Queries\GetMyPermissions\GetMyPermissionsQuery.cs`,
      `Finnova.Service\Auth\Queries\GetMyPermissions\GetMyPermissionsQueryHandler.cs`
      Verify: `dotnet build Finnova.Service/Finnova.Service.csproj` succeeds.

- [ ] 6. Add JwtBearer authentication to the UA host (REQUIRED — it is currently missing).
      Mirror `SystemAdminService/Program.cs`: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(...)` reading config section `Jwt` (Issuer/Audience/SigningKey) with
      `ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true` and `RoleClaimType = ClaimTypes.Role`;
      `AddAuthorization()`; and insert `app.UseAuthentication(); app.UseAuthorization();` BEFORE
      `app.MapControllers()`. UA `appsettings.json` already has the matching `Jwt` section — no config edit.
      Files: `Finnova.UAService\Program.cs`
      Verify: `dotnet build Finnova.UAService/Finnova.UAService.csproj` succeeds.

- [ ] 7. Add the authorized endpoint `GET /api/auth/me/permissions` to `AuthController`.
      `[Authorize]` on THIS action only (login/logout/refresh/change-password stay anonymous). Resolve
      the user id from `User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")`;
      if unparseable → `Unauthorized()`. Compute `isAdmin` = principal is in role `UserRole.Admin.ToString()`
      ("Admin") OR `User.IsInRole("SystemAdmin")`. Send `new GetMyPermissionsQuery(userId, isAdmin)` and
      return `Ok(result)`.
      Files: `Finnova.UAService\Controllers\AuthController.cs`
      Verify: `dotnet build Finnova.UAService/Finnova.UAService.csproj` succeeds.

- [ ] 8. Add the EF migration for the new table + seed (do NOT apply).
      Command (run from backend repo root):
      `dotnet ef migrations add AddScreenProgramsAndPermissions --project Finnova.Repository/Finnova.Repository.csproj --startup-project Finnova.SystemAdminService/Finnova.SystemAdminService.csproj`
      Review the generated `Up()` for: `CreateTable("programs")`, the unique index on ProgramName, and
      nine `InsertData` seed rows. Do NOT run `database update`.
      Files: new `Finnova.Repository\Migrations\<timestamp>_AddScreenProgramsAndPermissions.cs` (+ Designer)
      and updated `FinnovaDbContextModelSnapshot.cs`.
      Verify: `dotnet build Finnova.Backend.slnx` succeeds and the migration file exists with the table +
      seed.

- [ ] 9. Add the handler unit test (REQUIRED) for the collapse/OR + Admin bypass logic.
      Use xUnit + Moq (both already referenced). Mock `IUserManagementRepository`. Cases: (a) Admin →
      every active program present with all four flags true; (b) non-admin with two rows for the same
      ProgramName having complementary flags → one collapsed entry with OR'd flags; (c) a row whose
      ProgramName is not in the active registry is omitted; (d) a program the user has no rows for is
      omitted. Mirror existing unit-test style in `Finnova.Tests`.
      Files: `Finnova.Tests\Unit\GetMyPermissionsQueryHandlerTests.cs` (place under an existing test folder
      consistent with the project; `Unit/` or alongside similar handler tests).
      Verify: `dotnet test Finnova.Tests/Finnova.Tests.csproj` passes (new tests green, no regressions).
      NOTE: a `WebApplicationFactory` integration test against the UA host is an OPTIONAL follow-up because
      `Finnova.Tests` only hosts SystemAdminService and both hosts share `public partial class Program`
      (ambiguous `WebApplicationFactory<Program>`); do not block on it.

- [ ] 10. Build and test the whole backend solution.
      Verify: `dotnet build Finnova.Backend.slnx` is green AND
      `dotnet test Finnova.Tests/Finnova.Tests.csproj` passes.

---

## FRONTEND (direct in `D:\Projects\Finnova\Finnova-UI`, current branch `main`)

- [ ] 11. Add the permission model.
      `export interface ScreenPermission { programName: string; canAdd: boolean; canModify: boolean;
      canQuery: boolean; canDelete: boolean; }` and
      `export interface MyPermissions { isAdmin: boolean; programs: ScreenPermission[]; }`
      (field names MUST match backend camelCase JSON). Add `export * from './permission.model';` to the
      models barrel.
      Files: `D:\Projects\Finnova\Finnova-UI\src\models\permission.model.ts`,
      `D:\Projects\Finnova\Finnova-UI\src\models\index.ts`
      Verify: `npm run build` (tsc) — no type errors.

- [ ] 12. Add the permission service (interface + real + mock + toggle) and update barrels.
      `interfaces/permission.interface.ts`: `export interface IPermissionService { getMyPermissions():
      Promise<MyPermissions>; }`.
      `real/permission.real.ts`: `api.get<MyPermissions>('/auth/me/permissions')` returning `response.data`
      (reuses shared axios; the Bearer is injected automatically).
      `mock/permission.mock.ts`: `isAdmin: false` with a couple of programs with mixed flags (e.g.
      `LookupMaster` all true; `CourtMaster` canQuery true, others false). Respect `VITE_USE_MOCK_API`.
      `permission.service.ts`: `export const permissionService: IPermissionService = useMock ?
      permissionMockService : permissionRealService;`.
      Update barrels: add to `services/index.ts`, `services/interfaces/index.ts`.
      Files: `src\services\interfaces\permission.interface.ts`, `src\services\real\permission.real.ts`,
      `src\services\mock\permission.mock.ts`, `src\services\permission.service.ts`,
      `src\services\index.ts`, `src\services\interfaces\index.ts`
      Verify: `npm run build` — no type errors.

- [ ] 13. Add `PermissionsContext` and wire it into the provider tree.
      `src/context/PermissionsContext.tsx`: on `useAuth().isAuthenticated` true, call
      `permissionService.getMyPermissions()` (in `useEffect`), store `MyPermissions | null` and a loading
      flag; clear on logout / when not authenticated. Expose `usePermissions()` returning
      `{ permissions, loading, can(programName, action: 'add'|'modify'|'query'|'delete'): boolean }` where
      `can` returns `true` if `isAdmin`, else the matching flag of the program entry, else `false`
      (absent program ⇒ false). Wire the provider in `src/App.tsx` INSIDE `AuthProvider`
      (e.g. `AuthProvider > PermissionsProvider > NotificationProvider`) so it can read auth state.
      Files: `src\context\PermissionsContext.tsx`, `src\App.tsx`
      Verify: `npm run build` — no type errors.

- [ ] 14. Add the `useScreenPermissions(programName)` hook.
      Returns `{ canAdd, canModify, canQuery, canDelete }` by calling `usePermissions().can(programName, ...)`
      for each action (Admin ⇒ all true via `can`). Thin convenience wrapper.
      Files: `src\hooks\useScreenPermissions.ts`
      Verify: `npm run build` — no type errors.

- [ ] 15. Gate the master pages and forms (one coherent change across the listed files).
      ProgramName map (page → ProgramName): `LookupMaster→"LookupMaster"`, `CourtMaster→"CourtMaster"`,
      `DcnMaster→"DocumentNumberControl"`, `EntityMaster→"EntityMaster"`,
      `NationalityMaster→"NationalityMaster"`, `OrgHierarchyMaster→"OrganizationHierarchy"`,
      `LocationMaster→"LocationMaster"`, `UserManagementMaster→"UserManagement"`.
      In each master page: `const { canAdd, canModify, canDelete } = useScreenPermissions("<ProgramName>");`
      then disable Add via `disabled={<existing condition> || !canAdd}` (keep existing gating like
      `!canQuery` and AND it). Pass `canModify`/`canDelete` down to the grid component so its Edit/Delete
      icon buttons add `disabled={... || !canModify}` / `disabled={... || !canDelete}` (preserve the
      existing `isSystemLocked` disabling with AND/OR as appropriate). In each `*Form.tsx`: compute the
      effective permission (`canAdd` for create mode, `canModify` for edit mode) and set the Save button
      `disabled={submitting || !effectiveCan}`. Minimal, consistent edits — do NOT restructure pages.
      Files (pages): `src\pages\LookupMaster.tsx`, `LookupForm.tsx`, `CourtMaster.tsx`, `CourtForm.tsx`,
      `DcnMaster.tsx`, `DcnForm.tsx`, `EntityMaster.tsx`, `EntityForm.tsx`, `NationalityMaster.tsx`,
      `NationalityForm.tsx`, `OrgHierarchyMaster.tsx`, `OrgHierarchyForm.tsx`, `LocationMaster.tsx`,
      `LocationForm.tsx`, `UserManagementMaster.tsx`, `UserManagementForm.tsx`.
      Files (grids that own Edit/Delete): the matching components under `src\components\<feature>\*Grid.tsx`
      (e.g. `components\lookup\LookupGrid.tsx`, `components\court\*Grid.tsx`, etc.) — add `canModify`/
      `canDelete` props and apply to the icon buttons.
      Verify: `npm run build` — no type errors.

- [ ] 16. Fix the login contract: `email` → `userName`.
      Update `LoginRequest` to `{ userName: string; password: string }` in `models/auth.model.ts`; update
      the mock (`auth.mock.ts` keys off userName) and the `Login.tsx` field (state + TextField label/value
      + the `authService.login({ userName, password })` call + the mock-user fallback). Keep behavior
      otherwise unchanged.
      Files: `src\models\auth.model.ts`, `src\services\mock\auth.mock.ts`, `src\pages\Login.tsx`
      Verify: `npm run build` — no type errors.

- [ ] 17. Add frontend tests (Vitest + RTL).
      (a) Unit-test `usePermissions`/`can()`: admin ⇒ true for any program/action; non-admin ⇒ flag value;
      absent program ⇒ false. (b) Unit-test the permission service (mock returns the fixture; real calls
      `/auth/me/permissions` with a mocked axios). (c) Component-test `LookupMaster`: Add disabled when
      `canAdd` false and enabled when true (wrap render with a `PermissionsContext` provider or mock
      `usePermissions`). (d) Component-test one `*Form` (e.g. `LookupForm`): Save disabled without
      permission. Mirror existing `*.test.tsx` (mock the services barrel; use `MemoryRouter`).
      Files: `src\context\PermissionsContext.test.tsx` (or `src\hooks\useScreenPermissions.test.ts`),
      `src\services\permission.service.test.ts` (and/or `real/permission.real.test.ts`),
      additions to `src\pages\LookupMaster.test.tsx`, `src\pages\LookupForm.test.tsx`.
      Verify: `npm test -- --run` passes AND `npm run build` is green.

---

## Cross-repo parity checklist (verify during item 12/17)

- Wire JSON `{ isAdmin, programs: [{ programName, canAdd, canModify, canQuery, canDelete }] }`
  (System.Text.Json default camelCase on the backend) matches the UI `MyPermissions`/`ScreenPermission`
  field names exactly.
- The eight gated ProgramName strings exactly equal the backend seed `ProgramName` values.

## Commands summary

- Backend build: `dotnet build Finnova.Backend.slnx`
- Backend test: `dotnet test Finnova.Tests/Finnova.Tests.csproj`
- Backend migration (no apply): `dotnet ef migrations add AddScreenProgramsAndPermissions --project Finnova.Repository/Finnova.Repository.csproj --startup-project Finnova.SystemAdminService/Finnova.SystemAdminService.csproj`
- UI build: `npm run build`  (in `D:\Projects\Finnova\Finnova-UI`)
- UI test: `npm test -- --run`  (in `D:\Projects\Finnova\Finnova-UI`)

NOTHING is committed in either repo; committing is left to the user.
