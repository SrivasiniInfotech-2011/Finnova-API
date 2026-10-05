# Implementation Plan — Branch Access LocationId FK (two repos)

Two repositories change in lockstep. Implement backend first (it defines the wire shape), then the
frontend implements to that shape verbatim. Run the full build/tests ONCE at the end (final item),
not after each step.

- BACKEND: `d:\Projects\Finnova\Finnova-API` (branch `feature/FINNOVA-10-user-management`), .NET `Finnova.Backend.slnx`, net10.0.
- FRONTEND: `D:\Projects\Finnova\Finnova-UI` (branch `main`), React18 + TS + MUI + Vite, Vitest + RTL.

## LOCKED DECISIONS (do not re-litigate)

1. `UserBranchAssociation` keeps `BranchCode` (string, NON-authoritative reference) and `IsAll` (bool),
   and ADDS `LocationId` (`Guid?`, NULLABLE) as the authoritative FK to the existing `locations` table
   (entity `Location`, table `"locations"`). `IsAll==true` ⇒ `LocationId == null`, `BranchCode == "ALL"`.
   `IsAll==false` ⇒ `LocationId` is set and must reference a real `locations` row; `BranchCode` is filled
   from that location's `Code` for display.
2. Branch validation at SaveUserAccess = **existence only**. For each non-ALL selection the submitted
   `LocationId` must exist in `locations`. Do NOT check `IsActive`. Do NOT check `Level`. ALL rows skip
   location validation. Missing/non-existent `LocationId` ⇒ throw `UserValidationException` (already maps
   to ERR-USR-400 — no middleware change needed).
3. Relabel ONLY in the UI (no entity/table/program-key/route/service/file/ProgramName change). The screen
   title (`LocationMaster.tsx`) and nav label (`Layout.tsx`) ALREADY read "Branch Master" — verify and make
   the remaining user-visible headings/labels consistent (see item 12). ProgramName gating stays `LocationMaster`.
4. Wire key = `Id` (Guid). The branch picker sends `locationId` (Guid) per selected branch plus the ALL
   sentinel. Labels come from the location name/code.

## PINNED BRANCH WIRE SHAPE (camelCase JSON — frontend implements to this verbatim)

A single branch selection, in BOTH the SaveUserAccess request and the GetUserAccess response:

```json
{ "locationId": "<guid|null>", "isAll": <bool>, "branchCode": "<string>" }
```

- Request field renamed: `SaveUserAccessRequest.BranchCodes: string[]` → `Branches: BranchSelection[]`.
  On the wire the array property is `branches`. For a selection the caller sends `locationId` + `isAll`;
  `branchCode` is optional on input (server resolves/overwrites it from the location's `Code`; ALL ⇒ "ALL").
- Response field renamed: `UserAccessResponse.BranchCodes: string[]` → `Branches: BranchSelection[]`,
  each element fully populated (`locationId`, `isAll`, `branchCode`).
- ALL selection on the wire: `{ "locationId": null, "isAll": true, "branchCode": "ALL" }`.
- Branch-tree node (`BranchTreeNodeResponse`) gains `id` (string Guid; `null`/empty for ALL and non-leaf
  container nodes). On the wire: `{ "id": "<guid|null>", "code": "...", "name": "...", "level": "...", "children": [...] }`.

`BranchSelection` record (C#): `public record BranchSelection(Guid? LocationId, bool IsAll, string BranchCode = "");`
Default System.Text.Json camelCase (used across the API) serializes these as `locationId`/`isAll`/`branchCode`.

---

## BACKEND (dependency order)

- [ ] 1. Add `Guid? LocationId` to the `UserBranchAssociation` entity (keep `BranchCode` and `IsAll`;
      update the XML doc to note `LocationId` is the authoritative FK and `BranchCode` is reference-only).
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Models\Domain\Entities\UserBranchAssociation.cs`
      Verify: compiles as part of the final build (item 17); no standalone build.

- [ ] 2. Configure the new FK + index in `UserBranchAssociationConfiguration`:
      add `b.Property(x => x.LocationId);`, an optional FK
      `b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);`
      (Restrict, matching the LOB FK already there), and `b.HasIndex(x => x.LocationId);`. Keep the existing
      LOB FK, the two composite indexes, and the `CK_user_branch_associations_owner` check constraint unchanged.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Configuration\UserBranchAssociationConfiguration.cs`
      Verify: part of final build; the migration in item 7 will reflect this config.

- [ ] 3. Add the `BranchSelection` record and rebind the contracts. In `UserManagementRequests.cs`:
      add `public record BranchSelection(Guid? LocationId, bool IsAll, string BranchCode = "");` and change
      `SaveUserAccessRequest` to carry `IReadOnlyList<BranchSelection> Branches` instead of
      `IReadOnlyList<string> BranchCodes`. In `UserManagementResponses.cs`: change `UserAccessResponse` to
      carry `IReadOnlyList<BranchSelection> Branches` instead of `IReadOnlyList<string> BranchCodes`, and add
      `string? Id` as the FIRST positional member of `BranchTreeNodeResponse`
      (`record BranchTreeNodeResponse(string? Id, string Code, string Name, string Level, IReadOnlyList<BranchTreeNodeResponse> Children)`).
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Models\Contracts\UserManagement\UserManagementRequests.cs`,
      `d:\Projects\Finnova\Finnova-API\Finnova.Models\Contracts\UserManagement\UserManagementResponses.cs`
      Verify: part of final build; downstream items 4-10 consume these types.

- [ ] 4. Add a location-existence/code lookup to the repository. On `IUserManagementRepository` add
      `Task<Location?> GetLocationByIdAsync(Guid id, CancellationToken ct = default);` (returns the row so the
      handler can both check existence AND read `Code`). Implement in `UserManagementRepository` via
      `Context.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)` (do NOT filter on `IsActive`).
      Add the matching member to the test double `InMemoryUserManagementRepository` backed by a new public
      `List<Location> Locations = new();` returning `Locations.FirstOrDefault(l => l.Id == id)`.
      (Reusing `ILocationRepository.GetByIdAsync` is possible but would add a second repo dependency to the
      handler; a single method on the existing repo keeps the handler's one-dependency shape and is simpler to fake.)
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Interfaces\IUserManagementRepository.cs`,
      `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Repositories\UserManagementRepository.cs`,
      `d:\Projects\Finnova\Finnova-API\Finnova.Tests\Infrastructure\InMemoryUserManagementRepository.cs`
      Verify: part of final build + tests (item 17).

- [ ] 5. Update `SaveUserAccessCommand` to carry branches as selections: change
      `IReadOnlyList<string> BranchCodes` → `IReadOnlyList<BranchSelection> Branches`.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Commands\SaveUserAccess\SaveUserAccessCommand.cs`
      Verify: part of final build.

- [ ] 6. Rework `SaveUserAccessCommandHandler` branch handling and the Copy-Profile merge:
      - Replace the `branches` list (strings) with the command's `Branches` (selections).
      - For each selection build a `UserBranchAssociation`: if `IsAll` ⇒ `IsAll=true`, `LocationId=null`,
        `BranchCode="ALL"`; else resolve via `GetLocationByIdAsync(selection.LocationId!.Value)` — if
        `LocationId` is null or the location is not found, throw
        `new UserValidationException("The selected branch location was not found.")`; on success set
        `LocationId=location.Id`, `BranchCode=location.Code`, `IsAll=false`.
      - Copy-Profile: `GetAccessAsync` now returns `UserBranchAssociation`s already carrying `LocationId`/`IsAll`/
        `BranchCode`; map source branches to `BranchSelection(b.LocationId, b.IsAll, b.BranchCode)` and merge with
        the request selections (update `AccessAssignmentMerger` per item 6a).
      - Build the returned `UserAccessResponse` with `Branches` = the persisted selections
        (`new BranchSelection(a.LocationId, a.IsAll, a.BranchCode)`), and keep the audit `NewValues`/`Summary`
        branch count working off the selection list.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Commands\SaveUserAccess\SaveUserAccessCommandHandler.cs`
      Verify: part of final build + tests.

- [ ] 6a. Update `AccessAssignmentMerger.Merge` so the branch arm operates on `IEnumerable<BranchSelection>`
      instead of `IEnumerable<string>`, de-duplicating by a stable key (`IsAll` ⇒ key "ALL"; else the
      `LocationId` Guid). Return `List<BranchSelection>`. Keep the rows arm (RoleCode OR-merge) unchanged and
      keep it pure/commutative/idempotent (property tests P3/P4 must still hold — adjust them in item 9 if their
      branch generators used strings).
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Helpers\AccessAssignmentMerger.cs`
      Verify: part of final build + tests.

- [ ] 7. Generate the EF migration (schema only; NULLABLE column + FK + index on `user_branch_associations`).
      Table currently holds NO seeded rows (verified: no `HasData`/`InsertData` targets `user_branch_associations`
      in any migration or the snapshot), so NO backfill. **Do NOT run `database update`.**
      Command (run from the backend repo root):
      `dotnet ef migrations add AddBranchAccessLocationFk --project Finnova.Repository/Finnova.Repository.csproj --startup-project Finnova.SystemAdminService/Finnova.SystemAdminService.csproj`
      If a running host locks `bin` DLLs, EF fails with MSB3027/MSB3021 (file in use) — stop the host (or the
      watch process) and re-run; note this in findings rather than forcing.
      Files (generated): `d:\Projects\Finnova\Finnova-API\Finnova.Repository\Migrations\<timestamp>_AddBranchAccessLocationFk.cs`
      (+ `.Designer.cs`) and an updated `FinnovaDbContextModelSnapshot.cs`.
      Verify: the generated `Up` adds a nullable `LocationId` column, an FK to `locations`
      (`ReferentialAction.Restrict`), and `IX_user_branch_associations_LocationId`; the snapshot now shows the
      property/FK/index. (DB is NOT updated.)

- [ ] 8. Make the branch-location tree location-backed and expose each node's `Id`. Replace the hardcoded
      `BranchTreeCatalog.Tree()` consumption in `GetBranchLocationTreeQueryHandler` with a tree built from
      `ILocationRepository.GetAllFlatAsync(ct)`: inject `ILocationRepository` into the handler, build the
      hierarchy in memory from the flat list (parent/child via `ParentId`), map `Level` int → the response's
      string level (1=Country,2=State,3=City,4=Zone,5=Branch — keep the existing response `Level` vocabulary;
      map 5→"Branch", others→"Region", country→"Location" to preserve current UI semantics), set each node's
      `Id` to the location's Guid string, and prepend the ALL root
      `new BranchTreeNodeResponse(null, "ALL", "ALL", "Location", [])`. Container (non-leaf) nodes may carry their
      own `Id` but the UI only sends leaf (`children empty`) + ALL. Keep `GetBranchLocationTreeQuery(string LineOfBusiness)`
      signature. `BranchTreeCatalog` may be deleted or left unused — prefer deleting it and its only reference.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Queries\References\ReferenceQueryHandlers.cs`
      (and delete `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Internal\BranchTreeCatalog.cs` if unreferenced).
      Verify: part of final build + tests (reference-query tests, if any, assert nodes carry `Id`).

- [ ] 9. Update `GetUserAccessQueryHandler` to project branches as selections:
      change the final `branches.Select(b => b.BranchCode)` to
      `branches.Select(b => new BranchSelection(b.LocationId, b.IsAll, b.BranchCode)).ToList()` and pass as the
      response `Branches`. Update the controller `SaveAccess` action to pass `r.Branches` into
      `SaveUserAccessCommand` (rename of the positional arg from `r.BranchCodes`).
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Queries\GetUserAccess\GetUserAccessQueryHandler.cs`,
      `d:\Projects\Finnova\Finnova-API\Finnova.SystemAdminService\Controllers\UserManagementController.cs`
      Verify: part of final build + tests.

- [ ] 10. Update the backend validator. In `SaveUserAccessCommandValidator` rename the `BranchCodes` rule to
      `Branches` (`RuleFor(x => x.Branches).NotEmpty()...`), keep the "at least one branch" message. Optionally add
      a `RuleForEach(x => x.Branches)` requiring `LocationId` non-null when `!IsAll` (the handler already enforces
      existence; a validator rule gives the ERR-USR-400 path for the null case too — keep the message generic).
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Service\UserManagement\Commands\SaveUserAccess\SaveUserAccessCommandValidator.cs`
      Verify: part of final build + tests.

- [ ] 11. Update backend tests to the new shape. In the authorization integration test, change the "PUT access"
      request body `BranchCodes = new[] { "ALL" }` to `Branches = new[] { new { locationId = (Guid?)null, isAll = true, branchCode = "ALL" } }`.
      In `InMemoryUserManagementRepository` ensure the new `Locations` list + `GetLocationByIdAsync` are present
      (item 4) and seed at least one Level-5 location in any SaveUserAccess unit/handler test that exercises a
      non-ALL branch so existence passes; add a test asserting a non-existent `LocationId` ⇒ `UserValidationException`.
      Adjust any Copy-Profile / merger property tests that generated string branches to generate `BranchSelection`s.
      Files: `d:\Projects\Finnova\Finnova-API\Finnova.Tests\Integration\UserManagementAuthorizationTests.cs`,
      `d:\Projects\Finnova\Finnova-API\Finnova.Tests\Infrastructure\InMemoryUserManagementRepository.cs`,
      and any `Finnova.Tests` files under UserManagement/SaveUserAccess that reference `BranchCodes` (search
      `BranchCodes`, `UserBranchAssociation`, `AccessAssignmentMerger` and update each).
      Verify: part of final test run (item 17).

---

## FRONTEND (dependency order — implement after the backend wire shape is fixed)

- [ ] 12. Relabel-only pass (decision 3). The screen title in `LocationMaster.tsx` (line ~139) and the nav label
      in `Layout.tsx` (line ~109) ALREADY read "Branch Master" — verify them. Change the remaining user-visible
      "Location" display strings on this screen to "Branch" for consistency: the "Add Location" button, the
      "{n} location(s) found" counter, the "Delete Location" dialog title, and the "remove all child locations"
      body text. Do NOT touch imports, component/variable/function names, routes (`/locations`), the
      `useScreenPermissions('LocationMaster')` key, service names, or file names.
      Files: `D:\Projects\Finnova\Finnova-UI\src\pages\LocationMaster.tsx`,
      `D:\Projects\Finnova\Finnova-UI\src\components\Layout.tsx`
      Verify: part of final frontend build + tests (item 18); existing LocationMaster/Layout tests still pass.

- [ ] 13. Update the frontend model types. In `userManagement.model.ts`:
      - Add `export interface BranchSelection { locationId: string | null; isAll: boolean; branchCode: string; }`.
      - Add `id: string | null` to `BranchTreeNode` (first field): `{ id: string | null; code: string; name: string; level: 'Location' | 'Region' | 'Branch'; children: BranchTreeNode[]; }`.
      - Replace `branchCodes: string[]` with `branches: BranchSelection[]` in BOTH `SaveUserAccessData` and `UserAccess`.
      Files: `D:\Projects\Finnova\Finnova-UI\src\models\userManagement.model.ts`
      Verify: part of final build (`tsc -b` surfaces every consumer that still uses `branchCodes`).

- [ ] 14. Update the service layer (interface + real + mock) for the new shapes.
      - `interfaces/userManagement.interface.ts`: no signature change needed (types flow through the models), but
        confirm `saveAccess`/`getAccess`/`getBranchTree` reference the updated types.
      - `real/userManagement.real.ts`: no body change (passes `d` through / returns server JSON); confirm generic
        types resolve. The save payload now carries `branches` because `SaveUserAccessData` changed.
      - `mock/userManagement.mock.ts`: update `getBranchTree` to include `id` on each node (use the real seeded
        location Guids: ALL ⇒ `id:null`; set leaf branch nodes to real Level-5 ids, e.g. Fort Branch
        `00000000-0000-0000-0000-000000000010`, container nodes `id:null`); update `getAccess` default and
        `saveAccess` to use `branches: BranchSelection[]` (validate `!d.branches?.length` ⇒ ERR-USR-400) and
        echo them back on the `UserAccess` result.
      Files: `D:\Projects\Finnova\Finnova-UI\src\services\interfaces\userManagement.interface.ts`,
      `D:\Projects\Finnova\Finnova-UI\src\services\real\userManagement.real.ts`,
      `D:\Projects\Finnova\Finnova-UI\src\services\mock\userManagement.mock.ts`
      Verify: part of final build + service specs (`userManagement.mock.test.ts`, `userManagement.real.test.ts`).

- [ ] 15. Update the wizard draft + context to carry selections. In `useUserManagementDraft.ts` change
      `DraftAccess.branchCodes?: string[]` → `branches?: BranchSelection[]` (import `BranchSelection`). In
      `WizardContext.tsx` change the two `access: { rows: [], branchCodes: [] }` initializers (initialState +
      the `setConfiguration` reset) to `{ rows: [], branches: [] }`.
      Files: `D:\Projects\Finnova\Finnova-UI\src\hooks\useUserManagementDraft.ts`,
      `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\WizardContext.tsx`
      Verify: part of final build; draft hook test still passes.

- [ ] 16. Update the branch picker + Access/Review steps + submit payload to use `locationId`.
      - `BranchLocationTree.tsx`: selection key becomes the node's `locationId` for leaf branches and the ALL
        sentinel for the ALL node. Change `Props`/`NodeProps` to `selected: BranchSelection[]` and
        `onToggle: (node: BranchTreeNode) => void` (or pass `{ locationId, isAll }`); compute `checked` by matching
        `isAll` for the ALL node else `locationId === node.id`; keep the `branch-<code>` aria-label (tests rely on it).
        Only ALL and leaf nodes (`children.length === 0`) are selectable — render container nodes as non-interactive
        labels (or disabled checkboxes) since they have no `locationId`.
      - `AccessStep.tsx`: replace `branchCodes`/`toggleBranch(code)` with a `branches: BranchSelection[]` state via
        `patchAccess`. `toggleBranch(node)` adds/removes a `BranchSelection` (`{ locationId: node.id, isAll: node.code==='ALL', branchCode: node.code }`),
        matching on the ALL flag or `locationId`. Update the validity check to `!(access.branches?.length)`.
      - `ReviewStep.tsx`: render branches from `access.branches` (e.g. `.map(b => b.isAll ? 'ALL' : b.branchCode).join(', ')`).
      - `UserManagementWizard.tsx` submit: send `branches: access.branches ?? []` in the `saveAccess` payload
        (replace `branchCodes`).
      Files: `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\BranchLocationTree.tsx`,
      `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\AccessStep.tsx`,
      `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\ReviewStep.tsx`,
      `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\UserManagementWizard.tsx`
      Also update the specs: `BranchLocationTree.test.tsx` (nodes now carry `id`; `onToggle` asserts the selection
      object/node, not the bare code — keep `branch-HO`/`branch-ALL` aria-labels), and `AccessStep.test.tsx`
      (`getBranchTree` mock nodes gain `id`). Keep `useScreenPermissions` gating intact.
      Files (tests): `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\BranchLocationTree.test.tsx`,
      `D:\Projects\Finnova\Finnova-UI\src\components\userManagement\AccessStep.test.tsx`
      Verify: part of final frontend build + tests (item 18).

---

## FINAL VERIFICATION (run ONCE, at the end)

- [ ] 17. Backend build + targeted tests (from `d:\Projects\Finnova\Finnova-API`):
      `dotnet build Finnova.Backend.slnx`
      then
      `dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~UserManagement|FullyQualifiedName~SaveUserAccess|FullyQualifiedName~Auth"`
      Expected: build succeeds; all filtered tests pass. The migration from item 7 exists and compiles; the DB is
      NOT updated. (If the build fails only due to a locked host `bin` DLL during item 7's migration generation,
      resolve the lock and re-run — it is not a code failure.)

- [ ] 18. Frontend build + touched specs (from `D:\Projects\Finnova\Finnova-UI`):
      `npm run build`
      then
      `npm test -- --run src/components/userManagement/BranchLocationTree.test.tsx src/components/userManagement/AccessStep.test.tsx src/components/userManagement/UserManagementWizard.test.tsx src/services/mock/userManagement.mock.test.ts src/services/real/userManagement.real.test.ts src/hooks/useUserManagementDraft.test.ts`
      Expected: `tsc -b && vite build` succeeds (no remaining `branchCodes` references); all listed specs pass.

## Notes / assumptions

- Decision 8 (location-backed branch tree) is a design choice made here: the old `BranchTreeCatalog` used
  invented codes (HO, FORT…) with no `locations` rows, so a `LocationId` sent from those nodes could never pass
  existence validation. Building the tree from the seeded `locations` (which include Level-5 branches with real
  Guids) is the minimal way to give the UI real `LocationId`s to send. If the reviewer prefers keeping the static
  catalog, the alternative is to hardcode real seeded Guids into the catalog nodes — call it out before changing.
- `UserValidationException` already maps to ERR-USR-400 in `ExceptionHandlingMiddleware` — no middleware edit.
- No API gateway change: the `/api/user/**` route is unchanged (same host/controller).
- No seeding of sample user/group/branch rows in this task (explicitly out of scope).
