# Requirements Document

## Introduction

The Demand Processing feature provides an operational MIS of account dues and arrears for a billing period. Demand Processing contains two ordered internal stages: **Period Billing** and **Demand Generation/Spooling**. Period Billing reads the finalized Amortization Schedule, applies approved account-level financial allocations, and produces the period Billing Result. Demand Generation uses that Billing Result to create account-level demand, groups the results by branch, and makes branch-wise demand batches available for operational spooling. The overall Demand Run is the operational unit for the ordered stages, their status, diagnostics, audit history, and final snapshot. The feature covers the backend API in `e:\Finnova\Finnova-API` and the React frontend in `E:\Finnova\Finnova-UI\Finnova-UI`.

The supplied FRS excerpt, section 1.5.4.27.31 on page 42, explicitly describes Demand Processing as a MIS of dues and arrears processed automatically after billing is complete. The excerpt also states that Automizer triggers branches with Demand Spooling. The surrounding FRS context states that billing generates installment dues, debits overdue accounts, and generates billing journals. This document treats those statements as the explicit FRS baseline. The clarified scope makes Billing an internal, ordered Demand Processing stage rather than an entirely external prerequisite. The requested module shape of Demand Run, Demand Generation Output, and Demand Spooling, together with the listed inputs and outputs, is also captured below. The attached screenshot is corroborating source material only and does not add behavior beyond the quoted FRS text.

A Demand Run accepts Line of Business (LOB), Branch, Frequency, and Month/Year. Period Billing reads the finalized account Amortization Schedule for the selected period, applies approved receipts, waivers, reversals, adjustments, and other account-level allocation rules exactly once, produces the Billing Period Result and billing journals, and then Demand Generation produces account-level output containing account, customer, Current Due, Arrears, ODI, Other Charges, and Total Demand. The baseline processing rules include billing only eligible Active accounts according to the approved cut-off, excluding Closed, Cancelled, and Pre-Closed accounts, using Current Due + Arrears + Charges, creating branch-wise batches, and retaining Month/Year history. Demand Processing does not independently calculate EMI or mutate the Amortization Schedule.

This is an operational processing feature, not a master-data feature. It does not create or manage LOB, Branch, Customer, Account, Amortization Schedule, receipt, billing, installment, or accounting master records. It consumes or orchestrates the authoritative records and contracts owned by those areas. Where the FRS or request is silent, this document records a proposed decision as an **Assumption for confirmation** rather than presenting the decision as an explicit FRS requirement.

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

- **Dependency (Finalized Amortization Schedule):** Period Billing depends on the authoritative finalized Amortization Schedule for each eligible account and requested LOB, Branch scope, Frequency, and Month/Year. The schedule supplies the selected period's scheduled principal and scheduled interest/EMI; Demand Processing does not own, revise, or recompute the schedule.
- **Dependency (Receipts and account-level allocation sources):** Period Billing depends on authoritative receipt/collection records and approved waiver, reversal, adjustment, and other account-level allocation policies. The owning sources remain responsible for their records; Billing applies each eligible financial event once under the approved allocation contract.
- **Dependency (Billing and Demand accounting boundary):** Period Billing depends on the approved accounting journal boundary and must generate/post billing journals before publishing a successful Billing Snapshot and allowing Demand Generation. The exact journal ownership, posting transaction, and reconciliation contract remain an **Assumption for confirmation**.
- **Dependency (Account, Customer, and master sources):** Demand Processing consumes authoritative Account and Customer records and their status, Branch, LOB, and identifier relationships. It does not duplicate or manage those masters. LOB, Branch, Account, Customer, Amortization Schedule, receipt/collection, and related master/source ownership stays with the existing owning modules.
- **Dependency (Automation):** Automizer or the approved scheduler supplies the normal trigger, scope, period, authentication, correlation, and retry signal for the overall Demand Run. The exact event, schedule, authentication, retry, manual-trigger, and endpoint contract remain to be confirmed.
- **Dependency (Repositories):** Backend implementation belongs in `e:\Finnova\Finnova-API` and must integrate with the existing .NET service, repository, EF Core, CQRS, authentication, middleware, and gateway conventions selected during design. Frontend implementation belongs in `E:\Finnova\Finnova-UI\Finnova-UI` and must use the existing React 18, TypeScript, Vite, Material UI, shared axios, service-interface, real-service, mock-service, and API-toggle conventions.
- **Dependency (Charge sources):** Demand Processing consumes ODI and Other Charges from the successful Billing Period Result. It does not independently calculate those values or duplicate allocation of receipts, waivers, reversals, or adjustments.
- **Assumption A1 (Host placement):** Demand Processing is proposed for the existing operational/accounts service boundary rather than the SystemAdmin master-data boundary, because Demand Processing consumes billing and account transactions. The exact host and gateway route are proposed for confirmation.
- **Assumption A2 (Run identity):** Each accepted Demand Run receives a system-generated immutable Run identifier. The identifier format and sequence strategy are proposed for confirmation.
- **Assumption A3 (Period identity):** Month/Year identifies one billing and demand period in the business timezone. The baseline proposed business timezone is `Asia/Kolkata`, and the accepted Month/Year range, future-period policy, and date representation are proposed for confirmation.
- **Assumption A4 (Frequency values):** Frequency is an enumerated business value aligned with Billing. The supported values, whether a frequency is monthly-only or includes weekly/fortnightly/quarterly values, and the exact matching rule against billing are proposed for confirmation. Demand Processing must not invent a frequency that Billing does not support.
- **Assumption A5 (Scope semantics):** A selected Branch identifies one branch, while an omitted Branch represents all authorized branches for the selected LOB. Whether blank Branch is allowed, and whether a selected LOB is mandatory or can represent all authorized LOBs, are proposed for confirmation.
- **Assumption A6 (Charge composition):** The formula's `Charges` value is proposed to equal `ODI + Other Charges`, with both components shown separately in the output. If ODI is already included in Other Charges or if another component belongs in Charges, the authoritative Billing contract must define the composition and prevent double counting.
- **Assumption A7 (Current Due and Arrears):** Current Due is the unpaid selected-period Scheduled Installment after approved receipts, waivers, reversals, adjustments, and other account-level allocation rules are applied exactly once. Arrears is the unpaid amount carried from previous Billing Period Results, excluding ODI and Other Charges. The exact treatment of part-paid installments, allocation ordering, excess receipts, and suspense balances is proposed for confirmation.
- **Assumption A8 (Status source and timing):** Account status is evaluated against the authoritative status at the Demand Run's processing cut-off. The proposed eligible status is exactly `Active`; `Closed`, `Cancelled`, and `Pre-Closed` are excluded. The status code casing, any additional terminal statuses, and the cut-off timestamp are proposed for confirmation.
- **Assumption A9 (Branch ownership):** Each included account has one resolvable Branch and LOB at the processing cut-off. Accounts with missing, inactive, or conflicting organizational references are proposed to be excluded and reported as data-quality failures rather than silently assigned to a batch.
- **Assumption A10 (Batch semantics):** Demand Processing creates one logical Demand Batch per branch within a Demand Run. A batch contains the account-level demand rows for that branch, a deterministic batch status, totals, and an Automizer/spooling reference. Whether a branch can have multiple physical spool files or downstream queue messages is an implementation decision that must preserve one logical branch batch.
- **Assumption A11 (Spooling semantics):** Demand Spooling makes completed branch batches available to the approved downstream branch process; it does not recalculate demand. A spool action is proposed to be idempotent, auditable, and stateful with statuses such as Pending, Spooled, Failed, and Re-spool Pending. Exact delivery mechanism, acknowledgement, and retry policy are proposed for confirmation.
- **Assumption A12 (Automation versus manual run):** Automizer is the normal trigger after successful Billing. An authorized operations user may initiate a manual Demand Run for recovery or controlled reprocessing, subject to the same billing-completion, scope, authorization, and idempotency rules. Whether manual runs are allowed in production and which role may use them are proposed for confirmation.
- **Assumption A13 (Rerun policy):** Repeating the same LOB, branch scope, frequency, and Month/Year is treated as a rerun of the same logical period. The baseline proposed behavior is to return the existing completed result when the source snapshot and input scope are unchanged, and to require an explicit authorized rerun command to replace or supersede a failed or stale result. Whether a rerun appends a revision, replaces the prior revision, or is prohibited after spooling is proposed for confirmation.
- **Assumption A14 (Concurrency policy):** At most one active calculation or replacement rerun may hold the processing lease for one logical scope key at a time. Concurrent requests for the same scope are proposed to return the existing active Run or a conflict response, and must not create duplicate demand rows or duplicate branch batches.
- **Assumption A15 (Snapshot consistency):** A Demand Run reads a consistent snapshot of billing, account, customer, and charge data for its processing cut-off. Whether the implementation uses a database transaction, an immutable billing snapshot, or a versioned source export is a design decision, but the output must remain reproducible for the retained Run history.
- **Assumption A16 (Partial branch failures):** The baseline proposed behavior is branch isolation: a failure for one branch marks that branch batch Failed, preserves successful branch batches, and marks the overall Run Partially Failed. Whether the business requires all-or-nothing completion for a multi-branch run is proposed for confirmation.
- **Assumption A17 (Zero demand):** Accounts that meet eligibility rules but have a total demand of 0.00 INR are proposed to remain in the output with a zero amount and an explicit zero-demand indicator, unless the approved Billing contract says zero rows are omitted. The choice must be consistent across output totals and spooling.
- **Assumption A18 (Precision and currency):** All amounts are INR and are stored, calculated, displayed, and compared to two decimal places using the platform-approved non-negative monetary rounding rule. The exact rounding mode and whether source amounts are already rounded are proposed for confirmation.
- **Assumption A19 (History and retention):** Demand Runs, input scope, billing reference, account-level output, branch batches, spooling attempts, failures, actor, timestamps, and revisions are retained for the platform-approved financial and operational retention period. The duration, archival mechanism, and purge authority are proposed for confirmation.
- **Assumption A20 (Authorization):** Demand Run, Demand Generation Output, and Demand Spooling require an authenticated JWT Bearer token. Run and rerun actions are proposed for an Operations or Accounts role, output reads are proposed for authorized operational users, and spooling/re-spooling is proposed for an Operations or Branch Operations role. Exact role and policy names are proposed for confirmation.
- **Assumption A21 (Audit):** Every run request, accepted or rejected start, calculation completion, branch-batch transition, rerun decision, spool attempt, and failure is proposed to create an immutable audit entry with actor, source (`Automizer` or user), correlation identifier, scope, status transition, and UTC timestamp.
- **Assumption A22 (Frontend behavior):** The three requested screens are proposed as role-protected operational screens. Demand Run starts or requests a run, Demand Generation Output reads retained account-level results, and Demand Spooling reads branch batches and invokes the approved spool/re-spool action. Exact navigation labels, route, filters, pagination, export behavior, and polling interval are proposed for confirmation.
- **Assumption A23 (Testing):** The calculation and scope-selection logic are pure or isolated enough for FsCheck property-based tests with at least 100 iterations. Backend authorization, billing stage, schedule-driven allocation, idempotency, concurrency, failure handling, journal checks, and gateway wiring use xUnit examples and `WebApplicationFactory<Program>` integration tests. Frontend services and screen behavior use Vitest and React Testing Library. These are verification expectations, not a replacement for the acceptance criteria.
- **Assumption A24 (Billing placement):** Period Billing may initially be implemented as a background worker within `AccountsService` as part of Demand Processing. A separate Billing deployment is not required. The worker boundary, queue/host lifecycle, and future extraction strategy are proposed for confirmation.
- **Assumption A25 (Billing trigger):** The default Billing trigger is proposed to be automatic through Automizer or the approved scheduler for a valid LOB, Branch, Frequency, and Month/Year scope. Whether an authorized user may trigger or recover Billing manually is proposed for confirmation.
- **Assumption A26 (Billing cut-off and eligibility):** The period cut-off timestamp, schedule finalization version, receipt/collection cut-off, and exact Active-account eligibility rule are proposed for confirmation. Billing and Demand Generation must use the same approved cut-off and retain it with the run.
- **Assumption A27 (Allocation ordering):** The ordering and treatment of receipts, waivers, reversals, adjustments, suspense, excess receipts, and other account-level events are proposed for confirmation. The approved rules must allocate each source event exactly once and conserve the resulting unpaid balance.
- **Assumption A28 (ODI and Other Charges policy):** ODI and Other Charges are separate Billing Result components. Their formulas, effective dates, caps, rounding, exclusions, and ownership are proposed for confirmation; Demand Processing must consume the approved values without recomputation.
- **Assumption A29 (Journal ownership):** Billing journal generation, posting, idempotency key, failure handling, and reconciliation ownership are proposed for confirmation. Demand Generation must remain blocked until the approved journal checks succeed.
- **Assumption A30 (Billing retention):** Billing Run status, diagnostics, source schedule/version references, allocation evidence, Billing Period Results, journal references, Billing Snapshot, and audit history are retained according to the approved financial retention policy. Duration, archival mechanism, and purge authority are proposed for confirmation.
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
- **Billing Period Result**: The retained account-level result for one billed period, including the Scheduled Installment, allocated events, Current Due, Arrears, ODI, Other Charges, source schedule/version references, policy references, and billing status.
- **Scheduled Installment**: The selected period's scheduled principal plus scheduled interest/EMI read from the Account's finalized Amortization Schedule. Demand Processing treats this value as authoritative and does not recompute it.
- **Amortization Schedule**: The authoritative finalized account schedule containing period-level scheduled principal and scheduled interest/EMI, together with its identity and version. The owning Accounts or lending component maintains the schedule.
- **Current Due**: The unpaid amount of the selected period's Scheduled Installment after approved receipts, waivers, reversals, adjustments, and other account-level allocation rules are applied exactly once.
- **Arrears**: Previous unpaid amounts carried from earlier Billing Period Results after approved allocation, separate from the selected period's Current Due.
- **Billing Snapshot**: The consistent, versioned set of successful Billing Period Results, account eligibility, schedule/version references, allocation outcomes, ODI, Other Charges, journal references, and related source values read by Demand Generation.
- **Demand Snapshot**: The consistent, versioned publication containing the successful Billing Snapshot reference, completed Demand Rows, completed Demand Batches, aggregate totals, and stage/run status that becomes visible for Demand Output and Spooling only after Billing and Demand checks succeed.
- **Automizer**: The approved scheduling/orchestration component that triggers the overall Demand Run and supplies an automation identity and correlation data.
- **Billing**: The internal Period Billing capability that reads finalized Amortization Schedules, applies approved account-level allocations, produces Billing Period Results, and generates/posts billing journals for a selected period.
- **Billing Completion Marker**: The durable success record or contract response proving that the internal Billing Stage/Run completed successfully for the Demand scope.
- **Line of Business (LOB)**: An existing business classification used by Billing and Accounts. Demand Processing references LOB records and does not manage them.
- **Branch**: An existing organizational unit used to group accounts and Demand Batches. Demand Processing references Branch records and does not manage them.
- **Account**: An existing financial account with a customer, LOB, Branch, status, and financial activity. Demand Processing reads eligible accounts and does not create accounts.
- **Customer**: The existing customer associated with an Account and displayed on Demand Generation Output.
- **Active Account**: An Account whose authoritative status is exactly Active at the Demand Run cut-off.
- **Excluded Account**: An Account whose authoritative status is Closed, Cancelled, or Pre-Closed at the Demand Run cut-off.
- **ODI**: The overdue or delayed-payment interest amount supplied by the authoritative charge source. The exact expansion and calculation ownership require confirmation.
- **Other Charges**: Charges other than Current Due, Arrears, and ODI that the approved charge source includes in Demand.
- **Total Charges**: The charge component used by the demand formula. The proposed baseline is `ODI + Other Charges`.
- **Total Demand**: The amount calculated as `Current Due + Arrears + Total Charges`.
- **Frequency**: The billing periodicity selected for a Demand Run and matched to the completed Billing period.
- **Month/Year**: The business period identifier for a Demand Run, represented by a calendar month and year in the approved business timezone.
- **Processing Cut-off**: The timestamp or source version at which Demand Processing evaluates account status and financial values for a Run.
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

1. WHEN a caller submits a Demand Run, THE DemandProcessing_Service SHALL authenticate the caller before evaluating authorization or processing the request.
2. IF authentication fails because the JWT is missing, malformed, expired, or invalid, THEN THE DemandProcessing_Service SHALL reject the request with HTTP 401, SHALL disclose no demand data, and SHALL create no processable Run.
3. IF authentication succeeds but the caller lacks permission for the requested LOB or Branch scope, THEN THE DemandProcessing_Service SHALL reject the request with HTTP 403, SHALL disclose no demand data, and SHALL create no processable Run.
4. IF LOB, Frequency, or Month/Year is missing, THEN THE DemandProcessing_Service SHALL reject the request with a field-specific validation code and SHALL create no processable Run.
5. IF LOB, Branch, Frequency, or Month/Year cannot be represented as a Normalized Input, THEN THE DemandProcessing_Service SHALL reject the request with a field-specific validation code and SHALL create no processable Run.
6. IF Frequency is not an exact value supported by the Billing contract, THEN THE DemandProcessing_Service SHALL reject the request before reading financial source amounts or creating output.
7. WHEN authentication, authorization, and input validation succeed, THE DemandProcessing_Service SHALL persist one accepted Run with one immutable Run identifier and the normalized LOB, Branch scope, Frequency, and Month/Year as the Logical Scope Key.
8. WHEN the DemandProcessing_Service persists an accepted Run, THE DemandProcessing_Service SHALL retain the source identity, correlation identifier, UTC request timestamp, Processing Cut-off or source-version reference, and the accepted scope values.
9. WHERE the accepted scope contains multiple authorized Branch values, THE DemandProcessing_Service SHALL retain every resolved Branch and SHALL use each retained Branch as an independent batching dimension.
10. WHERE Assumption A5 is confirmed with omitted Branch meaning all authorized Branches for the selected LOB, WHEN Branch is omitted, THE DemandProcessing_Service SHALL resolve and retain the caller's authorized Branch set. **Assumption A5 remains pending confirmation.**
11. WHERE Assumption A5 is not confirmed with omitted Branch meaning all authorized Branches, WHEN Branch is omitted, THE DemandProcessing_Service SHALL reject the request with a Branch-required validation code. **Assumption A5 remains pending confirmation.**

### Requirement 2: Enforce successful internal Billing stage as a precondition

**User Story:** As a Financial Operations stakeholder, I want Demand Generation to use only a successfully completed internal Billing stage, so that the MIS does not report partial or unposted dues.

#### Acceptance Criteria

1. WHEN a Demand Run is accepted, THE DemandProcessing_Service SHALL create or resolve the internal Billing Stage/Run for the Run's exact normalized LOB, Branch scope, Frequency, and Month/Year.
2. IF the internal Billing Stage/Run is not successful for the exact normalized scope and period, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`, SHALL retain the Run in a non-processable status, and SHALL create no Demand Row or processable Demand Snapshot.
3. IF the internal Billing Stage/Run is failed, cancelled, partial, in progress, or missing a required Billing dependency, THEN THE DemandProcessing_Service SHALL retain the Billing status and dependency diagnostics and SHALL keep Demand Generation and Demand Spooling blocked.
4. WHEN the internal Billing Stage/Run reports success, THE DemandProcessing_Service SHALL verify finalized schedule references, account eligibility, exactly-once allocation outcomes, ODI, Other Charges, and billing-journal checks before starting Demand Generation.
5. IF any required Billing check is unsuccessful or missing, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`, SHALL retain each failed or missing check, SHALL create no Demand Row, and SHALL publish no Demand Snapshot.
6. WHEN all Billing checks succeed, THE DemandProcessing_Service SHALL retain the Billing Run reference, Billing Snapshot version, finalized schedule/version references, and Billing Period Results and SHALL use that one Billing Snapshot for the complete Demand Generation stage.
7. IF the Billing Stage/Run scope does not equal the Demand Run Logical Scope Key, THEN THE DemandProcessing_Service SHALL reject the Demand Run as `BillingScopeMismatch`, SHALL publish no Demand Snapshot, and SHALL retain the scope comparison diagnostics.

### Requirement 3: Execute schedule-driven period Billing

**User Story:** As a Financial Operations stakeholder, I want the Demand Run to bill each period from the finalized Amortization Schedule, so that demand uses the approved scheduled obligation and account-level financial allocations without recomputing EMI.

#### Acceptance Criteria

1. WHEN a Billing Stage/Run is requested, THE DemandProcessing_Service SHALL validate the normalized LOB, Branch scope, supported Frequency, and Month/Year before reading account financial values.
2. WHEN the recorded Billing Processing Cut-off is reached, THE DemandProcessing_Service SHALL evaluate account eligibility against the authoritative account status and organizational references at that cut-off and SHALL retain the decision.
3. WHEN an eligible Account is billed, THE DemandProcessing_Service SHALL read the Account's finalized Amortization Schedule version for the selected period and SHALL retain that exact schedule identity and version in the Billing Period Result.
4. WHEN the selected period exists in the finalized Amortization Schedule, THE DemandProcessing_Service SHALL set Scheduled Installment to the schedule's scheduled principal plus its scheduled interest/EMI component and SHALL use that retained schedule value as the period obligation before allocation.
5. THE DemandProcessing_Service SHALL not recompute EMI, derive a replacement installment from account inputs, mutate an Amortization Schedule, or create a Billing Period Result without an exact finalized schedule identity and version.
6. IF the schedule is absent, unfinalized, stale at the Processing Cut-off, or missing the selected period, THEN THE DemandProcessing_Service SHALL create no Billing Period Result for the affected Account, SHALL retain a schedule diagnostic, and SHALL prevent the affected output from entering a successful Billing Snapshot.
7. WHEN approved receipts, waivers, reversals, adjustments, and other account-level allocation events are available, THE DemandProcessing_Service SHALL apply each eligible event exactly once according to the approved allocation ordering and SHALL retain one event reference and one allocation outcome for each applied event.
8. WHEN allocation completes, THE DemandProcessing_Service SHALL calculate Current Due as the unpaid selected-period Scheduled Installment after the approved allocations and SHALL calculate Arrears from previous unpaid Billing Period Results without moving the selected-period unpaid amount into prior-period Arrears.
9. WHEN the Billing contract supplies ODI and Other Charges, THE DemandProcessing_Service SHALL retain ODI and Other Charges as separate components and SHALL exclude both components from Scheduled Installment, Current Due, and Arrears.
10. WHEN a Billing Period Result is produced, THE DemandProcessing_Service SHALL retain Scheduled Installment, scheduled principal, scheduled interest/EMI, every applied allocation reference and amount, Current Due, Arrears, ODI, Other Charges, source schedule identity/version, Processing Cut-off, and policy references.
11. WHEN all account Billing results and allocation checks succeed, THE DemandProcessing_Service SHALL generate and post the required billing journals before marking the Billing Stage/Run successful or publishing the Billing Snapshot.
12. WHEN the same logical Billing scope and Month/Year is retried with the same source versions, THE DemandProcessing_Service SHALL resolve the existing Billing Stage/Run and SHALL not create duplicate allocations, Billing Period Results, or billing journals.
13. IF an account Billing, allocation, schedule, charge, or journal check fails or remains incomplete, THEN THE DemandProcessing_Service SHALL retain the failure diagnostics, SHALL publish no successful Billing Snapshot containing the affected result, and SHALL keep Demand Generation and Demand Spooling blocked.
14. WHEN every required eligible Account has a successful Billing Period Result and all required journal checks succeed, THE DemandProcessing_Service SHALL publish one consistent Billing Snapshot for the exact requested scope and period and SHALL permit Demand Generation to proceed.
15. WHEN a Billing Stage/Run starts, progresses, completes, fails, or is retried, THE DemandProcessing_Service SHALL retain its status, diagnostics, source schedule/version references, allocation evidence, journal references, actor or Automizer source, correlation identifier, and audit history.
16. THE DemandProcessing_Service SHALL treat frequency values, period cut-off, allocation ordering, ODI and Other Charges formulas, journal ownership, trigger mode, and financial retention as **Assumptions for confirmation** until an approved policy defines each item.

### Requirement 4: Select eligible accounts and exclude ineligible accounts

**User Story:** As an Operations User, I want the MIS to include only eligible Active accounts, so that closed or cancelled exposures do not enter the branch demand batches.

#### Acceptance Criteria

1. WHEN the DemandProcessing_Service evaluates an Account, THE DemandProcessing_Service SHALL include the Account only when the Account is in the normalized LOB and Branch scope, has one resolvable Customer reference, has one resolvable Branch and LOB reference, and has authoritative status exactly `Active` at the recorded Processing Cut-off.
2. IF an Account has authoritative status `Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output and every Demand Batch.
3. IF an Account has authoritative status `Cancelled` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output and every Demand Batch.
4. IF an Account has authoritative status `Pre-Closed` at the Processing Cut-off, THEN THE DemandProcessing_Service SHALL exclude the Account from Demand Generation Output and every Demand Batch.
5. IF an Account has a missing, conflicting, or unsupported status, THEN THE DemandProcessing_Service SHALL retain a data-quality diagnostic for the Account and SHALL exclude the Account from Demand Generation Output and every Demand Batch.
6. IF an Account has a missing, inactive, or conflicting Customer, Branch, or LOB reference, THEN THE DemandProcessing_Service SHALL retain a data-quality diagnostic naming the reference type and Account identifier and SHALL exclude the Account from Demand Generation Output and every Demand Batch.
7. WHEN eligibility is evaluated, THE DemandProcessing_Service SHALL retain the Processing Cut-off, Billing Snapshot version, and status/reference values used for the decision. The exact status vocabulary and cut-off rule remain **Assumptions A8 and A26 for confirmation**.

### Requirement 5: Calculate account-level demand

**User Story:** As a Financial Operations stakeholder, I want each eligible account's demand calculated from authoritative due and charge components, so that the MIS supports collection and branch operations.

#### Acceptance Criteria

1. WHEN an eligible Account has a successful Billing Period Result in the retained Billing Snapshot, THE DemandProcessing_Service SHALL create one Demand Row for the Account unless the approved zero-demand policy requires omission.
2. WHEN the DemandProcessing_Service creates a Demand Row, THE DemandProcessing_Service SHALL copy Current Due from the unpaid selected-period Scheduled Installment, copy Arrears from previous unpaid Billing Period Results, copy ODI and Other Charges from the Billing Period Result, and retain the Account, Customer, LOB, Branch, Month/Year, Frequency, and Billing Period Result reference.
3. WHEN the DemandProcessing_Service calculates a Demand Row, THE DemandProcessing_Service SHALL calculate `Total Charges` as `ODI + Other Charges` once and SHALL calculate `Total Demand` as `Current Due + Arrears + Total Charges` using the approved INR rounding rule. The `ODI + Other Charges` composition remains **Assumption A6 for confirmation**.
4. THE DemandProcessing_Service SHALL not independently calculate EMI, derive a replacement Scheduled Installment, or mutate the Amortization Schedule, and SHALL preserve the Billing Period Result's source schedule/version reference.
5. IF a required source component is missing, non-finite, outside the source contract's permitted sign range, or not representable at approved INR precision, THEN THE DemandProcessing_Service SHALL reject the affected row, SHALL retain the Account, component, value-state, and diagnostic code, and SHALL exclude the row from every completed Demand Batch.
6. WHEN the approved monetary policy is applied, THE DemandProcessing_Service SHALL round and persist every component, Total Charges, Total Demand, and aggregate total in INR to two decimal places using one approved rounding mode. The rounding mode and source-scale rule remain **Assumption A18 for confirmation**.
7. WHERE the approved zero-demand policy retains zero rows, WHEN Total Demand equals `0.00 INR`, THE DemandProcessing_Service SHALL persist the eligible Account's zero-demand row and SHALL include it consistently in row counts, totals, and spooling eligibility. **Assumption A17 remains pending confirmation.**
8. WHERE the approved zero-demand policy omits zero rows, WHEN Total Demand equals `0.00 INR`, THE DemandProcessing_Service SHALL omit the Demand Row and SHALL exclude it consistently from row counts, totals, and spooling eligibility. **Assumption A17 remains pending confirmation.**
9. WHEN the same normalized scope, Processing Cut-off, and Billing Snapshot version are processed more than once, THE DemandProcessing_Service SHALL produce identical persisted component values and SHALL order rows by Account identifier ascending within the Run Revision.

### Requirement 6: Create branch-wise Demand Batches

**User Story:** As a Branch Operations User, I want generated demand grouped by Branch, so that each branch receives only its own accounts and totals.

#### Acceptance Criteria

1. WHEN Demand Processing has valid completed Demand Rows, THE DemandProcessing_Service SHALL create one logical Demand Batch for each represented Branch, where a represented Branch has at least one included Demand Row.
2. WHERE Assumption A10 confirms retention of empty batches, WHEN an in-scope Branch has no included Demand Row, THE DemandProcessing_Service SHALL create one empty logical Demand Batch for that Branch.
3. WHERE Assumption A10 confirms an explicit no-demand outcome instead of empty batches, WHEN an in-scope Branch has no included Demand Row, THE DemandProcessing_Service SHALL create no empty batch and SHALL retain the no-demand outcome for Output and Spooling.
4. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL include only rows whose Branch, LOB, Frequency, Month/Year, Run identifier, and Run Revision equal the batch dimensions.
5. WHEN more than one Branch is represented, THE DemandProcessing_Service SHALL place each Demand Row in exactly one batch whose Branch equals the row Branch.
6. WHEN a Demand Batch is persisted, THE DemandProcessing_Service SHALL persist row count and Current Due, Arrears, ODI, Other Charges, Total Charges, and Total Demand totals rounded to the approved INR precision.
7. WHEN Demand Batches are generated for a Run Revision, THE DemandProcessing_Service SHALL derive each batch identity from the Run Revision and Branch identity and SHALL order batches by Branch identifier ascending.
8. IF a persisted batch total differs from the sum of its persisted rows at approved INR precision, THEN THE DemandProcessing_Service SHALL mark the batch `Failed`, SHALL retain the discrepancy diagnostics, and SHALL block the batch from `ReadyToSpool`.
9. IF a row dimension does not equal its batch dimension, THEN THE DemandProcessing_Service SHALL mark the batch `Failed`, SHALL retain the dimension mismatch, and SHALL block the batch from spooling.

### Requirement 7: Trigger automatic processing and support controlled manual initiation

**User Story:** As a platform operator, I want Automizer to start the ordered Demand Run and continue to Demand Generation after the internal Billing stage succeeds, so that the monthly or configured workflow does not depend on a user remembering to start the MIS.

#### Acceptance Criteria

1. WHEN Automizer emits the approved Demand Run trigger, THE Automizer SHALL provide the LOB, Branch scope, Frequency, Month/Year, Automizer identity, and correlation identifier to the DemandProcessing_Service.
2. WHEN DemandProcessing_Service receives a valid Automizer trigger, THE DemandProcessing_Service SHALL create one asynchronous Demand Run for the Logical Scope Key and SHALL return its Run identifier and initial status.
3. WHEN DemandProcessing_Service receives an equivalent Automizer trigger for an active or completed Logical Scope Key, THE DemandProcessing_Service SHALL return the existing Run identifier and status and SHALL not create a second logical Run.
4. IF Automizer requests Demand Generation while Billing is incomplete, THEN THE DemandProcessing_Service SHALL return `BillingIncomplete`, SHALL keep Demand Rows and the Demand Snapshot unpublished, and SHALL provide a machine-readable retry/defer reason.
5. WHERE Assumption A12 confirms manual initiation, WHEN an authorized Operations User submits a manual Demand Run, THE DemandProcessing_Service SHALL apply the same scope, Billing, eligibility, calculation, batching, authorization, audit, and idempotency rules as an Automizer trigger.
6. WHERE Assumption A12 does not confirm manual initiation, WHEN a user submits a manual Demand Run, THE DemandProcessing_Service SHALL reject the request as unauthorized and SHALL create no processable Run.
7. WHEN an equivalent Automizer trigger is retried after a timeout or lost response, THE DemandProcessing_Service SHALL resolve it by Logical Scope Key and idempotency key and SHALL not create duplicate Demand Rows or Demand Batches.
8. WHEN a Demand Batch reaches its completed spool-eligible status, THE DemandProcessing_Service SHALL make the persisted batch available to Demand Spooling without recalculating or replacing any Demand Row. The delivery and acknowledgement policy remains **Assumption A11 for confirmation**.

### Requirement 8: Maintain period history and idempotent reruns

**User Story:** As a Financial Controller, I want Demand history retained by Month/Year and repeated triggers handled safely, so that prior periods remain auditable and retries do not duplicate demand.

#### Acceptance Criteria

1. WHEN a Demand Run completes, THE DemandProcessing_Service SHALL retain the Run identifier, LOB, Branch scope, Frequency, Month/Year, Billing reference, Processing Cut-off, Billing Snapshot version, Run Revision, Demand Rows, Demand Batches, totals, status, source, actor, and UTC lifecycle timestamps.
2. WHEN an authorized caller requests history for a Month/Year, THE DemandProcessing_Service SHALL return only Runs and Run Revisions within the caller's permitted scope and SHALL order them by Month/Year, completion timestamp, Run Revision, and Run identifier.
3. WHEN a request has the same Logical Scope Key and Billing reference as a completed Run, THE DemandProcessing_Service SHALL return the existing completed result and SHALL create no duplicate logical Demand Row or Demand Batch.
4. IF an idempotency key is reused with a different normalized scope, Month/Year, Billing reference, or immutable request payload, THEN THE DemandProcessing_Service SHALL reject the request as `IdempotencyConflict`, SHALL preserve the original Run, and SHALL retain the conflict record.
5. WHEN an authorized rerun is requested for a failed or stale Run, THE DemandProcessing_Service SHALL retain the original Run and SHALL create one separately identified Run Revision linked to the superseded Run. The replacement-versus-append detail remains **Assumption A13 for confirmation**.
6. IF an authorized rerun is requested after successful spooling, THEN THE DemandProcessing_Service SHALL not silently replace delivered output and SHALL retain links between the delivered result and the rerun. The post-spooling rerun outcome remains **Assumption A13 for confirmation**.
7. WHEN an unchanged successful result is requested without an explicit rerun instruction, THE DemandProcessing_Service SHALL return the existing result and SHALL not start a new calculation.
8. WHERE the approved retention or archival date is reached, THE DemandProcessing_Service SHALL apply the approved retention action and SHALL preserve the required financial and operational history until that date. Retention duration, archival mechanism, and purge authority remain **Assumption A19 for confirmation**.

### Requirement 9: Enforce concurrency and atomic result publication

**User Story:** As a platform operator, I want concurrent triggers and failures handled without duplicate or partially published demand, so that Demand history remains trustworthy.

#### Acceptance Criteria

1. WHEN equivalent Demand Run requests arrive concurrently, THE DemandProcessing_Service SHALL designate one authoritative Run for the Logical Scope Key and SHALL return that Run identifier to every equivalent request.
2. THE DemandProcessing_Service SHALL preserve one authoritative active Run Revision for each Logical Scope Key across process restarts and SHALL resolve every duplicate creation attempt to that existing Run or Run Revision without creating a second authoritative result. This is **Assumption A14 for confirmation**.
3. WHEN a Run is published as `Completed`, THE DemandProcessing_Service SHALL expose the complete Billing Period Results, successful Billing Snapshot, complete Demand Rows, complete Demand Batches, aggregate totals, and Demand Snapshot reference only after the internal Billing Stage/Run and required journal checks have succeeded.
4. IF a calculation loses its processing lease, terminates, or fails before publication, THEN THE DemandProcessing_Service SHALL keep the Run out of `Completed`, SHALL hide incomplete rows and batches from completed output, SHALL retain the interruption diagnostic, and SHALL mark the Run eligible for the approved recovery or rerun path.
5. IF one Branch Batch fails while another Branch Batch succeeds, THEN THE DemandProcessing_Service SHALL preserve the successful batch, SHALL keep the failed batch unavailable for spooling, and SHALL apply the branch-failure result defined by **Assumption A16** to the overall Run.
6. WHERE Assumption A16 confirms branch isolation, WHEN at least one Branch Batch fails and at least one Branch Batch succeeds, THE DemandProcessing_Service SHALL mark the overall Run `PartiallyFailed` and SHALL allow spooling only for successful reconciled batches.
7. WHERE Assumption A16 confirms all-or-nothing processing, WHEN any Branch Batch fails, THE DemandProcessing_Service SHALL keep every batch unavailable for spooling and SHALL mark the overall Run failed.

### Requirement 10: Handle failures and expose reconciliation information

**User Story:** As an Operations User, I want failures to be visible and recoverable, so that incomplete source data or downstream outages do not appear as valid demand.

#### Acceptance Criteria

1. IF a required Billing, Account, Customer, Branch, LOB, or charge dependency is unavailable, THEN THE DemandProcessing_Service SHALL classify the affected outcome as `Dependency`, SHALL retain the source and scope, and SHALL prevent the affected output from becoming ready to spool.
2. IF a dependency operation exceeds the configured timeout or returns a temporary failure, THEN THE DemandProcessing_Service SHALL classify the outcome as `Transient`, SHALL retain Run state, correlation identifier, source reference, and diagnostics, and SHALL permit only the configured retry or recovery path.
3. IF input validation or amount validation prevents processing, THEN THE DemandProcessing_Service SHALL classify the outcome as `Permanent`, SHALL retain the affected scope and component, and SHALL exclude invalid output.
4. IF an Account, Customer, Branch, LOB, or status reference cannot be resolved, THEN THE DemandProcessing_Service SHALL classify the outcome as `DataQuality`, SHALL retain the affected scope and reference type, and SHALL exclude invalid output.
5. IF persisted totals do not reconcile or a Demand Row violates a component or dimension invariant, THEN THE DemandProcessing_Service SHALL classify the outcome as `Reconciliation`, SHALL block `ReadyToSpool`, and SHALL expose the discrepancy in Run and Batch diagnostics.
6. WHEN a Run is failed, deferred, partially failed, or conflicted, THE DemandProcessing_Service SHALL expose one machine-readable status and one failure-class value from the approved vocabulary for Billing-incomplete, dependency, transient, permanent, data-quality, reconciliation, authorization, idempotency, calculation, and spooling outcomes.
7. THE DemandProcessing_Service SHALL redact credentials, bearer tokens, connection strings, and customer fields not required by the diagnostic contract from diagnostics, logs, audit entries, and UI notifications.
8. WHEN a controlled retry or re-spool is authorized, THE DemandProcessing_Service SHALL retain the original source snapshot, output, and failure history, SHALL create one new attempt record, and SHALL not duplicate logical rows or overwrite prior history. Retry limits and delivery acknowledgement remain **Assumptions A11 and A29 for confirmation**.

### Requirement 11: Provide Demand Run screen

**User Story:** As an Operations User, I want a Demand Run screen with the required inputs and clear processing status, so that I can start or inspect a period demand run.

#### Acceptance Criteria

1. WHEN an unauthorized user navigates to the Demand Run route, THE DemandProcessing_UI SHALL render an access-denied state and SHALL render no Demand Run controls or demand data.
2. WHEN an authorized user opens the Demand Run screen, THE DemandProcessing_UI SHALL display LOB, Branch, Frequency, and Month/Year controls limited to the user's permitted scope.
3. WHEN the user submits valid values, THE DemandProcessing_UI SHALL send the normalized values through the shared axios service with the JWT Bearer token and SHALL display the returned Run identifier and status.
4. IF a required field is missing or malformed, THEN THE DemandProcessing_UI SHALL display one field-level English error, SHALL block submission, and SHALL preserve the entered values.
5. IF the service reports `BillingIncomplete`, THEN THE DemandProcessing_UI SHALL display the distinct BillingIncomplete state and SHALL expose only the approved refresh or retry action.
6. WHILE a Run is processing, THE DemandProcessing_UI SHALL display the Run identifier, period, scope, Billing reference when available, and one refresh or polling control using the configured polling interval.
7. IF the service reports rejection, failure, conflict, timeout, or authorization failure, THEN THE DemandProcessing_UI SHALL display the mapped English state and SHALL preserve the submitted values.
8. WHERE manual initiation or rerun is permitted for the authenticated user, THE DemandProcessing_UI SHALL enable the action only after the permission check and required confirmation. Manual and rerun permissions remain **Assumptions A12, A20, and A22 for confirmation**.
9. WHERE manual initiation or rerun is not permitted for the authenticated user, THE DemandProcessing_UI SHALL hide or disable the action. Manual and rerun permissions remain **Assumptions A12, A20, and A22 for confirmation**.
10. THE DemandProcessing_UI SHALL use the shared axios instance for JWT propagation and centralized 401, 403, 409, timeout, and error-toast behavior and SHALL not create an ad-hoc HTTP client.

### Requirement 12: Provide Demand Generation Output screen

**User Story:** As an Operations User, I want to inspect account-level demand output, so that I can reconcile customer dues and arrears for a branch and period.

#### Acceptance Criteria

1. WHEN an unauthorized user requests Demand Generation Output, THE DemandProcessing_UI SHALL render an access-denied state and THE DemandProcessing_Service SHALL return no Demand Row or aggregate demand data.
2. WHEN an authorized user opens output for a Run, THE DemandProcessing_UI SHALL display Account, Customer, Branch, LOB, Month/Year, Frequency, Current Due, Arrears, ODI, Other Charges, Total Charges, Total Demand, row status, Run identifier, and Billing Snapshot version.
3. WHEN a Demand Row is displayed, THE DemandProcessing_UI SHALL display the persisted component values and SHALL make `Current Due + Arrears + Total Charges = Total Demand` verifiable at approved INR precision.
4. WHEN filters for Run, Month/Year, LOB, Branch, Account, Customer, or row status are supplied, THE DemandProcessing_UI SHALL send every supplied filter through the API contract and SHALL display only records matching all supplied filters.
5. WHEN output spans multiple pages, THE DemandProcessing_UI SHALL use the configured page-size bounds and SHALL preserve deterministic Account-identifier ordering across page changes.
6. WHEN no rows match an authorized query, THE DemandProcessing_UI SHALL display an English empty state distinct from loading, failed, timed-out, Billing-incomplete, and still-processing states.
7. IF an output refresh fails or times out, THEN THE DemandProcessing_UI SHALL display a retryable English error state and SHALL preserve the last successfully displayed data.
8. WHERE export or print is authorized by policy, THE DemandProcessing_UI SHALL expose only permitted rows and columns through the approved API contract. Export and print remain **Assumption A22 for confirmation**.
9. WHERE export or print is not authorized by policy, THE DemandProcessing_UI SHALL expose neither export nor print actions. Export and print remain **Assumption A22 for confirmation**.

### Requirement 13: Provide Demand Spooling screen and branch actions

**User Story:** As a Branch Operations User, I want to see branch-wise Demand Batches and spool approved batches, so that each branch receives its generated demand without recalculation or cross-branch leakage.

#### Acceptance Criteria

1. WHEN an authorized user opens Demand Spooling, THE DemandProcessing_UI SHALL request and display only Branch Batches within the user's permitted Branch and LOB scope.
2. WHEN a Branch Batch is displayed, THE DemandProcessing_UI SHALL display Branch, LOB, Frequency, Month/Year, Run identifier, Billing Snapshot version, row count, component totals, Total Demand, batch status, last spool timestamp, source/actor, and any failure class.
3. IF a Branch Batch is not in the completed reconciled status, THEN THE DemandProcessing_UI SHALL disable the spool action and SHALL display the batch's machine-readable failure or readiness reason.
4. WHEN an authorized user requests spooling for an eligible Branch Batch, THE DemandProcessing_UI SHALL send the batch identity through the shared service with the JWT Bearer token and SHALL display the returned spool status and correlation reference.
5. WHEN the same logical Branch Batch is submitted for spooling repeatedly, THE DemandProcessing_Service SHALL resolve the existing logical spool result and SHALL not deliver duplicate logical demand to the downstream consumer.
6. IF a spool attempt fails, THEN THE DemandProcessing_Service SHALL retain the batch and original output, SHALL record one failure-class and attempt-history entry, and SHALL expose only the approved retry or re-spool action without recalculating Demand Rows.
7. WHEN a Branch Batch is successfully spooled, THE DemandProcessing_Service SHALL persist success status, UTC timestamp, actor or source, and the downstream acknowledgement or an explicit acknowledgement-missing status.
8. WHEN a Branch Batch is spooled, THE DemandProcessing_Service SHALL use the completed persisted batch and SHALL not recalculate or replace its Demand Rows. Delivery mechanism, acknowledgement, and retry limits remain **Assumption A11 for confirmation**.

### Requirement 14: Enforce authentication, authorization, and data scope

**User Story:** As a security stakeholder, I want Demand Processing protected by least-privilege access and branch scope, so that financial demand data and operational actions are restricted.

#### Acceptance Criteria

1. IF a request contains a missing, malformed, expired, or signature-invalid JWT Bearer token, THEN THE DemandProcessing_Service SHALL return HTTP 401, SHALL disclose no demand data, and SHALL perform no processing, spooling, or processable-state change.
2. IF a request contains a valid authenticated identity without permission for the requested operation or LOB/Branch scope, THEN THE DemandProcessing_Service SHALL return HTTP 403, SHALL disclose no demand data, and SHALL perform no processing, spooling, or processable-state change.
3. WHEN an authenticated caller requests Run creation or rerun, THE DemandProcessing_Service SHALL evaluate the run/rerun permission independently from output-read and spooling permissions.
4. WHEN an authenticated caller requests output or history, THE DemandProcessing_Service SHALL return only records within the caller's permitted LOB and Branch data scope.
5. WHEN an authenticated caller requests spooling or re-spooling, THE DemandProcessing_Service SHALL validate the requested Branch against the caller's permitted Branch scope before changing batch or spool state.
6. WHEN Automizer invokes Demand Processing, THE DemandProcessing_Service SHALL authenticate and authorize the Automizer identity for the requested LOB and Branch scope and SHALL retain the Automizer identity, source, and correlation data.
7. WHEN a request lacks authentication and authorization, THE DemandProcessing_Service SHALL apply authentication first and SHALL return HTTP 401.
8. THE DemandProcessing_UI SHALL use the shared axios service for JWT propagation and centralized 401, 403, 409, timeout, and error handling and SHALL not create a second HTTP client.
9. WHERE Assumption A20 confirms separate permissions, THE DemandProcessing_Service SHALL enforce distinct permissions for run/rerun, output/history read, and spool/re-spool. Exact policy names remain **Assumption A20 for confirmation**.

### Requirement 15: Record audit history and operational traceability

**User Story:** As a Financial Controller, I want every demand lifecycle action traceable, so that period results and branch delivery can be investigated.

#### Acceptance Criteria

1. WHEN a Demand Run request is accepted or rejected, THE DemandProcessing_Service SHALL append an immutable audit entry containing request identifier, Logical Scope Key when available, scope, period, actor, source, correlation identifier, decision or status, reason or failure class, and UTC timestamp.
2. WHEN a Run status changes, THE DemandProcessing_Service SHALL append an immutable audit entry containing Run identifier, prior status, new status, reason, failure class when applicable, Billing reference or Billing Snapshot version, actor/source, correlation identifier, and UTC timestamp.
3. WHEN a Demand Batch status changes, THE DemandProcessing_Service SHALL append an immutable audit entry containing Run identifier, Run Revision, Batch identifier, Branch, prior state, new state, reason or failure class, actor/source, correlation identifier, and UTC timestamp.
4. WHEN a Spool Attempt occurs, THE DemandProcessing_Service SHALL append an immutable audit entry containing Run identifier, Run Revision, Batch identifier, Branch, attempt number, attempt state, reason or failure class, actor/source, correlation identifier, downstream reference when available, and UTC timestamp.
5. IF an unauthorized caller requests a Run, Demand Row, Demand Batch, or audit record, THEN THE DemandProcessing_Service SHALL apply Requirement 14 authorization behavior and SHALL disclose no demand data.
6. IF an unauthorized caller attempts to mutate or delete a Demand Run, Demand Row, Demand Batch, Spool Attempt, failure record, or audit entry, THEN THE DemandProcessing_Service SHALL reject the request and SHALL preserve the existing record.
7. WHEN a rerun or re-spool occurs, THE DemandProcessing_Service SHALL append new lifecycle records and SHALL preserve all prior output, source references, status transitions, spool attempts, failures, and audit entries.
8. WHERE the approved retention or archival date is reached, THE DemandProcessing_Service SHALL apply the approved financial-record retention action and SHALL preserve required history until expiry. Retention duration and archival/purge authority remain **Assumptions A19 and A21 for confirmation**.
9. THE DemandProcessing_Service SHALL permit security audit entries for rejected or unauthorized requests while creating no processable Run, Demand Row, Demand Batch, or spool state.

### Requirement 16: Ensure deterministic behavior and verifiable correctness

**User Story:** As a development and assurance stakeholder, I want Demand Processing rules to be deterministic and testable, so that financial output can be verified without relying only on end-to-end examples.

#### Acceptance Criteria

1. THE DemandProcessing_Service SHALL calculate Billing Period Results and Demand Rows deterministically so that identical normalized inputs, finalized schedule/version references, allocation inputs, rounding policy, scope, and Billing Snapshot version produce identical values and Account-identifier ordering.
2. THE DemandProcessing_Service SHALL preserve the invariant that each Scheduled Installment equals the finalized Amortization Schedule's scheduled principal plus scheduled interest/EMI component for the selected period.
3. THE DemandProcessing_Service SHALL preserve the allocation invariant that each approved receipt, waiver, reversal, adjustment, and other account-level event is applied once, and that the approved allocation result determines Current Due while prior-period unpaid values determine Arrears.
4. THE DemandProcessing_Service SHALL preserve the component-sum invariants `Total Charges = ODI + Other Charges` and `Total Demand = Current Due + Arrears + Total Charges` at approved INR precision.
5. THE DemandProcessing_Service SHALL preserve the eligibility and scope invariant that every completed Demand Row belongs to the requested LOB and Branch scope, has resolvable Customer, Branch, and LOB references, and has Account status exactly `Active` at the recorded Processing Cut-off.
6. THE DemandProcessing_Service SHALL preserve the batch invariant that every completed Demand Row belongs to exactly one matching logical Demand Batch for its Run Revision and that the batch Branch equals the row Branch.
7. THE DemandProcessing_Service SHALL preserve the retry invariant that retrying the same logical Billing scope and period does not increase logical Billing Period Result, Demand Row, Demand Batch, allocation, or billing-journal counts.
8. THE DemandProcessing_Service SHALL preserve the publication invariant that Demand Snapshot publication and Demand Spooling cannot occur before the internal Billing Stage/Run succeeds, all required Billing Period Results are complete, and all required journal checks succeed.
9. THE DemandProcessing_Service SHALL preserve the idempotency invariant that equivalent accepted requests do not increase logical Demand Row count, logical Demand Batch count, or successful logical spool-delivery count.
10. THE DemandProcessing_Service SHALL provide FsCheck property-based tests with at least 100 generated cases for schedule-driven Billing, Scheduled Installment principal-plus-interest equality, exactly-once allocation and unpaid-amount conservation, approved rounding, zero values, boundary statuses, eligibility, scope, batch isolation, retry idempotency, duplicate triggers, publication gating, and multi-Branch inputs.
11. THE DemandProcessing_Service SHALL provide xUnit example and edge-case tests for Billing preconditions and stage failures, schedule finalization, receipts, waivers, reversals, adjustments, allocation diagnostics, authentication and authorization, Branch scope, failure classification, reconciliation, idempotency, concurrency, reruns, journal checks, Demand Snapshot publication gating, and spooling, including `WebApplicationFactory<Program>` integration tests for host wiring and end-to-end API behavior.
12. THE DemandProcessing_UI SHALL provide Vitest and React Testing Library tests for service selection, shared axios/JWT behavior, form validation, successful and failed initiation, distinct status states, output rendering, deterministic filtering and pagination, spooling action state, unauthorized responses, and timeout behavior.
13. WHERE Assumption A23 confirms the testing policy, THE DemandProcessing_Service SHALL retain the approved property generators, integration examples, performance thresholds, and test-data factories without weakening the invariants in this requirement.

### Requirement 17: Apply India-only English-only product constraints

**User Story:** As a Finnova product stakeholder, I want Demand Processing aligned with the India-only platform, so that the feature is consistent with the product's localization policy.

#### Acceptance Criteria

1. THE DemandProcessing_Service SHALL represent, calculate, persist, compare, and expose monetary amounts in Indian Rupees (INR) using the approved two-decimal precision.
2. THE DemandProcessing_Service and DemandProcessing_UI SHALL use English for all user-facing labels, statuses, validation messages, error messages, seed/reference values, test data, and display values.
3. THE DemandProcessing_Service and DemandProcessing_UI SHALL use one plain display value for each business value and SHALL not add bilingual fields, language-suffixed fields, translation scaffolding, Arabic content, other non-English sample values, or non-English localization behavior.
4. THE DemandProcessing_UI SHALL use the platform's left-to-right layout conventions and SHALL not add RTL direction handling or `dir="rtl"` behavior.
5. WHERE Assumption A3 confirms `Asia/Kolkata`, THE DemandProcessing_Service SHALL derive Month/Year using that business timezone and SHALL persist UTC timestamps for audit, correlation, technical ordering, and lifecycle events.
6. WHERE an approved replacement business timezone is confirmed instead of `Asia/Kolkata`, THE DemandProcessing_Service SHALL derive Month/Year using that replacement timezone and SHALL continue to persist UTC technical and audit timestamps. The timezone decision remains **Assumption A3 for confirmation**.
