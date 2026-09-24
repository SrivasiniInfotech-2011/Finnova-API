# Requirements Document

## Introduction

The Nationality Master Management feature (JIRA: FINNOVA-9, Module: SystemAdmin, Master: Nationality) provides a centralized administrative capability for maintaining nationality reference data used across the Finnova platform (for example, populating nationality dropdowns in customer origination and KYC flows). Nationality reference data must be standardized in one place rather than duplicated per module.

A System Administrator can create a nationality record (code and name), modify an existing nationality's name, and search nationalities by code or name. Each nationality carries a short code and a single English display name. The code uniquely identifies a nationality; attempting to create a second record with an existing code is rejected. Every modification is captured in an audit trail so that changes to standardized reference data are traceable. All management operations are restricted to System Administrators; non-admin users are denied access.

This master-data feature mirrors the established Lookup Master Management feature (FINNOVA-8). It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI); this backend spec is API-only.

This document captures the requirements derived from the JIRA Gherkin scenarios and the ticket. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** Nationality Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (which already exposes the Lookup Master), reached through the gateway prefix `/api/systemadmin/**`. This mirrors the Lookup Master decision and is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8). This feature reuses that scheme and policy. Token issuance (login) ownership is external to this feature.
- **Assumption (Audit trail scope):** No general-purpose audit-trail infrastructure exists in the backend today (entities carry only `CreatedAt`/`UpdatedAt` timestamps). This feature introduces a nationality-scoped audit trail (Requirement 4). Whether the audit trail should be promoted to a shared, platform-wide capability is flagged for confirmation in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each nationality carries a single English display name (`Name`); there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **Nationality Master**: The centralized administrative catalog of nationality reference records used across business modules.
- **Nationality Record (Nationality)**: A single record in the Nationality Master, consisting of a Nationality Code, a single English display Name, an Is Active flag, and audit timestamps.
- **Nationality Code**: A short machine-readable identifier for a nationality (for example, `IN`), unique across the Nationality Master.
- **Name**: The single English display name of a nationality (for example, `Indian`).
- **Is Active**: A boolean flag indicating whether a nationality is available for consumption by module dropdowns. Defaults to true.
- **Audit Entry**: An immutable record capturing a change to a nationality, including the affected nationality identifier, the action performed, the changed field's before and after values, the acting System Administrator's identifier, and the timestamp of the change.
- **Audit Trail**: The ordered collection of Audit Entries recorded for the Nationality Master.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim, authorized to manage nationality records.
- **Consuming Module**: A business module (for example, customer origination) that reads active nationality records to populate dropdowns.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **Nationality_Service**: The backend service/API responsible for nationality record management and retrieval.
- **Nationality_Admin_UI**: The React-based Nationality Master administrative screen used by System Administrators (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Nationality Record

**User Story:** As a System Administrator, I want to create a new nationality with its code and name, so that the nationality becomes available for consuming module dropdowns.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a Nationality Code and a Name, THE Nationality_Service SHALL persist a new nationality record and return the created record including a system-generated identifier and the resolved Is Active value.
2. WHERE Is Active is not provided on a create request, THE Nationality_Service SHALL default Is Active to true.
3. IF a create request omits Nationality Code or omits Name, or provides either as an empty or whitespace-only value, THEN THE Nationality_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
4. IF Nationality Code exceeds 10 characters, or Name exceeds 100 characters, THEN THE Nationality_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Code 10, Name 100) are not specified in the ticket; these values are proposed for confirmation.
5. IF a create request provides a Nationality Code that matches an existing nationality record's Nationality Code using case-insensitive comparison, THEN THE Nationality_Service SHALL reject the request with a validation error indicating the code already exists and SHALL NOT persist any record.
6. WHEN a nationality record is created with Is Active set to true, THE Nationality_Service SHALL include the record in the results of the nationality query (Requirement 5) without requiring a service restart.

### Requirement 2: Prevent Duplicate Nationality Code

**User Story:** As a System Administrator, I want the system to prevent duplicate nationality codes, so that each nationality is uniquely identified.

#### Acceptance Criteria

1. THE Nationality_Service SHALL treat Nationality Code as unique across the Nationality Master, comparing codes case-insensitively (for example, `IN` and `in` are treated as the same code) after trimming leading and trailing whitespace.
2. IF a create request specifies a Nationality Code that, after trimming and case-insensitive comparison, matches the code of an existing nationality record, THEN THE Nationality_Service SHALL reject the request with a validation error carrying the message "Nationality code must be unique", SHALL create no new record, and SHALL retain the existing record unchanged.
3. IF an update request would change a nationality's Nationality Code to a value that, after trimming and case-insensitive comparison, matches the code of a different existing nationality record, THEN THE Nationality_Service SHALL reject the request with a validation error carrying the message "Nationality code must be unique" and SHALL preserve both records unchanged.
4. WHEN an update request submits the same Nationality Code as the record being updated (differing only by letter casing or surrounding whitespace), THE Nationality_Service SHALL treat the code as unchanged for the uniqueness check and SHALL NOT reject the request on the grounds of duplication.
5. IF a create or update request specifies a Nationality Code that is empty, contains only whitespace, or exceeds 10 characters after trimming, THEN THE Nationality_Service SHALL reject the request with a validation error indicating the nationality code is required and must be at most 10 characters, and SHALL make no change to the Nationality Master.

### Requirement 3: Modify Nationality Record

**User Story:** As a System Administrator, I want to update an existing nationality's name, so that reference data stays accurate.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing nationality's Name and the submitted value passes validation, THE Nationality_Service SHALL persist the change, refresh the record's UpdatedAt timestamp, and return the updated record reflecting the submitted Name.
2. IF a System Administrator submits an update where Name is empty or contains only whitespace, THEN THE Nationality_Service SHALL reject the request with a validation error identifying Name as required and SHALL preserve the existing record unchanged.
3. IF a System Administrator submits an update where Name exceeds 100 characters, THEN THE Nationality_Service SHALL reject the request with a validation error identifying Name and SHALL preserve the existing record unchanged.
4. IF a System Administrator attempts to update a nationality whose identifier does not exist, THEN THE Nationality_Service SHALL return a not-found error and SHALL make no change to the Nationality Master.
5. WHEN a System Administrator submits an update whose Name equals the record's current Name, THE Nationality_Service SHALL treat the update as a successful no-op and return the existing record.

### Requirement 4: Record Audit Trail on Modification

**User Story:** As a System Administrator, I want every nationality change captured in an audit trail, so that modifications to standardized reference data are traceable.

#### Acceptance Criteria

1. WHEN the Nationality_Service persists a change to a nationality's Name (per Requirement 3), THE Nationality_Service SHALL record exactly one Audit Entry containing the affected nationality identifier, the update action indicator, the prior Name value, the new Name value, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
2. WHEN the Nationality_Service creates a nationality (per Requirement 1), THE Nationality_Service SHALL record exactly one Audit Entry containing the created nationality identifier, the create action indicator, the resulting field values, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC).
   - **Assumption:** The ticket calls out an audit entry on modification; recording a create-time entry as well is proposed for a complete trail. Confirm whether create should be audited.
3. IF a create or update request is rejected by validation or by the duplicate-code rule (Requirement 2), THEN THE Nationality_Service SHALL NOT record an Audit Entry for that rejected request, and SHALL leave the count of persisted Audit Entries unchanged.
4. WHEN a System Administrator requests the audit entries for a specified nationality, THE Nationality_Service SHALL return the Audit Entries for that nationality ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
5. IF a System Administrator requests the audit entries for a nationality identifier that does not exist, THEN THE Nationality_Service SHALL return an empty result set and SHALL NOT return an error.
6. THE Nationality_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that any request to update or delete a previously recorded Audit Entry is rejected with an error indicating that audit entries are immutable.

### Requirement 5: Query and Search Nationality Records

**User Story:** As a System Administrator, I want to search nationalities by code or name, so that I can locate the correct record.

#### Acceptance Criteria

1. WHEN a System Administrator requests nationality records with a non-empty search term, THE Nationality_Service SHALL return only the records whose Nationality Code or Name contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHEN a System Administrator requests nationality records with a search term consisting only of whitespace or an empty string, THE Nationality_Service SHALL treat the request as having no search term and return the nationality records subject to the applied pagination parameters.
3. WHEN a System Administrator requests nationality records without a search term, THE Nationality_Service SHALL return the nationality records subject to the applied pagination parameters.
4. WHEN a System Administrator requests nationality records, THE Nationality_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
5. WHEN a System Administrator requests nationality records without specifying page number, THE Nationality_Service SHALL apply a default page number of 1.
6. WHEN a System Administrator requests nationality records without specifying page size, THE Nationality_Service SHALL apply a default page size of 20.
7. IF a System Administrator requests a page number less than 1, THEN THE Nationality_Service SHALL reject the request and return a validation error indicating the page number is out of range, without returning any records.
8. IF a System Administrator requests a page size less than 1 or greater than 100, THEN THE Nationality_Service SHALL reject the request and return a validation error indicating the page size is out of range, without returning any records.
9. WHEN a System Administrator requests a page number beyond the last available page, THE Nationality_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
10. WHEN returning nationality records, THE Nationality_Service SHALL order the records by Name ascending within the applied filter, and for records sharing the same Name SHALL apply a deterministic secondary ordering by Nationality Code ascending.
11. IF a search request matches no nationality records, THEN THE Nationality_Service SHALL return an empty item collection with a total count of 0 and a success status.

### Requirement 6: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want nationality management and query operations restricted to System Administrators, so that only authorized personnel can view or change standardized reference data.

#### Acceptance Criteria

1. IF a request to create, update, query, or read the audit trail of a nationality is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Nationality_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored nationality record, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts to create, update, query, or read the audit trail of a nationality record, THEN THE Nationality_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored nationality record, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, or audit-trail-read operation, THE Nationality_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 5.
4. WHEN the Nationality_Service evaluates authentication and authorization for any create, update, query, or audit-trail-read request, THE Nationality_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the Nationality_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) established by FINNOVA-8. Establishing that scheme is a prerequisite already satisfied by the Lookup Master feature.
   - **Assumption:** Read access (query and audit-trail read) is restricted to System Administrators, mirroring the "enforce permissions" scenario. If broader read access is desired for consuming-module dropdowns, that is flagged for confirmation in design.
