# Requirements Document

## Introduction

The Application Account Creation feature extends the Application Processing feature (`.kiro/specs/application-processing`). Today, processing an Application finalizes its Amortization Schedule and sets the Application Status to `Processed`. This feature adds the next step: every Application that reaches `Processed` produces exactly one **Loan Account**. The Account is the operational record that the Demand Processing feature (`.kiro/specs/demand-processing`) consumes (it reads Active Accounts and their finalized Amortization Schedules).

As part of that step, a **KYC Check** examines the Customer's basic details against Aadhaar and PAN using the identity verification capability defined in `.kiro/specs/pan-aadhar-validation`. The Account is created whether or not the KYC Check passes. When the check fails, is unavailable, or is not performed, the Account carries a **KYC flag** so that operations staff can follow up. The KYC Check is currently **bypassed** through configuration, and a **third-party aggregation provider** abstraction is provided so a KYC aggregator can be plugged in later without changing the Account Creation flow.

Account Creation is executed by a **Python 3.11 CRON worker**, following the execution model of the JV Posting Engine (`.kiro/specs/jv-posting-engine`): the .NET API is the boundary for submission, listing, viewing, and updating; the Python worker claims eligible work from a SQL Server work table, performs the processing, and commits the outcome in one SQL Server transaction. The feature also provides **List, View, and Update** operations for Accounts in the backend (`e:\Finnova\Finnova-API`, hosted in `Finnova.AccountsService`) and a matching screen in the frontend (`E:\Finnova\Finnova-UI\Finnova-UI`). The List shows the Application Number, Account Number, Customer basic details, and KYC pass details.

This document extends rather than duplicates the Application Processing requirements. It reuses the Application, Application Schedule, reference-validation, authorization, error-contract, and gateway decisions from that spec, and the existing `Account` entity, `AccountType`, `AccountStatus`, `AccountsController`, and Customer Master. Finnova is an India-only, English-only platform: amounts are in INR, there are no bilingual fields, no non-English data, and no right-to-left rendering.

This document contains requirements only. It does not authorize source-code implementation, migration execution, build execution, or test execution. Decisions not stated by the stakeholder are recorded as **Assumptions** for confirmation.

### Dependencies and Assumptions

- **Dependency (Application Processing):** An Application reaches `Processed` through the `ProcessApplicationCommand` of the application-processing spec, which persists the finalized Amortization Schedule and the Processed status in one transaction. Account Creation consumes the Processed Application and its finalized schedule and does not recalculate the schedule.
- **Dependency (Demand Processing):** Demand Processing reads accounts by LOB, Branch, and status, and treats `Active` as the eligible status. This feature produces the Accounts that Demand Processing consumes.
- **Dependency (Identity verification):** The KYC Check uses the Identity Verification capability of `pan-aadhar-validation` (PAN Identifier Validation, Aadhaar Identifier Validation, `verify-pan-name`, `verify-aadhar-address`) through a server-to-server contract. The provider wire schemas remain inside that capability's Provider Adapters.
- **Dependency (Python worker pattern):** The worker follows the JV Posting Engine conventions: OS CRON invocation, SQL Server work table, claim lease, bounded batch, retry schedule, dead-letter state, Asia/Kolkata business timezone, and a seeded `SystemUser` actor.
- **Dependency (Document Number Control):** The Account Number is issued by the DNC master (`.kiro/specs/document-number-control-master`) for Document Type `LoanAccount`. This depends on the DNC extension described in Requirement 16 (LOB scope dimension, `{LOB}` and `{BRANCH}` tokens, an internal service-authenticated issuance endpoint), which must be reflected in that spec.
- **Dependency (Customer Master):** Customer basic details and Customer identification data (PAN, Aadhaar, name, primary address, mobile) are read from the existing Customer Master and are not duplicated as editable data.
- **Assumption A1 (Host placement):** The Account List/View/Update API is placed in the existing `Finnova.AccountsService`, extending the existing `AccountsController` route family through a new controller with the route `api/loan-accounts` (user-confirmed), and is reached through the gateway alias `/api/ua/api/loan-accounts` forwarded to `accounts-cluster` (Requirement 12). The route is not `api/accounts/loan-accounts`. Application Processing already resides in the same host (application-processing Assumption on host placement). No new host is created.
- **Assumption A2 (Extend the existing Account entity):** The existing `Account` entity (`AccountNumber`, `AccountName`, `Type`, `Status`, `Balance`, `Currency`, `UserId`, `OrganizationId`, `BranchCode`, `IfscCode`, `OpenedAt`, `ClosedAt`, `CreatedAt`, `UpdatedAt`) is extended rather than replaced. New nullable columns are added for `ApplicationId`, `CustomerId`, `LineOfBusinessId`, `BranchId`, `ProductCode`, `SchemeCode`, and the KYC fields. Loan Accounts use `AccountType.Loan`. Existing non-loan accounts remain unaffected, and the new columns are null for them. The extension is proposed for confirmation during design.
- **Assumption A3 (Account status mapping):** The existing `AccountStatus` values (`Active`, `Inactive`, `Frozen`, `Closed`) are reused. A newly created Loan Account has status `Active`, because Demand Processing treats exactly `Active` as eligible. The Demand Processing statuses `Cancelled` and `Pre-Closed` are not added by this feature and remain a cross-spec item for confirmation.
- **Assumption A4 (Account creation does not move money):** Creating the Account does not disburse funds, post journals, or set a non-zero `Balance`. `Balance` starts at 0.00 INR. Disbursement and accounting posting belong to separate features.
- **Assumption A5 (Account number generation via Document Number Control):** The Account Number is NOT generated from a dedicated SQL Server sequence and carries no fixed `LN` prefix. It is issued by the Document Number Control master (`.kiro/specs/document-number-control-master`, FINNOVA-7), referred to as DNC. The AccountCreation_Worker requests the next number for Document Type `LoanAccount` (scheme code, for example, `LOAN-ACCT`), resolved by the Line of Business and Branch of the Application. The format, prefix, padding, and reset rule come from the scheme's Format Template, so the Account Number length is no longer fixed at 12 characters. The Account Number stays immutable after creation. The Application Number generation decision of application-processing is unchanged and is out of scope here. Specific template values are configured by a System Administrator in DNC and are not defined by this feature.
- **Assumption A22 (DNC extension, user-confirmed):** DNC is EXTENDED with an optional Line of Business dimension so that a `LoanAccount` scheme yields independent sequences per LOB and Branch combination. This is a change to the document-number-control-master spec and is detailed in Requirement 16. That other spec is NOT edited by this feature; the affected areas are listed in Requirement 16 so the change can be reflected there.
- **Assumption A23 (Issuance access path):** DNC issuance is currently SystemAdmin-only and hosted in `Finnova.SystemAdminService`. The Python worker obtains numbers through a service-authenticated internal issuance endpoint in `Finnova.SystemAdminService`, authenticated with the seeded `SystemUser` credential read from secure configuration. The internal issuance endpoint is not exposed through the public gateway (Requirement 12). The exact route and credential mechanism are proposed for confirmation during design, in the same manner as Assumption A15.
- **Assumption A24 (Numbering transactional behavior):** DNC issuance is a separate atomic call to `Finnova.SystemAdminService` and is not part of the worker's Account SQL transaction. A number issued by DNC is not returned when the worker's Account transaction rolls back, so gaps in the Account Number series are acceptable. Account Number uniqueness is still enforced by a unique index on the Account table. IF a retry occurs after a number was issued but the Account insert failed, THEN the worker issues a new number and the earlier number remains unused (gap accepted). The worker does not attempt to reuse or recycle an issued number.
- **Assumption A25 (Scheme resolution fallback, user-confirmed):** The Scheme Resolution Fallback (LOB and Branch, LOB only, Branch only, Global) is evaluated inside DNC on the server side by a pure `SchemeResolver` component of the DNC_Service, not in the worker. The worker supplies Document Type `LoanAccount`, the Line of Business, and the Branch, and DNC issues the number from the first active scheme found. IF DNC finds no active scheme, THEN DNC returns a no-scheme error, the worker maps it to `NoNumberingScheme`, the request goes to `Retry` and then `DeadLetter`, and no Account is created. The exhaustive fallback property test is a .NET FsCheck test of `SchemeResolver`; the worker is tested with `pytest` against a mocked DNC endpoint.
- **Assumption A26 (PAN and Aadhaar document codes):** The Customer's PAN and Aadhaar are identified among the Customer's documents by Document Master code. This feature assumes PAN document code `PAN` and Aadhaar document code `AADHAAR`, configurable through `AccountCreation:Kyc:PanDocumentCode` and `AccountCreation:Kyc:AadhaarDocumentCode`, together with the Document attribute `IsAadhaarDocument`. These values are an assumption to be changed later against the actual Document Master content. PAN and Aadhaar are read from `CustomerDocumentReference` rows of those document codes, with `IndividualCustomerDetails.IndiaIdentityReference` as the fallback when no such document reference exists.
- **Assumption A27 (Role creation in scope):** The roles `ApplicationProcessor` (existing), `AccountsViewer`, `AccountsOperator`, and `KycApprover` are created and seeded by this feature where they do not already exist, including their policy registration and assignment path. Role creation and seeding are in scope. Role names are proposed for confirmation.
- **Assumption A28 (Customer-master mapping profile):** The identity verification mapping registry of `pan-aadhar-validation` contains no customer-master mapping profile today. This feature adds one to that registry, mapping Customer Master name to the verification name and the composed Customer Master address to the verification address. The change is made to the pan-aadhar-validation mapping registry and is detailed in Requirement 17.
- **Assumption A6 (One Account per Application):** Each Processed Application has at most one Account, enforced by a unique constraint on `ApplicationId`. Multi-account (for example, co-applicant or sub-loan) structures are out of scope.
- **Assumption A7 (Trigger by Processed status):** The Python worker discovers work through an **Account Creation Request** row. The row is inserted in the same transaction in which `ProcessApplicationCommand` sets the Application to `Processed` (transactional outbox style, matching the JV Posting Inbox convention). The worker never scans the Application table directly.
- **Assumption A8 (KYC bypass flag):** A configuration setting `AccountCreation:Kyc:Mode` selects `Bypass` (default for this iteration), `Internal` (the pan-aadhar-validation providers), or `Aggregator` (the third-party aggregation provider). In `Bypass` mode no identity provider call is made and the KYC Status is recorded as `Bypassed`. The setting is read by the worker at the start of each run and recorded on each Account's KYC record.
- **Assumption A9 (KYC Status values):** The KYC Status is one of `Pending`, `Passed`, `Failed`, `PartiallyVerified`, `Unavailable`, and `Bypassed`. `Passed` requires all four checks (PAN identifier, PAN name, Aadhaar identifier, Aadhaar address) to be successful or matched. `Failed` means at least one check returned `Failure` or `NonMatch`. `PartiallyVerified` means no check failed but at least one check returned `NoUsableMatch` or was not applicable. `Unavailable` means the provider could not be reached after retries. `Pending` applies between Account creation and KYC completion.
- **Assumption A10 (KYC flag fields):** Each Account holds `KycStatus`, `KycFlagged` (true when the status is anything other than `Passed`), `KycCheckedAtUtc`, `KycSource` (`Bypass`, `Internal`, `Aggregator`), `KycVerificationSessionId` (the pan-aadhar-validation Verification Session when one exists), and four independent check results: `PanValidation`, `PanNameMatch`, `AadhaarValidation`, `AadhaarAddressMatch`. Each check result is one of `Passed`, `Failed`, `NotChecked`, `Unavailable`, or `Bypassed`. Raw PAN and Aadhaar values are never stored on the Account.
- **Assumption A11 (Account creation precedes KYC outcome):** Account creation does not wait on KYC success. The Account is created first with `KycStatus = Pending`, and the KYC Check then runs and updates the KYC fields. A KYC provider failure never rolls back or prevents the Account.
- **Assumption A12 (Idempotency):** The Account Creation Request has a unique key on `ApplicationId`. The worker creates the Account, writes the KYC outcome, and completes the request in one SQL Server transaction. Reprocessing a request whose Account already exists completes the request without creating a second Account.
- **Assumption A13 (Updatable Account fields):** After creation, an authorized user may update only **operational and contact-level fields**: `AccountName` (the display name), `Status` (restricted to the transitions in Requirement 8), and a **KYC manual resolution** (Requirement 9). The Application Number, Account Number, Customer reference, LOB, Branch, Product, Scheme, Type, Currency, Balance, opening date, and financial terms are immutable through this API. Branch transfer and financial-term changes are out of scope.
- **Assumption A14 (Third-party aggregation provider abstraction):** The aggregation provider is defined as a provider-neutral contract (`IKycAggregatorProvider`) that accepts a Provider-Neutral KYC Request (customer reference, masked identifiers, name, address, correlation identifier) and returns a Normalized KYC Result (per-check outcomes, overall outcome, provider reference, safe reason). No concrete aggregator is selected. Provider selection, endpoint, credentials, timeout, and retry are secure configuration. In this iteration only the `Bypass` mode is active, and the contract and configuration seam are delivered.
- **Assumption A15 (Where the KYC call is made):** The Python worker does not call external identity providers directly. The worker calls an internal, service-authenticated KYC endpoint in `Finnova.SystemAdminService` (pan-aadhar-validation host) that runs the combined `Full` Verification workflow. The worker uses a service credential for the seeded `SystemUser`. The aggregator contract of Assumption A14 is hosted behind the same internal endpoint. The exact internal route and credential mechanism are proposed for confirmation during design.
- **Assumption A16 (Worker configuration baseline):** Python 3.11, CRON every 1 minute, batch size 50, claim lease and processing deadline of 10 minutes, retry delays of 1, 5, and 30 minutes for up to 3 attempts, Asia/Kolkata business timezone. These match the JV Posting Engine baseline and are overridable through approved configuration.
- **Assumption A17 (Authorization):** Account List and View require authentication and the role `AccountsViewer` or `ApplicationProcessor`. Account Update and the submission of a manual KYC resolution request (the maker step) require `ApplicationProcessor`. Approval or rejection of a manual KYC resolution request (the checker step) requires the role `KycApprover`. `SystemAdmin` access is granted only through explicit policy configuration, consistent with application-processing. Operational endpoints (queue status, recovery) require `SystemAdmin` or `AccountsOperator`. The roles are created as stated in Assumption A27. Role names are proposed for confirmation.
- **Assumption A18 (Customer basic details on list):** The List shows Customer Code, Customer display name (assembled from the Customer Master name parts), primary mobile number (masked), and city. Full address, PAN, and Aadhaar are shown only on the View screen, masked. Display values are read-through from the Customer Master at query time and are not copied onto the Account, except the Customer display name snapshot (`CustomerNameSnapshot`) captured at creation for list performance and auditability.
- **Assumption A19 (Manual KYC resolution requires maker-checker, user-confirmed):** A flagged Account's KYC becomes `Passed` by manual resolution only through maker-checker. A maker (role `ApplicationProcessor`) submits a KYC Resolution Request with a mandatory reason of 10 to 500 characters. A different user holding the checker role `KycApprover` approves or rejects it with a comment. The maker cannot approve or reject their own request (four-eyes principle). The Account's `KycStatus` changes to `Passed` with `KycSource = Manual` only on approval. The states `Pending`, `Approved`, and `Rejected` are retained in the KYC history, the original automated outcome is retained, and each step writes an audit entry. At most one `Pending` KYC Resolution Request exists per Account. The Account list and view show a pending resolution indicator. This mirrors the Manual Override concept in pan-aadhar-validation.
- **Assumption A20 (Audit and history):** Account creation, KYC outcome, each Account update, each status transition, and each manual KYC resolution create an immutable audit entry with actor, source (`Worker`, `User`), correlation identifier, old and new values (for non-sensitive fields), and UTC timestamp. Retention follows the platform-approved financial record policy.
- **Assumption A21 (Testing):** Pure logic (KYC status aggregation, status transition rules, idempotency key logic, maker-checker rules) is verified with FsCheck.Xunit property tests of at least 100 iterations on the .NET side and with `pytest` plus `hypothesis` for the Python worker. The scheme resolution fallback order is verified exhaustively in .NET with FsCheck against the pure DNC `SchemeResolver` (Assumption A25). Account Number formatting is owned and tested by DNC (format template rendering) and is not re-implemented in this feature; the worker's DNC client is tested with `pytest` against a mocked issuance endpoint (including the `NoNumberingScheme` mapping), and DNC issuance wiring is covered by an integration test. API, authorization, persistence, and gateway wiring use xUnit and `WebApplicationFactory<Program>` tests. Frontend behavior uses Vitest and React Testing Library.
- **Note (Localization):** All labels, statuses, flags, messages, and test data are English. No bilingual fields, no non-English values, no translation scaffolding, and no RTL behavior.

## Glossary

- **Application**: A finance application captured and processed under the application-processing spec.
- **Processed Application**: An Application whose Application Status is `Processed` and which has a finalized Amortization Schedule.
- **Loan Account**: The Account created from a Processed Application, with `AccountType.Loan`. Referred to as the Account in this document.
- **Account Number**: A unique, immutable identifier of a Loan Account, issued by DNC according to the format template of the resolved `LoanAccount` scheme.
- **DNC**: The Document Number Control master (`.kiro/specs/document-number-control-master`) that defines numbering schemes and issues numbers atomically.
- **LoanAccount Scheme**: A DNC Numbering Scheme whose Document Type is `LoanAccount` (for example, scheme code `LOAN-ACCT`).
- **DNC_Service**: The backend service in `Finnova.SystemAdminService` that manages numbering schemes and issues numbers, as defined by the DNC spec and extended by Requirement 16.
- **DNC_Issuance_Endpoint**: The internal, service-authenticated number issuance endpoint in `Finnova.SystemAdminService` used by the AccountCreation_Worker, not exposed through the public gateway.
- **NoNumberingScheme**: The Dead Letter reason recorded when no active LoanAccount Scheme resolves for the Application's LOB and Branch.
- **Scheme Resolution Fallback**: The ordered lookup LOB+Branch, then LOB, then Branch, then Global, evaluated inside DNC.
- **SchemeResolver**: The pure DNC_Service component that applies the Scheme Resolution Fallback.
- **KYC Resolution Request**: A maker-checker record proposing manual resolution of an Account's KYC Flag, with status `Pending`, `Approved`, or `Rejected`.
- **Maker**: The user with role `ApplicationProcessor` who submits a KYC Resolution Request.
- **Checker**: The user with role `KycApprover`, different from the Maker, who approves or rejects a KYC Resolution Request.
- **Mapping Registry**: The identity verification mapping registry of `pan-aadhar-validation`.
- **Customer Master Mapping Profile**: The Mapping Registry profile added by this feature that maps Customer Master name and composed address to verification name and address.
- **Account Creation Request**: A durable work item in SQL Server, one per Processed Application, that the Python worker claims to create the Account.
- **AccountCreation_Worker**: The Python 3.11 process started by an OS CRON job that claims Account Creation Requests, creates Accounts, runs the KYC Check, and records outcomes.
- **AccountCreation_Service**: The .NET API in `Finnova.AccountsService` that exposes the Account List, View, Update, KYC resolution, and operational endpoints, and that inserts the Account Creation Request when an Application is processed.
- **AccountCreation_UI**: The React/TypeScript/MUI screens for Account List, View, and Update in Finnova-UI.
- **KYC Check**: The verification of the Customer's PAN and Aadhaar details, covering PAN identifier validation, PAN name comparison, Aadhaar identifier validation, and Aadhaar address comparison.
- **KYC Mode**: The configured behavior of the KYC Check: `Bypass`, `Internal`, or `Aggregator`.
- **KYC Status**: The overall KYC outcome of an Account: `Pending`, `Passed`, `Failed`, `PartiallyVerified`, `Unavailable`, or `Bypassed`.
- **KYC Flag**: A boolean on the Account that is true whenever the KYC Status is not `Passed`, used to find Accounts needing KYC follow-up.
- **KYC Check Result**: The outcome of one of the four individual checks, one of `Passed`, `Failed`, `NotChecked`, `Unavailable`, or `Bypassed`.
- **KYC Aggregator Provider**: A third-party service that verifies identity details on behalf of Finnova, accessed through the provider-neutral `IKycAggregatorProvider` contract.
- **Normalized KYC Result**: The provider-independent result containing per-check outcomes, an overall outcome, a provider reference, and a safe reason.
- **Verification Session**: The pan-aadhar-validation parent record grouping the results of one combined verification.
- **Customer Basic Details**: Customer Code, Customer display name, masked primary mobile number, and city.
- **Claim Lease**: The time-limited exclusive right of a worker instance to process an Account Creation Request.
- **Dead Letter**: The terminal state of an Account Creation Request that exhausted its retry attempts and requires operational recovery.
- **SystemUser**: The seeded automation identity used as the actor when the worker acts without an initiating user.
- **Correlation Identifier**: A non-sensitive identifier linking an Application, request, worker run, KYC call, Account, and audit entries.
- **EARS**: Easy Approach to Requirements Syntax.

## Requirements

### Requirement 1: Enqueue Account Creation When an Application Is Processed

**User Story:** As an Operations User, I want processing an Application to schedule the creation of its Account, so that every Processed Application results in an Account without a separate manual step.

#### Acceptance Criteria

1. WHEN the ApplicationProcessing_Service transitions an Application to `Processed`, THE AccountCreation_Service SHALL insert exactly one Account Creation Request with status `Pending` for that Application in the same SQL Server transaction as the status transition.
2. THE AccountCreation_Service SHALL store on each Account Creation Request the Application identifier, Application Number, Correlation Identifier, initiating user identifier, UTC creation timestamp, attempt count of 0, and status.
3. IF the transaction that transitions an Application to `Processed` is rolled back, THEN THE AccountCreation_Service SHALL persist no Account Creation Request for that Application.
4. IF an Account Creation Request already exists for an Application identifier, THEN THE AccountCreation_Service SHALL reject the second insertion through a unique constraint and SHALL leave the existing request unchanged.
5. THE AccountCreation_Service SHALL NOT create an Account Creation Request for an Application whose status is `Draft` or `Submitted`.
6. WHEN an Account Creation Request is inserted, THE AccountCreation_Service SHALL NOT create the Account within the API request, so that Account creation is performed only by the AccountCreation_Worker.

### Requirement 2: Claim and Process Account Creation Requests with the Python CRON Worker

**User Story:** As a platform operator, I want a CRON-driven Python worker to process account creation, so that account creation runs in the background with the same operational model as the JV Posting Engine.

#### Acceptance Criteria

1. WHEN the OS CRON job starts the AccountCreation_Worker, THE AccountCreation_Worker SHALL claim up to the configured batch size of Account Creation Requests whose status is `Pending`, or `Retry` with a due retry time, or `Processing` with an expired Claim Lease.
2. WHEN the AccountCreation_Worker claims a request, THE AccountCreation_Worker SHALL set the status to `Processing`, record the worker instance identifier, and set the Claim Lease expiry using a single atomic SQL Server statement so that two worker instances never hold the same request.
3. WHILE a request holds an unexpired Claim Lease, THE AccountCreation_Worker SHALL NOT allow another worker instance to claim that request.
4. WHEN the AccountCreation_Worker processes a claimed request, THE AccountCreation_Worker SHALL load the Processed Application, its finalized Amortization Schedule reference, and the referenced Customer, LOB, Branch, Product code, and Scheme code.
5. WHEN the AccountCreation_Worker finishes creating the Account and recording the KYC outcome, THE AccountCreation_Worker SHALL set the request status to `Completed` in the same SQL Server transaction as the Account insert.
6. IF a transient failure occurs while processing a request, THEN THE AccountCreation_Worker SHALL set the request status to `Retry`, increment the attempt count, and schedule the next attempt using the configured delays of 1, 5, and 30 minutes.
7. IF a request exhausts the configured maximum of 3 attempts, THEN THE AccountCreation_Worker SHALL set the request status to `DeadLetter`, record the safe failure reason, and SHALL create no Account for that request.
8. IF the Application referenced by a request is not in the `Processed` state, THEN THE AccountCreation_Worker SHALL set the request status to `DeadLetter` with reason `ApplicationNotProcessed` and SHALL create no Account.
9. WHEN the AccountCreation_Worker completes a run, THE AccountCreation_Worker SHALL emit a structured log entry containing the Correlation Identifier, claimed count, completed count, retried count, dead-lettered count, and elapsed time, with no PAN, Aadhaar, or secret values.
10. THE AccountCreation_Worker SHALL read its database credential and any service credential from secure configuration and SHALL NOT write those values to logs.
11. THE AccountCreation_Worker SHALL NOT expose a public network endpoint.

### Requirement 3: Create the Account from the Processed Application

**User Story:** As an Operations User, I want an Account created from each Processed Application, so that the loan can be serviced and billed.

#### Acceptance Criteria

1. WHEN the AccountCreation_Worker processes a request for a Processed Application with no existing Account, THE AccountCreation_Worker SHALL insert one Account with `AccountType.Loan`, status `Active`, currency `INR`, and Balance 0.00.
2. WHEN the AccountCreation_Worker inserts the Account, THE AccountCreation_Worker SHALL set the Application identifier, Customer identifier, Line of Business identifier, Branch identifier, Product code, Scheme code, Customer name snapshot, opening date (the Disbursement Date of the Application), UTC creation timestamp, and UTC updated timestamp from the Processed Application and its references.
3. WHEN the AccountCreation_Worker inserts the Account, THE AccountCreation_Worker SHALL derive the Account `BranchCode` from the referenced Branch and SHALL set `AccountName` to the Customer display name followed by the Application Number.
4. THE AccountCreation_Worker SHALL link the Account to the finalized Amortization Schedule of the Application by the Application identifier, and SHALL NOT copy, recalculate, or modify the schedule.
5. WHEN the AccountCreation_Worker creates an Account, THE AccountCreation_Worker SHALL obtain the Account Number from DNC through the DNC_Issuance_Endpoint for Document Type `LoanAccount`, supplying the Line of Business and Branch of the Application, and SHALL set the returned Issued Number as the Account Number without modification.
6. WHEN two worker instances process different requests concurrently, THE AccountCreation_Worker SHALL receive distinct Account Numbers from DNC, relying on DNC's per-scheme atomic issuance (DNC Requirement 5.2).
7. IF an Account already exists for the Application identifier, THEN THE AccountCreation_Worker SHALL create no second Account, SHALL complete the request, and SHALL leave the existing Account unchanged.
8. IF the referenced Customer, Line of Business, or Branch no longer exists or is inactive at processing time, THEN THE AccountCreation_Worker SHALL still create the Account, SHALL set the KYC Flag, and SHALL record the reference problem in the Account's creation notes.
   - **Assumption:** The Account is created despite reference problems in line with the stakeholder rule that the Account is created even when validations do not pass. A missing Customer (hard reference) is the exception: IF the Customer cannot be resolved at all, THEN THE AccountCreation_Worker SHALL set the request status to `DeadLetter` with reason `CustomerNotFound`, because the Account cannot be linked to a Customer.
9. WHEN the Account insert, the KYC Pending marker, the audit entry, and the request completion are committed, THE AccountCreation_Worker SHALL commit them in a single SQL Server transaction so that either all or none are persisted.
10. THE AccountCreation_Worker SHALL create the Account with the actor set to the initiating user of the request, or to the seeded SystemUser when no initiating user exists.

### Requirement 4: Generate Unique Account Numbers

**User Story:** As an Operations User, I want every Account to have a unique, readable Account Number, so that I can identify and reference each Account.

#### Acceptance Criteria

1. WHEN the AccountCreation_Worker needs an Account Number, THE AccountCreation_Worker SHALL call the DNC_Issuance_Endpoint with Document Type `LoanAccount`, the Line of Business identifier, and the Branch identifier of the Application, and SHALL NOT draw the number from a SQL Server sequence or apply a hard-coded prefix.
2. WHEN the DNC_Service receives an issuance request for Document Type `LoanAccount`, THE SchemeResolver SHALL apply the Scheme Resolution Fallback in the order LOB+Branch, LOB, Branch, Global and SHALL select the first active scheme found, so that the AccountCreation_Worker performs no scheme resolution itself.
3. THE AccountCreation_Service SHALL enforce uniqueness of the Account Number through a unique index on the Account table.
4. IF the DNC_Service reports that no active LoanAccount Scheme resolves for the Application's LOB and Branch, THEN THE AccountCreation_Worker SHALL map the response to `NoNumberingScheme`, SHALL set the request status to `Retry`, and WHEN the maximum attempts are exhausted SHALL set the status to `DeadLetter` with reason `NoNumberingScheme`, and SHALL create no Account.
5. IF DNC reports that the resolved scheme is inactive or that its sequence is exhausted, THEN THE AccountCreation_Worker SHALL treat the failure as the `NoNumberingScheme` case of criterion 4 with the DNC error code recorded as the safe reason, and SHALL create no Account.
6. IF the DNC_Issuance_Endpoint is unreachable or returns a transient error, THEN THE AccountCreation_Worker SHALL set the request status to `Retry` per Requirement 2.6 and SHALL create no Account.
7. IF the Account insert fails or its transaction is rolled back after DNC issued a number, THEN THE AccountCreation_Worker SHALL NOT return or reuse the issued number, and a subsequent retry SHALL obtain a new number from DNC (a gap in the series is accepted).
8. IF the issued Account Number collides with an existing Account Number at insert time, THEN THE AccountCreation_Worker SHALL roll back the Account transaction, set the request status to `Retry`, and SHALL create no Account.
9. THE AccountCreation_Service SHALL accept any Account Number length up to the existing Account Number column length and SHALL NOT require a fixed length.
   - **Assumption:** The existing column length is verified during design; if it is shorter than the longest template output permitted by DNC (DNC Format Template maximum 100 characters), the column is widened by migration.
10. WHEN the Account is updated through the API, THE AccountCreation_Service SHALL retain the Account Number unchanged, ignoring any Account Number value in the request.

### Requirement 5: Perform the KYC Check Against Aadhaar and PAN

**User Story:** As a compliance stakeholder, I want the Customer's basic details checked against Aadhaar and PAN when an Application is processed, so that identity problems are visible on the Account.

#### Acceptance Criteria

1. WHEN the AccountCreation_Worker has committed the Account and the KYC Mode is `Internal`, THE AccountCreation_Worker SHALL request a combined `Full` verification from the AccountCreation KYC internal endpoint for the Customer, covering PAN identifier validation, PAN name comparison, Aadhaar identifier validation, and Aadhaar address comparison.
2. WHEN the AccountCreation_Worker submits the KYC request, THE AccountCreation_Worker SHALL supply the Customer's PAN, Aadhaar, display name, and primary address read from the Customer Master and the Correlation Identifier, and SHALL NOT log those values.
3. WHEN the internal endpoint returns the four independent results, THE AccountCreation_Worker SHALL map each result to a KYC Check Result and SHALL store the Verification Session identifier on the Account.
4. THE AccountCreation_Worker SHALL derive the KYC Status from the four KYC Check Results as follows: `Passed` when all four are `Passed`; `Failed` when at least one is `Failed`; `Unavailable` when no check is `Failed` and at least one is `Unavailable`; `PartiallyVerified` otherwise.
5. WHEN the KYC Status is anything other than `Passed`, THE AccountCreation_Worker SHALL set the KYC Flag to true on the Account.
6. WHEN the KYC Status is `Passed`, THE AccountCreation_Worker SHALL set the KYC Flag to false on the Account.
7. IF the Customer has no PAN or no Aadhaar on file, THEN THE AccountCreation_Worker SHALL record the affected checks as `NotChecked`, SHALL derive the KYC Status per criterion 4, and SHALL set the KYC Flag.
8. IF the internal endpoint is unreachable or returns an error after the configured retry policy, THEN THE AccountCreation_Worker SHALL set the KYC Status to `Unavailable`, SHALL set the KYC Flag, and SHALL keep the Account in place.
9. IF the KYC Check fails for any reason, THEN THE AccountCreation_Worker SHALL NOT delete, deactivate, or roll back the Account.
10. WHEN the KYC Check completes, THE AccountCreation_Worker SHALL store `KycCheckedAtUtc`, `KycSource`, and the four KYC Check Results on the Account and SHALL write an immutable audit entry.
11. FOR ALL combinations of four KYC Check Results, THE KYC status derivation SHALL return `Passed` if and only if all four results are `Passed`.
12. FOR ALL combinations of four KYC Check Results, THE KYC Flag SHALL be false if and only if the derived KYC Status is `Passed`.
13. WHEN the AccountCreation_Worker reads the Customer's PAN and Aadhaar, THE AccountCreation_Worker SHALL read them from the `CustomerDocumentReference` rows whose Document code equals the configured `AccountCreation:Kyc:PanDocumentCode` (default `PAN`) or `AccountCreation:Kyc:AadhaarDocumentCode` (default `AADHAAR`), or whose Document has `IsAadhaarDocument` set for Aadhaar.
14. IF no `CustomerDocumentReference` of the PAN or Aadhaar document code exists for the Customer, THEN THE AccountCreation_Worker SHALL read the value from `IndividualCustomerDetails.IndiaIdentityReference` before recording the affected checks as `NotChecked`.

### Requirement 6: Bypass the KYC Check by Configuration

**User Story:** As a product owner, I want to bypass the KYC Check for now, so that Accounts can be created before the KYC integration is production-ready.

#### Acceptance Criteria

1. WHILE the KYC Mode is `Bypass`, THE AccountCreation_Worker SHALL make no call to the internal KYC endpoint and no call to any KYC Aggregator Provider.
2. WHILE the KYC Mode is `Bypass`, THE AccountCreation_Worker SHALL set the KYC Status to `Bypassed`, set all four KYC Check Results to `Bypassed`, set `KycSource` to `Bypass`, set the KYC Flag to true, and set `KycCheckedAtUtc` to the processing time.
   - **Assumption:** A bypassed Account is flagged (not `Passed`) so that Accounts created without verification remain discoverable for later KYC follow-up.
3. THE AccountCreation_Worker SHALL read the KYC Mode at the start of each run, so that a configuration change takes effect on the next run without code changes.
4. THE AccountCreation_Worker SHALL default the KYC Mode to `Bypass` WHEN the setting is absent.
5. IF the KYC Mode is set to a value other than `Bypass`, `Internal`, or `Aggregator`, THEN THE AccountCreation_Worker SHALL treat the run as a configuration error, SHALL claim no requests in that run, and SHALL log a safe error.
6. WHEN the AccountCreation_Service returns an Account, THE AccountCreation_Service SHALL include the KYC Mode recorded at the time of its KYC outcome so that a bypassed Account is distinguishable from a verified Account.

### Requirement 7: Provide a Third-Party KYC Aggregation Provision

**User Story:** As a platform integrator, I want a pluggable third-party aggregation provision for KYC, so that an aggregator can be adopted later without changing the Account Creation flow.

#### Acceptance Criteria

1. THE AccountCreation_Service SHALL define a provider-neutral `IKycAggregatorProvider` contract that accepts a Provider-Neutral KYC Request and returns a Normalized KYC Result.
2. THE Provider-Neutral KYC Request SHALL contain the Customer reference, the PAN, the Aadhaar, the Customer display name, the primary address, and the Correlation Identifier, and SHALL contain no provider-specific field.
3. THE Normalized KYC Result SHALL contain four per-check outcomes, an overall outcome, a provider reference, a safe reason, and a UTC timestamp.
4. WHILE the KYC Mode is `Aggregator`, THE AccountCreation_Worker SHALL obtain the KYC outcome through the configured KYC Aggregator Provider and SHALL map the Normalized KYC Result to the four KYC Check Results using the same derivation as Requirement 5.
5. WHERE a KYC Aggregator Provider is configured, THE AccountCreation_Service SHALL keep provider-specific request and response fields inside the provider adapter.
6. WHERE the KYC Mode is `Aggregator` and no provider is configured, THE AccountCreation_Worker SHALL treat the run as a configuration error and SHALL claim no requests in that run.
7. THE AccountCreation_Service SHALL read provider endpoint, credentials, timeout, and retry settings from secure configuration and SHALL NOT include them in source code, logs, or response contracts.
8. WHEN the provider returns a timeout or transient error, THE AccountCreation_Worker SHALL apply the configured timeout and a maximum of three attempts with exponential backoff before recording the KYC Status as `Unavailable`.
9. THE AccountCreation_Service SHALL store the provider reference on the Account's KYC record and SHALL NOT store raw provider payloads containing PAN or Aadhaar values.
10. WHEN a new provider adapter is added, THE AccountCreation_Service SHALL require no change to the Account entity, the Account Creation Request, the List contract, or the View contract.

### Requirement 8: List, View, and Update Accounts (Backend)

**User Story:** As an Operations User, I want to list, view, and update Accounts, so that I can manage Accounts created from Applications.

#### Acceptance Criteria

1. WHEN an authorized user requests the Account list, THE AccountCreation_Service SHALL return a `PaginatedResponse<AccountListItemResponse>` in which each item contains the Application Number, Account Number, Account Status, Customer Basic Details, KYC Status, KYC Flag, KYC Mode, `KycCheckedAtUtc`, a pending resolution indicator (true when the Account has a `Pending` KYC Resolution Request), and the Account opening date.
2. WHEN an authorized user requests the Account list with a search term that is non-empty after trimming, THE AccountCreation_Service SHALL return only Accounts whose Application Number, Account Number, Customer Code, or Customer display name contains the trimmed term, matched case-insensitively.
3. WHEN an authorized user requests the Account list filtered by KYC Status, KYC Flag, Account Status, Line of Business, Branch, Product code, or Scheme code, THE AccountCreation_Service SHALL return only Accounts matching every supplied filter combined with the search term.
4. IF a search term is longer than 100 characters, THEN THE AccountCreation_Service SHALL reject the request with a validation error and SHALL return no records.
5. WHEN the list request omits page number or page size, THE AccountCreation_Service SHALL apply page 1 and page size 20.
6. IF the page number is less than 1 or the page size is less than 1 or greater than 100, THEN THE AccountCreation_Service SHALL reject the request with a validation error and SHALL return no records.
7. WHEN the list request returns Accounts, THE AccountCreation_Service SHALL order them by `CreatedAt` descending and by Account Number ascending for equal `CreatedAt`.
8. WHEN a requested page number is beyond the last page, THE AccountCreation_Service SHALL return an empty item collection with the correct total count, page number, and page size.
9. WHEN an authorized user requests a single Account by identifier, THE AccountCreation_Service SHALL return an `AccountResponse` containing the list fields plus Line of Business, Branch, Product, Scheme, the four KYC Check Results, the Verification Session identifier, the KYC history entries (including `Pending`, `Approved`, and `Rejected` KYC Resolution Requests), the pending KYC Resolution Request when one exists, the finance terms summary (Finance Amount, IRR, Tenure, Repayment Frequency, Disbursement Date) read from the Application, and creation notes.
10. IF a requested Account identifier does not exist, THEN THE AccountCreation_Service SHALL return HTTP 404 with a stable `ERR-ACC-404` code.
11. WHEN the AccountCreation_Service returns the PAN or the Aadhaar, THE AccountCreation_Service SHALL return only masked values showing at most the last four characters.
12. WHEN an authorized user updates an Account with a new `AccountName` of 1 to 150 characters, THE AccountCreation_Service SHALL persist the change, refresh `UpdatedAt`, write an audit entry, and return the updated Account.
13. WHEN an authorized user updates the Account Status, THE AccountCreation_Service SHALL permit only the transitions `Active` to `Inactive`, `Active` to `Frozen`, `Inactive` to `Active`, and `Frozen` to `Active`, and SHALL write an audit entry for each transition.
14. IF an update requests a status transition that is not permitted, THEN THE AccountCreation_Service SHALL reject the request with HTTP 409 and `ERR-ACC-409`, and SHALL leave the Account unchanged.
15. IF an Account has status `Closed`, THEN THE AccountCreation_Service SHALL reject every update to that Account with HTTP 409 and SHALL leave the Account unchanged.
16. WHEN an update request contains Application Number, Account Number, Customer, Line of Business, Branch, Product, Scheme, Type, Currency, Balance, or opening date values, THE AccountCreation_Service SHALL ignore those values and SHALL NOT alter those fields.
17. WHEN an update request contains values equal to the stored values, THE AccountCreation_Service SHALL treat the update as a successful no-op, SHALL leave `UpdatedAt` unchanged, and SHALL return the existing record.
18. WHEN two update requests modify the same Account concurrently, THE AccountCreation_Service SHALL use a row version concurrency token so that the losing request returns HTTP 409 and does not overwrite the winning change.
19. THE AccountCreation_Service SHALL NOT provide a create or delete operation for Loan Accounts through this API.
20. THE AccountCreation_Service SHALL preserve the existing `AccountsController` operations (`GET {id}`, `GET user/{userId}`, `POST`, `POST {id}/close`) with their existing compatible behavior.
21. THE AccountCreation_Service SHALL expose the Account List, View, Update, KYC resolution, and operational endpoints under the controller route `api/loan-accounts`.

### Requirement 9: Resolve a KYC Flag Manually with Maker-Checker

**User Story:** As a compliance officer, I want a manual KYC resolution to need a second person's approval, so that Accounts verified outside the system are cleared under the four-eyes principle with a recorded reason.

#### Acceptance Criteria

1. WHEN a Maker submits a KYC Resolution Request with a reason of 10 to 500 characters for an Account whose KYC Flag is true and which has no `Pending` KYC Resolution Request, THE AccountCreation_Service SHALL persist the request with status `Pending`, the Maker, the reason, the previous KYC Status, and a UTC timestamp, and SHALL leave the Account's KYC Status and KYC Flag unchanged.
2. WHEN a Checker approves a `Pending` KYC Resolution Request, THE AccountCreation_Service SHALL set the request status to `Approved`, set the KYC Status to `Passed`, set the KYC Flag to false, set `KycSource` to `Manual`, and append a KYC history entry with the Maker, the Checker, the reason, the comment, the previous status, and a UTC timestamp.
3. WHEN a Checker rejects a `Pending` KYC Resolution Request with a comment, THE AccountCreation_Service SHALL set the request status to `Rejected`, record the Checker and the comment, and SHALL leave the Account's KYC Status and KYC Flag unchanged.
4. IF the Checker is the same user as the Maker of the request, THEN THE AccountCreation_Service SHALL reject the approval or rejection with HTTP 403 and SHALL leave the request and the Account unchanged.
5. IF an approval or rejection is submitted without a comment, THEN THE AccountCreation_Service SHALL reject the request with a validation error and SHALL leave the request and the Account unchanged.
6. THE AccountCreation_Service SHALL retain every earlier automated KYC outcome and every `Pending`, `Approved`, and `Rejected` KYC Resolution Request in the KYC history and SHALL NOT overwrite or delete them.
7. IF a KYC Resolution Request is submitted with a missing reason or a reason outside 10 to 500 characters, THEN THE AccountCreation_Service SHALL reject the request with a validation error and SHALL leave the Account unchanged.
8. IF a KYC Resolution Request is submitted for an Account whose KYC Flag is already false, THEN THE AccountCreation_Service SHALL reject the request with HTTP 409 and SHALL leave the Account unchanged.
9. IF a KYC Resolution Request is submitted for an Account that already has a `Pending` KYC Resolution Request, THEN THE AccountCreation_Service SHALL reject the request with HTTP 409 and SHALL leave the existing request unchanged.
10. IF an approval or rejection targets a KYC Resolution Request whose status is not `Pending`, THEN THE AccountCreation_Service SHALL reject the request with HTTP 409 and SHALL leave the request unchanged.
11. WHEN an authorized user requests a re-run of the KYC Check for a flagged Account, THE AccountCreation_Service SHALL set a KYC re-check marker that the AccountCreation_Worker processes on its next run under the current KYC Mode, SHALL NOT create a second Account, and SHALL append the new outcome to the KYC history.
12. IF the caller lacks the role `ApplicationProcessor` for submitting a KYC Resolution Request or the role `KycApprover` for approving or rejecting one, THEN THE AccountCreation_Service SHALL reject the request with HTTP 403.
13. FOR ALL sequences of KYC Resolution Request actions on one Account, THE AccountCreation_Service SHALL hold at most one `Pending` KYC Resolution Request, and the KYC Status SHALL become `Passed` with `KycSource` `Manual` only through an approval by a Checker different from the Maker.

### Requirement 10: Operational Visibility and Recovery

**User Story:** As a platform operator, I want to see and recover stuck or failed Account Creation Requests, so that no Processed Application is left without an Account.

#### Acceptance Criteria

1. WHEN an authorized operator requests the queue status, THE AccountCreation_Service SHALL return the count of Account Creation Requests per status (`Pending`, `Processing`, `Retry`, `Completed`, `DeadLetter`) and the age of the oldest non-completed request.
2. WHEN an authorized operator requests the dead-letter list, THE AccountCreation_Service SHALL return a `PaginatedResponse` of dead-lettered requests with Application Number, failure reason, attempt count, and last attempt timestamp.
3. WHEN an authorized operator requests reprocessing of a dead-lettered request, THE AccountCreation_Service SHALL set the status to `Pending`, reset the attempt count to 0, and write an audit entry with the actor and reason.
4. IF an operator requests reprocessing of a request whose status is not `DeadLetter`, THEN THE AccountCreation_Service SHALL reject the request with HTTP 409 and SHALL leave the request unchanged.
5. IF a request in `Processing` has an expired Claim Lease, THEN THE AccountCreation_Worker SHALL treat the request as claimable by the next run.
6. THE AccountCreation_Service SHALL restrict the operational endpoints to the operations authorization policy of Assumption A17.

### Requirement 11: Security, Authentication, and Audit

**User Story:** As a security stakeholder, I want Account operations restricted and audited, so that only authorized users can see or change Accounts and sensitive identity data stays protected.

#### Acceptance Criteria

1. IF a request to list, view, or update an Account is received with a missing, malformed, signature-invalid, or expired JWT Bearer token, THEN THE AccountCreation_Service SHALL reject the request with HTTP 401 and SHALL disclose no Account data.
2. IF an authenticated user lacks the required authorization claim for the operation, THEN THE AccountCreation_Service SHALL reject the request with HTTP 403 and SHALL change no Account.
3. WHEN a request fails both token validity and the authorization claim, THE AccountCreation_Service SHALL return HTTP 401 and not HTTP 403.
4. THE AccountCreation_Service SHALL store PAN and Aadhaar values only within the Customer Master and the identity verification capability, and SHALL store no raw PAN or Aadhaar on the Account or its KYC history.
5. THE AccountCreation_Service SHALL exclude PAN, Aadhaar, tokens, and provider credentials from application logs, error responses, and audit old/new value snapshots.
6. WHEN the AccountCreation_Service creates an error response, THE AccountCreation_Service SHALL include a stable `ERR-ACC-*` code, an English `message`, a `detail`, and the Correlation Identifier, consistent with the application-processing error contract.
7. WHEN an Account is created, updated, status-changed, re-checked, or has a KYC Resolution Request submitted, approved, or rejected, THE AccountCreation_Service SHALL write an immutable audit entry containing actor, source, Correlation Identifier, change summary, and UTC timestamp.
8. THE AccountCreation_Service SHALL expose no update or delete operation on audit entries.

### Requirement 12: Gateway Routing

**User Story:** As a platform integrator, I want the Account endpoints reachable through the API gateway, so that the UI calls only the gateway origin.

#### Acceptance Criteria

1. THE Finnova.ApiGateway SHALL forward `/api/accounts/**` requests to `accounts-cluster`, preserving the Authorization header, and SHALL forward `/api/loan-accounts/**` requests to `accounts-cluster`.
2. WHERE the shared UI base URL composes feature paths under the `/api/ua/api` prefix, THE Finnova.ApiGateway SHALL provide aliases `/api/ua/api/loan-accounts` and `/api/ua/api/loan-accounts/{**catch-all}` that forward to `accounts-cluster` and rewrite the path to the Accounts host controller route `/api/loan-accounts`, matching the application-processing alias convention.
3. THE Finnova.ApiGateway SHALL keep the Accounts host separate from the SystemAdmin host and SHALL NOT route Account endpoints to `systemadmin-cluster`.
4. THE internal KYC endpoint of Assumption A15 and the DNC_Issuance_Endpoint of Assumption A23 SHALL NOT be exposed through the public gateway routes.

### Requirement 13: Account List Screen (Frontend)

**User Story:** As an Operations User, I want an Account List screen, so that I can find Accounts and see their KYC status at a glance.

#### Acceptance Criteria

1. WHEN an authorized user opens the Account List screen, THE AccountCreation_UI SHALL display a table with columns Application Number, Account Number, Customer Code, Customer Name, Mobile (masked), City, Account Status, and KYC Status with a KYC Flag indicator and a pending KYC resolution indicator.
2. WHEN the Account List screen loads, THE AccountCreation_UI SHALL fetch the first page of Accounts through the shared axios instance and the Account service.
3. THE AccountCreation_UI SHALL provide a search input and filters for KYC Status, KYC Flag, Account Status, Line of Business, Branch, Product, and Scheme, with filter options populated from the existing reference and Lookup sources.
4. WHEN a user changes the search term or a filter, THE AccountCreation_UI SHALL request page 1 with the new criteria.
5. WHILE a list request is in progress, THE AccountCreation_UI SHALL show a loading indicator.
6. IF the list request returns an error, THEN THE AccountCreation_UI SHALL show the error message from the response and SHALL retain the current filters.
7. IF the list returns no Accounts, THEN THE AccountCreation_UI SHALL display an empty state.
8. WHEN the KYC Status is `Bypassed`, THE AccountCreation_UI SHALL display it with a distinct label so that it is distinguishable from `Passed`.
9. WHEN a user selects an Account row, THE AccountCreation_UI SHALL navigate to the Account View screen for that Account.
10. THE AccountCreation_UI SHALL render all labels and messages in English with no RTL layout and no bilingual field variants.

### Requirement 14: Account View and Update Screens (Frontend)

**User Story:** As an Operations User, I want to view and update an Account, so that I can review KYC details and maintain the Account.

#### Acceptance Criteria

1. WHEN a user opens the Account View screen, THE AccountCreation_UI SHALL display the Account details, the Application Number with a link to the Application, Customer Basic Details, finance terms summary, the four KYC Check Results, the KYC history, and a link to the Amortization Schedule of the Application.
2. THE AccountCreation_UI SHALL display PAN and Aadhaar values only as masked values returned by the API.
3. WHEN a user with update permission edits the Account Name or Account Status and saves, THE AccountCreation_UI SHALL call the update operation and display the returned Account.
4. THE AccountCreation_UI SHALL offer only the permitted status transitions of Requirement 8 for the current Account Status.
5. WHEN a Maker submits a KYC Resolution Request with a reason of 10 to 500 characters, THE AccountCreation_UI SHALL call the resolution request operation and display the pending resolution indicator while leaving the displayed KYC Status unchanged.
6. IF the reason for a KYC Resolution Request is missing or outside 10 to 500 characters, or the comment of an approval or rejection is missing, THEN THE AccountCreation_UI SHALL show a field-level validation message and SHALL NOT call the service.
7. IF an update or resolution returns an error, THEN THE AccountCreation_UI SHALL show the response message and SHALL retain the entered values.
8. WHILE a save is in progress, THE AccountCreation_UI SHALL disable further submission of the same form.
9. WHERE the signed-in user lacks update permission, THE AccountCreation_UI SHALL display the Account in read-only mode.
10. THE AccountCreation_UI SHALL follow the existing service-interface, mock, real, and `VITE_USE_MOCK_API` toggle pattern, use the shared axios instance, use React hooks and Context only, and register a protected route and a navigation entry.
11. WHILE an Account has a `Pending` KYC Resolution Request, THE AccountCreation_UI SHALL display the pending request details and SHALL hide the resolution request action.
12. WHERE the signed-in user holds the role `KycApprover` and is not the Maker of the `Pending` request, THE AccountCreation_UI SHALL offer approve and reject actions with a mandatory comment and SHALL display the updated KYC Status after approval.
13. WHERE the signed-in user is the Maker of the `Pending` request, THE AccountCreation_UI SHALL NOT offer approve or reject actions.

### Requirement 15: Verification Coverage

**User Story:** As an engineering stakeholder, I want the feature verified at the right level, so that financial and KYC logic is trustworthy.

#### Acceptance Criteria

1. THE project SHALL include FsCheck.Xunit property tests of at least 100 iterations for the KYC status derivation, the KYC Flag derivation, the exhaustive Scheme Resolution Fallback order of the DNC `SchemeResolver`, the status transition rule, and the maker-checker rules (four-eyes, single pending request, Passed only on approval).
2. THE project SHALL include `pytest` and `hypothesis` tests for the worker's claim logic, KYC status derivation, the worker's DNC client against a mocked issuance endpoint (success, no scheme mapped to `NoNumberingScheme`, inactive scheme, transient failure, gap on retry), and idempotent reprocessing, and SHALL NOT re-implement the Scheme Resolution Fallback in the worker.
3. THE project SHALL include xUnit and `WebApplicationFactory<Program>` tests for list filtering, pagination, ordering, view, update, no-op update, concurrency conflict, closed-account rejection, 401-before-403 behavior, gateway alias wiring for `/api/ua/api/loan-accounts`, KYC Resolution Request submission, approval, rejection, self-approval rejection, duplicate pending rejection, and the pending resolution indicator.
4. THE project SHALL include integration tests proving that processing an Application inserts exactly one Account Creation Request, that a KYC failure leaves the Account in place with the KYC Flag set, and that reprocessing creates no second Account.
5. THE project SHALL include Vitest and React Testing Library tests for the Account service (mock and real), list rendering, filters, empty and error states, the bypassed label, view rendering with masked values, update and resolution validation, the pending resolution indicator, approve and reject actions hidden for the Maker, and read-only mode.
6. THE project SHALL include tests for the Customer Master Mapping Profile (name and composed address mapping) and for the seeding of the roles `AccountsViewer`, `AccountsOperator`, and `KycApprover`.

### Requirement 16: DNC Extension for Line of Business and Branch Numbering

**User Story:** As a System Administrator, I want Document Number Control to support Line of Business and Branch in numbering, so that Loan Account Numbers have independent sequences per LOB and Branch combination.

#### Acceptance Criteria

1. THE DNC_Service SHALL support an optional Line of Business dimension on a Numbering Scheme scope, carrying a LOB Scope Id, so that one Document Type can have a distinct scheme and sequence per LOB.
2. THE DNC_Service SHALL support a combined scope of Line of Business and Branch, so that a `LoanAccount` scheme can yield an independent sequence for each LOB and Branch combination.
3. THE DNC_Service SHALL support the Format Template token `{LOB}`, substituted with the Line of Business code at issuance.
4. THE DNC_Service SHALL support the optional Format Template token `{BRANCH}`, substituted with the Branch code at issuance.
5. WHEN the DNC_Service validates a Format Template, THE DNC_Service SHALL accept `{LOB}` and `{BRANCH}` as supported tokens in addition to the tokens defined in DNC Requirement 1.12.
6. IF a Format Template contains `{LOB}` and the scheme scope has no Line of Business, or contains `{BRANCH}` and the scheme scope has no Branch, THEN THE DNC_Service SHALL reject the scheme with a validation error identifying the token and SHALL NOT persist any record.
7. THE DNC_Service SHALL key the Sequence State by scheme and by the full scope (Scope, Scope Id, LOB, and Branch), so that sequences of different LOB and Branch combinations never share a counter.
8. WHEN the DNC_Issuance_Endpoint receives an issuance request for Document Type `LoanAccount` with a Line of Business and a Branch, THE SchemeResolver SHALL resolve the scheme server-side using the Scheme Resolution Fallback (LOB+Branch, LOB, Branch, Global), IF no active scheme resolves THEN THE DNC_Service SHALL return a no-scheme error without advancing any sequence, and otherwise THE DNC_Service SHALL issue the number from the sequence of the scheme that was resolved.
9. THE DNC_Service SHALL treat the combination of Document Type, Scope, Scope Id, and Line of Business as unique across the DNC Master, extending DNC Requirement 2.3.
10. THE DNC_Service SHALL expose the DNC_Issuance_Endpoint as an internal, service-authenticated operation accepting the seeded `SystemUser` credential and SHALL NOT expose it through the public gateway.
11. THE document-number-control-master spec SHALL be updated to reflect this extension in these areas: the Scope enum and Scope columns (LOB dimension and Scope Id for LOB), Sequence State keying, Format Template tokens (`{LOB}`, `{BRANCH}`), validators (token and scope consistency, uniqueness), the issuance request and response contract (Line of Business and Branch inputs), the internal issuance endpoint and its authorization, and the DNC Master UI (scope selector, LOB and Branch pickers, token help).
   - **Assumption:** The update to `.kiro/specs/document-number-control-master` is NOT made by this feature and is tracked as a separate change; this requirement records the dependency only.
12. IF the Line of Business or Branch supplied to the DNC_Issuance_Endpoint does not exist, THEN THE DNC_Service SHALL reject the request with a validation error and SHALL NOT advance any sequence.

### Requirement 17: Customer Master Mapping Profile in the Identity Verification Registry

**User Story:** As a compliance stakeholder, I want the identity verification registry to know how Customer Master data maps to verification inputs, so that the KYC Check compares the correct name and address.

#### Acceptance Criteria

1. THE Mapping Registry SHALL contain a Customer Master Mapping Profile added by this feature, because no customer-master profile exists in the registry today.
2. WHEN the Customer Master Mapping Profile is applied to a Customer, THE Mapping Registry SHALL map the Customer display name assembled from the Customer Master name parts to the verification name.
3. WHEN the Customer Master Mapping Profile is applied to a Customer, THE Mapping Registry SHALL map the composed primary address of the Customer to the verification address.
4. IF the Customer has no name or no primary address, THEN THE Mapping Registry SHALL produce an empty mapped value for the missing field so that the affected check is recorded as `NotChecked`.
5. THE Customer Master Mapping Profile SHALL contain no PAN or Aadhaar values and SHALL NOT alter existing profiles of the Mapping Registry.
6. FOR ALL Customers with identical name parts and address parts, THE Customer Master Mapping Profile SHALL produce identical mapped name and address values (deterministic mapping).

### Requirement 18: Role Creation and Seeding

**User Story:** As a System Administrator, I want the roles required by this feature created, so that access to Accounts and the maker-checker flow can be granted.

#### Acceptance Criteria

1. THE AccountCreation_Service SHALL create and seed the roles `AccountsViewer`, `AccountsOperator`, and `KycApprover` where they do not already exist.
2. THE AccountCreation_Service SHALL ensure the role `ApplicationProcessor` exists and SHALL NOT alter its existing grants.
3. WHEN the seeding runs more than once, THE AccountCreation_Service SHALL create no duplicate role (idempotent seeding).
4. THE AccountCreation_Service SHALL register authorization policies that map List and View to `AccountsViewer` or `ApplicationProcessor`, Update and KYC Resolution Request submission to `ApplicationProcessor`, KYC Resolution approval and rejection to `KycApprover`, and operational endpoints to `AccountsOperator`, with `SystemAdmin` access only through explicit policy configuration.
