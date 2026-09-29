# Requirements Document

## Introduction

The Document Number Control / Entity Number Generation (DCN) Master feature (JIRA: FINNOVA-7, Module: SystemAdmin, Master: Document Number Control) provides a centralized administrative capability for defining how document and entity numbers are generated across the Finnova platform (for example, loan account numbers, customer ids, receipt numbers). Numbering rules must be standardized and controlled in one place rather than hard-coded per module, and number issuance must be safe under concurrent use so that no two callers ever receive the same number.

A System Administrator can define a numbering scheme for a document type (a code, a display name, a format template with prefix/suffix/sequence/date tokens, a sequence configuration, a reset rule, and an optional scope). A System Administrator can modify a scheme's display attributes, search schemes by code or name, read a scheme, and deactivate/reactivate a scheme. A separate generation capability issues the next number for a given document type (and scope), atomically incrementing the scheme's sequence and formatting the result according to the template, with the sequence resetting when its configured period rolls over. Every change to a scheme's configuration is captured in an immutable audit trail. All management operations — and issuance — are restricted to authenticated callers with the SystemAdmin role.

This feature mirrors the established Lookup Master (FINNOVA-8), Nationality Master (FINNOVA-9), and Organization Hierarchy Master (FINNOVA-5) features. It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI). The backend is API-only.

This document captures the requirements derived from the JIRA ticket. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** DCN Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host, reached through the gateway. Both configuration CRUD and the number-generation operation live in this host for now (a UI-alias gateway route mirrors the Lookup/Nationality/OrgHierarchy aliases). Confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8). This feature reuses that scheme and policy; token issuance is external.
- **Assumption (Generation authorization):** Number issuance is restricted to the SystemAdmin role, consistent with every other operation in this host. If consuming modules must call issuance under their own (non-admin) identity at runtime, that is a broader authorization change flagged for confirmation in design.
- **Assumption (Audit scope):** The scheme-configuration audit trail (Requirement 6) mirrors the node-scoped/nationality-scoped audit trails of the sibling masters. Individual number issuances are high-volume and are **not** audited per number; instead the scheme's last-issued sequence value and last-issued period are retained on the sequence state (Requirement 5). Whether issuance itself must be audited is flagged for confirmation.
- **Note (Localization):** Finnova is an India-only, English-only platform. A scheme carries a single English display name; there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **DCN Master**: The centralized administrative catalog of document numbering schemes used across business modules.
- **Numbering Scheme (Scheme)**: A single record in the DCN Master defining how numbers are generated for a document type, consisting of a Scheme Code, a display Name, a Document Type, a Format Template, a Sequence configuration, a Reset Rule, an optional Scope, an Is Active flag, and audit timestamps.
- **Scheme Code**: A short machine-readable identifier for a scheme (for example, `LOAN-ACCT`), unique across the DCN Master (case-insensitive, trimmed).
- **Document Type**: The category of document/entity the scheme numbers (for example, `LoanAccount`, `Receipt`). Together with Scope it identifies which scheme issues a given number.
- **Format Template**: A string with literal text and tokens describing the produced number. Supported tokens: `{PREFIX}`, `{SUFFIX}`, `{SEQ}` / `{SEQ:n}` (zero-padded sequence of width n), `{YYYY}`, `{YY}`, `{MM}`, `{DD}` (issuance date parts). For example, `INV-{YYYY}{MM}-{SEQ:5}`.
- **Prefix / Suffix**: Optional literal strings substituted for the `{PREFIX}` / `{SUFFIX}` tokens.
- **Sequence**: The running counter for a scheme (and scope). It has a configured start value, increment step, and padding width; the Sequence State holds the current value and the period key it belongs to.
- **Reset Rule**: The period at which the sequence resets to its start value: `Never`, `Yearly`, `Monthly`, or `Daily`.
- **Scope**: The dimension a scheme (and its sequence) applies to: `Global`, `Company`, or `Branch`, with an optional Scope Id identifying the specific company/branch. The same Document Type may have distinct sequences per scope.
- **Sequence State**: The persisted current counter value and the period key it applies to, updated atomically on each issuance.
- **Issued Number**: A formatted string produced by applying the Format Template to a resolved sequence value at issuance time.
- **Is Active**: A boolean flag indicating whether a scheme may issue numbers. Defaults to true. An inactive scheme is retained but cannot issue.
- **Audit Entry**: An immutable record capturing a change to a scheme's configuration, including the affected scheme identifier, the action performed, the before/after values of changed fields, the acting administrator's identifier, and the UTC timestamp.
- **Audit Trail**: The ordered collection of Audit Entries recorded for a scheme.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim.
- **Consuming Module**: A business module that requests an issued number for a document type at runtime.
- **EARS**: Easy Approach to Requirements Syntax.
- **DCN_Service**: The backend service/API responsible for scheme management, retrieval, and number generation.
- **DCN_Admin_UI**: The React-based DCN Master administrative screen (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Numbering Scheme

**User Story:** As a System Administrator, I want to define a numbering scheme for a document type, so that numbers for that document type are generated in a standardized way.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a Scheme Code, a Name, a Document Type, a Format Template, a sequence start value, a sequence increment, a sequence padding width, and a Reset Rule, THE DCN_Service SHALL persist a new scheme and return the created scheme including a system-generated identifier, the resolved Scope, and the resolved Is Active value.
2. WHERE Is Active is not provided on a create request, THE DCN_Service SHALL default Is Active to true.
3. WHERE Scope is not provided on a create request, THE DCN_Service SHALL default Scope to `Global` with no Scope Id.
4. WHERE the sequence start value is not provided, THE DCN_Service SHALL default it to 1; WHERE the increment is not provided, THE DCN_Service SHALL default it to 1; WHERE the padding width is not provided, THE DCN_Service SHALL default it to 1 (no padding beyond the natural digits).
5. IF a create request omits Scheme Code, Name, Document Type, or Format Template, or provides any of them as empty or whitespace-only, THEN THE DCN_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
6. IF Scheme Code exceeds 30 characters, or Name exceeds 100 characters, or Document Type exceeds 50 characters, or Format Template exceeds 100 characters, THEN THE DCN_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Code 30, Name 100, Document Type 50, Template 100) are not specified in the ticket; these values are proposed for confirmation.
7. IF the sequence start value is negative, or the increment is less than 1, or the padding width is less than 1 or greater than 18, THEN THE DCN_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
8. IF the Reset Rule is not one of `Never`, `Yearly`, `Monthly`, or `Daily`, THEN THE DCN_Service SHALL reject the request with a validation error and SHALL NOT persist any record.
9. IF the Scope is not one of `Global`, `Company`, or `Branch`, THEN THE DCN_Service SHALL reject the request with a validation error and SHALL NOT persist any record.
10. IF Scope is `Company` or `Branch` and no Scope Id is provided, THEN THE DCN_Service SHALL reject the request with a validation error indicating a Scope Id is required for the selected scope, and SHALL NOT persist any record.
11. IF Scope is `Global` and a Scope Id is provided, THEN THE DCN_Service SHALL reject the request with a validation error indicating a Scope Id must not be supplied for global scope, and SHALL NOT persist any record.
12. IF the Format Template contains a token that is not one of the supported tokens (`{PREFIX}`, `{SUFFIX}`, `{SEQ}`, `{SEQ:n}`, `{YYYY}`, `{YY}`, `{MM}`, `{DD}`), or contains a malformed token, THEN THE DCN_Service SHALL reject the request with a validation error identifying the invalid token and SHALL NOT persist any record.
13. IF the Format Template does not contain a `{SEQ}` or `{SEQ:n}` token, THEN THE DCN_Service SHALL reject the request with a validation error indicating the template must include a sequence token, and SHALL NOT persist any record.
14. WHEN a scheme is created with Is Active set to true, THE DCN_Service SHALL include the scheme in the results of the scheme query (Requirement 4) without requiring a service restart.

### Requirement 2: Prevent Duplicate Scheme Code and Scope Collision

**User Story:** As a System Administrator, I want the system to prevent conflicting scheme definitions, so that each document type and scope resolves to exactly one scheme.

#### Acceptance Criteria

1. THE DCN_Service SHALL treat Scheme Code as unique across the DCN Master, comparing codes case-insensitively after trimming leading and trailing whitespace.
2. IF a create request specifies a Scheme Code that, after trimming and case-insensitive comparison, matches the code of an existing scheme, THEN THE DCN_Service SHALL reject the request with a validation error carrying the message "Scheme code must be unique", SHALL create no new record, and SHALL retain the existing record unchanged.
3. THE DCN_Service SHALL treat the combination of Document Type, Scope, and Scope Id as unique across the DCN Master, so that at most one scheme resolves numbers for a given document type and scope.
4. IF a create request specifies a Document Type, Scope, and Scope Id combination that matches an existing scheme, THEN THE DCN_Service SHALL reject the request with a validation error carrying the message "A scheme already exists for this document type and scope" and SHALL create no new record.
5. IF a create or update request specifies a Scheme Code that is empty, contains only whitespace, or exceeds 30 characters after trimming, THEN THE DCN_Service SHALL reject the request with a validation error indicating the scheme code is required and must be at most 30 characters, and SHALL make no change to the DCN Master.
6. THE DCN_Service SHALL treat Scheme Code and the Document Type/Scope combination as immutable after creation; an update request SHALL NOT change these fields (see Requirement 3).

### Requirement 3: Modify Numbering Scheme

**User Story:** As a System Administrator, I want to update a scheme's editable attributes, so that numbering configuration stays accurate without disrupting already-issued numbers.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing scheme's Name, Format Template, sequence increment, sequence padding width, Reset Rule, or Is Active flag, and the submitted values pass validation, THE DCN_Service SHALL persist the changes, refresh the record's UpdatedAt timestamp, and return the updated scheme.
2. THE DCN_Service SHALL NOT allow an update to change the Scheme Code, the Document Type, the Scope, or the Scope Id (these are immutable after creation).
3. THE DCN_Service SHALL NOT allow an update to change the sequence's current value or period key; the current sequence state is managed only by issuance (Requirement 5) and reset (Requirement 5).
   - **Assumption:** Editing the live counter is disallowed to protect issued-number integrity. If an administrative "reseed sequence" capability is required, that is a separate requirement flagged for confirmation.
4. IF a System Administrator submits an update where Name is empty or whitespace, or exceeds 100 characters, or where the Format Template is empty, malformed, missing a sequence token, or exceeds 100 characters, or where the increment is less than 1, or the padding width is less than 1 or greater than 18, or the Reset Rule is not a supported value, THEN THE DCN_Service SHALL reject the request with a validation error identifying the offending field and SHALL preserve the existing record unchanged.
5. IF a System Administrator attempts to update a scheme whose identifier does not exist, THEN THE DCN_Service SHALL return a not-found error and SHALL make no change to the DCN Master.
6. WHEN a System Administrator submits an update whose editable values all equal the record's current values, THE DCN_Service SHALL treat the update as a successful no-op, return the existing record unchanged, refresh no timestamp, and record no audit entry.

### Requirement 4: Query, Search, and Read Numbering Schemes

**User Story:** As a System Administrator, I want to search and read schemes by code or name, so that I can locate and inspect the correct configuration.

#### Acceptance Criteria

1. WHEN a System Administrator requests schemes with a non-empty search term, THE DCN_Service SHALL return only the schemes whose Scheme Code, Name, or Document Type contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHEN a System Administrator requests schemes with a search term consisting only of whitespace or an empty string, THE DCN_Service SHALL treat the request as having no search term and return the schemes subject to the applied pagination parameters.
3. WHEN a System Administrator requests schemes, THE DCN_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, applied page size, and total page count.
4. WHEN a System Administrator requests schemes without specifying a page number, THE DCN_Service SHALL apply a default page number of 1; WHEN no page size is specified, THE DCN_Service SHALL apply a default page size of 20.
5. IF a System Administrator requests a page number less than 1, or a page size less than 1 or greater than 100, THEN THE DCN_Service SHALL reject the request with a validation error indicating the parameter is out of range, without returning any records.
6. WHEN a System Administrator requests a page number beyond the last available page, THE DCN_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
7. WHEN returning schemes, THE DCN_Service SHALL order the records by Name ascending within the applied filter, and for records sharing the same Name SHALL apply a deterministic secondary ordering by Scheme Code ascending.
8. IF a search request matches no schemes, THEN THE DCN_Service SHALL return an empty item collection with a total count of 0 and a success status.
9. WHEN a System Administrator requests a single scheme by its identifier, THE DCN_Service SHALL return the scheme if it exists, including its current sequence state (current value and period key); IF the identifier does not exist, THEN THE DCN_Service SHALL return a not-found error.

### Requirement 5: Generate Next Number

**User Story:** As a consuming module (via a System Administrator-authorized caller), I want to obtain the next number for a document type and scope, so that documents and entities receive unique, correctly formatted identifiers.

#### Acceptance Criteria

1. WHEN a caller requests the next number for a specified Document Type and Scope (with Scope Id where applicable), THE DCN_Service SHALL resolve the matching active scheme, atomically advance its sequence, and return the formatted Issued Number together with the raw sequence value used.
2. THE DCN_Service SHALL ensure number issuance is atomic and serialized per scheme so that under concurrent issuance requests for the same scheme, no two callers receive the same sequence value and no sequence value in the issued range is skipped due to a race.
3. WHEN issuing a number, THE DCN_Service SHALL compute the current period key from the Reset Rule and the issuance date in UTC (`Never` -> a constant key; `Yearly` -> year; `Monthly` -> year+month; `Daily` -> year+month+day), and IF the scheme's stored period key differs from the current period key, THEN THE DCN_Service SHALL reset the sequence to its start value for the new period before advancing.
4. WHEN issuing a number, THE DCN_Service SHALL advance the sequence by the configured increment from either the start value (on first issuance or after a reset) or the stored current value (within the same period), and SHALL persist the new current value and period key as part of the same atomic operation that returns the number.
5. WHEN formatting the Issued Number, THE DCN_Service SHALL substitute `{PREFIX}` and `{SUFFIX}` with the configured literals (empty when not configured), substitute `{SEQ:n}` with the sequence value left-padded with zeros to width n (and `{SEQ}` with the configured padding width), and substitute `{YYYY}`, `{YY}`, `{MM}`, `{DD}` with the corresponding UTC issuance-date parts.
6. IF no scheme matches the requested Document Type and Scope, THEN THE DCN_Service SHALL return a not-found error and SHALL issue no number.
7. IF the matching scheme is inactive, THEN THE DCN_Service SHALL reject the issuance request with an error indicating the scheme is inactive and SHALL issue no number and SHALL NOT advance the sequence.
8. IF an issuance request omits the Document Type, or specifies a Scope of `Company` or `Branch` without a Scope Id, THEN THE DCN_Service SHALL reject the request with a validation error and SHALL issue no number.
9. IF advancing the sequence would exceed the representable range for the configured padding width (numeric overflow of the counter), THEN THE DCN_Service SHALL reject the issuance request with an error indicating the sequence is exhausted and SHALL NOT return a number.
   - **Assumption:** Exhaustion is treated as an error rather than silently widening the number. Confirm whether overflow should instead widen the digits.

### Requirement 6: Record Audit Trail on Configuration Change

**User Story:** As a System Administrator, I want every scheme configuration change captured in an audit trail, so that changes to numbering rules are traceable.

#### Acceptance Criteria

1. WHEN the DCN_Service persists a create of a scheme (Requirement 1), THE DCN_Service SHALL record exactly one Audit Entry containing the created scheme identifier, the create action indicator, the resulting configuration values, the acting administrator's identifier, and the UTC timestamp.
2. WHEN the DCN_Service persists an update to a scheme's editable configuration (Requirement 3), THE DCN_Service SHALL record exactly one Audit Entry containing the affected scheme identifier, the update action indicator, the before and after values of the changed fields, the acting administrator's identifier, and the UTC timestamp.
3. WHEN the DCN_Service deactivates or reactivates a scheme (a change to Is Active), THE DCN_Service SHALL record the change as an update Audit Entry capturing the prior and new Is Active values.
4. THE DCN_Service SHALL NOT record an Audit Entry for a number issuance (Requirement 5); issuance is tracked only via the scheme's sequence state.
5. IF a create or update request is rejected by validation or by a uniqueness rule (Requirement 2), THEN THE DCN_Service SHALL NOT record an Audit Entry for that rejected request and SHALL leave the count of persisted Audit Entries unchanged.
6. WHEN a System Administrator requests the audit entries for a specified scheme, THE DCN_Service SHALL return the Audit Entries for that scheme ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
7. IF a System Administrator requests the audit entries for a scheme identifier that does not exist, THEN THE DCN_Service SHALL return an empty result set and SHALL NOT return an error.
8. THE DCN_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that no operation exposed by the service updates or deletes a previously recorded Audit Entry.

### Requirement 7: Deactivate and Reactivate Numbering Scheme

**User Story:** As a System Administrator, I want to deactivate a scheme without deleting it, so that a numbering rule can be retired while preserving its history and issued-number integrity.

#### Acceptance Criteria

1. WHEN a System Administrator deactivates an existing active scheme, THE DCN_Service SHALL set Is Active to false, refresh the UpdatedAt timestamp, record an update Audit Entry (Requirement 6.3), and return the updated scheme.
2. WHEN a System Administrator reactivates an existing inactive scheme, THE DCN_Service SHALL set Is Active to true, refresh the UpdatedAt timestamp, record an update Audit Entry, and return the updated scheme.
3. IF a System Administrator attempts to deactivate or reactivate a scheme whose identifier does not exist, THEN THE DCN_Service SHALL return a not-found error and SHALL make no change to the DCN Master.
4. WHILE a scheme is inactive, THE DCN_Service SHALL exclude it from number issuance (Requirement 5.7) while continuing to return it in scheme queries and single-scheme reads (Requirement 4).
5. **Assumption (delete):** The ticket does not specify hard deletion of schemes. Deactivation (soft retire) is the proposed default to protect issued-number integrity; hard delete is intentionally not offered. Confirm if hard delete is required.

### Requirement 8: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want DCN management, query, and issuance operations restricted to authorized callers, so that only authorized personnel can view, change, or trigger number generation.

#### Acceptance Criteria

1. IF a request to create, update, query, read, issue a number for, or read the audit trail of a scheme is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE DCN_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored scheme or advance any sequence, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts any DCN operation, THEN THE DCN_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored scheme or advance any sequence, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, read, issuance, or audit-trail-read operation, THE DCN_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 7.
4. WHEN the DCN_Service evaluates authentication and authorization for any request, THE DCN_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the DCN_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy established by FINNOVA-8.
