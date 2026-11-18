# Requirements Document

## Introduction

The Receipt Authorization and Inter-Branch Accounting feature (Jira FINNOVA-63, “Receipt Processing – Maker-Checker Authorization & Inter-Branch Accounting”; FRS Section 1.6 Receipt Authorization Screen and Drawee Bank Details) lets an Authorized Approver or Branch Manager review receipts created by a Cashier or Branch Collection Officer, authorize or reject receipts that require maker-checker approval, and initiate the approved asynchronous journal-posting flow.

FINNOVA-61 Receipt Processing creates a receipt in `PendingAuthorization` and explicitly defers Authorize, Revoke, and submission to the JV Posting Engine to this follow-up. FINNOVA-63 consumes that receipt aggregate. Entry Save does not post financial journals. Authorization performs the synchronous business decision and records the decision atomically; successful authorization submits one approved posting request to the existing JV Posting Engine (JVE) boundary. The JVE remains responsible for Cashflow Master rule resolution, canonical journal creation, balanced posting, retries, and final posting completion.

When a receipt line belongs to an account branch different from the collection branch, the posting request contains the two-sided inter-branch accounting contract. The JVE posts the collection-side debit to the collection branch cashier account and the collection-side credit to an inter-branch control account for the account branch. The JVE posts the account-side debit to the corresponding inter-branch control account and the account-side credit to the loan-installment account in the account branch books. Same-branch lines use the applicable local Cashflow Master rule without an inter-branch pair. All journal creation remains inside the JVE’s approved same-database atomic posting boundary.

The feature includes backend authorization and review APIs in `Finnova.AccountsService` behind `Finnova.ApiGateway`, and frontend authorization and receipt-review behavior in Finnova-UI. The UI remains English-only, uses INR with two decimal places, uses UTC technical timestamps for audit data, and contains no right-to-left rendering or localization framework. The feature does not create a second canonical journal model, a second Cashflow Master, or a public posting endpoint in the Python worker.

### Dependencies and Assumptions

The following dependencies and silent decisions require confirmation before implementation. They are recorded explicitly because FINNOVA-63 does not define the complete workflow configuration, status vocabulary, account ownership, or printing contract.

- **Dependency D1 — FINNOVA-61 Receipt Processing:** FINNOVA-61 owns receipt capture, receipt numbering, receipt lines, customer and account snapshots, the initial `PendingAuthorization` status, existing receipt audit entries, and the receipt read model. FINNOVA-63 extends the existing receipt lifecycle and does not create a parallel receipt aggregate.
- **Dependency D2 — JV Posting Engine:** The JVE is the only approved boundary for canonical journal creation. FINNOVA-63 submits an approved, immutable posting request and does not write Journal Header, Journal Line, source-link, or Cashflow rule tables directly.
- **Dependency D3 — Cashflow Master:** Cashflow Master remains the authoritative owner of event-to-accounting rules, account mappings, required dimensions, effective dates, and transaction-specific debit/credit derivation. FINNOVA-63 supplies the inter-branch context and required posting facts to the JVE; it does not duplicate or hard-code Cashflow rules.
- **Dependency D4 — GL Master:** The active GL Master validates postability, company ownership, branch applicability, effective dates, and required dimensions for cashier accounts, inter-branch control accounts, loan-installment accounts, and any other accounts selected by Cashflow Master.
- **Dependency D5 — Organization and branch configuration:** The organization hierarchy, Line of Business, company, branch, account-branch relationship, and user access-assignment masters provide the branch scope used by authorization and posting. The exact source of the account branch for each Loan Account must be confirmed.
- **Assumption A1 — Workflow applicability:** A configurable receipt workflow rule determines whether a receipt requires authorization. The default proposal is that the rule is evaluated from Company, Line of Business, Receipt Type, Receipt From, collection Branch, and each account branch. A receipt with no applicable workflow rule is treated as not requiring maker-checker authorization only when an approved configuration explicitly says so; otherwise the service fails closed. Confirm the rule owner, precedence, and default behavior.
- **Assumption A2 — Existing status fields:** FINNOVA-61 currently defines `PendingAuthorization`, `Authorized`, and `Revoked` as receipt statuses and reserves authorization fields. This feature proposes either a separate `AuthorizationStatus` and `PostingStatus` pair or additional receipt statuses such as `PostingPending` and `Posted`, while retaining `Authorized` for the synchronous approval decision. Confirm the authoritative status vocabulary and migration approach.
- **Assumption A3 — Approval decision and asynchronous posting:** Authorization is synchronous. A successful approval changes the receipt decision status to `Authorized` and commits the approved posting-request identity in the same authorization transaction. The JVE posts asynchronously after the transaction commits. The UI displays `Authorized / Posting Pending` until the JVE reports `Posted`. Confirm whether the product wants `Authorized` exposed immediately or a distinct `AuthorizationAccepted` display label.
- **Assumption A4 — Rejection mapping:** The user story calls the negative decision “reject”, while FINNOVA-61 reserves `Revoked`. This document proposes `Rejected` as the business decision and maps it to the existing `Revoked` status only if the platform cannot add a distinct rejection status. Confirm whether the persisted value must be `Rejected`, `Revoked`, or a separate authorization-decision field.
- **Assumption A5 — Authorization roles:** The roles `AuthorizedApprover` and `BranchManager` are proposed for authorization. `SystemAdmin` may inspect and operate only where platform policy grants the privilege. Confirm the existing role names and whether `BranchCollectionOfficer` may authorize in any branch.
- **Assumption A6 — Maker identity:** The Maker is the authenticated user identity captured by FINNOVA-61 as `CreatedBy` and in the receipt audit entry. Authorization compares the validated token identity with the immutable Maker identity, not with a display name or request-body value.
- **Assumption A7 — Authorization scope:** An Authorized Approver may authorize only receipts within the approver’s assigned Company, Line of Business, and Branch scope. A Branch Manager may authorize receipts collected in the manager’s assigned branch. Scope enforcement depends on existing user access assignments and must be confirmed with the authorization owner.
- **Assumption A8 — Third-party aggregate hold:** A Third-Party receipt is one aggregate authorization unit. If any account line requires authorization, the complete receipt remains pending; the service does not authorize or post individual account lines. A third-party receipt with no applicable workflow requirement may remain outside maker-checker only if A1’s approved workflow configuration permits that result.
- **Assumption A9 — Account branch:** Each Loan Account has one authoritative Account Branch. For a multi-account receipt, the service evaluates the account branch independently for every line and emits one posting instruction containing all line contexts. The collection branch comes from the receipt Branch snapshot.
- **Assumption A10 — Inter-branch account configuration:** The cashier account and inter-branch control account are selected from approved branch/company configuration and validated through GL Master. The configuration may be keyed by Company, Collection Branch, Account Branch, Currency, and Cashflow event. Missing, inactive, ambiguous, or non-postable configuration blocks authorization or causes the JVE to dead-letter the posting request according to the confirmed ownership boundary.
- **Assumption A11 — Inter-branch accounting shape:** For each authorized line where Collection Branch differs from Account Branch, the JVE creates a collection-book pair `Dr Collection Branch Cashier A/c` and `Cr Inter-Branch Control A/c - Account Branch`, plus an account-book pair `Dr Inter-Branch Control A/c - Account Branch` and `Cr Loan Installment - Account Branch Account`. The exact GL codes, dimensions, branch-book metadata, and narration are resolved by Cashflow Master and GL Master, not by FINNOVA-63.
- **Assumption A12 — Same-branch accounting:** When Collection Branch equals Account Branch, the JVE applies the normal same-branch Cashflow Master rule and does not create an inter-branch control pair solely because the receipt has multiple lines.
- **Assumption A13 — Posting request payload:** The approved posting request carries the receipt identifier, immutable receipt version or hash, Company, collection branch, account branch for every line, account identifiers, receipt type, receipt mode, amount, currency, value date, authorization actor, authorization timestamp, workflow decision, and a correlation identifier. Exact JSON schema version and event key are owned by the JVE contract.
- **Assumption A14 — Atomicity:** The authorization transaction atomically records the decision, approver, timestamp, reason, audit entry, and approved posting-request identity. Canonical journal writes and the final `Posted` outcome are atomic within the JVE transaction. A remote call made before authorization commits is not permitted to create a journal; the submission boundary must be transactional or use the approved JVE inbox contract so that an authorized receipt cannot lose its posting request.
- **Assumption A15 — Authorization idempotency:** The UI supplies a `ClientRequestId` per authorization action. Repeating the same action for the same receipt and expected version returns the existing decision and posting-request identity without a second decision, audit entry, or posting request. A different decision after a terminal decision is rejected.
- **Assumption A16 — Concurrency:** Authorization uses an expected receipt version or equivalent compare-and-set condition. Only one approver can transition a pending receipt. A stale review cannot overwrite a newer approval or rejection.
- **Assumption A17 — Printing gate:** The receipt is not available for regular printing while authorization is pending, rejected/revoked, or posting is pending/failed. The receipt becomes available for regular printing only after the JVE reports successful `Posted` completion and all required posting links are durable. Confirm whether authorized-but-not-yet-posted receipts may have a separate preview print.
- **Assumption A18 — Authorization rejection:** Rejection does not submit a JVE posting request and does not create financial journal entries. The rejected receipt remains read-only with the rejection actor, UTC timestamp, reason, and immutable audit trail. Reopening or resubmission requires a separately approved workflow.
- **Assumption A19 — Error ownership:** Synchronous validation, maker-checker, status, access, version, and configuration errors are returned by the Receipt Authorization service. JVE posting failures are represented through the approved JVE status and operational error contract and are not converted into a false synchronous authorization failure.
- **Assumption A20 — Currency and dates:** The feature supports INR only with two fractional digits. Business dates use the receipt’s India business-date rules, while authorization, submission, posting, and audit technical timestamps are UTC. Cross-currency and foreign-exchange inter-branch posting are out of scope.
- **Assumption A21 — UI composition:** The UI uses the existing shared axios instance, receipt service pattern, mock/real toggle, React hooks, MUI, and gateway-origin URL conventions established by FINNOVA-61. No Redux, RxJS, ad-hoc axios client, or second authorization state store is introduced.
- **Assumption A22 — Localization:** All labels, status values, workflow messages, error messages, audit descriptions, seed values, and test examples are English-only. No Arabic or other non-English values and no RTL handling are introduced.
- **Assumption A23 — Retention and audit immutability:** Receipt authorization decisions, posting-request links, JVE outcomes, and authorization audit records are retained under the platform’s financial-record retention policy. Normal receipt operations cannot update or delete historical authorization audit entries.

## Glossary

- **Receipt**: The FINNOVA-61 document containing the customer, collection branch, receipt type and mode, receipt amount, account lines, and current lifecycle state.
- **Receipt_Authorization_Service**: The backend service that reviews receipts, evaluates workflow applicability, authorizes or rejects receipts, controls printing eligibility, and submits approved posting requests to the JVE boundary.
- **Receipt_Authorization_UI**: The Finnova-UI review, authorization, rejection, audit, posting-status, and printing-gate experience.
- **Maker**: The authenticated user recorded as the creator of a Receipt by FINNOVA-61.
- **Checker**: An authenticated Authorized Approver or Branch Manager with permission and organizational scope to review and decide a Receipt.
- **Authorized Approver**: A user carrying the approved role and access assignment that permits authorization of a Receipt.
- **Branch Manager**: A user carrying the approved branch-management role and access assignment for the collection branch of a Receipt.
- **Maker-checker**: A control requiring a Checker other than the Maker to decide a Receipt before financial posting.
- **Workflow Rule**: An approved configuration that determines whether a Receipt or any account line requires maker-checker authorization.
- **Authorization Decision**: The immutable business decision of `Pending`, `Authorized`, or `Rejected`/`Revoked` recorded by the Receipt_Authorization_Service.
- **Authorization Status**: The state of the maker-checker decision. The existing FINNOVA-61 status vocabulary and the proposed separate decision field are subject to Assumption A2.
- **Posting Status**: The state of the approved JVE posting request, such as `NotSubmitted`, `PostingPending`, `Posted`, or `Failed`.
- **Pending Authorization**: The receipt state in which a receipt requires a Checker decision and no financial posting is permitted.
- **Authorized**: The synchronous approval decision recorded by a permitted Checker. `Authorized` does not mean that the asynchronous journal is already posted.
- **Rejected**: The synchronous negative authorization decision. The persisted mapping to `Revoked` is subject to Assumption A4.
- **Posted**: The terminal posting state reported after the JVE has durably created the approved canonical journal and source link.
- **Collection Branch**: The branch where the Receipt is entered and collected, taken from the FINNOVA-61 receipt Branch snapshot.
- **Account Branch**: The authoritative branch of a Loan Account to which a Receipt line applies.
- **Same-Branch Line**: A Receipt line whose Collection Branch equals its Account Branch.
- **Inter-Branch Line**: A Receipt line whose Collection Branch differs from its Account Branch.
- **Collection Books**: The branch-book accounting context of the Collection Branch.
- **Account-Branch Books**: The branch-book accounting context of the Account Branch.
- **Cashier Account**: The configured, active, postable GL account used for the collection-side debit in the Collection Branch.
- **Inter-Branch Control Account**: The configured, active, postable GL account used to represent an amount due between the Collection Branch and Account Branch.
- **Loan Installment Account**: The account-side GL account selected by the approved Cashflow Master rule for the loan installment or receipt component.
- **Inter-Branch Posting Contract**: The immutable posting facts that identify the collection branch, account branch, configured control account context, receipt line, amount, and required debit and credit relationships for an Inter-Branch Line.
- **JVE / JV Posting Engine**: The approved asynchronous financial-processing boundary that resolves Cashflow Master rules, validates GL and dimensions, writes canonical journals, and completes posting idempotently.
- **Posting Request**: An immutable approved request submitted to the JVE inbox for asynchronous processing.
- **Posting Request Identity**: The immutable identifier or source-link key that connects an authorized Receipt to exactly one JVE Posting Request.
- **Regular Printing**: The production receipt-print operation that is available only when the receipt satisfies the printing gate.
- **Preview Print**: A non-final display or print of receipt details, if separately approved. Preview Print is not Regular Printing and never implies authorization or posting.
- **Authorization Audit Entry**: An immutable record of a review, authorization, rejection, stale-decision rejection, or posting-status transition containing actor, receipt, decision, reason where applicable, correlation identifier, and UTC timestamp.
- **Expected Version**: The receipt version or concurrency token read by the Checker and supplied when submitting a decision.
- **ClientRequestId**: A client-generated GUID used to make one authorization action idempotent.
- **Atomic Authorization Transaction**: The persistence boundary that commits the authorization decision, decision metadata, audit entry, and approved Posting Request identity together.
- **Financial Posting**: Creation of canonical journal records by the JVE. Financial Posting does not occur during Entry Save or before authorization.
- **Cashflow Master**: The authoritative configuration owner for event-to-accounting rules and debit/credit derivation.
- **GL Master**: The authoritative source for active, approved, postable GL accounts and required dimensions.
- **EARS**: Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.

## Requirements

### Requirement 1: Review Pending Receipts

**User Story:** As an Authorized Approver or Branch Manager, I want to find and inspect pending receipts, so that I can verify receipt details before making a decision.

#### Acceptance Criteria

1. THE Receipt_Authorization_Service SHALL expose a paginated review operation that returns only receipts with Authorization Status `PendingAuthorization` and an applicable Workflow Rule requiring a Checker decision.
2. WHEN a Checker requests a receipt by identifier, THE Receipt_Authorization_Service SHALL return the receipt header, Maker identity, customer snapshot, Collection Branch, account lines, Account Branch for every line, receipt mode details, amount in INR, Authorization Status, Posting Status, Expected Version, and existing authorization audit entries as read-only data.
3. WHEN the Receipt_Authorization_Service returns a review list, THE Receipt_Authorization_Service SHALL order records by Doc Date descending, CreatedAtUtc descending, and Receipt identifier descending.
4. WHEN a review request supplies one or more supported filters, THE Receipt_Authorization_Service SHALL apply all supplied Receipt Type, Receipt Mode, Collection Branch, Customer Code, Document Number, Doc Date range, Maker, Authorization Status, and Posting Status filters together.
5. WHEN a review request omits page number, THE Receipt_Authorization_Service SHALL use page 1.
6. WHEN a review request omits page size, THE Receipt_Authorization_Service SHALL use page size 20.
7. IF a review request supplies a page number less than 1 or a page size less than 1 or greater than 100, THEN THE Receipt_Authorization_Service SHALL return a field-specific validation error and SHALL make no data change.
8. IF a requested receipt does not exist, THEN THE Receipt_Authorization_Service SHALL return the configured not-found error contract and SHALL disclose no receipt details.
9. IF a requested receipt is outside the Checker’s authorized Company, Line of Business, or Branch scope, THEN THE Receipt_Authorization_Service SHALL return an authorization error and SHALL disclose no receipt details.
10. WHEN the Receipt_Authorization_UI loads the review screen, THE Receipt_Authorization_UI SHALL display the pending receipt, Maker, Collection Branch, customer, amount, receipt type, receipt mode, and Posting Status in English.
11. WHEN the Receipt_Authorization_UI opens a receipt detail view, THE Receipt_Authorization_UI SHALL render receipt fields read-only and SHALL display the Account Branch for every account line.

### Requirement 2: Determine Workflow Applicability and Aggregate Authorization

**User Story:** As a compliance stakeholder, I want workflow applicability evaluated consistently, so that every receipt requiring maker-checker control is held as one unit.

#### Acceptance Criteria

1. WHEN the Receipt_Authorization_Service evaluates a Receipt, THE Receipt_Authorization_Service SHALL resolve the applicable Workflow Rule from the approved Company, Line of Business, Receipt Type, Receipt From, Collection Branch, and account-line context.
2. WHEN a Workflow Rule requires authorization for one Account Details line of a Third-Party Receipt, THE Receipt_Authorization_Service SHALL mark the complete Third-Party Receipt as requiring authorization.
3. WHILE a Third-Party Receipt contains at least one account line requiring authorization, THE Receipt_Authorization_Service SHALL apply one Authorization Decision to every account line.
4. WHILE a Third-Party Receipt contains at least one account line requiring authorization, THE Receipt_Authorization_Service SHALL prevent authorization, printing, and posting of a subset of account lines.
5. WHEN all applicable Workflow Rules indicate that authorization is not required, THE Receipt_Authorization_Service SHALL record the resolved workflow result and SHALL not require a Checker decision.
6. IF no applicable Workflow Rule exists and approved configuration requires fail-closed behavior, THEN THE Receipt_Authorization_Service SHALL return a configuration error and SHALL leave the Receipt without an executable posting decision.
7. IF multiple Workflow Rules have equal highest precedence for a Receipt, THEN THE Receipt_Authorization_Service SHALL return a workflow-configuration conflict and SHALL leave the Receipt without an executable posting decision.
8. WHEN the Receipt_Authorization_Service returns a workflow result, THE Receipt_Authorization_Service SHALL include the authorization-required flag, rule identifier or version, evaluated scope, and reason.
9. WHEN the Receipt_Authorization_UI displays a Third-Party Receipt with an authorization requirement on any account line, THE Receipt_Authorization_UI SHALL display an English message that the complete receipt requires authorization.

### Requirement 3: Entry Must Not Post Financial Journals

**User Story:** As a financial controller, I want receipt entry separated from authorization, so that unverified receipts cannot create financial postings.

#### Acceptance Criteria

1. WHEN FINNOVA-61 saves a Receipt in Entry mode, THE Receipt_Authorization_Service SHALL submit no JVE Posting Request as part of the entry operation.
2. WHEN FINNOVA-61 saves a Receipt in Entry mode, THE Receipt_Authorization_Service SHALL create no canonical journal as part of the entry operation.
3. WHILE a Receipt has Authorization Status `PendingAuthorization`, THE Receipt_Authorization_Service SHALL prevent Financial Posting.
4. WHILE a Receipt has Authorization Status `PendingAuthorization`, THE Receipt_Authorization_Service SHALL expose Posting Status `NotSubmitted` or the approved equivalent.
5. WHEN a Receipt does not require authorization under an approved Workflow Rule, THE Receipt_Authorization_Service SHALL submit the Receipt only through the approved JVE boundary.
6. WHEN a Receipt does not require authorization under an approved Workflow Rule, THE Receipt_Authorization_Service SHALL write no canonical journal table directly.
7. IF a caller invokes a canonical journal-write operation through the Receipt API, THEN the platform SHALL reject the operation.
8. IF a caller invokes a canonical journal-write operation through the Receipt API, THEN the platform SHALL leave the Receipt and canonical journal records unchanged.
9. WHEN the Receipt_Authorization_UI displays an Entry-mode Receipt with pending authorization to a user without Checker permission, THE Receipt_Authorization_UI SHALL disable Authorize, Reject, and Regular Printing.
10. WHEN the Receipt_Authorization_UI displays an Entry-mode Receipt with pending authorization, THE Receipt_Authorization_UI SHALL show that posting has not started.
11. IF authorization persistence or later JVE submission fails after a Receipt is saved, THEN the Receipt_Authorization_Service SHALL preserve the original Receipt and SHALL expose the failure through the approved status and error contract.
12. IF authorization persistence or later JVE submission fails after a Receipt is saved, THEN the Receipt_Authorization_Service SHALL not report the Receipt as `Posted`.

### Requirement 4: Authorize a Receipt with Maker-Checker Control

**User Story:** As an Authorized Approver or Branch Manager, I want to authorize a verified receipt, so that the approved receipt can enter financial posting.

#### Acceptance Criteria

1. WHEN a permitted Checker submits authorization for a Receipt in `PendingAuthorization` with a matching Expected Version, THE Receipt_Authorization_Service SHALL record Authorization Decision `Authorized`.
2. WHEN a permitted Checker submits authorization for a Receipt in `PendingAuthorization` with a matching Expected Version, THE Receipt_Authorization_Service SHALL record the Checker identity from the validated JWT.
3. WHEN a permitted Checker submits authorization for a Receipt in `PendingAuthorization` with a matching Expected Version, THE Receipt_Authorization_Service SHALL record the UTC authorization timestamp.
4. WHEN a permitted Checker submits authorization for a Receipt in `PendingAuthorization` with a matching Expected Version, THE Receipt_Authorization_Service SHALL record the supplied decision reason when present.
5. WHEN a permitted Checker submits authorization for a Receipt in `PendingAuthorization` with a matching Expected Version, THE Receipt_Authorization_Service SHALL commit the decision metadata, audit entry, and approved Posting Request Identity in one Atomic Authorization Transaction.
6. WHEN an authorization transaction commits successfully, THE Receipt_Authorization_Service SHALL return Authorization Status `Authorized` and Posting Status `PostingPending`.
7. WHEN an authorization transaction commits successfully, THE Receipt_Authorization_Service SHALL return the Posting Request Identity, Checker identity, and updated Expected Version.
8. WHEN an authorization request succeeds, THE Receipt_Authorization_Service SHALL submit exactly one immutable Posting Request to the JVE containing the complete receipt aggregate and all required inter-branch context.
9. WHEN a Receipt contains multiple account lines, THE Receipt_Authorization_Service SHALL authorize the complete Receipt as one unit.
10. WHEN a Receipt contains multiple account lines, THE Receipt_Authorization_Service SHALL include every account line in the single approved Posting Request.
11. IF a Receipt is not in `PendingAuthorization`, THEN THE Receipt_Authorization_Service SHALL reject the authorization request with a status-transition error and SHALL create no second decision or Posting Request.
12. IF the Checker lacks the required role or organizational scope, THEN THE Receipt_Authorization_Service SHALL reject the authorization request and SHALL modify neither the Receipt, audit trail, nor Posting Request.
13. WHEN a Checker authorizes a Receipt, THE Receipt_Authorization_Service SHALL use the Checker identity from the validated JWT and SHALL ignore any Checker identity in the request body.
14. WHEN the Receipt_Authorization_UI receives a successful authorization response, THE Receipt_Authorization_UI SHALL display `Authorized` and `Posting Pending`.
15. WHEN the Receipt_Authorization_UI receives a successful authorization response, THE Receipt_Authorization_UI SHALL disable further authorization and rejection actions.
16. WHEN the Receipt_Authorization_UI receives a successful authorization response, THE Receipt_Authorization_UI SHALL retain the review details.
17. IF the approved JVE boundary reports a synchronous submission rejection before the Atomic Authorization Transaction commits, THEN the Receipt_Authorization_Service SHALL reject the authorization operation.
18. IF the approved JVE boundary reports a synchronous submission rejection before the Atomic Authorization Transaction commits, THEN the Receipt_Authorization_Service SHALL leave the Receipt in `PendingAuthorization` and SHALL create no successful authorization audit entry.
19. IF the authorization transaction commits before final JVE posting completion, THEN the Receipt_Authorization_Service SHALL retain Authorization Decision `Authorized`.
20. IF the authorization transaction commits before final JVE posting completion, THEN the Receipt_Authorization_Service SHALL expose the JVE failure or retry status and SHALL not revert the authorization decision.

### Requirement 5: Reject a Receipt

**User Story:** As an Authorized Approver or Branch Manager, I want to reject an invalid receipt, so that the receipt cannot be posted without correction and re-review.

#### Acceptance Criteria

1. WHEN a permitted Checker submits rejection for a Receipt in `PendingAuthorization` with a matching Expected Version and a non-empty English reason, THE Receipt_Authorization_Service SHALL record Authorization Decision `Rejected` or the approved `Revoked` mapping.
2. WHEN a permitted Checker submits rejection for a Receipt in `PendingAuthorization` with a matching Expected Version and a non-empty English reason, THE Receipt_Authorization_Service SHALL record the Checker identity.
3. WHEN a permitted Checker submits rejection for a Receipt in `PendingAuthorization` with a matching Expected Version and a non-empty English reason, THE Receipt_Authorization_Service SHALL record the UTC decision timestamp and rejection reason.
4. WHEN a permitted Checker submits rejection for a Receipt in `PendingAuthorization` with a matching Expected Version and a non-empty English reason, THE Receipt_Authorization_Service SHALL commit the decision metadata and audit entry in one Atomic Authorization Transaction.
5. WHEN a rejection transaction commits successfully, THE Receipt_Authorization_Service SHALL create no JVE Posting Request.
6. WHEN a rejection transaction commits successfully, THE Receipt_Authorization_Service SHALL create no canonical journal and SHALL return Posting Status `NotSubmitted`.
7. IF a rejection request omits a reason or supplies a reason longer than the configured maximum, THEN THE Receipt_Authorization_Service SHALL return a validation error and SHALL leave the Receipt unchanged.
8. IF a Receipt is not in `PendingAuthorization`, THEN THE Receipt_Authorization_Service SHALL reject the rejection request with a status-transition error and SHALL leave the existing decision unchanged.
9. WHEN the Receipt_Authorization_UI receives a successful rejection response, THE Receipt_Authorization_UI SHALL display the rejection status and reason.
10. WHEN the Receipt_Authorization_UI receives a successful rejection response, THE Receipt_Authorization_UI SHALL disable Regular Printing and posting actions.
11. WHILE a Receipt has a rejected or revoked Authorization Decision, THE Receipt_Authorization_Service SHALL prevent Financial Posting unless a separately approved reopen workflow changes the decision.
12. WHEN a Checker rejects a Third-Party Receipt, THE Receipt_Authorization_Service SHALL reject the complete Receipt and SHALL create no decision for an individual account line.

### Requirement 6: Prevent Self-Authorization

**User Story:** As a compliance stakeholder, I want a Maker prevented from authorizing the Maker’s own receipt, so that maker-checker separation remains enforceable.

#### Acceptance Criteria

1. IF the authenticated Checker identity equals the immutable Maker identity of the Receipt, THEN THE Receipt_Authorization_Service SHALL deny authorization with HTTP status 422.
2. IF the authenticated Checker identity equals the immutable Maker identity of the Receipt, THEN THE Receipt_Authorization_Service SHALL return the exact message `Maker cannot authorize their own receipt`.
3. IF the authenticated Checker identity equals the immutable Maker identity of the Receipt, THEN THE Receipt_Authorization_Service SHALL leave the Receipt status unchanged.
4. IF the authenticated Checker identity equals the immutable Maker identity of the Receipt, THEN THE Receipt_Authorization_Service SHALL create no Posting Request or canonical journal.
5. IF the authenticated Checker identity equals the immutable Maker identity of the Receipt, THEN THE Receipt_Authorization_Service SHALL create no successful authorization audit entry.
6. WHEN the Receipt_Authorization_UI receives the self-authorization error, THE Receipt_Authorization_UI SHALL display exactly `Maker cannot authorize their own receipt`.
7. WHEN the Receipt_Authorization_UI receives the self-authorization error, THE Receipt_Authorization_UI SHALL keep the Receipt available for another permitted Checker.
8. WHEN the Maker attempts to reject the Maker’s own Receipt, THE Receipt_Authorization_Service SHALL apply the approved workflow policy for Maker rejection.
9. WHEN the Maker attempts to reject the Maker’s own Receipt, THE Receipt_Authorization_Service SHALL not bypass the self-authorization control for an authorization decision.
10. WHEN identity comparison is performed, THE Receipt_Authorization_Service SHALL compare stable user identifiers from the validated token and Receipt record.
11. WHEN identity comparison is performed, THE Receipt_Authorization_Service SHALL not compare display names, email addresses, or request-body labels.

### Requirement 7: Submit the Inter-Branch Posting Contract

**User Story:** As a financial controller, I want an authorized cross-branch receipt represented completely, so that the JVE can post both branch books without inferring missing context.

#### Acceptance Criteria

1. WHEN an authorized Receipt line has a Collection Branch different from its Account Branch, THE Receipt_Authorization_Service SHALL include one Inter-Branch Posting Contract for that line in the approved Posting Request.
2. WHEN an Inter-Branch Posting Contract is created, THE Receipt_Authorization_Service SHALL include the Receipt identifier, line identifier, Company, Collection Branch, Account Branch, account identifier, receipt component, amount, currency `INR`, Value Date, authorization actor, authorization timestamp, Workflow Rule identity, and immutable receipt version or hash.
3. WHEN a Receipt line has the same Collection Branch and Account Branch, THE Receipt_Authorization_Service SHALL mark the line as Same-Branch.
4. WHEN a Receipt line has the same Collection Branch and Account Branch, THE Receipt_Authorization_Service SHALL create no inter-branch control requirement for that line.
5. WHEN an authorized Receipt has multiple account lines with different Account Branch values, THE Receipt_Authorization_Service SHALL include one independently traceable branch context for every line.
6. WHEN an authorized Receipt has multiple account lines, THE Receipt_Authorization_Service SHALL preserve the aggregate receipt amount.
7. IF an Account Branch is missing, inactive, outside the Receipt Company, or unresolved from the authoritative Loan Account relationship, THEN THE Receipt_Authorization_Service SHALL reject authorization with a configuration or business-rule error.
8. IF an Account Branch is missing, inactive, outside the Receipt Company, or unresolved from the authoritative Loan Account relationship, THEN THE Receipt_Authorization_Service SHALL create no Posting Request.
9. IF the Collection Branch or Account Branch is not an active Level 5 branch, THEN THE Receipt_Authorization_Service SHALL reject authorization and SHALL create no executable Posting Request.
10. WHEN the Receipt_Authorization_Service builds an Inter-Branch Posting Contract, THE Receipt_Authorization_Service SHALL select no GL code from a hard-coded branch name or client-supplied GL value.
11. WHEN the Receipt_Authorization_Service submits an Inter-Branch Posting Contract, THE Receipt_Authorization_Service SHALL identify the Cashflow event and schema version required by the JVE.

### Requirement 8: Post Balanced Inter-Branch Entries Through the JVE

**User Story:** As a Financial Controller, I want cross-branch entries balanced in both branch books, so that the collection branch and account branch remain reconciled.

#### Acceptance Criteria

1. WHEN the JVE processes an authorized Inter-Branch Line for Collection Branch Barka and Account Branch Muscat, THE JVE SHALL create a collection-book debit to the configured Barka Cashier Account.
2. WHEN the JVE processes an authorized Inter-Branch Line for Collection Branch Barka and Account Branch Muscat, THE JVE SHALL create a collection-book credit to the configured Muscat Inter-Branch Control Account.
3. WHEN the JVE processes an authorized Inter-Branch Line for Collection Branch Barka and Account Branch Muscat, THE JVE SHALL create an account-book debit to the configured Muscat Inter-Branch Control Account.
4. WHEN the JVE processes an authorized Inter-Branch Line for Collection Branch Barka and Account Branch Muscat, THE JVE SHALL create an account-book credit to the configured Muscat Loan Installment Account.
5. WHEN the JVE processes an Inter-Branch Line, THE JVE SHALL use equal INR amounts for the collection-book pair and account-book pair after two-decimal rounding.
6. WHEN the JVE completes an Inter-Branch Posting Contract, THE JVE SHALL require total debits to equal total credits for the complete Posting Request to the smallest configured INR unit.
7. WHEN an authorized Receipt contains both Same-Branch and Inter-Branch Lines, THE JVE SHALL apply the applicable Cashflow Master rule to each line.
8. WHEN an authorized Receipt contains both Same-Branch and Inter-Branch Lines, THE JVE SHALL preserve reconciliation from every journal line to its source Receipt line.
9. IF a configured Cashier Account, Inter-Branch Control Account, or Loan Installment Account is missing, inactive, non-postable, outside the Company, outside the required branch book, or ambiguous, THEN THE JVE SHALL classify the request under the approved permanent-failure contract.
10. IF a configured Cashier Account, Inter-Branch Control Account, or Loan Installment Account is invalid, THEN THE JVE SHALL create no partial journal and SHALL retain the request for operational investigation.
11. IF the JVE cannot resolve an applicable Cashflow Master rule for an Inter-Branch Posting Contract, THEN THE JVE SHALL fail closed.
12. IF the JVE cannot resolve an applicable Cashflow Master rule for an Inter-Branch Posting Contract, THEN THE JVE SHALL infer no debit or credit account and SHALL create no canonical journal.
13. IF an inter-branch journal transaction fails before commit, THEN THE JVE SHALL roll back collection-book entries, account-book entries, source link, audit data, and final `Posted` outcome from that transaction.
14. WHEN the JVE posts an Inter-Branch Posting Contract successfully, THE JVE SHALL persist the canonical journal identifier, source Receipt identifier, Collection Branch, Account Branch, line identifier, rule version, branch-book context, debit total, credit total, and resolved posting actor for reconciliation.
15. WHEN the JVE reports successful posting for a complete Receipt, THE Receipt_Authorization_Service SHALL update Posting Status to `Posted` or the approved equivalent without changing the historical Authorization Decision.

### Requirement 9: Maintain the Synchronous Decision and Asynchronous Posting Boundary

**User Story:** As an operations user, I want authorization and financial posting states separated, so that the UI accurately represents a decision that is accepted while posting is still processing.

#### Acceptance Criteria

1. WHEN authorization succeeds, THE Receipt_Authorization_Service SHALL complete the synchronous Authorization Decision before the JVE completes asynchronous Financial Posting.
2. WHILE a Receipt has Authorization Status `Authorized` and Posting Status `PostingPending`, THE Receipt_Authorization_Service SHALL allow read-only status and audit queries.
3. WHILE a Receipt has Authorization Status `Authorized` and Posting Status `PostingPending`, THE Receipt_Authorization_Service SHALL prevent a second authorization, rejection, or Posting Request submission.
4. WHEN the JVE returns `RetryPending`, THE Receipt_Authorization_Service SHALL retain Authorization Decision `Authorized`.
5. WHEN the JVE returns `RetryPending`, THE Receipt_Authorization_Service SHALL expose the retry status and safe failure information.
6. WHEN the JVE returns `RetryPending`, THE Receipt_Authorization_Service SHALL create no second Posting Request.
7. WHEN the JVE returns `DeadLetter`, THE Receipt_Authorization_Service SHALL retain Authorization Decision `Authorized`.
8. WHEN the JVE returns `DeadLetter`, THE Receipt_Authorization_Service SHALL mark Posting Status `Failed` or the approved equivalent.
9. WHEN the JVE returns `DeadLetter`, THE Receipt_Authorization_Service SHALL expose an operational recovery reference and keep Regular Printing locked.
10. WHEN the JVE returns `Posted`, THE Receipt_Authorization_Service SHALL mark Posting Status `Posted` and store the canonical journal identifier.
11. WHEN the JVE returns `Posted`, THE Receipt_Authorization_Service SHALL unlock Regular Printing.
12. IF a status callback or reconciliation message identifies a Posting Request that does not match the Receipt’s immutable request identity or receipt version, THEN THE Receipt_Authorization_Service SHALL reject the update.
13. IF a status callback or reconciliation message identifies a Posting Request that does not match the Receipt’s immutable request identity or receipt version, THEN THE Receipt_Authorization_Service SHALL preserve the current Receipt state and record an integrity event.
14. THE Receipt_Authorization_Service SHALL not report a Receipt as `Posted` solely because the Receipt is `Authorized` or a Posting Request was accepted by the JVE inbox.
15. WHEN the Receipt_Authorization_UI displays an authorized Receipt before final posting, THE Receipt_Authorization_UI SHALL show `Authorized` and `Posting Pending` as separate states from `Posted`.

### Requirement 10: Control Receipt Printing and Unlocking

**User Story:** As an operations user, I want regular printing restricted to verified and posted receipts, so that unapproved or unposted documents cannot be presented as final receipts.

#### Acceptance Criteria

1. WHILE a Receipt has Authorization Status `PendingAuthorization`, THE Receipt_Authorization_Service SHALL deny Regular Printing.
2. WHILE a Receipt has a rejected or revoked Authorization Decision, THE Receipt_Authorization_Service SHALL deny Regular Printing.
3. WHILE a Receipt has Posting Status `PostingPending`, `RetryPending`, or `Failed`, THE Receipt_Authorization_Service SHALL deny Regular Printing.
4. WHEN a Receipt has Authorization Decision `Authorized`, Posting Status `Posted`, and a durable canonical journal identifier, THE Receipt_Authorization_Service SHALL allow Regular Printing to an authorized caller.
5. WHEN a Receipt has Posting Status `Posted`, THE Receipt_Authorization_Service SHALL return the printing-eligibility flag and posting reference in the receipt detail response.
6. IF a caller requests Regular Printing for a Receipt that does not satisfy the printing gate, THEN THE Receipt_Authorization_Service SHALL return a business-rule error.
7. IF a caller requests Regular Printing for a Receipt that does not satisfy the printing gate, THEN THE Receipt_Authorization_Service SHALL produce no final printable document.
8. WHEN a Third-Party Receipt has one account line requiring authorization, THE Receipt_Authorization_Service SHALL keep Regular Printing locked for the complete Receipt.
9. WHEN a Third-Party Receipt has one account line requiring authorization, THE Receipt_Authorization_Service SHALL unlock Regular Printing only after the complete Receipt is authorized and successfully posted.
10. WHERE Preview Print is enabled by approved configuration, THE Receipt_Authorization_Service SHALL label the output as a preview.
11. WHERE Preview Print is enabled by approved configuration, THE Receipt_Authorization_Service SHALL exclude any indication that the Receipt is authorized, posted, or available for Regular Printing.
12. WHEN the Receipt_Authorization_UI displays a locked receipt, THE Receipt_Authorization_UI SHALL show the English reason for the lock and disable Regular Print.
13. WHEN the Receipt_Authorization_UI receives a Posted status with a valid journal reference, THE Receipt_Authorization_UI SHALL enable Regular Print and display the final posting reference.

### Requirement 11: Authorization API and UI Contracts

**User Story:** As an Authorized Approver or Branch Manager, I want a focused authorization screen, so that review and decision actions are clear and auditable.

#### Acceptance Criteria

1. THE Receipt_Authorization_Service SHALL expose review-list, receipt-review-detail, authorize, reject, authorization-audit, posting-status, and Regular Printing eligibility operations through the existing receipt gateway origin.
2. THE Receipt_Authorization_Service SHALL accept Receipt identifier, Expected Version, ClientRequestId, and optional decision reason in an authorize request.
3. THE Receipt_Authorization_Service SHALL accept no Checker identity in an authorize request body.
4. THE Receipt_Authorization_Service SHALL accept Receipt identifier, Expected Version, ClientRequestId, and required rejection reason in a reject request.
5. THE Receipt_Authorization_Service SHALL accept no Checker identity in a reject request body.
6. WHEN an authorize or reject request succeeds, THE Receipt_Authorization_Service SHALL return Receipt identifier, Authorization Decision, Posting Status, Decision Actor, DecisionAtUtc, Expected Version, ClientRequestId outcome, and Posting Request Identity when available.
7. THE Receipt_Authorization_UI SHALL provide a Receipt Authorization screen with a pending review list, search and filters, read-only receipt details, Account Branch per line, audit trail, Authorize action, Reject action, rejection-reason input, Posting Status indicator, and Regular Print action.
8. WHEN the Receipt_Authorization_UI opens a pending Receipt, THE Receipt_Authorization_UI SHALL capture Expected Version.
9. WHEN the Receipt_Authorization_UI submits a decision request, THE Receipt_Authorization_UI SHALL send the captured Expected Version.
10. WHEN the Receipt_Authorization_UI submits Authorize or Reject, THE Receipt_Authorization_UI SHALL disable duplicate submission until a response or recoverable timeout is received.
11. IF the Receipt_Authorization_Service returns an error, THEN THE Receipt_Authorization_UI SHALL display the English message and preserve the review context.
12. IF the Receipt_Authorization_Service reports stale data, THEN THE Receipt_Authorization_UI SHALL refresh the Receipt version and SHALL not display a successful decision.
13. WHEN `VITE_USE_MOCK_API` is `true`, THE Receipt_Authorization_UI SHALL use the mock authorization service.
14. WHEN `VITE_USE_MOCK_API` is not `true`, THE Receipt_Authorization_UI SHALL use the real authorization service through the shared axios instance.
15. THE Receipt_Authorization_UI SHALL display monetary values as INR with two decimal places.
16. THE Receipt_Authorization_UI SHALL display technical timestamps in UTC or an explicitly labelled India business-date format.
17. THE Receipt_Authorization_UI SHALL contain no RTL handling.

### Requirement 12: Authorization, Authentication, and Scope Security

**User Story:** As a security stakeholder, I want only permitted Checkers to decide receipts, so that the maker-checker control cannot be bypassed.

#### Acceptance Criteria

1. IF an authorization endpoint request carries a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Receipt_Authorization_Service SHALL return HTTP status 401.
2. IF an authorization endpoint request carries an invalid JWT Bearer token, THEN THE Receipt_Authorization_Service SHALL modify no Receipt, audit entry, Posting Request, or journal.
3. IF an authenticated user lacks the approved AuthorizedApprover, BranchManager, or explicitly permitted SystemAdmin role, THEN THE Receipt_Authorization_Service SHALL return HTTP status 403.
4. IF an authenticated user lacks an approved authorization role, THEN THE Receipt_Authorization_Service SHALL modify no Receipt, audit entry, Posting Request, or journal.
5. WHEN an authenticated user has an approved Checker role and organizational scope, THE Receipt_Authorization_Service SHALL permit review and decision operations subject to Requirements 1 through 11.
6. IF a Checker’s Company, Line of Business, or Branch scope excludes the Receipt, THEN THE Receipt_Authorization_Service SHALL return HTTP status 403.
7. IF a Checker’s Company, Line of Business, or Branch scope excludes the Receipt, THEN THE Receipt_Authorization_Service SHALL not reveal whether an inaccessible Receipt exists.
8. THE Receipt_Authorization_Service SHALL take Maker, Checker, and acting-user identities from validated token claims and immutable receipt data.
9. THE Receipt_Authorization_Service SHALL ignore client-supplied identity values.
10. THE Receipt_Authorization_Service SHALL enforce the Maker-checker rule for Temporary, Regular, and Third-Party receipts that require authorization.
11. IF a user attempts to authorize through a client-side route or direct HTTP request without server-side permission, THEN THE Receipt_Authorization_Service SHALL reject the operation and leave financial records unchanged.
12. THE Receipt_Authorization_Service SHALL exclude credentials, bearer tokens, full instrument numbers, unnecessary customer contact data, and unredacted financial account details from authorization logs and audit summaries.
13. THE Finnova.ApiGateway SHALL forward the Authorization header for authorization, posting-status, audit, and printing-eligibility requests to the correct backend host.

### Requirement 13: Audit Authorization and Posting Lifecycle

**User Story:** As an auditor, I want every authorization decision and posting outcome traceable, so that a receipt can be reconciled from Maker entry through final journal posting.

#### Acceptance Criteria

1. WHEN a Checker successfully authorizes a Receipt, THE Receipt_Authorization_Service SHALL write exactly one immutable Authorization Audit Entry.
2. WHEN an authorization audit entry is written, THE entry SHALL contain Receipt identifier, prior Authorization Decision, new Authorization Decision, Maker identity, Checker identity, DecisionAtUtc, Expected Version, correlation identifier, Posting Request Identity, and a non-sensitive change summary.
3. WHEN a Checker successfully rejects a Receipt, THE Receipt_Authorization_Service SHALL write exactly one immutable Authorization Audit Entry containing the rejection reason and applicable traceability fields.
4. WHEN self-authorization, stale decision, invalid status, unauthorized request, or validation fails, THE Receipt_Authorization_Service SHALL write zero successful decision Audit Entries.
5. WHEN self-authorization, stale decision, invalid status, unauthorized request, or validation fails, THE Receipt_Authorization_Service SHALL create no Posting Request.
6. WHEN the JVE changes Posting Status, THE Receipt_Authorization_Service SHALL append an immutable posting-lifecycle audit reference.
7. WHEN a posting-lifecycle audit reference is written, THE reference SHALL contain prior Posting Status, new Posting Status, JVE request identity, safe outcome code, canonical journal identifier when available, and UTC timestamp.
8. WHEN an authorized reader requests the authorization audit trail, THE Receipt_Authorization_Service SHALL return entries ordered by OccurredAtUtc descending and Entry identifier descending.
9. THE Receipt_Authorization_Service SHALL expose no normal operation that updates or deletes an Authorization Audit Entry.
10. WHEN an auditor requests a Receipt trace, THE Receipt_Authorization_Service SHALL relate FINNOVA-61 creation audit, authorization decision audit, JVE Posting Request, JVE attempt history, canonical journal identifier, and printing-unlock event.
11. WHEN an auditor requests a Receipt trace, THE Receipt_Authorization_Service SHALL exclude prohibited secrets.
12. IF a Receipt has `Posted` status without a canonical journal identifier or source link, THEN THE Receipt_Authorization_Service SHALL report an integrity alert.
13. IF a Receipt has `Posted` status without a canonical journal identifier or source link, THEN THE Receipt_Authorization_Service SHALL keep Regular Printing locked.

### Requirement 14: Concurrency and Idempotency

**User Story:** As a platform engineer, I want repeated or concurrent authorization requests handled safely, so that one Receipt cannot receive two decisions or two postings.

#### Acceptance Criteria

1. WHEN two Checkers submit authorization requests concurrently for the same pending Receipt version, THE Receipt_Authorization_Service SHALL commit at most one Authorization Decision.
2. WHEN two Checkers submit authorization requests concurrently for the same pending Receipt version, THE Receipt_Authorization_Service SHALL create at most one Posting Request.
3. WHEN one Checker submits authorization and another submits rejection concurrently for the same pending Receipt version, THE Receipt_Authorization_Service SHALL commit at most one terminal decision.
4. WHEN one Checker submits authorization and another submits rejection concurrently for the same pending Receipt version, THE Receipt_Authorization_Service SHALL return a stale or conflict response to the losing request.
5. WHEN an authorization request carries a ClientRequestId associated with a successful authorization, THE Receipt_Authorization_Service SHALL return the existing decision and Posting Request Identity.
6. WHEN an authorization request carries a ClientRequestId associated with a successful authorization, THE Receipt_Authorization_Service SHALL create no duplicate decision, audit entry, or Posting Request.
7. WHEN a rejection request carries a ClientRequestId associated with a successful rejection, THE Receipt_Authorization_Service SHALL return the existing rejection outcome.
8. WHEN a rejection request carries a ClientRequestId associated with a successful rejection, THE Receipt_Authorization_Service SHALL create no duplicate decision or audit entry.
9. IF a ClientRequestId is reused with a different Receipt identifier, Expected Version, decision, or request payload, THEN THE Receipt_Authorization_Service SHALL reject the request as an idempotency conflict and preserve the original outcome.
10. IF an authorization response is uncertain, THEN THE Receipt_Authorization_Service SHALL look up ClientRequestId and Receipt Posting Request Identity before retrying.
11. IF an authorization response is uncertain, THEN THE Receipt_Authorization_Service SHALL not submit a second Posting Request.
12. WHEN the JVE retries an approved Posting Request, THE JVE SHALL preserve immutable Receipt identifier, receipt version or hash, inter-branch line contexts, authorization actor, and decision timestamp.
13. THE Receipt_Authorization_Service SHALL maintain the invariant that one Receipt version has at most one terminal Authorization Decision and one approved Posting Request.

### Requirement 15: Error Contract and Failure Handling

**User Story:** As a UI developer and operations user, I want stable authorization errors, so that the UI can explain failures and prevent unsafe retries.

#### Acceptance Criteria

1. IF an authorization request fails FluentValidation, THEN THE Receipt_Authorization_Service SHALL return an RFC 7807 problem response with HTTP status 400.
2. IF an authorization request fails FluentValidation, THEN the problem response SHALL contain code `ERR-RCA-400`, an English message, field details, and correlationId.
3. IF a Receipt or required reference record cannot be found, THEN THE Receipt_Authorization_Service SHALL return HTTP status 404 with code `ERR-RCA-404`, an English message, and correlationId.
4. IF an authorization request fails role, branch, Company, or Line of Business access, THEN THE Receipt_Authorization_Service SHALL return HTTP status 403 with code `ERR-RCA-403` and SHALL disclose no inaccessible record details.
5. IF the Maker attempts to authorize the Maker’s own Receipt, THEN THE Receipt_Authorization_Service SHALL return HTTP status 422 with code `ERR-RCA-422`, rule `MAKER_CANNOT_AUTHORIZE_OWN_RECEIPT`, and exact message `Maker cannot authorize their own receipt`.
6. IF an authorization request fails because of a stale Expected Version or concurrent terminal decision, THEN THE Receipt_Authorization_Service SHALL return HTTP status 409 with code `ERR-RCA-409` and identify the need to refresh the Receipt.
7. IF a Receipt is in a status that does not permit the requested decision, THEN THE Receipt_Authorization_Service SHALL return HTTP status 422 with code `ERR-RCA-422` and rule `INVALID_AUTHORIZATION_STATE`.
8. IF workflow, account-branch, cashier-account, or inter-branch-control configuration is missing or ambiguous before submission, THEN THE Receipt_Authorization_Service SHALL return HTTP status 422 with code `ERR-RCA-422` and a stable rule key identifying the missing configuration.
9. IF the JVE rejects or dead-letters a Posting Request after authorization, THEN THE Receipt_Authorization_Service SHALL preserve Authorization Decision `Authorized`.
10. IF the JVE rejects or dead-letters a Posting Request after authorization, THEN THE Receipt_Authorization_Service SHALL expose a safe Posting Status failure and SHALL not report a false rejection of the Checker’s decision.
11. IF an unexpected Receipt_Authorization_Service error occurs, THEN THE Receipt_Authorization_Service SHALL return HTTP status 500 with code `ERR-RCA-500`, a generic English message, and correlationId without internal details.
12. THE Receipt_Authorization_Service SHALL include `code`, `message`, `correlationId`, and a stable `rule` extension where a business rule applies to every authorization problem response.

### Requirement 16: Reference Data and Configuration Dependencies

**User Story:** As a platform owner, I want authorization and inter-branch posting to use authoritative configuration, so that branch accounting cannot drift from master data.

#### Acceptance Criteria

1. THE Receipt_Authorization_Service SHALL resolve Maker, Checker role, Company, Line of Business, Collection Branch, Account Branch, Workflow Rule, and JVE posting contract from authoritative platform records or approved versioned contracts.
2. THE Receipt_Authorization_Service SHALL treat no client-supplied Account Branch, Cashier Account, Inter-Branch Control Account, Cashflow Rule version, or Checker identity as authoritative.
3. WHEN an Account Branch is resolved, THE Receipt_Authorization_Service SHALL verify that the Account Branch belongs to the same Company as the Receipt unless an approved inter-company contract permits otherwise.
4. WHEN inter-branch account configuration is resolved, THE Receipt_Authorization_Service SHALL require active configuration for Company, Collection Branch, Account Branch, Currency, and applicable Cashflow event.
5. IF GL Master marks a configured account inactive, unauthorized, non-postable, expired, or incompatible with the Company, THEN THE owning boundary SHALL reject the posting path according to the confirmed ownership boundary.
6. IF GL Master marks a configured account invalid, THEN THE owning boundary SHALL create no partial journal.
7. WHEN the Receipt_Authorization_Service reads reference data for the UI, THE Receipt_Authorization_Service SHALL expose only active workflow-relevant roles, branches, Account Branch values, safe Posting Status values, and approved configuration labels.
8. THE Receipt_Authorization_Service SHALL use one English display value per reference field.
9. THE Receipt_Authorization_Service SHALL create no bilingual or language-suffixed reference fields.
10. IF Cashflow Master, GL Master, branch configuration, cashier-account configuration, or inter-branch-control configuration is unavailable, THEN the owning boundary SHALL fail closed and provide an operationally traceable safe error.

### Requirement 17: Acceptance Scenarios

**User Story:** As a stakeholder, I want the FINNOVA-63 business examples represented as executable behavior, so that implementation and testing can verify the complete workflow.

#### Acceptance Criteria

1. WHEN Cashier `cashier_barka` creates Receipt `REC-101` at Barka for Contract `ACC-889021` whose Account Branch is Muscat, THE Receipt_Authorization_Service SHALL retain `PendingAuthorization`.
2. WHEN Cashier `cashier_barka` creates Receipt `REC-101` at Barka for Contract `ACC-889021` whose Account Branch is Muscat, THE Receipt_Authorization_Service SHALL create no financial journal.
3. WHEN Cashier `cashier_barka` creates Receipt `REC-101` at Barka for Contract `ACC-889021` whose Account Branch is Muscat, THE Receipt_Authorization_Service SHALL keep Regular Printing locked.
4. WHEN an authorized supervisor other than `cashier_barka` reviews `REC-101` and submits authorization with the current Expected Version, THE Receipt_Authorization_Service SHALL record Authorization Decision `Authorized`.
5. WHEN an authorized supervisor other than `cashier_barka` reviews `REC-101` and submits authorization with the current Expected Version, THE Receipt_Authorization_Service SHALL return Posting Status `PostingPending`.
6. WHEN an authorized supervisor other than `cashier_barka` reviews `REC-101` and submits authorization with the current Expected Version, THE Receipt_Authorization_Service SHALL submit one complete Posting Request to the JVE.
7. WHEN the JVE successfully posts `REC-101`, THE JVE SHALL create balanced collection-book and account-book entries for the Barka-to-Muscat Inter-Branch Posting Contract.
8. WHEN the JVE successfully posts `REC-101`, THE Receipt_Authorization_Service SHALL report Posting Status `Posted` and unlock Regular Printing.
9. WHEN `cashier_barka` attempts to authorize `REC-101`, THE Receipt_Authorization_Service SHALL deny the attempt with exact message `Maker cannot authorize their own receipt`.
10. WHEN `cashier_barka` attempts to authorize `REC-101`, THE Receipt_Authorization_Service SHALL preserve `PendingAuthorization` and keep Regular Printing locked.
11. WHEN a Third-Party Receipt contains two account lines and one line warrants authorization, THE Receipt_Authorization_Service SHALL hold and authorize the complete Receipt as one unit.
12. WHEN a Third-Party Receipt contains two account lines and one line warrants authorization, THE Receipt_Authorization_Service SHALL submit no subset of account lines.
13. WHEN a Third-Party Receipt contains two account lines and one line warrants authorization, THE Receipt_Authorization_Service SHALL unlock Regular Printing only after complete JVE posting succeeds.
14. WHEN an authorized Receipt contains two Inter-Branch Lines for different Account Branches, THE JVE SHALL produce one reconciled branch context per line.
15. WHEN an authorized Receipt contains two Inter-Branch Lines for different Account Branches, THE JVE SHALL require balanced totals for the complete Receipt before reporting `Posted`.

### Requirement 18: Testability and Platform Compatibility

**User Story:** As a delivery team, I want the feature’s behavior represented by automated tests, so that maker-checker and accounting controls remain enforceable during change.

#### Acceptance Criteria

1. THE Receipt_Authorization_Service SHALL expose pure or deterministic decision logic for workflow applicability, aggregate Third-Party authorization, self-authorization rejection, status transitions, printing eligibility, and branch-context construction.
2. THE backend test suite SHALL include example and edge-case tests for pending review, successful authorization, rejection, exact self-authorization message, inaccessible branch scope, stale Expected Version, duplicate ClientRequestId, missing inter-branch configuration, and JVE Posting Status transitions.
3. THE backend test suite SHALL include property-based tests with at least 100 iterations for the invariant that every approved Inter-Branch Posting Contract has equal collection-side and account-side amounts after INR two-decimal rounding.
4. THE backend test suite SHALL include property-based tests with at least 100 iterations for the invariant that aggregate line amounts remain unchanged by branch-context construction.
5. THE backend integration test suite SHALL verify authenticated API behavior.
6. THE backend integration test suite SHALL verify unauthorized API behavior.
7. THE backend integration test suite SHALL verify atomic authorization persistence, one Posting Request per Receipt, gateway-compatible routes, and printing eligibility after a Posted outcome.
8. THE frontend Vitest and React Testing Library suite SHALL verify review-list rendering, read-only detail display, aggregate Third-Party hold messaging, Authorize and Reject interactions, exact self-authorization error rendering, Posting Pending state, and printing unlock after Posted status.
