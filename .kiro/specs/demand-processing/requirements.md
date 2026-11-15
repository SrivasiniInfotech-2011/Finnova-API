# Requirements Document

## Introduction

The Demand Processing feature provides an operational MIS of account dues and arrears for a billing period. Demand Processing contains two ordered internal stages: **Period Billing** and **Demand Generation/Spooling**. Period Billing reads the finalized Amortization Schedule, applies approved account-level financial allocations, and produces the period Billing Result. Demand Generation uses that Billing Result to create account-level demand, groups the results by branch, and makes branch-wise demand batches available for operational spooling. The overall Demand Run is the operational unit for the ordered stages, their status, diagnostics, audit history, and final snapshot. The feature covers the backend API in `e:\Finnova\Finnova-API` and the React frontend in `E:\Finnova\Finnova-UI\Finnova-UI`.

The supplied FRS excerpt, section 1.5.4.27.31 on page 42, explicitly describes Demand Processing as a MIS of dues and arrears processed automatically after billing is complete. The excerpt also states that Automizer triggers branches with Demand Spooling. The surrounding FRS context states that billing generates installment dues, debits overdue accounts, and generates billing journals. This document treats those statements as the explicit FRS baseline. The clarified scope makes Billing an internal, ordered Demand Processing stage rather than an entirely external prerequisite. The requested module shape of Demand Run, Demand Generation Output, and Demand Spooling, together with the listed inputs and outputs, is also captured below. The attached screenshot is corroborating source material only and does not add behavior beyond the quoted FRS text.

A Demand Run accepts Line of Business (LOB), Branch, Frequency, and Month/Year. Period Billing reads the finalized account Amortization Schedule for the selected period, applies approved receipts, waivers, reversals, adjustments, and other account-level allocation rules exactly once, produces the Billing Period Result and billing journals, and then Demand Generation produces account-level output containing account, customer, Current Due, Arrears, ODI, Other Charges, and Total Demand. The baseline processing rules include billing only eligible Active accounts according to the approved cut-off, excluding Closed, Cancelled, and Pre-Closed accounts, using Current Due + Arrears + Charges, creating branch-wise batches, and retaining Month/Year history. Demand Processing does not independently calculate EMI or mutate the Amortization Schedule.

This is an operational processing feature, not a master-data feature. It does not create or manage LOB, Branch, Customer, Account, Amortization Schedule, receipt, billing, installment, or accounting master records. It consumes or orchestrates the authoritative records and contracts owned by those areas. Where the FRS or request is silent, this document records the confirmed default or an explicitly deferred implementation detail rather than presenting an unapproved behavior as an explicit FRS requirement.

### Explicit FRS Baseline

The following behaviors are treated as explicit requirements from the supplied FRS excerpt and surrounding context or from the user's stated baseline rules:

- Demand Processing produces a MIS of dues and arrears.
- Demand Processing contains two ordered internal stages: Period Billing, followed by Demand Generation/Spooling; the Demand Run is the operational unit.
- Period Billing reads the finalized Amortization Schedule, applies approved account-level allocations, and produces the Billing Period Result before Demand Generation.
- Demand Generation runs only after the internal Billing stage completes successfully.
- Automizer triggers the Demand Run and branches with Demand Spooling.
- Billing journals are generated and posted as part of the Billing stage before the Demand snapshot is published.
- The module includes Demand Run, Demand Generation Output, and Demand Spooling screens.
- Demand Run inputs are LOB, Branch, Frequency, and Month/Year.
- Demand output includes account, customer, Current Due, Arrears, ODI, Other Charges, and Total Demand.
- Only Active and otherwise eligible accounts are included; Closed, Cancelled, and Pre-Closed accounts are excluded.
- Current Due and Arrears are sourced from Billing results, and Demand Processing does not recompute EMI or alter the Amortization Schedule.
- Demand is calculated as Current Due + Arrears + Charges.
- Demand results are grouped into branch-wise batches.
- Demand history is retained by Month/Year.

### Dependencies and Assumptions

#### Confirmed Decisions

The following decisions were confirmed by the product owner and supersede obsolete open-assumption wording below. Items marked out of scope or deferred are intentionally excluded from this release.

- **CD1 (Billing contract and Frequency, resolves A4, A25, A26 in part):** The Billing completion/snapshot contract and the supported Frequency values are configuration-driven through appSettings. The default Frequency is `Monthly`. Demand Processing does not hard-code any other Frequency.
- **CD2 (Period and timezone, resolves A3):** Month/Year is derived in the `Asia/Kolkata` business timezone. Technical and audit timestamps are UTC.
- **CD3 (Branch scope, resolves A5):** An omitted Branch means all authorized **Active** Branches for the selected LOB. Deactivated Branches are excluded from new Demand generation. Previously generated and retained Demand data for a deactivated Branch remains retrievable for read, query, and output.
- **CD4 (Allocation, resolves A7 and A27 in part):** Part-payments, waivers, reversals, adjustments, and suspense are all taken into account in Current Due, Arrears, ODI, and Other Charges. Each event is applied exactly once under the approved allocation policy.
- **CD5 (Account status, resolves A8 in part):** The status baseline is unchanged. `Active` is included. `Closed`, `Cancelled`, and `Pre-Closed` are excluded.
- **CD6 (Zero demand and empty Branch, resolves A17 and the A10 empty-batch choice):** All Active accounts are included, including accounts with zero demand, which are retained with a zero-demand indicator. A Branch with no eligible accounts produces an empty/no-demand batch outcome and no error.
- **CD7 (Manual run and rerun, resolves A12 and A13 in part):** A Manual Demand Run is supported alongside the Automizer trigger. Both use the same ordered Billing-then-Demand workflow, scope, authorization, and idempotency rules. A controlled rerun never overwrites delivered output and creates a linked Run Revision.
- **CD8 (Branch isolation, resolves A16):** Branch isolation is confirmed. A failed Branch Batch is isolated as `Failed` with diagnostics, remains separately inspectable and retryable, and does not affect other Branch Batches. The overall Run is `PartiallyFailed`. A Billing-stage failure that blocks the Billing Snapshot still blocks all Demand Generation.
- **CD9 (Spooling, resolves A11 as deferred):** Spool transport, downstream acknowledgement, retry limits, and downstream idempotency are **out of scope for now (deferred)**. Only the minimal spool status and the ready-to-spool guard remain in scope.
- **CD10 (Authorization and retention, resolves A20 in part, A19 and A30 as configurable/deferred):** Authorized roles and policy names are configuration-driven through appSettings, with defaults and an admin-role override. No role name is hard-coded. The retention period and purge authority are configurable and deferred, and are not hard-coded.
- **CD11 (Cut-off and schedule source/version contract, out of scope):** The detailed source of the Processing Cut-off and any Amortization Schedule finalization watermark or source-version integration are out of scope and deferred. Billing and Demand Generation SHALL use one internally consistent run snapshot/cut-off and SHALL retain whatever cut-off or source reference is available. This release SHALL NOT require a new schedule watermark or source-version integration.
- **CD12 (Default charge policy):** ODI is overdue principal balance multiplied by the configured annual ODI rate, overdue days, and the reciprocal of the configured day-count basis, with default day-count basis `365`; ODI excludes the current-period Scheduled Installment, applies a configured cap when present, has no cap by default, and is rounded to INR 2 decimals. Other Charges are net eligible posted non-ODI charge events effective for the account and period, less eligible waivers, reversals, and adjustments allocated to those charge events after suspense treatment; negative net values are clamped to zero after adjustment evidence is retained, and the result is rounded to INR 2 decimals. `Total Charges = ODI + Other Charges` and `Total Demand = Current Due + Arrears + Total Charges`. Billing/Accounting owns source event records, and Demand Processing consumes the published Billing result without recomputing these values. A configured Billing/charge adapter may replace the default policy without changing Demand output contracts.
- **CD13 (Accounting/GL journal ownership):** The existing Accounting/GL capability is the authoritative owner of billing journal creation, posting, idempotency, and reconciliation. AccountsService/Demand Processing orchestrates that capability through a narrow journal port and stores journal references and check results only. SystemAdmin may configure journal policy and adapter settings through appSettings but does not own the accounting ledger. Demand Snapshot publication remains blocked until configured required journal checks pass.
- **CD14 (PDF export and print scope):** Authorized users can request PDF exports for Demand Generation Output and Demand Spooling/batch views. Each PDF contains persisted values, run/period/branch metadata, totals, and a generated-at timestamp, and the export applies the caller's data scope without recalculation. Browser print is out of scope and deferred.
- **CD15 (Minimum audit record):** Run and Batch transitions require an immutable minimum audit record containing Run ID, Run Revision, scope, actor/source, correlation ID, previous and new status where applicable, UTC timestamp, outcome/error code, and safe references. The detailed event taxonomy, additional audit fields, content schema, retention, and purge rules are configurable or deferred.
- **CD16 (Implementation defaults):** The initial host is `Finnova.AccountsService`, Period Billing uses the existing AccountsService worker boundary, Run ID is an immutable GUID, Active-only account eligibility applies, branch isolation applies, controlled rerun creates a linked Run Revision, appSettings drive Frequency, roles, policies, and charge/journal settings, and monetary values use INR `decimal(19,2)` with `MidpointRounding.AwayFromZero`. Month/Year uses `Asia/Kolkata` and technical timestamps use UTC. The default charge composition is `ODI + Other Charges`.

- **Dependency (Finalized Amortization Schedule):** Period Billing consumes the authoritative finalized Amortization Schedule for each eligible account and requested scope. The schedule supplies the selected period's scheduled principal and scheduled interest/EMI; Demand Processing does not own, revise, or recompute the schedule. This release does not require a new schedule watermark or source-version integration. Billing and Demand use one internally consistent run snapshot/cut-off and retain whatever schedule reference is available.
- **Dependency (Receipts and account-level allocation sources):** Period Billing depends on authoritative receipt/collection records and approved waiver, reversal, adjustment, and other account-level allocation policies. The owning sources remain responsible for their records; Billing applies each eligible financial event once under the approved allocation contract.
- **Dependency (Billing and Demand accounting boundary):** Period Billing depends on the existing Accounting/GL capability as the authoritative owner of journal creation, posting, idempotency, and reconciliation. AccountsService/Demand Processing calls that capability through a narrow journal port, stores only journal references and check results, and SHALL publish no Billing Snapshot or Demand Snapshot until the configured required journal checks pass. SystemAdmin can configure policy and adapter settings through appSettings but does not own the ledger.
- **Dependency (Account, Customer, and master sources):** Demand Processing consumes authoritative Account and Customer records and their status, Branch, LOB, and identifier relationships. It does not duplicate or manage those masters. LOB, Branch, Account, Customer, Amortization Schedule, receipt/collection, and related master/source ownership stays with the existing owning modules.
- **Dependency (Automation):** Automizer or the approved scheduler supplies the normal trigger, scope, period, authentication, correlation, and retry signal for the overall Demand Run. The existing integration provides the operational trigger; endpoint and transport details are implementation details defined in design and do not change the Demand Processing behavior.
- **Dependency (Repositories):** Backend implementation belongs in `e:\Finnova\Finnova-API` and must integrate with the existing .NET service, repository, EF Core, CQRS, authentication, middleware, and gateway conventions selected during design. Frontend implementation belongs in `E:\Finnova\Finnova-UI\Finnova-UI` and must use the existing React 18, TypeScript, Vite, Material UI, shared axios, service-interface, real-service, mock-service, and API-toggle conventions.
- **Dependency (Charge sources):** Billing/Accounting owns charge and adjustment event records. The default Billing policy calculates ODI and Other Charges as defined by CD12, publishes them as separate Billing Period Result components, and Demand Processing consumes those published values without recalculating them after publication. A configured Billing/charge adapter may provide the same output contract.
- **Assumption A1 (Host placement) — CONFIRMED (CD16):** Demand Processing is hosted initially in `Finnova.AccountsService`, because the feature consumes billing and account transactions. The existing AccountsService worker boundary is used for Period Billing; a separate Billing deployment is not required in this release.
- **Assumption A2 (Run identity) — CONFIRMED (CD16):** Each accepted Demand Run receives an immutable system-generated GUID Run identifier.
- **Assumption A3 (Period identity) — CONFIRMED (CD2, CD16):** Month/Year identifies one billing and demand period in the `Asia/Kolkata` business timezone. Technical and audit timestamps are UTC. Month/Year must be a valid Normalized Input; detailed period-range and future-period policy is deferred to implementation configuration and does not change the period identity.
- **Assumption A4 (Frequency values) — CONFIRMED (CD1):** Frequency is a configuration-driven value supplied through appSettings and aligned with Billing, with default `Monthly`. Demand Processing must not accept a frequency that is not in the configured set.
- **Assumption A5 (Scope semantics) — CONFIRMED (CD3):** A selected Branch identifies one branch. An omitted Branch represents all authorized **Active** branches for the selected LOB. A selected LOB is mandatory.
- **Assumption A6 (Charge composition) — CONFIRMED (CD12, CD16):** `Total Charges` equals `ODI + Other Charges`. ODI uses overdue principal balance x configured annual ODI rate x overdue days / configured day-count basis, with default day-count basis `365`, no cap by default, optional configured cap, exclusion of the current-period Scheduled Installment, and INR 2-decimal rounding. Other Charges use net eligible posted non-ODI charge events effective for the account and period, less eligible waivers, reversals, and adjustments allocated to those charge events after suspense treatment; negative net values are clamped to zero after retaining adjustment evidence and rounded to INR 2 decimals. Billing/Accounting owns source events, while Demand Processing consumes the Billing result and does not recompute the values after publication. A configured Billing/charge adapter may replace the default policy without changing output contracts.
- **Assumption A7 (Current Due and Arrears) — CONFIRMED (CD4):** Current Due is the unpaid selected-period Scheduled Installment after approved receipts, part-payments, waivers, reversals, adjustments, suspense, and other account-level allocation rules are applied exactly once. Arrears is the unpaid amount carried from previous Billing Period Results, excluding ODI and Other Charges. The detailed allocation ordering and excess-receipt treatment are defined by the approved allocation policy (see A27).
- **Assumption A8 (Status source and timing) — CONFIRMED (CD5, CD11):** Account status is evaluated against the Active-only eligibility rule within the Demand Run's internally consistent snapshot/cut-off. `Active` is included; `Closed`, `Cancelled`, and `Pre-Closed` are excluded. The detailed cut-off source contract and schedule finalization watermark/source-version integration are out of scope and deferred; the run retains whatever cut-off or source reference is available.
- **Assumption A9 (Branch ownership) — CONFIRMED:** Each included account has one resolvable Branch and LOB at the internally consistent run snapshot. Accounts with missing, inactive, or conflicting organizational references are excluded and reported as data-quality failures rather than silently assigned to a batch.
- **Assumption A10 (Batch semantics) — CONFIRMED (CD6) for empty Branches:** Demand Processing creates one logical Demand Batch per branch within a Demand Run. A batch contains the account-level demand rows for that branch, a deterministic batch status, and totals. A Branch with no eligible accounts yields an empty/no-demand batch outcome and no error. Physical spool files and downstream queue messages are deferred (see A11).
- **Assumption A11 (Spooling semantics) — OUT OF SCOPE / DEFERRED (CD9):** Spool transport, downstream acknowledgement, retry limits, and downstream idempotency are deferred and not specified in this version. Only the minimal batch status (`ReadyToSpool` guard and spool status value) is in scope. Demand Spooling never recalculates demand.
- **Assumption A12 (Automation versus manual run) — CONFIRMED (CD7):** Automizer is the normal trigger. A Manual Demand Run is supported for authorized users and follows the same ordered Billing-then-Demand workflow, scope, authorization, and idempotency rules. The permitted roles are configuration-driven (see A20).
- **Assumption A13 (Rerun policy) — CONFIRMED (CD7):** Repeating the same LOB, branch scope, frequency, and Month/Year is treated as a rerun of the same logical period. An unchanged completed result is returned as is. An explicit authorized rerun creates a linked Run Revision, retains the prior revision, and never overwrites delivered output.
- **Assumption A14 (Concurrency policy) — CONFIRMED:** At most one active calculation or controlled rerun may hold the processing lease for one logical scope key at a time. Concurrent requests for the same scope resolve to the existing active Run or a conflict response and do not create duplicate demand rows or branch batches.
- **Assumption A15 (Snapshot consistency) — CONFIRMED (CD11):** A Demand Run reads one internally consistent snapshot/cut-off of Billing, Account, Customer, and charge data. The run retains whatever cut-off or source reference is available. The implementation mechanism is a design choice, and this release does not require a new schedule watermark, finalization version, or source-version integration.
- **Assumption A16 (Partial branch failures) — CONFIRMED (CD8):** Branch isolation applies. A failure for one branch marks that branch batch Failed, keeps it separately inspectable and retryable, preserves successful branch batches, and marks the overall Run PartiallyFailed. All-or-nothing completion is not used. A Billing-stage failure still blocks all Demand Generation.
- **Assumption A17 (Zero demand) — CONFIRMED (CD6):** All Active accounts are included. Accounts with a total demand of 0.00 INR remain in the output with an explicit zero-demand indicator, consistently across output totals and spooling.
- **Assumption A18 (Precision and currency) — CONFIRMED (CD16):** All amounts are INR and use `decimal(19,2)` storage and output precision. Monetary calculations and comparisons use `MidpointRounding.AwayFromZero`, including the default ODI and Other Charges policy.
- **Assumption A19 (History and retention) — CONFIGURABLE / DEFERRED (CD10):** Demand Runs, input scope, billing reference, account-level output, branch batches, spooling status, failures, actor, timestamps, and revisions are retained. The retention duration, archival mechanism, and purge authority are configurable and deferred, and no value is hard-coded.
- **Assumption A20 (Authorization) — CONFIRMED (CD10):** Demand Run, Demand Generation Output, and Demand Spooling require an authenticated JWT Bearer token. The roles and policy names for run/rerun (including Manual Demand Run), output/history read, and spool actions are read from appSettings, with default values and an admin-role override. No role name is hard-coded in code.
- **Assumption A21 (Audit) — CONFIRMED (CD15):** Run and Branch Batch transitions append an immutable minimum audit record containing Run ID, Run Revision, scope, actor/source, correlation identifier, previous and new status where applicable, UTC timestamp, outcome/error code, and safe references. The detailed event taxonomy, additional fields, content schema, retention duration, archival mechanism, and purge authority are configurable or deferred.
- **Assumption A22 (Frontend behavior) — CONFIRMED in scope (CD14):** The three requested screens are role-protected operational screens. Demand Run starts or requests a run, Demand Generation Output reads retained account-level results and supports authorized PDF export, and Demand Spooling reads branch batches, invokes the approved spool action, and supports authorized PDF export. Export contains persisted values, run/period/branch metadata, totals, and generated-at timestamp, follows caller scope, and does not recalculate. Browser print is out of scope and deferred. Navigation labels, route names, filters, pagination, and polling interval are implementation details defined in design.
- **Assumption A23 (Testing):** The calculation and scope-selection logic are pure or isolated enough for FsCheck property-based tests with at least 100 iterations. Backend authorization, billing stage, schedule-driven allocation, idempotency, concurrency, failure handling, journal checks, and gateway wiring use xUnit examples and `WebApplicationFactory<Program>` integration tests. Frontend services and screen behavior use Vitest and React Testing Library. These are verification expectations, not a replacement for the acceptance criteria.
- **Assumption A24 (Billing placement) — CONFIRMED (CD16):** Period Billing initially runs as a background worker within `Finnova.AccountsService` as part of Demand Processing. A separate Billing deployment is not required in this release.
- **Assumption A25 (Billing trigger) — CONFIRMED (CD1, CD7):** The default Billing trigger is automatic through Automizer or the approved scheduler for a valid LOB, Branch, Frequency, and Month/Year scope. An authorized user may also start a Manual Demand Run, which includes the Billing stage.
- **Assumption A26 (Billing cut-off and eligibility) — CONFIRMED/DEFERRED (CD5, CD11):** The Billing completion/snapshot contract is configuration-driven and Active-only eligibility is confirmed. Billing and Demand Generation use one internally consistent run snapshot/cut-off and retain whatever reference is available. The detailed cut-off source, schedule finalization watermark, and source-version integration are out of scope and deferred; the release does not require a new source/version contract.
- **Assumption A27 (Allocation ordering) — DEFERRED POLICY DETAIL:** Receipts, part-payments, waivers, reversals, adjustments, and suspense are all considered in the demand calculation, and each source event is allocated exactly once. The exact ordering and excess-receipt treatment remain governed by the existing approved allocation policy; this release does not introduce a new allocation-ordering contract.
- **Assumption A28 (ODI and Other Charges policy) — CONFIRMED (CD12):** ODI and Other Charges are separate Billing Period Result components using the CD12 default formulas and INR 2-decimal rounding. ODI uses overdue principal only, excludes the current-period Scheduled Installment, uses configured annual rate/day-count/overdue-day/cap values, and has no cap by default. Other Charges use net eligible posted non-ODI charge events after suspense treatment and eligible waivers, reversals, and adjustments, exclude scheduled installment, principal/interest, arrears, and ODI, and clamp negative net values to zero after retaining adjustment evidence. Billing/Accounting owns source events, and Demand consumes the published values without post-publication recomputation. A configured charge adapter may replace the policy without changing output contracts.
- **Assumption A29 (Journal ownership) — CONFIRMED (CD13):** The existing Accounting/GL capability owns billing journal creation, posting, idempotency, and reconciliation. AccountsService/Demand Processing orchestrates the work through a narrow journal port, stores journal references and check results only, and blocks Demand Snapshot publication until configured required journal checks pass. SystemAdmin configures policy/adapter settings through appSettings but does not own the ledger.
- **Assumption A30 (Billing retention) — CONFIGURABLE / DEFERRED (CD10, CD15):** Billing Run status, diagnostics, available source references, allocation evidence, Billing Period Results, journal references, Billing Snapshot, and minimum audit records are retained. Duration, archival mechanism, purge authority, detailed audit event catalog, and additional audit content are configurable and deferred.
- **Note (Localization):** Finnova is India-only and English-only. All labels, statuses, errors, output values, seed/reference values, test data, and documentation examples are in English. The feature introduces no bilingual fields, non-English values, translation framework, RTL handling, or `dir="rtl"` behavior.

## Glossary

- **Demand Processing**: The operational capability that executes the ordered Period Billing and Demand Generation/Spooling stages for one Demand Run and produces a period-specific MIS of eligible account dues and arrears.
- **Demand Run**: One immutable operational unit containing the selected LOB, Branch scope, Frequency, Month/Year, ordered stage statuses, retained source references, snapshots, results, diagnostics, and audit history.
- **DemandProcessing_Service**: The backend API, application service, and initial background-worker boundary that validates a Demand Run, executes or resolves Period Billing, generates demand, creates branch batches, and exposes history.
- **DemandProcessing_UI**: The React/TypeScript/MUI frontend capability containing the Demand Run, Demand Generation Output, and Demand Spooling screens.
- **Demand Generation Output**: The account-level result of a Demand Run, including Account, Customer, Current Due, Arrears, ODI, Other Charges, Total Charges, Total Demand, Branch, LOB, and period/run references.
- **Demand Spooling**: The operational action and screen that makes completed branch-wise Demand Batches available to the branch process or approved downstream consumer. Demand Spooling does not recalculate demand.
- **Demand Batch**: The logical branch-scoped collection of Demand Generation Output rows belonging to one Demand Run, with batch totals, status, and spool metadata.
- **Demand Row**: One account-level output row in a Demand Batch.
- **Billing Stage/Run**: The ordered internal Period Billing stage and its retained execution record for one valid LOB, Branch scope, Frequency, and Month/Year. It reads finalized schedules, applies approved allocations, creates Billing Period Results, posts billing journals, and publishes the Billing Snapshot consumed by Demand Generation.
- **Billing Period Result**: The retained account-level result for one billed period, including the Scheduled Installment, allocated events, Current Due, Arrears, ODI, Other Charges, available schedule/source references, policy references, and billing status.
- **Scheduled Installment**: The selected period's scheduled principal plus scheduled interest/EMI read from the Account's finalized Amortization Schedule. Demand Processing treats this value as authoritative and does not recompute it.
- **Amortization Schedule**: The authoritative finalized account schedule containing period-level scheduled principal and scheduled interest/EMI, together with its identity and version. The owning Accounts or lending component maintains the schedule.
- **Current Due**: The unpaid amount of the selected period's Scheduled Installment after approved receipts, waivers, reversals, adjustments, and other account-level allocation rules are applied exactly once.
- **Arrears**: Previous unpaid amounts carried from earlier Billing Period Results after approved allocation, separate from the selected period's Current Due.
- **Billing Snapshot**: The consistent set of successful Billing Period Results, account eligibility, available schedule/source references, allocation outcomes, ODI, Other Charges, journal references, and related source values read by Demand Generation. The snapshot retains whatever reference is available without requiring a new schedule watermark or source-version integration.
- **Demand Snapshot**: The consistent, versioned publication containing the successful Billing Snapshot reference, completed Demand Rows, completed Demand Batches, aggregate totals, and stage/run status that becomes visible for Demand Output and Spooling only after Billing and Demand checks succeed.
- **Automizer**: The approved scheduling/orchestration component that triggers the overall Demand Run and supplies an automation identity and correlation data.
- **Billing**: The internal Period Billing capability that reads finalized Amortization Schedules, applies approved account-level allocations, produces Billing Period Results, and generates/posts billing journals for a selected period.
- **Accounting/GL Capability**: The existing authoritative capability that creates, posts, deduplicates, and reconciles billing journals. AccountsService/Demand Processing calls the capability through a narrow journal port and stores journal references and check results.
- **Billing Completion Marker**: The durable success record or contract response proving that the internal Billing Stage/Run completed successfully for the Demand scope.
- **PDF Export**: An authorized, scope-filtered representation of persisted Demand Generation Output or Demand Batch values containing run/period/branch metadata, totals, and a generated-at timestamp. PDF Export does not recalculate demand.
- **Journal Port**: The narrow AccountsService/Demand Processing interface used to invoke the Accounting/GL Capability and receive journal references and configured check results.
- **Line of Business (LOB)**: An existing business classification used by Billing and Accounts. Demand Processing references LOB records and does not manage them.
- **Branch**: An existing organizational unit used to group accounts and Demand Batches. Demand Processing references Branch records and does not manage them.
- **Account**: An existing financial account with a customer, LOB, Branch, status, and financial activity. Demand Processing reads eligible accounts and does not create accounts.
- **Customer**: The existing customer associated with an Account and displayed on Demand Generation Output.
- **Active Account**: An Account whose authoritative status is exactly Active at the Demand Run cut-off.
- **Excluded Account**: An Account whose authoritative status is Closed, Cancelled, or Pre-Closed at the Demand Run cut-off.
- **ODI**: The overdue interest component calculated by the default policy as overdue principal balance x configured annual ODI rate x overdue days / configured day-count basis, default `365`, rounded to INR 2 decimals, excluding the current-period Scheduled Installment and applying an optional configured cap. Billing/Accounting owns the source values; Demand Processing consumes the Billing result.
- **Other Charges**: The rounded net eligible posted non-ODI charge events effective for the account and period, less eligible waivers, reversals, and adjustments allocated to those charge events after suspense treatment. Other Charges exclude scheduled installment, principal/interest, arrears, and ODI; a negative net is clamped to zero after adjustment evidence is retained.
- **Total Charges**: The charge component calculated once as `ODI + Other Charges` using the configured policy and approved INR rounding.
- **Total Demand**: The amount calculated as `Current Due + Arrears + Total Charges`.
- **Frequency**: The billing periodicity selected for a Demand Run and matched to the completed Billing period.
- **Month/Year**: The business period identifier for a Demand Run, represented by a calendar month and year in the approved business timezone.
- **Processing Cut-off**: The cut-off or source reference retained for one internally consistent Demand Run snapshot. The detailed source contract is out of scope and deferred; this release does not require a new schedule watermark or source-version integration.
- **Logical Scope Key**: The normalized combination of LOB scope, Branch scope, Frequency, Month/Year, and applicable Billing reference used for idempotency and concurrency control.
- **Run Revision**: A version of a Demand Run's calculated result when an authorized rerun creates a new retained result for the same logical scope.
- **Spool Attempt**: One attempt to deliver or mark a Demand Batch as available to the downstream branch process.
- **Eligible Branch**: A Branch included by the Demand Run scope and authorized for the initiating user or Automizer identity.
- **System Administrator**: An authenticated user holding the platform's SystemAdmin policy or role.
- **Operations User**: An authenticated user authorized to run, inspect, recover, or spool Demand Processing according to the approved policy.
- **BillingIncomplete**: The machine-readable outcome used when the internal Billing Stage/Run is not successful for the exact Demand Run scope and period; it prevents Demand Generation and Demand Spooling.
- **Normalized Input**: A syntactically valid, canonical LOB, Branch, Frequency, or Month/Year value accepted by the Demand Processing Service.
- **EARS**: Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.

## Requirements

### Requirement 1: Define and validate a Demand Run

**User Story:** As an Operations User or Automizer, I want to submit a Demand Run for a defined scope and period, so that the system can generate the dues and arrears MIS for that scope.

#### Acceptance Criteria

1. WHEN a caller submits a Demand Run, THE DemandProcessing_Service SHALL authenticate the caller before authorization and validation.
2. IF the JWT is missing, malformed, expired, or invalid, THEN THE DemandProcessing_Service SHALL return HTTP 401.
3. IF authentication fails, THEN THE DemandProcessing_Service SHALL disclose no demand data.
4. IF authentication fails, THEN THE DemandProcessing_Service SHALL create no processable Run.
5. IF authentication succeeds and the caller lacks permission for the requested LOB or Branch scope, THEN THE DemandProcessing_Service SHALL return HTTP 403.
6. IF authorization fails, THEN THE DemandProcessing_Service SHALL disclose no demand data.
7. IF authorization fails, THEN THE DemandProcessing_Service SHALL create no processable Run.
8. IF LOB, Frequency, or Month/Year is missing, THEN THE DemandProcessing_Service SHALL reject the request with a field-specific validation code.
9. IF LOB, Frequency, or Month/Year is missing, THEN THE DemandProcessing_Service SHALL create no processable Run.
10. IF LOB, Branch, Frequency, or Month/Year cannot be represented as a Normalized Input, THEN THE DemandProcessing_Service SHALL reject the request with a field-specific validation code.
11. IF a required input cannot be normalized, THEN THE DemandProcessing_Service SHALL create no processable Run.
12. IF Frequency is not an exact value in the configured appSettings Frequency set, THEN THE DemandProcessing_Service SHALL reject the request with a Frequency validation code before reading financial source amounts.
13. WHEN a valid request has no configured Frequency set, THE DemandProcessing_Service SHALL accept `Monthly` as the only supported Frequency.
14. WHEN authentication, authorization, and input validation succeed, THE DemandProcessing_Service SHALL persist one accepted Run.
15. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL assign one immutable system-generated GUID Run identifier.
16. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL retain normalized LOB, Branch scope, Frequency, and Month/Year as the Logical Scope Key.
17. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL retain the source identity.
18. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL retain the correlation identifier.
19. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL retain the UTC request timestamp.
20. WHEN an accepted Run is persisted, THE DemandProcessing_Service SHALL retain the available Processing Cut-off or source reference.
21. WHEN Branch is omitted, THE DemandProcessing_Service SHALL resolve all authorized Active Branches for the selected LOB.
22. WHEN Branch is omitted, THE DemandProcessing_Service SHALL retain every resolved Active Branch in the Run scope.
23. IF the authorized Active Branch set for the selected LOB is empty, THEN THE DemandProcessing_Service SHALL reject the request with a no-active-branch validation code.
24. IF the authorized Active Branch set is empty, THEN THE DemandProcessing_Service SHALL create no processable Run.
25. WHEN a request names a deactivated Branch for new Demand generation, THE DemandProcessing_Service SHALL reject the request with a deactivated-branch validation code.
26. IF a request names a deactivated Branch for new Demand generation, THEN THE DemandProcessing_Service SHALL create no processable Run.
27. WHEN Month/Year is evaluated, THE DemandProcessing_Service SHALL derive the business period in the `Asia/Kolkata` timezone.
28. WHERE the accepted scope contains multiple authorized Branches, THE DemandProcessing_Service SHALL use each retained Branch as an independent batching dimension.

### Requirement 2: Enforce successful internal Billing stage as a precondition

**User Story:** As a Financial Operations stakeholder, I want Demand Generation to use only a successfully completed internal Billing stage, so that the MIS does not report partial or unposted dues.

#### Acceptance Criteria

1. WHEN a Demand Run is accepted, THE DemandProcessing_Service SHALL create or resolve the internal Billing Stage/Run for the exact normalized LOB, Branch scope, Frequency, and Month/Year.
2. IF the internal Billing Stage/Run is not successful for the exact normalized scope and period, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`.
3. IF the internal Billing Stage/Run is not successful, THEN THE DemandProcessing_Service SHALL retain the Demand Run in a non-processable status.
4. IF the internal Billing Stage/Run is not successful, THEN THE DemandProcessing_Service SHALL create no Demand Row.
5. IF the internal Billing Stage/Run is not successful, THEN THE DemandProcessing_Service SHALL publish no processable Demand Snapshot.
6. IF the Billing Stage/Run is failed, cancelled, partial, in progress, or missing a required Billing dependency, THEN THE DemandProcessing_Service SHALL retain the Billing status.
7. IF a required Billing dependency is missing, THEN THE DemandProcessing_Service SHALL retain dependency diagnostics.
8. IF the Billing Stage/Run is not successful, THEN THE DemandProcessing_Service SHALL keep Demand Generation and Demand Spooling blocked.
9. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify the internally consistent run snapshot.
10. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify account eligibility.
11. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify exactly-once allocation outcomes.
12. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify ODI and Other Charges.
13. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify the configured Accounting/GL journal checks.
14. IF any required Billing check is unsuccessful or missing, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`.
15. IF any required Billing check is unsuccessful or missing, THEN THE DemandProcessing_Service SHALL retain each failed or missing check.
16. IF any required Billing check is unsuccessful or missing, THEN THE DemandProcessing_Service SHALL create no Demand Row.
17. IF any required Billing check is unsuccessful or missing, THEN THE DemandProcessing_Service SHALL publish no Demand Snapshot.
18. WHEN all required Billing checks succeed, THE DemandProcessing_Service SHALL retain the Billing Run reference.
19. WHEN all required Billing checks succeed, THE DemandProcessing_Service SHALL retain the Billing Snapshot version.
20. WHEN all required Billing checks succeed, THE DemandProcessing_Service SHALL use one internally consistent Billing Snapshot for the complete Demand Generation stage.
21. IF the Billing Stage/Run scope differs from the Demand Run Logical Scope Key, THEN THE DemandProcessing_Service SHALL return `BillingScopeMismatch`.
22. IF the Billing Stage/Run scope differs from the Demand Run Logical Scope Key, THEN THE DemandProcessing_Service SHALL publish no Demand Snapshot.
24. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify three required Billing check groups: internally consistent snapshot, complete account-level Billing results including eligibility, allocation, ODI, and Other Charges, and configured Accounting/GL journal checks.

**User Story:** As a Financial Operations stakeholder, I want the Demand Run to bill each period from the finalized Amortization Schedule, so that demand uses the approved scheduled obligation and account-level financial allocations without recomputing EMI.

#### Acceptance Criteria

1. WHEN a Billing Stage/Run is requested, THE DemandProcessing_Service SHALL validate normalized LOB, Branch scope, supported Frequency, and Month/Year before reading account financial values.
2. WHEN the recorded Billing Processing Cut-off is reached, THE DemandProcessing_Service SHALL evaluate account eligibility within one internally consistent run snapshot.
3. WHEN the Billing Processing Cut-off is evaluated, THE DemandProcessing_Service SHALL retain the available cut-off or source reference.
4. WHEN an eligible Account is billed, THE DemandProcessing_Service SHALL read the available finalized Amortization Schedule reference for the selected period.
5. WHEN an eligible Account is billed, THE DemandProcessing_Service SHALL retain the available schedule identity or source reference.
6. WHEN the selected period exists in the finalized Amortization Schedule, THE DemandProcessing_Service SHALL set Scheduled Installment to scheduled principal plus scheduled interest/EMI.
7. WHEN the selected period exists in the finalized Amortization Schedule, THE DemandProcessing_Service SHALL use the retained schedule value as the period obligation before allocation.
8. THE DemandProcessing_Service SHALL not recompute EMI.
9. THE DemandProcessing_Service SHALL not derive a replacement installment from account inputs.
10. THE DemandProcessing_Service SHALL not mutate an Amortization Schedule.
11. IF a required schedule identity or source reference is unavailable, THEN THE DemandProcessing_Service SHALL create no Billing Period Result for the affected Account.
12. IF the schedule is absent, unfinalized under the source contract, or missing the selected period, THEN THE DemandProcessing_Service SHALL retain a schedule diagnostic for the affected Account.
13. IF the schedule is absent, unfinalized under the source contract, or missing the selected period, THEN THE DemandProcessing_Service SHALL prevent the affected result from entering a successful Billing Snapshot.
14. THE DemandProcessing_Service SHALL not require a new schedule watermark or source-version integration in this release.
15. WHEN approved receipts, part-payments, waivers, reversals, adjustments, and suspense events are available, THE DemandProcessing_Service SHALL consider each eligible event under the approved allocation policy.
16. WHEN an eligible allocation event is applied, THE DemandProcessing_Service SHALL apply the event exactly once.
17. WHEN an eligible allocation event is applied, THE DemandProcessing_Service SHALL retain one event reference.
18. WHEN an eligible allocation event is applied, THE DemandProcessing_Service SHALL retain one allocation outcome.
19. WHEN allocation completes, THE DemandProcessing_Service SHALL calculate Current Due as the unpaid selected-period Scheduled Installment after approved allocation.
20. WHEN allocation completes, THE DemandProcessing_Service SHALL calculate Arrears from previous unpaid Billing Period Results.
21. WHEN allocation completes, THE DemandProcessing_Service SHALL keep the selected-period unpaid amount separate from prior-period Arrears.
22. WHEN the Billing contract supplies ODI and Other Charges, THE DemandProcessing_Service SHALL retain ODI as a separate Billing Period Result component.
23. WHEN the Billing contract supplies ODI and Other Charges, THE DemandProcessing_Service SHALL retain Other Charges as a separate Billing Period Result component.
24. WHEN the default ODI policy is used, THE DemandProcessing_Service SHALL calculate ODI as overdue principal balance multiplied by configured annual ODI rate, overdue days, and the reciprocal of the configured day-count basis.
25. WHEN the default ODI policy is used without a configured day-count basis, THE DemandProcessing_Service SHALL use `365` as the day-count basis.
26. WHEN the default ODI policy is used, THE DemandProcessing_Service SHALL exclude the current-period Scheduled Installment from overdue principal.
27. WHEN the default ODI policy has a configured cap, THE DemandProcessing_Service SHALL apply that cap to ODI.
28. WHEN the default ODI policy has no configured cap, THE DemandProcessing_Service SHALL apply no ODI cap.
29. WHEN the default ODI policy is used, THE DemandProcessing_Service SHALL round ODI to INR two decimals using `MidpointRounding.AwayFromZero`.
30. WHEN the default Other Charges policy is used, THE DemandProcessing_Service SHALL calculate Other Charges from eligible posted non-ODI charge events effective for the account and period.
31. WHEN the default Other Charges policy is used, THE DemandProcessing_Service SHALL subtract eligible waivers, reversals, and adjustments allocated to those charge events after suspense treatment.
32. WHEN the default Other Charges policy produces a negative net value, THE DemandProcessing_Service SHALL retain adjustment evidence before clamping the value to zero.
33. WHEN the default Other Charges policy is used, THE DemandProcessing_Service SHALL round Other Charges to INR two decimals using `MidpointRounding.AwayFromZero`.
34. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain Scheduled Installment.
35. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain scheduled principal and scheduled interest/EMI.
36. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain every applied allocation reference and amount.
37. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain Current Due and Arrears.
38. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain ODI and Other Charges.
39. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain the available schedule/source reference and Processing Cut-off reference.
40. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain the applicable policy references.
41. WHEN all account Billing results and allocation checks succeed, THE DemandProcessing_Service SHALL invoke the Accounting/GL Capability through the Journal Port.
42. THE Accounting/GL Capability SHALL remain the authoritative owner of billing journal creation, posting, idempotency, and reconciliation.
43. WHEN the Journal Port returns journal results, THE DemandProcessing_Service SHALL retain journal references and configured check results only.
44. IF a required Accounting/GL journal check fails, THEN THE DemandProcessing_Service SHALL prevent Billing Snapshot publication.
45. WHEN the same logical Billing scope and Month/Year is retried with the same available source references, THE DemandProcessing_Service SHALL resolve the existing Billing Stage/Run.
46. WHEN the same logical Billing scope and Month/Year is retried with the same available source references, THE DemandProcessing_Service SHALL create no duplicate allocation.
47. WHEN the same logical Billing scope and Month/Year is retried with the same available source references, THE DemandProcessing_Service SHALL create no duplicate Billing Period Result.
48. WHEN the same logical Billing scope and Month/Year is retried with the same available source references, THE DemandProcessing_Service SHALL create no duplicate billing journal.
49. IF an account Billing, allocation, schedule, charge, or journal check fails or remains incomplete, THEN THE DemandProcessing_Service SHALL retain failure diagnostics.
50. IF an account Billing, allocation, schedule, charge, or journal check fails or remains incomplete, THEN THE DemandProcessing_Service SHALL publish no successful Billing Snapshot containing the affected result.
51. IF an account Billing, allocation, schedule, charge, or journal check fails or remains incomplete, THEN THE DemandProcessing_Service SHALL keep Demand Generation and Demand Spooling blocked.
52. WHEN every required eligible Account has a successful Billing Period Result and all configured Accounting/GL journal checks succeed, THE DemandProcessing_Service SHALL publish one consistent Billing Snapshot for the exact requested scope and period.
53. WHEN a Billing Stage/Run starts, progresses, completes, fails, or is retried, THE DemandProcessing_Service SHALL retain its status and diagnostics.
54. WHEN a Billing Stage/Run starts, progresses, completes, fails, or is retried, THE DemandProcessing_Service SHALL retain available schedule/source references, allocation evidence, journal references, and configured check results.
55. WHEN a Billing Stage/Run starts, progresses, completes, fails, or is retried, THE DemandProcessing_Service SHALL retain actor or Automizer source, correlation identifier, snapshot/cut-off reference, and minimum audit history.
56. THE DemandProcessing_Service SHALL read supported Frequency values, Billing completion/snapshot settings, charge policy values, and journal policy/adapter settings from appSettings.
57. THE DemandProcessing_Service SHALL use `Monthly`, `365`, no ODI cap, and the CD12 charge formulas as defaults when the corresponding settings are absent.

### Requirement 4: Select eligible accounts and exclude ineligible accounts

**User Story:** As an Operations User, I want the MIS to include only eligible Active accounts, so that closed or cancelled exposures do not enter the branch demand batches.

#### Acceptance Criteria

1. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require the Account to belong to the normalized LOB.
2. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require the Account to belong to an Active Branch in the Run scope.
3. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require one resolvable Customer reference.
4. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require one resolvable Branch reference.
5. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require one resolvable LOB reference.
6. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL require authoritative status exactly `Active` at the recorded Processing Cut-off.
7. IF an Account has status `Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output.
8. IF an Account has status `Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from every Demand Batch.
9. IF an Account has status `Cancelled` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output.
10. IF an Account has status `Cancelled` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from every Demand Batch.
11. IF an Account has status `Pre-Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output.
12. IF an Account has status `Pre-Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from every Demand Batch.
13. IF an Account has a missing, conflicting, or unsupported status, THEN THE DemandProcessing_Service SHALL retain a data-quality diagnostic for the Account.
14. IF an Account has a missing, conflicting, or unsupported status, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output.
15. IF an Account has a missing, conflicting, or unsupported status, THEN THE DemandProcessing_Service SHALL exclude the Account from every Demand Batch.
16. IF an Account has a missing, inactive, or conflicting Customer, Branch, or LOB reference, THEN THE DemandProcessing_Service SHALL retain a data-quality diagnostic naming the reference type and Account identifier.
17. IF an Account has a missing, inactive, or conflicting Customer, Branch, or LOB reference, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output.
18. IF an Account has a missing, inactive, or conflicting Customer, Branch, or LOB reference, THEN THE DemandProcessing_Service SHALL exclude the Account from every Demand Batch.
19. WHEN eligibility is evaluated, THE DemandProcessing_Service SHALL retain the Processing Cut-off or available source reference used for the decision.
20. WHEN eligibility is evaluated, THE DemandProcessing_Service SHALL retain the Billing Snapshot version used for the decision.
21. WHEN eligibility is evaluated, THE DemandProcessing_Service SHALL retain the status and reference values used for the decision.

### Requirement 5: Calculate account-level demand

**User Story:** As a Financial Operations stakeholder, I want each eligible account's demand calculated from authoritative due and charge components, so that the MIS supports collection and branch operations.

#### Acceptance Criteria

1. WHEN an eligible Account has a successful Billing Period Result in the retained Billing Snapshot, THE DemandProcessing_Service SHALL create one Demand Row for the Account.
2. WHEN an eligible Account has zero Total Demand, THE DemandProcessing_Service SHALL create the Demand Row.
3. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL copy Current Due from the Billing Period Result.
4. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL copy Arrears from the Billing Period Result.
5. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL copy ODI from the Billing Period Result.
6. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL copy Other Charges from the Billing Period Result.
7. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL retain Account, Customer, LOB, Branch, Month/Year, Frequency, and Billing Period Result reference.
8. WHEN a Demand Row is calculated, THE DemandProcessing_Service SHALL calculate Total Charges as ODI plus Other Charges once.
9. WHEN a Demand Row is calculated, THE DemandProcessing_Service SHALL calculate Total Demand as Current Due plus Arrears plus Total Charges.
10. WHEN a Demand Row is calculated, THE DemandProcessing_Service SHALL use INR `decimal(19,2)` and `MidpointRounding.AwayFromZero`.
11. THE DemandProcessing_Service SHALL not independently calculate EMI.
12. THE DemandProcessing_Service SHALL not derive a replacement Scheduled Installment.
13. THE DemandProcessing_Service SHALL not mutate the Amortization Schedule.
14. WHEN a Demand Row is created, THE DemandProcessing_Service SHALL preserve the Billing Period Result's available schedule/source reference.
15. IF a required source component is missing, non-finite, outside the permitted sign range, or not representable at approved INR precision, THEN THE DemandProcessing_Service SHALL reject the affected row.
16. IF a required source component is invalid, THEN THE DemandProcessing_Service SHALL retain the Account, component, value-state, and diagnostic code.
17. IF a required source component is invalid, THEN THE DemandProcessing_Service SHALL exclude the row from every completed Demand Batch.
18. WHEN Total Demand equals `0.00 INR`, THE DemandProcessing_Service SHALL persist an explicit zero-demand indicator.
19. WHEN Total Demand equals `0.00 INR`, THE DemandProcessing_Service SHALL include the row in row counts, totals, and spooling eligibility.
20. WHEN the same normalized scope, Processing Cut-off, and Billing Snapshot version are processed more than once, THE DemandProcessing_Service SHALL produce identical persisted component values.
21. WHEN a Run Revision contains multiple Demand Rows, THE DemandProcessing_Service SHALL order rows by Account identifier ascending.

### Requirement 6: Create branch-wise Demand Batches

**User Story:** As a Branch Operations User, I want generated demand grouped by Branch, so that each branch receives only its own accounts and totals.

#### Acceptance Criteria

1. WHEN Demand Processing has valid completed Demand Rows, THE DemandProcessing_Service SHALL create one logical Demand Batch for each represented Branch.
2. WHEN an in-scope Active Branch has no eligible Account, THE DemandProcessing_Service SHALL record one empty/no-demand batch outcome for the Branch.
3. WHEN an empty/no-demand batch outcome is recorded, THE DemandProcessing_Service SHALL classify the outcome as non-failure.
4. WHEN an empty/no-demand batch outcome is recorded, THE DemandProcessing_Service SHALL keep the Run out of failure solely because of the empty outcome.
5. WHEN an empty/no-demand batch exists, THE DemandProcessing_Service SHALL expose the outcome in Demand Output.
6. WHEN an empty/no-demand batch exists, THE DemandProcessing_Service SHALL expose the outcome in Demand Spooling.
7. WHEN an empty/no-demand batch exists, THE DemandProcessing_Service SHALL mark the batch not spoolable.
8. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL include only rows matching the batch Branch.
9. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL include only rows matching the batch LOB, Frequency, Month/Year, Run identifier, and Run Revision.
10. WHEN more than one Branch is represented, THE DemandProcessing_Service SHALL place each Demand Row in exactly one batch.
11. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL persist row count.
12. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL persist Current Due, Arrears, ODI, Other Charges, Total Charges, and Total Demand totals.
13. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL round each total to approved INR precision.
14. WHEN Demand Batches are generated for a Run Revision, THE DemandProcessing_Service SHALL derive each batch identity from the Run Revision and Branch identity.
15. WHEN multiple Demand Batches exist, THE DemandProcessing_Service SHALL order batches by Branch identifier ascending.
16. IF a persisted batch total differs from the sum of persisted rows at approved INR precision, THEN THE DemandProcessing_Service SHALL mark the batch `Failed`.
17. IF a persisted batch total differs from the sum of persisted rows at approved INR precision, THEN THE DemandProcessing_Service SHALL retain discrepancy diagnostics.
18. IF a persisted batch total differs from the sum of persisted rows at approved INR precision, THEN THE DemandProcessing_Service SHALL block the batch from `ReadyToSpool`.
19. IF a row dimension differs from its batch dimension, THEN THE DemandProcessing_Service SHALL mark the batch `Failed`.
20. IF a row dimension differs from its batch dimension, THEN THE DemandProcessing_Service SHALL retain the dimension mismatch.
21. IF a row dimension differs from its batch dimension, THEN THE DemandProcessing_Service SHALL block the batch from spooling.

### Requirement 7: Trigger automatic processing and support controlled manual initiation

**User Story:** As a platform operator, I want Automizer to start the ordered Demand Run and continue to Demand Generation after the internal Billing stage succeeds, so that the monthly or configured workflow does not depend on a user remembering to start the MIS.

#### Acceptance Criteria

1. WHEN Automizer emits the approved Demand Run trigger, THE Automizer SHALL provide LOB, Branch scope, Frequency, and Month/Year.
2. WHEN Automizer emits the approved Demand Run trigger, THE Automizer SHALL provide Automizer identity and correlation identifier.
3. WHEN DemandProcessing_Service receives a valid Automizer trigger, THE DemandProcessing_Service SHALL create one asynchronous Demand Run for the Logical Scope Key.
4. WHEN an asynchronous Demand Run is created, THE DemandProcessing_Service SHALL return the Run identifier and initial status.
5. WHEN DemandProcessing_Service receives an equivalent Automizer trigger for an active Logical Scope Key, THE DemandProcessing_Service SHALL return the existing Run identifier and status.
6. WHEN DemandProcessing_Service receives an equivalent Automizer trigger for a completed Logical Scope Key, THE DemandProcessing_Service SHALL return the existing Run identifier and status.
7. WHEN an equivalent Automizer trigger is received, THE DemandProcessing_Service SHALL create no second logical Run.
8. IF Automizer requests Demand Generation while Billing is incomplete, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`.
9. IF Automizer requests Demand Generation while Billing is incomplete, THEN THE DemandProcessing_Service SHALL keep Demand Rows and the Demand Snapshot unpublished.
10. IF Automizer requests Demand Generation while Billing is incomplete, THEN THE DemandProcessing_Service SHALL provide a machine-readable retry/defer reason.
11. WHEN a user with the configured manual-run permission submits a Manual Demand Run, THE DemandProcessing_Service SHALL use the ordered Billing-then-Demand workflow.
12. WHEN a user with the configured manual-run permission submits a Manual Demand Run, THE DemandProcessing_Service SHALL apply the same scope, authorization, Billing, eligibility, calculation, batching, audit, and idempotency rules as an Automizer trigger.
13. IF a user without the configured manual-run permission submits a Manual Demand Run, THEN THE DemandProcessing_Service SHALL return HTTP 403.
14. IF a user without the configured manual-run permission submits a Manual Demand Run, THEN THE DemandProcessing_Service SHALL create no processable Run.
15. WHEN an equivalent Automizer trigger is retried after a timeout or lost response, THE DemandProcessing_Service SHALL resolve the request by Logical Scope Key and idempotency key.
16. WHEN an equivalent Automizer trigger is retried, THE DemandProcessing_Service SHALL create no duplicate Demand Row.
17. WHEN an equivalent Automizer trigger is retried, THE DemandProcessing_Service SHALL create no duplicate Demand Batch.
18. WHEN a Demand Batch reaches completed spool-eligible status, THE DemandProcessing_Service SHALL mark the persisted batch `ReadyToSpool`.
19. WHEN a Demand Batch reaches `ReadyToSpool`, THE DemandProcessing_Service SHALL not recalculate or replace any Demand Row.
20. THE DemandProcessing_Service SHALL not define spool transport, downstream acknowledgement, retry limits, or downstream idempotency in this release.

### Requirement 8: Maintain period history and idempotent reruns

**User Story:** As a Financial Controller, I want Demand history retained by Month/Year and repeated triggers handled safely, so that prior periods remain auditable and retries do not duplicate demand.

#### Acceptance Criteria

1. WHEN a Demand Run completes, THE DemandProcessing_Service SHALL retain Run identifier, LOB, Branch scope, Frequency, and Month/Year.
2. WHEN a Demand Run completes, THE DemandProcessing_Service SHALL retain Billing reference, Processing Cut-off, Billing Snapshot version, and Run Revision.
3. WHEN a Demand Run completes, THE DemandProcessing_Service SHALL retain Demand Rows, Demand Batches, totals, status, source, actor, and UTC lifecycle timestamps.
4. WHEN an authorized caller requests history for a Month/Year, THE DemandProcessing_Service SHALL return only Runs within the caller's permitted scope.
5. WHEN an authorized caller requests history for a Month/Year, THE DemandProcessing_Service SHALL return only Run Revisions within the caller's permitted scope.
6. WHEN history is returned, THE DemandProcessing_Service SHALL order records by Month/Year, completion timestamp, Run Revision, and Run identifier.
7. WHEN a request has the same Logical Scope Key and Billing reference as a completed Run, THE DemandProcessing_Service SHALL return the existing completed result.
8. WHEN a request has the same Logical Scope Key and Billing reference as a completed Run, THE DemandProcessing_Service SHALL create no duplicate Demand Row.
9. WHEN a request has the same Logical Scope Key and Billing reference as a completed Run, THE DemandProcessing_Service SHALL create no duplicate Demand Batch.
10. IF an idempotency key is reused with a different normalized scope, Month/Year, Billing reference, or immutable request payload, THEN THE DemandProcessing_Service SHALL reject the request as `IdempotencyConflict`.
11. IF an idempotency conflict occurs, THEN THE DemandProcessing_Service SHALL preserve the original Run.
12. IF an idempotency conflict occurs, THEN THE DemandProcessing_Service SHALL retain the conflict record.
13. WHEN an authorized rerun is requested for a failed or stale Run, THE DemandProcessing_Service SHALL retain the original Run.
14. WHEN an authorized rerun is requested for a failed or stale Run, THE DemandProcessing_Service SHALL create one separately identified Run Revision linked to the superseded Run.
15. IF an authorized rerun is requested after a Demand Batch is marked spooled, THEN THE DemandProcessing_Service SHALL not replace delivered output.
16. IF an authorized rerun is requested after a Demand Batch is marked spooled, THEN THE DemandProcessing_Service SHALL create a linked Run Revision.
17. IF an authorized rerun is requested after a Demand Batch is marked spooled, THEN THE DemandProcessing_Service SHALL retain links between the delivered result and the rerun.
18. WHEN an unchanged successful result is requested without an explicit rerun instruction, THE DemandProcessing_Service SHALL return the existing result.
19. WHEN an unchanged successful result is requested without an explicit rerun instruction, THE DemandProcessing_Service SHALL start no new calculation.
20. WHERE a retention or archival period is configured, WHEN the configured period is reached, THE DemandProcessing_Service SHALL apply the configured retention action.
21. WHERE a retention or archival period is configured, THE DemandProcessing_Service SHALL preserve required financial and operational history until the configured expiry.
22. THE DemandProcessing_Service SHALL leave retention duration, archival mechanism, and purge authority configurable and deferred.
23. WHEN an authorized caller requests Demand data for a deactivated Branch, THE DemandProcessing_Service SHALL return retained Runs within the caller's permitted scope.
24. WHEN an authorized caller requests Demand data for a deactivated Branch, THE DemandProcessing_Service SHALL return retained Demand Rows within the caller's permitted scope.
25. WHEN an authorized caller requests Demand data for a deactivated Branch, THE DemandProcessing_Service SHALL return retained Demand Batches within the caller's permitted scope.
26. WHEN a Branch is deactivated after a Run completes, THE DemandProcessing_Service SHALL keep the Branch's retained Demand history unchanged.
27. WHEN a Branch is deactivated, THE DemandProcessing_Service SHALL exclude the Branch from later Demand generation.

### Requirement 9: Enforce concurrency and atomic result publication

**User Story:** As a platform operator, I want concurrent triggers and failures handled without duplicate or partially published demand, so that Demand history remains trustworthy.

#### Acceptance Criteria

1. WHEN equivalent Demand Run requests arrive concurrently, THE DemandProcessing_Service SHALL designate one authoritative Run for the Logical Scope Key.
2. WHEN equivalent Demand Run requests arrive concurrently, THE DemandProcessing_Service SHALL return the authoritative Run identifier to every equivalent request.
3. THE DemandProcessing_Service SHALL preserve one authoritative active Run Revision for each Logical Scope Key across process restarts.
4. WHEN a duplicate creation attempt occurs, THE DemandProcessing_Service SHALL resolve the attempt to the existing authoritative Run or Run Revision.
5. WHEN a duplicate creation attempt occurs, THE DemandProcessing_Service SHALL create no second authoritative result.
6. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL expose the complete Billing Period Results.
7. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL expose the successful Billing Snapshot.
8. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL expose complete Demand Rows and complete Demand Batches.
9. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL expose aggregate totals and Demand Snapshot reference.
10. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL require successful internal Billing and required journal checks.
11. IF a calculation loses its processing lease, terminates, or fails before publication, THEN THE DemandProcessing_Service SHALL keep the Run out of `Completed`.
12. IF a calculation loses its processing lease, terminates, or fails before publication, THEN THE DemandProcessing_Service SHALL hide incomplete rows and batches from completed output.
13. IF a calculation loses its processing lease, terminates, or fails before publication, THEN THE DemandProcessing_Service SHALL retain the interruption diagnostic.
14. IF a calculation loses its processing lease, terminates, or fails before publication, THEN THE DemandProcessing_Service SHALL mark the Run eligible for the approved recovery or rerun path.
15. IF one Branch Batch fails while another Branch Batch succeeds, THEN THE DemandProcessing_Service SHALL isolate the failed batch.
16. IF one Branch Batch fails while another Branch Batch succeeds, THEN THE DemandProcessing_Service SHALL preserve successful batches unchanged.
17. IF one Branch Batch fails while another Branch Batch succeeds, THEN THE DemandProcessing_Service SHALL keep the failed batch unavailable for spooling.
18. WHEN at least one Branch Batch fails and at least one Branch Batch succeeds, THE DemandProcessing_Service SHALL mark the overall Run `PartiallyFailed`.
19. WHEN at least one Branch Batch fails and at least one Branch Batch succeeds, THE DemandProcessing_Service SHALL allow spooling status only for successful reconciled batches.
20. WHEN a Branch Batch is isolated as `Failed`, THE DemandProcessing_Service SHALL retain batch diagnostics and failure class.
21. WHEN a Branch Batch is isolated as `Failed`, THE DemandProcessing_Service SHALL allow an authorized user to inspect the batch.
22. WHEN a Branch Batch is isolated as `Failed`, THE DemandProcessing_Service SHALL allow an authorized user to retry the batch separately.
23. IF the Billing stage fails or Billing Snapshot publication is blocked, THEN THE DemandProcessing_Service SHALL block Demand Generation for every Branch in the Run.

### Requirement 10: Handle failures and expose reconciliation information

**User Story:** As an Operations User, I want failures to be visible and recoverable, so that incomplete source data or downstream outages do not appear as valid demand.

#### Acceptance Criteria

1. IF a required Billing, Account, Customer, Branch, LOB, or charge dependency is unavailable, THEN THE DemandProcessing_Service SHALL classify the affected outcome as `Dependency`.
2. IF a dependency is unavailable, THEN THE DemandProcessing_Service SHALL retain the source and scope.
3. IF a dependency is unavailable, THEN THE DemandProcessing_Service SHALL prevent affected output from becoming ready to spool.
4. IF a dependency operation exceeds the configured timeout, THEN THE DemandProcessing_Service SHALL classify the outcome as `Transient`.
5. IF a dependency operation returns a temporary failure, THEN THE DemandProcessing_Service SHALL classify the outcome as `Transient`.
6. IF a transient failure occurs, THEN THE DemandProcessing_Service SHALL retain Run state, correlation identifier, source reference, and diagnostics.
7. IF a transient failure occurs, THEN THE DemandProcessing_Service SHALL permit only the configured retry or recovery path.
8. IF input validation or amount validation prevents processing, THEN THE DemandProcessing_Service SHALL classify the outcome as `Permanent`.
9. IF input validation or amount validation prevents processing, THEN THE DemandProcessing_Service SHALL retain the affected scope and component.
10. IF input validation or amount validation prevents processing, THEN THE DemandProcessing_Service SHALL exclude invalid output.
11. IF an Account, Customer, Branch, LOB, or status reference cannot be resolved, THEN THE DemandProcessing_Service SHALL classify the outcome as `DataQuality`.
12. IF a reference cannot be resolved, THEN THE DemandProcessing_Service SHALL retain the affected scope and reference type.
13. IF a reference cannot be resolved, THEN THE DemandProcessing_Service SHALL exclude invalid output.
14. IF persisted totals do not reconcile, THEN THE DemandProcessing_Service SHALL classify the outcome as `Reconciliation`.
15. IF a Demand Row violates a component or dimension invariant, THEN THE DemandProcessing_Service SHALL classify the outcome as `Reconciliation`.
16. IF a reconciliation outcome occurs, THEN THE DemandProcessing_Service SHALL block `ReadyToSpool`.
17. IF a reconciliation outcome occurs, THEN THE DemandProcessing_Service SHALL expose the discrepancy in Run and Batch diagnostics.
18. WHEN a Run is failed, deferred, partially failed, or conflicted, THE DemandProcessing_Service SHALL expose one machine-readable status.
19. WHEN a Run is failed, deferred, partially failed, or conflicted, THE DemandProcessing_Service SHALL expose one failure-class value from the approved vocabulary.
20. THE approved failure-class vocabulary SHALL include Billing-incomplete, Dependency, Transient, Permanent, DataQuality, Reconciliation, Authorization, Idempotency, Calculation, and Spooling outcomes.
21. THE DemandProcessing_Service SHALL redact credentials, bearer tokens, connection strings, and customer fields not required by the diagnostic contract from diagnostics, logs, audit entries, and UI notifications.
22. WHEN a controlled retry or re-spool is authorized, THE DemandProcessing_Service SHALL retain the original source snapshot.
23. WHEN a controlled retry or re-spool is authorized, THE DemandProcessing_Service SHALL retain the original output and failure history.
24. WHEN a controlled retry or re-spool is authorized, THE DemandProcessing_Service SHALL create no duplicate logical row.
25. WHEN a controlled retry or re-spool is authorized, THE DemandProcessing_Service SHALL not overwrite prior history.
26. THE DemandProcessing_Service SHALL leave spool retry limits, delivery acknowledgement, and attempt-history details deferred under CD9.
27. THE DemandProcessing_Service SHALL use the existing Accounting/GL Capability as the journal owner.

### Requirement 11: Provide Demand Run screen

**User Story:** As an Operations User, I want a Demand Run screen with the required inputs and clear processing status, so that I can start or inspect a period demand run.

#### Acceptance Criteria

1. WHEN an unauthorized user navigates to the Demand Run route, THE DemandProcessing_UI SHALL render an access-denied state.
2. WHEN an unauthorized user navigates to the Demand Run route, THE DemandProcessing_UI SHALL render no Demand Run controls.
3. WHEN an unauthorized user navigates to the Demand Run route, THE DemandProcessing_UI SHALL render no demand data.
4. WHEN an authorized user opens the Demand Run screen, THE DemandProcessing_UI SHALL display LOB, Branch, Frequency, and Month/Year controls.
5. WHEN the Demand Run controls are displayed, THE DemandProcessing_UI SHALL limit values to the user's permitted scope.
6. WHEN the user submits valid values, THE DemandProcessing_UI SHALL send normalized values through the shared axios service.
7. WHEN the user submits valid values, THE shared axios service SHALL propagate the JWT Bearer token.
8. WHEN the service accepts a run, THE DemandProcessing_UI SHALL display the returned Run identifier and status.
9. IF a required field is missing or malformed, THEN THE DemandProcessing_UI SHALL display one field-level English error for the affected field.
10. IF a required field is missing or malformed, THEN THE DemandProcessing_UI SHALL block submission.
11. IF a required field is missing or malformed, THEN THE DemandProcessing_UI SHALL preserve entered values.
12. IF the service reports `BillingIncomplete`, THEN THE DemandProcessing_UI SHALL display the distinct `BillingIncomplete` state.
13. IF the service reports `BillingIncomplete`, THEN THE DemandProcessing_UI SHALL expose only the approved refresh or retry action.
14. WHILE a Run is processing, THE DemandProcessing_UI SHALL display Run identifier, period, scope, and Billing reference when available.
15. WHILE a Run is processing, THE DemandProcessing_UI SHALL display one refresh or polling control using the configured polling interval.
16. IF the service reports rejection, failure, conflict, timeout, or authorization failure, THEN THE DemandProcessing_UI SHALL display the mapped English state.
17. IF the service reports rejection, failure, conflict, timeout, or authorization failure, THEN THE DemandProcessing_UI SHALL preserve submitted values.
18. WHERE manual initiation or rerun is permitted by the configured role or policy, THE DemandProcessing_UI SHALL enable the action only after a permission check.
19. WHERE manual initiation or rerun is permitted by the configured role or policy, THE DemandProcessing_UI SHALL require a confirmation step.
20. WHERE manual initiation or rerun is not permitted by the configured role or policy, THE DemandProcessing_UI SHALL hide or disable the action.
21. WHEN the Frequency control is rendered, THE DemandProcessing_UI SHALL list only values returned by service configuration.
22. WHEN the Frequency control is rendered without a configured default, THE DemandProcessing_UI SHALL select `Monthly`.
23. WHEN the Branch control is rendered for a new Demand Run, THE DemandProcessing_UI SHALL list only Active Branches within the user's scope.
24. WHEN the Branch control is rendered for a new Demand Run, THE DemandProcessing_UI SHALL allow an empty Branch value to mean all authorized Active Branches.
25. THE DemandProcessing_UI SHALL use the shared axios instance for JWT propagation and centralized 401, 403, 409, timeout, and error-toast behavior.
26. THE DemandProcessing_UI SHALL not create an ad-hoc HTTP client for Demand Processing.

### Requirement 12: Provide Demand Generation Output screen

**User Story:** As an Operations User, I want to inspect account-level demand output, so that I can reconcile customer dues and arrears for a branch and period.

#### Acceptance Criteria

1. WHEN an unauthorized user requests Demand Generation Output, THE DemandProcessing_UI SHALL render an access-denied state.
2. WHEN an unauthorized user requests Demand Generation Output, THE DemandProcessing_Service SHALL return no Demand Row.
3. WHEN an unauthorized user requests Demand Generation Output, THE DemandProcessing_Service SHALL return no aggregate demand data.
4. WHEN an authorized user opens output for a Run, THE DemandProcessing_UI SHALL display Account, Customer, Branch, LOB, Month/Year, Frequency, Current Due, Arrears, ODI, Other Charges, Total Charges, and Total Demand.
5. WHEN an authorized user opens output for a Run, THE DemandProcessing_UI SHALL display row status, Run identifier, and Billing Snapshot version.
6. WHEN a Demand Row is displayed, THE DemandProcessing_UI SHALL display persisted component values.
7. WHEN a Demand Row is displayed, THE DemandProcessing_UI SHALL make Current Due plus Arrears plus Total Charges equal to Total Demand verifiable at approved INR precision.
8. WHEN filters for Run, Month/Year, LOB, Branch, Account, Customer, or row status are supplied, THE DemandProcessing_UI SHALL send every supplied filter through the API contract.
9. WHEN filters are supplied, THE DemandProcessing_UI SHALL display only records matching all supplied filters.
10. WHEN output spans multiple pages, THE DemandProcessing_UI SHALL use configured page-size bounds.
11. WHEN output spans multiple pages, THE DemandProcessing_UI SHALL preserve deterministic Account-identifier ordering across page changes.
12. WHEN no rows match an authorized query, THE DemandProcessing_UI SHALL display an English empty state.
13. WHEN no rows match an authorized query, THE DemandProcessing_UI SHALL distinguish the empty state from loading, failed, timed-out, Billing-incomplete, and still-processing states.
14. IF an output refresh fails or times out, THEN THE DemandProcessing_UI SHALL display a retryable English error state.
15. IF an output refresh fails or times out, THEN THE DemandProcessing_UI SHALL preserve the last successfully displayed data.
16. WHEN an authorized user requests a Demand Generation Output PDF Export, THE DemandProcessing_Service SHALL include persisted row values.
17. WHEN an authorized user requests a Demand Generation Output PDF Export, THE DemandProcessing_Service SHALL include run, period, and branch metadata.
18. WHEN an authorized user requests a Demand Generation Output PDF Export, THE DemandProcessing_Service SHALL include totals and a generated-at timestamp.
19. WHEN an authorized user requests a Demand Generation Output PDF Export, THE DemandProcessing_Service SHALL apply the caller's permitted data scope.
20. WHEN an authorized user requests a Demand Generation Output PDF Export, THE DemandProcessing_Service SHALL not recalculate demand.
21. IF a caller is unauthorized for the requested Output PDF Export or requests data outside permitted scope, THEN THE DemandProcessing_Service SHALL return HTTP 403.
22. IF a caller is unauthorized for the requested Output PDF Export, THEN THE DemandProcessing_Service SHALL generate no PDF.
23. IF a caller is unauthorized for the requested Output PDF Export, THEN THE DemandProcessing_Service SHALL disclose no unauthorized data.
24. THE DemandProcessing_UI SHALL not expose browser print as a separate Demand Processing capability in this release.
25. THE DemandProcessing_Service SHALL leave browser print deferred.
26. WHEN an authorized user selects a deactivated Branch in Output filters, THE DemandProcessing_UI SHALL display retained Demand Rows returned by the service.
27. WHEN an authorized user selects a deactivated Branch in Output filters, THE DemandProcessing_UI SHALL label the Branch as deactivated.

### Requirement 13: Provide Demand Spooling screen and branch actions

**User Story:** As a Branch Operations User, I want to see branch-wise Demand Batches and spool approved batches, so that each branch receives its generated demand without recalculation or cross-branch leakage.

#### Acceptance Criteria

1. WHEN an authorized user opens Demand Spooling, THE DemandProcessing_UI SHALL request only Branch Batches within the user's permitted Branch and LOB scope.
2. WHEN an authorized user opens Demand Spooling, THE DemandProcessing_UI SHALL display only Branch Batches within the user's permitted Branch and LOB scope.
3. WHEN a Branch Batch is displayed, THE DemandProcessing_UI SHALL display Branch, LOB, Frequency, Month/Year, Run identifier, and Billing Snapshot version.
4. WHEN a Branch Batch is displayed, THE DemandProcessing_UI SHALL display row count, component totals, Total Demand, batch status, last spool timestamp, source/actor, and failure class when present.
5. IF a Branch Batch is not in the completed reconciled status, THEN THE DemandProcessing_UI SHALL disable the spool action.
6. IF a Branch Batch is not in the completed reconciled status, THEN THE DemandProcessing_UI SHALL display the machine-readable failure or readiness reason.
7. WHEN an authorized user requests spooling for a `ReadyToSpool` Branch Batch, THE DemandProcessing_Service SHALL use the persisted batch only.
8. WHEN an authorized user requests spooling for a `ReadyToSpool` Branch Batch, THE DemandProcessing_Service SHALL set the minimal batch spool status.
9. WHEN an authorized user requests spooling for a `ReadyToSpool` Branch Batch, THE DemandProcessing_Service SHALL record the actor and UTC timestamp.
10. WHEN an authorized user requests spooling for a `ReadyToSpool` Branch Batch, THE DemandProcessing_Service SHALL return the resulting status to DemandProcessing_UI through the shared service.
11. WHEN the same logical Branch Batch is requested for spooling repeatedly, THE DemandProcessing_Service SHALL return the existing spool status.
12. WHEN the same logical Branch Batch is requested for spooling repeatedly, THE DemandProcessing_Service SHALL create no duplicate logical spool record.
13. IF a Branch Batch is not `ReadyToSpool`, THEN THE DemandProcessing_Service SHALL reject the request with a spool-not-ready code.
14. IF a Branch Batch is not `ReadyToSpool`, THEN THE DemandProcessing_Service SHALL leave the batch unchanged.
15. THE DemandProcessing_Service SHALL not define spool transport in this release.
16. THE DemandProcessing_Service SHALL not define downstream acknowledgement in this release.
17. THE DemandProcessing_Service SHALL not define spool retry limits in this release.
18. THE DemandProcessing_Service SHALL not define downstream idempotency in this release.
19. WHEN an authorized user requests a Demand Spooling or Branch Batch PDF Export, THE DemandProcessing_Service SHALL include persisted batch values.
20. WHEN an authorized user requests a Demand Spooling or Branch Batch PDF Export, THE DemandProcessing_Service SHALL include run, period, and branch metadata.
21. WHEN an authorized user requests a Demand Spooling or Branch Batch PDF Export, THE DemandProcessing_Service SHALL include component totals, Total Demand, and generated-at timestamp.
22. WHEN an authorized user requests a Demand Spooling or Branch Batch PDF Export, THE DemandProcessing_Service SHALL apply the caller's permitted Branch scope.
23. WHEN an authorized user requests a Demand Spooling or Branch Batch PDF Export, THE DemandProcessing_Service SHALL not recalculate or replace batch values.
24. IF a caller is unauthorized for the requested Spooling PDF Export or requests a Branch outside permitted scope, THEN THE DemandProcessing_Service SHALL return HTTP 403.
25. IF a caller is unauthorized for the requested Spooling PDF Export, THEN THE DemandProcessing_Service SHALL generate no PDF.
26. IF a caller is unauthorized for the requested Spooling PDF Export, THEN THE DemandProcessing_Service SHALL disclose no unauthorized batch data.
27. THE DemandProcessing_UI SHALL not expose browser print as a separate Demand Spooling capability in this release.
28. THE DemandProcessing_Service SHALL leave browser print deferred.
29. WHEN a Branch Batch is spooled, THE DemandProcessing_Service SHALL use the completed persisted batch.
30. WHEN a Branch Batch is spooled, THE DemandProcessing_Service SHALL not recalculate or replace Demand Rows.

### Requirement 14: Enforce authentication, authorization, and data scope

**User Story:** As a security stakeholder, I want Demand Processing protected by least-privilege access and branch scope, so that financial demand data and operational actions are restricted.

#### Acceptance Criteria

1. IF a request contains a missing, malformed, expired, or signature-invalid JWT Bearer token, THEN THE DemandProcessing_Service SHALL return HTTP 401.
2. IF a request contains a missing, malformed, expired, or signature-invalid JWT Bearer token, THEN THE DemandProcessing_Service SHALL disclose no demand data.
3. IF a request contains a missing, malformed, expired, or signature-invalid JWT Bearer token, THEN THE DemandProcessing_Service SHALL perform no processing, spooling, or processable-state change.
4. IF a request contains a valid authenticated identity without permission for the requested operation or LOB/Branch scope, THEN THE DemandProcessing_Service SHALL return HTTP 403.
5. IF authorization fails, THEN THE DemandProcessing_Service SHALL disclose no demand data.
6. IF authorization fails, THEN THE DemandProcessing_Service SHALL perform no processing, spooling, or processable-state change.
7. WHEN an authenticated caller requests Run creation or rerun, THE DemandProcessing_Service SHALL evaluate run/rerun permission independently from output-read permission.
8. WHEN an authenticated caller requests Run creation or rerun, THE DemandProcessing_Service SHALL evaluate run/rerun permission independently from spooling permission.
9. WHEN an authenticated caller requests output or history, THE DemandProcessing_Service SHALL return only records within the caller's permitted LOB and Branch data scope.
10. WHEN an authenticated caller requests spooling or re-spooling, THE DemandProcessing_Service SHALL validate the requested Branch against the caller's permitted Branch scope before changing state.
11. WHEN Automizer invokes Demand Processing, THE DemandProcessing_Service SHALL authenticate the Automizer identity.
12. WHEN Automizer invokes Demand Processing, THE DemandProcessing_Service SHALL authorize the requested LOB and Branch scope.
13. WHEN Automizer invokes Demand Processing, THE DemandProcessing_Service SHALL retain Automizer identity, source, and correlation data.
14. WHEN a request lacks authentication and authorization, THE DemandProcessing_Service SHALL apply authentication first.
15. WHEN a request lacks authentication and authorization, THE DemandProcessing_Service SHALL return HTTP 401.
16. THE DemandProcessing_UI SHALL use the shared axios service for JWT propagation and centralized 401, 403, 409, timeout, and error handling.
17. THE DemandProcessing_UI SHALL not create a second HTTP client.
18. THE DemandProcessing_Service SHALL read roles and policy names for run/rerun, Manual Demand Run, output/history read, and spool actions from appSettings.
19. WHERE an admin-role override is configured in appSettings, THE DemandProcessing_Service SHALL grant configured admin roles access to Demand Processing operations within the admin data scope.
20. IF a required role or policy setting is missing from appSettings, THEN THE DemandProcessing_Service SHALL use the documented default value.
21. THE DemandProcessing_Service SHALL not use a role name hard-coded outside configuration defaults.

### Requirement 15: Record audit history and operational traceability

**User Story:** As a Financial Controller, I want every demand lifecycle action traceable, so that period results and branch delivery can be investigated.

#### Acceptance Criteria

1. WHEN a Demand Run status changes, THE DemandProcessing_Service SHALL append an immutable audit record.
2. WHEN a Demand Run status changes, THE DemandProcessing_Service SHALL include in the appended audit record Run ID and Run Revision.
3. WHEN a Demand Run status changes, THE DemandProcessing_Service SHALL include in the appended audit record scope, actor/source, and correlation identifier.
4. WHEN a Demand Run status changes, THE DemandProcessing_Service SHALL include in the appended audit record previous and new status where applicable.
5. WHEN a Demand Run status changes, THE DemandProcessing_Service SHALL include in the appended audit record a UTC timestamp, outcome/error code, and safe references.
6. WHEN a Demand Batch status changes, THE DemandProcessing_Service SHALL append an immutable audit record.
7. WHEN a Demand Batch status changes, THE DemandProcessing_Service SHALL include in the appended audit record Run ID, Run Revision, Branch scope, actor/source, and correlation identifier.
8. WHEN a Demand Batch status changes, THE DemandProcessing_Service SHALL include in the appended audit record previous and new status where applicable.
9. WHEN a Demand Batch status changes, THE DemandProcessing_Service SHALL include in the appended audit record a UTC timestamp, outcome/error code, and safe references.
10. WHEN a controlled rerun, spool action, retry, or authorization outcome changes lifecycle state, THE DemandProcessing_Service SHALL append the minimum immutable audit record.
11. THE DemandProcessing_Service SHALL not place bearer tokens, credentials, or connection strings in the minimum audit record or safe references.
12. THE DemandProcessing_Service SHALL not place unnecessary customer fields in the minimum audit record or safe references.
13. IF an unauthorized caller requests a Run, Demand Row, Demand Batch, or audit record, THEN THE DemandProcessing_Service SHALL apply Requirement 14 authorization behavior.
14. IF an unauthorized caller requests a Run, Demand Row, Demand Batch, or audit record, THEN THE DemandProcessing_Service SHALL disclose no demand data.
15. IF an unauthorized caller attempts to mutate or delete a Demand Run, Demand Row, Demand Batch, Spool Attempt, failure record, or audit entry, THEN THE DemandProcessing_Service SHALL reject the request.
16. IF an unauthorized caller attempts to mutate or delete a retained record, THEN THE DemandProcessing_Service SHALL preserve the existing record.
17. WHEN a rerun or re-spool occurs, THE DemandProcessing_Service SHALL append new lifecycle records.
18. WHEN a rerun or re-spool occurs, THE DemandProcessing_Service SHALL preserve prior output, source references, status transitions, failures, and audit entries.
19. WHERE a retention or archival period is configured, WHEN the configured period is reached, THE DemandProcessing_Service SHALL apply the configured retention action.
20. WHERE a retention or archival period is configured, THE DemandProcessing_Service SHALL preserve required financial and operational history until expiry.
21. THE DemandProcessing_Service SHALL leave retention duration, archival mechanism, purge authority, detailed event taxonomy, additional fields, and audit-content schema configurable or deferred.
22. WHEN a rejected or unauthorized request is audited, THE DemandProcessing_Service SHALL append only safe minimum audit fields.
23. WHEN a rejected or unauthorized request is audited, THE DemandProcessing_Service SHALL create no processable Run, Demand Row, Demand Batch, or spool state.

### Requirement 16: Ensure deterministic behavior and verifiable correctness

**User Story:** As a development and assurance stakeholder, I want Demand Processing rules to be deterministic and testable, so that financial output can be verified without relying only on end-to-end examples.

#### Acceptance Criteria

1. THE DemandProcessing_Service SHALL calculate Billing Period Results deterministically.
2. THE DemandProcessing_Service SHALL calculate Demand Rows deterministically.
3. WHEN normalized inputs, available schedule/source references, allocation inputs, rounding policy, scope, and Billing Snapshot version are identical, THE DemandProcessing_Service SHALL produce identical component values.
4. WHEN normalized inputs, available schedule/source references, allocation inputs, rounding policy, scope, and Billing Snapshot version are identical, THE DemandProcessing_Service SHALL order Accounts identically.
5. THE DemandProcessing_Service SHALL preserve the invariant that Scheduled Installment equals finalized schedule principal plus finalized schedule scheduled interest/EMI.
6. THE DemandProcessing_Service SHALL preserve the invariant that each approved receipt, waiver, reversal, adjustment, and other account-level event is applied once.
7. THE DemandProcessing_Service SHALL preserve the invariant that approved allocation results determine Current Due.
8. THE DemandProcessing_Service SHALL preserve the invariant that prior-period unpaid values determine Arrears.
9. THE DemandProcessing_Service SHALL preserve the invariant `Total Charges = ODI + Other Charges` at approved INR precision.
10. THE DemandProcessing_Service SHALL preserve the invariant `Total Demand = Current Due + Arrears + Total Charges` at approved INR precision.
11. THE DemandProcessing_Service SHALL preserve the invariant that every completed Demand Row belongs to the requested LOB and Branch scope.
12. THE DemandProcessing_Service SHALL preserve the invariant that every completed Demand Row has resolvable Customer, Branch, and LOB references.
13. THE DemandProcessing_Service SHALL preserve the invariant that every completed Demand Row has Account status exactly `Active` within the internally consistent run snapshot/cut-off.
14. THE DemandProcessing_Service SHALL preserve the invariant that every completed Demand Row belongs to exactly one matching Demand Batch.
15. THE DemandProcessing_Service SHALL preserve the invariant that a Demand Batch Branch equals each contained row Branch.
16. WHEN the same logical Billing scope and period is retried, THE DemandProcessing_Service SHALL not increase logical Billing Period Result count.
17. WHEN the same logical Billing scope and period is retried, THE DemandProcessing_Service SHALL not increase logical Demand Row count.
18. WHEN the same logical Billing scope and period is retried, THE DemandProcessing_Service SHALL not increase logical Demand Batch count.
19. WHEN the same logical Billing scope and period is retried, THE DemandProcessing_Service SHALL not increase allocation count.
20. WHEN the same logical Billing scope and period is retried, THE DemandProcessing_Service SHALL not increase billing-journal count.
21. THE DemandProcessing_Service SHALL block Demand Snapshot publication before internal Billing succeeds.
22. THE DemandProcessing_Service SHALL block Demand Snapshot publication before all required Billing Period Results complete.
23. THE DemandProcessing_Service SHALL block Demand Snapshot publication before all required journal checks succeed.
24. THE DemandProcessing_Service SHALL block Demand Spooling before the publication gates succeed.
25. WHEN equivalent accepted requests are processed, THE DemandProcessing_Service SHALL not increase logical Demand Row count.
26. WHEN equivalent accepted requests are processed, THE DemandProcessing_Service SHALL not increase logical Demand Batch count.
27. WHEN equivalent accepted requests are processed, THE DemandProcessing_Service SHALL not increase successful logical spool-delivery count.
28. THE DemandProcessing_Service SHALL provide FsCheck property-based tests with at least 100 generated cases for schedule-driven Billing.
29. THE DemandProcessing_Service SHALL provide FsCheck property-based tests with at least 100 generated cases for installment equality, exactly-once allocation, unpaid-amount conservation, charge formulas, approved rounding, zero values, boundary statuses, eligibility, scope, batch isolation, retry idempotency, duplicate triggers, publication gating, and multi-Branch inputs.
30. THE DemandProcessing_Service SHALL provide xUnit example and edge-case tests for Billing preconditions, stage failures, schedule finalization, source references, receipts, waivers, reversals, adjustments, charge formulas and adapter output, allocation diagnostics, Journal Port checks, authentication, authorization, Branch scope, failure classification, reconciliation, idempotency, concurrency, reruns, publication gating, PDF authorization, persisted-value generation, and spooling.
31. THE DemandProcessing_Service SHALL provide `WebApplicationFactory<Program>` integration tests for host wiring and end-to-end API behavior.
32. THE DemandProcessing_UI SHALL provide Vitest and React Testing Library tests for service selection, shared axios/JWT behavior, form validation, initiation success and failure, status states, output rendering, deterministic filtering and pagination, authorized PDF export, unauthorized export rejection, persisted-value export content, spooling state, unauthorized responses, and timeout behavior.
33. WHERE Assumption A23 confirms the testing policy, THE DemandProcessing_Service SHALL retain approved property generators, integration examples, performance thresholds, and test-data factories without weakening the stated invariants.

### Requirement 17: Apply India-only English-only product constraints

**User Story:** As a Finnova product stakeholder, I want Demand Processing aligned with the India-only platform, so that the feature is consistent with the product's localization policy.

#### Acceptance Criteria

1. THE DemandProcessing_Service SHALL represent monetary amounts in Indian Rupees (INR).
2. THE DemandProcessing_Service SHALL calculate monetary amounts using approved two-decimal precision.
3. THE DemandProcessing_Service SHALL persist monetary amounts using approved two-decimal precision.
4. THE DemandProcessing_Service SHALL compare monetary amounts using approved two-decimal precision.
5. THE DemandProcessing_Service SHALL expose monetary amounts using approved two-decimal precision.
6. THE DemandProcessing_Service and DemandProcessing_UI SHALL use English for all user-facing labels, statuses, validation messages, error messages, seed/reference values, test data, and display values.
7. THE DemandProcessing_Service and DemandProcessing_UI SHALL use one plain display value for each business value.
8. THE DemandProcessing_Service and DemandProcessing_UI SHALL not add bilingual fields or language-suffixed fields.
9. THE DemandProcessing_Service and DemandProcessing_UI SHALL not add translation scaffolding, Arabic content, or other non-English sample values.
10. THE DemandProcessing_Service and DemandProcessing_UI SHALL not add non-English localization behavior.
11. THE DemandProcessing_UI SHALL use the platform's left-to-right layout conventions.
12. THE DemandProcessing_UI SHALL not add RTL direction handling or `dir="rtl"` behavior.
13. THE DemandProcessing_Service SHALL derive Month/Year in the `Asia/Kolkata` business timezone.
14. THE DemandProcessing_Service SHALL persist UTC timestamps for audit, correlation, technical ordering, and lifecycle events.
15. THE DemandProcessing_Service and DemandProcessing_UI SHALL present amounts and period labels in English.
16. THE DemandProcessing_Service and DemandProcessing_UI SHALL present amounts and period labels using left-to-right layout.
