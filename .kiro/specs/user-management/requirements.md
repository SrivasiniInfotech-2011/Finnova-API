# Requirements Document

## Introduction

The User Management feature (Module: SystemAdmin, Master: User Management, Functional Spec section 9) provides a centralized administrative capability for adding and removing employees (users) in the Finnova platform, and for defining their roles, permissions, reporting hierarchy, and access rights. User Management is the single place where a System Administrator provisions who can log in, what programs they can reach, which access rights (Add / Modify / Query / Delete) they hold per program, and which branches and lines of business they operate against.

The master supports three creation configurations selected from a **User Configuration** list of values: (a) **User** — an individual employee account; (b) **User Group** — a named collection of existing active users; and (c) **Functional Group** — a named grouping of program functions that can later be assigned to a User or a User Group. System behavior varies by the configuration selected: when User Group or Functional Group is chosen, the individual user detail fields are not editable.

The master operates in three modes: **Create Mode** (provision a new user, user group, or functional group), **Modify Mode** (populate and edit an existing record keyed by its generated code), and **Query Mode** (view-only, Save disabled). Each record carries a System Administrator-defined set of access rights expressed through a Role Center, a program list, a role-code access matrix, and a branch/location tree. Every create and modify operation records the acting user's identifier and the transaction date as an audit trail.

The backend follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + PostgreSQL, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy (`/api/systemadmin/**`). The user-facing screens are implemented in the separate Finnova-UI repository (React 18 + TypeScript + Vite + Material UI, React hooks + Context for state, the shared axios instance in `src/services/api.ts`, and the service interface / mock / real / toggle pattern driven by `VITE_USE_MOCK_API`). This spec covers both backend and frontend.

This document captures the requirements derived from functional spec section 9 (including the data-items table in 9.5 and data validations in 9.6, which are authoritative for field sizes and mandatory flags) and the four reference screens (User Details tab, User Group popup, Access tab, Functional list details). Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** User Management is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (which already exposes Lookup Master, Nationality Master, and Organization Hierarchy Master), reached through the gateway prefix `/api/systemadmin/**`. This mirrors the Organization Hierarchy Master decision and is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy (established by FINNOVA-8 and reused by subsequent SystemAdmin masters). This feature reuses that scheme and policy. Token issuance (login) ownership is external to this feature. Where platform-wide JWT wiring is incomplete, establishing it remains a prerequisite dependency resolved in design.
- **Assumption (Prerequisite master data):** Branch Master, Line of Business, and Role Code definitions must exist before a user can be provisioned. User Management consumes these as references and does not create them. Designation, Department, and User Type values are consumed from the Lookup Master. If any prerequisite master is missing, provisioning that depends on it cannot complete.
- **Assumption (Password policy dependency):** User passwords must comply with the System Admin **GPS password policy**. The policy definition and evaluation are owned outside this feature; User Management invokes/enforces the configured policy rather than defining it. The concrete policy parameters are a design dependency.
- **Assumption (Date format):** Date of Joining uses the GPS display format `DD/MM/YYYY`. Internally dates are stored as a date value; the display format is a UI concern.
- **Assumption (Audit trail scope):** This feature records, per user record, the identifier of the creating/modifying System Administrator and the transaction date, mirroring the audit approach of prior SystemAdmin masters. Whether this should be promoted to a shared, platform-wide audit capability is flagged for confirmation in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each display value is a single English field (for example `Value`, `Name`); there are no bilingual fields, no Arabic content, and no right-to-left rendering. Sample location-tree names in requirements use India-appropriate values (for example, Locations → State/Region → Branch, e.g. MUMBAI, HEAD OFFICE, FORT BRANCH, MAHARASHTRA).

## Glossary

- **User Management Master**: The centralized administrative capability for provisioning users, user groups, and functional groups, and for assigning their roles, access rights, branches, and lines of business.
- **User Configuration**: The selection that determines the creation mode of a record. Exactly one of three values: `User`, `User Group`, or `Functional Group`.
- **User**: An individual employee account provisioned in the master, identified by a system-generated User Code and associated with a Name, Designation, Department, and credentials.
- **User Group**: A named collection of existing active Users, identified by a system-generated User Group Code, used to assign access to many users at once.
- **Functional Group**: A named grouping of program functions, identified by a system-generated Functional Group Code generated from a Role Center Name, used to club functionality for later assignment to a User or a User Group.
- **User Code**: A system-generated, uppercase, alphanumeric identifier for an individual user, unique across the company, 4 to 6 characters, whose first character is an alphabet, derived from the user's Name and a number, read-only.
- **User Group Code**: A system-generated, uppercase, alphanumeric identifier for a user group, unique across the company, 4 to 6 characters, first character an alphabet, derived from the group name and a number, read-only.
- **Functional Group Code**: A system-generated, uppercase, alphanumeric identifier for a functional group, unique across the company, 4 to 6 characters, first character an alphabet, derived from the Role Center Name, read-only.
- **User Name**: The individual user's display name; free text up to 50 characters, spaces allowed, mandatory.
- **User Group Name**: The user group's display name; free text up to 30 characters, spaces allowed, mandatory.
- **Password**: The user's secret credential; alphanumeric with special characters, compliant with the GPS password policy, masked on entry.
- **Reset Password**: A facility, available only in Modify Mode, to set a new Password for an existing user.
- **Date of Joining (DOJ)**: The user's joining date, displayed as `DD/MM/YYYY`, optional, defaulting to the system date and modifiable.
- **Designation**: The user's job title; a Lookup Master value, up to 40 characters, mandatory.
- **Department**: The user's department; a Lookup Master value, up to 40 characters, mandatory.
- **Mobile Number**: The user's mobile number; numeric, up to 12 digits, optional, no special characters or spaces.
- **Email id**: The user's email address; up to 60 characters, must contain exactly one `@` and at least one `.`, must not start or end with `@`, `.`, or a special character, optional.
- **User Type**: A classification of the user; a Lookup Master value, one of `Corporate` or `Branch`, mandatory.
- **Active Indicator**: A boolean flag indicating whether a record is active; defaults to active (ticked). An unticked indicator marks the record inactive.
- **Line of Business (LOB)**: A business line the user is granted access to; only one LOB is selectable at a time per selection and must be linked to defined Role Codes.
- **Role Center**: A named grouping of programs (for example, `System Admin`, `Origination`) whose selection drives the programs presented for access assignment.
- **Role Center Name**: The display name of a Role Center; a mandatory Lookup/list value used to build Role Codes.
- **Program**: A functional screen or capability to which access can be granted (for example, Company Master, Asset Master, Entity Master).
- **Role Code**: A code formed by concatenating the Role Center Name with the Program Name (for example, `SSCMP`, `OOASM`, `OOETM`), identifying a program-within-role-center access line.
- **Access Rights**: The combination of `Add`, `Modify`, `Query`, and `Delete` permissions granted for a given Role Code / Program; `Select All` grants all four for that row.
- **Branch**: An organizational branch the user is associated with, selected from a hierarchical Location Tree (Locations → State/Region → Branch). An `ALL` option applies access to all branches.
- **Location Tree**: The hierarchical presentation of Locations, states/regions, and branches from which branch associations are chosen.
- **Copy Profile**: A facility (available in Create Mode) to copy an existing active user's branches and program access for a selected LOB into the record being created.
- **GPS Password Policy**: The System Admin-configured password complexity and validity rules that a Password must satisfy.
- **Create Mode / Modify Mode / Query Mode**: The three operating modes of the master — provisioning a new record, editing an existing record, and viewing an existing record read-only, respectively.
- **Audit Entry**: The record of the acting System Administrator's identifier and the transaction date captured when a user record is created or modified.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim, authorized to manage user records.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **User_Service**: The backend service/API responsible for user, user group, and functional group management and retrieval.
- **User_Admin_UI**: The React-based User Management administrative screen used by System Administrators (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: User Configuration Selection and Prerequisites

**User Story:** As a System Administrator, I want to choose one of three user configurations and have the system behave accordingly, so that I can provision an individual user, a user group, or a functional group from one master.

#### Acceptance Criteria

1. THE User_Service SHALL associate each provisioned record with exactly one User Configuration value from the set `User`, `User Group`, and `Functional Group`.
2. WHEN a System Administrator selects the User Configuration value `User`, THE User_Admin_UI SHALL enable the individual user detail fields (User Code, User Name, Password, Date of Joining, Designation, Department, Mobile Number, Email id, User Type, Active Indicator).
3. WHILE the selected User Configuration value is `User Group`, THE User_Admin_UI SHALL render the individual user detail fields (User Code, User Name, Password, Date of Joining, Designation, Department, Mobile Number, Email id) as not editable.
4. WHILE the selected User Configuration value is `Functional Group`, THE User_Admin_UI SHALL render the individual user detail fields as not editable.
5. IF a System Administrator submits a create request without a User Configuration value, THEN THE User_Service SHALL reject the request with a validation error identifying the User Configuration as required and SHALL NOT persist any record.
6. IF a System Administrator attempts to provision a record whose required references (Branch Master, Line of Business, or Role Code) are not defined, THEN THE User_Service SHALL reject the request with a validation error indicating the missing prerequisite and SHALL NOT persist any record.

### Requirement 2: System-Generated Codes with Uniqueness

**User Story:** As a System Administrator, I want the system to generate the User Code, User Group Code, and Functional Group Code automatically and keep them unique, so that each record is reliably and consistently identified.

#### Acceptance Criteria

1. WHEN a System Administrator enters a User Name under the `User` configuration, THE User_Service SHALL generate a User Code derived from the User Name and a number, consisting only of alphabetic and numeric characters, with no special characters and no spaces, 4 to 6 characters in length, whose first character is an alphabet, rendered in uppercase.
2. WHEN a System Administrator enters a User Group Name under the `User Group` configuration, THE User_Service SHALL generate a User Group Code derived from the group name and a number, consisting only of alphabetic and numeric characters, with no special characters and no spaces, 4 to 6 characters in length, whose first character is an alphabet, rendered in uppercase.
3. WHEN a System Administrator selects a Role Center Name under the `Functional Group` configuration, THE User_Service SHALL generate a Functional Group Code derived from the Role Center Name, consisting only of alphabetic and numeric characters, with no special characters and no spaces, 4 to 6 characters in length, whose first character is an alphabet, rendered in uppercase.
4. THE User_Service SHALL treat User Code, User Group Code, and Functional Group Code each as unique across the company, such that no two records share the same generated code.
5. IF generating a code produces a value that matches an existing code of the same kind, THEN THE User_Service SHALL derive an alternative unique code of the same kind that satisfies the format rules (4 to 6 characters, alphanumeric, first character alphabetic, uppercase) before persisting the record.
   - **Assumption:** Collisions are resolved by appending or varying the numeric portion of the generated code; the exact derivation algorithm is a design decision.
6. THE User_Admin_UI SHALL present the generated User Code, User Group Code, and Functional Group Code as read-only values.
7. WHEN the User Configuration value is `User`, THE User_Service SHALL generate the User Code only after the User Name has been entered, and SHALL NOT present a User Code before a User Name is provided.
8. WHEN the User Configuration value is `Functional Group`, THE User_Service SHALL generate the Functional Group Code only after a Role Center Name has been selected.

### Requirement 3: Create Individual User — User Details

**User Story:** As a System Administrator, I want to capture an individual user's details with validated fields, so that a valid, uniquely identified user account is provisioned.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request under the `User` configuration with a User Name, Password, Designation, Department, and User Type, THE User_Service SHALL persist a new user record, assign the generated User Code, and return the created record including the User Code, resolved Active Indicator, and resolved Date of Joining.
2. IF a create request omits the User Name or provides it as empty or whitespace-only, THEN THE User_Service SHALL reject the request with the message "Please enter the User Name" and SHALL NOT persist any record.
3. IF the User Name exceeds 50 characters, THEN THE User_Service SHALL reject the request with a validation error identifying the User Name and SHALL NOT persist any record.
4. IF a create request omits the Password, THEN THE User_Service SHALL reject the request with the message "Please enter the User Password" and SHALL NOT persist any record.
5. IF the submitted Password does not comply with the GPS password policy, THEN THE User_Service SHALL reject the request with the message "Please enter a valid Password" and SHALL NOT persist any record.
6. WHEN the User_Admin_UI accepts a Password, THE User_Admin_UI SHALL mask the entry, displaying masking characters in place of the typed characters.
7. WHERE a Date of Joining is not provided on a create request, THE User_Service SHALL default the Date of Joining to the system date.
8. WHEN a System Administrator provides a Date of Joining, THE User_Service SHALL accept a date rendered in the `DD/MM/YYYY` format and persist it as the user's Date of Joining.
9. IF a create request omits the Designation, THEN THE User_Service SHALL reject the request with the message "Please select the Designation" and SHALL NOT persist any record.
10. IF a create request omits the Department, THEN THE User_Service SHALL reject the request with the message "Please select the Department" and SHALL NOT persist any record.
11. IF a create request omits the User Type, THEN THE User_Service SHALL reject the request with the message "Please select the User Type" and SHALL NOT persist any record.
12. THE User_Service SHALL accept the User Type only as one of the values `Corporate` or `Branch`, and SHALL reject any other value with a validation error identifying the User Type.
13. IF the submitted Designation exceeds 40 characters or is not a value defined in the Lookup Master, THEN THE User_Service SHALL reject the request with a validation error identifying the Designation and SHALL NOT persist any record.
14. IF the submitted Department exceeds 40 characters or is not a value defined in the Lookup Master, THEN THE User_Service SHALL reject the request with a validation error identifying the Department and SHALL NOT persist any record.
15. WHERE an Active Indicator value is not provided on a create request, THE User_Service SHALL default the Active Indicator to active.

### Requirement 4: Validate Mobile Number and Email id

**User Story:** As a System Administrator, I want mobile number and email id to be validated to defined formats, so that contact details are well-formed and consistent.

#### Acceptance Criteria

1. WHERE a Mobile Number is provided, THE User_Service SHALL accept it only when it consists of numeric digits with no special characters and no spaces, up to 12 digits in length.
2. IF a submitted Mobile Number contains any character other than a numeric digit, THEN THE User_Service SHALL reject the request with the message "Special characters are not allowed in this field" and SHALL NOT persist any record.
3. IF a submitted Mobile Number exceeds 12 digits, THEN THE User_Service SHALL reject the request with a validation error identifying the Mobile Number and SHALL NOT persist any record.
4. WHERE an Email id is provided, THE User_Service SHALL accept it only when it is at most 60 characters, contains exactly one `@` character, contains at least one `.` character, and does not begin or end with `@`, `.`, or any special character.
5. IF a submitted Email id does not satisfy the Email id format rules, THEN THE User_Service SHALL reject the request with the message "Please enter a valid Email id" and SHALL NOT persist any record.
6. IF the User_Admin_UI requires an Email id for a given workflow and the field is left blank, THEN THE User_Admin_UI SHALL display the message "Please enter the Email id".
   - **Assumption:** The data-items table marks Email id as optional for storage, while the UI surfaces a blank-field prompt; the prompt applies where a workflow treats Email id as required. Whether Email id is globally optional or conditionally required is flagged for confirmation.

### Requirement 5: Create User Group

**User Story:** As a System Administrator, I want to create a user group and tag existing active users to it, so that access can be assigned to many users at once.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request under the `User Group` configuration with a User Group Name and at least one selected user, THE User_Service SHALL persist a new user group record, assign the generated User Group Code, and return the created record including the User Group Code and the tagged user members.
2. IF a create request under the `User Group` configuration omits the User Group Name or provides it as empty or whitespace-only, THEN THE User_Service SHALL reject the request with the message "Please enter the User Group Name" and SHALL NOT persist any record.
3. IF the User Group Name exceeds 30 characters, THEN THE User_Service SHALL reject the request with a validation error identifying the User Group Name and SHALL NOT persist any record.
4. WHEN a System Administrator opens the User Group facility, THE User_Admin_UI SHALL present a popup through which the System Administrator can create a new group and add or delete members of an existing group.
5. WHEN the User Group member selector is opened, THE User_Admin_UI SHALL present a list of all active users already created, from which users are selected to be tagged to the User Group Code.
6. IF a create request under the `User Group` configuration tags no users, THEN THE User_Service SHALL reject the request with the message "Please enter the User Code & Name" and SHALL NOT persist any record.
7. WHEN a System Administrator tags one or more selected users to a user group, THE User_Service SHALL associate each selected user with the User Group Code so that the group membership is retrievable.
8. IF a create or modify request tags a user that is not an active user, THEN THE User_Service SHALL reject the request with a validation error indicating the user is not active and SHALL NOT persist the membership.

### Requirement 6: Create Functional Group

**User Story:** As a System Administrator, I want to create a functional group that clubs program functions together, so that grouped functionality can be assigned to a user or user group.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request under the `Functional Group` configuration with a selected Role Center Name, THE User_Service SHALL persist a new functional group record, assign the generated Functional Group Code, and map all functions of the associated Program to the Functional Group Code.
2. WHEN a System Administrator opens the Functional Group facility, THE User_Admin_UI SHALL present a popup through which the System Administrator can create a new functional group and add or delete functionality in an existing functional group.
3. WHEN a functional group is created, THE User_Service SHALL make the grouped functionality retrievable as the set of functions clubbed under the Functional Group Code so that it can be assigned to a User or a User Group.
4. WHILE the `Functional Group` configuration is in use for access assignment, THE User_Service SHALL make available in the Role Center Name selection only the functionality clubbed in the entered Functional Group Code.
5. WHEN a System Administrator assigns a functional group, THE User_Service SHALL allow the System Administrator to select either a User Code & Name or a User Group Code as the assignment target.
6. THE User_Service SHALL support assigning one functional group to many users and many functional groups to one user, so that one-to-many and many-to-one combinations of functions and users are representable.
7. IF a create request under the `Functional Group` configuration omits the Role Center Name, THEN THE User_Service SHALL reject the request with a validation error identifying the Role Center Name as required and SHALL NOT persist any record.

### Requirement 7: Access Tab — Line of Business Selection

**User Story:** As a System Administrator, I want to grant a user access to a line of business they are permitted to assign, so that access is scoped to the correct business line.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Access tab, THE User_Admin_UI SHALL list only the active Lines of Business that the logged-in System Administrator has access to.
2. THE User_Admin_UI SHALL allow exactly one Line of Business to be selected at a time for a given selection.
3. THE User_Service SHALL accept a selected Line of Business only when the Line of Business is linked to one or more already-defined Role Codes.
4. IF a System Administrator attempts to save the Access tab without selecting any Line of Business, THEN THE User_Service SHALL reject the request with the message "Please select at least one Line of Business" and SHALL NOT persist the access assignment.
5. IF a System Administrator selects a Line of Business that is not linked to any defined Role Code, THEN THE User_Service SHALL reject the request with a validation error indicating the Line of Business has no linked Role Codes and SHALL NOT persist the access assignment.

### Requirement 8: Access Tab — Role Center, Program List, and Access Rights Matrix

**User Story:** As a System Administrator, I want to pick a role center, see its programs, and grant per-program access rights, so that the user's permissions are precisely defined.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Role Center Name selector, THE User_Admin_UI SHALL present a list of all active Role Centers (for example, `System Admin`, `Origination`).
2. IF a System Administrator attempts to save the Access tab without selecting a Role Center Name, THEN THE User_Service SHALL reject the request with a validation error identifying the Role Center Name as required and SHALL NOT persist the access assignment.
3. WHEN a System Administrator selects a Role Center Name, THE User_Admin_UI SHALL display in the access rights grid the Programs attached to that Role Center.
4. WHEN the User_Service forms a Role Code for a Program under a selected Role Center, THE User_Service SHALL concatenate the Role Center Name with the Program Name to produce the Role Code (for example, `SSCMP` for System Admin → Company Master, `OOASM` for Origination → Asset Master, `OOETM` for Origination → Entity Master).
5. THE User_Admin_UI SHALL present the access rights grid with the columns Role Code, Program Description, Add, Modify, Query, Delete, and Select All.
6. WHEN a System Administrator sets any combination of the Add, Modify, Query, and Delete flags for a Program row, THE User_Service SHALL persist exactly the selected combination of access rights for that Role Code and Program.
7. WHEN a System Administrator activates Select All for a Program row, THE User_Admin_UI SHALL set the Add, Modify, Query, and Delete flags for that row to granted.
8. THE User_Admin_UI SHALL present the selected Line of Business, Role Code, and Program Description in a separate grid, and SHALL allow the System Administrator to add and delete rows in that grid at any time.
9. WHEN a System Administrator saves access assignments, THE User_Service SHALL persist each access row as the combination of Line of Business, Role Code, Program, and the four access-right flags.

### Requirement 9: Access Tab — Branch / Location Tree Association

**User Story:** As a System Administrator, I want to associate a user with one or more branches from a location tree, so that the user operates only within permitted branches.

#### Acceptance Criteria

1. WHEN a System Administrator opens the branch selector on the Access tab, THE User_Admin_UI SHALL present a hierarchical Location Tree of Locations, states or regions, and branches (for example, MAHARASHTRA → MUMBAI → HEAD OFFICE, FORT BRANCH).
2. WHEN a System Administrator selects one or more branches in the Location Tree, THE User_Service SHALL associate each selected branch with the record so that the user's branch associations are retrievable.
3. THE User_Admin_UI SHALL present an `ALL` option in both the Branch selector and the Role Center Name selector.
4. WHEN a System Administrator selects the `ALL` option in the Branch selector, THE User_Service SHALL apply the access assignment to all branches available to the record.
5. WHEN a System Administrator selects the `ALL` option in the Role Center Name selector, THE User_Service SHALL apply the access assignment across all Role Centers available to the record.
6. THE User_Service SHALL require at least one branch association (including the `ALL` selection) for a user record before the access assignment is persisted.
   - **Assumption:** At least one branch association is required to persist access; the spec implies branch association but does not state an explicit blank-selection message. Flagged for confirmation.

### Requirement 10: Copy Profile Facility

**User Story:** As a System Administrator, I want to copy an existing user's profile for a selected line of business, so that I can provision a similar user quickly without re-entering every access row.

#### Acceptance Criteria

1. WHILE the master is in Create Mode, THE User_Admin_UI SHALL present a Copy Profile control and a Copy Profile source selector listing active users.
2. WHEN a System Administrator enables Copy Profile and selects a source user and a source Line of Business, THE User_Service SHALL append the branches and program access of the selected source user's selected Line of Business to the Line of Business selected on the record being created.
3. THE User_Service SHALL make the Copy Profile facility available in Create Mode and SHALL NOT offer Copy Profile in Query Mode.
4. IF a System Administrator enables Copy Profile without selecting a source user, THEN THE User_Admin_UI SHALL block the copy and display a field-level validation message indicating a source user is required.
   - **Assumption:** Copy Profile append semantics add the source branches and program access to the current selection rather than replacing it; duplicate access rows resolve to a single granted row. The de-duplication rule is a design decision.

### Requirement 11: Modify Mode

**User Story:** As a System Administrator, I want to retrieve an existing record by its code and edit its editable fields, so that I can keep user, group, and functional-group records accurate.

#### Acceptance Criteria

1. WHEN a System Administrator enters a User Code, User Group Code, or Functional Group Code in Modify Mode, THE User_Service SHALL populate all relevant data for the matching record.
2. THE User_Admin_UI SHALL present the User Code, User Group Code, and Functional Group Code as not modifiable in Modify Mode.
3. WHEN a System Administrator submits a modify request changing any of User Name, User Group Name, Password, Mobile Number, Email id, Date of Joining, Designation, Department, Branch, Line of Business, Role Code, Program Description, or Active Indicator, and the submitted values pass validation, THE User_Service SHALL persist the changes and return the updated record.
4. WHILE the master is in Modify Mode, THE User_Admin_UI SHALL enable the Reset Password facility; and WHILE the master is in Create Mode or Query Mode, THE User_Admin_UI SHALL disable the Reset Password facility.
5. WHEN a System Administrator uses the Reset Password facility and submits a new Password that complies with the GPS password policy, THE User_Service SHALL replace the stored Password with the new Password.
6. IF a System Administrator submits a Reset Password value that does not comply with the GPS password policy, THEN THE User_Service SHALL reject the request with the message "Please enter a valid Password" and SHALL preserve the existing Password unchanged.
7. IF a System Administrator attempts to modify a record whose code does not exist, THEN THE User_Service SHALL return a not-found error and SHALL make no change to the master.
8. WHEN a modify request leaves the Password field blank, THE User_Service SHALL retain the existing stored Password unchanged.
   - **Assumption:** A blank Password in Modify Mode means "no change"; the Password is changed only via the Reset Password facility. Flagged for confirmation.

### Requirement 12: Query Mode

**User Story:** As a System Administrator, I want to view an existing record read-only, so that I can inspect its details without risk of accidental change.

#### Acceptance Criteria

1. WHEN a System Administrator enters a User Code, User Group Code, or Functional Group Code in Query Mode, THE User_Service SHALL return all relevant data for the matching record for display.
2. WHILE the master is in Query Mode, THE User_Admin_UI SHALL render all fields as read-only and SHALL disable the Save control.
3. IF a System Administrator enters a code in Query Mode that does not match any record, THEN THE User_Service SHALL return a not-found error and THE User_Admin_UI SHALL display a not-found indication without populating fields.

### Requirement 13: List and Pagination of Users

**User Story:** As a System Administrator, I want to list users with pagination, so that I can review and locate records efficiently across large data sets.

#### Acceptance Criteria

1. WHEN a System Administrator requests a list of users, THE User_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
2. WHERE a search term is provided, THE User_Service SHALL return only the records whose User Code, User Name, User Group Code, User Group Name, or Functional Group Code contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
3. WHERE an Active Indicator filter is provided, THE User_Service SHALL return only the records matching the requested active status; WHERE no Active Indicator filter is provided, THE User_Service SHALL return records regardless of active status.
4. WHEN a System Administrator requests a list without specifying page number, THE User_Service SHALL apply a default page number of 1.
5. WHEN a System Administrator requests a list without specifying page size, THE User_Service SHALL apply a default page size of 20.
6. IF a System Administrator requests a page number less than 1, THEN THE User_Service SHALL reject the request with a validation error indicating the page number is out of range and SHALL NOT return any records.
7. IF a System Administrator requests a page size less than 1 or greater than 100, THEN THE User_Service SHALL reject the request with a validation error indicating the page size is out of range and SHALL NOT return any records.
8. WHEN a System Administrator requests a page number beyond the last available page, THE User_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
9. WHEN returning a paginated list, THE User_Service SHALL order the records by User Code ascending, applying a deterministic secondary ordering by unique identifier ascending for records sharing an identical ordering key.

### Requirement 14: Record Audit Trail on Create and Modify

**User Story:** As a System Administrator, I want each create and modify captured with the acting user and transaction date, so that changes to user provisioning are traceable.

#### Acceptance Criteria

1. WHEN the User_Service persists a create of a user, user group, or functional group record, THE User_Service SHALL record an Audit Entry containing the created record identifier, the create action indicator, the acting System Administrator's identifier, and the transaction date.
2. WHEN the User_Service persists a modify of a user, user group, or functional group record, THE User_Service SHALL record an Audit Entry containing the affected record identifier, the modify action indicator, the acting System Administrator's identifier, and the transaction date.
3. IF a create or modify request is rejected by validation, by the uniqueness rule (Requirement 2), or by any authorization rule (Requirement 15), THEN THE User_Service SHALL NOT record an Audit Entry for that rejected request.
4. THE User_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that any request to update or delete a recorded Audit Entry is rejected with an error indicating that audit entries are immutable.
   - **Assumption:** The transaction date is recorded in Coordinated Universal Time (UTC) and rendered to the System Administrator in the GPS `DD/MM/YYYY` format by the UI. Flagged for confirmation.

### Requirement 15: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want user management operations restricted to System Administrators, so that only authorized personnel can provision users and grant access rights.

#### Acceptance Criteria

1. IF a request to create, modify, query, or list a user, user group, or functional group is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE User_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create or modify any stored record, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts to create, modify, query, or list a user, user group, or functional group, THEN THE User_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create or modify any stored record, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the SystemAdmin role claim invokes a create, modify, query, or list operation, THE User_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 14.
4. WHEN the User_Service evaluates authentication and authorization for any request, THE User_Service SHALL enforce authentication before authorization, such that a request carrying a missing, expired, malformed, or signature-invalid JWT Bearer token that also lacks SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the User_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) established by prior SystemAdmin masters. Establishing that scheme is a prerequisite satisfied by those features.

### Requirement 16: User Management Administrative UI

**User Story:** As a System Administrator, I want a User Management screen matching the three configurations and the User Details, User Group, Access, and Functional-list views, so that I can provision and maintain users through a consistent interface.

#### Acceptance Criteria

1. WHEN a System Administrator opens the User_Admin_UI, THE User_Admin_UI SHALL present a User Configuration selector offering `User`, `User Group`, and `Functional Group`, and a mode indication for Create, Modify, and Query.
2. WHEN the `User` configuration is selected, THE User_Admin_UI SHALL present the User Details tab with the individual user detail fields, the Active Indicator checkbox defaulted to ticked, and the Reset Password checkbox enabled only in Modify Mode.
3. WHEN the `User Group` configuration is selected and the System Administrator opens the User Group facility, THE User_Admin_UI SHALL present the User Group popup for creating a group and adding or deleting members from the list of active users.
4. WHEN a System Administrator opens the Access tab, THE User_Admin_UI SHALL present the Line of Business selector, the Role Center Name selector, the program access rights grid (Role Code, Program Description, Add, Modify, Query, Delete, Select All), the separate Line of Business / Role Code / Program Description grid with add and delete row controls, and the branch Location Tree with the `ALL` option.
5. WHEN the `Functional Group` configuration is selected, THE User_Admin_UI SHALL present the functional-group list details showing the functionality clubbed under the entered Functional Group Code and the target selector for a User Code & Name or a User Group Code.
6. IF a System Administrator clicks Save with a missing required field, THEN THE User_Admin_UI SHALL block submission and display the corresponding field-level validation message defined in Requirements 3 through 9.
7. IF a request to the User_Service does not complete within 10 seconds, THEN THE User_Admin_UI SHALL stop waiting and display a retry-able error indication to the System Administrator.
8. WHEN the User_Admin_UI issues any request to the User_Service, THE User_Admin_UI SHALL include the JWT Bearer token in the Authorization header via the shared axios instance.
9. THE User_Admin_UI SHALL implement the User Management feature using React hooks and Context for state and the service interface / mock / real / toggle pattern selected by `VITE_USE_MOCK_API`, and SHALL render tables and dialogs using Material UI components (`@mui/material`, `@mui/x-data-grid`).
10. WHILE the master is in Query Mode, THE User_Admin_UI SHALL disable the Save control and render all fields read-only.
