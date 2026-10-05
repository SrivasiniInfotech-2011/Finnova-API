# Verification — Branch Access LocationId FK (two repos)

Iteration: first (no `review.json` present). All commands run ONCE as mandated — suites were not
re-run repeatedly. Evidence below so the reviewer does not need to re-run anything.

## Backend — `d:\Projects\Finnova\Finnova-API`

### Build
Command: `dotnet build Finnova.Backend.slnx`
Result: **Build succeeded — 0 Error(s)**.
(First attempt surfaced CS0246 for `Location` in `ReferenceQueryHandlers.cs`; fixed by adding
`using Finnova.Models.Domain.Entities;`. Rebuild clean.)

### Migration
Command:
`dotnet ef migrations add AddBranchAccessLocationFk --project Finnova.Repository/Finnova.Repository.csproj --startup-project Finnova.SystemAdminService/Finnova.SystemAdminService.csproj`
Result: **"Done."** — no host lock / MSB3027.
Generated: `Finnova.Repository/Migrations/20261005055758_AddBranchAccessLocationFk.cs` (+ `.Designer.cs`)
and updated `FinnovaDbContextModelSnapshot.cs`.
`Up` content verified:
- `AddColumn<Guid> "LocationId"` on `user_branch_associations`, `nullable: true`.
- `CreateIndex IX_user_branch_associations_LocationId`.
- `AddForeignKey FK_user_branch_associations_locations_LocationId` -> `locations(Id)`,
  `onDelete: ReferentialAction.Restrict`.
No backfill / no `HasData` into `user_branch_associations` (table holds no seeded rows — confirmed).
**`dotnet ef database update` was NOT run** (orchestrator applies it after review).

### Targeted test slice
Command:
`dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~UserManagement|FullyQualifiedName~SaveUserAccess|FullyQualifiedName~Auth"`
Result: **Passed! — Failed: 0, Passed: 212, Skipped: 0, Total: 212** (~9 s).

New backend test file `Finnova.Tests/Unit/SaveUserAccessBranchTests.cs` covers:
- non-existent `LocationId` on a non-ALL selection => `UserValidationException` (ERR-USR-400), nothing persisted.
- null `LocationId` on a non-ALL selection => `UserValidationException`.
- ALL selection => persists `IsAll=true`, `LocationId=null`, `BranchCode="ALL"`; echoed on the response.
- valid existing `LocationId` (seeded with `IsActive=false` to prove existence-only, no active check)
  => persists FK + `BranchCode` from the location's `Code`; round-trips on `UserAccessResponse.Branches`.
Merger property tests (P3/P4) updated to generate `BranchSelection`s; integration auth test "PUT access"
body switched to `Branches = [{ locationId:null, isAll:true, branchCode:"ALL" }]`.

## Frontend — `D:\Projects\Finnova\Finnova-UI`

### Build
Command: `npm run build` (`tsc -b && vite build`)
Result: **built in ~28 s** — tsc clean (no remaining `branchCodes` producer references; the only
`branchCodes` strings left in source are gone), vite bundle emitted. 0 errors.

### Touched specs
Command:
`npm test -- --run src/components/userManagement/BranchLocationTree.test.tsx src/components/userManagement/AccessStep.test.tsx src/components/userManagement/UserManagementWizard.test.tsx src/services/mock/userManagement.mock.test.ts src/services/real/userManagement.real.test.ts src/hooks/useUserManagementDraft.test.ts`
Result: **Test Files 6 passed (6); Tests 27 passed (27)**.

Added/updated frontend coverage:
- `BranchLocationTree.test.tsx`: leaf toggle passes the node; checked state matched by `locationId`;
  container nodes disabled; ALL rendered.
- `AccessStep.test.tsx`: branch-tree mock nodes now carry `id`.
- `userManagement.mock.test.ts`: branch-selection round-trip (`locationId`+`isAll`+`branchCode`) on
  saveAccess; branch tree exposes `id` (null for ALL, Guid for leaf).
- `userManagement.real.test.ts`: `saveAccess` body asserts the `branches` shape.
- `UserManagementWizard.test.tsx` / `useUserManagementDraft.test.ts`: draft/payload use `branches`.

## Cross-repo parity
Backend `BranchSelection(Guid? LocationId, bool IsAll, string BranchCode)` serializes (default
camelCase) as `{ locationId, isAll, branchCode }`. Frontend `BranchSelection` interface and the
`saveAccess`/`getAccess` wire shapes match verbatim. `BranchTreeNodeResponse` gained `Id` (first
member) -> UI `BranchTreeNode.id`.

## Not done (per instructions)
- No commit (orchestrator/user commits).
- No `database update`.
- No sample user/group/branch seeding.
