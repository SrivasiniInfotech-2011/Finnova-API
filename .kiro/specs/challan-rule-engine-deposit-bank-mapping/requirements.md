# Requirements Document

## Introduction

FINNOVA-65, **Challan Rule Engine – Deposit Bank Mapping**, provides an India-specific SystemAdmin master for routing cheque instruments before challan preparation and authorization. The authoritative UI is the **Challan Rule Creation** screen. One creation or edit operation represents one active Challan Rule Set for exactly one `Line of Business + Location + Deposit Bank Code` combination and a collection of Drawee Bank mapping rows.

The screen allows an Operations Administrator to choose the required Line of Business, Location, and Deposit Bank Codes values, view the read-only Account No resolved from the selected deposit-bank account, mark the set Active, and manage Drawee Bank rows through Add, Edit, and Delete actions. Save, Clear, and Cancel complete the form workflow. The screen uses English-only India examples such as `LN - Loan`, `Mumbai`, `HDFC Bank`, `HDFC`, and `SBI`.

FINNOVA-65 is separate from FINNOVA-16 `ChallanRule`. FINNOVA-16 remains the format, validation, and routing entity keyed only by `DraweeBankId`. FINNOVA-65 owns the LOB/Location/deposit-bank mapping master and does not replace, extend, or duplicate FINNOVA-16 `ChallanRule`.

Receipt Processing resolves an active FINNOVA-65 rule before challan preparation, authorization, and JVE submission. Receipt Processing persists an immutable routing snapshot containing the rule-set identity/version, matched Drawee Bank or `OTHERS`, deposit-bank identity/account details, and resolution timestamp. FINNOVA-63 includes that snapshot in the JVE request. JVE remains the only canonical journal owner, and FINNOVA-65 performs no resolution after posting.

The backend belongs in `Finnova.SystemAdminService` behind the existing YARP gateway. The frontend belongs in `E:\Finnova\Finnova-UI\Finnova-UI` and follows the existing React 18, TypeScript, MUI, shared axios, service interface, real/mock/toggle, and Vitest/React Testing Library conventions. No Arabic, bilingual fields, non-English values, RTL support, Redux, RxJS, or ad-hoc axios client is introduced.

## Glossary

- **Challan_Rule_Set**: The FINNOVA-65 aggregate for exactly one Line of Business, one level-5 Location/Branch, and one Deposit Bank Code. The set owns the Drawee Bank mapping rows, lifecycle state, version, and audit history.
- **Challan_Rule_Creation_UI**: The SystemAdmin React screen titled `Challan Rule Creation` that creates and edits one Challan_Rule_Set at a time.
- **Line_of_Business (LOB)**: The existing LineOfBusiness master row selected by the UI and stored as `LineOfBusinessId`.
- **Location**: The UI term for the existing authoritative level-5 Branch master row. Backend contracts may use `BranchId` while UI labels and requests use `Location`.
- **Deposit_Bank_Code**: The value selected from the Deposit Bank Codes dropdown. The authoritative account source and whether this value is a bank code or account code are recorded assumptions.
- **Deposit_Bank_Account**: The authoritative deposit-bank account resolved from the selected Deposit_Bank_Code. Account No is derived from this record and is read-only in the UI.
- **Drawee_Bank**: An existing active FINNOVA-16 drawee-bank master value available for mapping.
- **Drawee_Bank_Mapping_Row**: A row containing one concrete Drawee Bank or the reserved `OTHERS` value for the selected Challan_Rule_Set.
- **OTHERS**: The reserved catch-all mapping value used when no concrete Drawee Bank mapping matches an incoming key.
- **Active_Set**: A Challan_Rule_Set whose Active flag is true and whose effective period, if used, includes the evaluation date.
- **Routing_Snapshot**: Immutable pre-posting facts containing rule-set ID/version, matched Drawee Bank or `OTHERS`, deposit-bank identity/account snapshot, and UTC resolution timestamp.
- **Runtime_Resolver**: The internal FINNOVA-65 service that resolves one incoming Drawee Bank to one active routing snapshot.
- **Receipt_Processing**: The pre-posting workflow that prepares a cheque challan and obtains the Routing_Snapshot.
- **JVE**: The JV Posting Engine. JVE owns canonical journal creation and posting.
- **System_Administrator**: An authenticated user authorized by the existing SystemAdmin policy.
- **Problem_Details**: The platform's structured English error response containing a stable code, message, and correlation identifier.
- **Idempotency_Key**: A client-provided key that allows a retried mutation to return the original committed result without applying a second mutation.
- **EARS**: Easy Approach to Requirements Syntax for ubiquitous, event-driven, state-driven, unwanted-event, optional-feature, and complex requirements.

## Dependencies/Assumptions

- **Dependency D1, authoritative masters:** Reuse the existing LineOfBusiness, level-5 Location/Branch, and FINNOVA-16 DraweeBank masters. FINNOVA-65 creates no parallel master tables.
- **Dependency D2, separate FINNOVA-16 entity:** FINNOVA-16 `ChallanRule` remains keyed only by `DraweeBankId` and continues to own format, validation, and routing configuration. FINNOVA-65 uses separate `ChallanRuleSet` and `DraweeBankMappingRow` concepts.
- **Dependency D3, consumer boundary:** Receipt Processing and FINNOVA-63 consume an internal resolver contract and do not read FINNOVA-65 tables directly.
- **Dependency D4, gateway and UI conventions:** The backend uses the existing SystemAdmin host and gateway. The frontend uses the shared axios instance and existing mock/real service toggle.
- **Assumption A1, Deposit Bank Codes meaning:** `Deposit Bank Codes` is assumed to select an authoritative deposit-bank account record by bank code. If the delivered master treats the value as an account code, the contract and labels must preserve the screenshot wording while mapping to that account-code identity.
- **Assumption A2, Account No resolution:** Account No is assumed to be server-resolved from the selected active Deposit_Bank_Account and returned with the selected bank record. The user cannot type or edit Account No.
- **Assumption A3, full replacement editing:** Editing a saved Challan_Rule_Set is assumed to submit the complete current Drawee Bank row list. The server atomically reconciles additions, retained rows, and deletions/inactivations in one transaction.
- **Assumption A4, inactive visibility:** Inactive Challan_Rule_Sets remain visible in administrative list and detail results with Active unchecked. Inactive sets are excluded from runtime resolution.
- **Assumption A5, one active set:** At most one Active_Set exists for the same LOB + Location/Branch + Deposit_Bank_Code combination. Historical inactive sets may remain visible.
- **Assumption A6, concrete uniqueness:** A concrete Drawee Bank can be mapped to at most one deposit-bank set for the same LOB + Location/Branch. The rule applies across deposit-bank codes, not only within one set.
- **Assumption A7, OTHERS uniqueness:** `OTHERS` can belong to only one Active_Set per LOB + Location/Branch. An inactive set may retain historical `OTHERS` data but cannot participate in runtime resolution.
- **Assumption A8, key normalization:** Drawee Bank keys are trimmed and uppercased with invariant culture before validation, uniqueness checks, persistence, and resolution. `others`, ` Others `, and `OTHERS` become `OTHERS`.
- **Assumption A9, lifecycle dates:** The first release may retain effective dates for runtime eligibility. If the existing product contract does not use dates, the compatibility gate removes only date fields without changing the uniqueness rules.
- **Assumption A10, mutation safety:** Save uses an expected rule-set version and Idempotency_Key. Database constraints and compare-and-set handling remain authoritative under concurrent administrators.
- **Assumption A11, snapshot timing:** Receipt Processing resolves and persists the Routing_Snapshot before challan authorization or JVE submission. Later master changes cannot alter the snapshot.
- **Assumption A12, fail closed:** Missing, inactive, ambiguous, or stale configuration prevents challan/JVE submission and returns an English configuration error.
- **Assumption A13, product localization:** All user-facing labels, lookup values, fixtures, and tests are English and India-specific. `LN - Loan`, `Mumbai`, `HDFC Bank`, `HDFC`, and `SBI` are representative values; non-India and non-English examples are excluded.
- **Assumption A14, repository paths:** Backend paths are relative to `E:\Finnova\Finnova-API`; frontend paths are relative to `E:\Finnova\Finnova-UI\Finnova-UI`.

## Requirements

### Requirement 1: Render the Challan Rule Creation Screen

**User Story:** As a System Administrator, I want the Challan Rule Creation screen to match the approved UI, so that I can create one unambiguous rule set.

#### Acceptance Criteria

1. WHEN a System Administrator opens the creation or edit screen, THE Challan_Rule_Creation_UI SHALL display the title `Challan Rule Creation`.
2. THE Challan_Rule_Creation_UI SHALL display required dropdown fields labeled `Line of Business`, `Location`, and `Deposit Bank Codes`.
3. THE Challan_Rule_Creation_UI SHALL display a read-only field labeled `Account No` and an `Active` checkbox.
4. THE Challan_Rule_Creation_UI SHALL display a mapping grid with columns `Sl.No.`, `Drawee Bank`, and actions.
5. WHEN the mapping grid contains a persisted row, THE Challan_Rule_Creation_UI SHALL display `Edit` and `Delete` actions for that row.
6. WHEN the mapping grid contains an empty new row, THE Challan_Rule_Creation_UI SHALL display an `Add` action for that row.
7. THE Challan_Rule_Creation_UI SHALL display footer actions `Save`, `Clear`, and `Cancel`.

### Requirement 2: Select the One-Set Scope

**User Story:** As a System Administrator, I want dependent scope fields, so that each screen operation targets one LOB, Location, and deposit bank combination.

#### Acceptance Criteria

1. WHEN a System Administrator selects a Line of Business, THE Challan_Rule_Creation_UI SHALL load and enable only Locations valid for the selected active LOB.
2. WHEN a System Administrator selects a Location, THE Challan_Rule_Creation_UI SHALL load and enable the Deposit Bank Codes valid for the selected LOB and Location.
3. WHEN a System Administrator selects a Deposit Bank Code, THE Challan_Rule_Creation_UI SHALL request the corresponding active Deposit_Bank_Account and SHALL populate Account No from the server response.
4. IF a required parent selection changes, THEN THE Challan_Rule_Creation_UI SHALL clear dependent selections, clear Account No, and remove unsaved mapping rows that belong to the previous combination.
5. THE Challan_Rule_Creation_UI SHALL not present one creation or edit form containing multiple Deposit Bank Codes or multiple deposit-bank groups.
6. WHEN a System Administrator submits a valid scope, THE Challan_Rule_Mapping_Service SHALL persist one Challan_Rule_Set with one LOB, one authoritative level-5 Branch referenced by the UI as Location, and one Deposit_Bank_Code.

### Requirement 3: Resolve and Protect Account No

**User Story:** As a System Administrator, I want Account No resolved from the selected deposit bank, so that the routing account cannot be manually altered on the screen.

#### Acceptance Criteria

1. WHEN the selected Deposit Bank Code has one active Deposit_Bank_Account, THE Challan_Rule_Creation_UI SHALL display the returned Account No as read-only text.
2. IF the selected Deposit Bank Code has no active Deposit_Bank_Account, THEN THE Challan_Rule_Mapping_Service SHALL reject Save with an English configuration error and THE Challan_Rule_Creation_UI SHALL preserve the selected fields and Drawee Bank rows.
3. IF the selected Deposit Bank Code resolves to multiple active Deposit_Bank_Account records, THEN THE Challan_Rule_Mapping_Service SHALL reject Save with an English ambiguity error and THE Challan_Rule_Creation_UI SHALL preserve the selected fields and Drawee Bank rows.
4. THE Challan_Rule_Creation_UI SHALL not provide an editable input control for Account No.
5. WHEN a saved set is returned, THE Challan_Rule_Mapping_Service SHALL return the Deposit Bank Code and resolved account snapshot used by the set.

### Requirement 4: Manage Drawee Bank Mapping Rows

**User Story:** As a System Administrator, I want multiple Drawee Banks in one selected scope, so that concrete banks and the OTHERS fallback route correctly.

#### Acceptance Criteria

1. WHEN a System Administrator uses Add with a valid Drawee Bank, THE Challan_Rule_Creation_UI SHALL append one unsaved row to the selected LOB + Location + Deposit Bank Code combination.
2. WHEN a System Administrator uses Edit on a persisted row, THE Challan_Rule_Creation_UI SHALL load that row's Drawee Bank value into the row editor without changing the selected scope.
3. WHEN a System Administrator uses Delete on a row, THE Challan_Rule_Creation_UI SHALL mark the row for removal from the pending Save without deleting server data before Save.
4. WHEN a System Administrator saves a valid row list, THE Challan_Rule_Mapping_Service SHALL persist every retained Drawee Bank row in the selected Challan_Rule_Set and SHALL reconcile removed rows atomically.
5. WHEN no concrete Drawee Bank matches an incoming key and one active `OTHERS` row exists for the LOB + Location, THE Runtime_Resolver SHALL use the `OTHERS` row's deposit-bank account.
6. THE Challan_Rule_Mapping_Service SHALL allow multiple Drawee Bank rows in one Challan_Rule_Set while assigning each concrete key to the selected deposit bank.

### Requirement 5: Enforce Cross-Set Uniqueness

**User Story:** As a System Administrator, I want conflicting mappings rejected, so that one concrete Drawee Bank has one deposit-bank destination within an LOB and Location.

#### Acceptance Criteria

1. IF an Active_Set already exists for the same LOB + Location + Deposit_Bank_Code, THEN THE Challan_Rule_Mapping_Service SHALL reject creation of another Active_Set for that combination and SHALL preserve the existing set.
2. IF a concrete Drawee Bank is already active in a different deposit-bank set for the same LOB + Location, THEN THE Challan_Rule_Mapping_Service SHALL reject the complete Save with the English conflict message `Drawee bank is already mapped for this LOB and Location`.
3. IF an Active_Set already contains `OTHERS` for an LOB + Location, THEN THE Challan_Rule_Mapping_Service SHALL reject adding `OTHERS` to another Active_Set for that LOB + Location with the English conflict message `OTHERS is already mapped for this LOB and Location`.
4. IF a single Save contains duplicate normalized concrete keys or more than one `OTHERS` row, THEN THE Challan_Rule_Mapping_Service SHALL reject the complete Save and SHALL persist no row change.
5. THE Challan_Rule_Mapping_Service SHALL enforce active-set, concrete-key, and `OTHERS` uniqueness in application validation and database constraints or equivalent transactional locking.
6. WHEN a concrete Drawee Bank row and an `OTHERS` row both exist in one LOB + Location, THE Runtime_Resolver SHALL prefer the concrete row for the matching key.

### Requirement 6: Validate and Save Atomically

**User Story:** As a System Administrator, I want Save to validate the complete screen before committing, so that partial rule sets cannot route instruments.

#### Acceptance Criteria

1. WHEN a System Administrator selects all required fields and submits a valid Drawee Bank list, THE Challan_Rule_Mapping_Service SHALL commit the Challan_Rule_Set, account snapshot, rows, version increment, and audit entry in one transaction.
2. IF Line of Business, Location, Deposit Bank Codes, or Drawee Bank is missing or inactive, THEN THE Challan_Rule_Mapping_Service SHALL return field-specific English validation errors and SHALL persist no part of the Save.
3. IF a Save fails validation, uniqueness, concurrency, dependency, or persistence checks, THEN THE Challan_Rule_Mapping_Service SHALL roll back all set and row changes, SHALL leave the previous version unchanged, and SHALL return Problem_Details.
4. WHEN Save succeeds, THE Challan_Rule_Creation_UI SHALL refresh the set from the server and SHALL display the committed Active state, Account No, Drawee Bank rows, and rule-set version.
5. WHEN a Save request is retried with the same Idempotency_Key and equivalent payload, THE Challan_Rule_Mapping_Service SHALL return the original committed result without a second version increment or audit success event.
6. IF a Save request supplies an old expected rule-set version, THEN THE Challan_Rule_Mapping_Service SHALL return an English concurrency conflict and THE Challan_Rule_Creation_UI SHALL preserve unsaved form values.

### Requirement 7: Provide Clear and Cancel Behavior

**User Story:** As a System Administrator, I want safe footer actions, so that I can reset or abandon a form without accidental persistence.

#### Acceptance Criteria

1. WHEN a System Administrator clicks Clear, THE Challan_Rule_Creation_UI SHALL reset Line of Business, Location, Deposit Bank Codes, Account No, Active, and all pending Drawee Bank rows to the initial form state.
2. WHEN a System Administrator changes Line of Business or Location, THE Challan_Rule_Creation_UI SHALL clear dependent fields and Account No before accepting new dependent values.
3. WHEN a System Administrator clicks Cancel before Save, THE Challan_Rule_Creation_UI SHALL navigate to the preceding master/list context without sending a mutation request.
4. WHEN a System Administrator clicks Cancel after loading an existing set, THE Challan_Rule_Creation_UI SHALL leave the persisted set unchanged.

### Requirement 8: Expose Administrative and Runtime Contracts

**User Story:** As SystemAdmin UI and Receipt Processing, I want stable contracts, so that configuration and pre-posting resolution use the same one-set model.

#### Acceptance Criteria

1. THE Challan_Rule_Mapping_Service SHALL expose authenticated list, detail, create, full-replacement update, activate/inactivate, and audit operations for Challan_Rule_Set records.
2. THE Challan_Rule_Mapping_Service SHALL expose a paginated administrative response containing LOB, UI Location label, Deposit Bank Code, Account No snapshot, Active state, version, and timestamps.
3. THE Runtime_Resolver SHALL expose an internal request containing LOB, authoritative level-5 Branch/Location, incoming Drawee Bank key, evaluation date, and correlation ID.
4. WHEN the Runtime_Resolver succeeds, THE Receipt_Processing SHALL persist a Routing_Snapshot containing rule-set ID/version, matched Drawee Bank or `OTHERS`, deposit-bank identity/account snapshot, and UTC resolution timestamp before authorization/JVE submission.
5. WHEN FINNOVA-63 creates a JVE request, THE JVE request SHALL include the persisted Routing_Snapshot without creating or owning the canonical journal in FINNOVA-65.
6. WHILE a journal is posted, THE platform SHALL not call the Runtime_Resolver and SHALL preserve the posted record's existing Routing_Snapshot.

### Requirement 9: Handle Errors and Security

**User Story:** As a security owner, I want safe administrative and runtime failure behavior, so that unauthorized or incomplete configuration cannot route money incorrectly.

#### Acceptance Criteria

1. IF a request has a missing, expired, malformed, or invalid JWT, THEN THE Challan_Rule_Mapping_Service SHALL return HTTP 401 and SHALL disclose no mapping data.
2. IF an authenticated request lacks the SystemAdmin role, THEN THE Challan_Rule_Mapping_Service SHALL return HTTP 403 and SHALL disclose no mapping data.
3. WHEN a server error occurs during Save, THE Challan_Rule_Creation_UI SHALL preserve the current field values and pending Drawee Bank rows and SHALL display the English server message.
4. IF no active or uniquely applicable rule exists for runtime resolution, THEN THE Runtime_Resolver SHALL fail closed with an English configuration error and SHALL not return a deposit-bank decision.
5. THE Challan_Rule_Mapping_Service SHALL derive audit actor and timestamp from trusted server context and SHALL exclude bearer tokens and unmasked credentials from responses, audit data, and logs.

### Requirement 10: Test and Integration Expectations

**User Story:** As the delivery team, I want the corrected one-set UI and resolver boundary tested, so that configuration changes cannot create incorrect or untraceable postings.

#### Acceptance Criteria

1. THE backend test suite SHALL cover one-set scope, level-5 Location validation, account resolution, full-replacement Save atomicity, active-set uniqueness, cross-set concrete-key and `OTHERS` uniqueness, normalization, version/audit, idempotency, concurrency, authorization, and fail-closed resolution.
2. THE backend property-based tests SHALL exercise pure normalization and explicit-before-OTHERS resolution with at least 100 generated cases per property.
3. THE backend integration tests SHALL use `WebApplicationFactory<Program>` for authorization, API contracts, persistence wiring, transaction rollback, and rejection of missing runtime mappings.
4. THE frontend Vitest/React Testing Library suite SHALL cover the exact screen title and fields, dependency resets, read-only Account No, grid Add/Edit/Delete, Save/Clear/Cancel, successful reload, conflict preservation, server-error preservation, and mock/real service selection.
5. THE implementation SHALL create no canonical journal record, shall not resolve after posting, and shall keep FINNOVA-65 separate from FINNOVA-16 `ChallanRule` and JVE ownership.
