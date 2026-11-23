# Requirements Document

## Introduction

FINNOVA-66, **Challan Processing – Instrument Listing, Selection & Pay-In-Slip Generation**, gives an authorized Cashier a controlled workflow for selecting eligible receipts and generating one consolidated Bank Deposit Challan, also called a Pay-In-Slip. The workflow starts from the supplied legacy screen shape: breadcrumb `Home > Collections > Challan Generation`, title `Challan Generation - Create` or `Challan Generation - View`, required fields `Line of Business`, `Branch`, `Date`, `Deposit Bank`, `Receipt Type`, and `Instrument Type`, the `Challan No` field, the `Past Receipts` checkbox, the actions `Challan Listing`, `Challan Generation`, `Print`, `Clear`, and `Cancel`, and the instrument table columns `Sl.No`, `Instrument Date`, `Receipt No`, `Entity/Customer Code`, `Entity/Customer Name`, `Drawee Bank`, `Location`, and `Amount`. The table also includes an explicit `Exclude` action because the business rule requires individual exclusion even though the legacy screenshot does not show that column.

FINNOVA-66 lists only active, eligible receipts owned by FINNOVA-61, rechecks every server-issued receipt ID at commit time, resolves FINNOVA-65 deposit-bank routing before posting, persists immutable routing snapshots, locks selected receipts, and creates one immutable challan. The feature submits one approved JVE Posting Request through the existing FINNOVA-63/JVE .NET boundary. FINNOVA-66 does not create canonical journals and does not write Journal Header or Journal Line tables. The JVE worker validates the request, resolves Cashflow Master and GL rules, validates the Drawee Bank clearing debit account and Cashier credit account, and owns the canonical journal transaction.

The feature is India-specific and English-only. Illustrative values are `LN - Loan`, `Mumbai`, `HDFC Bank`, `SBI`, `Cash`, `Cheque`, `PDC`, `Local`, `Outstation`, `DD`, and INR dates and amounts. Oman, Muscat, Arabic, OMR, non-India values, bilingual fields, translation scaffolding, and RTL behavior are excluded even when they appear in the legacy visual reference.

This document is a planning artifact only. It authorizes no source-code change, migration execution, build, test, deployment, or journal posting during spec delivery.

## Glossary

- **Cashier:** An authenticated Finnova user with the FINNOVA-66 permission and branch/LOB scope to list and generate challans.
- **Receipt:** The FINNOVA-61-owned financial receipt record. FINNOVA-66 references receipt IDs and does not create a parallel Receipt aggregate.
- **Eligible_Receipt:** An active receipt that is not cancelled, not bounced, not previously challaned, and satisfies the selected branch, LOB, date, receipt-type, and instrument-type filters.
- **Undeposited_Pool:** The server-side set of eligible receipts that have not been committed to a challan. An excluded receipt returns to this pool when no other process has changed its eligibility.
- **Instrument_Type:** The receipt instrument classification `Local`, `Outstation`, or `DD`. `All` is a filter option, not a stored instrument type.
- **Receipt_Type:** The receipt classification `Cash`, `Cheque`, or `PDC`. `All` is a filter option, not a stored receipt type.
- **Past_Receipts:** A listing mode in which the server includes eligible receipts created strictly before the selected Challan Date, in addition to the selected date according to the approved date policy.
- **Challan:** The immutable FINNOVA-66 aggregate representing one generated Bank Deposit Challan and its selected receipt membership.
- **Challan_No:** A server-issued unique challan number allocated atomically for the applicable India branch/LOB/date scope.
- **Routing_Snapshot:** The immutable FINNOVA-65 result stored on a selected receipt/challan, including rule-set version, matched Drawee Bank or `OTHERS`, deposit-bank identity, account details, and GL-routing values returned by FINNOVA-65.
- **ChallanPending:** The receipt and challan state used while the committed challan awaits durable acceptance or processing status from JVE. Receipts in this state are locked against receipt edits.
- **Challaned:** The FINNOVA-61-compatible receipt state set when the challan is committed. A Challaned receipt is locked and cannot be selected into another challan.
- **Approved:** The immutable challan status returned only after one approved JVE Posting Request is durably accepted by the existing submission boundary. Approved is not an un-approval operation and is separate from final journal posting.
- **PostingStatus:** The separate JVE outcome projection: `Ready`, `Processing`, `Posted`, `Failed`, or `DeadLetter`.
- **JVE:** The existing JV Posting Engine. JVE owns Cashflow/GL validation, canonical Journal Header/Line writes, final journal status, and durable journal identity.
- **JVE_Posting_Request:** The approved request submitted through the existing .NET JVE boundary. It carries selected receipt IDs, challan identity, context, routing snapshots, amount, and the approved journal rule key.
- **Cashier_Account:** The approved GL credit account resolved by the FINNOVA-63/JVE accounting contract for the authenticated Cashier and branch. FINNOVA-66 does not create or duplicate this GL master.
- **Drawee_Bank_Clearing_Account:** The approved GL debit account resolved from the FINNOVA-65 routing snapshot and validated by JVE.
- **Pay-In-Slip:** The English/India print representation of an immutable Challan, including challan number, date, deposit bank, selected instrument rows, total INR amount, status, and posting status.
- **Server-Issued_Receipt_ID:** The authoritative receipt identifier returned by the backend listing API. The browser may send these IDs back but cannot assign Challaned state.
- **Compatibility_Gate:** A documented pre-implementation check that confirms an existing owner or contract before FINNOVA-66 adds an adapter or extension.
- **Problem_Details:** The existing RFC 7807-style error response with English detail, stable error code, and correlation ID.
- **EARS:** Easy Approach to Requirements Syntax, using ubiquitous, event-driven, state-driven, unwanted-event, optional-feature, and complex patterns.

## Dependencies/Assumptions

- **Dependency D1, FINNOVA-61:** FINNOVA-61 owns Receipt capture, receipt identifiers, receipt amount/date/type/instrument/customer/location fields, cancellation/bounce flags, and the authoritative receipt status vocabulary. FINNOVA-66 consumes that contract and does not create a second Receipt model.
- **Dependency D2, FINNOVA-63:** FINNOVA-63 owns receipt authorization/posting state, Cashier authorization context, JVE submission coordination, and the approved source-to-JVE boundary. FINNOVA-66 adds an adapter only where the existing contract lacks the required challan payload.
- **Dependency D3, FINNOVA-64:** FINNOVA-64 protects Challaned receipts. FINNOVA-66 must call or extend that protection boundary rather than bypassing it.
- **Dependency D4, FINNOVA-65:** FINNOVA-65 owns deposit-bank routing by LOB, Branch/Location, and Drawee Bank. Runtime resolution occurs before the JVE request, and each selected receipt/challan stores an immutable routing snapshot.
- **Dependency D5, JVE:** The existing `JvPostingSubmissionRequest` and `JvPostingSubmissionService` are the approved .NET submission boundary. The Python worker remains the canonical journal owner.
- **Dependency D6, platform data:** Existing LOB, Branch/Location, Deposit Bank, Drawee Bank, Customer/Entity, Cashier, GL, authentication, audit, SQL Server, EF Core, ProblemDetails, and YARP conventions remain authoritative.
- **Assumption A1, host placement:** The compatibility gate identifies the existing host that owns FINNOVA-61/63 receipt processing. The default task paths use `Finnova.Service` for application logic and `Finnova.AccountsService` for the protected controller if no dedicated Collections host owns the contract. The implementation must relocate the controller to the confirmed owner instead of creating a parallel receipt host.
- **Assumption A2, Cash and PDC:** Cash receipts use their receipt amount and may have no Drawee Bank or instrument identifier. PDC receipts use the FINNOVA-61 receipt date, type, and status rules; FINNOVA-66 adds no maturity-date filter until FINNOVA-61 confirms one. Instrument filters apply only to stored instrument classifications; `All` includes every eligible classification.
- **Assumption A3, date boundary:** A normal listing uses receipts eligible on or before the selected Challan Date under the FINNOVA-61 date contract. `Past Receipts` additionally includes eligible receipts created strictly before the selected Challan Date. The confirmed FINNOVA-61 date field and timezone remain the compatibility gate.
- **Assumption A4, Challan Generation behavior:** Clicking `Challan Generation` validates filters and selection, commits the Challan and selected receipt locks, submits the approved JVE request, and returns the committed challan. It is not a draft-only save. A separate `Challan Listing` action remains available for read-only listing/navigation.
- **Assumption A5, status semantics:** The preferred lifecycle is `Draft -> PostingPending -> Approved`; `Approved` is returned only after the JVE request is durably accepted by the .NET submission boundary. `PostingStatus` independently reports `Ready`, `Processing`, `Posted`, `Failed`, or `DeadLetter`. The exact stored enum names are a compatibility gate with FINNOVA-63/JVE.
- **Assumption A6, receipt lock timing:** Selected receipts become `Challaned` and locked in the same transaction that commits the Challan, before the JVE request is submitted. A failed JVE post does not un-challan or unlock them. Controlled operational recovery retries the same immutable request or follows an approved FINNOVA-63 correction process.
- **Assumption A7, JVE event key:** The requested event/rule key is `Collections.ChallanPayInSlip` until FINNOVA-63 and Cashflow Master confirm the canonical key. FINNOVA-66 must not hard-code account mappings behind that key.
- **Assumption A8, GL sources:** FINNOVA-65 supplies the deposit-bank and Drawee Bank routing snapshot. FINNOVA-63/JVE resolves or supplies the approved Cashier GL source. FINNOVA-66 stores the snapshot and submits context but does not administer either GL master.
- **Assumption A9, print timing:** Print is allowed after `Approved`, including when `PostingStatus` is `Ready` or `Processing`, and the Pay-In-Slip clearly displays the current posting status. The final product owner may require `Posted` before printing; if changed, only the print authorization rule changes.
- **Assumption A10, numbering:** Challan numbers are allocated by a transaction-safe server-side number source, preferably the existing Document Number Control contract. No browser-generated number is accepted.
- **Assumption A11, idempotency:** Generation uses a caller/request idempotency key and a deterministic selected-receipt fingerprint. An equivalent retry returns the original challan and JVE request; a key reused for different selection or context returns a conflict.
- **Assumption A12, failure recovery:** JVE failures preserve the Challan, selected receipt locks, routing snapshots, audit history, and request identity. Authorized operations may retry a failed request idempotently. FINNOVA-66 never silently un-challans or creates a replacement journal request.
- **Assumption A13, selection ownership:** The browser sends only server-issued receipt IDs and filter/context data. The server re-queries and rechecks every receipt inside the commit boundary. The browser cannot mark receipts Challaned by itself.
- **Assumption A14, print representation:** The initial print output is a server-generated PDF or approved printable HTML using the immutable challan snapshot. No non-English, Arabic, OMR, Oman, Muscat, RTL, or bilingual content is permitted.
- **Assumption A15, authorization:** Cashier permission and branch/LOB scope are enforced on every read and mutation. SystemAdmin or Operations permissions may inspect or recover failures only through separate existing policies; a Cashier cannot recover or un-challan a failed posting.
- **Assumption A16, migration and contract safety:** Existing specs are planning inputs only. Before source edits, the implementation must confirm entity names, receipt status fields, authorization states, FINNOVA-64 lock hooks, FINNOVA-65 snapshot shape, JVE request schema, and canonical number source. If a required contract is absent, implementation stops at the compatibility gate and records the exact adapter decision.
- **Assumption A17, localization:** All labels, status values, fixture data, validation messages, PDF text, and tests are English and India-specific. No secondary language field or RTL handling is introduced.

## Requirements

### Requirement 1: Render the Challan Generation UI

**User Story:** As a Cashier, I want the Challan Generation screen to match the approved legacy workflow, so that I can recognize the collection process and prepare a Bank Deposit Challan.

#### Acceptance Criteria

1. WHEN a Cashier opens a new challan workflow, THE Challan_Generation_UI SHALL display the breadcrumb `Home > Collections > Challan Generation` and the title `Challan Generation - Create`.
2. WHEN a Cashier opens an existing challan, THE Challan_Generation_UI SHALL display the title `Challan Generation - View` and SHALL populate the saved filter/header values.
3. THE Challan_Generation_UI SHALL display required fields labeled `Line of Business`, `Branch`, `Date`, `Deposit Bank`, `Receipt Type`, and `Instrument Type`, plus `Challan No` and the `Past Receipts` checkbox.
4. THE Challan_Generation_UI SHALL display the actions `Challan Listing`, `Challan Generation`, `Print`, `Clear`, and `Cancel` using English labels.
5. THE Challan_Generation_UI SHALL display a receipt table with columns `Sl.No`, `Instrument Date`, `Receipt No`, `Entity/Customer Code`, `Entity/Customer Name`, `Drawee Bank`, `Location`, `Amount`, and `Exclude`.
6. THE Challan_Generation_UI SHALL display pagination, record count, and page-size controls for a receipt listing response.
7. THE Challan_Generation_UI SHALL use only India-specific English fixture values, including `LN - Loan`, `Mumbai`, `HDFC Bank`, `Cash`, `Cheque`, `PDC`, `Local`, `Outstation`, `DD`, and INR.

### Requirement 2: Load Dependent Filters and Validate Listing Context

**User Story:** As a Cashier, I want dependent LOB, Branch, and Deposit Bank filters, so that the receipt listing uses a valid operational scope.

#### Acceptance Criteria

1. WHEN a Cashier selects a Line of Business, THE Challan_Generation_UI SHALL load active Branch values valid for the selected Line of Business and SHALL clear any previous Branch and Deposit Bank selections.
2. WHEN a Cashier selects a Branch, THE Challan_Generation_UI SHALL load active Deposit Bank values valid for the selected Line of Business and Branch and SHALL clear any previous Deposit Bank selection.
3. WHEN a required parent selection changes, THE Challan_Generation_UI SHALL clear dependent values, receipt rows, selected receipt IDs, excluded receipt IDs, and the displayed total.
4. IF a Cashier submits a listing request with a missing required field, THEN THE Challan_Processing_Service SHALL reject the request with field-specific Problem_Details and SHALL not query or mutate receipt state.
5. WHEN a Cashier submits a valid listing context, THE Challan_Processing_Service SHALL send the selected LOB, Branch, Challan Date, Deposit Bank, Receipt Type, Instrument Type, and Past Receipts flag to the server.
6. THE Challan_Processing_Service SHALL derive the Branch and Deposit Bank options from authoritative active masters and SHALL not accept a browser-supplied display label as an identifier.

### Requirement 3: List Only Eligible Receipts

**User Story:** As a Cashier, I want the listing to show only receipts that can enter a challan, so that cancelled, bounced, or previously deposited instruments cannot be selected.

#### Acceptance Criteria

1. WHEN a listing request is received, THE Challan_Processing_Service SHALL return only receipts that are active, not cancelled, not bounced, and not previously challaned.
2. WHEN a listing request contains a Branch and Line of Business, THE Challan_Processing_Service SHALL return only eligible receipts in the requested operational scope.
3. WHEN a listing request contains `Receipt Type` other than `All`, THE Challan_Processing_Service SHALL return only eligible receipts with the requested `Cash`, `Cheque`, or `PDC` type.
4. WHEN a listing request contains `Instrument Type` other than `All`, THE Challan_Processing_Service SHALL return only eligible receipts with the requested `Local`, `Outstation`, or `DD` type.
5. WHEN a listing request contains `Past Receipts`, THE Challan_Processing_Service SHALL include eligible receipts created before the selected Challan Date according to the FINNOVA-61 date contract.
6. WHEN a listing request contains no eligible receipt, THE Challan_Processing_Service SHALL return an empty page, a zero record count, and no selected receipt IDs.
7. THE Challan_Processing_Service SHALL order listing rows deterministically by Instrument Date, Receipt No, and server-issued Receipt ID before applying pagination.
8. WHEN a Cashier requests a page, THE Challan_Processing_Service SHALL return only the requested page, the total eligible record count, the page number, and the page size.

### Requirement 4: Select, Exclude, and Calculate the Challan Total

**User Story:** As a Cashier, I want to select eligible instruments and exclude individual rows, so that the generated challan contains exactly the physical instruments sent to clearing.

#### Acceptance Criteria

1. WHEN a Cashier selects a listing row, THE Challan_Generation_UI SHALL add that row's server-issued Receipt ID to the pending selection and SHALL include its INR amount in the displayed total.
2. WHEN a Cashier excludes an eligible row, THE Challan_Generation_UI SHALL remove that row from the pending selection, SHALL mark the row excluded in the current listing, and SHALL not include its amount in the displayed total.
3. WHEN a Cashier clears an exclusion before generation, THE Challan_Generation_UI SHALL make the row selectable again only after the server confirms that the row remains eligible.
4. FOR ALL selected receipt sets, THE Challan_Processing_Service SHALL calculate the challan total as the sum of selected server-side amounts rounded to two INR decimal places and SHALL ignore excluded rows.
5. IF a generation request contains no selected Receipt ID, THEN THE Challan_Processing_Service SHALL reject the request with an English validation error and SHALL leave all receipts unchallaned.
6. THE Challan_Generation_UI SHALL submit server-issued Receipt IDs and SHALL not submit client-calculated amount, status, routing, or Challaned flags as authoritative values.
7. WHEN a receipt is excluded from a pending selection, THE Challan_Processing_Service SHALL leave the receipt in the Undeposited_Pool unless another committed transaction has changed its eligibility.

### Requirement 5: Commit a Unique Challan and Lock Receipts Atomically

**User Story:** As a Cashier, I want one atomic generation operation, so that the challan, selected receipt membership, and receipt locks cannot become inconsistent.

#### Acceptance Criteria

1. WHEN a valid generation request is received, THE Challan_Processing_Service SHALL re-query every submitted Server-Issued_Receipt_ID and SHALL recheck eligibility inside the commit transaction.
2. WHEN all submitted receipt IDs remain eligible, THE Challan_Processing_Service SHALL allocate a unique Challan_No from the server-side number source and SHALL create one immutable Challan with the selected receipt IDs and server-derived total.
3. WHEN the Challan transaction commits, THE Challan_Processing_Service SHALL set every selected receipt to Challaned and SHALL lock every selected receipt against edits and reuse.
4. WHEN the Challan transaction commits, THE Challan_Processing_Service SHALL persist the selected receipt count, total INR amount, LOB, Branch, Challan Date, Deposit Bank identity, and cashier audit identity.
5. IF any submitted receipt ID is missing, ineligible, already locked, or changed since listing, THEN THE Challan_Processing_Service SHALL roll back the complete generation transaction and SHALL return a conflict identifying the affected receipt IDs.
6. IF concurrent generation requests contain the same receipt ID, THEN THE Challan_Processing_Service SHALL commit at most one request and SHALL reject the other request without creating a second Challan.
7. WHEN a Challan is created, THE Challan_Processing_Service SHALL make the Challan_No immutable and SHALL not accept a browser-provided Challan_No as the authoritative number.
8. THE Challan_Processing_Service SHALL not allow the browser to mark a receipt Challaned by itself; only the server transaction may write the Challaned state.

### Requirement 6: Resolve and Persist FINNOVA-65 Routing Snapshots

**User Story:** As a Cashier, I want each selected instrument routed to the correct deposit-bank clearing account, so that the physical deposit and accounting request use the same approved routing decision.

#### Acceptance Criteria

1. WHEN generation validates selected receipts, THE Challan_Processing_Service SHALL resolve one active FINNOVA-65 routing result for each selected receipt using LOB, Branch, Deposit Bank, Drawee Bank, and Challan Date.
2. WHEN FINNOVA-65 resolves a receipt, THE Challan_Processing_Service SHALL persist an immutable Routing_Snapshot containing rule-set ID/version, matched Drawee Bank or `OTHERS`, deposit-bank identity/account snapshot, clearing GL routing values, and resolution timestamp.
3. IF FINNOVA-65 returns no unique active routing result for a selected receipt, THEN THE Challan_Processing_Service SHALL reject generation, SHALL not create a Challan, and SHALL not submit a JVE_Posting_Request.
4. WHEN a Routing_Snapshot is persisted, THE Challan_Processing_Service SHALL use that snapshot in the JVE_Posting_Request and SHALL not resolve FINNOVA-65 again after the JVE request is accepted.
5. WHILE a Challan or receipt has a persisted Routing_Snapshot, THE Challan_Processing_Service SHALL preserve the snapshot even if FINNOVA-65 configuration later changes.

### Requirement 7: Submit the Approved JVE Posting Request

**User Story:** As a Financial Controller, I want challan custody transfer to use the approved JVE boundary, so that FINNOVA-66 cannot create an unvalidated or duplicate canonical journal.

#### Acceptance Criteria

1. WHEN a Challan commits successfully, THE Challan_Processing_Service SHALL create exactly one approved JVE_Posting_Request through the existing .NET submission boundary.
2. THE JVE_Posting_Request SHALL carry selected receipt IDs, Challan ID, Challan_No, LOB, Branch, Challan Date, Deposit Bank and Routing_Snapshot values, total INR amount, Cashier actor context, and the approved event or rule key.
3. THE JVE_Posting_Request SHALL express the accounting intent `Dr Drawee Bank Clearing / Cr Cashier Account` without embedding hard-coded GL account mappings.
4. WHEN JVE accepts the request, THE Challan_Processing_Service SHALL store the JVE request identity, current PostingStatus, and correlation ID on the Challan and SHALL expose the status to authorized clients.
5. THE JVE worker SHALL resolve Cashflow Master and GL rules, validate the Drawee_Bank_Clearing_Account and Cashier_Account, and write the canonical journal atomically; FINNOVA-66 SHALL not write Journal Header or Journal Line tables.
6. THE JVE worker SHALL return a durable journal identity and status when posting completes, and THE Challan_Processing_Service SHALL expose that identity and status without becoming a second journal owner.
7. WHEN the JVE submission boundary is unavailable, THE Challan_Processing_Service SHALL preserve the committed Challan and receipt locks, SHALL expose a submission failure status, and SHALL make the request eligible for controlled idempotent recovery.

### Requirement 8: Enforce Lifecycle, Failure, Retry, and Idempotency Semantics

**User Story:** As an Operations User, I want visible and recoverable challan and posting states, so that failures do not silently change custody or create duplicate accounting.

#### Acceptance Criteria

1. WHEN a generation transaction begins, THE Challan_Processing_Service SHALL represent the new Challan as Draft or the equivalent pre-commit state and SHALL not expose it as Approved before the transaction and JVE acceptance succeed.
2. WHEN receipt locks and the JVE request are durably accepted, THE Challan_Processing_Service SHALL transition the Challan to Approved and SHALL never transition an Approved Challan back to Draft or an unapproved state.
3. WHILE a Challan or receipt is ChallanPending or Approved, THE Challan_Processing_Service SHALL reject receipt edits, cancellation, reuse, and direct status changes except through the approved FINNOVA-64/operations recovery boundary.
4. WHEN the JVE worker reports `Ready`, `Processing`, `Posted`, `Failed`, or `DeadLetter`, THE Challan_Processing_Service SHALL expose that PostingStatus separately from the immutable Challan status.
5. IF JVE posting fails after Challan commit, THEN THE Challan_Processing_Service SHALL retain the Challan, Challaned receipt states, routing snapshots, audit trail, and JVE request identity, and SHALL not silently un-challan.
6. WHEN an authorized recovery retries the same JVE request, THE Challan_Processing_Service SHALL reuse the original request identity and SHALL not create a second Challan or canonical journal.
7. IF a generation request reuses an Idempotency_Key with equivalent filters, selected Receipt IDs, and immutable context, THEN THE Challan_Processing_Service SHALL return the original committed result.
8. IF a generation request reuses an Idempotency_Key with different selected Receipt IDs, filters, amount context, or actor scope, THEN THE Challan_Processing_Service SHALL reject the request and SHALL preserve the original result.
9. THE Challan_Processing_Service SHALL define `Approved` as durable acceptance of the JVE request rather than final `Posted` journal completion, unless FINNOVA-63 confirms a compatible final-posting contract before implementation.

### Requirement 9: Generate and Print the Pay-In-Slip

**User Story:** As a Cashier, I want to print the consolidated Bank Deposit Challan, so that the physical instruments and deposit details accompany the bank submission.

#### Acceptance Criteria

1. WHEN an authorized client requests a Pay-In-Slip for an Approved Challan, THE Challan_Processing_Service SHALL return a server-generated printable representation using the immutable Challan and Routing_Snapshot data.
2. THE Pay-In-Slip SHALL display Challan_No, Challan Date, LOB, Branch, Deposit Bank, receipt rows, selected total in INR, Challan status, and PostingStatus.
3. WHERE the approved print policy permits printing before final JVE posting, THE Challan_Processing_Service SHALL allow printing while PostingStatus is Ready or Processing and SHALL display that the journal is not yet Posted.
4. IF a client requests print for a missing, unauthorized, or unapproved Challan, THEN THE Challan_Processing_Service SHALL return a safe Problem_Details response and SHALL not generate a document.
5. THE Pay-In-Slip SHALL use English-only India-specific labels, dates, bank values, and INR formatting and SHALL not include Oman, Muscat, Arabic, OMR, or RTL content.

### Requirement 10: Authorize, Audit, and Protect Data

**User Story:** As a security owner, I want Cashier actions and accounting-boundary calls protected and traceable, so that receipt custody cannot be changed by an unauthorized client.

#### Acceptance Criteria

1. IF a request has a missing, expired, malformed, or invalid JWT, THEN THE Challan_Processing_Service SHALL return HTTP 401 and SHALL disclose no receipt or challan data.
2. IF an authenticated user lacks the Cashier permission or branch/LOB scope, THEN THE Challan_Processing_Service SHALL return HTTP 403 and SHALL disclose no out-of-scope receipt or challan data.
3. THE Challan_Processing_Service SHALL derive the actor, branch scope, correlation ID, and audit timestamp from trusted server context and SHALL ignore forged browser actor fields.
4. THE Challan_Processing_Service SHALL redact bearer tokens, credentials, full account secrets, and unnecessary personal data from logs, Problem_Details, audit payloads, and print output.
5. WHEN a listing, exclusion, generation, print, JVE acceptance, JVE status change, or recovery event occurs, THE Challan_Processing_Service SHALL append an immutable audit record containing event type, actor/service identity, challan or receipt references, correlation ID, and UTC timestamp.
6. THE Challan_Processing_Service SHALL expose only the minimum receipt/customer fields needed by the screen and SHALL not expose unrelated personal or financial data.

### Requirement 11: Expose Stable Backend and Frontend Contracts

**User Story:** As the UI and downstream accounting boundary, I want stable contracts, so that listing, generation, printing, and status monitoring can evolve without duplicating ownership.

#### Acceptance Criteria

1. THE Challan_Processing_Service SHALL expose authenticated operations for listing eligible receipts, excluding or restoring a pending row, generating a Challan, retrieving Challan detail/status, and printing a Pay-In-Slip.
2. THE listing response SHALL contain server-issued Receipt IDs, row display values, server amounts, page metadata, and eligibility context needed by the UI.
3. THE generation request SHALL contain filter context, Challan Date, server-issued selected Receipt IDs, an Idempotency_Key, and no authoritative client amount, routing, status, or Challan_No.
4. THE generation response SHALL contain Challan ID, immutable Challan_No, selected count, server total, Challan status, JVE request ID, PostingStatus, and correlation ID.
5. THE service interface SHALL have real and mock implementations selected by `VITE_USE_MOCK_API` and shall use the shared axios instance for real calls.
6. THE frontend SHALL use React hooks and MUI components and SHALL not introduce Redux, RxJS, ad-hoc axios clients, bilingual fields, or RTL support.

### Requirement 12: Verify the Acceptance Scenario and Feature Boundaries

**User Story:** As the delivery team, I want the complete business scenario tested, so that selection, exclusion, locking, and accounting submission are verifiable without writing a second journal owner.

#### Acceptance Criteria

1. THE backend unit suite SHALL test eligibility, date/past-receipt filtering, pagination, selection total, status transitions, idempotency, and failure classification with xUnit examples and FsCheck.Xunit properties of at least 100 iterations for each declared property.
2. THE backend integration suite SHALL use `WebApplicationFactory<Program>` to test Cashier authorization, API contracts, transaction rollback, concurrent selection conflict, FINNOVA-65 routing snapshot, JVE submission, and no direct canonical journal write.
3. THE frontend Vitest/React Testing Library suite SHALL test the exact screenshot labels, dependent lookups, listing/pagination, selection, Exclude behavior, total, Clear, Cancel, Print, error preservation, and mock/real service toggle.
4. WHEN the integration suite creates three authorized eligible receipts, excludes one, and generates a Challan, THE test SHALL verify a unique Challan_No, a total equal to the two selected server amounts, two Challaned locked receipts, one excluded unchallaned receipt, one immutable routing snapshot per selected receipt, and one approved JVE_Posting_Request.
5. THE integration suite SHALL verify that JVE, not FINNOVA-66, owns canonical Journal Header and Journal Line creation and returns the durable journal identity/status through the approved boundary.
6. THE implementation SHALL preserve FINNOVA-61 Receipt ownership, FINNOVA-63 authorization/JVE ownership, FINNOVA-64 receipt protection, FINNOVA-65 routing ownership, and JVE canonical journal ownership.
