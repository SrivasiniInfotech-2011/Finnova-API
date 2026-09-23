# Requirements Document

## Introduction

The Lookup Master Management feature (JIRA: FINNOVA-8, Module: SystemAdmin, Master: Lookup) provides a centralized administrative capability for configuring code-value tables ("lookups") used across all business modules of the Finnova platform. Lookups drive dropdown values and classification categories (for example, marital status options in customer origination) and must be standardized in one place rather than duplicated per module.

A System Administrator can filter lookups by Module and Lookup Type, view them in a paginated grid, and create, update, or delete lookup values. Each lookup value carries a code, a single English display value, a display order, and an active flag. Certain lookup codes are flagged as System Locked and are protected from renaming or deletion. Newly added active values become immediately available to the consuming modules' dropdowns.

The backend follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + PostgreSQL, generic `IRepository<T>` + `RepositoryBase`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`). The frontend follows React + Redux + RxJS conventions with React Testing Library.

This document captures the requirements derived from the Confluence Gherkin scenarios (TC-LKP-01, TC-LKP-02) and the JIRA ticket. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Authentication dependency):** JWT Bearer authentication is **not currently configured** in any Finnova service host (no `AddAuthentication`/`AddJwtBearer`, no `[Authorize]` attributes exist). This feature requires System Administrator authorization, so the JWT Bearer scheme and a SystemAdmin authorization policy/role **must be established** as a prerequisite. Requirement 7 states the enforcement behavior; the actual wiring of the JWT scheme is a design/infrastructure dependency to be resolved in the design phase.
- **Assumption (Host placement):** Lookup Master is a SystemAdmin concern and there is currently no SystemAdmin service host. Whether this feature lives in a new SystemAdmin service host or an existing host is a **design decision**, not a requirement, and is deferred to the design phase.
- **Note (Localization):** Finnova is an India-only platform. Each lookup value has a single English display value (`Value`); there are no bilingual fields.

## Glossary

- **Lookup Master**: The centralized administrative catalog of code-value entries used to populate dropdowns and classification categories across all business modules.
- **Lookup Value (Lookup Entry)**: A single record in the Lookup Master, consisting of a Lookup Code, a single English display value (`Value`), a Display Order, an Is Active flag, and a System Locked flag, scoped to a Module and a Lookup Type.
- **Lookup Type**: A named category that groups related lookup values (for example, `MARITAL_STATUS`). A lookup value belongs to exactly one Lookup Type.
- **Module**: A named business area that scopes lookups (for example, `Origination`). A lookup value belongs to exactly one Module.
- **Lookup Code**: A short machine-readable identifier for a lookup value (for example, `WID`), unique within its Module and Lookup Type scope.
- **System Locked**: A boolean flag on a lookup value indicating the code is system-defined. When true, the value cannot be renamed or deleted.
- **Display Order**: An integer that determines the ordering of lookup values when presented in a dropdown or grid, ascending.
- **Is Active**: A boolean flag indicating whether a lookup value is available for consumption by module dropdowns. Defaults to true.
- **System Administrator**: An authenticated user holding the SystemAdmin role/policy, authorized to manage lookup values.
- **Consuming Module**: A business module (for example, customer origination) that reads active lookup values to populate its dropdowns.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **Lookup_Service**: The backend service/API responsible for lookup value management and retrieval.
- **Lookup_Admin_UI**: The React-based Lookup Master administrative screen used by System Administrators.

## Requirements

### Requirement 1: Lookup Type and Module Taxonomy with Filtering

**User Story:** As a System Administrator, I want lookups to be scoped by Module and Lookup Type and filterable by both, so that I can locate and manage the correct set of code-value entries.

#### Acceptance Criteria

1. THE Lookup_Service SHALL associate each lookup value with exactly one Module and exactly one Lookup Type.
2. WHEN a System Administrator requests lookup values filtered by a Module and a Lookup Type, THE Lookup_Service SHALL return only the lookup values whose Module identifier and Lookup Type identifier are an exact, case-sensitive match to the requested Module and Lookup Type values.
3. WHEN a System Administrator requests lookup values filtered by Module `Origination` and Lookup Type `MARITAL_STATUS` (per TC-LKP-01), THE Lookup_Service SHALL return the lookup values belonging to that Module and Lookup Type scope.
4. WHERE a Module filter is provided without a Lookup Type filter, THE Lookup_Service SHALL return the lookup values matching the specified Module across all Lookup Types.
5. WHERE a Lookup Type filter is provided without a Module filter, THE Lookup_Service SHALL return the lookup values matching the specified Lookup Type across all Modules.
6. IF a filter references a Module or Lookup Type that is recognized but has no matching lookup values, THEN THE Lookup_Service SHALL return an empty result set with a success status.
7. IF a filter references a Module or Lookup Type identifier that is not recognized or is malformed, THEN THE Lookup_Service SHALL reject the request without returning lookup values and return a validation error response indicating which filter parameter was invalid.

### Requirement 2: Create Lookup Value (TC-LKP-01)

**User Story:** As a System Administrator, I want to add a new lookup value with its code, display value, display order, and active flag, so that the option becomes available in consuming module dropdowns.

#### Acceptance Criteria

1. WHEN a System Administrator submits a new lookup value with Lookup Code, Value, Display Order, Is Active, Module, and Lookup Type, THE Lookup_Service SHALL create the lookup value and return the created record with a system-generated identifier.
2. WHEN a System Administrator adds the lookup value `{ Code: WID, Value: Widowed, DisplayOrder: 4, IsActive: True }` under Module `Origination` and Lookup Type `MARITAL_STATUS` and saves it (per TC-LKP-01), THE Lookup_Service SHALL persist the value such that it is retrievable by the dropdown consumption query for that Module and Lookup Type.
3. IF a create request omits Lookup Code, Value, Module, or Lookup Type, THEN THE Lookup_Service SHALL reject the request with a validation error identifying the missing field and SHALL NOT persist any record.
4. WHERE Is Active is not provided on a create request, THE Lookup_Service SHALL default Is Active to true.
5. IF Display Order is not an integer in the range 0 to 9999 inclusive, THEN THE Lookup_Service SHALL reject the request with a validation error identifying Display Order and SHALL NOT persist any record.
6. WHEN a lookup value is created with Is Active set to true, THE Lookup_Service SHALL make the value available to the dropdown consumption query (Requirement 6) for its Module and Lookup Type without requiring a service restart.
7. IF Lookup Code exceeds 50 characters, or Value exceeds 200 characters, THEN THE Lookup_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
8. IF a create request specifies a Lookup Code that already exists for the same Module and Lookup Type, THEN THE Lookup_Service SHALL reject the request with a validation error indicating a duplicate Lookup Code and SHALL retain the existing record unchanged.
9. IF a create request references a Module or Lookup Type that does not exist, THEN THE Lookup_Service SHALL reject the request with a validation error indicating the unknown reference and SHALL NOT persist any record.

### Requirement 3: System-Locked Protection (TC-LKP-02)

**User Story:** As a System Administrator, I want system-defined lookup codes to be protected from rename and deletion, so that core system behavior that depends on those codes is not broken.

#### Acceptance Criteria

1. IF a System Administrator attempts to delete a lookup value whose System Locked flag is true (for example `SYS_TXN_TYPE`, per TC-LKP-02), THEN THE Lookup_Service SHALL block the operation, leave the target lookup value and all of its attributes unchanged, and return error code `ERR-LKP-005` with the message "System-defined lookup codes cannot be altered."
2. IF a System Administrator attempts to rename (change the Lookup Code of) a lookup value whose System Locked flag is true, THEN THE Lookup_Service SHALL block the operation, leave the existing Lookup Code and all other attributes unchanged, and return error code `ERR-LKP-005` with the message "System-defined lookup codes cannot be altered."
3. WHILE a lookup value's System Locked flag is true, THE Lookup_Service SHALL restrict protection to deletion and to changing the Lookup Code only, and SHALL continue to permit modification of non-identity attributes (such as display value and active/inactive status) under the standard lookup edit rules.
4. IF an update request targeting a lookup value whose System Locked flag is true includes both a protected change (Lookup Code change or deletion) and any other attribute change in a single operation, THEN THE Lookup_Service SHALL block the entire operation, persist no changes from that request, and return error code `ERR-LKP-005` with the message "System-defined lookup codes cannot be altered."
5. WHEN a System Administrator retrieves a lookup value whose System Locked flag is true, THE Lookup_Service SHALL include the System Locked status in the returned record so that the Lookup_Admin_UI can disable rename and delete controls.
   - **Assumption:** The System Locked flag itself is set by data seeding/administration outside this feature's create surface; this feature reads and enforces it rather than exposing it as a user-editable field.

### Requirement 4: List Lookup Values with Filtering and Pagination

**User Story:** As a System Administrator, I want to list lookup values filtered by module, type, and active status with pagination, so that I can review large sets of entries efficiently.

#### Acceptance Criteria

1. WHEN a System Administrator requests a list of lookup values, THE Lookup_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
2. WHERE a Module, Lookup Type, or active-status filter is provided, THE Lookup_Service SHALL return only lookup values matching all provided filters combined with logical AND.
3. WHERE no active-status filter is provided, THE Lookup_Service SHALL return lookup values regardless of their Is Active flag.
4. WHEN a System Administrator requests a list without specifying page size or page number, THE Lookup_Service SHALL apply default pagination parameters and return the corresponding page.
   - **Assumption:** Default page size and default page number follow the existing platform conventions established by `PaginatedResponse<T>` usage.
5. WHEN a System Administrator requests a page number beyond the last available page, THE Lookup_Service SHALL return an empty item collection while reporting the correct total count and pagination parameters.
6. WHEN returning a paginated list, THE Lookup_Service SHALL order the lookup values by Display Order ascending within the applied filters, and for lookup values sharing the same Display Order, SHALL apply a deterministic secondary ordering by unique identifier ascending.

### Requirement 5: Update and Delete Lookup Values with Uniqueness Enforcement

**User Story:** As a System Administrator, I want to update and delete lookup values while the system enforces code uniqueness and the system-lock rule, so that the lookup catalog remains consistent and safe.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to a non-system-locked lookup value's editable fields (Lookup Code, Value, Display Order, Is Active) and all submitted field values pass validation, THE Lookup_Service SHALL persist the changes and return the updated record reflecting the submitted values.
2. WHEN a System Administrator deletes a non-system-locked lookup value that is not referenced by any other record, THE Lookup_Service SHALL remove the value so that it no longer appears in list or dropdown consumption queries.
   - **Assumption:** Deletion is a hard delete of the lookup record. If soft-delete/deactivation is preferred, this is a design decision to confirm; deactivation can alternatively be achieved via Requirement 5.1 by setting Is Active to false.
3. IF a create or update request would result in a Lookup Code that is not unique within its Module and Lookup Type scope, THEN THE Lookup_Service SHALL reject the request with a validation error indicating a duplicate Lookup Code and SHALL preserve the existing record unchanged.
   - **Assumption:** Uniqueness is enforced on the combination of Lookup Code + Module + Lookup Type (the Gherkin/JIRA do not specify scope).
4. IF a System Administrator attempts to update or delete a lookup value that does not exist, THEN THE Lookup_Service SHALL return a not-found error and SHALL make no change to the catalog.
5. WHERE a lookup value's System Locked flag is true, THE Lookup_Service SHALL enforce the protection defined in Requirement 3 for update-that-renames and delete operations.
6. IF a System Administrator attempts to delete a lookup value that is referenced by one or more other records, THEN THE Lookup_Service SHALL reject the deletion with a validation error indicating the value is in use and SHALL preserve the value unchanged.
7. IF a System Administrator submits an update where Value is empty or exceeds 200 characters, or where Display Order is not an integer within the range 0 to 9999, THEN THE Lookup_Service SHALL reject the request with a validation error identifying the invalid field and SHALL preserve the existing record unchanged.

### Requirement 6: Dropdown Consumption Query

**User Story:** As a Consuming Module, I want an endpoint that returns active lookup values for a given module and lookup type ordered by display order, so that I can populate dropdowns with standardized, current options.

#### Acceptance Criteria

1. WHEN a Consuming Module requests lookup values for a given Module and Lookup Type, THE Lookup_Service SHALL return only the lookup values whose Is Active flag is true.
2. WHEN returning lookup values for dropdown consumption, THE Lookup_Service SHALL order the results by Display Order ascending, and for lookup values sharing the same Display Order, SHALL apply a deterministic secondary ordering by the Value ascending so that repeated queries with identical data return results in the same sequence.
3. WHEN a lookup value has been created as active (per TC-LKP-01), THE Lookup_Service SHALL include that value in the dropdown consumption result for its Module and Lookup Type on the next query.
4. WHEN returning a lookup value for dropdown consumption, THE Lookup_Service SHALL include the Value as the label so that the requesting client can render the option (for example "Widowed").
5. IF a Consuming Module requests lookup values for a Module or Lookup Type that is not defined in the system, THEN THE Lookup_Service SHALL reject the request without returning any lookup values and SHALL return an error indication identifying that the requested Module or Lookup Type is unknown.
6. WHEN a Consuming Module requests lookup values for a defined Module and Lookup Type that has no active lookup values, THE Lookup_Service SHALL return an empty result set rather than an error.

### Requirement 7: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want lookup management operations restricted to System Administrators, so that only authorized personnel can change centrally standardized values.

#### Acceptance Criteria

1. IF a request to create, update, or delete a lookup value is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Lookup_Service SHALL reject the request with an unauthorized (401) response and SHALL NOT modify any stored lookup value.
2. IF an authenticated user whose token lacks the SystemAdmin claim attempts to create, update, or delete a lookup value, THEN THE Lookup_Service SHALL reject the request with a forbidden (403) response and SHALL NOT modify any stored lookup value.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin claim invokes a create, update, or delete operation, THE Lookup_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 2, 3, and 5.
4. WHERE the dropdown consumption query (Requirement 6) is invoked by a caller presenting a valid, unexpired JWT Bearer token, THE Lookup_Service SHALL permit access regardless of the caller's role.
   - **Assumption:** Read access for dropdown consumption is broader than administrative access (authenticated users, not restricted to SystemAdmin). Whether this should be fully public/anonymous or scoped to authenticated callers is flagged for confirmation.
5. IF the dropdown consumption query is invoked without a valid JWT Bearer token, THEN THE Lookup_Service SHALL reject the request with an unauthorized (401) response.
6. **Assumption (dependency):** THE Finnova service host that exposes the Lookup_Service SHALL have the JWT Bearer authentication scheme and a SystemAdmin authorization policy configured, since neither currently exists in the platform. Establishing this scheme is a prerequisite for enforcing criteria 1 through 5.

### Requirement 8: Lookup Master Administrative UI

**User Story:** As a System Administrator, I want a Lookup Master screen with module/type filters and an editable grid, so that I can view, add, edit, and save lookup values.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Lookup_Admin_UI, THE Lookup_Admin_UI SHALL present a Module filter control and a Lookup Type filter control, with the Lookup Type control disabled until a Module is selected.
2. WHEN a System Administrator selects a Module and Lookup Type filter, THE Lookup_Admin_UI SHALL request the matching lookup values and display them in a grid ordered by Display Order ascending, with Lookup Code ascending as a tie-breaker.
3. WHEN a filter selection returns no lookup values, THE Lookup_Admin_UI SHALL display an empty-state indication rather than a blank grid.
4. IF a request to the Lookup_Service does not complete within 10 seconds, THEN THE Lookup_Admin_UI SHALL stop waiting and display a retry-able error indication to the System Administrator.
5. WHEN a System Administrator adds a new row and clicks "Save" (per TC-LKP-01), THE Lookup_Admin_UI SHALL send a create request for the new lookup value and, on success, refresh the grid to show the newly added value.
6. IF a System Administrator clicks "Save" on a new row with a missing required field (Lookup Code or Value), THEN THE Lookup_Admin_UI SHALL block submission and display a field-level validation message.
7. WHILE a lookup value's System Locked flag is true, THE Lookup_Admin_UI SHALL disable the rename and delete controls for that row.
8. IF a create, update, or delete request fails with error code `ERR-LKP-005`, THEN THE Lookup_Admin_UI SHALL display the message "System-defined lookup codes cannot be altered." to the System Administrator and SHALL leave the affected row's contents unchanged.
9. IF a create, update, or delete request fails with an error code other than `ERR-LKP-005`, THEN THE Lookup_Admin_UI SHALL display a general error indication and SHALL leave the affected row's contents unchanged.
10. WHEN the Lookup_Admin_UI issues any request to the Lookup_Service, THE Lookup_Admin_UI SHALL include the JWT Bearer token in the Authorization header.
   - **Assumption:** UI state is managed via a Redux lookups slice and asynchronous API calls (including JWT header propagation) are handled via RxJS epics, per platform conventions.
