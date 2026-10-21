# Requirements Document

## Introduction

The Customer Master, Customer Reference, and Mind Mappings feature (Customer Master stories FINNOVA-51 through FINNOVA-55, plus the Customer Reference feature) provides a controlled customer-data foundation for the India-only Finnova platform. The feature stores the customer attributes approved by FINNOVA-51..55, supports the customer-reference relationships required by the business, and persists structured Mind Mappings that can later be consumed by a separate AI Agent to identify relationships between one Customer, another Customer, and a Location.

The Customer Master is the authoritative source for customer identity and lifecycle data and, when required or supplied by the approved FINNOVA-51..55 contract, coordinates Aadhaar and PAN validation through the existing Finnova-Document-Verifier API. The Customer Master stores only the minimum approved verification outcome and metadata; it does not become a document repository. The existing Location Master is the authoritative source for location identity; this feature stores validated references to existing Location records rather than copying location names or hierarchy data. Customer References represent explicit, business-maintained relationships and are kept distinct from Mind Mappings. Mind Mappings are persisted relationship facts or relationship candidates with stable identifiers, normalized endpoints, provenance, lifecycle state, and timestamps so a future AI Agent can consume them without requiring a schema migration. The future AI Agent that retrieves Customer Information or performs relationship inference is explicitly outside this feature.

This feature covers both repositories:

- **Backend:** `e:\Finnova\Finnova-API`, using the existing .NET layered service conventions, EF Core persistence, API contracts, authorization, audit handling, and gateway routing.
- **Frontend:** `E:\Finnova\Finnova-UI\Finnova-UI`, using React 18, TypeScript, Material UI, the shared axios instance, feature service interfaces, real/mock implementations, the `VITE_USE_MOCK_API` toggle, and the existing master-data page patterns.

FINNOVA-51..55 are the authoritative business scope. Their full acceptance criteria and field definitions were not available in the workspace when this document was prepared. This document therefore does not invent story-specific customer fields, workflows, statuses, uniqueness keys, role permissions, or identity-document verification rules. Those unavailable details are listed as explicit assumptions and confirmation points. The baseline requirements below define the cross-cutting behavior needed for a safe customer-data foundation, the required boundary to the existing Finnova-Document-Verifier API, and the additional relationship/mapping capabilities requested by this feature; story-specific behavior must be reconciled against the source stories before implementation.

The future Customer Information AI Agent is not implemented, exposed, trained, invoked, or evaluated by this feature. This feature only stores and returns bounded, authorized data that a later agent may consume.

## Glossary

- **Customer Master**: The authoritative catalog of customer records maintained by this feature.
- **Customer**: A persisted customer record containing only the attributes approved by FINNOVA-51..55, a stable identifier, lifecycle state, and audit timestamps.
- **Customer Identifier**: The immutable system-generated identifier of a Customer. The identifier is the primary reference used by Customer References and Mind Mappings.
- **Customer Business Key**: A customer code or other business identifier used to locate a Customer. The exact key, format, uniqueness scope, and mutability are defined by FINNOVA-51..55 and remain unconfirmed until the stories are supplied.
- **Identity Document**: An Aadhaar or PAN document supplied or required under the approved FINNOVA-51..55 Customer contract.
- **Finnova-Document-Verifier API**: The existing integration used by Customer_Service to validate applicable Aadhaar and PAN documents; Customer_Service does not reimplement document validation locally.
- **Verification Outcome**: The minimum approved result of an identity-document validation, including the approved verification status, verifier reference, masked or non-sensitive metadata, and UTC timestamps.
- **Verification Status**: The controlled state returned or derived from the Finnova-Document-Verifier API, such as valid, invalid, rejected, or unavailable; the exact vocabulary remains unconfirmed.
- **Verifier Reference**: A non-sensitive identifier returned by the Finnova-Document-Verifier API that allows an authorized caller to correlate a validation without storing raw document content.
- **Document Validation Failure**: A required Aadhaar or PAN validation that fails, is rejected, returns an invalid or mismatched result, or cannot be safely completed within the approved dependency policy.
- **Customer-to-Customer Reference**: A Customer Reference whose source and target endpoints are two distinct Customer records.
- **Customer-to-Location Reference**: A Customer Reference whose source endpoint is a Customer and whose target endpoint is an existing Location record.
- **Reference Type**: A controlled classification describing the meaning of a Customer Reference. The source and allowed values remain unconfirmed and SHALL not be silently invented.
- **Mind Mapping**: A persisted, structured relationship record intended to represent a known or candidate relation among a Customer, another Customer, and optionally a Location, together with provenance and lifecycle metadata for future AI consumption.
- **Mind Mapping Tuple**: The normalized relationship endpoints in a Mind Mapping: `CustomerId`, `RelatedCustomerId`, and `LocationId` when a location is part of the relation.
- **Mapping Status**: The lifecycle state of a Mind Mapping, such as active, superseded, rejected, or archived. The exact allowed values require confirmation.
- **Provenance**: Metadata identifying how a Customer Reference or Mind Mapping was created or supplied, such as an administrator, import process, source system, or future AI process.
- **Source System**: The system or process that originated a relationship or mapping. A source-system identifier is metadata and is not a replacement for validated Customer or Location foreign keys.
- **Location**: An existing Location Master record. The current backend Location entity uses a Guid identifier, code, name, level, optional parent, active flag, and UTC timestamps.
- **System Administrator**: An authenticated user authorized by the existing or confirmed SystemAdmin policy to manage customer data, references, and mappings.
- **Authorized Consumer**: An authenticated caller explicitly granted read access to the minimum customer, reference, or mapping data needed for an approved business function.
- **Customer_Service**: The backend API/application service responsible for Customer Master operations.
- **CustomerReference_Service**: The backend API/application service responsible for Customer Reference operations.
- **MindMapping_Service**: The backend API/application service responsible for Mind Mapping persistence and authorized retrieval.
- **Customer_Admin_UI**: The React/TypeScript administrative UI for Customer Master, Customer References, and Mind Mappings.
- **PaginatedResponse**: The platform response contract containing page items, total count, page number, and page size, with any additional fields established by the existing shared contract.
- **Audit Entry**: An immutable record describing a permitted create, update, lifecycle change, or relationship change, including the affected record, changed values, actor/provenance, and UTC timestamp.
- **PII**: Personally identifiable information or other customer data that can identify or materially describe a person or organization.
- **Finnova Platform**: The combined Finnova backend services, API gateway, and authorized frontend clients that enforce the platform's shared security and data-contract conventions.
- **EARS**: Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.

## Dependencies and Assumptions

### Dependencies

- **FINNOVA-51..55 business source:** Customer Master stories FINNOVA-51 through FINNOVA-55 define the authoritative customer fields, workflows, validations, lifecycle, uniqueness, and acceptance behavior. The story text was unavailable during requirements preparation and must be supplied before implementation planning is finalized.
- **Finnova-Document-Verifier integration:** Customer create/update behavior depends on the existing Finnova-Document-Verifier API when Aadhaar or PAN is required or supplied by the approved FINNOVA-51..55 contract. The verifier's endpoint, schema, authentication, status vocabulary, matching rules, timeout, cancellation, retry, and idempotency behavior require confirmation.
- **Location Master:** Customer-to-Location References and Mind Mappings depend on the existing Location Master and its stable `Location.Id` identifier. The feature does not create, modify, duplicate, or infer Location records.
- **Authentication and authorization:** The feature depends on the JWT Bearer authentication scheme and the SystemAdmin authorization policy used by the platform. Exact role/policy names and whether authorized non-admin consumers may read projections require confirmation against the deployed host configuration.
- **Audit infrastructure:** Existing master specifications use feature-scoped immutable audit trails. This feature depends on the platform's established audit conventions or introduces equivalent scoped audit persistence for Customer, Customer Reference, and Mind Mapping changes.
- **Backend host and gateway:** The feature is expected to live in the existing SystemAdmin host and be reachable through the Finnova.ApiGateway. The final route prefix and any UI-facing alias must follow the deployed gateway configuration.
- **Frontend repository:** UI artifacts belong in `E:\Finnova\Finnova-UI\Finnova-UI`; no UI source is part of this requirements-only deliverable.

### Assumptions for Confirmation

- **A1 — Story details unavailable:** FINNOVA-51..55 are authoritative, but their field lists and acceptance criteria were not available in the workspace. No implementation SHALL finalize story-specific fields or behavior until the stories are supplied and traced to these requirements.
- **A2 — Customer scope:** The baseline Customer Master supports the create, modify, query/read, and lifecycle operations explicitly present in FINNOVA-51..55. The exact operation-to-story mapping is unconfirmed; hard deletion, deactivation, approval, merge, duplicate detection, and bulk import SHALL not be assumed unless a story requires them.
- **A3 — Customer identity:** The system-generated Customer Identifier is immutable. The business key, whether it is required, its format, uniqueness scope, normalization, and mutability are all governed by FINNOVA-51..55 and are currently unconfirmed.
- **A4 — Customer attributes and identity documents:** Only fields explicitly approved by FINNOVA-51..55 will be persisted or returned. Aadhaar and PAN are in scope for validation when either document is required or supplied by the approved contract. The feature does not store raw Aadhaar/PAN images or documents, authentication tokens, or unnecessary full document numbers; it stores only the minimum approved Verification Outcome unless FINNOVA-51..55 explicitly requires additional data.
- **A5 — Roles:** System Administrators may manage Customer records, references, mappings, and applicable document-verification workflows. Read access for other authenticated roles is not assumed; an approved Authorized Consumer policy must be confirmed before any broader read surface is exposed.
- **A6 — Customer Reference shape:** A Customer Reference is proposed to contain a source Customer, a Reference Type, exactly one target kind, and either a target Customer or a target Location, plus lifecycle and audit metadata. Whether references may include both targets, whether self-references are allowed, and the allowed Reference Type values require confirmation.
- **A7 — Reference direction and duplicates:** Customer References are proposed to be directed and unique by normalized source, target, and Reference Type while active. Whether a business relationship must be represented as directed or symmetric, and whether historical duplicates are allowed, require confirmation.
- **A8 — Location eligibility:** A Customer-to-Location Reference and a Mind Mapping Location endpoint must reference an existing Location. Whether inactive locations are permitted and which Location levels are valid require confirmation; the safe baseline is to permit only existing active locations.
- **A9 — Mind Mapping cardinality:** A Mind Mapping is proposed to support a Customer-to-Customer relation with an optional Location context. Whether a mapping without a Location is valid, whether one mapping must always include both Customers, and whether multiple mappings may share the same tuple require confirmation.
- **A10 — Mapping semantics:** The allowed relation type, status, confidence/score, rationale, source system, and evidence fields are not specified. The implementation SHALL use a versioned/controlled vocabulary and bounded optional metadata only after those values and limits are confirmed.
- **A11 — Mapping authorship:** This feature persists mappings supplied by authorized users or approved source processes. It does not call an AI model, generate a relation, calculate a confidence score, or claim that a mapping is true. AI-generated mappings, if later introduced, require explicit provenance and a review status.
- **A12 — Deletion policy:** Customer records and relationship records are proposed to use deactivation/archival rather than hard deletion where history or foreign-key references exist. The exact delete/deactivate behavior is a FINNOVA-51..55 and Customer Reference decision requiring confirmation.
- **A13 — Audit granularity:** Create, update, lifecycle, and reference/mapping changes are proposed to produce immutable audit entries. Whether reads, failed requests, exports, and privacy events require separate audit events requires confirmation with security/compliance stakeholders.
- **A14 — Privacy retention:** Customer PII, relationship records, audit entries, and mappings are retained only for the approved business and legal retention period. The retention period, erasure/legal-hold behavior, and export controls are not specified and require confirmation.
- **A15 — Search behavior:** Customer and relationship search is proposed to use only explicitly approved searchable fields and paginated results. Case sensitivity, partial matching, exact matching, ranking, and maximum search lengths require confirmation from FINNOVA-51..55.
- **A16 — UI workflow:** The frontend will mirror existing MUI master pages and service-toggle patterns. Exact screens, columns, wizard/dialog flows, permission-based controls, and whether Mind Mappings are directly editable require confirmation after the story details are supplied.
- **A17 — Integration contract:** Future AI consumption is expected to use versioned API contracts or read-only projections containing stable IDs and provenance. No event bus, vector store, embeddings, model provider, or AI-specific infrastructure is included unless separately approved.
- **A18 — Localization:** Finnova remains India-only and English-only. Customer labels, reference types, statuses, validation messages, sample data, and tests use English values only. No bilingual fields, translation scaffolding, Arabic/non-English values, or RTL behavior are included.
- **A19 — Verifier contract unknown:** The Finnova-Document-Verifier API endpoint, request/response schema, authentication mechanism, status vocabulary, document matching rules, verifier reference format, and whether Aadhaar, PAN, or both documents are mandatory are unknown. These values SHALL be confirmed from the existing integration and FINNOVA-51..55 before design finalization; this requirements document intentionally does not invent them.
- **A20 — Verification dependency policy unknown:** The approved timeout, cancellation behavior, retry/backoff policy, idempotency key or deduplication behavior, transient-versus-permanent failure classification, and safe error-code mapping for Finnova-Document-Verifier are unknown and require confirmation. A required validation failure, rejection, invalid result, mismatched result, timeout, cancellation, or unavailable dependency will not create or update a Customer as verified until the approved policy is defined.
- **A21 — Verification retention and presentation:** The approved Verification Outcome fields, retention period, masking rules, authorized readers, audit treatment, and UI status labels require confirmation. The baseline is to persist and display only minimum non-sensitive outcome data and a safe retry path, without raw Aadhaar/PAN content or unnecessary full document numbers.

## Requirements

### Requirement 1: Preserve FINNOVA-51..55 as the Customer Master Authority

**User Story:** As a product owner, I want Customer Master behavior to follow FINNOVA-51..55, so that implementation does not introduce unsupported customer fields or workflows.

#### Acceptance Criteria

1. THE Customer_Service SHALL treat FINNOVA-51..55 as the authority for Customer fields, operations, lifecycle states, and validations.
2. WHEN the complete FINNOVA-51..55 acceptance criteria are supplied, THE Customer_Service SHALL map every story acceptance criterion to at least one numbered requirement before implementation tasks are created.
3. IF a Customer behavior is absent from FINNOVA-51..55 and absent from an approved assumption, THEN THE Customer_Service SHALL exclude that behavior from the implementation scope.
4. IF a FINNOVA-51..55 story detail conflicts with a baseline requirement, THEN THE Customer_Service SHALL apply the story detail only after recording the conflict and revising the affected requirement.
5. THE Customer_Service SHALL use one stable Customer Identifier for every Customer Reference and Mind Mapping Customer relationship.

**Unresolved story details:** The complete field list, operation list, lifecycle model, business-key rules, validation limits, duplicate behavior, authorization matrix, and audit expectations for FINNOVA-51..55 must be provided for confirmation.

### Requirement 2: Create and Maintain Customer Records

**User Story:** As an authorized administrator, I want to maintain approved Customer records, so that business modules use consistent customer data.

#### Acceptance Criteria

1. WHEN an authorized caller submits a Customer create request containing all fields required by FINNOVA-51..55, THE Customer_Service SHALL validate the request before persistence.
2. WHEN a valid Customer create request is accepted, THE Customer_Service SHALL persist exactly one Customer record.
3. WHEN a Customer create succeeds, THE Customer_Service SHALL return the persisted Customer with its stable Customer Identifier, resolved lifecycle values, and audit timestamps.
4. WHEN an authorized caller submits an update for an existing Customer with fields approved by FINNOVA-51..55, THE Customer_Service SHALL validate the update before persistence.
5. WHEN a valid Customer update is accepted, THE Customer_Service SHALL persist all approved changes atomically.
6. WHEN a Customer update succeeds, THE Customer_Service SHALL return the resulting Customer with its stable Customer Identifier and audit timestamps.
7. IF a Customer create or update request contains an omitted, empty, malformed, over-length, unsupported, or mutually inconsistent value defined as invalid by FINNOVA-51..55, THEN THE Customer_Service SHALL return field-level validation errors and SHALL persist no partial change.
8. IF a Customer update targets a Customer Identifier that does not exist, THEN THE Customer_Service SHALL return a not-found error and SHALL preserve all existing Customer records.
9. WHERE FINNOVA-51..55 defines a default Customer field value, THE Customer_Service SHALL apply the default only when the request omits that field.
10. WHERE FINNOVA-51..55 defines an immutable Customer field, THE Customer_Service SHALL reject any request that changes that field.
11. WHEN a Customer create or update succeeds, THE Customer_Service SHALL store and return timestamps in the platform's established UTC representation.
12. WHERE an Aadhaar or PAN document is required or supplied by the approved FINNOVA-51..55 contract, THE Customer_Service SHALL apply the validation, persistence, and failure rules defined in Requirement 14 before persisting a new Customer or applying a Customer update.
13. IF an applicable Aadhaar or PAN validation fails, is rejected, returns an invalid or mismatched result, or cannot be completed under the approved verifier dependency policy, THEN THE Customer_Service SHALL apply the failure behavior defined in Requirement 14 and SHALL not mark the Customer as verified.
14. WHEN the approved Finnova-Document-Verifier validation succeeds, THE Customer_Service SHALL persist only the approved Verification Outcome fields and SHALL not persist raw document content or authentication tokens.

**Assumption:** This requirement deliberately leaves Customer field names, defaults, lengths, identity keys, lifecycle operations, and the exact Aadhaar/PAN verification contract to FINNOVA-51..55 and the existing Finnova-Document-Verifier integration rather than inventing them.

### Requirement 3: Customer Identity, Uniqueness, and Lifecycle Integrity

**User Story:** As a data owner, I want Customer identity rules enforced consistently, so that relationships do not point to ambiguous or duplicated Customers.

#### Acceptance Criteria

1. THE Customer_Service SHALL enforce the Customer business-key uniqueness rule defined by FINNOVA-51..55.
2. THE Customer_Service SHALL apply the business-key normalization and uniqueness scope defined by FINNOVA-51..55.
3. IF a Customer create or update request conflicts with an existing Customer under the approved business-key rule, THEN THE Customer_Service SHALL return the platform's conflict or validation error.
4. IF a Customer create or update request conflicts with an existing Customer, THEN THE Customer_Service SHALL preserve the existing Customer records.
5. IF concurrent Customer create requests resolve to the same approved unique business key, THEN THE Customer_Service SHALL persist no more than one Customer for that key.
6. IF a concurrent Customer create request loses the approved unique-key conflict, THEN THE Customer_Service SHALL return the same deterministic conflict outcome as any other duplicate request.
7. WHERE FINNOVA-51..55 defines a deactivation or archival lifecycle, THE Customer_Service SHALL retain the Customer Identifier and relationship history when applying that lifecycle state.
8. IF a requested Customer lifecycle transition is not allowed by FINNOVA-51..55, THEN THE Customer_Service SHALL reject the transition and SHALL preserve the existing Customer state.
9. IF FINNOVA-51..55 does not explicitly require hard deletion, THEN THE Customer_Service SHALL not expose a hard-delete operation for Customer records.

**Assumption:** A stable identity and history-preserving lifecycle are required for reliable Customer References and future Mind Mapping consumption, but the exact business-key and lifecycle rules remain unconfirmed.

### Requirement 4: Query and Search Customer Records

**User Story:** As an authorized consumer, I want to find Customers using approved search fields, so that I can select the correct Customer when maintaining relationships.

#### Acceptance Criteria

1. WHEN an authorized caller supplies an approved Customer search term, THE Customer_Service SHALL search only fields approved for Customer search by FINNOVA-51..55.
2. WHEN an authorized caller supplies an approved Customer search term, THE Customer_Service SHALL apply the matching semantics defined by FINNOVA-51..55.
3. WHEN an authorized caller requests Customers without a search term, THE Customer_Service SHALL apply the approved lifecycle filters.
4. WHEN an authorized caller requests Customers, THE Customer_Service SHALL return a `PaginatedResponse` containing the page items, total count, page number, and page size.
5. IF a Customer query contains an invalid page number, page size, filter, or search value under the approved query contract, THEN THE Customer_Service SHALL return a validation error.
6. IF a Customer query contains an invalid query parameter, THEN THE Customer_Service SHALL not return a partial result for that request.
7. WHEN a Customer query matches no records, THE Customer_Service SHALL return a successful empty page with a total count of zero.
8. THE Customer_Service SHALL order Customer query results deterministically using fields approved by FINNOVA-51..55.
9. THE Customer_Service SHALL use the stable Customer Identifier as a tie-breaker when approved ordering fields contain equal values.
10. IF an authorized caller requests a Customer by an unknown Customer Identifier, THEN THE Customer_Service SHALL return the platform's not-found response.
11. IF a caller supplies a malformed Customer Identifier, THEN THE Customer_Service SHALL return the platform's validation response.
12. IF a caller requests a Customer by an unknown or malformed Customer Identifier, THEN THE Customer_Service SHALL not disclose unrelated Customer data.

**Assumption:** Default page size, maximum page size, searchable fields, ordering, active/inactive filtering, and exact-match versus substring behavior require confirmation from FINNOVA-51..55.

### Requirement 5: Create and Maintain Customer References

**User Story:** As an authorized administrator, I want to record explicit relationships between Customers and Locations, so that downstream modules can use maintained business references rather than inferred or duplicated data.

#### Acceptance Criteria

1. WHEN an authorized caller submits a valid Customer-to-Customer Reference with an existing source Customer, an existing target Customer, and an approved Reference Type, THE CustomerReference_Service SHALL validate the request.
2. WHEN a valid Customer-to-Customer Reference request is accepted, THE CustomerReference_Service SHALL persist exactly one Customer Reference.
3. WHEN a Customer-to-Customer Reference is persisted, THE CustomerReference_Service SHALL return its stable identifier, normalized Customer endpoints, lifecycle state, provenance, and UTC timestamps.
4. WHEN an authorized caller submits a valid Customer-to-Location Reference with an existing source Customer, an existing Location, and an approved Reference Type, THE CustomerReference_Service SHALL validate the request.
5. WHEN a valid Customer-to-Location Reference request is accepted, THE CustomerReference_Service SHALL persist exactly one Customer Reference.
6. WHEN a Customer-to-Location Reference is persisted, THE CustomerReference_Service SHALL return its stable Customer and Location endpoints, lifecycle state, provenance, and UTC timestamps.
7. IF a Customer Reference request contains a source Customer, target Customer, or Location Identifier that does not exist, THEN THE CustomerReference_Service SHALL return a reference-validation error and SHALL persist no reference.
8. IF a Customer Reference request contains an unapproved endpoint kind, THEN THE CustomerReference_Service SHALL return a validation error and SHALL persist no reference.
9. IF a Customer Reference request contains an unapproved Reference Type, THEN THE CustomerReference_Service SHALL return a validation error and SHALL persist no reference.
10. IF a Customer Reference request violates an approved self-reference, target-cardinality, directionality, or duplicate rule, THEN THE CustomerReference_Service SHALL reject the request and SHALL preserve existing references.
11. WHEN an authorized caller updates an editable Customer Reference field approved by the reference contract, THE CustomerReference_Service SHALL persist the change atomically.
12. WHEN a Customer Reference update succeeds, THE CustomerReference_Service SHALL return the updated reference with normalized endpoints and UTC timestamps.
13. WHERE the approved Customer Reference lifecycle supports deactivation, THE CustomerReference_Service SHALL preserve the reference identifier and audit history when marking the reference inactive.
14. WHERE a Customer Reference points to an inactive Customer or Location, THE CustomerReference_Service SHALL apply the approved inactive-endpoint policy.
15. WHERE a Customer Reference points to an inactive Customer or Location, THE CustomerReference_Service SHALL preserve the stored endpoint identifier.

**Assumptions:** The proposed baseline permits either Customer-to-Customer or Customer-to-Location references, not a reference containing both targets at once; references are directed; self-references are rejected; and active endpoints are required for new references. These decisions require confirmation.

### Requirement 6: Query Customer References and Resolve Reference Endpoints

**User Story:** As an authorized consumer, I want to retrieve Customer References by Customer or Location, so that business workflows can navigate maintained relationships safely.

#### Acceptance Criteria

1. WHEN an authorized caller requests references for a Customer, THE CustomerReference_Service SHALL return only references whose source or target endpoint matches the Customer under the approved directionality rule.
2. WHEN an authorized caller requests references for a Location, THE CustomerReference_Service SHALL return only Customer-to-Location References associated with that Location.
3. WHEN the CustomerReference_Service returns a Customer Reference, THE CustomerReference_Service SHALL include stable endpoint identifiers.
4. WHEN the CustomerReference_Service returns a Customer Reference, THE CustomerReference_Service SHALL resolve endpoint display fields from the current authorized Customer and Location records.
5. WHEN an authorized caller supplies approved Customer Reference filters, THE CustomerReference_Service SHALL apply each filter according to the reference query contract.
6. WHEN an authorized caller requests a paginated Customer Reference result, THE CustomerReference_Service SHALL return a deterministic `PaginatedResponse`.
7. IF a Customer Reference query targets an unknown Customer Identifier, THEN THE CustomerReference_Service SHALL return the approved not-found or empty-result response.
8. IF a Customer Reference query targets an unknown Location Identifier, THEN THE CustomerReference_Service SHALL return the approved not-found or empty-result response.
9. IF a Customer Reference query targets an unknown Customer or Location Identifier, THEN THE CustomerReference_Service SHALL not disclose unrelated references.
10. IF a Customer or Location endpoint becomes inactive or archived, THEN THE CustomerReference_Service SHALL preserve the stored endpoint identifier.
11. IF a Customer or Location endpoint becomes inactive or archived, THEN THE CustomerReference_Service SHALL return the endpoint lifecycle state to an authorized consumer.

**Assumption:** Both administrative relationship browsing and narrow read projections for approved consumers are needed, but the exact read roles and endpoint shapes require confirmation.

### Requirement 7: Persist Mind Mappings for Future AI Consumption

**User Story:** As a data platform owner, I want to persist structured Mind Mappings, so that a future AI Agent can identify and retrieve relationships without changing the core Customer schema.

#### Acceptance Criteria

1. WHEN an authorized caller submits a Mind Mapping with a valid Customer Identifier, a valid Related Customer Identifier, and an optional valid Location Identifier permitted by the approved mapping contract, THE MindMapping_Service SHALL validate every endpoint before persistence.
2. WHEN a valid Mind Mapping request is accepted, THE MindMapping_Service SHALL persist exactly one Mind Mapping record.
3. THE MindMapping_Service SHALL store the Mind Mapping Tuple as normalized Customer and Location foreign-key identifiers.
4. THE MindMapping_Service SHALL not use copied Customer or Location names as authoritative Mind Mapping endpoints.
5. THE MindMapping_Service SHALL persist a controlled relation type or relation code approved by the mapping contract.
6. THE MindMapping_Service SHALL persist a Mapping Status approved by the mapping contract.
7. THE MindMapping_Service SHALL persist provenance metadata that identifies the approved source of the mapping.
8. WHERE the approved mapping contract includes a confidence or certainty value, THE MindMapping_Service SHALL validate the value against its defined numeric range.
9. WHERE the approved mapping contract includes a confidence or certainty value, THE MindMapping_Service SHALL preserve whether the value was supplied, calculated, or unavailable.
10. WHERE the approved mapping contract includes evidence or rationale, THE MindMapping_Service SHALL enforce the approved length and content bounds.
11. IF a Mind Mapping request references an unknown Customer, Related Customer, or Location Identifier, THEN THE MindMapping_Service SHALL return a validation error and SHALL persist no mapping.
12. IF a Mind Mapping request violates an approved self-reference, endpoint-cardinality, relation-type, duplicate, or status rule, THEN THE MindMapping_Service SHALL reject the request and SHALL preserve existing mappings.
13. WHEN a Mind Mapping is persisted, THE MindMapping_Service SHALL assign a stable mapping identifier.
14. WHEN a Mind Mapping is persisted, THE MindMapping_Service SHALL assign the approved schema or contract version.
15. THE MindMapping_Service SHALL not treat persistence as factual verification of a Mind Mapping.

**Assumptions:** The baseline supports a Customer-to-Customer relationship with an optional Location context, stable GUID-based endpoints, controlled relation/status values, optional bounded provenance, and an explicit contract version. The exact relation vocabulary, confidence model, duplicate key, and whether a Location is mandatory remain unconfirmed.

### Requirement 8: Query, Filter, and Export Mind Mappings Safely

**User Story:** As an authorized consumer, I want to retrieve Mind Mappings in a stable contract, so that future relationship analysis can use the data without exposing unnecessary Customer information.

#### Acceptance Criteria

1. WHEN an authorized caller requests Mind Mappings, THE MindMapping_Service SHALL return stable mapping identifiers, normalized endpoint identifiers, approved relation and status fields, provenance, contract version, and UTC timestamps.
2. WHEN an authorized caller filters Mind Mappings by Customer, Related Customer, Location, relation type, status, provenance, or contract version, THE MindMapping_Service SHALL apply only filters defined by the approved mapping contract.
3. WHEN an authorized caller requests Mind Mappings, THE MindMapping_Service SHALL return results in the deterministic order defined by the approved API contract.
4. WHEN an authorized caller requests a paginated Mind Mapping result, THE MindMapping_Service SHALL return a `PaginatedResponse` using the approved page parameters.
5. WHEN a future AI consumer is granted Mind Mapping access, THE MindMapping_Service SHALL expose only the minimum fields approved for that consumer.
6. WHEN a future AI consumer requests Mind Mappings, THE MindMapping_Service SHALL enforce the consumer's approved authorization scope.
7. IF a caller lacks permission to read a Customer, Related Customer, Location, or Mind Mapping, THEN THE MindMapping_Service SHALL omit or mask the unauthorized value according to the approved privacy policy.
8. IF a caller lacks permission to read a Customer, Related Customer, Location, or Mind Mapping, THEN THE MindMapping_Service SHALL not reveal the protected value through errors, counts, ordering, or free-text metadata.
9. IF a Mind Mapping is superseded, rejected, archived, or otherwise inactive, THEN THE MindMapping_Service SHALL preserve the record for authorized history access.
10. IF a Mind Mapping is inactive and the caller does not request its status, THEN THE MindMapping_Service SHALL exclude the record from active relationship queries.
11. WHERE a Mind Mapping export or bulk retrieval operation is approved, THE MindMapping_Service SHALL require explicit authorization before returning the operation's data.
12. WHERE a Mind Mapping export or bulk retrieval operation is approved, THE MindMapping_Service SHALL apply approved purpose, field-minimization, row-limit, and audit rules.

**Assumption:** A future AI Agent will consume a versioned read-only projection rather than direct database access. No bulk export is included unless explicitly approved.

### Requirement 9: Audit Customer, Reference, and Mapping Changes

**User Story:** As a security and data-governance stakeholder, I want changes to Customer relationships and mappings to be traceable, so that the platform can investigate who changed relationship data and why.

#### Acceptance Criteria

1. WHEN the Customer_Service persists a Customer create, update, deactivation, archival, or other approved lifecycle change, THE Customer_Service SHALL persist exactly one immutable Audit Entry for that change.
2. WHEN a Customer change is audited, THE Customer_Service SHALL include the affected Customer Identifier, action, changed fields or approved field snapshot, acting identity, provenance, and UTC timestamp in the Audit Entry.
3. WHEN the CustomerReference_Service persists a Customer Reference create, update, deactivation, or archival, THE CustomerReference_Service SHALL persist exactly one immutable Audit Entry for that change.
4. WHEN a Customer Reference change is audited, THE CustomerReference_Service SHALL include the reference identifier, normalized endpoints, action, changed values, acting identity, provenance, and UTC timestamp in the Audit Entry.
5. WHEN the MindMapping_Service persists a Mind Mapping create, update, status transition, supersession, or archival, THE MindMapping_Service SHALL persist exactly one immutable Audit Entry for that change.
6. WHEN a Mind Mapping change is audited, THE MindMapping_Service SHALL include the mapping identifier, endpoint identifiers, action, changed values, provenance, contract version, acting identity where applicable, and UTC timestamp in the Audit Entry.
7. IF a Customer, Customer Reference, or Mind Mapping request is rejected by authentication, authorization, validation, uniqueness, referential-integrity, or lifecycle rules, THEN the relevant service SHALL persist no business-data change.
8. IF a Customer, Customer Reference, or Mind Mapping request is rejected, THEN the relevant service SHALL persist no ordinary change Audit Entry for that rejected operation.
9. IF a business-data change and its required Audit Entry cannot be persisted atomically, THEN the relevant service SHALL roll back the business-data change.
10. IF a business-data change and its required Audit Entry cannot be persisted atomically, THEN the relevant service SHALL return an error indicating that the change was not recorded.
11. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL prevent modification of previously recorded Audit Entries through feature APIs.
12. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL prevent deletion of previously recorded Audit Entries through feature APIs.
13. WHEN an authorized auditor requests history for a Customer, Customer Reference, or Mind Mapping, THE relevant service SHALL return entries in deterministic reverse chronological order.
14. WHEN audit entries have equal timestamps, THE relevant service SHALL use the stable audit identifier as the ordering tie-breaker.

**Assumption:** Create and lifecycle changes are audited in addition to field updates. Read/access logging, failed-request security events, privacy events, and retention schedules require separate confirmation.

### Requirement 10: Authentication, Authorization, and Privacy Enforcement

**User Story:** As a security stakeholder, I want Customer data and relationship data protected by authentication, authorization, and minimization, so that sensitive information is disclosed only for an approved purpose.

#### Acceptance Criteria

1. IF a Customer, Customer Reference, Mind Mapping, or audit request is received without a valid JWT Bearer token, THEN the applicable service SHALL return an unauthorized response.
2. IF a request is received without a valid JWT Bearer token, THEN the applicable service SHALL disclose no Customer, relationship, mapping, or audit data.
3. IF an authenticated caller lacks the role or policy required for the requested operation, THEN the applicable service SHALL return a forbidden response.
4. IF an authenticated caller lacks the role or policy required for the requested operation, THEN the applicable service SHALL disclose no protected data.
5. WHEN a request fails both authentication and authorization, THE Finnova Platform SHALL return the authentication failure response before role authorization evaluation.
6. THE Customer_Service SHALL authorize Customer management operations separately from Customer read operations.
7. THE CustomerReference_Service SHALL authorize Customer Reference management operations separately from Customer Reference read operations.
8. THE MindMapping_Service SHALL authorize Mind Mapping management operations separately from Mind Mapping read operations.
9. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL collect only fields approved for the request purpose and caller authorization scope.
10. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL return only fields approved for the request purpose and caller authorization scope.
11. IF a response contains Customer PII or sensitive relationship metadata, THEN the applicable service SHALL apply the approved field-level minimization and masking rules before serialization.
12. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL not place secrets, raw authentication tokens, unbounded Customer documents, or unnecessary PII in audit entries, provenance fields, error messages, or logs.
13. WHEN a caller requests a Customer Reference or Mind Mapping, THE applicable service SHALL enforce authorization checks for every referenced Customer and Location before returning endpoint details.
14. WHERE approved retention, legal hold, correction, or erasure rules apply to Customer data, THE applicable service SHALL execute only the approved governance action.
15. WHERE approved retention, legal hold, correction, or erasure rules apply to Customer data, THE applicable service SHALL preserve records required by audit or legal-hold policy.
16. WHEN the Customer_Service calls the Finnova-Document-Verifier API, THE Customer_Service SHALL use the approved service authentication mechanism and SHALL transmit only the minimum document data required by the approved verifier contract.
17. IF a verifier request, response, error, or log contains Aadhaar/PAN data or verifier credentials, THEN the Finnova Platform SHALL prevent raw document content, unnecessary full document numbers, authentication tokens, and unbounded PII from appearing in Customer responses, audit entries, error messages, or logs.
18. IF a caller lacks authorization to initiate, view, or retry identity-document verification, THEN the applicable service SHALL return the approved unauthorized or forbidden response without disclosing document data or verifier details.
19. WHEN a verification dependency failure is returned to an authorized caller, THE applicable service SHALL expose only a safe retryable status and approved English error message without revealing PII or internal verifier response content.

**Assumptions:** System Administrators manage all three data sets and applicable document-verification workflows; broader read access is opt-in and policy-based; and privacy, retention, verifier credential, and field-level masking rules will be confirmed with the product, security, and compliance owners before implementation.

### Requirement 11: Backend Contracts, API Consistency, and Integration Boundaries

**User Story:** As a backend integrator, I want stable, explicit contracts for Customers, references, and mappings, so that the frontend and a future AI consumer can integrate without coupling to persistence details.

#### Acceptance Criteria

1. THE backend SHALL expose separate contracts for Customer records, Customer References, Mind Mappings, audit entries, create requests, update requests, query parameters, and paginated responses.
2. THE backend SHALL represent Customer, Related Customer, and Location relationships with stable identifiers and explicit endpoint names.
3. THE backend SHALL represent an optional Customer Reference or Mind Mapping endpoint as a nullable field or approved discriminated endpoint shape.
4. IF a request supplies incompatible optional endpoint combinations, THEN the applicable backend service SHALL return a validation error.
5. THE backend SHALL include contract version information in Mind Mapping responses.
6. THE backend SHALL preserve backward interpretation for Mind Mapping records created under each earlier supported contract version.
7. THE backend SHALL use the existing shared HTTP error and ProblemDetails conventions for Customer, Customer Reference, Mind Mapping, and Location validation failures.
8. THE backend SHALL include field-level validation details when the shared error contract supports field-level details.
9. THE backend SHALL use stable feature error codes when the shared error contract supports feature error codes.
10. THE backend SHALL route Customer, Customer Reference, Mind Mapping, and Location-related requests through the existing API gateway.
11. THE backend SHALL preserve host separation between the SystemAdmin service and the Location Master host.
12. THE backend SHALL not expose direct database access or internal entity navigation graphs through Customer, Customer Reference, or Mind Mapping contracts.
13. THE backend SHALL not expose unapproved raw PII through Customer, Customer Reference, or Mind Mapping contracts.
14. THE backend SHALL treat Location as the authoritative external reference for Customer-to-Location References and Mind Mappings.
15. WHEN a Customer Reference or Mind Mapping contains a Location Identifier, THE backend SHALL validate that identifier through the approved Location repository or service boundary.
16. WHEN a Customer create or update request requires or supplies an Aadhaar or PAN document under FINNOVA-51..55, THE backend SHALL define an integration boundary to the existing Finnova-Document-Verifier API rather than implementing document validation locally.
17. THE backend SHALL represent only the approved Verification Outcome fields, including status, verifier reference, masked or non-sensitive metadata, and timestamps, in Customer contracts.
18. THE backend SHALL not expose raw Aadhaar/PAN images or documents, authentication tokens, or unnecessary full document numbers through Customer, audit, error, log, or verifier-facing contracts unless FINNOVA-51..55 explicitly requires a field.
19. IF the Finnova-Document-Verifier API returns a rejected, invalid, mismatched, timed-out, cancelled, unavailable, or otherwise unprocessable result, THEN the backend SHALL return the approved validation or retryable dependency error and SHALL not persist or update a Customer as verified.
20. WHEN the backend calls the Finnova-Document-Verifier API, THE backend SHALL enforce the approved authentication, timeout, cancellation, retry, and idempotency policy.
21. THE backend SHALL preserve the verifier endpoint, request/response schema, authentication mechanism, status vocabulary, matching rules, retry policy, and document-mandatory policy as confirmed integration configuration or contract documentation rather than inventing undocumented values.

**Assumption:** Customer, Customer Reference, Mind Mapping, and identity-document verification APIs will be hosted with SystemAdmin functionality and will use gateway aliases if the shared UI axios prefix requires them; the final route names, error-code family, verifier integration contract, and dependency policy require design confirmation.

### Requirement 12: Customer Administration and Relationship UI

**User Story:** As a System Administrator, I want a safe administrative UI for Customers, References, and Mind Mappings, so that I can maintain approved data and inspect relationships without editing raw identifiers incorrectly.

#### Acceptance Criteria

1. WHEN a System Administrator opens the Customer_Admin_UI, THE UI SHALL present only Customer fields and operations approved by FINNOVA-51..55.
2. WHEN a System Administrator creates or edits a Customer, THE UI SHALL validate required, format, length, and cross-field rules approved by FINNOVA-51..55 before submission.
3. IF a Customer save response contains field-level validation errors, THEN the Customer_Admin_UI SHALL display each error at the relevant field or section.
4. WHEN a System Administrator manages a Customer Reference, THE Customer_Admin_UI SHALL provide controlled Customer selection using authorized Customer service data.
5. WHEN a System Administrator manages a Customer-to-Location Reference, THE Customer_Admin_UI SHALL provide controlled Location selection using authorized Location service data.
6. IF a Customer Reference form contains an invalid endpoint combination, THEN the Customer_Admin_UI SHALL prevent submission.
7. WHEN a System Administrator manages a Mind Mapping, THE Customer_Admin_UI SHALL display only relation type, endpoint, status, provenance, and confidence controls approved by the mapping contract.
8. THE Customer_Admin_UI SHALL not present AI inference controls unless a later approved story adds those controls.
9. WHEN a Customer, Customer Reference, or Mind Mapping save succeeds, THE Customer_Admin_UI SHALL refresh the relevant view from the service response.
10. IF a create, update, query, or relationship request fails, THEN the Customer_Admin_UI SHALL preserve unsaved user input where safe.
11. IF a create, update, query, or relationship request fails, THEN the Customer_Admin_UI SHALL display a clear English error without protected server data.
12. IF a service request exceeds the platform's approved timeout, THEN the Customer_Admin_UI SHALL display a retryable error state.
13. WHEN the Customer_Admin_UI calls a feature service, THE Customer_Admin_UI SHALL use the shared axios instance.
14. WHEN the Customer_Admin_UI calls a feature service, THE Customer_Admin_UI SHALL select the real or mock implementation through `VITE_USE_MOCK_API`.
15. THE Customer_Admin_UI SHALL implement explicit loading, empty, success, validation-error, authorization-error, and retry states for Customer, Customer Reference, and Mind Mapping views.
16. THE Customer_Admin_UI SHALL render English labels and values in left-to-right layouts.
17. THE Customer_Admin_UI SHALL not render bilingual fields, Arabic or other non-English seed values, or RTL handling.
18. WHEN the Customer_Admin_UI creates or edits a Customer with Aadhaar or PAN required or supplied by the approved contract, THE Customer_Admin_UI SHALL display the approved verification status and SHALL submit through the Customer service without exposing raw document content.
19. IF identity-document verification fails, is rejected, returns an invalid or mismatched result, or encounters a retryable dependency failure, THEN the Customer_Admin_UI SHALL display the approved safe English validation or retry message, preserve safe unsaved input, and SHALL not display raw verifier responses or sensitive document data.
20. WHEN an authorized administrator retries an eligible verification dependency failure, THE Customer_Admin_UI SHALL use the approved Customer service retry flow and SHALL display the resulting verification status without treating the Customer as verified before successful validation.

**Assumption:** The final UI may use separate Customer Master, Customer References, and Mind Mapping views or a tabbed/detail flow; the exact navigation, editable mapping workflow, verification status labels, safe document-entry behavior, and retry interaction require confirmation after FINNOVA-51..55 and the verifier contract are available.

### Requirement 13: Out-of-Scope Future AI Agent Boundary

**User Story:** As a product owner, I want the future Customer Information AI Agent kept separate, so that this feature delivers reliable persistence without prematurely implementing AI behavior.

#### Acceptance Criteria

1. THE Customer_Service SHALL not implement an AI Agent that retrieves Customer Information.
2. THE CustomerReference_Service SHALL not implement an AI Agent that retrieves Customer Information.
3. THE MindMapping_Service SHALL not implement an AI Agent that retrieves Customer Information.
4. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL not invoke a machine-learning model as part of this feature.
5. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL not generate embeddings as part of this feature.
6. THE Customer_Service, CustomerReference_Service, and MindMapping_Service SHALL not infer relationships, rank Customers, or make automated relationship decisions as part of this feature.
7. THE MindMapping_Service SHALL persist provenance that distinguishes explicit business mappings from future AI-proposed mappings when the approved mapping contract supports both sources.
8. THE MindMapping_Service SHALL persist Mapping Status values that distinguish approved, proposed, rejected, superseded, or archived mappings when those values are approved.
9. WHEN a future AI Agent consumes Mind Mappings, THE future AI Agent SHALL use the approved versioned read contract.
10. WHEN a future AI Agent consumes Mind Mappings, THE future AI Agent SHALL remain responsible for retrieval, inference, ranking, explanation, and model-specific behavior.
11. IF a requested change requires AI inference, model configuration, training data, embeddings, retrieval-augmented generation, or agent orchestration, THEN the requested change SHALL be deferred to a separate future story.
12. IF a requested change requires AI inference, model configuration, training data, embeddings, retrieval-augmented generation, or agent orchestration, THEN the Customer Master, Customer Reference, and Mind Mapping implementation scope SHALL not be expanded by that change.

### Requirement 14: Validate Customer Identity Documents Through the Existing Verifier

**User Story:** As a data owner, I want Aadhaar and PAN validated by the existing document-verification service, so that Customer records are not marked verified using locally reimplemented or untrusted identity checks.

#### Acceptance Criteria

1. WHEN a Customer create or update request requires or supplies an Aadhaar or PAN document under the approved FINNOVA-51..55 contract, THE Customer_Service SHALL request validation for each applicable document from the existing Finnova-Document-Verifier API before persistence or update completion.
2. THE Customer_Service SHALL delegate Aadhaar and PAN document validation to the existing Finnova-Document-Verifier API and SHALL not reimplement document-validation rules locally.
3. WHEN the Finnova-Document-Verifier API returns an approved valid and matched result for every required or supplied document, THE Customer_Service SHALL persist or update the Customer with only the approved Verification Outcome and SHALL expose the approved verification status.
4. IF the Finnova-Document-Verifier API returns a failed, rejected, invalid, or mismatched result for a required or supplied document, THEN THE Customer_Service SHALL preserve the existing Customer on update or persist no new Customer, and SHALL not persist or expose the Customer as verified.
5. IF the Finnova-Document-Verifier API is unavailable, times out, is cancelled, returns an ambiguous result, or fails under the approved dependency policy, THEN THE Customer_Service SHALL preserve the existing Customer on update or persist no new Customer, and SHALL return a safe retryable validation or dependency error without leaking PII.
6. WHEN the Customer_Service calls the Finnova-Document-Verifier API, THE Customer_Service SHALL authorize the call using the approved integration mechanism and SHALL enforce the approved timeout and cancellation bounds.
7. WHEN the Customer_Service retries an eligible verifier call, THE Customer_Service SHALL use the approved idempotency or deduplication behavior where the verifier integration supports it.
8. THE Customer_Service SHALL persist only the minimum approved Verification Outcome: verification status, verifier reference, masked or non-sensitive metadata, and timestamps; raw Aadhaar/PAN images or documents, authentication tokens, and unnecessary full document numbers SHALL not be persisted unless FINNOVA-51..55 explicitly requires them.
9. WHEN the Customer_Service returns a verification failure or dependency error, THE Customer_Service SHALL return only the approved safe English error code/message and SHALL not return raw verifier response content, raw document data, authentication tokens, or unnecessary full document numbers.
10. WHERE FINNOVA-51..55 does not require Aadhaar or PAN and the caller supplies neither document, THE Customer_Service SHALL complete the Customer operation without an identity-document verifier call.
11. IF only one of Aadhaar or PAN is required or supplied under the approved contract, THEN THE Customer_Service SHALL apply validation and persistence rules to that document without assuming that the other document is mandatory.
12. WHEN an existing Customer has a prior Verification Outcome, THE Customer_Service SHALL replace or retain that outcome only according to the approved document-change, revalidation, and verifier-status policy.

**Assumptions:** The verifier endpoint, request/response schema, authentication mechanism, status vocabulary, matching rules, retry policy, idempotency behavior, timeout/cancellation bounds, safe error mapping, and whether both Aadhaar and PAN are mandatory remain unknown and require confirmation. The future Customer Information AI Agent and all relationship inference remain outside this requirement.

## Confirmation Checklist Before Design

The following answers are required before the design and task artifacts can be finalized:

1. Provide the complete acceptance criteria and field definitions for FINNOVA-51, FINNOVA-52, FINNOVA-53, FINNOVA-54, and FINNOVA-55.
2. Confirm the Customer business key, uniqueness scope, normalization, immutability, and lifecycle states.
3. Confirm which Customer fields are PII/sensitive, which roles may view each field, and the masking/export rules.
4. Confirm the existing Finnova-Document-Verifier endpoint, request/response schema, authentication mechanism, status vocabulary, matching rules, verifier reference format, timeout/cancellation bounds, idempotency and retry policy, safe dependency errors, and whether Aadhaar, PAN, or both documents are mandatory.
5. Confirm whether Customer References are directed or symmetric, the allowed Reference Type values, endpoint cardinality, self-reference behavior, duplicate rules, and inactive-endpoint behavior.
6. Confirm whether a Mind Mapping may omit Location, whether it must include two Customers, allowed relation/status values, confidence semantics, provenance fields, evidence limits, and duplicate/supersession behavior.
7. Confirm whether System Administrators are the only readers or whether approved authenticated consumers/future AI services receive read projections, including identity-verification status access.
8. Confirm deactivation versus hard deletion, audit scope, retention, legal hold, correction, erasure, and identity-verification outcome retention behavior.
9. Confirm backend host, gateway route aliases, API paths, error-code family, pagination defaults, verifier integration routing, and UI navigation.
10. Confirm whether Mind Mappings are directly editable by administrators, imported from another source, or only reviewed/approved in the UI.
11. Confirm that the future Customer Information AI Agent remains a separate story and that this feature only delivers persistence, authorized contracts, and the approved document-verification boundary.

