# Requirements Document

## Introduction

The Document Master Management feature (JIRA: FINNOVA-49, FINNOVA-50, FINNOVA-52; Module: SystemAdmin; Master: Document; Parent Epic: FINNOVA-23 Customer Master) provides a centralized administrative capability for defining every statutory, KYC, financial, legal, and incorporation document used across the Finnova platform. The Document Master is the single source of truth: Customer Master and other consuming modules load document definitions, applicability rules, validation rules, and masking rules from this master rather than defining them per module.

This spec consolidates three source tickets into one cohesive Document Master feature:

- **FINNOVA-49** — the document data model, role-based Create/View/Edit/Authorize screens, maker-checker lifecycle, audit trail, and delete-vs-inactivate protection.
- **FINNOVA-50** — entity and constitution applicability rules (mapping a document to one or more Constitutions and Entity Roles with a per-mapping mandatory flag), authorized through the same maker-checker lifecycle.
- **FINNOVA-52** — the statutory validation engine, format rules, masking rules, and file rules per document, authorized through the same maker-checker lifecycle.

A Document Master Maker can create and edit document definitions, applicability rules, and validation/masking configuration; all such changes are submitted for approval and held as `PENDING_APPROVAL` without affecting consumers. A Document Master Checker reviews a submission against the currently live values (shown old/new side by side), then authorizes it (making it `ACTIVE` and immediately consumable) or rejects it with a mandatory remark (returning it to the Maker). A Maker cannot authorize a record they created or edited, even when that same user also holds the Checker role. Every action is captured in an immutable audit trail.

Document category, applicable entity, status, constitution, entity role, and validation engine values are sourced from the Lookup Master (FINNOVA-8); dropdowns load only `ACTIVE` lookup values and reject free text. A document referenced by Customer Master cannot be deleted and can only be inactivated; `INACTIVE` documents are hidden from the Customer Master document checklist but remain visible with an "Inactive" tag for customers who already hold them.

This master-data feature mirrors the established Lookup Master (FINNOVA-8), Nationality Master (FINNOVA-9), and Organization Hierarchy Master (FINNOVA-5) features. It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI). This spec covers both the backend API and the frontend screen.

This document captures the requirements derived from the three JIRA tickets and their Gherkin scenarios. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Dependency (Lookup Master, FINNOVA-8):** The Document Master consumes `ACTIVE` lookup values from the Lookup Master for the following Lookup Types: `DOC_CATEGORY` (KYC, FINANCIAL, LEGAL_COLLATERAL, INCORPORATION, TAX), `APPLICABLE_ENTITY` (CUSTOMER, GUARANTOR, CO_APPLICANT, EMPLOYER, DEALER, ASSET), `STATUS` (ACTIVE, INACTIVE, PENDING_APPROVAL), `CONSTITUTION` (Individual, Pvt Ltd, Public Ltd, Partnership, LLP, Trust), and `VALIDATION_ENGINE` (NONE, NSDL_PAN_API, AADHAAR_VAULT_EKYC, MCA_CIN_API, GSTIN_API, MANUAL). This feature reads these values; it does not define or seed them.
  - **Assumption (Status modeling):** `STATUS` is enumerated through the Lookup Master `STATUS` type per FINNOVA-49, but the maker-checker lifecycle in this feature treats status as a controlled state machine (`PENDING_APPROVAL` → `ACTIVE` / back to Maker on reject; `ACTIVE` → `INACTIVE`). Whether status is persisted as a Lookup-backed value or as a domain enum constrained to those lookup values is a design decision, flagged for confirmation. Additionally, the post-reject `RETURNED_TO_MAKER` state introduced by Requirement 5 is a lifecycle-only state (not a Lookup Master `STATUS` value); it marks that a rejected submission's Pending Change has been returned to the Maker for correction and resubmission, and it is flagged for confirmation.
- **Dependency (Customer Master epic, FINNOVA-23):** Customer Master is the primary consumer. The "document referenced by Customer Master cannot be deleted" rule (Requirement 7) depends on a way to detect whether a document is in use by Customer Master, which is a separate epic.
  - **Assumption (In-use detection):** Because Customer Master is a separate epic and may not exist when this feature ships, the mechanism for detecting "in use" is deferred to design. This spec treats in-use detection as an abstract reference check (for example, a count returned by a Customer Master query or an outbound usage-check contract); the exact integration contract is flagged for confirmation. Until Customer Master exposes such a check, the Document Master SHALL treat the in-use check as returning "in use" only when the integration reports a reference, and otherwise permit deletion of a `PENDING_APPROVAL`/never-consumed document.
- **Assumption (Host placement):** Document Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host (which already exposes the Lookup, Nationality, and Organization Hierarchy masters), reached through the gateway prefix `/api/systemadmin/**`. This mirrors the prior master-data decisions and is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication (established by FINNOVA-8). This feature reuses that scheme and adds two role-based authorization policies, `DOC_MASTER_MAKER` and `DOC_MASTER_CHECKER`, evaluated via the role claim. Token issuance (login) ownership is external to this feature.
- **Assumption (Maker-checker versioning model):** The maker-checker lifecycle requires keeping the currently live (`ACTIVE`) values intact while a pending edit is in review, so that consumers keep seeing the old value and the Checker can view old/new side by side. How this is modeled — a pending-changes payload column, a shadow/draft row, or a separate change-request entity keyed to the document — is a design decision, flagged for confirmation. The requirements below describe the behavior, not the storage shape.
- **Assumption (Validation/masking config placement):** Whether the FINNOVA-52 validation engine, format regex, masking, and file rules live as columns on the Document entity or in a separate Document Validation Configuration entity is a design decision, flagged for confirmation. FINNOVA-49 already lists `REQUIRES_MASKING`, `MASK_PATTERN`, `MAX_FILE_SIZE_MB`, and `ALLOWED_MIME_TYPES` on the document, so this spec treats validation/masking configuration as attributes of the document unless design decides to extract them.
- **Assumption (Applicability matrix placement):** The FINNOVA-50 applicability rules (document × constitution × entity role × mandatory flag) are modeled as a separate Document Applicability Rule collection associated with a document, rather than as columns on the Document entity, because they are multi-valued. This is flagged for confirmation.
- **Assumption (FORMAT_REGEX validation ownership):** The Document Master stores and manages `FORMAT_REGEX`, `VALIDATION_ENGINE`, and `MASK_PATTERN` as configuration. Actually executing format validation (for example, applying the PAN regex, the Aadhaar Verhoeff checksum, or the GSTIN check) against a submitted document instance is the responsibility of the consuming module (Customer Master) at onboarding time, not of the Document Master. The Document Master validates that the configured regex is a syntactically valid regular expression and that the configuration is internally consistent (Requirement 9). The Verhoeff checksum for Aadhaar is applied by the consumer, not by this master. This boundary is flagged for confirmation.
- **Assumption (Audit trail scope):** No shared platform-wide audit infrastructure exists today; prior masters introduced feature-scoped audit trails. This feature introduces a document-scoped audit trail (Requirement 8) covering document changes, applicability-rule changes, validation/masking-config changes, and lifecycle transitions. Whether to promote audit to a shared capability is flagged for confirmation in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each document carries single English display fields (`DocName`, `DocShortName`, `Description`); there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **Document Master**: The centralized administrative catalog defining every statutory, KYC, financial, legal, and incorporation document used across the Finnova platform.
- **Document (Document Definition)**: A single record in the Document Master, identified by a Doc Code and carrying descriptive attributes, lifecycle Status, applicability rules, and validation/masking configuration.
- **Doc Code**: The primary business identifier for a document (for example, `DOC-PAN`), unique across the Document Master, restricted to letters, digits, and hyphens, maximum 20 characters.
- **Doc Name**: The single English display name of a document (for example, `PAN Card`), maximum 150 characters.
- **Doc Short Name**: An optional single English short label for a document used in compact displays.
- **Doc Category**: The classification of a document, sourced from the Lookup Master `DOC_CATEGORY` type (KYC, FINANCIAL, LEGAL_COLLATERAL, INCORPORATION, TAX).
- **Applicable Entity**: The entity type a document applies to on the document definition, sourced from the Lookup Master `APPLICABLE_ENTITY` type (CUSTOMER, GUARANTOR, CO_APPLICANT, EMPLOYER, DEALER, ASSET).
- **Target Module**: The business module that consumes a document (for example, Customer Master).
- **Is Mandatory**: A boolean flag indicating whether a document is required. On the document definition it is the default; on an Applicability Rule it is specific to a constitution/entity-role mapping.
- **Has Expiry**: A boolean flag indicating whether a document carries an expiry date that must be captured during onboarding.
- **Requires Masking**: A boolean flag indicating whether a document's value must be masked for PII protection.
- **Mask Pattern**: The pattern applied to mask a document value (for example, `ABCDE****F` for PAN, `XXXX-XXXX-1234` for Aadhaar).
- **Max File Size MB**: The maximum permitted uploaded file size in megabytes for a document, an integer in the range 1 to 25 inclusive, defaulting to 5.
- **Allowed Mime Types**: The set of permitted uploaded file content types for a document (for example, PDF, JPG, PNG).
- **Validation Engine**: The verification engine configured for a document, sourced from the Lookup Master `VALIDATION_ENGINE` type (NONE, NSDL_PAN_API, AADHAAR_VAULT_EKYC, MCA_CIN_API, GSTIN_API, MANUAL).
- **Format Regex**: A regular expression configured for a document that the consuming module uses to validate the format of a submitted document value.
- **Constitution**: A customer constitution type sourced from the Lookup Master `CONSTITUTION` type (Individual, Pvt Ltd, Public Ltd, Partnership, LLP, Trust).
- **Entity Role**: The role an entity plays for a document's applicability, sourced from the Lookup Master `APPLICABLE_ENTITY` type and constrained in FINNOVA-50 to Customer, Co-Applicant, and Guarantor.
- **Applicability Rule**: A mapping from a document to a combination of Constitution and Entity Role carrying an Is Mandatory flag, used by Customer Master to determine whether the document is mandatory or optional for a given customer.
- **Applicability Matrix**: The full set of Applicability Rules configured for a document.
- **Status**: The lifecycle state of a document, one of `ACTIVE`, `INACTIVE`, or `PENDING_APPROVAL`, sourced from the Lookup Master `STATUS` type. The maker-checker lifecycle may also use a `RETURNED_TO_MAKER` state after a rejection, which returns the Pending Change to the Maker for correction; `RETURNED_TO_MAKER` is a lifecycle-only state and is not a Lookup Master `STATUS` value, flagged as an assumption for confirmation.
- **Maker-Checker Lifecycle**: The two-stage authorization workflow in which a Maker submits a change held as `PENDING_APPROVAL` and a Checker authorizes or rejects it before it becomes effective.
- **Maker (Document Master Maker)**: An authenticated user whose token carries the `DOC_MASTER_MAKER` role claim, authorized to create and edit document definitions, applicability rules, and validation/masking configuration.
- **Checker (Document Master Checker)**: An authenticated user whose token carries the `DOC_MASTER_CHECKER` role claim, authorized to authorize or reject submissions.
- **Pending Change**: A set of submitted values held for approval that does not alter the currently live values until authorized.
- **Live Values**: The currently authorized (`ACTIVE`) values of a document that consumers see while a Pending Change is in review.
- **Audit Entry**: An immutable record capturing a change, including the affected document identifier, the action performed, the acting user's identifier, the change timestamp in UTC, and the changed field's before and after values.
- **Audit Trail**: The ordered collection of Audit Entries recorded for the Document Master.
- **Consuming Module**: A business module (primarily Customer Master, FINNOVA-23) that reads document definitions, applicability rules, and validation/masking configuration.
- **Document Checklist**: The list of applicable documents presented by Customer Master for a given customer, populated only from `ACTIVE` documents.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **Document_Service**: The backend service/API responsible for document definition, applicability, validation/masking management, lifecycle, and retrieval.
- **Document_Admin_UI**: The React-based Document Master administrative screen used by Makers and Checkers (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create Document Definition (FINNOVA-49)

**User Story:** As a Document Master Maker, I want to create a new document definition with its code, name, category, and attributes, so that the document is defined centrally for Customer Master and other modules.

#### Acceptance Criteria

1. WHEN a Maker submits a create request containing a Doc Code, a Doc Name, a Doc Category, and an Applicable Entity, THE Document_Service SHALL persist a new document record with Status `PENDING_APPROVAL` and return the created record including a system-generated identifier.
2. WHEN a document is created, THE Document_Service SHALL set the document Status to `PENDING_APPROVAL` and SHALL NOT make the document visible to any Consuming Module until the document is authorized (per Requirement 5).
3. IF a create request omits Doc Code, Doc Name, Doc Category, or Applicable Entity, or provides any of them as an empty or whitespace-only value, THEN THE Document_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
4. IF the submitted Doc Code contains any character other than an uppercase letter, lowercase letter, digit, or hyphen, or exceeds 20 characters after trimming leading and trailing whitespace, THEN THE Document_Service SHALL reject the request with a validation error identifying Doc Code and SHALL NOT persist any record.
5. IF the submitted Doc Name exceeds 150 characters after trimming leading and trailing whitespace, THEN THE Document_Service SHALL reject the request with a validation error identifying Doc Name and SHALL NOT persist any record.
6. WHERE Max File Size MB is not provided on a create request, THE Document_Service SHALL default Max File Size MB to 5.
7. IF Max File Size MB is provided and is not an integer in the range 1 to 25 inclusive, THEN THE Document_Service SHALL reject the request with a validation error carrying the message "Max file size must be between 1 and 25 MB" and SHALL NOT persist any record.
8. IF the submitted Doc Category is not an `ACTIVE` value of the Lookup Master `DOC_CATEGORY` type, or the submitted Applicable Entity is not an `ACTIVE` value of the Lookup Master `APPLICABLE_ENTITY` type, THEN THE Document_Service SHALL reject the request with a validation error identifying the invalid lookup-backed field and SHALL NOT persist any record.
9. WHERE Is Mandatory, Has Expiry, or Requires Masking is not provided on a create request, THE Document_Service SHALL default each absent flag to false.

### Requirement 2: Enforce Doc Code Uniqueness (FINNOVA-49)

**User Story:** As a Document Master Maker, I want the system to prevent duplicate document codes, so that each document is uniquely identified across the platform.

#### Acceptance Criteria

1. THE Document_Service SHALL treat Doc Code as unique across the Document Master, comparing codes case-insensitively (for example, `doc-pan` and `DOC-PAN` are treated as the same code) after trimming leading and trailing whitespace.
2. IF a create request specifies a Doc Code that, after trimming and case-insensitive comparison, matches the Doc Code of an existing document, THEN THE Document_Service SHALL reject the request with a validation error indicating the document code already exists, SHALL create no new record, and SHALL retain the existing record unchanged.
3. WHILE a document's Status is `ACTIVE`, THE Document_Service SHALL treat the Doc Code as read-only and SHALL reject any edit request that would change the Doc Code with a validation error indicating the document code cannot be changed after approval.
4. WHILE a document's Status is `PENDING_APPROVAL` and the document has never been authorized, THE Document_Service SHALL permit a Maker to change the Doc Code subject to the format and uniqueness rules in this requirement and Requirement 1.

### Requirement 3: Edit Document Definition Under Maker-Checker (FINNOVA-49)

**User Story:** As a Document Master Maker, I want to edit an existing document's attributes, so that corrections and updates are captured without immediately affecting live consumers.

#### Acceptance Criteria

1. WHEN a Maker submits an edit to an `ACTIVE` document's editable attributes and all submitted values pass validation, THE Document_Service SHALL record the submitted values as a Pending Change, set the document to `PENDING_APPROVAL`, and preserve the document's Live Values so that Consuming Modules continue to receive the Live Values until the change is authorized.
2. WHEN a Maker submits an edit, THE Document_Service SHALL retain both the Live Values and the Pending Change so that a Checker can view the old and new values side by side (per Requirement 5).
3. IF a Maker submits an edit for a document identifier that does not exist, THEN THE Document_Service SHALL return a not-found error and SHALL make no change to the Document Master.
4. IF a Maker submits an edit that fails any attribute validation defined in Requirement 1, THEN THE Document_Service SHALL reject the request with the corresponding validation error and SHALL preserve both the Live Values and any existing Pending Change unchanged.
5. WHILE a document's Status is `PENDING_APPROVAL` and its Pending Change is awaiting authorization, THE Document_Service SHALL reject a further edit submission against the same document with a conflict error indicating a change is already pending authorization, and SHALL preserve the existing Pending Change unchanged.
   - **Assumption:** Only one Pending Change per document at a time. Whether concurrent pending edits should be queued is flagged for confirmation.
6. WHEN a Maker submits an edit to a document whose Status is `RETURNED_TO_MAKER` and all submitted values pass validation, THE Document_Service SHALL update the retained Pending Change with the corrected values and keep the Live Values unchanged so that the Maker can resubmit the corrected change for authorization (per Requirement 5).

### Requirement 4: Configure Applicability Rules by Constitution and Entity Role (FINNOVA-50)

**User Story:** As a Document Master Maker, I want to map a document to one or more constitutions and entity roles with a mandatory flag per mapping, so that Customer Master knows which documents are mandatory or optional for a given customer.

#### Acceptance Criteria

1. WHEN a Maker submits one or more Applicability Rules for a document, each mapping the document to one Constitution and one Entity Role with an Is Mandatory flag, THE Document_Service SHALL record the submitted Applicability Rules as a Pending Change, set the document to `PENDING_APPROVAL`, and preserve the currently live Applicability Matrix until the change is authorized.
2. THE Document_Service SHALL allow a single document to be mapped to multiple Constitutions and multiple Entity Roles.
3. IF a submitted Applicability Rule references a Constitution that is not an `ACTIVE` value of the Lookup Master `CONSTITUTION` type, or an Entity Role that is not an `ACTIVE` value of the Lookup Master `APPLICABLE_ENTITY` type, THEN THE Document_Service SHALL reject the submission with a validation error identifying the invalid lookup-backed field and SHALL NOT persist any Applicability Rule.
4. IF a submission contains two or more Applicability Rules with the same combination of Constitution and Entity Role, THEN THE Document_Service SHALL reject the submission with a validation error indicating a duplicate applicability mapping and SHALL NOT persist any Applicability Rule from that submission.
5. WHEN a Checker authorizes an Applicability Rule change (per Requirement 5), THE Document_Service SHALL make the updated Applicability Matrix the Live Values so that Customer Master uses the updated rules on the next query.
6. WHILE an Applicability Rule change is `PENDING_APPROVAL`, THE Document_Service SHALL continue to serve the previously authorized Applicability Matrix to Consuming Modules and SHALL NOT apply the pending rules.

### Requirement 5: Maker-Checker Authorization Lifecycle (FINNOVA-49, FINNOVA-50, FINNOVA-52)

**User Story:** As a Document Master Checker, I want to review and authorize or reject submitted document changes, so that only verified changes become effective for consumers.

#### Acceptance Criteria

1. WHEN a Checker retrieves a document that is in `PENDING_APPROVAL` status, THE Document_Service SHALL return both the current Live Values and the Pending Change in a single response so that the Document_Admin_UI can present the old and new values side by side.
2. IF a Checker retrieves a document that does not exist or is not in `PENDING_APPROVAL` status, THEN THE Document_Service SHALL reject the retrieval with a validation error indicating there is no pending change to review and SHALL return no Live/Pending comparison payload.
3. WHEN a Checker authorizes a `PENDING_APPROVAL` document, THE Document_Service SHALL replace the Live Values with the Pending Change values, set the document Status to `ACTIVE`, and clear the Pending Change.
4. WHEN the Document_Service completes an authorization, THE Document_Service SHALL make the authorized Live Values the values returned to Consuming Modules on every query issued after the authorizing transaction commits, without requiring a service restart or deployment.
5. IF the authenticated user attempting to authorize a document is the same user who created or last edited that document's Pending Change, THEN THE Document_Service SHALL reject the authorization with error code `ERR-DOC-001` and the message "You cannot authorize a record you created or edited", and SHALL leave the document in `PENDING_APPROVAL` with its Pending Change unchanged.
6. WHEN a Checker rejects a `PENDING_APPROVAL` document with a rejection remark of 1 to 500 characters after trimming leading and trailing whitespace, THE Document_Service SHALL set the document Status to `RETURNED_TO_MAKER`, SHALL retain the Pending Change so the Maker can correct and resubmit it, SHALL leave the Live Values unchanged, and SHALL record the rejection remark in the audit trail.
7. IF a Checker submits a reject action whose rejection remark is absent, empty, or longer than 500 characters after trimming leading and trailing whitespace, THEN THE Document_Service SHALL reject the action with a validation error indicating a rejection remark of 1 to 500 characters is required and SHALL leave the document in `PENDING_APPROVAL` with its Status, Live Values, and Pending Change unchanged.
8. WHEN the Document_Service evaluates an authorize or reject action, THE Document_Service SHALL require the acting user to hold the `DOC_MASTER_CHECKER` role, enforced per Requirement 12.
9. IF a Checker attempts to authorize or reject a document that is not in `PENDING_APPROVAL` status, THEN THE Document_Service SHALL reject the action with a validation error indicating there is no pending change to act upon and SHALL leave the document's Status, Live Values, and Pending Change unchanged.
10. WHEN a Maker resubmits a document that is in `RETURNED_TO_MAKER` status after correcting its Pending Change, THE Document_Service SHALL set the document Status back to `PENDING_APPROVAL` and SHALL record the Maker as the last editor of the Pending Change for the purposes of the self-authorization prohibition.

#### Assumptions

- **Reject outcome:** A reject does not discard the submission; it sets the document to `RETURNED_TO_MAKER` and returns the Pending Change to the Maker for correction and resubmission. Flagged for confirmation.
- **Returned status value:** `RETURNED_TO_MAKER` is a lifecycle-only state used by the maker-checker workflow; it is not an `ACTIVE` value of the Lookup Master `STATUS` type. Flagged for confirmation.
- **Rejection remark length:** The rejection remark is capped at 500 characters after trimming. Flagged for confirmation.
- **Effectiveness timing:** Authorized Live Values become effective to Consuming Modules once the authorizing transaction commits, with no caching delay assumed. Flagged for confirmation.

### Requirement 6: Activate and Inactivate Documents (FINNOVA-49)

**User Story:** As a Document Master Maker, I want to inactivate a document instead of deleting it, so that documents referenced by Customer Master are retained while being removed from active checklists.

#### Acceptance Criteria

1. WHEN a Maker submits a request to inactivate an `ACTIVE` document and the change is authorized by a Checker (per Requirement 5), THE Document_Service SHALL set the document Status to `INACTIVE`.
2. WHILE a document's Status is `INACTIVE`, THE Document_Service SHALL exclude the document from the Customer Master Document Checklist returned by the active-document consumption query (Requirement 11).
3. WHERE a customer already holds a document whose Status is `INACTIVE`, THE Document_Service SHALL still return that document to Customer Master with an indicator that the document is `INACTIVE` so that the Document_Admin_UI and Customer Master can display an "Inactive" tag.
   - **Assumption:** The "customers already having it" case is surfaced by returning the document with its `INACTIVE` status when queried by identifier; detecting which customers hold it is Customer Master's responsibility.
4. WHEN a Maker submits a request to reactivate an `INACTIVE` document and the change is authorized by a Checker, THE Document_Service SHALL set the document Status to `ACTIVE` and include it in the Document Checklist on the next query.

### Requirement 7: Delete Protection for Referenced Documents (FINNOVA-49)

**User Story:** As a Document Master Maker, I want deletion blocked for documents in use by Customer Master, so that referenced reference data is never lost.

#### Acceptance Criteria

1. IF a Maker attempts to delete a document that the in-use check reports as referenced by Customer Master, THEN THE Document_Service SHALL block the deletion, SHALL return error code `ERR-DOC-002` with the message "Document is in use and cannot be deleted. You can mark it Inactive", and SHALL leave the document record unchanged in the Document Master.
2. WHEN a Maker deletes a document that the in-use check reports as not referenced by Customer Master and whose Status value is `PENDING_APPROVAL` and has never been authorized, THE Document_Service SHALL permanently remove the document record so that it returns zero results in every subsequent query by its identifier.
   - **Assumption:** Deletion is a hard delete, restricted to never-consumed documents (for example, `PENDING_APPROVAL` records that were never authorized). Any `ACTIVE` document that could have been consumed is inactivated rather than deleted. This policy is flagged for confirmation.
3. IF a Maker attempts to delete a document whose Status value is `ACTIVE`, THEN THE Document_Service SHALL block the deletion, SHALL return an error indicating that an active document must be inactivated rather than deleted, and SHALL leave the document record unchanged in the Document Master.
4. IF a Maker attempts to delete a document whose identifier does not exist in the Document Master, THEN THE Document_Service SHALL return a not-found error and SHALL make no change to the Document Master.
5. IF the in-use check cannot be completed within 5 seconds or the Customer Master integration returns an error, THEN THE Document_Service SHALL block the deletion, SHALL return an error indicating the in-use status could not be confirmed, and SHALL leave the document record unchanged in the Document Master.
   - **Assumption:** When reference status is unknown, the Service fails safe by refusing deletion. This conservative default is flagged for confirmation.

### Requirement 8: Immutable Audit Trail (FINNOVA-49, FINNOVA-50, FINNOVA-52)

**User Story:** As a Document Master Checker, I want every document action captured in an immutable audit trail, so that changes to standardized document data are fully traceable.

#### Acceptance Criteria

1. WHEN the Document_Service persists a create, edit, applicability-rule change, validation/masking-config change, authorize, reject, inactivate, reactivate, or delete action, THE Document_Service SHALL record exactly one Audit Entry per action containing the affected document identifier, the action indicator, the acting user's identifier, the change timestamp in Coordinated Universal Time (UTC), and the changed field's before value and after value.
2. WHEN the Document_Service records an Audit Entry for a reject action, THE Document_Service SHALL include the rejection remark in the Audit Entry.
3. IF an action is rejected by validation, by the uniqueness rule, or by the self-authorization rule, THEN THE Document_Service SHALL NOT record an Audit Entry for that rejected action and SHALL leave the count of persisted Audit Entries unchanged.
4. WHEN a user requests the Audit Entries for a specified document, THE Document_Service SHALL return the Audit Entries for that document ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
5. THE Document_Service SHALL retain each Audit Entry without modifying or removing it after it is recorded, such that any request to update or delete a previously recorded Audit Entry is rejected with an error indicating that audit entries are immutable.
6. IF a user requests the Audit Entries for a document identifier that does not exist, THEN THE Document_Service SHALL return an empty result set and SHALL NOT return an error.

### Requirement 9: Validation Engine, Format, Masking, and File Configuration (FINNOVA-52)

**User Story:** As a Document Master Maker, I want to configure the validation engine, format regex, masking rules, and file rules per document, so that Customer Master applies the correct verification and PII protection.

#### Acceptance Criteria

1. WHEN a Maker submits a validation/masking configuration containing a Validation Engine value that is an `ACTIVE` value of the Lookup Master `VALIDATION_ENGINE` type, THE Document_Service SHALL record the configuration as a Pending Change requiring authorization per Requirement 5 and SHALL NOT alter the current Live Values until authorization completes.
2. IF a submitted Validation Engine value is not an `ACTIVE` value of the Lookup Master `VALIDATION_ENGINE` type, or is submitted as free text not present in that lookup, THEN THE Document_Service SHALL reject the submission with a validation error identifying Validation Engine and SHALL NOT persist the configuration.
3. IF Requires Masking is true and Mask Pattern is empty, null, or consists only of whitespace characters, THEN THE Document_Service SHALL reject the submission with a validation error carrying the message "Mask pattern is required when masking is enabled" and SHALL NOT persist the configuration.
4. IF a document is identified as an Aadhaar document and a submission sets Requires Masking to false, THEN THE Document_Service SHALL reject the submission with a validation error carrying the message "Aadhaar must always be masked" and SHALL NOT persist the configuration.
   - **Assumption:** An "Aadhaar document" is identified by a configurable marker (for example, Doc Code `DOC-AADHAAR` or a dedicated flag) rather than by parsing the Doc Name; the exact marker is a design decision flagged for confirmation.
5. IF a submitted Format Regex is a non-empty value that cannot be compiled as a syntactically valid regular expression, THEN THE Document_Service SHALL reject the submission with a validation error identifying Format Regex and SHALL NOT persist the configuration.
6. IF a submitted Mask Pattern exceeds 200 characters, THEN THE Document_Service SHALL reject the submission with a validation error identifying Mask Pattern and SHALL NOT persist the configuration.
7. IF a submitted Format Regex exceeds 500 characters, THEN THE Document_Service SHALL reject the submission with a validation error identifying Format Regex and SHALL NOT persist the configuration.
8. IF a submitted Max File Size MB is not an integer, or is an integer outside the range 1 to 25 inclusive, THEN THE Document_Service SHALL reject the submission with a validation error carrying the message "Max file size must be between 1 and 25 MB" and SHALL NOT persist the configuration.
9. IF a submitted Allowed Mime Types set is empty, or contains any value outside the permitted set (PDF, JPG, PNG), THEN THE Document_Service SHALL reject the submission with a validation error identifying Allowed Mime Types and SHALL NOT persist the configuration.
10. WHEN a Checker authorizes a validation/masking configuration change (per Requirement 5), THE Document_Service SHALL make the Validation Engine, Format Regex, Mask Pattern, Max File Size MB, and Allowed Mime Types the Live Values so that Customer Master applies the new configuration on the next query.
11. THE Document_Service SHALL store the Validation Engine, Format Regex, and Mask Pattern as configuration consumed by Customer Master and SHALL NOT itself execute format validation or checksum verification (for example, the Aadhaar Verhoeff checksum) against submitted document instances.
    - **Assumption:** Execution of format/checksum validation is the consuming module's responsibility; this boundary is flagged for confirmation.

### Requirement 10: Query and Search Documents (FINNOVA-49)

**User Story:** As a Document Master Maker, I want to search documents by code, name, category, and status with pagination, so that I can locate the correct document.

#### Acceptance Criteria

1. WHEN a user requests documents with a non-empty search term, THE Document_Service SHALL return only the documents whose Doc Code or Doc Name contains the search term as a substring, matched case-insensitively, subject to the applied pagination parameters.
2. WHERE a Doc Category filter or a Status filter is provided, THE Document_Service SHALL return only the documents matching all provided filters combined with logical AND, subject to the applied pagination parameters.
3. WHEN a user requests documents, THE Document_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
4. WHEN a user requests documents without specifying a page number, THE Document_Service SHALL apply a default page number of 1.
5. WHEN a user requests documents without specifying a page size, THE Document_Service SHALL apply a default page size of 20.
6. IF a user requests a page number less than 1, THEN THE Document_Service SHALL reject the request with a validation error indicating the page number is out of range and SHALL NOT return any records.
7. IF a user requests a page size less than 1 or greater than 100, THEN THE Document_Service SHALL reject the request with a validation error indicating the page size is out of range and SHALL NOT return any records.
8. WHEN returning documents, THE Document_Service SHALL order the results by Doc Code ascending, and for documents sharing the same Doc Code SHALL apply a deterministic secondary ordering by document identifier ascending.
9. WHEN a search or filter request matches no documents, THE Document_Service SHALL return an empty item collection with a total count of 0 and a success status.

### Requirement 11: Active-Document Consumption for Customer Master (FINNOVA-49, FINNOVA-50, FINNOVA-52)

**User Story:** As a Consuming Module, I want an endpoint that returns active documents with their applicability and validation configuration, so that Customer Master can build the document checklist and apply verification and masking.

#### Acceptance Criteria

1. WHEN a Consuming Module requests the active documents for a given Constitution and Entity Role, THE Document_Service SHALL return only documents whose Status is `ACTIVE` and whose Applicability Matrix includes a rule for the requested Constitution and Entity Role.
2. WHEN returning an active document for consumption, THE Document_Service SHALL include the resolved Is Mandatory flag from the matching Applicability Rule, the Has Expiry flag, the Validation Engine, the Format Regex, the Requires Masking flag, the Mask Pattern, the Max File Size MB, and the Allowed Mime Types.
3. WHEN returning active documents for consumption, THE Document_Service SHALL exclude documents whose Status is `INACTIVE` or `PENDING_APPROVAL` from the Document Checklist.
4. WHEN a document holds an unauthorized Pending Change, THE Document_Service SHALL return the document's Live Values to the Consuming Module and SHALL NOT expose the Pending Change values.
5. WHEN a Consuming Module requests active documents for a defined Constitution and Entity Role that has no applicable active documents, THE Document_Service SHALL return an empty result set rather than an error.
6. IF a Consuming Module requests active documents for a Constitution or Entity Role that is not a defined Lookup Master value, THEN THE Document_Service SHALL reject the request with a validation error identifying the unknown value and SHALL NOT return any documents.

### Requirement 12: Authorization and Authentication Enforcement (FINNOVA-49, FINNOVA-50, FINNOVA-52)

**User Story:** As a security stakeholder, I want document management operations restricted to the correct Maker and Checker roles, so that only authorized personnel can define or approve standardized document data.

#### Acceptance Criteria

1. IF a request to create, edit, configure, authorize, reject, inactivate, delete, query, or read the audit trail of a document is received with a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE Document_Service SHALL reject the request with an unauthorized (401) response and SHALL NOT create, modify, or delete any stored document record.
2. IF an authenticated user whose token lacks the `DOC_MASTER_MAKER` role claim attempts to create, edit, configure applicability or validation/masking, inactivate, reactivate, or delete a document, THEN THE Document_Service SHALL reject the request with a forbidden (403) response and SHALL NOT modify any stored document record.
3. IF an authenticated user whose token lacks the `DOC_MASTER_CHECKER` role claim attempts to authorize or reject a document, THEN THE Document_Service SHALL reject the request with a forbidden (403) response and SHALL NOT modify any stored document record.
4. WHEN a request carrying a valid, unexpired JWT Bearer token bearing the required role claim invokes an operation permitted to that role, THE Document_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 11.
5. WHEN the Document_Service evaluates authentication and authorization for any request, THE Document_Service SHALL enforce authentication before authorization such that a request failing both token validity and role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
6. THE Document_Service SHALL enforce the self-authorization prohibition of Requirement 5 independently of role membership, such that a user holding both `DOC_MASTER_MAKER` and `DOC_MASTER_CHECKER` roles is still prohibited from authorizing a change the same user created or edited.

### Requirement 13: Document Master Administrative UI (FINNOVA-49, FINNOVA-50, FINNOVA-52)

**User Story:** As a Document Master Maker and Checker, I want a Document Master screen with role-based Create/View/Edit/Authorize modes, so that I can define, review, and authorize document definitions, applicability rules, and validation/masking configuration.

#### Acceptance Criteria

1. WHEN a Maker opens the Document_Admin_UI, THE Document_Admin_UI SHALL present a searchable, paginated grid of documents with filters for Doc Category and Status and search by Doc Code or Doc Name, ordered by Doc Code ascending.
2. WHEN the Document_Admin_UI renders a Doc Category, Applicable Entity, Constitution, Entity Role, or Validation Engine selection control, THE Document_Admin_UI SHALL populate the control only from `ACTIVE` Lookup Master values and SHALL NOT allow free-text entry.
3. WHEN a Maker opens a document in View mode, THE Document_Admin_UI SHALL present all fields as read-only.
4. WHILE a document's Status is `ACTIVE`, THE Document_Admin_UI SHALL render the Doc Code control as read-only in Edit mode.
5. WHEN a Checker opens a `PENDING_APPROVAL` document in Authorize mode, THE Document_Admin_UI SHALL display the Live Values and the Pending Change values side by side.
6. IF a Maker clicks Save with a missing required field (Doc Code, Doc Name, Doc Category, or Applicable Entity), THEN THE Document_Admin_UI SHALL block submission and display a field-level validation message.
7. WHEN a Checker selects Reject, THE Document_Admin_UI SHALL require a non-empty rejection remark before submitting the reject action.
8. IF an authorize request fails with error code `ERR-DOC-001`, THEN THE Document_Admin_UI SHALL display the message "You cannot authorize a record you created or edited" and SHALL leave the document unchanged.
9. IF a delete request fails with error code `ERR-DOC-002`, THEN THE Document_Admin_UI SHALL display the message "Document is in use and cannot be deleted. You can mark it Inactive" and SHALL leave the document unchanged.
10. WHILE the authenticated user lacks the `DOC_MASTER_CHECKER` role, THE Document_Admin_UI SHALL hide or disable the Authorize and Reject controls; WHILE the authenticated user lacks the `DOC_MASTER_MAKER` role, THE Document_Admin_UI SHALL hide or disable the Create, Edit, and Delete controls.
11. IF a request to the Document_Service does not complete within 10 seconds, THEN THE Document_Admin_UI SHALL stop waiting and display a retry-able error indication to the user.
12. WHEN the Document_Admin_UI issues any request to the Document_Service, THE Document_Admin_UI SHALL include the JWT Bearer token in the Authorization header using the shared axios instance.
