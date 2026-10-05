# Requirements Document

## Introduction

The Drawee Bank & Challan Rules Master Management feature (JIRA: FINNOVA-16, Module: SystemAdmin) provides a centralized administrative capability for maintaining drawee bank reference data and the challan rules associated with those banks. Drawee bank data is used across the Finnova platform (for example, in instrument clearing and payment routing flows) and must be standardized in one place rather than duplicated per module.

A System Administrator can create a Drawee Bank (a short Bank Code plus a single English Bank Name), maintain the list of Drawee Branches (also called Drawee Places) under a bank, maintain the bank's Restriction Details (clearing days and an effective date range), and configure Challan Rules (format patterns, validation expressions, and routing targets) associated with drawee banks. A System Administrator can also modify existing records and search drawee banks by Bank Code or Bank Name. The Bank Code uniquely identifies a drawee bank; attempting to create a second record with an existing code is rejected. Every create and modify operation is captured in an audit trail so that changes to standardized reference data are traceable. All management operations are restricted to System Administrators; non-admin users are denied access.

This master-data feature mirrors the established Lookup Master Management feature (FINNOVA-8) and the Nationality Master Management feature (FINNOVA-9). It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy at the gateway prefix `/api/systemadmin/**`. The user-facing screen (the "Drawee Bank Master" screen, with the Bank header, a "List of Branches" grid, and a "Restriction Details" panel, and Save/Clear/Cancel actions) is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI); the UI tasks accompany this spec and follow the repository's established service-layer, mock/real toggle, and MUI DataGrid patterns.

This document captures the requirements derived from the JIRA ticket, its acceptance criteria, and the attached "Drawee Bank Master" mockup. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** Drawee Bank & Challan Rules Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (which already exposes the Lookup and Nationality Masters), reached through the gateway prefix `/api/systemadmin/**`. This mirrors the Lookup and Nationality Master decisions and is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8). This feature reuses that scheme and policy. Token issuance (login) ownership is external to this feature.
- **Assumption (Aggregate boundary):** A Drawee Bank is the aggregate root; Drawee Branches and Restriction Details are owned by (and persisted with) their parent Drawee Bank. Challan Rules are modeled as a related configuration set associated with a Drawee Bank (by the bank identifier) rather than as free-standing records, because the ticket frames them as rules "as required" for drawee banks. Whether Challan Rules should instead be a global, bank-independent catalog is flagged for confirmation in design.
- **Assumption (Challan Rules shape):** The attached mockup shows only the Bank header, the Branches grid, and the Restriction Details panel; it does not show challan-rule fields. The challan rule fields (Rule Code, Format Pattern, Validation Expression, Routing Target, Is Active) are proposed from the ticket description ("formats, validations, routing") for confirmation.
- **Assumption (Audit trail scope):** No general-purpose audit-trail infrastructure exists in the backend today (entities carry only `CreatedAt`/`UpdatedAt` timestamps). This feature introduces a drawee-bank-scoped audit trail (Requirement 7). Whether the audit trail should be promoted to a shared, platform-wide capability is flagged for confirmation in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each drawee bank carries a single English `Bank Name` and each branch a single English `Place Name`; there are no bilingual fields, no Arabic content, and no right-to-left rendering. Sample values in this document are India-appropriate (for example, `HDFC` / `HDFC Bank Ltd`, branch places such as `Mumbai` and `Pune`, and six-digit PIN codes); any region-specific sample data from the source mockup is not carried into this spec.

## Glossary

- **Drawee Bank Master**: The centralized administrative catalog of drawee bank reference records (banks, their branches, restriction details, and challan rules) used across business modules.
- **Drawee Bank (Bank)**: The aggregate record consisting of a Bank Code, a single English Bank Name, an Is Active flag, a collection of Drawee Branches, a Restriction Detail, and audit timestamps.
- **Bank Code**: A short machine-readable identifier for a drawee bank (for example, `HDFC`), unique across the Drawee Bank Master.
- **Bank Name**: The single English display name of a drawee bank (for example, `HDFC Bank Ltd`).
- **Drawee Branch (Drawee Place)**: A branch/place under a Drawee Bank, consisting of a Place Code, a single English Place Name, an Address, a Postal Code (PIN code), and an effective date range (Start Date, End Date).
- **Place Code**: A short identifier for a Drawee Branch, unique within the owning Drawee Bank.
- **Place Name**: The single English display name of a Drawee Branch (for example, `Mumbai`).
- **Postal Code (PIN Code)**: The six-digit Indian postal code recorded on a branch Address.
- **Restriction Detail**: A per-bank record consisting of Clearing Days and an effective date range (Start Date, End Date).
- **Clearing Days**: A non-negative integer count of days used by the Restriction Detail.
- **Effective Date Range**: A pair of dates (Start Date, End Date) in which End Date is on or after Start Date.
- **Challan Rule**: A configuration record associated with a Drawee Bank, consisting of a Rule Code, a Format Pattern, a Validation Expression, a Routing Target, and an Is Active flag.
- **Rule Code**: A short identifier for a Challan Rule, unique within the owning Drawee Bank.
- **Is Active**: A boolean flag indicating whether a Drawee Bank (or Challan Rule) is available for consumption. Defaults to true.
- **Audit Entry**: An immutable record capturing a create or modify action, including the affected record identifier, the action performed, the changed values' before and after states, the acting System Administrator's identifier, and the timestamp of the change.
- **Audit Trail**: The ordered collection of Audit Entries recorded for the Drawee Bank Master.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim, authorized to manage drawee bank records.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **DraweeBank_Service**: The backend service/API responsible for drawee bank, branch, restriction, and challan rule management and retrieval.
- **DraweeBank_Admin_UI**: The React-based "Drawee Bank Master" administrative screen used by System Administrators (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Drawee Bank

**User Story:** As a System Administrator, I want to create a new drawee bank with its code, name, branches, and restriction details, so that the bank becomes available for consuming module clearing and routing flows.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a Bank Code, a Bank Name, zero or more Drawee Branches, and an optional Restriction Detail, THE DraweeBank_Service SHALL persist the new drawee bank record, its submitted Drawee Branches, and its submitted Restriction Detail as a single atomic (all-or-nothing) operation, and SHALL return the created record including a system-generated identifier for the drawee bank, a system-generated identifier for each persisted Drawee Branch, and the resolved Is Active value.
2. WHERE Is Active is not provided on a create request, THE DraweeBank_Service SHALL default Is Active to true.
3. IF a create request omits Bank Code or omits Bank Name, or provides either as an empty or whitespace-only value, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
4. IF Bank Code exceeds 20 characters, or Bank Name exceeds 150 characters, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Bank Code 20, Bank Name 150) are not specified in the ticket; these values are proposed for confirmation.
5. IF a create request provides a Bank Code that matches an existing drawee bank record's Bank Code using case-insensitive comparison after trimming, THEN THE DraweeBank_Service SHALL reject the request with a validation error indicating the code already exists and SHALL NOT persist any record.
6. WHEN a drawee bank record is created with Is Active set to true, THE DraweeBank_Service SHALL include the record in the results of the drawee bank query (Requirement 8) without requiring a service restart.
7. IF a create request fails to persist any part of the drawee bank aggregate (the bank record, a Drawee Branch, or the Restriction Detail), THEN THE DraweeBank_Service SHALL persist no part of the aggregate, SHALL return an error, and SHALL leave the Drawee Bank Master unchanged.
8. WHEN a create request includes Drawee Branches or a Restriction Detail, THE DraweeBank_Service SHALL validate each submitted Drawee Branch per Requirement 3 and the submitted Restriction Detail per Requirement 4, and IF any such branch or restriction validation fails, THEN THE DraweeBank_Service SHALL reject the entire create request and SHALL NOT persist any record.

### Requirement 2: Prevent Duplicate Bank Code

**User Story:** As a System Administrator, I want the system to prevent duplicate bank codes, so that each drawee bank is uniquely identified.

#### Acceptance Criteria

1. THE DraweeBank_Service SHALL treat Bank Code as unique across the Drawee Bank Master, comparing codes case-insensitively (for example, `HDFC` and `hdfc` are treated as the same code) after trimming leading and trailing whitespace.
2. IF a create request specifies a Bank Code that, after trimming and case-insensitive comparison, matches the code of an existing drawee bank record, THEN THE DraweeBank_Service SHALL reject the request with a validation error carrying the message "Bank code must be unique", SHALL create no new record, and SHALL retain the existing record unchanged.
3. IF an update request would change a drawee bank's Bank Code to a value that, after trimming and case-insensitive comparison, matches the code of a different existing drawee bank record, THEN THE DraweeBank_Service SHALL reject the request with a validation error carrying the message "Bank code must be unique" and SHALL preserve both records unchanged.
4. WHEN an update request submits the same Bank Code as the record being updated (differing only by letter casing or surrounding whitespace), THE DraweeBank_Service SHALL treat the code as unchanged for the uniqueness check and SHALL NOT reject the request on the grounds of duplication.
5. IF a create or update request specifies a Bank Code that is empty, contains only whitespace, or exceeds 20 characters after trimming, THEN THE DraweeBank_Service SHALL reject the request with a validation error indicating the bank code is required and must be at most 20 characters, and SHALL make no change to the Drawee Bank Master.

### Requirement 3: Manage Drawee Branches (Drawee Places)

**User Story:** As a System Administrator, I want to add and modify the branches (drawee places) of a drawee bank, so that the clearing places under each bank are maintained accurately.

#### Acceptance Criteria

1. WHEN a System Administrator submits a Drawee Branch under a drawee bank with a Place Code, a Place Name, an Address, a Postal Code, and an Effective Date Range, THE DraweeBank_Service SHALL persist the branch as owned by that drawee bank and SHALL return the branch including a system-generated identifier.
2. IF a submitted Drawee Branch omits Place Code or Place Name, or provides either as an empty or whitespace-only value, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist the branch or its parent create/update.
3. IF two or more Drawee Branches within the same drawee bank share a Place Code after trimming and case-insensitive comparison, THEN THE DraweeBank_Service SHALL reject the request with a validation error carrying the message "Place code must be unique within the bank" and SHALL make no change to the Drawee Bank Master.
4. IF a Drawee Branch provides an Effective Date Range whose End Date is earlier than its Start Date, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying the branch date range and SHALL make no change to the Drawee Bank Master.
5. WHERE a Drawee Branch omits End Date, THE DraweeBank_Service SHALL treat the branch as having an open-ended effective period and SHALL persist the branch with no End Date.
   - **Assumption:** An open-ended (null) End Date is permitted to represent an indefinitely active branch; confirm whether End Date is mandatory.
6. IF a Drawee Branch provides a Postal Code that is not a six-digit numeric value, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying the Postal Code and SHALL make no change to the Drawee Bank Master.
   - **Assumption:** Indian PIN codes are six digits; the six-digit numeric rule is proposed for confirmation.
7. WHEN a System Administrator removes a Drawee Branch from a drawee bank during an update, THE DraweeBank_Service SHALL delete that branch from the owning drawee bank while preserving the remaining branches.

### Requirement 4: Maintain Restriction Details

**User Story:** As a System Administrator, I want to maintain a drawee bank's restriction details, so that clearing-day and effective-period constraints are recorded.

#### Acceptance Criteria

1. WHEN a System Administrator submits a Restriction Detail for a drawee bank with Clearing Days and an Effective Date Range, THE DraweeBank_Service SHALL persist the Restriction Detail as owned by that drawee bank and SHALL return the Restriction Detail with the submitted values.
2. IF a submitted Restriction Detail provides Clearing Days that is not an integer greater than or equal to 0, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying Clearing Days and SHALL make no change to the Drawee Bank Master.
3. IF a submitted Restriction Detail provides an Effective Date Range whose End Date is earlier than its Start Date, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying the restriction date range and SHALL make no change to the Drawee Bank Master.
4. WHERE a create or update request omits the Restriction Detail, THE DraweeBank_Service SHALL persist the drawee bank with no Restriction Detail and SHALL treat the bank as having no clearing-day restriction.
   - **Assumption:** The Restriction Detail is optional (a bank may have none); confirm whether every bank must carry a Restriction Detail.
5. IF a submitted Restriction Detail provides Clearing Days greater than 365, THEN THE DraweeBank_Service SHALL reject the request with a validation error indicating Clearing Days is out of range and SHALL make no change to the Drawee Bank Master.
   - **Assumption:** An upper bound of 365 clearing days is proposed for confirmation.

### Requirement 5: Modify Drawee Bank

**User Story:** As a System Administrator, I want to update an existing drawee bank's name, branches, and restriction details, so that reference data stays accurate.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing drawee bank's Bank Name, Drawee Branches, or Restriction Detail and the submitted values pass validation, THE DraweeBank_Service SHALL persist the changes, refresh the record's UpdatedAt timestamp, and return the updated record reflecting the submitted values.
2. IF a System Administrator submits an update where Bank Name is empty or contains only whitespace, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying Bank Name as required and SHALL preserve the existing record unchanged.
3. IF a System Administrator submits an update where Bank Name exceeds 150 characters, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying Bank Name and SHALL preserve the existing record unchanged.
4. IF a System Administrator attempts to update a drawee bank whose identifier does not exist, THEN THE DraweeBank_Service SHALL return a not-found error and SHALL make no change to the Drawee Bank Master.
5. WHEN a System Administrator submits an update whose Bank Name, Drawee Branches, and Restriction Detail all equal the record's current values, THE DraweeBank_Service SHALL treat the update as a successful no-op and return the existing record.

### Requirement 6: Configure Challan Rules

**User Story:** As a System Administrator, I want to configure challan rules (formats, validations, routing) associated with a drawee bank, so that challan processing is governed by standardized rules.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request for a Challan Rule under a drawee bank containing a Rule Code, a Format Pattern, a Validation Expression, and a Routing Target, THE DraweeBank_Service SHALL persist a new Challan Rule owned by that drawee bank and SHALL return the created rule including a system-generated identifier and the resolved Is Active value.
2. WHERE Is Active is not provided on a Challan Rule create request, THE DraweeBank_Service SHALL default Is Active to true.
3. IF a Challan Rule create or update request omits Rule Code, Format Pattern, Validation Expression, or Routing Target, provides any of these as an empty or whitespace-only value, or provides a Rule Code exceeding 50 characters, a Format Pattern exceeding 500 characters, a Validation Expression exceeding 500 characters, or a Routing Target exceeding 200 characters, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying each missing, empty, or over-length field and SHALL NOT persist any Challan Rule.
4. IF a Challan Rule create or update request provides a Rule Code that, after trimming and case-insensitive comparison, matches the Rule Code of a different existing Challan Rule under the same drawee bank, THEN THE DraweeBank_Service SHALL reject the request with a validation error carrying the message "Rule code must be unique within the bank" and SHALL preserve the existing rules unchanged.
5. WHEN a System Administrator submits an update to an existing Challan Rule's Format Pattern, Validation Expression, Routing Target, or Is Active flag and the submitted values pass validation, THE DraweeBank_Service SHALL persist the changes and return the updated rule.
6. IF a System Administrator attempts to update a Challan Rule whose identifier does not exist, THEN THE DraweeBank_Service SHALL return a not-found error and SHALL make no change to the Drawee Bank Master.
7. IF a Challan Rule create or update request references a drawee bank identifier that does not exist, THEN THE DraweeBank_Service SHALL return a not-found error for the owning drawee bank and SHALL NOT persist any Challan Rule.
8. IF a Challan Rule create or update request provides a Format Pattern or a Validation Expression that cannot be parsed as a valid expression, THEN THE DraweeBank_Service SHALL reject the request with a validation error identifying the unparseable field and SHALL NOT persist any Challan Rule.
   - **Assumption:** The ticket does not define the Format Pattern or Validation Expression grammar; validating that each is a parseable expression (and the grammar used) is proposed for confirmation.

### Requirement 7: Record Audit Trail on Create and Modify

**User Story:** As a System Administrator, I want every drawee bank and challan rule change captured in an audit trail, so that modifications to standardized reference data are traceable.

#### Acceptance Criteria

1. WHEN the DraweeBank_Service persists a change to a drawee bank, a Drawee Branch, a Restriction Detail, or a Challan Rule (per Requirements 3, 4, 5, and 6), THE DraweeBank_Service SHALL record exactly one Audit Entry containing the affected record identifier, the update action indicator, the prior values, the new values, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
2. WHEN the DraweeBank_Service creates a drawee bank or a Challan Rule (per Requirements 1 and 6), THE DraweeBank_Service SHALL record exactly one Audit Entry containing the created record identifier, the create action indicator, the resulting values, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
   - **Assumption:** The ticket calls for an audit trail on create and modify; recording one entry per create and one per modify is proposed for a complete trail. Confirm the expected granularity (per aggregate vs. per child entity).
3. IF a create or update request is rejected by validation or by a uniqueness rule (Requirements 2, 3, or 6), THEN THE DraweeBank_Service SHALL NOT record an Audit Entry for that rejected request, and SHALL leave the count of persisted Audit Entries unchanged.
4. WHEN a System Administrator requests the audit entries for a specified drawee bank, THE DraweeBank_Service SHALL return the Audit Entries for that drawee bank ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
5. IF a System Administrator requests the audit entries for a drawee bank identifier that does not exist, THEN THE DraweeBank_Service SHALL return an empty result set and SHALL NOT return an error.
6. THE DraweeBank_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that any request to update or delete a previously recorded Audit Entry is rejected with an error indicating that audit entries are immutable.

### Requirement 8: Query and Search Drawee Banks

**User Story:** As a System Administrator, I want to search drawee banks by code or name, so that I can locate the correct record.

#### Acceptance Criteria

1. WHEN a System Administrator requests drawee bank records with a non-empty search term, THE DraweeBank_Service SHALL return only the records whose Bank Code or Bank Name contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHEN a System Administrator requests drawee bank records with a search term consisting only of whitespace or an empty string, THE DraweeBank_Service SHALL treat the request as having no search term and return the drawee bank records subject to the applied pagination parameters.
3. WHEN a System Administrator requests drawee bank records without a search term, THE DraweeBank_Service SHALL return the drawee bank records subject to the applied pagination parameters.
4. WHEN a System Administrator requests drawee bank records, THE DraweeBank_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
5. WHEN a System Administrator requests drawee bank records without specifying page number, THE DraweeBank_Service SHALL apply a default page number of 1.
6. WHEN a System Administrator requests drawee bank records without specifying page size, THE DraweeBank_Service SHALL apply a default page size of 20.
7. IF a System Administrator requests a page number less than 1, THEN THE DraweeBank_Service SHALL reject the request and return a validation error indicating the page number is out of range, without returning any records.
8. IF a System Administrator requests a page size less than 1 or greater than 100, THEN THE DraweeBank_Service SHALL reject the request and return a validation error indicating the page size is out of range, without returning any records.
9. WHEN a System Administrator requests a page number beyond the last available page, THE DraweeBank_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
10. WHEN returning drawee bank records, THE DraweeBank_Service SHALL order the records by Bank Name ascending within the applied filter, and for records sharing the same Bank Name SHALL apply a deterministic secondary ordering by Bank Code ascending.
11. IF a search request matches no drawee bank records, THEN THE DraweeBank_Service SHALL return an empty item collection with a total count of 0 and a success status.

### Requirement 9: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want drawee bank management and query operations restricted to System Administrators, so that only authorized personnel can view or change standardized reference data.

#### Acceptance Criteria

1. IF a request to create, update, query, or read the audit trail of a drawee bank or challan rule is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE DraweeBank_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored record, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts to create, update, query, or read the audit trail of a drawee bank or challan rule, THEN THE DraweeBank_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored record, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, or audit-trail-read operation, THE DraweeBank_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 8.
4. WHEN the DraweeBank_Service evaluates authentication and authorization for any create, update, query, or audit-trail-read request, THE DraweeBank_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the DraweeBank_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) established by FINNOVA-8. Establishing that scheme is a prerequisite already satisfied by the Lookup Master feature.
   - **Assumption:** Read access (query and audit-trail read) is restricted to System Administrators, mirroring the "enforce permissions" scenario. If broader read access is desired for consuming-module flows, that is flagged for confirmation in design.
