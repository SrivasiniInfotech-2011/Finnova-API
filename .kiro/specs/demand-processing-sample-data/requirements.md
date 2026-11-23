# Demand Processing Sample Data Requirements

## Summary

Provide deterministic, synthetic Demand Processing data for six monthly periods without placing any fixture code in a production host. The work has three parts:

1. **Reference-data migration.** An EF Core migration adds the real Tamil Nadu state and six city locations that every environment needs.
2. **`Finnova.Data` project.** A new console project, outside every service host, holds the sample-data catalog and an idempotent seeder. It is run on demand with `migrate`, `seed`, and `verify` commands.
3. **Test-only March processing adapters.** The production account, schedule, and allocation-event ports are placeholders that throw. `Finnova.Tests` supplies database-backed test adapters so the real processor can calculate March 2026 over the seeded data.

The specification covers backend and tooling only. The existing gateway alias and Finnova-UI Demand Processing screen read the seeded history without a frontend change.

## Introduction

Finnova is an India-only financial platform. The data contains English-only synthetic values, Tamil Nadu locations, Indian Rupee amounts, and `Asia/Kolkata` business dates.

The previous revision seeded data from a startup hosted service inside `Finnova.AccountsService` and swapped production ports for sample adapters behind two configuration flags. This revision removes that design. Fixture code does not ship in any host, no flag changes the production ports, and nothing seeds at startup.

The fixed window is October 2025 through March 2026. October 2025 through February 2026 are published previous-demand periods written by the seeder. March 2026 is source data only; the real processor calculates it in tests.

## Glossary

- **Reference data**: Rows every environment needs, delivered by an EF Core migration (here, the Tamil Nadu state and six cities).
- **Sample data**: Synthetic rows for development and testing only, written by the `Finnova.Data` seed command and never by a migration.
- **Dataset key**: The fixture identifier `SAMPLE-DP-V1`, used in deterministic IDs, ownership records, and diagnostics.
- **Business period**: A year and month in `Asia/Kolkata`. Persisted technical timestamps are UTC.
- **External India root**: The existing active `Location` with ID `00000000-0000-0000-0000-000000000001`, code `IND`, name `India`, level 1, and no parent.
- **Owned row**: A sample row whose deterministic logical key is recorded in the `DemandProcessing/SAMPLE_OWNERSHIP` lookup catalog with its ID and canonical content hash.
- **Loan account**: The `LoanAccount` entity that the Demand Processing account source consumes.
- **Generic account projection**: The separate `Account` row with the same account number. `Transaction.AccountId` references it.
- **Previous demand**: A published `DemandSnapshot` and `DemandRow` from the preceding period with matching account, branch, line of business (LOB), frequency, revision, and cutoff dimensions.
- **Seed run**: One transactional execution of the `seed` command.
- **Idempotent**: A repeated seed produces the same IDs and fingerprint, creates no duplicates, preserves unchanged values and timestamps, repairs owned changes, and never overwrites an unowned collision.
- **EARS**: Easy Approach to Requirements Syntax.

## Dependencies and Assumptions

1. **Assumption, fixed window:** "Last six months" means October 2025 through March 2026, anchored at `2026-03-31`. Confirm if the window must move relative to the execution date.
2. **Assumption, March behavior:** Only October 2025 through February 2026 receive seeded published history. March is processed by the existing processor.
3. **Assumption, external root:** The existing `IND` row is migration-owned. Neither the migration nor the seeder changes it.
4. **Assumption, reference data scope:** The reference migration covers `TN` (state) and `CHN`, `CBE`, `MDU`, `SLM`, `TRI`, `TNJ` (cities). Zones, branches, the organization, the LOB, the nationality, lookups, customers, and everything else stay in the sample profile because they are fictitious. Confirm whether the real Tamil Nadu cities belong in every environment.
5. **Assumption, migration style:** The reference migration uses idempotent SQL inside an EF migration (`migrationBuilder.Sql`) and is generated with `dotnet ef migrations add`. It inserts a row only when its parent exists and has no child with that code, which matches the existing unique index on parent and code. It does not use `HasData`, so the model snapshot is unchanged.
6. **Assumption, ownership metadata:** Ownership is stored in existing `LookupValue` rows (`Module = DemandProcessing`, `LookupType = SAMPLE_OWNERSHIP`). No new table is added.
7. **Assumption, nationality:** The `IN` nationality is not seeded by any migration, so the sample profile owns it. If an unowned `IN` row already exists, the seed fails with a collision error and changes nothing.
8. **Assumption, account relationship:** Demand reads `LoanAccount`, while receipts reference the generic `Account` table. Each sample loan has one explicitly mapped generic account projection.
9. **Assumption, catalog size:** Eight customers, eight level-5 branches, seven processed applications, seven active loan accounts, seven generic account projections, seven finalized schedules with six installments each. Customer 8 and branch `MDU-B02` deliberately exercise empty-result paths.
10. **Assumption, sensitive data:** Names and contact data are synthetic. No PAN, Aadhaar, bank account, credential, document, or real-person data is seeded.
11. **Assumption, receipts:** Receipts are existing `Transaction` rows. Invalid receipts are persisted on purpose as owned test inputs and are excluded by the test adapter.
12. **Assumption, March needs adapters that do not exist yet:** The registered production ports (`ConfiguredScheduleAdapter`, `DependencyAccountSourcePort`, `DependencyAllocationEventPort`) throw `DemandDependencyException`. Until real database-backed ports exist, a March run can only be executed in `Finnova.Tests` with the test adapters. The seeded history is already readable through the existing history queries and the UI. Confirm this boundary, or schedule real ports as a separate ticket.
13. **Assumption, previous-demand seam:** The processor currently passes zero previous arrears. A small `IPreviousDemandPort` seam with a null default is added to `Finnova.Service`. Only the test-support implementation reads seeded history. Confirm the seam is acceptable; without it the March arrears cases cannot be asserted.
14. **Assumption, charges and journal:** The default charge adapter and configured journal port are used. The data expects zero ODI and other charges.
15. **Assumption, environments:** The tool runs only against Development or Testing. It refuses Production and any unrecognized environment.
16. **Assumption, no reset command:** Resetting means dropping and recreating the local database and running `migrate` and `seed` again. A selective reset needs reverse-order deletes across about thirty entity types and is out of scope.
17. **Assumption, SQL verification:** Seeder, migration, and host tests use an isolated SQL Server database. EF Core InMemory cannot test transactions, migrations, or foreign keys.
18. **Assumption, frontend:** The existing `/api/ua/api/demand-processing` gateway alias and the current Finnova-UI service and page are sufficient. No frontend task is required.

## Functional Requirements

### R1 — Dataset and localization (Ubiquitous)

The sample data SHALL use `SAMPLE-DP-V1`, English-only labels, synthetic Indian names, Tamil Nadu locations, INR amounts, `Asia/Kolkata` business periods, and UTC timestamps. It SHALL NOT add bilingual fields, non-English data, translation scaffolding, RTL behavior, or sensitive identity data.

After the migration and one seed run the database SHALL contain: the external `IND` root; `TN`; six cities; six zones; eight branches; ten business lookup values; one organization; one active LOB; eight customers each with one address and one individual-details row; seven processed applications; seven active loan accounts; seven generic accounts; seven finalized schedules with six installments each; valid and invalid receipts; and five published history graphs.

### R2 — Referential relationships (Complex)

When a seed commits, every owned foreign key SHALL resolve. Customers SHALL reference the sample organization and active English lookup values. Applications SHALL reference customers, the sample LOB, and explicit level-5 branches. Loan accounts SHALL reference applications, customers, the LOB, and branches. Generic accounts SHALL share loan account numbers. Valid transactions SHALL reference only the mapped generic account IDs. Schedules and installments SHALL reference applications and schedules. History runs, branch scopes, revisions, stages, results, evidence, snapshots, rows, batches, and audit entries SHALL reference their persisted parents.

The hierarchy SHALL be `IND` level 1, `TN` level 2, city level 3, zone level 4, branch level 5. Branch-to-zone, branch-to-city, application-to-branch, account-to-branch, and application-to-account relationships SHALL be explicit catalog mappings. SQL row order SHALL NOT infer them.

### R3 — Deterministic identity, ownership, and collision safety (State-driven)

While a sample row is owned, its ID SHALL be derived from `(dataset key, entity type, logical key)` with SHA-256, and its ownership record SHALL contain a canonical hash of every persisted business field, including foreign keys and statuses. A deterministic-ID row or natural-key row without a matching ownership record SHALL cause a safe collision error and a transaction rollback.

The manifest fingerprint SHALL be calculated from sorted non-manifest ownership records only.

### R4 — Seed command, validation, and idempotency (Event-driven)

When `seed demand-sample` starts, it SHALL validate the environment guard, the options, the catalog, and the `IND` invariants, and verify that the reference locations (`TN` and the six cities) exist with the expected level and parent, before opening the write transaction. It SHALL then upsert dependency-first, validate relationships and aggregates, persist a manifest, and commit only after all checks pass.

When the reference locations are missing, the seed SHALL fail with a message that names the migration to apply and SHALL NOT create them.

When the same data is seeded again, the command SHALL return the same fingerprint, create no duplicates, preserve unchanged values and timestamps, and repair owned rows that a test deleted or changed. It SHALL NOT overwrite an unowned ID or natural-key collision.

### R5 — Receipt source and strict parsing in test support (Event-driven)

A valid receipt description SHALL contain exactly eleven pipe-delimited tokens:

```text
SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT|<loan-account-number>|<yyyy-MM>|<FULL|PART|OVER|NONE>|<amount>|Deposit|Completed|<reference>
```

The test-support allocation-event adapter SHALL validate token count, dataset, version, event type, loan account number, target period, invariant-culture amount, `Transaction.Type`, `Transaction.Status`, non-null bounded reference equality, generic-account mapping, persisted amount equality, amount semantics, and the `Asia/Kolkata` cutoff converted to UTC. `NONE` requires zero amount; the other kinds require a positive amount. Invalid rows SHALL produce no allocation event and a bounded reason code.

Invalid cases SHALL include malformed token count, wrong dataset, wrong loan number, negative amount, `NONE` with a positive amount, persisted amount mismatch, null or unsafe reference, wrong entity type, wrong entity status, and a transaction after the local business-date cutoff.

### R6 — Schedules and previous history (State-driven)

Each application schedule SHALL be finalized and contain exactly six installments due on the 15th of October 2025 through March 2026. Each historical period SHALL contain seven succeeded billing results, seven demand rows, eight branch-scope rows, eight batches, seven `ReadyToSpool` batches, and one `NoDemand` batch for `MDU-B02`. Billing and demand snapshots SHALL be published and linked to the same run and revision. History SHALL include owned `BillingAllocationEvidence` and `DemandAuditEntry` records.

The test-support previous-demand adapter SHALL return arrears only when the exact preceding run, revision, stage, billing snapshot, demand snapshot, ownership record, account, LOB, frequency, period, cutoff, and branch-set dimensions match and both snapshots are published. Any missing, unpublished, unlinked, mismatched, extra-branch, missing-branch, or unowned tuple SHALL return no result without throwing.

### R7 — March processing outcomes (Complex)

When an authorized March 2026 run starts in `Finnova.Tests` for the sample LOB without a branch filter, the test account source SHALL return exactly the seven sample accounts across the eight branches. The unchanged processor SHALL produce seven rows and eight batches, including `MDU-B02` as `NoDemand`, with no unrelated account or branch.

| Account | March source case | February arrears | March current due before allocation | Expected March arrears |
|---|---|---:|---:|---:|
| `LN-DP-0001` | Full ₹10,000 receipt | 0 | 10,000 | 0 |
| `LN-DP-0002` | Part ₹2,000 receipt | 12,000 | 12,000 | 10,000 |
| `LN-DP-0003` | Overpayment ₹18,000 receipt | 0 | 15,000 | 0 |
| `LN-DP-0004` | No payment | 8,000 | 8,000 | 8,000 |
| `LN-DP-0005` | No payment | 0 | 11,000 | 0 |
| `LN-DP-0006` | Future ₹9,000 receipt excluded | 0 | 9,000 | 0 |
| `LN-DP-0007` | Part ₹5,000 receipt | 0 | 13,500 | 0 |

The final scoped totals SHALL be `CurrentDue = 48,500`, `Arrears = 18,000`, `TotalCharges = 0`, and `TotalDemand = 66,500`.

### R8 — Recoverable and fatal errors (Unwanted-event)

If a receipt is malformed, outside the cutoff, mismatched, or unsafe, the test adapter SHALL exclude it, log only a bounded safe reference and reason code, and continue with other accounts. It SHALL NOT log the full description, personal identity values, secrets, or connection strings.

If options, the environment guard, catalog validation, reference locations, the external root, ownership, foreign keys, mappings, or aggregate validation fail, the seed command SHALL fail before commit, roll back all changes, and exit with a non-zero code. The message SHALL identify only safe entity, logical key, period, or aggregate information.

### R9 — Data tool guard and isolation (Unwanted-event, Optional-feature)

The `Finnova.Data` project SHALL NOT be referenced by any service host, SHALL NOT register itself in any dependency-injection container, and SHALL NOT run at host startup.

If the resolved environment is not `Development` or `Testing`, the `seed` and `verify` commands SHALL refuse to run and exit with a non-zero code before opening a database connection. `migrate` SHALL follow the same guard so the tool cannot alter a production database by accident. The connection string SHALL come from configuration (`ConnectionStrings:FinnovaConnection`, environment variables, or an ignored local file) and SHALL NOT be committed or logged.

The `Finnova.AccountsService`, `Finnova.UAService`, and `Finnova.SystemAdminService` projects SHALL NOT contain sample options, sample adapters, sample seeders, or sample flags.

### R10 — Reference-data migration (Event-driven)

When migrations are applied, the reference migration SHALL insert `TN` (level 2, parent `IND`) and the cities `CHN`, `CBE`, `MDU`, `SLM`, `TRI`, `TNJ` (level 3, parent `TN`) only when the parent exists and has no child with that code. It SHALL be safe to apply on a database that already contains any of those rows. Its `Down` SHALL remove only the rows it inserted and only when they have no children. The migration SHALL NOT change the model snapshot or any existing row.

### R11 — Previous-demand seam (Ubiquitous)

`Finnova.Service` SHALL expose `IPreviousDemandPort` with a registered null default. `DemandProcessor` SHALL use `previous?.Arrears ?? 0m` as prior arrears. All other demand rules, ports, and registrations SHALL stay unchanged, so production behavior is identical to today.

## Non-Functional Requirements

- IDs, descriptions, timestamps, hashes, periods, and aggregate values SHALL be repeatable across machines.
- All amounts SHALL be decimal and rounded away from zero to two places.
- The seed SHALL accept cancellation, use one SQL transaction, and be safe to retry after a rollback.
- Catalog and parser logic SHALL have unit tests, and FsCheck properties SHALL run at least 100 iterations.
- SQL tests SHALL cover the reference migration, foreign keys, idempotency, repair, unowned-collision rollback, fingerprint stability, history linkage, and exact previous-demand tuples.
- The host test SHALL create a unique SQL Server database, migrate it, seed it with `Finnova.Data`, and drop only that database during disposal.

## Acceptance Criteria

1. `dotnet ef migrations add SeedTamilNaduReferenceLocations` followed by the documented edit produces a migration that creates `TN` and six cities on a migrated database, is safe to apply when any code already exists, and leaves `FinnovaDbContextModelSnapshot.cs` unchanged.
2. `dotnet run --project Finnova.Data -- seed demand-sample` on a migrated Development database commits all documented entities without changing `IND`, `TN`, or the cities.
3. A second seed returns the same fingerprint, creates no duplicates, keeps unchanged timestamps and fields, repairs an owned changed or deleted row, and rolls back on an unowned deterministic or natural-key collision.
4. A seed on a database without the reference locations fails before any write and names the migration.
5. `seed`, `verify`, and `migrate` exit non-zero without opening a connection when the environment is `Production` or unrecognized.
6. Catalog tests reject duplicate logical keys, duplicate natural keys, invalid parent codes, broken branch mappings, non-contiguous periods, and invalid receipt definitions before the transaction begins.
7. The hierarchy test proves every `IND → TN → city → zone → branch` path and the explicit branch mappings.
8. Receipt tests accept full, part, overpayment, and none cases and reject every documented invalid case without producing an allocation event.
9. Each of the five history periods has eight branch scopes, linked published snapshots, seven results, seven rows, seven ready batches, one `NoDemand` batch, evidence, audit, and reconciled totals.
10. Previous-demand tests accept only the exact published owned tuple and reject wrong marker, account, branch set, LOB, frequency, linkage, revision, publication, and ownership.
11. The authenticated March flow in `Finnova.Tests` returns seven rows and eight batches with totals `48,500 / 18,000 / 66,500`. A one-branch run reads and writes only that branch.
12. No file under `Finnova.AccountsService`, `Finnova.UAService`, or `Finnova.SystemAdminService` mentions sample data, and none of those projects references `Finnova.Data`.
13. The authoring action changes only the spec documents. No source, migration, database, frontend, build, or test file is changed and no build or test command is run.

## Out of Scope

- Real customer, KYC, payment, bank, PAN, Aadhaar, credential, or production data.
- Arabic, non-English, bilingual, translation, localization, or RTL support.
- Production activation of any sample data, request-controlled flags, or a data-seeding API endpoint.
- Real database-backed account, schedule, and allocation-event ports for production Demand Processing. That is a separate feature.
- A selective `reset` command.
- GL journal implementation, settlement, spooling, document generation, or Demand Processing rule changes beyond the previous-demand seam.
- Frontend source changes.
- Running migrations, seed commands, builds, tests, or database commands during specification authoring.
