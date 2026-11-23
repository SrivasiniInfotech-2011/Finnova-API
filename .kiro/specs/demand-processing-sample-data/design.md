# Design — Demand Processing Sample Data

## Overview

The previous revision placed the fixture inside `Finnova.AccountsService`: a startup seeder, four sample ports swapped in by two flags, and fixture options in the production configuration class. This revision moves everything out of the hosts.

| Concern | Where it lives | When it runs |
|---|---|---|
| Real Tamil Nadu state and city locations | EF Core migration in `Finnova.Repository` | `dotnet ef database update`, in every environment |
| Sample catalog, ownership, seeder, command-line tool | New `Finnova.Data` console project | On demand, Development and Testing only |
| Adapters and receipt parser that let the real processor calculate March | `Finnova.Tests/DemandProcessing/Support` | Inside tests only |
| `IPreviousDemandPort` seam with a null default | `Finnova.Service` | Always, with no behavior change |

No service host references `Finnova.Data`. No flag, startup hook, or sample port exists in production code.

### A fact the design depends on

`Finnova.Service/DependencyInjection.cs` registers `ConfiguredScheduleAdapter`, `DependencyAccountSourcePort`, and `DependencyAllocationEventPort`. All three throw `DemandDependencyException`. A March run therefore cannot work in any host until real database-backed ports exist, whatever data is seeded. This design does not build those ports. March is calculated in `Finnova.Tests`, where test adapters read the seeded tables. The seeded history (October 2025 to February 2026) needs no port and shows in the existing history queries and the UI. See Assumption 12.

## Technology and repository integration

- Backend: `e:\Finnova\Finnova-API` (.NET 10, EF Core 10 SQL Server).
- New project: `Finnova.Data` (`OutputType` Exe), added to `Finnova.Backend.slnx`. It references `Finnova.Models` and `Finnova.Repository` only.
- `Finnova.Tests` gains a project reference to `Finnova.Data` and a SQL Server package.
- Frontend: no files. `E:\Finnova\Finnova-UI\Finnova-UI` is reused as is.

## Reuse versus new work

| Concern | Reuse | New work |
|---|---|---|
| Persistence | `FinnovaDbContext`, all existing entity configurations and migrations | One data-only migration; `Finnova.Data` seeder and ownership store |
| Reference-data pattern | Existing migrations that already insert data (`ReplaceBranchesWithLocations`, `SeedDefaultOrganization`) | `SeedTamilNaduReferenceLocations` |
| Source entities | `LookupValue`, `Nationality`, `Organization`, `LineOfBusiness`, `Location`, `Customer` and children, `Application`, schedules, `Account`, `LoanAccount`, `Transaction`, demand history entities | Deterministic catalog; small domain helpers for private-setter customer IDs and timestamps |
| Demand processing | `DemandProcessor`, `DemandRules`, `BillingRules`, allocation policy, default charge adapter, journal port, repository, policies | `IPreviousDemandPort` and `NullPreviousDemandPort`; one changed statement in `DemandProcessor` |
| March in tests | Existing `WebApplicationFactory` host pattern, JWT test key from the runsettings file | Test adapters, receipt parser, host test |
| Composition | Existing `AddFinnovaServices` | One added registration line |
| UI and routing | Existing gateway alias and Finnova-UI demand page | None |

## Requirements coverage map

| Requirement | Design realization |
|---|---|
| R1 | Catalog values; migration for `TN` and cities; seeder for the rest. |
| R2 | Parent-first upserts and explicit catalog maps. Reference locations are resolved by code and verified, not inferred. |
| R3 | SHA-256 deterministic IDs, ownership lookup rows, canonical hashes, non-manifest fingerprint. |
| R4 | `seed` command: preflight validation, one transaction, reference-location check, fixed timestamps, manifest. |
| R5 | `SampleReceiptParser` in test support. |
| R6 | Seeder history graphs; `SamplePreviousDemandPort` in test support. |
| R7 | Test adapters feed the unchanged processor; host test asserts totals. |
| R8 | Parser reason codes for receipts; seeder rollback and non-zero exit code for fatal errors. |
| R9 | `SampleDataEnvironmentGuard`; no host references `Finnova.Data`; a test scans host projects. |
| R10 | `SeedTamilNaduReferenceLocations` migration. |
| R11 | `IPreviousDemandPort`, `NullPreviousDemandPort`, one `DemandProcessor` edit. |

## Reference-data migration

`SeedTamilNaduReferenceLocations` is created with `dotnet ef migrations add`. Because the model does not change, EF generates an empty `Up`, an empty `Down`, and an unchanged snapshot. The task file replaces the empty bodies.

| Code | Name | Level | Parent code | Fixed ID |
|---|---|---:|---|---|
| `TN` | Tamil Nadu | 2 | `IND` | `7a1e0000-0000-4000-8000-000000000001` |
| `CHN` | Chennai | 3 | `TN` | `…0002` |
| `CBE` | Coimbatore | 3 | `TN` | `…0003` |
| `MDU` | Madurai | 3 | `TN` | `…0004` |
| `SLM` | Salem | 3 | `TN` | `…0005` |
| `TRI` | Tiruchirappalli | 3 | `TN` | `…0006` |
| `TNJ` | Thanjavur | 3 | `TN` | `…0007` |

Each row is written with `INSERT … SELECT … FROM [locations] p WHERE p.[Code] = <parent> AND p.[Level] = <level - 1> AND NOT EXISTS (child of p with [Code] = <code>)`. Three properties follow:

- The parent ID is read from the database, so the migration works even when an administrator created the parent earlier with a different ID.
- A row whose parent already has a child with that code is skipped, so the migration never collides with the unique (parent, code) index or with admin-created data.
- The statements contain only compile-time constants, so there is no injection surface.

`Down` deletes the seven fixed IDs, cities first, and only rows with no children. If sample zones exist under a city, that city stays and the rollback does not fail.

I chose SQL inside the migration over `InsertData` or `HasData`. Either of those would change the model snapshot, hard-code a parent ID that may differ per database, and fail on an existing code.

## Finnova.Data project

### Layout

```text
Finnova.Data/
  Finnova.Data.csproj
  Program.cs                       command routing, exit codes
  appsettings.json                 no secrets, empty connection string
  DemandSample/
    SampleDataCatalog.cs           immutable catalog, stable IDs, cutoff helper, validation
    SampleDataExceptions.cs
    SampleDataOptions.cs
    SampleDataEnvironmentGuard.cs
    SampleOwnershipStore.cs
    SampleDataSeeder.cs
    SampleDataVerifier.cs
```

### Commands

```text
dotnet run --project Finnova.Data -- migrate
dotnet run --project Finnova.Data -- seed demand-sample
dotnet run --project Finnova.Data -- verify demand-sample
```

- `migrate` calls `Database.MigrateAsync()`, which applies the reference migration and every other pending migration.
- `seed demand-sample` runs the preflight checks and the transactional seeder and prints the fingerprint.
- `verify demand-sample` is read-only. It recomputes the fingerprint from the ownership records, compares it with the stored manifest, and checks the owned-row counts and the five history graphs.
- Exit codes: `0` success, `1` validation or collision failure, `2` usage or environment guard failure.

### Configuration and environment guard

The environment comes from `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT`. `SampleDataEnvironmentGuard.Require` allows only `Development` and `Testing` (case-insensitive) and throws for anything else, including an unset value. It runs before the connection string is read. This is the one control that keeps sample rows out of a shared or production database, so it is deliberately strict and has no override option.

Configuration layers, lowest to highest precedence: `appsettings.json`, `appsettings.{Environment}.json`, `appsettings.{Environment}.local.json` (already git-ignored by the `appsettings.*.local.json` pattern), environment variables. The key is `ConnectionStrings:FinnovaConnection`, the same key the hosts use. The tool never prints it.

### Catalog and seeder

`SampleDataCatalog`, `SampleOwnershipStore`, and `SampleDataSeeder` keep the logic of the earlier design with these differences:

- They live in `Finnova.Data.DemandSample` and depend on `Finnova.Models` and `Finnova.Repository` only.
- `SampleDataOptions` (dataset key, as-of date, business timezone, all with fixed defaults) replaces `IOptions<DemandProcessingOptions>`. The seeder still rejects any non-default value.
- Rounding uses `SampleDataCatalog.Money`, the same `decimal.Round(…, 2, MidpointRounding.AwayFromZero)` as `BillingRules.Money`.
- Levels 2 and 3 (`TN` and the cities) are **not upserted and not owned**. The seeder looks each up by code under the expected parent and checks level, active flag, and the parent chain. A mismatch throws `SampleDataValidationException` with the message "Reference location `<code>` is missing or invalid. Apply the reference-data migration first." Zones (level 4) and branches (level 5) stay sample-owned with deterministic IDs and parent IDs taken from the resolved rows.
- The ownership rules are unchanged: an existing deterministic-ID or natural-key row without a matching ownership record is a collision and rolls back the transaction.

Upsert order inside one transaction:

1. Nationality, business lookup values, organization, LOB.
2. Reference locations resolved and verified; zones and branches upserted.
3. Customers, addresses, individual details.
4. Applications, generic accounts, loan accounts, schedules, installments.
5. Valid and invalid transactions.
6. Five history graphs: run, branch scopes, revision, stage, snapshots, results, evidence, rows, batches, audit.
7. Aggregate validation, fingerprint, manifest, commit.

### Verifier

`SampleDataVerifier.VerifyAsync` returns a list of problem strings. It checks that the manifest exists, that the stored fingerprint equals a fresh computation over the non-manifest ownership rows, that each of the five history runs has eight branch scopes and published snapshots, and that the seven loan accounts exist. `verify` prints the problems and exits `1` when any exist.

## Test-support adapters (Finnova.Tests)

These classes keep the behavior of the earlier sample adapters but live under `Finnova.Tests/DemandProcessing/Support` in the `Finnova.Tests.DemandProcessing.Support` namespace:

- `SampleAccountSourcePort` filters by LOB, active state, the sample branch set, and `scope.BranchIds`. It never enumerates every sample branch for a branch-scoped run.
- `SampleFinalizedSchedulePort` resolves an active loan, its finalized schedule, and the installment for the period.
- `SampleAllocationEventPort` maps a loan to its generic account, filters by the `Asia/Kolkata` cutoff converted to UTC, and parses in memory with `SampleReceiptParser`.
- `SamplePreviousDemandPort` verifies the exact previous run, revision, stage, snapshots, publication flags, row identity, branch set, and ownership record, and returns `null` on any mismatch.
- `SampleReceiptParser` validates the eleven-token description and the persisted transaction fields.

The host test factory replaces the three throwing ports and `IPreviousDemandPort` with these classes through `ConfigureServices`. Production code never sees them.

## Previous-demand seam (Finnova.Service)

```csharp
public sealed record PreviousDemandResult(decimal Arrears, string SourceReference);

public interface IPreviousDemandPort
{
    Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct);
}
```

`NullPreviousDemandPort` returns `null`. `DemandProcessor` replaces its hard-coded prior arrears of `0m` with `previous?.Arrears ?? 0m`. With the null default this is the same behavior as today. This is the only change to production behavior code, and it is optional if you decide March arrears do not need assertion (Assumption 13).

## Data model and deterministic catalog

### Stable dimensions

- Dataset key `SAMPLE-DP-V1`; as-of date `2026-03-31`; timezone `Asia/Kolkata` (Windows fallback `India Standard Time`).
- Cutoff marker `SAMPLE-DP|V1|ASOF|2026-03-31`.
- Periods `2025-10` through `2026-03`; history is the first five.
- Organization code `SMPD`, currency `INR`. LOB key `SAMPLE-DP-LENDING`.
- Fixed seed timestamp `2026-03-31T18:30:00Z`; fixed manifest timestamp `2026-03-31T18:31:00Z`.
- `StableId(entityType, logicalKey)` is the first 16 bytes of SHA-256 over `SAMPLE-DP-V1|entityType|logicalKey`.

### Location hierarchy

`IND` and `TN` plus the six cities come from migrations. The catalog owns six zones (`<city>-Z01`) and eight branches:

| Branch | City | Zone | Purpose |
|---|---|---|---|
| `CHN-B01` | Chennai | `CHN-Z01` | Active account |
| `CHN-B02` | Chennai | `CHN-Z01` | Active account |
| `CBE-B01` | Coimbatore | `CBE-Z01` | Active account |
| `MDU-B01` | Madurai | `MDU-Z01` | Active account |
| `MDU-B02` | Madurai | `MDU-Z01` | Empty branch, `NoDemand` case |
| `SLM-B01` | Salem | `SLM-Z01` | Active account |
| `TRI-B01` | Tiruchirappalli | `TRI-Z01` | Future-receipt exclusion |
| `TNJ-B01` | Thanjavur | `TNJ-Z01` | Part-payment case |

### Customers, applications, accounts

Customers `CUSTDP000001` to `CUSTDP000008`. Seven explicit loan mappings: `LN-DP-0001` to `LN-DP-0007`, applications `APP-DP-0001` to `APP-DP-0007`, EMIs ₹10,000, ₹12,000, ₹15,000, ₹8,000, ₹11,000, ₹9,000, ₹13,500. Customer 8 has no application and `MDU-B02` has no loan. Every application is `Processed`; every account is `Active`, `Loan`, `INR`.

### Ownership record

```text
Module:     DemandProcessing
LookupType: SAMPLE_OWNERSHIP
Code:       <EntityType>:<LogicalKey>
Value:      SAMPLE-DP-V1|<EntityType>|<deterministic-id>|<canonical-hash>
```

The manifest uses code `Manifest:SAMPLE-DP-V1` and is excluded from the fingerprint input.

## Error handling

| Operation | Failure | Behavior |
|---|---|---|
| Environment guard | Not `Development` or `Testing` | Exit `2` before any connection |
| Preflight | Wrong key, date, or timezone; duplicate key; broken mapping; wrong `IND` | `SampleDataValidationException`, exit `1`, no transaction |
| Reference locations | `TN` or a city missing, wrong level, wrong parent, inactive | Fatal, names the migration, no write |
| Upsert | Unowned deterministic ID or natural key | `SampleDataCollisionException`, rollback, exit `1` |
| Aggregate check | Missing parent, bad linkage, bad totals | Fatal, rollback |
| Receipt parse (tests) | Bad description, amount, entity field, account, reference, cutoff | Exclude that event, log bounded reason code, continue |
| Previous-demand lookup (tests) | Any mismatch | Return `null` |
| Cancellation or SQL error | Interrupted | Roll back and propagate |

No message or log line contains a full receipt description, a connection string, or an unbounded reference.

## Security and operational controls

- Production hosts contain no sample code, no flags, and no reference to `Finnova.Data`. A test fails if a host `.csproj` references it or a host source file mentions the sample namespace.
- The guard allows only two named environments. Anything else, including an empty value, is refused.
- The connection string is read from configuration and never written to output.
- All seed values are synthetic. Queries are parameterized EF Core queries. The migration SQL uses constants only.
- SQL tests use a unique database name per run and drop only that database.

## Testability

- Pure tests: catalog validation, stable IDs, cutoff conversion, parser success and rejection, environment guard. FsCheck properties use at least 100 iterations.
- SQL tests (`FINNOVA_SAMPLE_SQL_CONNECTION`): migrate a fresh database; assert `TN` and six cities exist; seed twice and compare fingerprints and snapshots; repair a changed row; roll back on an unowned collision; fail cleanly when a reference city is removed; check the five history graphs and the exact previous-demand tuple.
- Host test: unique database, migrate, seed with `Finnova.Data`, authenticated March run through the real Accounts host with the test ports, totals `48,500 / 18,000 / 66,500`, eight batches, one-branch isolation, unauthenticated rejection.
- Isolation test: no host project references `Finnova.Data` or contains the sample namespace.

## Design decisions and tradeoffs

- **Separate project instead of a hosted seeder.** The data can be run, reviewed, and repeated without starting a service, and production binaries cannot contain it. Cost: one more project and a manual command.
- **Migration only for real reference data.** Fictitious branches, customers, and loans do not belong in a migration that runs in every environment. Cost: two steps (`migrate`, then `seed`) for a full sample setup.
- **SQL inside the EF migration.** Idempotent, parent-aware, no snapshot change. Cost: the SQL is hand-written, so a SQL test covers it.
- **Locations resolved by code, not owned.** The seeder cannot delete or rewrite a real location. Cost: the seed fails when the migration has not run, which is the intended message.
- **Test adapters instead of production adapters.** The sample data cannot silently change production behavior. Cost: March cannot be run by hand until real ports exist (Assumption 12).
- **Null previous-demand seam in production code.** Small and behavior-neutral, and the only way to test arrears carry-forward. Cost: one interface and one changed statement.
- **No `reset` command.** Deleting thirty entity types in reverse order is easy to get wrong. Recreating a local database is simple and certain.
- **Strict two-environment guard.** Blocks the most damaging mistake. Cost: UAT cannot use the tool without a deliberate code change.

## Implementation-ordered task list

1. Reference migration: scaffold with `dotnet ef`, fill in `Up` and `Down`.
2. Previous-demand seam in `Finnova.Service` (port, null adapter, registration, processor edit).
3. Private-setter helper methods on customer entities in `Finnova.Models`.
4. `Finnova.Data` project: csproj, solution entry, appsettings.
5. Catalog, exceptions, options, environment guard.
6. Ownership store.
7. Seeder (including reference-location verification).
8. Verifier and `Program.cs`.
9. Test-support: receipt parser and four adapters.
10. `Finnova.Tests.csproj` references and packages.
11. Tests: catalog, parser, guard and isolation, reference migration SQL, seeder SQL, host.
12. Run order and manual verification commands.

## What changed from the previous revision

- Removed: `DemandProcessingOptions` fixture properties, `AddDemandProcessingSampleData`, `SampleDataStartupSeeder`, the Accounts `Program.cs` edits, the two configuration flags, and the Production startup error.
- Moved: catalog, ownership store, and seeder to `Finnova.Data`; adapters and parser to `Finnova.Tests`.
- Added: reference migration, command-line tool, environment guard, verifier, isolation test.
- Changed: `TN` and the cities are reference data rather than sample-owned rows.
