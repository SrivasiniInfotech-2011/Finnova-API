# Requirements Document

## Introduction

Receipt Processing Controls (Jira FINNOVA-64) enables authorized Branch Supervisors to correct operational receipt errors without breaking document continuity, auditability, or accounting integrity. The feature covers modification of eligible unauthorized receipts, cancellation of erroneous receipts, permanent retirement of cancelled document numbers, accounting reversal coordination when a receipt has already caused an accrual, and controlled reprinting of an already printed receipt.

The feature applies to receipts created by the FINNOVA-61 Receipt Processing capability. An unauthorized receipt may be corrected or cancelled only while the receipt remains in an eligible pre-authorization state and has no challan relationship. A challaned receipt is immutable through the receipt controls. An authorized receipt cannot be modified directly; an approved Cheque Return or JV reversal process is required for a financial correction. Cancellation changes the receipt status to `Cancelled`, records the acting supervisor, UTC timestamp, and mandatory reason, and stores an immutable retired-document record that prevents reuse of the document number. A reprint of an already printed receipt produces a prominent `DUPLICATE RECEIPT` title and watermark, assigns the next duplicate copy number, and increments the persisted Duplication Count.

The eventual implementation covers both repositories: the backend API in `E:\Finnova\Finnova-API` and the React + TypeScript frontend in `E:\Finnova\Finnova-UI\Finnova-UI`. The backend remains the source of truth for authorization, state transitions, document retirement, accounting-boundary invocation, audit persistence, and duplicate numbering. The frontend reflects server state, disables controls that the server says are unavailable, preserves displayed data after failed operations, and does not replace backend enforcement with client-only checks.

Finnova is an India-only, English-only financial platform. All user-facing labels, status values, validation messages, audit descriptions, sample values, and test examples defined by this specification are English-only. Monetary values use Indian Rupees (INR) with two fractional digits. Technical audit timestamps use Coordinated Universal Time (UTC). The feature introduces no bilingual fields, Arabic or other non-English localization, translation scaffolding, or right-to-left rendering.

## Glossary

- **Receipt**: The FINNOVA-61 financial document containing the customer, branch, receipt type and mode, document number, amount, account details, lifecycle status, and related operational data.
- **Receipt_Control_Service**: The backend API and application service responsible for receipt modification, cancellation, document retirement, duplicate printing, audit control, and coordination with the authoritative accounting boundary.
- **Receipt_Control_UI**: The Finnova-UI screens and controls that list, view, modify, cancel, and reprint receipts through the shared API service layer.
- **Receipt Aggregate**: The authoritative receipt record and its owned details, status, version, document identity, print state, and audit relationships.
- **Unauthorized Receipt**: A Receipt whose current authorization state permits supervisor correction or cancellation and whose status is not terminal. The exact status values are subject to Assumption A6.
- **Pending Authorization**: The FINNOVA-61 receipt state in which a saved receipt awaits an authorization decision. This state is the primary example of an Unauthorized Receipt in the acceptance scenarios.
- **Authorized Receipt**: A Receipt that has passed the approved authorization step or has otherwise entered a state in which direct receipt mutation is prohibited.
- **Challaned Receipt**: A Receipt linked to a challan document or challan identifier through the authoritative challan relationship.
- **Challan**: The authoritative banking or collection document associated with a receipt when the receipt has been challaned.
- **Branch Supervisor**: An authenticated user who holds an approved supervisor role and has organizational scope for the collection branch of the Receipt.
- **Authorized Supervisor Role**: A role approved to modify or cancel an eligible Unauthorized Receipt and to reprint a Receipt. The exact role names are subject to Assumption A4.
- **Authorization Scope**: The set of Company, Line of Business, Branch, and other organizational assignments that limit a user's permitted Receipt operations.
- **Receipt Status**: The lifecycle status of a Receipt, including at least `Pending Authorization` and `Cancelled`; the complete vocabulary is subject to Assumption A6.
- **Authorization Status**: The maker-checker decision state associated with a Receipt, such as pending, authorized, rejected, or revoked.
- **Cancellation**: The controlled operation that marks an eligible Receipt as `Cancelled`, records a reason and actor, retires the document number, and coordinates any required accounting reversal.
- **Cancellation Reason**: The mandatory English explanation supplied by an authorized supervisor for a Cancellation.
- **Document Number**: The unique business identifier printed on a Receipt, whether issued by an online sequence or entered from a manual receipt book.
- **Document Retirement Record**: An immutable record retaining a cancelled Document Number, Receipt identifier, Receipt type or numbering scope, cancellation actor, cancellation timestamp, and retirement reason so that the number cannot be reused.
- **Retired Document Number**: A Document Number held by a Document Retirement Record and permanently unavailable for a new Receipt within the applicable numbering scope.
- **Online Number**: A Document Number issued by the platform's document-numbering service.
- **Manual Number**: A Document Number entered by an authorized user from a controlled manual receipt book.
- **Printed Receipt**: A Receipt for which the authoritative print state records that a production receipt output has been issued.
- **Duplication Count**: The persisted number of duplicate copies issued for a Receipt. The initial value for a first regular print is subject to Assumption A7; the first duplicate copy is numbered `1`.
- **Duplicate Copy Number**: The sequential number assigned to a duplicate output, beginning at `1` for the first reprint of a Printed Receipt.
- **Duplicate Output**: The receipt PDF or print-ready response produced by a reprint operation and containing the exact title and watermark `DUPLICATE RECEIPT` plus the assigned Duplicate Copy Number.
- **Reprint**: A production print operation performed after the Receipt has already been printed once.
- **Accrual**: A previously persisted accounting recognition or financial posting attributable to a Receipt before Cancellation.
- **Accounting Reversal**: The compensating accounting operation that reverses an Accrual without deleting the original accounting record.
- **Cheque Return Boundary**: The approved service or workflow boundary through which an authorized receipt correction or reversal is initiated when the business event is a returned cheque.
- **JV Posting Engine**: The approved accounting boundary responsible for creating balanced canonical journal entries and reversal entries. The Receipt_Control_Service does not write canonical journal tables directly.
- **Reversal Request**: An immutable, idempotent request submitted to the Cheque Return Boundary or JV Posting Engine for an Accounting Reversal.
- **Receipt Audit Entry**: An immutable record of a receipt operation, including operation type, receipt identifier, Document Number, actor, UTC timestamp, reason or before-and-after values, and correlation data.
- **Receipt Version**: The optimistic-concurrency value used to ensure that a supervisor updates the Receipt version reviewed by the supervisor.
- **Idempotency Key**: A client-supplied identifier that lets the backend return the result of a previously completed operation without applying the operation twice.
- **Problem Details**: The platform error response containing an HTTP status, stable error code, English message, and operation context.
- **PaginatedResponse**: The platform response contract containing page data, total count, current page, page size, and total pages.
- **System Administrator**: An authenticated user carrying the approved `SystemAdmin` role claim.
- **EARS**: Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.

## Dependencies and Assumptions

The following dependencies and silent decisions require confirmation before design and implementation. The repository investigation found no implemented Receipt aggregate, receipt controls, receipt cancellation, receipt print endpoint, duplicate-print state, receipt-specific audit persistence, receipt authorization policy, or receipt gateway route. Existing receipt-related documents are planning artifacts only.

- **Dependency D1 — FINNOVA-61 Receipt Processing:** FINNOVA-61 is currently unimplemented. This feature assumes that FINNOVA-61 will provide one authoritative Receipt Aggregate, Receipt identifier, Document Number, Receipt Status, Authorization Status, Receipt details, audit relationship, print state, and receipt read contract. FINNOVA-64 will extend that aggregate and will not create a parallel Receipt table or parallel receipt API. Confirm the final FINNOVA-61 entity, status, numbering, audit, and print contracts before implementation.
- **Dependency D2 — FINNOVA-63 Authorization and Inter-Branch Accounting:** FINNOVA-63 is currently unimplemented. This feature assumes that FINNOVA-63 will provide the authoritative authorization state, maker-checker decision, posting state, and JV Posting Engine integration contract. FINNOVA-64 will consume those contracts and will not duplicate authorization or canonical journal models. Confirm the final FINNOVA-63 status vocabulary, posting-link fields, and integration boundary before implementation.
- **Assumption A1 — Backend host and gateway:** Receipt controls will be hosted in the existing `Finnova.AccountsService` beside Receipt Processing and will be reachable through the existing receipt gateway convention, including a UI alias under `/api/ua/api/receipts/**` when required by the shared UI axios base URL. Confirm host placement, controller route, gateway alias, and route rewriting after FINNOVA-61 is designed.
- **Assumption A2 — Receipt ownership:** FINNOVA-61 owns receipt capture, Receipt Aggregate identity, customer and branch snapshots, initial status, document-number issuance, and ordinary receipt reads. FINNOVA-64 owns only the control operations and fields required by this specification.
- **Assumption A3 — Modification surface:** An eligible Unauthorized Receipt may modify the business fields explicitly declared mutable by the final FINNOVA-61 contract. Receipt identity, Document Number, numbering mode, creation actor, creation timestamp, cancellation status, authorization decision, and existing accounting links are immutable through the modification operation. Confirm the exact mutable field set and whether modification returns the Receipt to `Pending Authorization` or keeps the current unauthorized status.
- **Assumption A4 — Authorization roles and scopes:** The proposed actor is a Branch Supervisor with an approved role such as `BranchSupervisor` or `BranchManager`. `BranchCollectionOfficer`, `AuthorizedApprover`, and `SystemAdmin` access is not assumed. Confirm the exact role names, whether a supervisor may act on a receipt created by the same user, and whether Company, Line of Business, Branch, or receipt-type scopes are required. The backend must enforce scopes from trusted user assignments rather than request-body values.
- **Assumption A5 — Challan relationship:** A receipt is Challaned when the authoritative FINNOVA-61 or future challan contract contains a durable link to a challan identifier. The receipt-control service will not infer challan state from free text, UI state, or a client-supplied boolean. Confirm the source of truth, relationship cardinality, and whether a receipt can become Challaned after creation.
- **Assumption A6 — Status vocabulary:** `Pending Authorization` and `Cancelled` are required business concepts. The final persisted values, separate Authorization Status versus Receipt Status fields, terminal-state rules, and the status used for other unauthorized receipts must be aligned with FINNOVA-61 and FINNOVA-63. Confirm whether `Rejected`, `Revoked`, `Authorized`, `Posted`, and `Posting Pending` are separate fields or one status enumeration.
- **Assumption A7 — Online and manual numbering retirement:** An Online Number remains retired after Cancellation even if the document-number sequence advances independently. A Manual Number remains retired in the same uniqueness scope as the original Receipt. Confirm whether uniqueness and retirement are scoped by Receipt Type, Company, Line of Business, Branch, numbering scheme, or one platform-wide namespace, and confirm how manual receipt-book gaps and ranges are recorded.
- **Assumption A8 — Cheque Return authority:** The investigation found no implemented Cheque Return boundary. This specification assumes the product will either identify an existing authoritative Cheque Return service or introduce one before authorized-receipt correction is implemented. Confirm whether a Cheque Return operation owns returned-cheque reversals, whether a generic JV Posting Engine reversal contract is sufficient, and which service owns the final business status.
- **Assumption A9 — Accounting reversal ownership:** The Receipt_Control_Service will submit an immutable Reversal Request through the confirmed Cheque Return Boundary or JV Posting Engine. The Receipt_Control_Service will not insert canonical journal headers, journal lines, balances, or reversal rows directly. Confirm the authoritative boundary, request schema, idempotency key, response status, retry contract, and source-link rules.
- **Assumption A10 — Accrual detection:** A Receipt has a prior Accrual when the authoritative accounting boundary exposes a durable receipt-to-accrual or receipt-to-posting link. The control service will not infer accrual state from receipt status alone. Confirm the source of truth and whether a partially posted or failed posting is considered an Accrual requiring reversal.
- **Assumption A11 — Cancellation reason:** Cancellation Reason is mandatory, English-only, trimmed, and bounded by a length to be confirmed during design. A proposed maximum is 500 characters. Confirm the maximum length, whether a controlled reason code is also required, and whether a free-text explanation is retained with the code.
- **Assumption A12 — Cancellation atomicity:** Receipt status, Receipt Audit Entry, Document Retirement Record, and the Reversal Request identity must commit atomically when cancellation requires a reversal. If the authoritative accounting boundary is asynchronous, the approved inbox or transactional-outbox contract must make the Reversal Request durable before the cancellation response reports success. Confirm the transaction and outbox boundary.
- **Assumption A13 — Reversal failure behavior:** If a required Reversal Request cannot be durably accepted, the Receipt remains in its pre-cancellation status, no successful cancellation audit entry is written, and no Document Retirement Record is committed. Confirm whether a separate `Cancellation Pending` state is required for asynchronous acceptance.
- **Assumption A14 — Print state:** FINNOVA-61 or FINNOVA-63 will provide an authoritative Printed Receipt state and the first regular-print operation. FINNOVA-64 will not count a browser preview or an uncommitted client-side print as a production print. Confirm the print event source, whether print state is immutable, and whether a cancelled or unposted receipt can be printed.
- **Assumption A15 — Duplicate count and copy numbering:** Duplication Count represents the number of successful duplicate outputs. The first duplicate has Duplicate Copy No `1`; every later duplicate increments by one. Confirm whether Duplication Count starts at `0` after the first regular print, whether the original print is counted, and whether duplicate numbering is per Receipt or per Document Number.
- **Assumption A16 — Reprint authorization:** A user may reprint only a Receipt within the user's Authorization Scope and only when the receipt's final print eligibility rules permit reprint. The exact role set, terminal-status restrictions, branch scope, and whether a supervisor approval is needed for each reprint require confirmation.
- **Assumption A17 — Reprint response:** A successful Reprint returns a print-ready response or document reference that contains the exact visible title `DUPLICATE RECEIPT`, a prominent repeated watermark with the same text, the original Document Number, and `Duplicate Copy No: N`. Confirm whether the backend returns a PDF, a signed document URL, a rendering token, or a structured print model.
- **Assumption A18 — Concurrency and idempotency:** Modification and Cancellation use Receipt Version or an equivalent compare-and-set condition. Reprint uses an atomic counter update and an Idempotency Key so concurrent requests cannot assign the same Duplicate Copy Number. Confirm the database concurrency token, retry behavior, and idempotency-key retention period.
- **Assumption A19 — Audit retention:** Receipt Audit Entries and Document Retirement Records are append-only and retained under the platform's financial-record retention policy. Normal receipt APIs expose no update or delete operation for either record. Confirm retention duration, archive rules, legal hold behavior, and whether audit records use the FINNOVA-61 audit table or a shared audit table.
- **Assumption A20 — UI architecture:** Finnova-UI will use React hooks and Context where needed, the shared axios instance in `src/services/api.ts`, feature service interfaces, real and mock implementations, the `VITE_USE_MOCK_API` toggle, MUI components, and Vitest + React Testing Library. No Redux, RxJS, ad-hoc axios client, or second authentication store will be introduced.
- **Assumption A21 — Product localization:** All status labels, control labels, warnings, reason text, errors, audit values, document output text, mock records, and tests remain English-only. All financial values use INR with two fractional digits. No RTL support is required.

## Requirements

### Requirement 1: Identify Receipt Control Eligibility

**User Story:** As a Branch Supervisor, I want the system to identify which receipts I may control, so that operational corrections are limited to authorized and structurally eligible records.

#### Acceptance Criteria

1. WHEN an authenticated user requests an in-scope Receipt control view, THE Receipt_Control_Service SHALL return the Receipt identifier, Document Number, Receipt Status, Authorization Status, Challan state, print state, Duplication Count, Receipt Version, collection branch, and permission decisions.
2. WHEN the Receipt_Control_Service calculates a permission decision, THE Receipt_Control_Service SHALL use the validated JWT identity and trusted organizational assignments as the sources for Authorization Scope.
3. WHILE a Receipt has an eligible Unauthorized Receipt state and no authoritative Challan relationship, THE Receipt_Control_Service SHALL report modification and cancellation as permitted only when the caller has an Authorized Supervisor Role and matching Authorization Scope.
4. WHILE a Receipt has an authoritative Challan relationship, THE Receipt_Control_Service SHALL report modification as unavailable.
5. WHILE a Receipt has an authoritative Challan relationship, THE Receipt_Control_Service SHALL report cancellation as unavailable.
6. WHILE a Receipt has an Authorized state, THE Receipt_Control_Service SHALL report direct modification and direct cancellation as unavailable.
7. WHILE a Receipt has an Authorized state, THE Receipt_Control_Service SHALL identify the approved Cheque Return or JV reversal route in the control response.
8. IF a Receipt identifier does not exist, THEN THE Receipt_Control_Service SHALL return a not-found Problem Details response without disclosing Receipt details.
9. IF a requested Receipt is outside the caller's Authorization Scope, THEN THE Receipt_Control_Service SHALL return a forbidden or disclosure-equivalent Problem Details response without disclosing Receipt details.

### Requirement 2: Protect Challaned and Authorized Receipts

**User Story:** As a financial controller, I want challaned and authorized receipts protected from direct mutation, so that operational controls cannot corrupt banking records or posted accounting.

#### Acceptance Criteria

1. IF a caller submits a modification request for a Challaned Receipt, THEN THE Receipt_Control_Service SHALL return a conflict Problem Details response with a stable challan-protection error code.
2. IF a caller submits a cancellation request for a Challaned Receipt, THEN THE Receipt_Control_Service SHALL return a conflict Problem Details response with a stable challan-protection error code.
3. IF a modification request targets an Authorized Receipt, THEN THE Receipt_Control_Service SHALL reject direct modification with a stable authorized-receipt error code.
4. IF a cancellation request targets an Authorized Receipt, THEN THE Receipt_Control_Service SHALL reject direct cancellation with a stable authorized-receipt error code.
5. WHEN the Receipt_Control_Service rejects a direct operation for a Challaned Receipt, THE Receipt_Control_Service SHALL return an English message identifying the challan-protection reason.
6. WHEN the Receipt_Control_Service rejects a direct operation for an Authorized Receipt, THE Receipt_Control_Service SHALL return an English message identifying the approved Cheque Return or JV reversal path.
7. WHEN the Receipt_Control_Service rejects a Challaned or Authorized modification or cancellation request, THE Receipt_Control_Service SHALL preserve the Receipt, audit entries, document-retirement records, and accounting links.
7. WHILE the Receipt_Control_UI displays a Challaned Receipt, THE Receipt_Control_UI SHALL disable Edit and Cancel controls.
8. WHILE the Receipt_Control_UI displays a Challaned Receipt, THE Receipt_Control_UI SHALL display an English warning that the Receipt is linked to a challan and cannot be modified or cancelled.
9. WHILE the Receipt_Control_UI displays an Authorized Receipt, THE Receipt_Control_UI SHALL disable direct Edit and Cancel controls.
10. WHILE the Receipt_Control_UI displays an Authorized Receipt, THE Receipt_Control_UI SHALL display an English instruction directing the user to the approved Cheque Return or JV reversal process.

### Requirement 3: Modify an Eligible Unauthorized Receipt

**User Story:** As a Branch Supervisor, I want to modify an eligible unauthorized receipt, so that data-entry errors can be corrected before authorization or accounting posting.

#### Acceptance Criteria

1. WHEN an Authorized Supervisor Role submits a valid modification for an eligible Unauthorized Receipt with a matching Receipt Version, THE Receipt_Control_Service SHALL persist the requested changes to approved mutable fields and return the updated Receipt.
2. WHEN a valid modification succeeds, THE Receipt_Control_Service SHALL preserve the Receipt identifier, Document Number, creation metadata, and Document Retirement state.
3. WHEN a valid modification succeeds, THE Receipt_Control_Service SHALL increment the Receipt Version and refresh the update timestamp.
4. WHEN a valid modification changes one or more fields, THE Receipt_Control_Service SHALL create exactly one immutable Receipt Audit Entry with operation `Modify`, Receipt identifier, Document Number, actor from the validated JWT, UTC timestamp, and before-and-after values for every changed field.
5. WHEN a valid modification succeeds, THE Receipt_Control_Service SHALL create no canonical journal.
6. WHEN a valid modification succeeds, THE Receipt_Control_Service SHALL create no Accounting Reversal.
7. IF a modification request contains a field outside the approved mutable-field set, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response.
8. IF a modification request contains an invalid value for an approved mutable field, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response.
9. IF a modification request fails validation, THEN THE Receipt_Control_Service SHALL preserve the Receipt, Receipt Version, audit history, and accounting links.
10. IF a modification request supplies a Receipt Version different from the current version, THEN THE Receipt_Control_Service SHALL return a concurrency-conflict Problem Details response and preserve the current Receipt state.
11. IF a modification request targets a Receipt that became Challaned, Authorized, Cancelled, or otherwise ineligible after loading, THEN THE Receipt_Control_Service SHALL return a state-conflict Problem Details response and preserve the current Receipt state.
12. WHEN the Receipt_Control_UI receives a successful modification response, THE Receipt_Control_UI SHALL replace the displayed Receipt with the returned server state.
13. WHEN the Receipt_Control_UI receives a successful modification response, THE Receipt_Control_UI SHALL display the returned Receipt Status and Receipt Version.
14. IF the Receipt_Control_UI receives a modification error, THEN THE Receipt_Control_UI SHALL preserve the currently displayed Receipt and display the English Problem Details message through the shared error-notification pattern.

### Requirement 4: Cancel an Erroneous Unauthorized Receipt

**User Story:** As a Branch Supervisor, I want to cancel an erroneous unauthorized receipt, so that the operational record remains traceable while the erroneous document cannot be used again.

#### Acceptance Criteria

1. WHEN an Authorized Supervisor Role submits a Cancellation for an eligible Unauthorized Receipt with a matching Receipt Version and a non-empty Cancellation Reason, THE Receipt_Control_Service SHALL set Receipt Status to `Cancelled` and return the cancelled Receipt.
2. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL preserve the original Document Number.
3. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL record the actor from the validated JWT and the cancellation timestamp in UTC.
4. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL increment the Receipt Version.
5. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL create exactly one immutable Receipt Audit Entry with operation `Cancel`, Receipt identifier, Document Number, prior Receipt Status, `Cancelled` status, Cancellation Reason, actor, and UTC timestamp.
6. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL create exactly one immutable Document Retirement Record with Receipt identifier, Document Number, numbering scope, actor, UTC timestamp, and Cancellation Reason.
7. WHEN a Cancellation succeeds, THE Receipt_Control_Service SHALL atomically commit the Receipt status, Receipt Version, cancellation metadata, Receipt Audit Entry, and Document Retirement Record.
8. IF a Cancellation request fails validation, authorization, or state validation before commit, THEN THE Receipt_Control_Service SHALL preserve the Receipt, Receipt Version, audit history, and Document Retirement Records.
9. IF a Cancellation request supplies a Receipt Version different from the current version, THEN THE Receipt_Control_Service SHALL return a concurrency-conflict Problem Details response and preserve the current Receipt state.
10. IF a Receipt is already `Cancelled` and a request repeats the completed operation with the same Idempotency Key, THEN THE Receipt_Control_Service SHALL return the existing cancellation result without creating another audit entry or retirement record.
11. IF a Receipt is already `Cancelled` and a request uses a new or mismatched Idempotency Key, THEN THE Receipt_Control_Service SHALL reject a new cancellation transition with a state-conflict or idempotency-conflict Problem Details response.
12. WHEN the Receipt_Control_UI displays an eligible `Pending Authorization` Receipt to an Authorized Supervisor Role, THE Receipt_Control_UI SHALL enable Cancel and require a Cancellation Reason before submission.
13. WHEN the Receipt_Control_UI receives a successful Cancellation response, THE Receipt_Control_UI SHALL display `Cancelled`, disable Edit, Cancel, and ordinary print controls, and refresh audit and retirement information.

### Requirement 5: Retire Document Numbers Permanently

**User Story:** As a financial controller, I want cancelled document numbers retained as immutable retired records, so that document continuity remains auditable and a cancelled number cannot be reused.

#### Acceptance Criteria

1. THE Receipt_Control_Service SHALL treat every Document Number in a Document Retirement Record as unavailable for future Receipt creation within the applicable numbering scope.
2. WHEN an Online Number selected for a new Receipt matches a Retired Document Number within the applicable numbering scope, THE Receipt_Control_Service SHALL reject the issuance with a stable document-retirement conflict error.
3. WHEN a Manual Number submitted for a new Receipt matches a Retired Document Number within the applicable numbering scope, THE Receipt_Control_Service SHALL reject the submission with a stable document-retirement conflict error.
4. WHEN a new Receipt accepts a Document Number, THE Receipt_Control_Service SHALL atomically check active Receipt records and Document Retirement Records within the applicable numbering scope.
5. IF a caller attempts to modify a Document Retirement Record, THEN THE Receipt_Control_Service SHALL reject the request with an immutability error and preserve the record.
6. IF a caller attempts to delete a Document Retirement Record, THEN THE Receipt_Control_Service SHALL reject the request with an immutability error and preserve the record.
7. WHEN an authorized user requests continuity history for a cancelled Receipt, THE Receipt_Control_Service SHALL return the original Document Number, cancellation operation, retirement timestamp, retirement actor, and Cancellation Reason.
8. WHEN the Receipt_Control_UI displays a cancelled Receipt, THE Receipt_Control_UI SHALL display the original Document Number and an English indication that the number is permanently retired.

### Requirement 6: Coordinate Accounting Reversal for Prior Accruals

**User Story:** As a Branch Supervisor, I want cancellation to reverse prior accruals through the approved accounting boundary, so that cancellation does not corrupt the accounting books.

#### Acceptance Criteria

1. WHEN an eligible Unauthorized Receipt has a prior Accrual and cancellation is requested, THE Receipt_Control_Service SHALL submit exactly one immutable Reversal Request before reporting completed Cancellation.
2. THE Receipt_Control_Service SHALL submit every Reversal Request to the confirmed Cheque Return Boundary or JV Posting Engine.
3. THE Receipt_Control_Service SHALL include Receipt identifier, Document Number, original accounting source link, reversal amount, INR currency, value date, collection branch, validated-JWT actor, UTC cancellation timestamp, Cancellation Reason, and Idempotency Key in a Reversal Request.
4. WHEN the authoritative accounting boundary accepts a Reversal Request, THE Receipt_Control_Service SHALL persist the Reversal Request identity and source link with the cancellation result.
5. WHEN an eligible Unauthorized Receipt has no prior Accrual, THE Receipt_Control_Service SHALL complete cancellation without submitting a Reversal Request.
6. WHEN an eligible Unauthorized Receipt has no prior Accrual, THE Receipt_Control_Service SHALL record that no reversal was required in the cancellation outcome or audit context.
7. THE Receipt_Control_Service SHALL use the confirmed Cheque Return Boundary or JV Posting Engine as the only authority for canonical reversal journal creation.
8. IF the authoritative accounting boundary rejects or cannot durably accept a required Reversal Request, THEN THE Receipt_Control_Service SHALL reject the Cancellation or apply the confirmed `Cancellation Pending` state.
9. IF a required Reversal Request is rejected or not durably accepted, THEN THE Receipt_Control_Service SHALL not report a completed Cancellation without a durable reversal reference.
10. IF a Reversal Request is retried with the same Idempotency Key, THEN THE authoritative accounting boundary SHALL return the existing reversal outcome without creating a second canonical reversal journal.
11. IF a caller attempts to create a canonical reversal journal through a Receipt_Control_Service endpoint, THEN THE Receipt_Control_Service SHALL reject the operation and preserve the Receipt and canonical accounting records.
12. WHEN the Receipt_Control_UI receives a successful cancellation with a reversal reference, THE Receipt_Control_UI SHALL display the reversal status and reference in the English audit or accounting section.
13. WHEN the Receipt_Control_UI receives a cancellation-pending response, THE Receipt_Control_UI SHALL display the returned English pending status.
14. WHEN the Receipt_Control_UI receives a reversal-failure response, THE Receipt_Control_UI SHALL display the returned English error.
15. WHEN the backend does not return the confirmed terminal cancellation status, THE Receipt_Control_UI SHALL not display the Receipt as fully cancelled.

### Requirement 7: Reprint an Already Printed Receipt as a Duplicate

**User Story:** As an authorized operator, I want a reprint to be visibly marked as a duplicate, so that duplicate paper cannot be mistaken for the original receipt.

#### Acceptance Criteria

1. WHEN an authorized user requests a Reprint for a Printed Receipt within Authorization Scope and the Receipt satisfies the confirmed print-eligibility rules, THE Receipt_Control_Service SHALL create exactly one Duplicate Output.
2. WHEN a Reprint succeeds, THE Receipt_Control_Service SHALL preserve the original Document Number in the Duplicate Output.
3. WHEN a Reprint succeeds, THE Receipt_Control_Service SHALL set the Duplicate Output title to the exact English text `DUPLICATE RECEIPT`.
4. WHEN a Reprint succeeds, THE Receipt_Control_Service SHALL render a prominent repeated watermark containing the exact English text `DUPLICATE RECEIPT`.
5. WHEN the first Reprint for a Receipt succeeds, THE Receipt_Control_Service SHALL assign Duplicate Copy No `1`.
6. WHEN the first Reprint for a Receipt succeeds, THE Receipt_Control_Service SHALL increment Duplication Count by one.
7. WHEN a later Reprint for a Receipt succeeds, THE Receipt_Control_Service SHALL assign the next sequential Duplicate Copy Number.
8. WHEN a later Reprint for a Receipt succeeds, THE Receipt_Control_Service SHALL increment Duplication Count by one without changing any prior Duplicate Copy Number.
9. WHEN a Reprint succeeds, THE Receipt_Control_Service SHALL include Receipt identifier, original Document Number, Duplicate Copy Number, Duplication Count, and UTC print timestamp in the print-ready response or document.
10. WHEN a Reprint succeeds, THE Receipt_Control_Service SHALL create exactly one immutable Receipt Audit Entry with operation `Reprint`, Receipt identifier, Document Number, Duplicate Copy Number, validated-JWT actor, and UTC timestamp.
11. IF a Receipt has not reached the authoritative Printed Receipt state, THEN THE Receipt_Control_Service SHALL reject the Reprint with a state-conflict Problem Details response.
12. IF a Reprint is rejected because the Receipt has not reached Printed Receipt state, THEN THE Receipt_Control_Service SHALL not increment Duplication Count or create a Reprint audit entry.
13. IF a Receipt is Cancelled, outside Authorization Scope, or otherwise ineligible under the confirmed print rules, THEN THE Receipt_Control_Service SHALL reject the Reprint and preserve Duplication Count.
14. WHEN the Receipt_Control_UI receives a successful first Reprint response, THE Receipt_Control_UI SHALL display the exact title `DUPLICATE RECEIPT`, the exact watermark text `DUPLICATE RECEIPT`, and `Duplicate Copy No: 1` in the print or preview surface.
15. WHEN the Receipt_Control_UI displays a Printed Receipt eligible for Reprint, THE Receipt_Control_UI SHALL display the Reprint action and current Duplication Count.
16. IF the Receipt_Control_UI receives a Reprint error, THEN THE Receipt_Control_UI SHALL preserve the displayed Receipt and current Duplication Count until a successful server response or explicit refresh.

### Requirement 8: Enforce Concurrent and Idempotent Control Operations

**User Story:** As a financial controller, I want competing corrections and repeated requests handled deterministically, so that one receipt cannot receive conflicting state changes or duplicate accounting and print effects.

#### Acceptance Criteria

1. THE Receipt_Control_Service SHALL apply an atomic Receipt Version or equivalent compare-and-set condition to every modification transition.
2. THE Receipt_Control_Service SHALL apply an atomic Receipt Version or equivalent compare-and-set condition to every Cancellation transition.
3. WHEN two modification or Cancellation requests target the same Receipt Version, THE Receipt_Control_Service SHALL commit at most one state-changing request.
4. WHEN two modification or Cancellation requests target the same Receipt Version, THE Receipt_Control_Service SHALL return a concurrency-conflict response for every request that does not commit.
5. THE Receipt_Control_Service SHALL allocate Duplicate Copy Numbers through an atomic counter and uniqueness boundary.
6. WHEN concurrent Reprint requests use different Idempotency Keys, THE Receipt_Control_Service SHALL assign distinct sequential Duplicate Copy Numbers.
7. WHEN a completed Modification, Cancellation, or Reprint request is repeated with the same Idempotency Key and equivalent payload, THE Receipt_Control_Service SHALL return the original operation result.
8. WHEN a completed state-changing request is replayed with the same Idempotency Key, THE Receipt_Control_Service SHALL create no second state transition, audit entry, retirement record, Reversal Request, or Duplicate Output.
9. IF a repeated request uses an Idempotency Key with a different Receipt identifier or materially different payload, THEN THE Receipt_Control_Service SHALL return an idempotency-conflict Problem Details response.
10. IF a transaction fails after validation and before required control records commit, THEN THE Receipt_Control_Service SHALL roll back Receipt fields, Receipt Version, audit entry, Document Retirement Record, Reversal Request identity, and Duplication Count changes in the transaction scope.
11. WHEN a successful control operation is retried after a transport timeout with the same Idempotency Key, THE Receipt_Control_Service SHALL return a determinable original result.

### Requirement 9: Enforce Authentication, Authorization, and Audit Integrity

**User Story:** As a compliance stakeholder, I want receipt controls protected by trusted identity and immutable audit records, so that operational corrections are attributable and limited to authorized staff.

#### Acceptance Criteria

1. IF a request to view, modify, cancel, or reprint a Receipt has a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Receipt_Control_Service SHALL return HTTP 401.
2. IF a request is rejected under the invalid-token condition, THEN THE Receipt_Control_Service SHALL disclose no protected Receipt data.
3. IF a request is rejected under the invalid-token condition, THEN THE Receipt_Control_Service SHALL make no data change.
4. IF an authenticated user lacks the approved role for the requested operation, THEN THE Receipt_Control_Service SHALL return HTTP 403.
5. IF an authenticated user lacks the approved role for the requested operation, THEN THE Receipt_Control_Service SHALL disclose no protected Receipt data beyond the permitted authorization response.
6. IF an authenticated user lacks the approved role for the requested operation, THEN THE Receipt_Control_Service SHALL make no data change.
7. IF an authenticated user is outside the Receipt Authorization Scope, THEN THE Receipt_Control_Service SHALL return the confirmed authorization or disclosure-equivalent error.
8. IF an authenticated user is outside the Receipt Authorization Scope, THEN THE Receipt_Control_Service SHALL make no Receipt, audit, retirement, reversal, or print-state change.
9. WHEN the Receipt_Control_Service records an actor for a modification, Cancellation, or Reprint, THE Receipt_Control_Service SHALL derive the actor identity from the validated JWT.
10. WHEN a request supplies an actor identity in the body, THE Receipt_Control_Service SHALL ignore that supplied actor identity.
11. WHEN a Receipt Audit Entry is persisted, THE Receipt_Control_Service SHALL retain immutable operation, Receipt identifier, Document Number, actor, UTC timestamp, and operation-specific context.
12. IF a caller attempts to update a Receipt Audit Entry, THEN THE Receipt_Control_Service SHALL reject the request with an immutability error.
13. IF a caller attempts to delete a Receipt Audit Entry, THEN THE Receipt_Control_Service SHALL reject the request with an immutability error.
14. WHEN the Receipt_Control_Service evaluates token validity and role membership, THE Receipt_Control_Service SHALL evaluate authentication before authorization.
15. WHEN the Receipt_Control_UI receives HTTP 401 or HTTP 403, THE Receipt_Control_UI SHALL use the shared axios authentication and authorization error behavior.
16. WHEN the Receipt_Control_UI receives HTTP 401 or HTTP 403, THE Receipt_Control_UI SHALL not expose controls that imply successful authorization.

### Requirement 10: Expose Stable Backend API Behavior

**User Story:** As a frontend developer, I want stable receipt-control contracts and errors, so that the UI can present accurate controls and outcomes.

#### Acceptance Criteria

1. THE Receipt_Control_Service SHALL expose authenticated operations for control summary or detail, eligible modification, Cancellation, audit retrieval, and Reprint through the backend host and gateway route confirmed during design.
2. THE Receipt_Control_Service SHALL accept an expected Receipt Version on every state-changing operation that uses optimistic concurrency.
3. THE Receipt_Control_Service SHALL accept an Idempotency Key on every state-changing operation that supports retry safety.
4. THE Receipt_Control_Service SHALL return successful modification responses containing Receipt identifier, Document Number, Receipt Status, Authorization Status, Receipt Version, audit-relevant timestamps, and modification result data.
5. THE Receipt_Control_Service SHALL return successful Cancellation responses containing Receipt identifier, Document Number, Receipt Status, Authorization Status, Receipt Version, audit-relevant timestamps, and cancellation result data.
6. THE Receipt_Control_Service SHALL return successful Reprint responses containing Receipt identifier, Document Number, Receipt Status, Authorization Status, Receipt Version, audit-relevant timestamps, and duplicate-output result data.
7. THE Receipt_Control_Service SHALL return Problem Details with a stable error code, HTTP status, English message, and operation context for validation, authentication, authorization, state, conflict, concurrency, immutability, and accounting-boundary failures.
8. IF a request omits a required Receipt identifier, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response and persist no data.
9. IF a state-changing request omits a required Receipt Version, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response and persist no data.
10. IF a retry-safe state-changing request omits a required Idempotency Key, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response and persist no data.
11. IF a Cancellation request omits a required Cancellation Reason, THEN THE Receipt_Control_Service SHALL return a field-specific validation Problem Details response and persist no data.
12. WHEN a client requests audit entries for a Receipt, THE Receipt_Control_Service SHALL return entries ordered by UTC timestamp descending.
13. WHEN two audit entries have the same UTC timestamp, THE Receipt_Control_Service SHALL order the entries by audit identifier descending.
14. IF a client requests audit entries for an unknown Receipt identifier, THEN THE Receipt_Control_Service SHALL apply the confirmed not-found and disclosure policy without returning unrelated audit entries.
15. THE Receipt_Control_Service SHALL expose no API operation that directly updates Receipt Audit Entries.
16. THE Receipt_Control_Service SHALL expose no API operation that directly deletes Receipt Audit Entries.
17. THE Receipt_Control_Service SHALL expose no API operation that directly updates Document Retirement Records.
18. THE Receipt_Control_Service SHALL expose no API operation that directly deletes Document Retirement Records.

### Requirement 11: Provide Safe Receipt-Control UI Behavior

**User Story:** As a Branch Supervisor, I want the UI controls to reflect server-approved receipt state, so that the screen prevents unsafe actions while preserving the server as the authority.

#### Acceptance Criteria

1. WHEN the Receipt_Control_UI loads a Receipt, THE Receipt_Control_UI SHALL display Document Number, Receipt Status, Authorization Status, Challan state, print state, Duplication Count, collection branch, and server-provided available actions in English.
2. WHILE the Receipt_Control_UI displays a Challaned Receipt, THE Receipt_Control_UI SHALL disable Edit.
3. WHILE the Receipt_Control_UI displays a Challaned Receipt, THE Receipt_Control_UI SHALL disable Cancel.
4. WHILE the Receipt_Control_UI displays a Challaned Receipt, THE Receipt_Control_UI SHALL display the English challan warning.
5. WHILE the Receipt_Control_UI displays an eligible `Pending Authorization` Receipt for an Authorized Supervisor Role, THE Receipt_Control_UI SHALL enable Edit and Cancel according to server-provided permissions.
6. WHEN the Receipt_Control_UI opens a Cancel form for an eligible Receipt, THE Receipt_Control_UI SHALL require a non-whitespace English Cancellation Reason before submitting.
7. WHILE the Receipt_Control_UI displays an Authorized Receipt, THE Receipt_Control_UI SHALL disable direct Edit and Cancel.
8. WHILE the Receipt_Control_UI displays an Authorized Receipt, THE Receipt_Control_UI SHALL display the approved Cheque Return or JV reversal instruction.
9. WHEN the Receipt_Control_UI opens an Edit form, THE Receipt_Control_UI SHALL display server-approved mutable fields.
10. WHEN the Receipt_Control_UI opens an Edit form, THE Receipt_Control_UI SHALL display the current Receipt Version.
11. WHEN the Receipt_Control_UI opens an Edit form, THE Receipt_Control_UI SHALL display the immutable Document Number as read-only data.
12. WHEN the Receipt_Control_UI submits a valid Edit, THE Receipt_Control_UI SHALL send the expected Receipt Version and a unique Idempotency Key through the feature service.
13. WHEN the Receipt_Control_UI receives a successful Edit response, THE Receipt_Control_UI SHALL refresh the form and detail view from the returned server state.
14. WHEN the Receipt_Control_UI opens a Cancel form, THE Receipt_Control_UI SHALL display the original Document Number and identify that cancellation permanently retires the Document Number.
15. WHEN the Receipt_Control_UI receives a successful Cancel response, THE Receipt_Control_UI SHALL display `Cancelled`, the original Document Number, the available cancellation actor or timestamp, and the audit or retirement outcome.
16. WHEN the Receipt_Control_UI opens a Reprint action for a Printed Receipt, THE Receipt_Control_UI SHALL display the current Duplication Count.
17. WHEN the Receipt_Control_UI submits a Reprint, THE Receipt_Control_UI SHALL send a unique Idempotency Key through the feature service.
18. WHEN the Receipt_Control_UI receives a successful Reprint response, THE Receipt_Control_UI SHALL render or open the returned Duplicate Output without changing the original Receipt identity or Document Number.
19. IF a control operation fails, THEN THE Receipt_Control_UI SHALL preserve the current list, detail view, status, and Duplication Count until a successful server response or explicit refresh.
20. WHEN the Receipt_Control_UI uses mock services, THE Receipt_Control_UI SHALL expose the same status transitions, authorization outcomes, stable error shape, exact watermark text, Duplicate Copy Number behavior, and audit behavior as the backend contract.
21. THE Receipt_Control_UI SHALL use React hooks for feature state.
22. THE Receipt_Control_UI SHALL use the shared axios instance for real HTTP requests.
23. THE Receipt_Control_UI SHALL use the established feature service interface, real implementation, mock implementation, and `VITE_USE_MOCK_API` toggle.
24. THE Receipt_Control_UI SHALL use MUI controls and English-only user-facing text.
25. THE Receipt_Control_UI SHALL introduce no Redux, RxJS, RTL handling, or non-English localization.

## Testing Expectations

The eventual implementation must include automated tests in both repositories. This section records the expected test coverage without implementing source code or tests in this requirements phase.

### Backend tests

- Use xUnit for example and edge cases, FsCheck.Xunit for pure control logic with at least 100 iterations where generated inputs add value, and `WebApplicationFactory<Program>` integration tests for host wiring, authentication, authorization, gateway-facing contracts, and persistence behavior.
- Test eligibility for Pending Authorization, other unauthorized states, Challaned, Authorized, Cancelled, unknown, and out-of-scope receipts.
- Test modification success, immutable-field rejection, validation failure, stale Receipt Version, actor derivation, retained Document Number, and exactly one before-and-after audit entry.
- Test cancellation success for the Pending Authorization scenario, mandatory reason validation, status transition to Cancelled, actor/timestamp/reason capture, exactly one retirement record, no number reuse, duplicate cancellation idempotency, and rejection for Challaned or Authorized receipts.
- Test cancellation with a prior Accrual through a mocked authoritative accounting boundary, cancellation without a prior Accrual, reversal idempotency, accounting-boundary rejection, and absence of direct canonical journal writes.
- Test Reprint output for a previously printed Receipt, exact `DUPLICATE RECEIPT` title and watermark, first Duplicate Copy No `1`, Duplication Count increment, later sequential copies, immutable original Document Number, audit entry, ineligible-state rejection, and concurrent counter updates.
- Test optimistic concurrency, idempotency-key replay, payload mismatch, atomic rollback, append-only audit behavior, and document-retirement uniqueness under concurrent create/cancel requests.
- Test HTTP 401 for missing, expired, malformed, and invalid-signature tokens; HTTP 403 for authenticated users without the confirmed role; and scope rejection for users outside the confirmed branch or organization assignment.
- Test the dependency adapters against the final FINNOVA-61 and FINNOVA-63 contracts before implementation is considered complete. Tests must fail closed when either authoritative contract is unavailable or incompatible.

### Frontend tests

- Use Vitest and React Testing Library with the established mock/real service pattern.
- Test the Challaned Receipt view with disabled Edit and Cancel controls and the English challan warning.
- Test the Pending Authorization scenario with supervisor cancellation, required reason, successful `Cancelled` state, original Document Number, retirement information, and refreshed audit display.
- Test direct modification and cancellation controls for Authorized Receipts and the English Cheque Return or JV reversal instruction.
- Test first Reprint output with the exact title `DUPLICATE RECEIPT`, visible watermark, and `Duplicate Copy No: 1`; test subsequent count and copy-number updates.
- Test UI state preservation on validation, authorization, conflict, concurrency, accounting-boundary, and reprint errors, including stable messages from the shared error response shape.
- Test real service URL and payload composition, mock service parity, route and navigation wiring, and the absence of Redux, RxJS, RTL, and non-English localization behavior.

## Open Confirmation Decisions

The following decisions must be confirmed before the design and task phases are finalized:

1. The authoritative FINNOVA-61 Receipt Aggregate, receipt statuses, numbering contract, print state, and receipt audit model.
2. The authoritative FINNOVA-63 authorization, posting-status, and JV Posting Engine contract.
3. Whether Cheque Return already exists as an authoritative boundary or must be introduced, and whether returned-cheque correction differs from generic accounting reversal.
4. The exact authorized roles and Company, Line of Business, Branch, and maker-versus-checker scopes for modification, Cancellation, audit access, and Reprint.
5. The exact mutable fields for an Unauthorized Receipt and the status behavior after a modification.
6. The online and manual numbering scope, retirement representation, manual receipt-book behavior, and sequence-gap policy.
7. The authoritative challan relationship and the status/relationship transition that makes a Receipt Challaned.
8. The cancellation reason length, reason-code catalog, retention period, and archive/legal-hold policy.
9. The transaction or outbox boundary that makes Cancellation, audit, retirement, and required reversal submission atomic.
10. The print-state authority, regular-print eligibility, duplicate count baseline, PDF/rendering contract, and whether cancelled receipts can ever be reprinted.
