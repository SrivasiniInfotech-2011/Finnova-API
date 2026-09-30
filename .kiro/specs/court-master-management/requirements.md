# Requirements Document

## Introduction

The Court Master Management feature (JIRA: FINNOVA-13, Module: SystemAdmin, Master: Court) provides a centralized administrative catalog of courts used across the Finnova platform (for example, when recording legal cases, notices, or recovery proceedings tied to a court). Court reference data must be standardized and controlled in one place rather than entered ad hoc per module.

A System Administrator can create a court (a code, an English display name, a court type, a jurisdiction, and a location), modify a court's editable attributes, search courts by code, name, or jurisdiction with pagination, read a single court's details, and deactivate/reactivate a court. Every change to a court's configuration is captured in an immutable audit trail. All operations — including reads — are restricted to authenticated callers with the SystemAdmin role.

This feature mirrors the established Lookup Master (FINNOVA-8), Nationality Master (FINNOVA-9), Organization Hierarchy Master (FINNOVA-5), and Document Number Control Master (FINNOVA-7) features. It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` host behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI). The backend is API-only.

This document captures the requirements derived from the JIRA ticket. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** Court Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host, reached through the gateway. A UI-alias gateway route mirrors the lookup/nationality/orghierarchy/dcn aliases. Confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8). This feature reuses that scheme and policy; token issuance is external.
- **Assumption (Audit scope):** The court-configuration audit trail (Requirement 5) mirrors the audit trails of the sibling masters. It captures create and update (including activate/deactivate).
- **Assumption (Delete):** The ticket calls for Create/Modify/Query and does not mention hard deletion. Deactivation (soft retire) is the proposed default to preserve referential history; hard delete is intentionally not offered. Confirm if hard delete is required.
- **Assumption (Reference attributes):** Jurisdiction and Location are captured as single English free-text fields rather than structured references to other masters (e.g. a State lookup). If they must be structured/linked, raise it explicitly.
- **Note (Localization):** Finnova is an India-only, English-only platform. A court carries a single English display name; there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **Court Master**: The centralized administrative catalog of courts used across business modules.
- **Court**: A single record in the Court Master, consisting of a Court Code, a display Name, a Court Type, a Jurisdiction, a Location, an Is Active flag, and audit timestamps.
- **Court Code**: A short machine-readable identifier for a court (for example, `DELHC`), unique across the Court Master (case-insensitive, trimmed).
- **Court Type**: The category of court: `Supreme`, `High`, `District`, `Tribunal`, or `Other`.
- **Jurisdiction**: The territorial or subject-matter jurisdiction the court covers (for example, a state or region), captured as a single English value.
- **Location**: The place/city where the court sits, captured as a single English value.
- **Is Active**: A boolean flag indicating whether a court is available for selection in consuming modules. Defaults to true. An inactive court is retained but flagged as retired.
- **Audit Entry**: An immutable record capturing a change to a court's configuration, including the affected court identifier, the action performed, the before/after values of changed fields, the acting administrator's identifier, and the UTC timestamp.
- **Audit Trail**: The ordered collection of Audit Entries recorded for a court.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim.
- **EARS**: Easy Approach to Requirements Syntax.
- **Court_Service**: The backend service/API responsible for court management and retrieval.
- **Court_Admin_UI**: The React-based Court Master administrative screen (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Court

**User Story:** As a System Administrator, I want to create a court with its reference data, so that courts are standardized for use across modules.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a Court Code, a Name, a Court Type, a Jurisdiction, and a Location, THE Court_Service SHALL persist a new court and return the created court including a system-generated identifier and the resolved Is Active value.
2. WHERE Is Active is not provided on a create request, THE Court_Service SHALL default Is Active to true.
3. IF a create request omits Court Code, Name, Court Type, Jurisdiction, or Location, or provides any of them as empty or whitespace-only, THEN THE Court_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
4. IF Court Code exceeds 20 characters, or Name exceeds 150 characters, or Jurisdiction exceeds 100 characters, or Location exceeds 100 characters, THEN THE Court_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Code 20, Name 150, Jurisdiction 100, Location 100) are not specified in the ticket; these values are proposed for confirmation.
5. IF the Court Type is not one of `Supreme`, `High`, `District`, `Tribunal`, or `Other`, THEN THE Court_Service SHALL reject the request with a validation error and SHALL NOT persist any record.
6. WHEN a court is created, THE Court_Service SHALL include the court in the results of the court query (Requirement 3) without requiring a service restart.

### Requirement 2: Prevent Duplicate Court Code

**User Story:** As a System Administrator, I want the system to prevent duplicate court codes, so that each court is uniquely identifiable.

#### Acceptance Criteria

1. THE Court_Service SHALL treat Court Code as unique across the Court Master, comparing codes case-insensitively after trimming leading and trailing whitespace.
2. IF a create request specifies a Court Code that, after trimming and case-insensitive comparison, matches the code of an existing court, THEN THE Court_Service SHALL reject the request with a validation error carrying the message "Court code must be unique", SHALL create no new record, and SHALL retain the existing record unchanged.
3. THE Court_Service SHALL treat Court Code as immutable after creation; an update request SHALL NOT change the Court Code (see Requirement 4).
4. IF a create request specifies a Court Code that is empty, contains only whitespace, or exceeds 20 characters after trimming, THEN THE Court_Service SHALL reject the request with a validation error indicating the court code is required and must be at most 20 characters, and SHALL make no change to the Court Master.
5. WHEN validating a create request, THE Court_Service SHALL evaluate required-field and length validation before the duplicate-code check, so that a request that is both invalid and duplicate is reported as a validation error rather than a duplicate conflict.

### Requirement 3: Query, Search, and Read Courts

**User Story:** As a System Administrator, I want to search and read courts by code, name, or jurisdiction, so that I can locate and inspect the correct court.

#### Acceptance Criteria

1. WHEN a System Administrator requests courts with a non-empty search term, THE Court_Service SHALL return only the courts whose Court Code, Name, or Jurisdiction contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHEN a System Administrator requests courts with a search term consisting only of whitespace or an empty string, THE Court_Service SHALL treat the request as having no search term and return the courts subject to the applied pagination parameters.
3. WHEN a System Administrator requests courts, THE Court_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, applied page size, and total page count.
4. WHEN a System Administrator requests courts without specifying a page number, THE Court_Service SHALL apply a default page number of 1; WHEN no page size is specified, THE Court_Service SHALL apply a default page size of 20.
5. IF a System Administrator requests a page number less than 1, or a page size less than 1 or greater than 100, THEN THE Court_Service SHALL reject the request with a validation error indicating the parameter is out of range, without returning any records.
6. WHEN a System Administrator requests a page number beyond the last available page, THE Court_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
7. WHEN returning courts, THE Court_Service SHALL order the records by Name ascending within the applied filter, and for records sharing the same Name SHALL apply a deterministic secondary ordering by Court Code ascending.
8. IF a search request matches no courts, THEN THE Court_Service SHALL return an empty item collection with a total count of 0 and a success status.
9. WHEN a System Administrator requests a single court by its identifier, THE Court_Service SHALL return the court if it exists; IF the identifier does not exist, THEN THE Court_Service SHALL return a not-found error.

### Requirement 4: Modify Court

**User Story:** As a System Administrator, I want to update a court's editable attributes, so that court reference data stays accurate.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing court's Name, Court Type, Jurisdiction, Location, or Is Active flag, and the submitted values pass validation, THE Court_Service SHALL persist the changes, refresh the record's UpdatedAt timestamp, and return the updated court.
2. THE Court_Service SHALL NOT allow an update to change the Court Code (it is immutable after creation).
3. IF a System Administrator submits an update where Name is empty or whitespace or exceeds 150 characters, or where Court Type is not a supported value, or where Jurisdiction is empty/whitespace or exceeds 100 characters, or where Location is empty/whitespace or exceeds 100 characters, THEN THE Court_Service SHALL reject the request with a validation error identifying the offending field and SHALL preserve the existing record unchanged.
4. IF a System Administrator attempts to update a court whose identifier does not exist, THEN THE Court_Service SHALL return a not-found error and SHALL make no change to the Court Master.
5. WHEN a System Administrator submits an update whose editable values all equal the record's current values, THE Court_Service SHALL treat the update as a successful no-op, return the existing record unchanged, refresh no timestamp, and record no audit entry.

### Requirement 5: Record Audit Trail on Configuration Change

**User Story:** As a System Administrator, I want every court change captured in an audit trail, so that changes to court reference data are traceable.

#### Acceptance Criteria

1. WHEN the Court_Service persists a create of a court (Requirement 1), THE Court_Service SHALL record exactly one Audit Entry containing the created court identifier, the create action indicator, the resulting configuration values, the acting administrator's identifier, and the UTC timestamp.
2. WHEN the Court_Service persists an update to a court's editable configuration (Requirement 4), THE Court_Service SHALL record exactly one Audit Entry containing the affected court identifier, the update action indicator, the before and after values of the changed fields, the acting administrator's identifier, and the UTC timestamp.
3. WHEN the Court_Service deactivates or reactivates a court (a change to Is Active), THE Court_Service SHALL record the change as an update Audit Entry capturing the prior and new Is Active values.
4. IF a create or update request is rejected by validation or by the uniqueness rule (Requirement 2), THEN THE Court_Service SHALL NOT record an Audit Entry for that rejected request and SHALL leave the count of persisted Audit Entries unchanged.
5. WHEN a System Administrator requests the audit entries for a specified court, THE Court_Service SHALL return the Audit Entries for that court ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
6. IF a System Administrator requests the audit entries for a court identifier that does not exist, THEN THE Court_Service SHALL return an empty result set and SHALL NOT return an error.
7. THE Court_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that no operation exposed by the service updates or deletes a previously recorded Audit Entry.

### Requirement 6: Deactivate and Reactivate Court

**User Story:** As a System Administrator, I want to deactivate a court without deleting it, so that a court can be retired while preserving its history.

#### Acceptance Criteria

1. WHEN a System Administrator deactivates an existing active court, THE Court_Service SHALL set Is Active to false, refresh the UpdatedAt timestamp, record an update Audit Entry (Requirement 5.3), and return the updated court.
2. WHEN a System Administrator reactivates an existing inactive court, THE Court_Service SHALL set Is Active to true, refresh the UpdatedAt timestamp, record an update Audit Entry, and return the updated court.
3. IF a System Administrator attempts to deactivate or reactivate a court whose identifier does not exist, THEN THE Court_Service SHALL return a not-found error and SHALL make no change to the Court Master.
4. WHILE a court is inactive, THE Court_Service SHALL continue to return it in court queries and single-court reads (Requirement 3), distinguished by its Is Active flag.
5. **Assumption (delete):** Hard deletion is intentionally not offered; deactivation is the retire mechanism. Confirm if hard delete is required.

### Requirement 7: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want Court management, query, and read operations restricted to authorized callers, so that only authorized personnel can view or change court reference data.

#### Acceptance Criteria

1. IF a request to create, update, query, read, or read the audit trail of a court is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Court_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored court, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts any Court operation, THEN THE Court_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored court, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, read, or audit-trail-read operation, THE Court_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 6.
4. WHEN the Court_Service evaluates authentication and authorization for any request, THE Court_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the Court_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy established by FINNOVA-8.
