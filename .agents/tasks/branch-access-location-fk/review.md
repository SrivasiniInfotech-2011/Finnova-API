# Branch access LocationId FK normalization (two repos)

`UserBranchAssociation` gains a nullable `LocationId` FK into the existing `locations` table so branch access is anchored to a real location row instead of a free-text code. `BranchCode` is retained as a reference-only display field (resolved server-side from the location's `Code`), and the `IsAll` sentinel is preserved (`ALL` ⇒ `LocationId == null`, `BranchCode == "ALL"`). The wire contract switches from `string[] BranchCodes` to `BranchSelection[] Branches` (`{ locationId, isAll, branchCode }`) across both the save request and the access response, the branch-location tree is now built from the real locations master so each leaf carries a Guid the UI sends back, and the UI relabels the Location Master screen to "Branch Master" as text only. SaveUserAccess validates each non-ALL selection's `LocationId` for existence only (no IsActive, no Level). The two repos move in lockstep against a pinned camelCase wire shape.

Watch for: nothing blocking. Reviewed against the pinned plan and the recorded build/test evidence; the diff matches the plan item-for-item, cross-repo wire shapes are identical, and the migration is schema-only with no backfill and was not applied. (confirmed)

**Verdict**: APPROVED

## High-level view

The entity and EF configuration add `LocationId` as a nullable FK to `locations` with `OnDelete(Restrict)` and a single-column index, mirroring the existing LOB FK. The owner check constraint and the two composite indexes are untouched, and `BranchCode`/`IsAll` keep their roles. The generated migration adds exactly the column, index, and FK with no `HasData`/backfill, and the evidence states `database update` was not run.

SaveUserAccess resolves each selection in the handler: ALL rows short-circuit to `IsAll=true`/`LocationId=null`/`BranchCode="ALL"` with no lookup; non-ALL rows require a non-null `LocationId` that must exist via `GetLocationByIdAsync` (a plain `FirstOrDefaultAsync` with no `IsActive` filter), and on success `BranchCode` is overwritten from the resolved location's `Code`. A null or non-existent `LocationId` throws `UserValidationException`, which already maps to ERR-USR-400, and the validator adds a matching per-item rule. The Copy-Profile merge now de-dups `BranchSelection`s by a stable key (ALL, else the LocationId Guid) and stays commutative/idempotent.

The contract surface is `BranchSelection(Guid? LocationId, bool IsAll, string BranchCode = "")` on both the request `Branches` and response `Branches`, plus a new `Id` as the first member of `BranchTreeNodeResponse`. The frontend `BranchSelection` interface, `BranchTreeNode.id`, and the mock/real service bodies serialize to the identical camelCase shape, so cross-repo parity holds.

The UI relabel is confined to visible strings on `LocationMaster.tsx` (button, counter, delete dialog) with the screen title and nav label already reading "Branch Master". The route `/locations`, the `useScreenPermissions('LocationMaster')` key, the `locationService`, the `Location` entity, and all file/component names are unchanged — a true label-only change.

<details>
<summary>Issues (0)</summary>

No HIGH or MEDIUM findings. No blocking issues.

</details>

<details>
<summary>Details</summary>

### Entity, EF config, and migration (checklist a, f)

`UserBranchAssociation` adds `Guid? LocationId` alongside the retained `BranchCode` and `IsAll`, with XML docs that correctly describe `LocationId` as authoritative and `BranchCode` as reference-only. The configuration adds `b.Property(x => x.LocationId)`, a nullable `HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict)` (matching the existing LOB FK's Restrict), and `b.HasIndex(x => x.LocationId)`. The LOB FK, the two composite indexes, and the `CK_user_branch_associations_owner` check constraint are all present and unchanged. (confirmed)

The migration `20261005055758_AddBranchAccessLocationFk` adds the nullable `LocationId` column, `IX_user_branch_associations_LocationId`, and `FK_user_branch_associations_locations_LocationId` → `locations(Id)` with `ReferentialAction.Restrict`, and a symmetric `Down`. There is no `HasData`/`InsertData` touching the table, so no backfill. The model snapshot reflects the property, index, and Restrict FK. Evidence records that `dotnet ef database update` was not run. (confirmed)

### SaveUserAccess existence-only validation (checklist b)

The handler builds associations per selection: ALL short-circuits to the sentinel with no lookup; non-ALL throws `UserValidationException("The selected branch location was not found.")` when `LocationId` is null or when `GetLocationByIdAsync` returns null, and otherwise sets `LocationId`/`BranchCode` (from `location.Code`)/`IsAll=false`. `GetLocationByIdAsync` is `Context.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)` with no `IsActive` or `Level` filter. The validator adds `RuleForEach(x => x.Branches).Must(b => b.IsAll || b.LocationId.HasValue)` for the null-on-the-request path. `SaveUserAccessBranchTests` covers non-existent → throws + nothing persisted, null → throws, ALL → sentinel persisted/echoed, and a valid location seeded with `IsActive=false` to prove existence-only, then asserts the FK and server-resolved `BranchCode`. (confirmed)

### Contracts and cross-repo parity (checklist c)

`BranchSelection(Guid? LocationId, bool IsAll, string BranchCode = "")` is the single wire record on both `SaveUserAccessRequest.Branches` and `UserAccessResponse.Branches`; `BranchTreeNodeResponse` gains `string? Id` as its first member. `GetUserAccessQueryHandler` projects persisted rows to `new BranchSelection(b.LocationId, b.IsAll, b.BranchCode)`, and the controller passes `r.Branches` through. The frontend `BranchSelection` interface (`locationId`/`isAll`/`branchCode`) and `BranchTreeNode.id` match the default camelCase serialization exactly; the real-service test asserts the `branches` body and the mock round-trips the same shape. (confirmed)

### Branch tree now location-backed

`GetBranchLocationTreeQueryHandler` injects `ILocationRepository`, builds the hierarchy from `GetAllFlatAsync` via `ParentId`, maps `Level` (5→Branch, 1→Location, else Region), sets each node's `Id` to the location Guid, and prepends the ALL root `(null, "ALL", "ALL", "Location", [])`. `BranchTreeCatalog` is deleted. This is the design choice flagged in the plan: the old static catalog used invented codes with no `locations` rows, so a `LocationId` from them could never pass existence validation; sourcing from seeded locations is the minimal way to hand the UI real Guids. The UI only sends ALL + leaf nodes, and `isSelectable` enforces that (ALL, or a childless node with a non-null id), so container nodes render disabled. (confirmed)

### Copy-Profile merge

`AccessAssignmentMerger.Merge` now operates on `IEnumerable<BranchSelection>`, de-duplicating by a stable key (ALL ⇒ "ALL", else the `LocationId` Guid string); a non-ALL selection with a null `LocationId` key is skipped during merge, which is safe because the validator and handler both reject that case on the request path. The row arm (RoleCode OR-merge) is unchanged, and the property tests P3/P4 were updated to generate `BranchSelection`s while still asserting commutativity/idempotency. (confirmed)

### UI relabel is label-only (checklist d)

`LocationMaster.tsx` changes only visible strings: "Add Branch", the "{n} branch(es) found" counter, and the "Delete Branch" dialog title/body. The screen title (line 139) and the `Layout.tsx` nav label (line 109) already read "Branch Master" and are not re-touched. The route `/locations` (and `/locations/new`), the `useScreenPermissions('LocationMaster')` gating key, the `locationService` import, and all component/file names are unchanged; the `App.tsx` diff is empty. The branch picker, access step, review step, wizard submit, draft hook, and WizardContext all swap `branchCodes` → `branches` consistently. (confirmed)

### Test coverage and recorded evidence (checklist e)

Backend evidence: `dotnet build Finnova.Backend.slnx` succeeded (0 errors, after a one-line `using` fix), targeted slice `~UserManagement|~SaveUserAccess|~Auth` passed 212/212, migration generated cleanly with no host lock. Frontend evidence: `npm run build` (`tsc -b && vite build`) clean with no remaining `branchCodes` producer references, and the six touched specs passed 27/27. The new/updated tests exercise the existence-only path, the ALL sentinel, the FK round-trip, the merger with selections, the integration auth body, and the UI branch-selection toggle/checked/disabled behavior plus tree `id`. Per the step instructions the suites were not re-run; the diff and evidence are internally consistent and no specific doubt warranted a spot-check.

Not tested: no end-to-end test applies the migration against a real database (intentionally out of scope — DB not updated); the location-backed tree handler has no direct reference-query unit test in the diff, though its output shape is exercised indirectly through the mock on the UI side.

</details>

<details>
<summary>File map</summary>

Backend (`d:\Projects\Finnova\Finnova-API`):
- `Finnova.Models/Domain/Entities/UserBranchAssociation.cs` — adds nullable `LocationId`; `BranchCode` now reference-only.
- `Finnova.Repository/Configuration/UserBranchAssociationConfiguration.cs` — nullable `Location` FK (Restrict) + index; owner check constraint intact.
- `Finnova.Models/Contracts/UserManagement/UserManagementRequests.cs` / `UserManagementResponses.cs` — `BranchSelection` record; `Branches` replaces `BranchCodes`; `Id` added to `BranchTreeNodeResponse`.
- `Finnova.Repository/Interfaces/IUserManagementRepository.cs` / `Repositories/UserManagementRepository.cs` — `GetLocationByIdAsync` (existence only).
- `Finnova.Service/.../SaveUserAccess/*` — command/handler/validator rebind to selections + existence validation.
- `Finnova.Service/UserManagement/Helpers/AccessAssignmentMerger.cs` — merge over `BranchSelection`.
- `Finnova.Service/UserManagement/Queries/References/ReferenceQueryHandlers.cs` — location-backed tree with `Id`.
- `Finnova.Service/UserManagement/Internal/BranchTreeCatalog.cs` — deleted.
- `Finnova.Service/.../GetUserAccess/GetUserAccessQueryHandler.cs`, `Controllers/UserManagementController.cs` — project/pass `Branches`.
- `Finnova.Repository/Migrations/20261005055758_AddBranchAccessLocationFk.cs` (+ `.Designer.cs`), `FinnovaDbContextModelSnapshot.cs` — schema-only migration.
- `Finnova.Tests/...` — `SaveUserAccessBranchTests.cs` (new), InMemory repo `Locations`/`GetLocationByIdAsync`, property + integration test updates.

Frontend (`D:\Projects\Finnova\Finnova-UI`):
- `src/models/userManagement.model.ts` — `BranchSelection`, `BranchTreeNode.id`, `branches` on save/access.
- `src/components/userManagement/{BranchLocationTree,AccessStep,ReviewStep,UserManagementWizard,WizardContext}.tsx` + specs — selection-based picker + submit.
- `src/hooks/useUserManagementDraft.ts` — draft carries `branches`.
- `src/services/{mock,real}/userManagement.*` — wire shape + tree `id`.
- `src/pages/LocationMaster.tsx` — label-only "Branch" strings.

Full diffs: `git -C d:\Projects\Finnova\Finnova-API diff` and `git -C D:\Projects\Finnova\Finnova-UI diff` (plus untracked migration/test files).

</details>
