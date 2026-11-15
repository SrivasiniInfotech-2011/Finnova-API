# Demand Processing Sample Data Requirements

## Summary

Provide a deterministic, synthetic fixture for the existing Demand Processing flow. The fixture must seed lookups, customers, Tamil Nadu locations and branches, applications, loan accounts, generic account projections, receipts, finalized schedules, and previous-demand history for six monthly periods. It must let an authorized test run process March 2026 through the real Accounts Service pipeline while keeping the fixture disabled outside an explicitly enabled Development or Testing host.

The specification is backend-only. The existing gateway alias and Finnova-UI Demand Processing screen remain the consumer path; no frontend source change is needed.

## Introduction

Finnova is an India-only financial platform. The fixture therefore contains English-only synthetic values, Tamil Nadu locations, Indian Rupee amounts, and `Asia/Kolkata` business dates. It uses existing EF Core tables and Demand Processing ports. It does not add a schema table or a fixture migration because the current model already contains the required entities and demand history tables. Existing migrations must be applied before the fixture is seeded.

The fixed fixture window is October 2025 through March 2026. October 2025 through February 2026 are published previous-demand periods. March 2026 is source data for the real processor and is not pre-published by the seed.

## Glossary

- **Fixture**: The complete synthetic dataset identified by `SAMPLE-DP-V1`.
- **Dataset key**: The immutable fixture identifier used in deterministic IDs, ownership records, descriptions, and diagnostics.
- **Business period**: A year/month interpreted in `Asia/Kolkata`; persisted technical timestamps are UTC.
- **External India root**: The existing active `Location` with ID `00000000-0000-0000-0000-000000000001`, code `IND`, name `India`, level 1, and no parent.
- **Owned row**: A fixture row whose deterministic logical key is recorded in the `DemandProcessing/SAMPLE_OWNERSHIP` lookup catalog with its ID and canonical content hash.
- **Loan account**: The `LoanAccount` entity consumed by the Demand Processing account source.
- **Generic account projection**: The separate `Account` row with the same account number. `Transaction.AccountId` references this row.
- **Previous demand**: A published `DemandSnapshot` and `DemandRow` from the immediately preceding period with matching account, branch, LOB, frequency, revision, and cutoff dimensions.
- **Seed run**: One transactional execution of the fixture seeder.
- **Idempotent**: A repeat seed has the same IDs and fingerprint, creates no duplicates, preserves unchanged persisted values, repairs owned changes, and never overwrites an unowned collision.
- **EARS**: Easy Approach to Requirements Syntax.

## Dependencies and Assumptions

1. **Assumption — fixed window:** “Last six months” means October 2025 through March 2026, anchored at `2026-03-31`. Confirm if the window must move relative to the execution date.
2. **Assumption — current-period behavior:** March 2026 is processed by the existing processor. Only October 2025 through February 2026 receive seeded published demand history.
3. **Assumption — external root:** The existing `IND` row is migration-owned. The seeder verifies its invariants and never changes, owns, or recreates it.
4. **Assumption — ownership metadata:** Ownership is stored in existing `LookupValue` rows with `Module = DemandProcessing` and `LookupType = SAMPLE_OWNERSHIP`; no new table is introduced.
5. **Assumption — account relationship:** Demand reads `LoanAccount`, while receipts reference the generic `Account` table. Each sample loan has one explicitly mapped generic account projection.
6. **Assumption — catalog size:** The fixture contains eight customers, eight level-5 branches, seven processed applications, seven active loan accounts, seven generic account projections, seven finalized schedules, and six installments per schedule. Customer 8 and branch `MDU-B02` deliberately exercise empty-result paths.
7. **Assumption — customer information:** Customer names and contact data are synthetic. No PAN, Aadhaar, bank account, password, credential, document, or real-person data is seeded.
8. **Assumption — lookup values:** All lookup values are technical test values and English display values. Ownership rows are excluded from business lookup assertions.
9. **Assumption — receipts:** Receipts are existing `Transaction` rows. Invalid receipt rows are intentionally persisted as owned test inputs and must be excluded by the source adapter.
10. **Assumption — previous arrears seam:** The current processor passes zero previous arrears. A new `IPreviousDemandPort` is required; the normal implementation returns no previous demand and the sample implementation reads only validated published fixture history.
11. **Assumption — charges and journal:** The default charge adapter and configured journal port are used. The fixture expects zero ODI and other charges and requires the normal journal checks to pass in the enabled test profile.
12. **Assumption — activation contract:** Fixture behavior occurs only when both `DemandProcessing:UseSampleAdapters` and `DemandProcessing:SeedSampleDataOnStartup` are true in Development or Testing. Any partial configuration leaves production ports active; either flag in Production is a startup error.
13. **Assumption — SQL verification:** Seeder and host tests use an isolated SQL Server database because transaction, migration, foreign-key, and startup behavior must be tested relationally. EF Core InMemory is not a substitute for these tests.
14. **Assumption — frontend:** The existing `/api/ua/api/demand-processing` gateway alias and current Finnova-UI service/page are sufficient. No frontend task is required.

## Functional Requirements

### R1 — Dataset and localization (Ubiquitous)

The fixture SHALL use `SAMPLE-DP-V1`, English-only labels, synthetic Indian names, Tamil Nadu locations, INR amounts, `Asia/Kolkata` business periods, and UTC technical timestamps. It SHALL not add bilingual fields, non-English seed data, translation scaffolding, RTL behavior, or sensitive identity data.

The fixture SHALL include the external `IND` root, `TN`, six cities, six zones, eight branches, ten business lookup values, one organization, one active LOB, eight customers with one address and one individual-details row each, seven processed applications, seven active loan accounts, seven generic accounts, seven finalized schedules with six installments each, valid and invalid receipts, and five published history graphs.

### R2 — Referential relationships (Complex)

When a seed commits, every owned foreign key SHALL resolve. Customers SHALL reference the sample organization and active English lookup values. Applications SHALL reference customers, the sample LOB, and explicit level-5 branches. Loan accounts SHALL reference applications, customers, the LOB, and branches. Generic accounts SHALL share loan account numbers. Valid transactions SHALL reference only the mapped generic account IDs. Schedules and installments SHALL reference applications and schedules. History runs, branch scopes, revisions, billing stages, billing results, evidence, snapshots, rows, batches, and audit entries SHALL reference their persisted parents.

The hierarchy SHALL be `IND` level 1 → `TN` level 2 → city level 3 → zone level 4 → branch level 5. Branch-to-zone, branch-to-city, application-to-branch, account-to-branch, and application-to-account relationships SHALL be explicit catalog mappings; SQL row order SHALL never infer them.

### R3 — Deterministic identity, canonical ownership, and collision safety (State-driven)

While a fixture row is owned, its ID SHALL be derived from `(dataset key, entity type, logical key)` with SHA-256, and its ownership record SHALL contain a canonical hash of every persisted fixture business field, including foreign keys and status values. A deterministic-ID row or natural-key row occupied without a matching ownership record SHALL cause a safe collision error and transaction rollback. The seeder SHALL reject an unowned deterministic row even when its natural key is absent.

The manifest fingerprint SHALL be calculated from sorted non-manifest ownership records only. The manifest SHALL not change the input used to calculate its own fingerprint.

### R4 — Validation and transactional idempotency (Event-driven)

When a seed starts, it SHALL validate the configured key, fixed as-of date, timezone, catalog uniqueness, parent topology, mapping completeness, receipt invariants, and external `IND` invariants before opening the write transaction. It SHALL then upsert dependency-first, validate all relationships and aggregate equations, persist a manifest, and commit only after all checks pass.

When the same fixture is seeded again, it SHALL return the same fingerprint, create no duplicates, preserve unchanged field values and timestamps, and repair owned rows deleted or changed by a test. It SHALL not overwrite an unowned ID or natural-key collision.

### R5 — Receipt source and strict parsing (Event-driven)

A valid receipt description SHALL contain exactly eleven pipe-delimited tokens:

```text
SAMPLE-DP|V1|SAMPLE-DP-V1|RECEIPT|<loan-account-number>|<yyyy-MM>|<FULL|PART|OVER|NONE>|<amount>|Deposit|Completed|<reference>
```

The source adapter SHALL validate token count, dataset, version, event type, loan account number, target period, invariant-culture amount, `Transaction.Type`, `Transaction.Status`, non-null bounded reference equality, generic-account mapping, persisted `Transaction.Amount` equality, amount semantics, and the `Asia/Kolkata` cutoff converted to UTC. `NONE` requires zero amount; the other kinds require a positive amount. Invalid rows SHALL produce no allocation event and a bounded reason code.

Invalid cases SHALL include malformed token count, wrong dataset, wrong loan number, negative amount, `NONE` with a positive amount, persisted amount mismatch, null or unsafe reference, wrong entity type, wrong entity status, and a transaction after the local business-date cutoff.

### R6 — Schedules and previous history (State-driven)

Each application schedule SHALL be finalized and contain exactly six installments due on the 15th of October 2025 through March 2026. Each historical period SHALL contain seven succeeded billing results, seven demand rows, eight branch-scope rows, eight batches, seven `ReadyToSpool` batches, and one `NoDemand` batch for `MDU-B02`. Billing and demand snapshots SHALL be published and linked to the same run and revision. History SHALL include owned `BillingAllocationEvidence` and `DemandAuditEntry` records.

The previous-demand adapter SHALL return arrears only when the exact preceding run, revision, stage, billing snapshot, demand snapshot, ownership record, account, LOB, frequency, period, cutoff, and branch-set dimensions match and both snapshots are published. Missing, unpublished, unlinked, mismatched, extra-branch, missing-branch, or unowned tuples SHALL return no result without throwing.

### R7 — March processing outcomes (Complex)

When an authorized March 2026 run starts for the sample LOB without a branch filter, the sample account source SHALL return exactly the seven fixture accounts across the eight fixture branches. The existing processor SHALL produce seven rows and eight batches, including `MDU-B02` as `NoDemand`, with no unrelated account or branch.

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

If a receipt is malformed, outside the cutoff, mismatched, or unsafe, the adapter SHALL exclude it, log only a bounded safe reference and reason code, and continue other accounts. It SHALL never log the full description, personal identity values, secrets, or connection strings.

If options, catalog validation, the external root, ownership, foreign keys, mappings, or historical aggregate validation fail, the seeder SHALL fail before commit and roll back all fixture changes. The error SHALL identify only safe entity, logical key, period, or aggregate information.

### R9 — Optional local/test fixture mode (Optional-feature)

When both fixture flags are true in Development or Testing, the Accounts host SHALL register the sample ports and hosted seeder. When both flags are false, or exactly one is true, production ports SHALL remain registered and no fixture seed SHALL run. Either flag set in Production SHALL fail host startup with a configuration error. Existing JWT authentication, Demand policies, LOB scope, and branch scope SHALL remain mandatory.

## Non-Functional Requirements

- IDs, descriptions, timestamps, hashes, periods, and aggregate values SHALL be repeatable across machines.
- All amounts SHALL be decimal and rounded through `BillingRules.Money`.
- The seed SHALL accept cancellation, use one SQL transaction, and be safe to retry after rollback.
- Pure catalog and parser logic SHALL have unit tests and FsCheck properties with at least 100 iterations.
- SQL tests SHALL cover migration compatibility, foreign keys, idempotency, repair, unowned collision rollback, ownership fingerprint stability, history publication/linkage, branch filtering, exact previous-demand tuples, and receipt rejection.
- The host test SHALL initialize a unique SQL Server database by applying existing migrations and running the seeder, and SHALL remove only that database during disposal.

## Acceptance Criteria

1. On a migrated isolated SQL Server database containing the verified external `IND` row, one seed commits all documented fixture entities and counts without changing `IND`.
2. A second seed returns the same fingerprint, creates no duplicates, leaves unchanged timestamps and business fields equal, repairs an owned changed or deleted row, and rolls back on an unowned deterministic or natural-key collision.
3. Catalog tests reject duplicate logical keys, duplicate natural keys, invalid parent codes, broken branch mappings, non-contiguous six-period windows, and invalid receipt definitions before the transaction begins.
4. The hierarchy test proves each `IND → TN → city → zone → branch` path and the explicit branch mappings.
5. Receipt tests accept full, part, overpayment, and zero/no-receipt cases, and reject every documented invalid case without producing an allocation event.
6. Each of the five history periods has eight branch scopes, linked published snapshots, seven results, seven rows, seven ready batches, one `NoDemand` batch, evidence, audit, and reconciled totals.
7. Previous-demand tests accept only the exact published owned tuple and reject wrong marker, account, branch set, LOB, frequency, linkage, revision, publication, and ownership.
8. The authenticated March flow returns seven rows and eight batches with totals `48,500 / 18,000 / 66,500`; a one-branch run reads and writes only that branch.
9. DI tests cover `(false,false)`, `(true,false)`, `(false,true)`, `(true,true)`, and Production activation behavior.
10. The authoring action changes only the four requested specification artifacts (the three documents and config); no backend, database, frontend, build, or test file is changed and no build/test command is run.

## Out of Scope

- Real customer, KYC, payment, bank, PAN, Aadhaar, credential, or production data.
- Arabic, non-English, bilingual, translation, localization, or RTL support.
- Production activation, request-controlled fixture flags, or a fixture API endpoint.
- New schema tables, fixture `HasData`, GL journal implementation, settlement, spooling, document generation, or changes to Demand Processing business rules beyond the previous-demand port seam.
- Frontend source changes. Existing gateway routing and Finnova-UI service/page are reused.
- Running migrations, seed commands, builds, tests, or database commands during specification authoring.
