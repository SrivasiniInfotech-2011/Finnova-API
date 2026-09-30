# Requirements Document

## Introduction

The Entity Master Management feature (JIRA: FINNOVA-11, Module: SystemAdmin, Master: Entity) provides a centralized administrative catalog of external and internal parties used across the Finnova platform. These parties — Dealers, Debt Collectors, Life Insurers, Suppliers, and Employers — are referenced by multiple business modules (for example, when recording recovery assignments, disbursements, insurance linkages, or supplier payments). Entity reference data must be standardized and controlled in one place, typed by category, rather than entered ad hoc per module.

A System Administrator can create an entity of a given Entity Type (a code, an English display name, an Entity Type, common contact/registration attributes, and any per-type attributes), modify an entity's editable attributes, search entities by code, name, or registration identifier with pagination and an optional filter by Entity Type, read a single entity's details, and deactivate/reactivate an entity. Every change to an entity's configuration is captured in an immutable audit trail. All operations — including reads — are restricted to authenticated callers with the SystemAdmin role.

This feature mirrors the established Lookup Master (FINNOVA-8), Nationality Master (FINNOVA-9), Organization Hierarchy Master (FINNOVA-5), Document Number Control Master (FINNOVA-7), and Court Master (FINNOVA-13) features. It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` host behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI). The backend is API-only.

This document captures the requirements derived from the JIRA ticket. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** Entity Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host, reached through the gateway. A UI-alias gateway route mirrors the lookup/nationality/orghierarchy/dcn/court aliases. Confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8). This feature reuses that scheme and policy; token issuance is external.
- **Assumption (Audit scope):** The entity-configuration audit trail (Requirement 5) mirrors the audit trails of the sibling masters. It captures create and update (including activate/deactivate).
- **Assumption (Delete):** The ticket calls for Create/Modify/Query and does not mention hard deletion. Deactivation (soft retire) is the proposed default to preserve referential history; hard delete is intentionally not offered. Confirm if hard delete is required.
- **Assumption (Entity Type as enum):** Entity Type is modeled as a fixed enum with the values `Dealer`, `DebtCollector`, `Insurer`, `Supplier`, and `Employer`. New categories require a code change rather than data entry. Confirm if Entity Type must instead be an admin-maintainable lookup.
- **Assumption (Uniqueness key):** Entity Code is proposed as the unique business key. It is unique **per Entity Type** (the same code may recur across different types) rather than globally. Confirm whether Entity Code must instead be globally unique across all types.
- **Assumption (Common vs per-type attributes):** Common attributes (Entity Code, Name, Entity Type, a Registration Identifier, contact fields) apply to all entities; per-type attributes are captured as a typed set specific to each Entity Type. The concrete per-type attribute list is deferred to design and confirmed there.
- **Assumption (Registration identifier):** A Registration Identifier (for example a GST or PAN-style business identifier) is captured as a single optional English free-text field and is not validated against an external registry. Confirm if format validation or external verification is required.
- **Note (Localization):** Finnova is an India-only, English-only platform. An entity carries a single English display name and single plain display fields; there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **Entity Master**: The centralized administrative catalog of external and internal parties (Dealers, Debt Collectors, Insurers, Suppliers, Employers) used across business modules.
- **Entity**: A single record in the Entity Master, consisting of an Entity Code, a display Name, an Entity Type, a Registration Identifier, common contact attributes, per-type attributes, an Is Active flag, and audit timestamps.
- **Entity Code**: A short machine-readable identifier for an entity (for example, `DLR001`), unique per Entity Type across the Entity Master (case-insensitive, trimmed).
- **Entity Type**: The category of party the record represents: `Dealer`, `DebtCollector`, `Insurer`, `Supplier`, or `Employer`.
- **Registration Identifier**: A single English business/registration reference for the entity (for example a GST or PAN-style value), captured as a plain optional value.
- **Common Attributes**: Attributes present on every entity regardless of type (Entity Code, Name, Entity Type, Registration Identifier, contact fields).
- **Per-Type Attributes**: Attributes that apply only to entities of a specific Entity Type, captured as a typed set resolved during design.
- **Is Active**: A boolean flag indicating whether an entity is available for selection in consuming modules. Defaults to true. An inactive entity is retained but flagged as retired.
- **Audit Entry**: An immutable record capturing a change to an entity's configuration, including the affected entity identifier, the action performed, the before/after values of changed fields, the acting administrator's identifier, and the UTC timestamp.
- **Audit Trail**: The ordered collection of Audit Entries recorded for an entity.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim.
- **EARS**: Easy Approach to Requirements Syntax.
- **Entity_Service**: The backend service/API responsible for entity management and retrieval.
- **Entity_Admin_UI**: The React-based Entity Master administrative screen (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Entity

**User Story:** As a System Administrator, I want to create an entity of a given type with its reference data, so that external and internal parties are standardized for use across modules.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing an Entity Code, a Name, an Entity Type, and the applicable attributes for that Entity Type, and every applicable validation in criteria 2 through 6 passes, THE Entity_Service SHALL persist exactly one new entity and return the created entity including a system-generated identifier, the stored Entity Code and Name with leading and trailing whitespace trimmed, and the resolved Is Active value.
2. WHERE Is Active is not provided on a create request, THE Entity_Service SHALL default Is Active to true.
3. IF a create request omits Entity Code, Name, or Entity Type, or provides any of them as empty or whitespace-only, THEN THE Entity_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
4. IF Entity Code exceeds 20 characters, or Name exceeds 200 characters, or the Registration Identifier exceeds 50 characters (each measured after trimming leading and trailing whitespace), THEN THE Entity_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
   - **Assumption:** Length bounds (Code 20, Name 200, Registration Identifier 50) are not specified in the ticket; these values are proposed for confirmation.
5. IF the Entity Type is not one of `Dealer`, `DebtCollector`, `Insurer`, `Supplier`, or `Employer`, THEN THE Entity_Service SHALL reject the request with a validation error and SHALL NOT persist any record.
6. IF a create request supplies per-type attributes that are not applicable to the submitted Entity Type, THEN THE Entity_Service SHALL reject the request with a validation error identifying the inapplicable attributes and SHALL NOT persist any record.
7. WHEN an entity is created, THE Entity_Service SHALL include the entity in the results of the entity query (Requirement 3) on the next query request without requiring a service restart.

### Requirement 2: Prevent Duplicate Entity

**User Story:** As a System Administrator, I want the system to prevent duplicate entities, so that each entity is uniquely identifiable within its type.

#### Acceptance Criteria

1. THE Entity_Service SHALL treat Entity Code as unique per Entity Type across the Entity Master, comparing codes case-insensitively after trimming leading and trailing whitespace within the same Entity Type.
   - **Assumption:** Uniqueness scope is per Entity Type (a `Dealer` and a `Supplier` may share the code `X01`). Confirm if Entity Code must be globally unique across all types.
2. IF a create request specifies an Entity Code that, after trimming and case-insensitive comparison, matches the code of an existing entity of the same Entity Type, THEN THE Entity_Service SHALL reject the request with a validation error carrying the message "Entity code must be unique per entity type", SHALL create no new record, and SHALL leave the existing record's stored Entity Code, Entity Type, and all other fields unchanged.
3. THE Entity_Service SHALL treat Entity Code and Entity Type as immutable after creation; an update request SHALL NOT change the Entity Code or the Entity Type (see Requirement 4).
4. IF a create request specifies an Entity Code that is empty, contains only whitespace, or exceeds 20 characters after trimming, THEN THE Entity_Service SHALL reject the request with a validation error indicating the entity code is required and must be at most 20 characters, and SHALL make no change to the Entity Master.
5. WHEN validating a create request, THE Entity_Service SHALL evaluate required-field and length validation before the duplicate-code check, so that a request that is both invalid and duplicate is reported as a validation error rather than a duplicate conflict.
6. IF two or more create requests specifying an Entity Code that, after trimming and case-insensitive comparison, resolves to the same code within the same Entity Type are processed concurrently, THEN THE Entity_Service SHALL persist exactly one entity for that Entity Code and Entity Type and SHALL reject every other such request with the duplicate-code validation error described in criterion 2.
   - **Assumption:** The Entity Master is subject to concurrent create requests from multiple System Administrators; uniqueness must hold even under simultaneous submission. Confirm if creates are serialized upstream and this guarantee is unnecessary.

### Requirement 3: Query, Search, and Read Entities

**User Story:** As a System Administrator, I want to search and read entities by code, name, or registration identifier and filter by entity type, so that I can locate and inspect the correct entity.

#### Acceptance Criteria

1. WHEN a System Administrator requests entities with a non-empty search term, THE Entity_Service SHALL trim the search term and return only the entities whose Entity Code, Name, or Registration Identifier contains the trimmed term as a substring, matched case-insensitively, subject to the applied Entity Type filter and pagination parameters.
2. WHERE a System Administrator supplies an Entity Type filter, THE Entity_Service SHALL return only the entities whose Entity Type equals the supplied value, subject to any search term and the applied pagination parameters.
3. IF a System Administrator supplies an Entity Type filter value that is not one of `Dealer`, `DebtCollector`, `Insurer`, `Supplier`, or `Employer`, THEN THE Entity_Service SHALL reject the request with a validation error indicating the entity type filter is invalid, without returning any records.
4. WHEN a System Administrator requests entities with a search term consisting only of whitespace or an empty string, THE Entity_Service SHALL treat the request as having no search term and return the entities subject to the applied Entity Type filter and pagination parameters.
5. IF a System Administrator supplies a search term that, after trimming, exceeds 200 characters, THEN THE Entity_Service SHALL reject the request with a validation error indicating the search term is too long, without returning any records.
   - **Assumption:** The maximum search-term length (200) is not specified in the ticket; it is aligned to the Name bound in Requirement 1 and proposed for confirmation.
6. WHEN a System Administrator requests entities, THE Entity_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, applied page size, and total page count.
7. WHEN a System Administrator requests entities without specifying a page number, THE Entity_Service SHALL apply a default page number of 1; WHEN no page size is specified, THE Entity_Service SHALL apply a default page size of 20.
8. IF a System Administrator requests a page number less than 1, or a page size less than 1 or greater than 100, THEN THE Entity_Service SHALL reject the request with a validation error indicating the parameter is out of range, without returning any records.
9. WHEN a System Administrator requests a page number beyond the last available page, THE Entity_Service SHALL return an empty item collection while reporting the correct total count, the requested current page number, and the applied page size.
10. WHEN returning entities, THE Entity_Service SHALL order the records by Name ascending within the applied filter, and for records sharing the same Name SHALL apply a deterministic secondary ordering by Entity Code ascending.
11. IF a search request matches no entities, THEN THE Entity_Service SHALL return an empty item collection with a total count of 0, a total page count of 0, the requested current page number, the applied page size, and a success status.
12. WHEN a System Administrator requests a single entity by its identifier, THE Entity_Service SHALL return the entity if it exists; IF the identifier does not exist, THEN THE Entity_Service SHALL return a not-found error.
13. IF a System Administrator requests a single entity using a missing or malformed identifier, THEN THE Entity_Service SHALL reject the request with a validation error and SHALL NOT return an entity.

### Requirement 4: Modify Entity

**User Story:** As a System Administrator, I want to update an entity's editable attributes, so that entity reference data stays accurate.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing entity's Name, Registration Identifier, contact attributes, applicable per-type attributes, or Is Active flag, and the submitted values pass validation and differ from the current values, THE Entity_Service SHALL persist the changes, set the record's UpdatedAt timestamp to the current UTC time, record exactly one update Audit Entry (Requirement 5.2), and return the updated entity with its persisted values.
2. IF a System Administrator submits an update that attempts to change the Entity Code or the Entity Type, THEN THE Entity_Service SHALL reject the request with a validation error identifying the immutable field and SHALL preserve the existing record unchanged.
3. IF a System Administrator submits an update where Name is empty or whitespace or exceeds 200 characters, or where the Registration Identifier exceeds 50 characters, or where a contact attribute exceeds 100 characters, or where a per-type attribute is not applicable to the entity's Entity Type, THEN THE Entity_Service SHALL reject the request with a validation error identifying each offending field, SHALL preserve the existing record unchanged, and SHALL record no Audit Entry.
   - **Assumption:** The contact-attribute length bound (100) is not specified in the ticket; it is proposed for confirmation.
4. IF a System Administrator attempts to update an entity whose identifier does not exist, THEN THE Entity_Service SHALL return a not-found error, SHALL make no change to the Entity Master, and SHALL record no Audit Entry.
5. WHEN a System Administrator submits an update whose editable values, compared after trimming leading and trailing whitespace on Name and Registration Identifier, all equal the record's current values, THE Entity_Service SHALL treat the update as a successful no-op, return the existing record unchanged, refresh no timestamp, and record no Audit Entry.

### Requirement 5: Record Audit Trail on Configuration Change

**User Story:** As a System Administrator, I want every entity change captured in an audit trail, so that changes to entity reference data are traceable.

#### Acceptance Criteria

1. WHEN the Entity_Service persists a create of an entity (Requirement 1), THE Entity_Service SHALL record exactly one Audit Entry containing the created entity identifier, the create action indicator, the resulting configuration values, the acting administrator's identifier, and the change timestamp recorded in UTC to millisecond precision.
2. WHEN the Entity_Service persists an update to an entity's editable configuration (Requirement 4), THE Entity_Service SHALL record exactly one Audit Entry containing the affected entity identifier, the update action indicator, the before and after values of the changed fields, the acting administrator's identifier, and the change timestamp recorded in UTC to millisecond precision.
3. WHEN the Entity_Service deactivates or reactivates an entity (a change to Is Active), THE Entity_Service SHALL record the change as an update Audit Entry capturing the prior and new Is Active values.
4. IF a create or update request is rejected by validation or by the uniqueness rule (Requirement 2), THEN THE Entity_Service SHALL NOT record an Audit Entry for that rejected request and SHALL leave the count of persisted Audit Entries unchanged.
5. WHEN a System Administrator requests the audit entries for a specified entity, THE Entity_Service SHALL return the Audit Entries for that entity with each entry's change timestamp expressed in UTC, ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
6. IF a System Administrator requests the audit entries for an entity identifier that does not exist, THEN THE Entity_Service SHALL return an empty result set and SHALL NOT return an error.
7. THE Entity_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that no operation exposed by the service updates or deletes a previously recorded Audit Entry.
8. IF the Entity_Service fails to record the Audit Entry for a create or update after the entity change would otherwise be persisted, THEN THE Entity_Service SHALL roll back the entity change so that neither the entity change nor the Audit Entry is persisted, and SHALL return an error response indicating the change could not be recorded.

### Requirement 6: Deactivate and Reactivate Entity

**User Story:** As a System Administrator, I want to deactivate an entity without deleting it, so that an entity can be retired while preserving its history.

#### Acceptance Criteria

1. WHEN a System Administrator deactivates an existing active entity, THE Entity_Service SHALL set Is Active to false, set the UpdatedAt timestamp to the current UTC time, record an update Audit Entry (Requirement 5.3), and return the updated entity.
2. WHEN a System Administrator reactivates an existing inactive entity, THE Entity_Service SHALL set Is Active to true, set the UpdatedAt timestamp to the current UTC time, record an update Audit Entry, and return the updated entity.
3. WHEN a System Administrator deactivates an entity that is already inactive, or reactivates an entity that is already active, THE Entity_Service SHALL treat the request as a successful no-op, return the existing record unchanged, refresh no timestamp, and record no Audit Entry.
4. IF a System Administrator attempts to deactivate or reactivate an entity whose identifier does not exist, THEN THE Entity_Service SHALL return a not-found error and SHALL make no change to the Entity Master.
5. WHILE an entity is inactive, THE Entity_Service SHALL continue to return it in entity queries and single-entity reads (Requirement 3), distinguished by its Is Active flag.
6. **Assumption (delete):** Hard deletion is intentionally not offered; deactivation is the retire mechanism. Confirm if hard delete is required.

### Requirement 7: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want Entity management, query, and read operations restricted to authorized callers, so that only authorized personnel can view or change entity reference data.

#### Acceptance Criteria

1. IF a request to any create, update, query, single-read, or audit-trail-read operation of the Entity_Service is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Entity_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored entity, SHALL NOT disclose any entity reference data in the response, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts any Entity operation, THEN THE Entity_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored entity, SHALL NOT disclose any entity reference data in the response, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, update, query, read, or audit-trail-read operation, THE Entity_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 6.
4. WHEN the Entity_Service evaluates authentication and authorization for any request, THE Entity_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response, treating a token as expired at or after its exact expiry instant with no clock-skew tolerance.
   - **Assumption:** Zero clock-skew tolerance at the token-expiry boundary is proposed for confirmation.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the Entity_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy established by FINNOVA-8.
