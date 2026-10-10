# Requirements Document

## Introduction

The Asset Master Management feature (JIRA: FINNOVA-14, Module: SystemAdmin, Master: Asset) re-engineers the legacy "Asset Master" ASP.NET screen into a modern, full-stack capability spanning the Finnova backend API and the Finnova-UI React application. The feature lets a System Administrator maintain the master data that classifies and describes fixed assets used across the Finnova platform.

The legacy screen is organized into two top-level areas that this feature preserves end-to-end (database → repository → service → controller → gateway → UI):

1. **Asset Definition** — four code masters: **Class Codes**, **Make Codes**, **Type Codes**, and **Model Codes**. Each code master is a set of records consisting of a Code and a Description, supporting list/paginate/search, create, update, and delete/deactivate. Together they form the classification vocabulary that assets draw on.
2. **Asset Mapping** — actual asset records carrying an auto-generated Asset Code, an Asset Code Description, an Asset Category (drawn from Class Codes), an Asset Type (drawn from Type Codes), book and stock depreciation categories and rates, a guideline limit, and an active indicator. Asset Mapping supports create (with server-side Asset Code generation), update, list/paginate/search, and get-by-id.

This is a **full-stack** effort covering **both** the backend and the frontend:

- **Backend** (`e:\Finnova\Finnova-API`, solution `Finnova.Backend.slnx`) follows the platform's layered CQRS conventions: `Finnova.Models` (entities, enums, exceptions, record-based request/response contracts), `Finnova.Repository` (EF Core + **SQL Server**, generic `IRepository<T>` + `RepositoryBase<T>`, `IEntityTypeConfiguration<T>` configurations, `DbSet`s on `FinnovaDbContext`), `Finnova.Service` (MediatR Commands/Queries, FluentValidation validators behind the `ValidationBehavior` pipeline, static `.ToResponse()` mappers), the `Finnova.SystemAdminService` host (:5030) with its controller and `ExceptionHandlingMiddleware`, and the `Finnova.ApiGateway` YARP routing (`/api/systemadmin/**` → `systemadmin-cluster`, plus the UI-prefix alias pattern `/api/ua/api/...` already used by SystemAdmin features such as Lookup). The platform `PaginatedResponse<T>` contract is reused.
- **Frontend** (`E:\Finnova\Finnova-UI\Finnova-UI`) is React 18 + TypeScript + Material UI (`@mui/material`, `@mui/x-data-grid`), built with Vite, using React hooks and React Context — no Redux, no RxJS. The screen is built **mock-first**: it consumes an `Asset_Master_Service` abstraction defined as a TypeScript interface (`src/services/interfaces`) with a mock implementation (`src/services/mock`), a real axios implementation (`src/services/real`), and a toggle module selecting between them via `VITE_USE_MOCK_API`. The real implementation reuses the shared axios instance (`src/services/api.ts`) that injects the JWT Bearer token from `localStorage('finnova_token')` and centrally handles 401/403/409/timeout toasts. The screen mirrors the established `location.service.ts` / `lookup.service.ts` services and `LookupMaster.tsx` / `LocationMaster.tsx` reference pages, replacing the legacy grid + per-row Query/Modify icon + manual paging layout with a Material UI DataGrid plus dialog-based create/edit while preserving the underlying data fields.

This feature mirrors the structure and conventions of the Lookup Master Management (FINNOVA-8) and Nationality Master Management (FINNOVA-9) features. This document captures the requirements derived from the two legacy "Asset Master" screenshots (Asset Master – Details with Asset Definition / Asset Mapping tabs, and Asset Master – Create) and the JIRA ticket. The legacy interface is indicative only; modernization of layout and interaction is explicitly in scope. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Backend host placement):** Asset Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (:5030), which already exposes Lookup Master and Nationality Master, reached through the gateway prefix `/api/systemadmin/**`. A UI-prefix alias route (mirroring the Lookup pattern `/api/ua/api/...` rewritten to the SystemAdmin cluster) is added so the UI-composed URL reaches the SystemAdmin cluster. The exact host, route, and alias are confirmed during design.
- **Assumption (Persistence engine):** The platform persists through EF Core on **SQL Server** (not PostgreSQL). All entity configurations, migrations, and uniqueness/index decisions target SQL Server.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8 as a prerequisite, since no authentication scheme previously existed in the platform). This feature reuses that scheme and policy. Requirement 7 states the enforcement behavior; token issuance (login) ownership is external to this feature.
- **Assumption (Asset classification taxonomy):** Class, Make, Type, and Model are four independent code masters. An asset record references an Asset Category drawn from Class Codes and an Asset Type drawn from Type Codes. Whether an asset additionally references a Make Code and a Model Code (and whether those references are mandatory) is derived from the legacy screen and flagged for confirmation; Make and Model are maintained as code masters in Asset Definition regardless.
- **Assumption (Code uniqueness scope):** Each code master enforces Code uniqueness within its own master only (a Class Code and a Type Code may share the same Code string). Comparison is case-insensitive after trimming surrounding whitespace. The exact scope is flagged for confirmation.
- **Assumption (Asset Code auto-generation):** The Asset Code is generated server-side by the Asset_Master_Service and is read-only on the UI. The exact format (for example a category-prefixed zero-padded sequence) and timing (generated on save rather than on dialog open) are flagged for confirmation; the mock implementation generates a representative value.
- **Assumption (Numeric ranges and precision):** Book Depreciation Rate % and Stock Depreciation Rate % are decimals in the inclusive range 0 to 100 with at most 2 decimal places. The Guideline Limit is a non-negative decimal. The exact bounds and precision are flagged for confirmation.
- **Assumption (Delete vs deactivate):** Code master records and asset records are retired by setting their Active flag to false (deactivation / soft delete) rather than hard deletion, so that historical references are preserved. Whether a hard-delete action is also required is flagged for confirmation (Requirement 6).
- **Assumption (Pagination defaults):** List endpoints default to page number 1 and page size 20, accept page size up to 100, and use `PaginatedResponse<T>`. These defaults mirror the Nationality Master and are flagged for confirmation.
- **Assumption (Seed data shape):** Seed and sample data for the four code masters and asset records are English-only and representative of India-market asset classifications, mirroring the legacy data fields. The exact seed shape is confirmed during design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Every description/display value is a single plain English field (for example `Description`); there are no bilingual (English/Arabic) fields, no non-English seed, sample, or test data, and no right-to-left rendering.

## Glossary

- **Asset Master**: The administrative capability for maintaining asset classification codes (Asset Definition) and asset records (Asset Mapping), spanning backend and frontend.
- **Asset Definition**: The area of the Asset Master containing the four code masters — Class Codes, Make Codes, Type Codes, and Model Codes.
- **Asset Mapping**: The area of the Asset Master listing actual asset records and providing their create/edit flows.
- **Code Master**: A set of code records, each consisting of a Code and a Description, maintained by one of the four Asset Definition sub-tabs (Class, Make, Type, Model).
- **Class Code**: A code record classifying an asset's class (asset category grouping), consisting of a Code and a Description.
- **Make Code**: A code record identifying an asset's make (manufacturer), consisting of a Code and a Description.
- **Type Code**: A code record identifying an asset's type, consisting of a Code and a Description.
- **Model Code**: A code record identifying an asset's model, consisting of a Code and a Description.
- **Code**: A short machine-readable identifier for a code master record, unique within its own code master.
- **Description**: The single English display text of a code master record or asset record.
- **Asset Record (Asset)**: A single record in Asset Mapping, consisting of an Asset Code, an Asset Code Description, an Asset Category, an Asset Type, book and stock depreciation categories and rates, a guideline limit, and an Active indicator.
- **Asset Code**: The identifier of an asset record. It is auto-generated server-side and read-only on the UI.
- **Asset Code Description**: The single English display text describing an asset record.
- **Asset Category**: The classification referenced by an asset, drawn from the Class Codes.
- **Asset Type**: The type classification referenced by an asset, drawn from the Type Codes.
- **Book Depreciation Category / Book Depreciation Rate %**: The depreciation category and percentage rate applied for book (accounting) depreciation of an asset.
- **Stock Depreciation Category / Stock Depreciation Rate %**: The depreciation category and percentage rate applied for stock depreciation of an asset.
- **Guideline Limit**: A non-negative numeric limit captured on an asset record per the legacy screen.
- **Active Indicator / Active flag / Is Active**: A boolean indicating whether an asset record or code master record is active.
- **Class-Code Filter Panel**: The search panel on the asset create flow that filters class codes by Code and presents a class-code checklist for selection.
- **System Administrator**: An authenticated user whose JWT token carries the SystemAdmin role claim, authorized to maintain Asset Master data.
- **DataGrid**: The Material UI `@mui/x-data-grid` component used to render tabular data with sorting and pagination.
- **PaginatedResponse<T>**: The platform contract carrying page items, total count, current page number, and applied page size.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **Asset_Master_Service**: The backend SystemAdmin service/API (hosted in `Finnova.SystemAdminService`) responsible for asset classification code and asset record management and retrieval. The frontend consumes this through a TypeScript service abstraction of the same conceptual name.
- **Asset_Master_UI**: The React-based Asset Master screen (and its child components) implemented in the Finnova-UI repository.

## Requirements

### Requirement 1: Asset Classification Code Masters — Data Model and Taxonomy

**User Story:** As a System Administrator, I want asset classification maintained as four independent code masters (Class, Make, Type, Model), so that assets can be consistently classified from a standardized vocabulary.

#### Acceptance Criteria

1. THE Asset_Master_Service SHALL model four distinct code masters — Class Code, Make Code, Type Code, and Model Code — each persisted as its own entity with a system-generated identifier, a Code, a Description, and an Is Active flag.
2. THE Asset_Master_Service SHALL treat the Code of a code master record as unique within that code master only, comparing codes case-insensitively after trimming leading and trailing whitespace.
3. IF a create or update request would result in a Code that is not unique within its own code master (per case-insensitive, trimmed comparison), THEN THE Asset_Master_Service SHALL reject the request with a validation error indicating a duplicate Code and SHALL preserve any existing record unchanged.
4. THE Asset_Master_Service SHALL persist each code master through EF Core on SQL Server using a generic `IRepository<T>` plus an `IEntityTypeConfiguration<T>` configuration, with each code master exposed as a `DbSet` on `FinnovaDbContext`.
5. WHERE an asset record references an Asset Category, THE Asset_Master_Service SHALL resolve that Asset Category to an existing Class Code.
6. WHERE an asset record references an Asset Type, THE Asset_Master_Service SHALL resolve that Asset Type to an existing Type Code.
   - **Assumption:** Make Code and Model Code are maintained as code masters but their mandatory linkage to an asset record is derived from the legacy screen and flagged for confirmation.

### Requirement 2: Code Master CRUD, List, Search, and Pagination (Backend)

**User Story:** As a System Administrator, I want to create, update, delete, list, search, and paginate code records for each code master through the service API, so that the classification vocabulary can be maintained programmatically and by the UI.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request for a code master with a Code and a Description, THE Asset_Master_Service SHALL persist a new record and return the created record including its system-generated identifier and the resolved Is Active value.
2. WHERE Is Active is not provided on a code master create request, THE Asset_Master_Service SHALL default Is Active to true.
3. IF a code master create or update request omits the Code or the Description, or provides either as an empty or whitespace-only value, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any change.
4. IF a code master create or update request provides a Code exceeding 20 characters or a Description exceeding 100 characters, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any change.
   - **Assumption:** Length bounds (Code 20, Description 100) are not specified in the ticket; these values are proposed for confirmation.
5. WHEN a System Administrator submits an update to an existing code master record whose submitted values pass validation, THE Asset_Master_Service SHALL persist the change and return the updated record reflecting the submitted values.
6. IF a System Administrator attempts to update a code master record whose identifier does not exist, THEN THE Asset_Master_Service SHALL return a not-found error and SHALL make no change.
7. WHEN a System Administrator requests code master records with a non-empty search term, THE Asset_Master_Service SHALL return only the records whose Code or Description contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
8. WHEN a System Administrator requests code master records with an empty or whitespace-only search term or no search term, THE Asset_Master_Service SHALL return the records for that code master subject to the applied pagination parameters.
9. WHEN a System Administrator requests code master records, THE Asset_Master_Service SHALL return the results as a `PaginatedResponse<T>` including the page items, total count, current page number, and applied page size, applying a default page number of 1 and a default page size of 20 when unspecified.
10. IF a System Administrator requests a page number less than 1, or a page size less than 1 or greater than 100, THEN THE Asset_Master_Service SHALL reject the request with a validation error indicating the out-of-range pagination parameter and SHALL return no records.
11. WHEN returning code master records, THE Asset_Master_Service SHALL order the records by Code ascending, and for records sharing the same Code SHALL apply a deterministic secondary ordering by identifier ascending.

### Requirement 3: Asset Record Data Model and Asset Code Auto-Generation (Backend)

**User Story:** As a System Administrator, I want asset records persisted with their classification and depreciation attributes and a server-generated Asset Code, so that each asset is uniquely and completely identified.

#### Acceptance Criteria

1. THE Asset_Master_Service SHALL persist an asset record with a system-generated identifier, an Asset Code, an Asset Code Description, an Asset Category reference, an Asset Type reference, a Book Depreciation Category, a Book Depreciation Rate %, a Stock Depreciation Category, a Stock Depreciation Rate %, a Guideline Limit, and an Is Active flag, through EF Core on SQL Server exposed as a `DbSet` on `FinnovaDbContext`.
2. WHEN the Asset_Master_Service creates an asset record, THE Asset_Master_Service SHALL generate the Asset Code server-side and SHALL ignore any Asset Code value supplied by the caller.
   - **Assumption:** The Asset Code is generated on save using a deterministic scheme (for example a category-derived prefix plus a zero-padded incrementing sequence); the exact format is flagged for confirmation.
3. THE Asset_Master_Service SHALL generate each Asset Code so that it is unique across all asset records.
4. WHEN the Asset_Master_Service generates an Asset Code, THE Asset_Master_Service SHALL return the generated Asset Code in the created asset record response.
5. THE Asset_Master_Service SHALL treat the generated Asset Code as read-only after creation and SHALL NOT change the Asset Code of an existing asset record on update.
6. WHEN two asset-create operations are processed, THE Asset_Master_Service SHALL assign each a distinct Asset Code such that no two asset records share an Asset Code.

### Requirement 4: Create and Update Asset Record with Depreciation Attributes and Validation (Backend)

**User Story:** As a System Administrator, I want to create and update asset records with validated category, description, depreciation, guideline-limit, and active attributes, so that asset data is complete and consistent.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing an Asset Category, an Asset Code Description, book and stock depreciation categories and rates, a Guideline Limit, and an Is Active value, THE Asset_Master_Service SHALL persist a new asset record with a server-generated Asset Code and return the created record including its system-generated identifier.
2. WHERE Is Active is not provided on an asset create request, THE Asset_Master_Service SHALL default Is Active to true.
3. IF an asset create or update request omits the Asset Category or the Asset Code Description, or provides either as an empty or whitespace-only value, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any change.
4. IF an asset create or update request references an Asset Category that does not resolve to an existing Class Code, or an Asset Type that does not resolve to an existing Type Code, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying the unresolved reference and SHALL NOT persist any change.
5. IF an asset create or update request provides a Book Depreciation Rate % or a Stock Depreciation Rate % that is not a number within the inclusive range 0 to 100, or that has more than 2 decimal places, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying the offending rate field and SHALL NOT persist any change.
6. IF an asset create or update request provides a Guideline Limit that is not a number greater than or equal to 0, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying the Guideline Limit field and SHALL NOT persist any change.
7. IF an asset create or update request provides an Asset Code Description exceeding 200 characters, THEN THE Asset_Master_Service SHALL reject the request with a validation error identifying the Asset Code Description and SHALL NOT persist any change.
   - **Assumption:** The Asset Code Description length bound (200) is proposed for confirmation.
8. WHEN a System Administrator submits an update to an existing asset record whose submitted values pass validation, THE Asset_Master_Service SHALL persist the change, SHALL preserve the existing Asset Code, and SHALL return the updated record reflecting the submitted values.
9. IF a System Administrator attempts to update an asset record whose identifier does not exist, THEN THE Asset_Master_Service SHALL return a not-found error and SHALL make no change.
10. WHEN the Asset_Master_Service validates an asset create or update request, THE Asset_Master_Service SHALL perform the validation through a FluentValidation validator executed by the `ValidationBehavior` MediatR pipeline, and SHALL map the persisted record to its response through a static `.ToResponse()` mapper.

### Requirement 5: List, Search, Paginate, and Get-by-Id Asset Records (Backend)

**User Story:** As a System Administrator, I want to list, search, paginate, and retrieve individual asset records, so that I can review and locate assets efficiently.

#### Acceptance Criteria

1. WHEN a System Administrator requests asset records, THE Asset_Master_Service SHALL return the results as a `PaginatedResponse<T>` including the page items, total count, current page number, and applied page size.
2. WHEN a System Administrator requests asset records with a non-empty search term, THE Asset_Master_Service SHALL return only the records whose Asset Code or Asset Code Description contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
3. WHEN a System Administrator requests asset records with an empty or whitespace-only search term or no search term, THE Asset_Master_Service SHALL return the asset records subject to the applied pagination parameters.
4. WHEN a System Administrator requests asset records without specifying page number or page size, THE Asset_Master_Service SHALL apply a default page number of 1 and a default page size of 20.
5. IF a System Administrator requests a page number less than 1, or a page size less than 1 or greater than 100, THEN THE Asset_Master_Service SHALL reject the request with a validation error indicating the out-of-range pagination parameter and SHALL return no records.
6. WHEN a System Administrator requests a page number beyond the last available page, THE Asset_Master_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
7. WHEN returning asset records, THE Asset_Master_Service SHALL order the records by Asset Code ascending, and for records sharing the same Asset Code SHALL apply a deterministic secondary ordering by identifier ascending.
8. WHEN a System Administrator requests a single asset record by its identifier, THE Asset_Master_Service SHALL return that asset record including all of its attributes.
9. IF a System Administrator requests a single asset record by an identifier that does not exist, THEN THE Asset_Master_Service SHALL return a not-found error and SHALL return no asset record.
10. IF a search request matches no asset records, THEN THE Asset_Master_Service SHALL return an empty item collection with a total count of 0 and a success status.

### Requirement 6: Delete and Deactivate Rules (Backend)

**User Story:** As a System Administrator, I want to retire code master records and asset records in a controlled way, so that inactive records are excluded from downstream use without losing historical references.

#### Acceptance Criteria

1. WHEN a System Administrator deactivates a code master record or an asset record by setting its Is Active flag to false, THE Asset_Master_Service SHALL persist the change and SHALL retain the record so that it remains retrievable by get-by-id and by unfiltered list queries.
2. WHILE a code master record or asset record has its Is Active flag set to false, THE Asset_Master_Service SHALL continue to resolve existing references to that record so that previously created asset records remain valid.
   - **Assumption:** Deactivation (Is Active = false) is the primary retirement mechanism rather than hard deletion, preserving historical references. Whether an additional hard-delete action is required is flagged for confirmation.
3. IF a System Administrator attempts to deactivate or delete a code master record whose identifier does not exist, or an asset record whose identifier does not exist, THEN THE Asset_Master_Service SHALL return a not-found error and SHALL make no change.
4. IF a hard-delete of a code master record is requested while one or more asset records reference that code master record, THEN THE Asset_Master_Service SHALL reject the deletion with a validation error indicating the code is in use and SHALL preserve the record unchanged.

### Requirement 7: Authentication and Authorization Enforcement (Backend + Gateway)

**User Story:** As a security stakeholder, I want all Asset Master operations restricted to System Administrators and reachable only through the gateway, so that only authorized personnel can view or change asset classification and asset records.

#### Acceptance Criteria

1. IF a request to create, update, deactivate, list, search, or read an Asset Master code master record or asset record is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Asset_Master_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored record, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts any Asset Master operation, THEN THE Asset_Master_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored record, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes any Asset Master operation, THE Asset_Master_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 6.
4. WHEN the Asset_Master_Service evaluates authentication and authorization for any request, THE Asset_Master_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. THE Finnova.ApiGateway SHALL route requests for the Asset Master endpoints under `/api/systemadmin/**` to the `systemadmin-cluster` and SHALL forward the `Authorization` header unchanged.
6. THE Finnova.ApiGateway SHALL provide a UI-prefix alias route (mirroring the Lookup pattern `/api/ua/api/...`) that forwards the UI-composed Asset Master path to the `systemadmin-cluster` and rewrites it to the service path.
7. WHEN the Asset_Master_Service raises a domain or validation exception, THE Asset_Master_Service SHALL translate it through the host `ExceptionHandlingMiddleware` into the corresponding HTTP status code (for example 400 for validation, 404 for not-found, 409 for conflict) with a message body.
8. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the Asset_Master_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) established by FINNOVA-8. Establishing that scheme is a prerequisite already satisfied by the Lookup Master feature.

### Requirement 8: Asset Master Screen Shell and Area / Sub-Tab Navigation (Frontend)

**User Story:** As a System Administrator, I want the Asset Master screen to present the Asset Definition and Asset Mapping areas with clear navigation, so that I can move between asset classification codes and asset records.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Asset_Master_UI, THE Asset_Master_UI SHALL present exactly two top-level areas labelled "Asset Definition" and "Asset Mapping", with "Asset Definition" in the selected state and "Asset Mapping" in the unselected state.
2. WHEN a System Administrator selects the "Asset Mapping" area, THE Asset_Master_UI SHALL render the Asset Mapping grid area, remove the Asset Definition area from the rendered view, mark "Asset Mapping" selected and "Asset Definition" unselected, without a full-page reload.
3. WHEN a System Administrator selects the "Asset Definition" area, THE Asset_Master_UI SHALL render exactly four code sub-tabs labelled "Class Codes", "Make Codes", "Type Codes", and "Model Codes", with "Class Codes" in the selected state and the other three in the unselected state, without a full-page reload.
4. THE Asset_Master_UI SHALL be reachable from the application navigation (`Layout.tsx`) and registered as a single route in the application router (`App.tsx`).
   - **Assumption:** The route path and navigation label follow the existing master-screen convention (for example `/asset-master`); the exact path is confirmed during design.
5. WHEN the Asset_Master_UI issues any request through the real Asset_Master_Service, THE Asset_Master_UI SHALL include the JWT Bearer token in the Authorization header via the shared axios instance.
6. IF a System Administrator switches areas or sub-tabs while a request for the previously selected view is still in progress, THEN THE Asset_Master_UI SHALL discard the in-flight response and display only data belonging to the currently selected view.

### Requirement 9: Code Master Grid, Search, Pagination, and Create/Edit/Delete Dialogs (Frontend)

**User Story:** As a System Administrator, I want each Asset Definition sub-tab to show its code records in a searchable, paginated grid with dialog-based create, edit, and delete, so that I can maintain the classification vocabulary without the legacy row-icon workflow.

#### Acceptance Criteria

1. WHEN a System Administrator selects one of the Asset Definition sub-tabs (Class Codes, Make Codes, Type Codes, or Model Codes), THE Asset_Master_UI SHALL request that sub-tab's code records from the Asset_Master_Service and display them in a DataGrid with columns for Code, Description, and Active.
2. WHEN a System Administrator enters a search term in a code master's search control, THE Asset_Master_UI SHALL request the matching code records whose Code or Description contains the search term as a case-insensitive substring and display only those records.
3. WHEN a System Administrator clears the search term in a code master, THE Asset_Master_UI SHALL request and display the code records for that sub-tab without a search filter.
4. WHEN a System Administrator changes the page or page size of a code master grid, THE Asset_Master_UI SHALL display the corresponding page of code records for that sub-tab.
   - **Assumption:** Default page size follows the existing master-screen convention (20 rows); the exact default is confirmed during design.
5. WHEN a System Administrator activates the create action on a sub-tab, THE Asset_Master_UI SHALL open a create dialog containing a Code field, a Description field, and an Active indicator for that code master.
6. WHEN a System Administrator activates the edit action on a code master row, THE Asset_Master_UI SHALL open an edit dialog pre-filled with that record's Code, Description, and Active values.
7. WHEN a System Administrator submits a valid create or edit dialog for a code master, THE Asset_Master_UI SHALL send the corresponding create or update request through the Asset_Master_Service and, on success, close the dialog and refresh the grid to reflect the change.
8. IF a System Administrator submits a code master create or edit dialog with an empty Code or an empty Description, THEN THE Asset_Master_UI SHALL block submission and display a field-level validation message identifying the empty field.
9. IF the Asset_Master_Service rejects a code master create or edit request with a duplicate-code conflict, THEN THE Asset_Master_UI SHALL keep the dialog open, preserve the entered values, and display an error indication that the Code already exists.
10. WHEN a System Administrator activates the delete/deactivate action on a code master row and confirms, THE Asset_Master_UI SHALL send the corresponding request through the Asset_Master_Service and, on success, refresh the grid to reflect the record's removal or inactive state.
11. WHEN a code master request returns no code records, THE Asset_Master_UI SHALL display an empty-state indication within the grid rather than a blank area.
12. WHEN a System Administrator cancels a code master create or edit dialog, THE Asset_Master_UI SHALL close the dialog without sending a request and SHALL leave the grid unchanged.

### Requirement 10: Asset Mapping Grid, Search, Show-All, Pagination, and View (Frontend)

**User Story:** As a System Administrator, I want the Asset Mapping area to list asset records in a searchable, paginated grid with a view action, so that I can review and locate assets.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Asset Mapping area, THE Asset_Master_UI SHALL request asset records from the Asset_Master_Service and display them in a DataGrid with columns for Asset Code, Asset Code Description, Asset Type, and Active.
2. THE Asset_Master_UI SHALL render the Active column as a clear active/inactive indicator rather than a raw boolean value.
3. WHEN a System Administrator activates the show-all action, THE Asset_Master_UI SHALL request and display asset records without a search filter applied.
4. WHEN a System Administrator enters a search term in the Asset Mapping search control, THE Asset_Master_UI SHALL request the matching asset records whose Asset Code or Asset Code Description contains the search term as a case-insensitive substring and display only those records.
5. WHEN a System Administrator changes the page or page size of the Asset Mapping grid, THE Asset_Master_UI SHALL display the corresponding page of asset records.
6. WHEN an Asset Mapping request returns no asset records, THE Asset_Master_UI SHALL display an empty-state indication within the grid rather than a blank area.
7. WHEN a System Administrator activates the query/view action on an Asset Mapping row, THE Asset_Master_UI SHALL request that asset record by its identifier through the Asset_Master_Service and open the asset detail view populated with that record's values.

### Requirement 11: Create and Edit Asset Dialog (Frontend)

**User Story:** As a System Administrator, I want to create and edit assets through a dialog capturing the read-only auto-generated Asset Code, category selection, depreciation attributes, guideline limit, and active indicator, so that asset records are complete and accurate.

#### Acceptance Criteria

1. WHEN a System Administrator activates the create action in the Asset Mapping area, THE Asset_Master_UI SHALL open an asset create dialog containing Asset Category (selection), Asset Code (read-only), Asset Code Description, Book Depreciation Category, Book Depreciation Rate %, Stock Depreciation Category, Stock Depreciation Rate %, Guideline Limit, and an Active indicator defaulting to active.
2. THE Asset_Master_UI SHALL render the Asset Code field as read-only, populated from the auto-generated value returned by the Asset_Master_Service, and SHALL NOT allow the System Administrator to edit it.
   - **Assumption:** The Asset Code is generated by the service (the mock generates a representative value); whether it is displayed before save or only after a successful create is confirmed during design.
3. WHEN a System Administrator selects an Asset Category on the asset dialog, THE Asset_Master_UI SHALL populate the Asset Category selection options from the Class Codes maintained in Asset Definition.
4. IF a System Administrator submits the asset create or edit dialog with an empty Asset Category or an empty Asset Code Description, THEN THE Asset_Master_UI SHALL block submission and display a field-level validation message identifying the empty field.
5. IF a System Administrator enters a Book Depreciation Rate % or Stock Depreciation Rate % that is not a number within the inclusive range 0 to 100 with at most 2 decimal places, THEN THE Asset_Master_UI SHALL block submission and display a field-level validation message identifying the offending rate field.
6. IF a System Administrator enters a Guideline Limit that is not a number greater than or equal to 0, THEN THE Asset_Master_UI SHALL block submission and display a field-level validation message identifying the Guideline Limit field.
7. WHEN a System Administrator submits a valid asset create dialog, THE Asset_Master_UI SHALL send a create request through the Asset_Master_Service and, on success, close the dialog and refresh the Asset Mapping grid to include the new asset record.
8. WHEN a System Administrator activates the modify/edit action on an asset record, THE Asset_Master_UI SHALL open an asset edit dialog pre-filled with that record's values, with the Asset Code shown read-only, and on valid submission SHALL send an update request and refresh the grid to reflect the change.
9. WHEN a System Administrator sets the Active indicator and submits the asset dialog, THE Asset_Master_UI SHALL send the chosen Active value through the Asset_Master_Service and reflect it in the Asset Mapping grid Active column on success.
10. IF the Asset_Master_Service rejects an asset create or edit request, THEN THE Asset_Master_UI SHALL keep the dialog open, preserve the entered values, and display an error indication describing the failure.
11. WHEN a System Administrator cancels the asset create or edit dialog, THE Asset_Master_UI SHALL close the dialog without sending a request and SHALL leave the Asset Mapping grid unchanged.

### Requirement 12: Class-Code Filter and Selection Panel on Asset Create (Frontend)

**User Story:** As a System Administrator, I want a class-code filter and selection panel on the asset create flow, so that I can search class codes by code and select the relevant class code for the asset's classification.

#### Acceptance Criteria

1. WHERE the asset create flow is displayed, THE Asset_Master_UI SHALL present a Class-Code Filter Panel containing a Code search field and a class-code checklist populated from the Class Codes.
2. WHEN a System Administrator enters a value in the Class-Code Filter Panel Code search field, THE Asset_Master_UI SHALL display only the class codes whose Code contains the entered value as a case-insensitive substring.
3. WHEN a System Administrator selects a class code in the class-code checklist, THE Asset_Master_UI SHALL retain the selected class code as the Asset Category of the asset being created.
4. WHEN the Class-Code Filter Panel search field is cleared, THE Asset_Master_UI SHALL display the full class-code checklist while preserving any selection already made.
5. WHEN a Class-Code Filter Panel search returns no class codes, THE Asset_Master_UI SHALL display an empty-state indication within the checklist rather than a blank panel.
   - **Assumption:** A single class code maps to an asset's Asset Category; whether multiple class codes may be selected per asset is derived from the legacy screen and flagged for confirmation.

### Requirement 13: Mock-First Service Layer, Build Toggle, and Shared Axios (Frontend)

**User Story:** As a developer, I want the Asset Master screen to run against a mock service by default and switch to the real API via a build toggle, so that the UI can be developed and demonstrated without backend availability.

#### Acceptance Criteria

1. THE Asset_Master_Service abstraction SHALL be defined as a TypeScript interface in `src/services/interfaces` exposing operations to list, search, create, update, and delete/deactivate code records for each code master, and to list, search, create, update, deactivate, and get-by-id asset records.
2. THE Asset_Master_Service SHALL have a mock implementation in `src/services/mock` that serves in-memory sample data covering the four code masters and asset records and that satisfies the list, search, create, update, pagination, and auto-generated Asset Code behaviors described in Requirements 9 through 12.
   - **Assumption:** Mock sample data is English-only and representative of India-market asset classifications; its exact shape mirrors the backend contracts and is confirmed during design.
3. THE Asset_Master_Service SHALL have a real implementation in `src/services/real` that performs its requests through the shared axios instance in `src/services/api.ts`.
4. WHERE the `VITE_USE_MOCK_API` environment flag selects the mock implementation, THE Asset_Master_UI SHALL resolve the Asset_Master_Service to the mock implementation through the service toggle module.
5. WHERE the `VITE_USE_MOCK_API` environment flag selects the real implementation, THE Asset_Master_UI SHALL resolve the Asset_Master_Service to the real axios implementation through the service toggle module.
6. THE Asset_Master_UI SHALL consume the Asset_Master_Service only through the toggle module and the shared axios instance, and SHALL NOT create an ad-hoc axios client.

### Requirement 14: Error, Loading, and Timeout Handling (Frontend)

**User Story:** As a System Administrator, I want the Asset Master screen to clearly communicate loading, success, and error states, so that I always know the outcome of my actions.

#### Acceptance Criteria

1. WHILE any Asset_Master_Service request is in progress, THE Asset_Master_UI SHALL present a loading indication for the affected grid or dialog in place of stale content.
2. WHEN an Asset_Master_Service request completes successfully, THE Asset_Master_UI SHALL update the affected grid or dialog to reflect the result and clear the loading indication.
3. IF an Asset_Master_Service request fails with a 401 or 403 response, THEN THE Asset_Master_UI SHALL rely on the shared axios handling to surface the authorization toast and SHALL leave the affected grid or dialog content unchanged.
4. IF an Asset_Master_Service request fails with a 409 conflict (for example a duplicate Code), THEN THE Asset_Master_UI SHALL display the conflict message to the System Administrator and SHALL preserve the entered values.
5. IF an Asset_Master_Service request does not complete within the shared axios timeout, THEN THE Asset_Master_UI SHALL stop waiting and display a retry-able error indication to the System Administrator.
6. IF a create or update request fails, THEN THE Asset_Master_UI SHALL leave the underlying grid data unchanged until a subsequent request succeeds.
