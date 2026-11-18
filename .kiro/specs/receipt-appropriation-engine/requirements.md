# Requirements Document

## Introduction

The Receipt Appropriation Engine feature (Jira FINNOVA-62, “Receipt Processing – Receipt Appropriation Engine (Manual vs. Auto Suggest)”) lets a Collection Officer allocate a receipt amount against a customer’s outstanding dues. The feature supports two modes: Manual, in which the officer edits allocations directly, and Suggest, in which the system applies the active Appropriation Logic Master rule for the applicable Line of Business (LOB) and Company.

The engine works with the existing Receipt Processing feature (FINNOVA-61). The receipt document supplies the customer, Company, LOB, receipt document amount, and receipt identifier. The engine retrieves pending dues for the customer, displays them in the required business order, calculates or accepts allocations, transfers an approved residual to the customer’s Advance Account, and confirms the result atomically. This specification covers both the backend API in `Finnova-API` and the React + TypeScript user interface in `Finnova-UI`.

All monetary values are Indian Rupees (INR) with two fractional digits. All labels, values, validation messages, lookup values, and test examples are English-only. The feature does not add bilingual fields, Arabic or other non-English content, RTL rendering, or translation infrastructure.

The screenshot supplied with the Jira story is treated as supporting context for the Appropriation Logic configuration shape. It indicates fields such as Line of Business, Type, Full Due, Active, and ordered receipt-type percentage details. The screenshot does not define the complete rule-maintenance scope or override the explicit decisions recorded below.

## Glossary

- **Receipt**: An existing receipt document containing a customer, Company, LOB, and Receipt Document Amount against which the engine may allocate funds.
- **Receipt Document Amount**: The total INR amount available for appropriation on a Receipt.
- **Receipt Appropriation**: The persisted set of allocations and any Advance Account residual associated with a Receipt.
- **Appropriation Engine**: The backend service that retrieves dues, validates manual allocations, calculates Suggest allocations, and confirms Receipt Appropriation.
- **Appropriation_UI**: The Finnova-UI screen or dialog that displays pending dues and lets an authorized Collection Officer choose Manual or Suggest behavior.
- **Collection Officer**: An authenticated user authorized to operate receipt appropriation. The existing `BranchCollectionOfficer` role is assumed to represent this business role unless authorization review confirms another role mapping.
- **Company**: The legal or accounting company identified by the Receipt and used to select the applicable Appropriation Logic Master rule and Advance Account.
- **Line of Business (LOB)**: The active business line identified by the Receipt and used to select the applicable Appropriation Logic Master rule.
- **Contract**: A customer account, identified by Account Number, against which one or more dues are outstanding.
- **Account Number**: The customer contract or loan account number displayed in the due list.
- **Pending Due**: An outstanding cash-flow due belonging to the selected customer and eligible for appropriation, with an Outstanding Amount greater than zero and a due status that permits collection.
- **Cash Flow Due Type**: The business category of a Pending Due. Supported ordering categories are Installment, Principal, Interest, Insurance, ODI, and Other Charges.
- **ODI**: The overdue-interest category represented by the existing cash-flow due model.
- **Outstanding Amount**: The unpaid INR amount remaining on a Pending Due at the time the engine reads or confirms it.
- **Allocated Amount**: The INR amount assigned from a Receipt to one Pending Due.
- **Manual Mode**: An appropriation mode in which the Collection Officer enters or edits Allocated Amount values.
- **Suggest Mode**: An appropriation mode in which the Appropriation Engine calculates Allocated Amount values from an active rule.
- **Appropriation Logic Master**: The existing or separately maintained configuration source containing active appropriation rules for a Company and LOB.
- **Appropriation Rule**: One applicable configuration in the Appropriation Logic Master containing an appropriation direction, a Full Due setting, and ordered receipt-type percentage details.
- **Receipt-Type Rule Detail**: An ordered rule detail pairing a Cash Flow Due Type with a percentage of the Receipt Document Amount.
- **Appropriation Direction**: The rule setting that controls iteration order. `Vertical` processes each Contract through the configured due-type sequence before moving to the next Contract. `Horizontal` processes each configured due type across the customer’s Contracts before moving to the next due type.
- **Full Due**: A rule setting that indicates whether the engine should satisfy each eligible due in the configured sequence up to the full Outstanding Amount before moving to the next configured item. The exact interaction between Full Due and percentages is an assumption requiring confirmation.
- **Advance Account**: The Company/customer account configured to receive an unappropriated residual amount from a Receipt.
- **Residual Amount**: The Receipt Document Amount remaining after all confirmed allocations to Pending Dues, calculated to two decimal places.
- **Show**: The Appropriation_UI action that retrieves and displays all Pending Dues for the selected customer.
- **Suggest**: The Appropriation_UI action that requests a calculated preview from the Appropriation Engine without confirming or persisting the preview.
- **Confirm**: The Appropriation_UI action that submits the selected or suggested allocations for validation and atomic persistence.
- **Appropriation Preview**: A non-persisted response containing ordered dues, proposed allocations, totals, and any Residual Amount.
- **Appropriation Audit Entry**: An immutable record of a confirmed appropriation, including the Receipt, mode, actor, allocations, residual, and timestamp.
- **System Administrator**: An authenticated user carrying the `SystemAdmin` role claim.
- **Appropriation_Service**: The backend API and application service responsible for Receipt Appropriation.
- **PaginatedResponse**: The platform response shape containing page items, total count, current page, and page size.
- **EARS**: Easy Approach to Requirements Syntax, the structured requirement format used by this document.

## Dependencies and Assumptions

The following dependencies and silent decisions require confirmation before implementation. They are written explicitly because FINNOVA-62 does not define the complete rule-maintenance, persistence, or accounting behavior.

- **Assumption A1 — Existing receipt dependency:** The engine operates on a Receipt created or edited by FINNOVA-61. The Receipt already contains a valid Customer, Company, LOB, and Receipt Document Amount. This feature does not replace receipt capture or create a second receipt header.
- **Assumption A2 — Backend host and gateway:** The engine is hosted in the existing `Finnova.AccountsService` with Receipt Processing because dues, loan accounts, and receipts are Accounts concerns. The UI reaches the host through the existing gateway receipt route and a new appropriation alias only if the current UI-composed URL requires one. A new backend host is not introduced.
- **Assumption A3 — Due source:** Pending Dues are read from the existing demand/billing or cash-flow due data used by Accounts. FINNOVA-62 introduces the appropriation read and allocation contract but does not create a second outstanding-balance ledger. The final implementation must confirm the authoritative due entity and its status values.
- **Assumption A4 — Rule-master ownership:** The Appropriation Logic Master is an existing master or a separate master-management ticket. FINNOVA-62 consumes active rules and does not add rule-maintenance CRUD unless product ownership confirms that rule maintenance is part of this Jira story.
- **Assumption A5 — One applicable active rule:** For a Company and LOB, the rule lookup is expected to resolve exactly one active Appropriation Rule. If multiple active rules match, the engine returns a configuration-conflict error rather than selecting an arbitrary rule. If no active rule matches, Suggest is unavailable and Confirm cannot use Suggest Mode.
- **Assumption A6 — Rule detail semantics:** Receipt-Type Rule Details use the supported Cash Flow Due Types and have a deterministic order. Details are expected to have percentages from 0.00 through 100.00, and the percentages in one active rule are expected to total exactly 100.00. The rule-master owner must confirm whether zero-percent details are stored, whether duplicate due types are allowed, and whether percentage precision is greater than two decimals.
- **Assumption A7 — Full Due semantics:** When Full Due is enabled, the engine fills each selected due up to its Outstanding Amount in configured order, subject to the remaining percentage target and Receipt Document Amount. When Full Due is disabled, the engine applies the configured percentage target to the selected due type and caps each line at its Outstanding Amount. Product must confirm whether this interpretation matches the screenshot and business policy.
- **Assumption A8 — Direction semantics:** Vertical means Contract-first iteration and Horizontal means Cash Flow Due Type-first iteration. Within either direction, the engine uses the required due ordering and the rule detail order. Product must confirm whether Horizontal allocation should distribute a due-type target evenly, proportionally, or oldest-first across Contracts. This document assumes oldest-first within the due-type group.
- **Assumption A9 — Manual due cap:** A Manual Allocated Amount cannot exceed the corresponding Pending Due Outstanding Amount. This protects the due balance and is proposed for confirmation because the Jira story explicitly states the receipt-total cap but does not explicitly state the per-due cap.
- **Assumption A10 — Manual residual:** Manual confirmation may leave part of the Receipt Document Amount unallocated. The UI displays the Residual Amount and requires the officer to accept transfer of that residual to the Advance Account before Confirm succeeds. Product must confirm whether Manual Mode should instead require exact allocation of the full receipt amount.
- **Assumption A11 — Advance Account:** Each applicable Company has one active Advance Account for the customer or receipt context. A missing or inactive Advance Account blocks confirmation whenever the Residual Amount is greater than zero. The engine does not silently discard or round away a residual.
- **Assumption A12 — Persistence timing:** Show and Suggest are preview operations. Confirm persists the Receipt Appropriation and links it to the Receipt. FINNOVA-62 does not authorize, post, reverse, or submit the appropriation to the General Ledger; those actions remain in the Receipt Authorization/JV Posting scope unless the product owner expands this ticket.
- **Assumption A13 — Reconfirmation and idempotency:** Confirm accepts a client request identifier and is idempotent. Repeating the same successful request returns the existing appropriation without a second persistence operation. A different allocation for a Receipt that already has a confirmed appropriation is rejected unless a future reversal/amendment workflow is approved.
- **Assumption A14 — Concurrency:** The engine rechecks due balances and rule applicability during Confirm. If another transaction changes a Pending Due after Show or Suggest, Confirm fails without partial persistence and asks the user to refresh the appropriation.
- **Assumption A15 — Authorization:** `BranchCollectionOfficer` and `SystemAdmin` are authorized to Show, Suggest, and Confirm. The final role-to-business-title mapping and any Company/LOB/Branch scope restrictions require confirmation. Unauthenticated requests receive 401 and authenticated users without an allowed role receive 403.
- **Assumption A16 — Audit:** A confirmed appropriation creates one immutable Appropriation Audit Entry. Show, Suggest, validation failures, and rejected Confirm requests do not create audit entries. The product owner must confirm whether this feature should reuse the receipt audit trail from FINNOVA-61 or create a separate appropriation audit record.
- **Assumption A17 — Amount precision and rounding:** All monetary fields use INR with exactly two decimal places. Suggest percentage multiplication rounds to two decimals using round-half-up accounting rounding. The engine assigns any rounding remainder to the final eligible allocation in the deterministic iteration order, subject to the Outstanding Amount cap, and sends any remaining amount to the Advance Account.
- **Assumption A18 — Customer and account scope:** Show retrieves Pending Dues for the selected Customer across all eligible Contracts, not only the Contract used by the receipt example. The Company and LOB filter the rule, while the customer determines the due population.
- **Assumption A19 — English and India context:** User-facing text and persisted display values are English-only, all amounts are INR, dates use the platform’s India time-zone conventions, and no RTL behavior is required.

## Requirements

### Requirement 1: Retrieve and Display Pending Dues

**User Story:** As a Collection Officer, I want to see all pending dues for a customer in a predictable order, so that I can decide how to apply a receipt.

#### Acceptance Criteria

1. WHEN a Collection Officer selects Show for a Receipt containing a valid Customer and a positive Receipt Document Amount with no more than two fractional digits, THE Appropriation_Service SHALL return every eligible Pending Due for the Customer across all eligible Contracts, including Account Number, Cash Flow Due Type, Due Date, Outstanding Amount, and Allocated Amount initialized to `0.00` for a new appropriation.
2. WHEN THE Appropriation_Service returns a Pending Due collection, THE Appropriation_Service SHALL order the collection by Account Number ascending, Cash Flow Due Type in the sequence Installment, Principal, Interest, Insurance, ODI, Other Charges, Due Date ascending, and a stable unique identifier ascending when all preceding values are equal.
3. WHEN THE Appropriation_UI receives a successful Pending Due collection, THE Appropriation_UI SHALL display the Pending Dues in the response order and SHALL display Receipt Document Amount, Total Allocated Amount, and Residual Amount.
4. IF the selected Customer does not exist, is inactive, or is outside the requesting user’s access scope, THEN THE Appropriation_Service SHALL reject Show with a not-found or authorization error and SHALL return zero Pending Due records.
5. IF the selected Customer has zero eligible Pending Dues, THEN THE Appropriation_Service SHALL return a successful empty collection and THE Appropriation_UI SHALL display an English message indicating that no pending dues are available.
6. IF a Pending Due has an Outstanding Amount less than or equal to `0.00` or has a non-collectible status, THEN THE Appropriation_Service SHALL exclude the Pending Due from Show results.
7. IF a Receipt has a Receipt Document Amount with more than two fractional digits or a Receipt Document Amount less than or equal to `0.00`, THEN THE Appropriation_Service SHALL reject Show with a validation error identifying Receipt Document Amount.

### Requirement 2: Manual Allocation

**User Story:** As a Collection Officer, I want to edit allocations against individual dues, so that I can apply a receipt according to a business decision.

#### Acceptance Criteria

1. WHEN a Collection Officer enters a Manual Allocated Amount for a Pending Due, THE Appropriation_UI SHALL display the amount with exactly two fractional digits and SHALL recalculate Total Allocated Amount and Residual Amount without submitting Confirm.
2. WHEN Confirm receives Manual Allocated Amount values, THE Appropriation_Service SHALL accept each value from `0.00` through the related Pending Due Outstanding Amount inclusive, subject to the Receipt Document Amount limit.
3. IF Confirm receives a Manual Allocated Amount that is negative, contains more than two fractional digits, or exceeds the related Pending Due Outstanding Amount, THEN THE Appropriation_Service SHALL reject Confirm with a validation error identifying Account Number and Cash Flow Due Type and SHALL preserve the previously confirmed Receipt Appropriation.
4. IF the sum of Manual Allocated Amount values exceeds Receipt Document Amount, THEN THE Appropriation_UI SHALL display `Total allocated amount exceeds Receipt Document Amount` and SHALL keep Confirm disabled.
5. IF Confirm receives Manual Allocated Amount values whose sum exceeds Receipt Document Amount, THEN THE Appropriation_Service SHALL reject Confirm with the error message `Total allocated amount exceeds Receipt Document Amount` and SHALL persist no allocation or Appropriation Audit Entry.
6. WHEN a Collection Officer accepts the displayed residual transfer and confirms valid Manual allocations with a positive Residual Amount, THE Appropriation_Service SHALL allocate the Residual Amount to the Advance Account during Confirm.
7. IF Manual Allocated Amount values total less than Receipt Document Amount and the Collection Officer has not accepted residual transfer, THEN THE Appropriation_UI SHALL keep Confirm disabled and SHALL display the Residual Amount requiring an explicit Advance Account decision.
8. WHEN a Collection Officer clears a Manual Allocated Amount field, THE Appropriation_UI SHALL treat the cleared value as `0.00` and SHALL include the related Pending Due in the recalculated remaining amount.
9. WHEN Confirm succeeds for valid Manual allocations, THE Appropriation_Service SHALL persist every positive allocation against its Pending Due and SHALL persist no allocation row for a zero Manual Allocated Amount.

### Requirement 3: Suggest Allocation from Active Rules

**User Story:** As a Collection Officer, I want the system to suggest allocations from the configured policy, so that routine receipts can be appropriated consistently.

#### Acceptance Criteria

1. WHEN a Collection Officer selects Suggest for a Receipt containing a valid Company and LOB, THE Appropriation_Service SHALL resolve the active Appropriation Rule for the Company and LOB and SHALL return an Appropriation Preview without persisting allocations.
2. WHEN an active Appropriation Rule contains a Receipt-Type Rule Detail with percentage `P`, THE Appropriation_Service SHALL calculate the detail target as Receipt Document Amount multiplied by `P` divided by `100`, rounded to two fractional digits, and SHALL cap every resulting allocation at the applicable Outstanding Amount.
3. WHEN THE Appropriation_Service calculates Suggest allocations, THE Appropriation_Service SHALL process Receipt-Type Rule Details in configured order and SHALL process Pending Dues according to the configured Appropriation Direction.
4. WHEN a Receipt for Customer `CUST-100234` and Contract `ACC-889021` has an Installment Outstanding Amount of `30000.00`, an ODI Outstanding Amount of `15000.00`, active Installment and ODI rule details of `80.00%` and `20.00%`, and a Receipt Document Amount of `35000.00`, THE Appropriation_Service SHALL suggest `28000.00` for Installment and `7000.00` for ODI.
5. WHEN an Appropriation Preview contains a Pending Due allocation, THE Appropriation_Service SHALL return the proposed Allocated Amount, the resulting remaining Outstanding Amount, the applied Receipt-Type Rule Detail, and the applied rule order.
6. IF no active Appropriation Rule matches the Receipt Company and LOB, THEN THE Appropriation_Service SHALL reject Suggest and Confirm in Suggest Mode with a configuration error indicating that no active appropriation rule is configured.
7. IF more than one active Appropriation Rule matches the Receipt Company and LOB, THEN THE Appropriation_Service SHALL reject Suggest and Confirm in Suggest Mode with a configuration-conflict error identifying ambiguous active-rule selection.
8. IF an active Appropriation Rule contains zero usable Receipt-Type Rule Details or Receipt-Type Rule Detail percentages whose total is not exactly `100.00%`, THEN THE Appropriation_Service SHALL reject Suggest and Confirm in Suggest Mode with a rule-validation error and SHALL persist no allocation.
9. IF a configured Receipt-Type Rule Detail has no matching Pending Due, THEN THE Appropriation_Service SHALL carry the unapplied detail target into Residual Amount and SHALL apply the Advance Account behavior defined in Requirement 5.

### Requirement 4: Apply Appropriation Direction and Full Due Policy

**User Story:** As a Collection Officer, I want Suggest to honor the configured direction and Full Due policy, so that the same rule produces the intended allocation order.

#### Acceptance Criteria

1. WHEN an active Appropriation Rule has Appropriation Direction `Vertical`, THE Appropriation_Service SHALL process each Contract through the complete configured Receipt-Type Rule Detail order before processing the next Contract.
2. WHEN an active Appropriation Rule has Appropriation Direction `Horizontal`, THE Appropriation_Service SHALL process each configured Receipt-Type Rule Detail across all eligible Contracts before processing the next Receipt-Type Rule Detail.
3. WHEN THE Appropriation_Service processes Pending Dues within a Contract or Cash Flow Due Type group, THE Appropriation_Service SHALL order Pending Dues by Account Number ascending, Cash Flow Due Type hierarchy, Due Date ascending, and a stable unique identifier ascending when all preceding values are equal.
4. WHERE an active Appropriation Rule has Full Due enabled, THE Appropriation_Service SHALL fill each eligible Pending Due up to the Pending Due Outstanding Amount before moving to the next eligible Pending Due in the configured iteration order, subject to the Receipt-Type Rule Detail target and remaining Receipt Document Amount.
5. WHERE an active Appropriation Rule has Full Due disabled, THE Appropriation_Service SHALL apply each Receipt-Type Rule Detail target in deterministic oldest-Due-Date-first order across eligible Pending Dues and SHALL cap every allocation at the related Pending Due Outstanding Amount.
6. IF an Appropriation Rule contains an Appropriation Direction other than `Vertical` or `Horizontal`, THEN THE Appropriation_Service SHALL reject Suggest and Confirm in Suggest Mode with a rule-validation error and SHALL persist no allocation.
7. WHEN THE Appropriation_Service receives repeated Suggest requests with unchanged Receipt Document Amount, Pending Due collection, and active Appropriation Rule, THE Appropriation_Service SHALL return identical allocation amounts and identical allocation order.

### Requirement 5: Transfer the Residual to the Advance Account

**User Story:** As a Collection Officer, I want any receipt amount that cannot be applied to dues to be identified and transferred to the Advance Account, so that the complete receipt remains accounted for.

#### Acceptance Criteria

1. WHEN Suggest allocations or accepted Manual allocations leave a positive Residual Amount, THE Appropriation_Service SHALL include exactly one Advance Account allocation equal to the Residual Amount rounded to two fractional digits.
2. WHEN Suggest allocations for a Receipt Document Amount of `35000.00` total `35000.00`, THE Appropriation_Service SHALL report Residual Amount `0.00` and SHALL create no Advance Account allocation.
3. WHEN Receipt Document Amount exceeds the total balances of all eligible Pending Dues, THE Appropriation_Service SHALL assign the excess to the configured Advance Account and SHALL report the Advance Account amount separately from due allocations.
4. IF Residual Amount is positive and the applicable Company has no active Advance Account, THEN THE Appropriation_Service SHALL reject Confirm with an error indicating that the Advance Account is not configured and SHALL persist no Receipt Appropriation.
5. WHEN THE Appropriation_Service returns an Appropriation Preview, THE Appropriation_Service SHALL report Receipt Document Amount, total due allocations, Advance Account allocation, and Residual Amount with a reconciliation calculation showing that due allocations plus Advance Account allocation equals Receipt Document Amount after two-decimal rounding.
6. IF due allocations plus Advance Account allocation differs from Receipt Document Amount after two-decimal rounding, THEN THE Appropriation_Service SHALL reject Confirm with a reconciliation error and SHALL persist no Receipt Appropriation.

### Requirement 6: Confirm and Persist Appropriation Atomically

**User Story:** As a Collection Officer, I want Confirm to save the complete appropriation as one operation, so that receipt allocation and outstanding balances cannot be left partially updated.

#### Acceptance Criteria

1. WHEN a Collection Officer confirms a valid Manual or Suggest allocation, THE Appropriation_Service SHALL persist the Receipt Appropriation, every positive due allocation, any Advance Account allocation, the applied mode, and the acting user in one atomic transaction.
2. WHEN Confirm succeeds, THE Appropriation_Service SHALL return Receipt identifier, Appropriation identifier, mode, allocations, totals, Advance Account amount, confirmation timestamp in UTC, and resulting status.
3. IF a validation check, rule check, Advance Account check, due-balance check, or persistence check fails during Confirm, THEN THE Appropriation_Service SHALL roll back every allocation in the Confirm request and SHALL leave the Receipt and Pending Dues unchanged.
4. WHEN Confirm receives a new Client Request Identifier, THE Appropriation_Service SHALL associate the Client Request Identifier with the resulting Receipt Appropriation.
5. WHEN Confirm receives a Client Request Identifier associated with a previously successful appropriation, THE Appropriation_Service SHALL return the existing Receipt Appropriation and SHALL create no duplicate allocations or Appropriation Audit Entry.
6. IF a Receipt has a confirmed Receipt Appropriation and Confirm receives a different allocation set, THEN THE Appropriation_Service SHALL reject Confirm with a conflict error indicating that the Receipt has already been appropriated.
7. IF a Pending Due balance or applicable Appropriation Rule changes between Show or Suggest and Confirm, THEN THE Appropriation_Service SHALL reject Confirm with a stale-data error, SHALL persist no partial result, and SHALL require the Appropriation_UI to refresh the Pending Dues and rule preview.
8. WHEN Confirm succeeds, THE Appropriation_Service SHALL create exactly one immutable Appropriation Audit Entry for the confirmed Receipt Appropriation.
9. WHEN Show succeeds, Suggest succeeds, validation fails, stale-data rejection occurs, or Confirm fails, THE Appropriation_Service SHALL create zero Appropriation Audit Entries.

### Requirement 7: Backend Contracts and Service Errors

**User Story:** As a Collection Officer, I want clear responses for previews, confirmations, and failures, so that the UI can explain the result and prevent unsafe allocation.

#### Acceptance Criteria

1. THE Appropriation_Service SHALL expose a Show operation that accepts a Receipt identifier or an equivalent validated Receipt context and returns the ordered Pending Due collection and Receipt Document Amount.
2. THE Appropriation_Service SHALL expose a Suggest operation that accepts a Receipt identifier or an equivalent validated Receipt context and returns an Appropriation Preview containing applied rule identity, Appropriation Direction, Full Due setting, proposed allocations, totals, and Residual Amount.
3. THE Appropriation_Service SHALL expose a Confirm operation that accepts Receipt identifier, allocation mode, allocation set, Advance Account acceptance decision, and Client Request Identifier.
4. IF a request omits a required Receipt identifier, Customer identifier, Company, LOB, Receipt Document Amount, allocation mode, or Client Request Identifier, THEN THE Appropriation_Service SHALL return a validation response identifying every missing field and SHALL persist no data.
5. IF a request contains an unknown Cash Flow Due Type, malformed identifier, negative amount, amount with more than two fractional digits, or unsupported allocation mode, THEN THE Appropriation_Service SHALL return a validation response identifying the offending value and SHALL persist no data.
6. IF the Receipt is missing, inaccessible, cancelled, or in a status that does not permit appropriation, THEN THE Appropriation_Service SHALL return a business-rule or not-found response and SHALL persist no data.
7. WHEN THE Appropriation_Service returns an error, THE Appropriation_Service SHALL provide a stable error code, an English error message, and field or allocation context when the error identifies a specific input.
8. WHEN THE Appropriation_Service returns a successful paginated Pending Due response, THE Appropriation_Service SHALL use the platform `PaginatedResponse` contract; WHEN the customer Pending Due set is bounded and non-paginated, THE Appropriation_Service SHALL use an explicitly documented collection contract agreed during design.

### Requirement 8: Appropriation_UI Behavior

**User Story:** As a Collection Officer, I want a clear Manual versus Suggest workflow, so that I can review the engine’s result before confirming a receipt.

#### Acceptance Criteria

1. WHEN the Appropriation_UI opens for a valid Receipt, THE Appropriation_UI SHALL display Customer, Receipt Document Amount, Manual choice, Suggest choice, Show action, Suggest action, Confirm action, and an allocation grid.
2. WHEN a Collection Officer selects Show, THE Appropriation_UI SHALL request Pending Dues and SHALL render Account Number, Cash Flow Due Type, Due Date, Outstanding Amount, Allocated Amount, and remaining balance columns in the required order.
3. WHEN a Collection Officer selects Suggest, THE Appropriation_UI SHALL render suggested allocations, applied rule details, Appropriation Direction, Full Due value, due allocation total, Advance Account amount, and Residual Amount before enabling Confirm.
4. WHEN a Collection Officer selects Manual, THE Appropriation_UI SHALL make Allocated Amount fields editable and SHALL exclude rule-derived values from the Confirm payload unless the Collection Officer explicitly selects Suggest.
5. IF a Collection Officer enters an invalid amount or enters allocation values whose total exceeds Receipt Document Amount, THEN THE Appropriation_UI SHALL display the applicable English validation message beside the affected field or total and SHALL keep Confirm disabled.
6. WHEN an Appropriation_Service request is in progress, THE Appropriation_UI SHALL disable the action that initiated the request and SHALL display a loading state until the request succeeds or fails.
7. IF Show, Suggest, or Confirm returns an error, THEN THE Appropriation_UI SHALL preserve current editable values when the values remain valid, SHALL display the returned English error message, and SHALL keep Confirm disabled until the blocking error is resolved.
8. WHEN Confirm succeeds, THE Appropriation_UI SHALL display confirmed status and totals, SHALL prevent a second confirmation for the successful request, and SHALL provide an action to return to the receipt workflow.
9. THE Appropriation_UI SHALL use the shared HTTP client, service interface, mock implementation, real implementation, feature toggle, React hooks, and Material UI conventions used by FINNOVA-61 and other Finnova master screens.
10. THE Appropriation_UI SHALL render English text only and SHALL use the existing left-to-right application layout without RTL rendering or translation handling.

### Requirement 9: Authorization and Access Control

**User Story:** As a security stakeholder, I want receipt appropriation restricted to authorized collection users, so that only permitted staff can allocate funds.

#### Acceptance Criteria

1. IF a Show, Suggest, or Confirm request contains a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Appropriation_Service SHALL reject the request with HTTP `401`, SHALL return an English authentication error, and SHALL change no Receipt, Pending Due, or Receipt Appropriation record.
2. IF an authenticated user lacks the configured Collection Officer or System Administrator role, THEN THE Appropriation_Service SHALL reject Show, Suggest, and Confirm with HTTP `403`, SHALL return an English authorization error, and SHALL change no Receipt, Pending Due, or Receipt Appropriation record.
3. WHEN an authenticated user has an allowed appropriation role and an in-scope Company, LOB, Branch, and Customer, THE Appropriation_Service SHALL authorize the requested operation subject to Requirements 1 through 8.
4. WHEN THE Appropriation_Service evaluates access, THE Appropriation_Service SHALL authenticate the request before evaluating role or business scope so that a request failing both authentication and authorization receives HTTP `401`.
5. WHEN the Appropriation_UI receives HTTP `401` or HTTP `403`, THE Appropriation_UI SHALL use the shared client’s centralized session or authorization handling and SHALL display no control state indicating successful operation.

### Requirement 10: Monetary Precision, Ordering, and Reconciliation Invariants

**User Story:** As an accounting stakeholder, I want appropriation calculations to be precise and repeatable, so that receipt amounts reconcile to every allocation.

#### Acceptance Criteria

1. THE Appropriation_Service SHALL represent every Receipt Document Amount, Outstanding Amount, Allocated Amount, Residual Amount, percentage target, and Advance Account amount with a monetary scale of two fractional digits in requests, responses, and persistence.
2. IF a monetary input contains more than two fractional digits, THEN THE Appropriation_Service SHALL reject the input with a validation error and SHALL not silently truncate the input.
3. WHEN THE Appropriation_Service calculates a preview or confirms an appropriation, THE Appropriation_Service SHALL preserve the invariant that total due allocations plus Advance Account allocation plus Residual Amount equals Receipt Document Amount to two fractional digits.
4. WHEN THE Appropriation_Service applies percentage rounding, THE Appropriation_Service SHALL use round-half-up accounting rounding to two fractional digits and SHALL apply any rounding remainder deterministically in configured iteration order.
5. WHEN THE Appropriation_Service receives identical Receipt, Pending Due, Appropriation Rule, and allocation inputs, THE Appropriation_Service SHALL return identical allocation amounts and identical allocation ordering for every repeated execution.
6. WHEN two Pending Dues have equal Account Number, Cash Flow Due Type, and Due Date, THE Appropriation_Service SHALL use a stable unique-identifier tie-breaker so that allocation order remains deterministic.
7. IF a confirmed allocation would make a Pending Due balance negative, THEN THE Appropriation_Service SHALL reject Confirm with a reconciliation error and SHALL roll back the transaction.
8. WHEN Confirm succeeds, THE Appropriation_Service SHALL update or reserve affected Pending Due balances in the same atomic operation as the Receipt Appropriation according to the authoritative due-ledger contract agreed during design.

### Requirement 11: Testable Acceptance Examples and Edge Cases

**User Story:** As a product owner, I want the Manual and Suggest rules to be verifiable with representative cases, so that the implementation can be accepted against the Jira story.

#### Acceptance Criteria

1. WHEN a Receipt for Customer `CUST-100234` and Contract `ACC-889021` has Installment Outstanding Amount `30000.00`, ODI Outstanding Amount `15000.00`, active rule details Installment `80.00%` and ODI `20.00%`, and Receipt Document Amount `35000.00`, THE Appropriation_Service SHALL return Suggest allocations of Installment `28000.00` and ODI `7000.00`, Installment remaining amount `2000.00`, ODI remaining amount `8000.00`, and Residual Amount `0.00`.
2. WHEN a Collection Officer enters Manual allocation values totaling `36000.00` for a Receipt Document Amount of `35000.00`, THE Appropriation_UI SHALL display `Total allocated amount exceeds Receipt Document Amount` and SHALL keep Confirm disabled.
3. IF a service request contains Manual allocation values totaling `36000.00` for a Receipt Document Amount of `35000.00`, THEN THE Appropriation_Service SHALL reject the request with `Total allocated amount exceeds Receipt Document Amount` and SHALL persist no allocation.
4. WHEN a Receipt has `1000.00` available and one Pending Due has `600.00` outstanding, THE Appropriation_Service SHALL allocate no more than `600.00` to the Pending Due and SHALL route `400.00` to the Advance Account when the Advance Account is configured.
5. WHEN a Receipt has zero eligible Pending Dues and a configured Advance Account, THE Appropriation_Service SHALL return zero due allocations, an Advance Account allocation equal to Receipt Document Amount, and a successful Appropriation Preview.
6. WHEN an active Suggest rule has Receipt-Type Rule Details of `33.33%`, `33.33%`, and `33.34%` for a Receipt Document Amount of `100.00`, THE Appropriation_Service SHALL return rounded allocations whose total reconciles exactly to `100.00` or SHALL route the deterministic rounding remainder to the Advance Account.
7. WHEN a Collection Officer confirms an allocation after another process reduces an affected Pending Due Outstanding Amount below the requested Allocated Amount, THE Appropriation_Service SHALL reject Confirm as stale, SHALL preserve the changed Pending Due balance, and SHALL require a refreshed Appropriation Preview.
8. WHEN a Collection Officer confirms a valid allocation twice with one Client Request Identifier, THE Appropriation_Service SHALL return one Receipt Appropriation result, SHALL persist one set of allocation rows, and SHALL persist one Appropriation Audit Entry.
