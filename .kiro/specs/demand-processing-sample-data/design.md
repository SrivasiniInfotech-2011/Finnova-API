# Design — Demand Processing Sample Data

## Overview

This is a backend fixture vertical slice for the existing .NET 10, C#, EF Core SQL Server, MediatR, and Demand Processing pipeline. The implementation will seed existing tables in `FinnovaDbContext`, publish five deterministic historical graphs, and let the existing Accounts Service processor calculate March 2026. The fixture is not a second demand engine and does not add an API or schema table.

The implementation has three boundaries. `SampleDataCatalog` is pure, immutable, deterministic, and independent of EF and the current clock. `SampleDataSeeder` owns validation, collision detection, dependency-ordered persistence, ownership metadata, history graph construction, aggregate validation, and one transaction. Sample adapters are registered only by `Finnova.AccountsService` when both opt-in flags are enabled in Development or Testing. The normal ports remain registered for every other configuration.

The frontend is unchanged. Finnova-UI already calls the gateway demand-processing alias, and the existing gateway route continues to target the Accounts host. The fixture is consumed through the existing authenticated Demand Processing endpoints.

## Technology and repository integration

The future changes target these repositories and paths:

- Backend: `d:\Projects\Finnova\Finnova-API`.
- Domain/service options and ports: `Finnova.Models` and `Finnova.Service`.
- Persistence: `Finnova.Repository\Context\FinnovaDbContext` and existing entity configurations.
- Host composition: `Finnova.AccountsService\Program.cs`.
- Tests: `Finnova.Tests` with xUnit, FsCheck.Xunit, EF Core SQL Server, and `WebApplicationFactory`.
- Frontend: no files. The existing `E:\Finnova\Finnova-UI\Finnova-UI` service and page are reused.

The existing migrations already create `LookupValue`, location, customer, application, account, transaction, schedule, and Demand Processing tables. Therefore no migration file is created. The implementation task includes the exact `dotnet ef database update` command to apply those existing migrations in an isolated test profile.

## Reuse versus new work

| Concern | Reuse | New work in the later implementation |
|---|---|---|
| Persistence | `FinnovaDbContext`, existing entity configurations and migrations | Seed upserts, ownership metadata, manifest fingerprint, and fixture validation |
| Source entities | `LookupValue`, `Nationality`, `Organization`, `LineOfBusiness`, `Location`, `Customer`, customer children, `Application`, schedules, `Account`, `LoanAccount`, `Transaction` | Deterministic catalog and small domain helper methods for private-setter customer IDs/timestamps |
| Demand processing | `DemandProcessor`, `DemandRules`, `BillingRules`, `AllocationPolicy`, default charge adapter, journal port, repository, authorization policies | `IPreviousDemandPort`, null adapter, sample account/schedule/receipt/history adapters |
| Composition | `AddFinnovaServices`, existing Accounts host and worker | Accounts-only guarded extension and startup seeder |
| UI and routing | Existing gateway alias and Finnova-UI demand page/service | None |
| Tests | Existing xUnit, FsCheck and integration conventions | Catalog/parser unit tests, SQL invariant tests, DI tests, authenticated SQL host flow |

## Requirements coverage map

| Requirement | Design realization |
|---|---|
| R1 | Catalog uses English-only synthetic Indian values, Tamil Nadu hierarchy, INR, fixed UTC timestamps, and no sensitive identity data. |
| R2 | Parent-first hierarchy and explicit maps are used; every FK is assigned from catalog dictionaries, never inferred by row order. |
| R3 | SHA-256 deterministic IDs, ownership rows, full canonical hashes, non-manifest fingerprinting, and unowned ID/natural-key collision rejection. |
| R4 | Clock-free preflight validation, one SQL transaction, fixed timestamps, repair of owned rows, and stable manifest fingerprint. |
| R5 | Strict eleven-token parser validates both description and persisted transaction fields, with local-date-to-UTC cutoff conversion. |
| R6 | Six finalized installments and five complete linked graphs, including `DemandRunBranchScope`, evidence, audit, publication, and reconciliation. |
| R7 | Sample account/schedule/event/previous-demand ports feed the unchanged allocation and demand rules. |
| R8 | Receipt errors are recoverable and safe; seed, FK, ownership, and aggregate errors are fatal and rolled back. |
| R9 | Accounts-only registration requires both flags and Development/Testing; partial configuration never overrides normal ports. |

## Data model and deterministic catalog

### Stable dimensions

- Dataset key: `SAMPLE-DP-V1`.
- As-of business date: `2026-03-31`.
- Business timezone: `Asia/Kolkata`; the Windows fallback ID is `India Standard Time`.
- Cutoff marker: `SAMPLE-DP|V1|ASOF|2026-03-31`.
- Periods: `2025-10`, `2025-11`, `2025-12`, `2026-01`, `2026-02`, `2026-03`.
- History periods: the first five periods; current processing period: March 2026.
- Organization: code `SMPD`, English name `Finnova Tamil Nadu Lending Sample`, currency `INR`.
- LOB: description/key `SAMPLE-DP-LENDING`.
- External India ID: `00000000-0000-0000-0000-000000000001`.
- Fixed seed timestamp: `2026-03-31T18:30:00Z`; fixed manifest timestamp: `2026-03-31T18:31:00Z`.

`StableId(entityType, logicalKey)` takes the first 16 bytes of SHA-256 over `SAMPLE-DP-V1|entityType|logicalKey`. It is a fixture convention and does not change production ID generation. The catalog validates that every logical key, natural key, parent code, branch code, account number, application number, and transaction reference is unique before the transaction starts.

### Location hierarchy

The external `IND` row is verified. The catalog owns `TN`, `CHN`, `CBE`, `MDU`, `SLM`, `TRI`, `TNJ`, one zone under each city, and these branches:

| Branch | City | Zone | Purpose |
|---|---|---|---|
| `CHN-B01` | Chennai | `CHN-Z01` | Active account |
| `CHN-B02` | Chennai | `CHN-Z01` | Active account |
| `CBE-B01` | Coimbatore | `CBE-Z01` | Active account |
| `MDU-B01` | Madurai | `MDU-Z01` | Active account |
| `MDU-B02` | Madurai | `MDU-Z01` | Empty branch and `NoDemand` case |
| `SLM-B01` | Salem | `SLM-Z01` | Active account |
| `TRI-B01` | Tiruchirappalli | `TRI-Z01` | Future-receipt exclusion case |
| `TNJ-B01` | Thanjavur | `TNJ-Z01` | Part-payment case |

The catalog carries both `CityCode` and `ZoneCode` on every branch. Seeder lookups verify the parent chain, level, active flag, and expected code before assigning `ParentId`.

### Customers, applications, and accounts

Customers are `CUSTDP000001` through `CUSTDP000008`. Seven explicit application mappings are used:

| Loan | Customer | Application | Branch | Monthly installment |
|---|---|---|---|---:|
| `LN-DP-0001` | `CUSTDP000001` | `APP-DP-0001` | `CHN-B01` | ₹10,000 |
| `LN-DP-0002` | `CUSTDP000002` | `APP-DP-0002` | `CHN-B02` | ₹12,000 |
| `LN-DP-0003` | `CUSTDP000003` | `APP-DP-0003` | `CBE-B01` | ₹15,000 |
| `LN-DP-0004` | `CUSTDP000004` | `APP-DP-0004` | `MDU-B01` | ₹8,000 |
| `LN-DP-0005` | `CUSTDP000005` | `APP-DP-0005` | `SLM-B01` | ₹11,000 |
| `LN-DP-0006` | `CUSTDP000006` | `APP-DP-0006` | `TRI-B01` | ₹9,000 |
| `LN-DP-0007` | `CUSTDP000007` | `APP-DP-0007` | `TNJ-B01` | ₹13,500 |

Customer 8 has no application. Every application is `Processed`; every loan and generic account is `Active`, `Loan`, and `INR`. The generic account has the same account number as its loan account and is the only valid transaction FK. No loan is created for `MDU-B02`.

### Schedules, receipts, and dates

Every application has one finalized schedule with six installments due on the 15th of each fixture period. Principal and interest portions are non-negative and sum to the EMI after `BillingRules.Money` rounding.

Valid descriptions use the exact format in R5. March rows drive the expected processing cases. Invalid rows remain in the database so the parser can prove exclusion. The parser calculates an end-of-day cutoff by taking the `DateOnly` in the business timezone as an unspecified local `DateTime`, converting it with `TimeZoneInfo.ConvertTimeToUtc`, and comparing the UTC transaction timestamp. It never treats a local business date as UTC midnight.

## Contracts and interfaces

### Options

`DemandProcessingOptions` gains false-by-default `UseSampleAdapters` and `SeedSampleDataOnStartup`, plus `SampleDataKey = "SAMPLE-DP-V1"` and `SampleAsOfDate = new(2026, 3, 31)`. Existing `BusinessTimezone` remains authoritative and the seeder rejects any other value for this fixture.

### Previous-demand port

Add the following service contract and production null implementation:

```csharp
public sealed record PreviousDemandResult(decimal Arrears, string SourceReference);

public interface IPreviousDemandPort
{
    Task<PreviousDemandResult?> GetAsync(
        Guid accountId,
        DemandScope scope,
        DemandPeriod targetPeriod,
        string snapshotReference,
        CancellationToken ct);
}
```

`DemandProcessor` receives the port and replaces its hard-coded `0m` prior arrears with `previous?.Arrears ?? 0m`. All other demand rules remain unchanged.

### Ownership record

Each owned row has a lookup row with:

```text
Module:     DemandProcessing
LookupType: SAMPLE_OWNERSHIP
Code:       <EntityType>:<LogicalKey>
Value:      SAMPLE-DP-V1|<EntityType>|<deterministic-id>|<canonical-hash>
Locked:     true
```

The manifest uses code `Manifest:SAMPLE-DP-V1` but is excluded from the input set used to calculate its fingerprint. The canonical hash includes every persisted business field for that entity, including FK IDs, status/type enum values, amounts, references, dates, and the fixture timestamps that are intentionally fixed.

## Components and interfaces

### `SampleDataCatalog`

A static catalog defines all periods, hierarchy rows, explicit customer/loan maps, lookup rows, receipts, stable IDs, UTC cutoffs, canonical serialization, and expected March totals. `Validate()` is pure and checks duplicate keys, duplicate natural keys, parent topology, six-period order, mapping completeness, receipt amount/kind rules, and reference safety. It runs before `BeginTransactionAsync`.

### `SampleDataSeeder`

The seeder first validates options, calls `SampleDataCatalog.Validate()`, verifies the external `IND` invariants, and only then begins a transaction. It upserts in this order:

1. Nationality, business lookup values, organization, and LOB.
2. Parent-first state/city/zone/branch locations.
3. Customers, addresses, and individual details.
4. Applications, generic accounts, loan accounts, schedules, and installments.
5. Valid and invalid transaction rows.
6. Historical demand runs, all eight branch scopes per run, revisions, stages, billing results, evidence, snapshots, rows, batches, and audit entries.
7. Cross-table FK and aggregate validation.
8. Non-manifest ownership fingerprint and manifest upsert.
9. One `SaveChangesAsync` inside the transaction followed by commit.

Every upsert checks both the deterministic ID and natural key. An existing row is usable only when the ownership record names the same ID. New and changed owned rows receive fixed fixture timestamps. Unchanged rows are not assigned a new timestamp. A failed collision, missing parent, invalid aggregate, cancellation, or database error rolls back the transaction.

History owns one `DemandRunBranchScope` per branch with the same revision number and deterministic sort order. It also owns one allocation-evidence row for each seeded billing result and one audit row per historical run, so the normal authorized history query and evidence paths can see the fixture.

### Sample adapters

- `SampleAccountSourcePort` filters by sample LOB, active loan state, and `scope.BranchIds`. It never enumerates all fixture branches for a branch-scoped run. `ResolveActiveBranchesAsync` intersects sample active branches with the caller's branch claims and checks the sample LOB. `IsActiveBranchAsync` verifies the requested branch is an active level-5 sample location and the LOB is active.
- `SampleFinalizedSchedulePort` resolves only an active owned loan, its finalized owned schedule, and the installment for the requested period.
- `SampleAllocationEventPort` maps a loan to its generic account, applies SQL-safe account/date filters, parses in memory, and emits bounded diagnostic reason codes. It uses the business-date cutoff conversion described above.
- `SamplePreviousDemandPort` checks the exact previous run ID, current revision, stage, billing snapshot, demand snapshot, publication flags, shared IDs/revision, cutoff marker, LOB, frequency, period, exact branch set, deterministic row ID, and ownership record. Every missing or mismatched step returns `null`; it never dereferences a missing snapshot.
- `NullPreviousDemandPort` returns `null` for all normal non-fixture hosts.

### Composition and startup

`AddDemandProcessingSampleData(IConfiguration, IHostEnvironment)` is called only after `AddFinnovaServices` in Accounts Service. It returns without changing registrations for `(false,false)`, `(true,false)`, or `(false,true)`. It throws for either flag in Production. For `(true,true)` in Development/Testing it replaces the four source ports, registers the ownership/seeder services, and registers the startup seeder. The startup seeder applies existing migrations in Testing/Development before calling `SeedAsync`; it does not run in production.

The `using Finnova.Service.DemandProcessing.SampleData;` directive belongs with the other top-level using directives in `Program.cs`, never between top-level statements.

## Error handling

| Operation | Failure | Caller behavior | Logging |
|---|---|---|---|
| Preflight options/catalog/root | Wrong key/date/timezone, duplicate key, broken parent, invalid mapping, wrong `IND` | Fatal validation exception before transaction | Error with safe code and logical key |
| Entity upsert | Unowned deterministic ID or natural-key collision | `SampleDataCollisionException`, rollback | Entity type and logical key only |
| FK/history/aggregate check | Missing parent, wrong linkage, bad totals, missing branch scope | Fatal validation exception, rollback, no publication | Period, entity, and safe field only |
| Receipt parse | Bad description, amount, entity field, account, reference, or cutoff | Exclude one event and continue | Bounded safe reference and reason code |
| Previous-history lookup | Missing, unowned, unpublished, or dimension mismatch | `null`, processing continues | Debug safe tuple code only |
| Cancellation/deadlock/database error | Transaction interrupted or database unavailable | Roll back and propagate | Stage and safe database operation code |
| API authorization | Missing/invalid JWT or unauthorized LOB/branch | Existing 401/403 behavior | Existing middleware and policy logging |

No log line contains a full receipt description, PAN, Aadhaar, password, token, connection string, or unbounded user-controlled reference.

## Security and operational controls

The fixture is disabled by default, cannot be enabled by a request payload, and is rejected in Production. Existing JWT bearer authentication, `DemandRun`, `DemandOutputRead`, LOB scope, and branch scope remain mandatory. Sample adapters only narrow caller scope. EF queries remain parameterized. SQL tests use a unique database name and drop only that database during disposal. Seed values are synthetic and contain no sensitive identifiers.

## Testability

Pure tests cover catalog validation, stable IDs, canonical hashes, local-date cutoff conversion, parser success/rejection, safe reference handling, and allocation outcomes. FsCheck properties use `MaxTest = 100` or greater.

SQL tests use `FINNOVA_SAMPLE_SQL_CONNECTION`, append a unique database name, call `Database.MigrateAsync()`, seed twice, and capture complete owned-row field/timestamp snapshots. They test repair of a changed and deleted owned row, unowned ID and natural-key collision rollback, FK integrity, five history graphs, branch scopes, evidence, audit, exact previous-demand tuples, and authorized history queries.

The Accounts host test uses a unique SQL Server database. Its factory enables both fixture flags, the startup seeder migrates and seeds, and the test then explicitly calls the same migration/seed lifecycle to make setup visible and repeatable. Disposal calls `EnsureDeletedAsync` for that generated database only. It uses an authenticated principal with the sample LOB and branch claims, asserts the March totals and eight batches, asserts a one-branch run reads no other branch, and asserts missing/invalid JWT requests remain unauthorized.

## Design decisions and tradeoffs

- **Existing tables instead of a fixture schema:** avoids schema drift and exercises the same data contracts that production code reads. Ownership lookup rows provide cleanup and collision safety without a migration.
- **Fixed data instead of execution-relative dates:** makes test assertions and incident reproduction repeatable. The assumption is recorded for confirmation.
- **Separate generic account projection:** preserves the existing transaction FK without changing the account model.
- **Direct historical graph plus real March flow:** makes previous-demand lookup deterministic while still exercising production allocation and demand generation.
- **Strict parser with invalid persisted rows:** tests resilience without allowing malformed source data to influence totals.
- **SQL Server tests instead of InMemory:** verifies transaction, migration, FK, and query translation behavior.
- **Both flags required:** prevents accidental partial configuration from replacing production adapters or creating rows.
- **No frontend work:** the existing gateway/UI path already provides the required processing screen and API composition.

## Implementation-ordered task list

1. Add fixture options and private-setter customer helper methods.
2. Create the complete immutable catalog, business-timezone cutoff helper, and catalog validation.
3. Create collision exceptions, ownership store, canonical hash helper, and the complete transactional seeder.
4. Add the null previous-demand port and update the processor call site.
5. Create strict parser and sample account, schedule, receipt, and previous-demand adapters.
6. Add the Accounts-only both-flags composition extension and startup migration/seeder; add the correctly placed Program using directive and call.
7. Add the local/test configuration and document the existing migration command; create no fixture migration.
8. Add complete catalog, parser, and pure allocation/property tests.
9. Add complete SQL Server seeder/invariant tests, including valid class-contained methods and exact tuple checks.
10. Add one complete Accounts-host SQL factory/test file with migration, seed, authenticated March flow, branch filtering, auth, and cleanup.
11. Verify the existing gateway and Finnova-UI path manually later without changing frontend files.

## Review resolution

This revision addresses the existing review findings as follows:

- The test payload is specified as one class per file, with every method inside its class and no duplicate `CREATE` path.
- The SQL host lifecycle explicitly migrates and seeds the generated database and deletes only that generated database during disposal.
- The Accounts `using` directive is placed in the directive block, and the service-registration call remains after `AddFinnovaServices`.
- Fixture ports and startup seeding require both flags; all four flag combinations are tested.
- Account reads intersect `scope.BranchIds`, and branch resolution verifies active sample branches and the sample LOB.
- All business-date cutoffs use `TimeZoneInfo` conversion from `Asia/Kolkata`/`India Standard Time`, not UTC end-of-day.
- Previous-demand lookup checks nulls, exact revision/linkage/publication/dimensions, deterministic row identity, ownership, and exact branch-set equality.
- Manifest fingerprinting excludes the manifest; deterministic-ID and natural-key rows require ownership; canonical hashes include persisted fixture fields.
- History includes branch-scope, evidence, and audit rows.
- Receipt parsing compares description and persisted amount, rejects null/unsafe references, and logs only bounded safe references.
- Catalog validation runs before the transaction and is covered by tests.
