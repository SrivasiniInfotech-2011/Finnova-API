# Requirements Document

## Introduction

The PAN-Aadhaar Validation feature (JIRA: FINNOVA-15, Module: SystemAdmin, Feature: Identity Verification) provides a unified identity-verification capability for the India market. The capability keeps PAN identifier validation and Aadhaar identifier validation as separate operations, adds `verify-pan-name` for PAN document-name comparison, adds `verify-aadhar-address` for Aadhaar document-address comparison, supports combined workflows, and records results for authorized System Administrators.

The feature uses the existing PAN identifier validation endpoint and existing Aadhaar identifier validation endpoint, plus the two separate comparison APIs. The comparison APIs may use uploaded document files, extracted document values, or both. Because the external provider request and response wire schemas were not provided, the requirements define provider-neutral internal adapter inputs and normalized outputs without inventing provider-specific fields.

The feature supports automated verification through configured external providers and manual review or override when a provider is unavailable or returns no usable match. PAN validation, Aadhaar validation, PAN-name comparison, and Aadhaar-address comparison remain independent operations. Independent results remain available when a combined workflow partially fails. Validation and comparison results are persisted as immutable records with an auditable relationship to the request, acting administrator, method, timestamps, outcome, and Verification Session.

The backend is hosted in the existing `Finnova.SystemAdminService` behind `Finnova.ApiGateway`. The frontend is implemented in the separate Finnova-UI repository as an English-only React and TypeScript administrative experience. All user-facing behavior is India-specific, uses English values, and does not add right-to-left or non-English localization.

## Glossary

- **Identity Verification**: The process of validating identity identifiers and comparing submitted identity attributes with identity-document attributes.
- **PAN**: Permanent Account Number, the 10-character alphanumeric identifier issued by the Indian Income Tax Department.
- **Aadhaar**: The 12-digit identity number issued by the Unique Identification Authority of India (UIDAI). Existing code and the requested API name may use the spelling “Aadhar”; API names retain the requested `aadhar` spelling.
- **PAN Identifier Validation**: The existing operation that checks a submitted PAN identifier through the configured PAN validation provider.
- **Aadhaar Identifier Validation**: The existing operation that checks a submitted Aadhaar identifier through the configured Aadhaar validation provider.
- **PAN Name Verification**: The separate `verify-pan-name` operation that compares a submitted name with the name represented by a PAN document or configured PAN provider.
- **Aadhaar Address Verification**: The separate `verify-aadhar-address` operation that compares a submitted address with the address represented by an Aadhaar document or configured Aadhaar provider.
- **Comparison API**: Either `verify-pan-name` or `verify-aadhar-address`.
- **Identity Verification Flow**: A single request context containing one or more independent identifier validations or attribute comparisons.
- **Comparison Outcome**: The normalized result of a comparison: `Match`, `NonMatch`, or `NoUsableMatch` when the comparison cannot produce a reliable match result.
- **Validation Status**: The normalized status of an identifier validation: `Success`, `Failure`, or `Unavailable`.
- **Verification Method**: The method used for a result: `Auto`, `Manual`, or `ManualOverride`.
- **Document File**: An uploaded PAN or Aadhaar document in JPG, PNG, or PDF format, subject to the 5 MB maximum file-size limit.
- **Extracted Value**: A submitted or provider-extracted name, address, PAN, or Aadhaar value used by a comparison adapter.
- **Provider Adapter**: A backend integration that maps provider-neutral verification inputs to a configured external provider and maps the provider response into the normalized result contract.
- **Provider-Neutral Adapter Input**: The internal operation type, submitted comparison value, optional protected Document File input, optional Extracted Value inputs, and correlation metadata passed to a Provider Adapter without exposing provider-specific wire fields.
- **Normalized Result**: A provider-independent response containing an operation outcome or status, safe reason or message, server UTC timestamp, correlation identifier, and record identifier when persistence occurred.
- **No Usable Match**: A comparison outcome used when the adapter cannot obtain enough reliable source data to return `Match` or `NonMatch`, including an unavailable provider, missing configured input, or unusable extraction.
- **Validation Record**: An immutable persisted record for an identifier validation or comparison result, including operation type, outcome or status, method, timestamps, actor, correlation identifier, source summary, and record identifier.
- **Verification Session**: The persisted parent context that groups independent records created by one combined or full identity-verification request.
- **Manual Override**: An authorized action that records a human decision after automated validation or comparison is unavailable, indeterminate, or unsuitable.
- **System Administrator**: An authenticated user whose token carries the `SystemAdmin` role claim and who is authorized to perform identity-verification operations.
- **IdentityVerification_Service**: The backend API and application services responsible for identity validation, attribute comparison, persistence, and query operations.
- **IdentityVerification_Admin_UI**: The React-based administrative screen used by System Administrators to submit, review, override, and query identity-verification results.
- **Provider Secret**: A credential, token, signing key, or connection secret used to call an external provider.
- **Correlation Identifier**: A non-sensitive identifier used to associate a request, provider call, response, log entry, and persisted result.
- **Protected Document Reference**: A reference to a Document File held in the platform-approved protected storage mechanism.
- **PaginatedResponse<T>**: The response shape containing result items, total count, current page number, and applied page size.
- **UTC**: Coordinated Universal Time used for server timestamps and date-range filtering.
- **JWT Bearer Token**: The signed access token used by the platform for authenticated API requests.
- **EARS**: Easy Approach to Requirements Syntax, the structured requirement style used in this document.

## Dependencies/Assumptions

- **Assumption (host placement):** The feature remains in `Finnova.SystemAdminService` and is reached through the existing `/api/systemadmin/**` gateway route. The feature does not create a new backend host.
- **Assumption (existing endpoint compatibility):** The existing PAN identifier validation endpoint and existing Aadhaar identifier validation endpoint remain supported. Their existing compatible request and response behavior remains available to current clients. Exact route names and response fields are confirmed against existing controllers during design; this requirements document does not replace either endpoint.
- **Assumption (comparison endpoint identity):** `verify-pan-name` and `verify-aadhar-address` are separate logical API operations. The final HTTP verbs, version prefixes, and gateway aliases are confirmed against existing controller conventions during design.
- **Assumption (provider-neutral comparison input):** Each comparison Provider Adapter can be configured to consume a Document File, one or more Extracted Values, or both. Exact provider request fields, response fields, extraction behavior, and comparison algorithms remain provider-specific and are not public requirements.
- **Assumption (provider-neutral adapter contract):** The internal comparison request includes an operation type, submitted value, optional Protected Document Reference or protected stream, optional Extracted Values, and Correlation Identifier. The Provider Adapter returns a Normalized Result. Provider-specific field names, payloads, response bodies, confidence algorithms, and wire schemas remain inside the adapter configuration.
- **Assumption (comparison semantics):** A provider or adapter supplies the comparison decision. `Match` and `NonMatch` are returned only when the adapter has usable source data. The adapter returns `NoUsableMatch` when reliable comparison data is unavailable.
- **Assumption (document storage):** Manual-review document bytes are stored through the platform-approved protected storage mechanism and associated with the immutable record. The current proposed default is database BLOB storage, but a separate protected object store may be selected during design. Document bytes are not written to application logs.
- **Assumption (sensitive-value protection):** PAN, Aadhaar, submitted names, and addresses needed for verification may be persisted in protected form for audit and query support. User-facing responses and query results use masked or otherwise minimized values unless an authorized workflow explicitly requires the submitted value.
- **Assumption (manual authorization):** Manual override is restricted to `SystemAdmin` users until a more granular identity-verification permission is approved.
- **Assumption (provider configuration):** Provider endpoints, adapter selection, timeout, retry, and credentials are supplied through secure deployment configuration or a secret store. Provider Secrets are not stored in source code, response contracts, or logs.
- **Assumption (retry policy):** An external call may retry up to three attempts with exponential backoff for configured transient failures. Maximum attempts, base delay, jitter, and retryable status codes remain configurable and are confirmed during design.
- **Assumption (file validation):** Manual Document Files have a maximum size of 5 MB and accepted formats are JPG, PNG, and PDF. Extension, MIME type, and content-signature validation are required. The exact content-signature library is confirmed during design.
- **Assumption (audit model):** The immutable Validation Record and its Verification Session provide the primary audit trail. A separate audit-event store is not required unless security or compliance review requires one.
- **Assumption (frontend integration):** The UI uses the shared axios instance, the existing service interface/real implementation/mock implementation/toggle pattern, React hooks, and Material UI conventions in Finnova-UI. The UI does not introduce Redux, RxJS, RTL, or translation scaffolding.
- **Dependency:** JWT Bearer authentication and the `SystemAdmin` authorization policy established by FINNOVA-8 are available in `Finnova.SystemAdminService`.
- **Dependency:** The API gateway forwards `/api/systemadmin/**` requests and the UI calls the gateway rather than calling a backend host directly.

## Requirements

### Requirement 1: Existing PAN Identifier Validation Operation

**User Story:** As a System Administrator, I want to validate a PAN identifier through the existing PAN validation endpoint, so that I can confirm the identifier against the configured Indian PAN provider.

#### Acceptance Criteria

1. WHEN a System Administrator submits a PAN Identifier Validation request containing an uppercase-normalized value with five letters, four digits, and one final letter, THE IdentityVerification_Service SHALL invoke the existing PAN identifier validation endpoint's configured Provider Adapter.
2. WHEN the PAN Provider Adapter returns a result, THE IdentityVerification_Service SHALL return a Normalized Result containing Validation Status, a safe message, and a server UTC timestamp.
3. IF a PAN identifier does not match the required 10-character pattern, THEN THE IdentityVerification_Service SHALL reject the request with a validation error before any Provider Adapter call.
4. WHEN the PAN Provider Adapter returns a successful identifier validation, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with operation type `PanIdentifierValidation`, status `Success`, method `Auto`, server UTC timestamp, actor, and Verification Session identifier when a session exists.
5. WHEN the PAN Provider Adapter returns an invalid, not-found, or provider-declared failure result, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with status `Failure`, method `Auto`, and a safe normalized reason.
6. IF the PAN Provider Adapter is unavailable after the configured timeout and retry policy, THEN THE IdentityVerification_Service SHALL return status `Unavailable` and offer the Manual Override path.
7. IF the PAN Provider Adapter is unavailable after the configured timeout and retry policy, THEN THE IdentityVerification_Service SHALL not return a successful PAN validation result.
8. WHEN an existing client calls the PAN identifier validation endpoint, THE IdentityVerification_Service SHALL preserve the endpoint's existing compatible response fields while applying the privacy, authorization, error, and persistence requirements in this document.

### Requirement 2: Existing Aadhaar Identifier Validation Operation

**User Story:** As a System Administrator, I want to validate an Aadhaar identifier through the existing Aadhaar validation endpoint, so that I can confirm the identifier against the configured Indian Aadhaar provider.

#### Acceptance Criteria

1. WHEN a System Administrator submits an Aadhaar Identifier Validation request containing exactly 12 digits, THE IdentityVerification_Service SHALL invoke the existing Aadhaar identifier validation endpoint's configured Provider Adapter.
2. WHEN the Aadhaar Provider Adapter returns a result, THE IdentityVerification_Service SHALL return a Normalized Result containing Validation Status, a safe message, and a server UTC timestamp.
3. IF an Aadhaar identifier is not exactly 12 digits, THEN THE IdentityVerification_Service SHALL reject the request with a validation error before any Provider Adapter call.
4. WHEN the Aadhaar Provider Adapter returns a successful identifier validation, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with operation type `AadhaarIdentifierValidation`, status `Success`, method `Auto`, server UTC timestamp, actor, and Verification Session identifier when a session exists.
5. WHEN the Aadhaar Provider Adapter returns an invalid, not-found, or provider-declared failure result, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with status `Failure`, method `Auto`, and a safe normalized reason.
6. IF the Aadhaar Provider Adapter is unavailable after the configured timeout and retry policy, THEN THE IdentityVerification_Service SHALL return status `Unavailable` and offer the Manual Override path.
7. IF the Aadhaar Provider Adapter is unavailable after the configured timeout and retry policy, THEN THE IdentityVerification_Service SHALL not return a successful Aadhaar validation result.
8. WHEN an existing client calls the Aadhaar identifier validation endpoint, THE IdentityVerification_Service SHALL preserve the endpoint's existing compatible response fields while applying the privacy, authorization, error, and persistence requirements in this document.

### Requirement 3: `verify-pan-name` PAN Document-Name Comparison Operation

**User Story:** As a System Administrator, I want to compare a submitted name with the PAN document, so that I can detect whether the PAN document belongs to the submitted name.

#### Acceptance Criteria

1. WHEN a System Administrator invokes `verify-pan-name` with a non-empty submitted name and the configured adapter input sources, THE IdentityVerification_Service SHALL invoke the PAN Name Provider Adapter.
2. WHEN the PAN Name Provider Adapter has a usable PAN document name and submitted name and determines equality under the configured comparison rules, THE IdentityVerification_Service SHALL return `Match`.
3. WHEN the PAN Name Provider Adapter has usable names and determines that the names do not match under the configured comparison rules, THE IdentityVerification_Service SHALL return `NonMatch`.
4. IF `verify-pan-name` lacks a required configured input source, cannot extract a usable PAN document name, or receives an unusable provider result, THEN THE IdentityVerification_Service SHALL return `NoUsableMatch` with a safe operational reason.
5. WHEN `verify-pan-name` returns `Match`, `NonMatch`, or `NoUsableMatch`, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with operation type `PanNameVerification`, the independent Comparison Outcome, method `Auto`, server UTC timestamp, actor, source summary, and Verification Session identifier when a session exists.
6. WHEN `verify-pan-name` returns a response, THE IdentityVerification_Service SHALL expose only the normalized comparison contract and safe metadata.
7. WHEN `verify-pan-name` returns a response, THE IdentityVerification_Service SHALL keep provider-specific request and response fields inside the Provider Adapter.
8. IF `verify-pan-name` returns `NoUsableMatch`, THEN THE IdentityVerification_Service SHALL offer Manual Override without treating the result as `Match`.

### Requirement 4: `verify-aadhar-address` Aadhaar Document-Address Comparison Operation

**User Story:** As a System Administrator, I want to compare a submitted address with the address in the Aadhaar document, so that I can detect whether the submitted address matches the identity document.

#### Acceptance Criteria

1. WHEN a System Administrator invokes `verify-aadhar-address` with a non-empty submitted address and the configured adapter input sources, THE IdentityVerification_Service SHALL invoke the Aadhaar Address Provider Adapter.
2. WHEN the Aadhaar Address Provider Adapter has a usable document address and submitted address and determines equality under the configured comparison rules, THE IdentityVerification_Service SHALL return `Match`.
3. WHEN the Aadhaar Address Provider Adapter has usable addresses and determines that the addresses do not match under the configured comparison rules, THE IdentityVerification_Service SHALL return `NonMatch`.
4. IF `verify-aadhar-address` lacks a required configured input source, cannot extract a usable document address, or receives an unusable provider result, THEN THE IdentityVerification_Service SHALL return `NoUsableMatch` with a safe operational reason.
5. WHEN `verify-aadhar-address` returns `Match`, `NonMatch`, or `NoUsableMatch`, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record with operation type `AadhaarAddressVerification`, the independent Comparison Outcome, method `Auto`, server UTC timestamp, actor, source summary, and Verification Session identifier when a session exists.
6. WHEN `verify-aadhar-address` returns a response, THE IdentityVerification_Service SHALL expose only the normalized comparison contract and safe metadata.
7. WHEN `verify-aadhar-address` returns a response, THE IdentityVerification_Service SHALL keep provider-specific request and response fields inside the Provider Adapter.
8. IF `verify-aadhar-address` returns `NoUsableMatch`, THEN THE IdentityVerification_Service SHALL offer Manual Override without treating the result as `Match`.

### Requirement 5: Provider-Neutral Configurable Document and Extracted-Value Adapter Contract

**User Story:** As a platform integrator, I want comparison providers to accept configured document and extracted-value inputs, so that provider integrations can evolve without changing the public verification APIs.

#### Acceptance Criteria

1. THE IdentityVerification_Service SHALL define a Provider-Neutral Adapter Input containing the operation type, submitted comparison value, optional Protected Document Reference or protected stream, optional Extracted Values, and Correlation Identifier.
2. WHERE a provider configuration requires a Document File, THE IdentityVerification_Service SHALL pass the file through the protected adapter boundary.
3. WHERE a provider configuration requires a Document File and the request does not supply a valid file, THE IdentityVerification_Service SHALL return a validation error before the provider call.
4. WHERE a provider configuration requires Extracted Values, THE IdentityVerification_Service SHALL pass only the configured values through the adapter boundary.
5. WHERE a provider configuration requires an Extracted Value and the request does not supply a usable required value, THE IdentityVerification_Service SHALL return a validation error before the provider call.
6. WHERE a provider configuration accepts both a Document File and Extracted Values, THE IdentityVerification_Service SHALL pass both according to the configured mapping.
7. WHEN a comparison adapter maps a provider response, THE IdentityVerification_Service SHALL convert the response to the normalized Comparison Outcome or Validation Status.
8. WHEN a comparison adapter maps a provider response, THE IdentityVerification_Service SHALL not require public contracts to contain provider-specific fields.
9. IF a provider configuration is missing, invalid, or inconsistent with submitted input sources, THEN THE IdentityVerification_Service SHALL return a configuration error before the provider call.
10. IF a provider configuration error occurs, THEN THE IdentityVerification_Service SHALL record a safe operational error without recording a `Match`.
11. WHEN an adapter accepts Document File and/or Extracted Value inputs, THE IdentityVerification_Service SHALL preserve an independent source summary in the immutable Validation Record.

### Requirement 6: Combined and Full Identity Verification Workflows

**User Story:** As a System Administrator, I want to run related identifier and attribute checks in one identity-verification flow, so that I can review the complete result while preserving each independent decision.

#### Acceptance Criteria

1. WHEN a System Administrator requests PAN validation plus PAN-name verification, THE IdentityVerification_Service SHALL execute the PAN Identifier Validation and `verify-pan-name` as independent operations within one Verification Session.
2. WHEN a PAN validation plus PAN-name verification workflow completes, THE IdentityVerification_Service SHALL return the PAN identifier result and PAN-name result separately.
3. WHEN a System Administrator requests Aadhaar validation plus Aadhaar-address verification, THE IdentityVerification_Service SHALL execute the Aadhaar Identifier Validation and `verify-aadhar-address` as independent operations within one Verification Session.
4. WHEN an Aadhaar validation plus Aadhaar-address verification workflow completes, THE IdentityVerification_Service SHALL return the Aadhaar identifier result and Aadhaar-address result separately.
5. WHEN a System Administrator requests a full identity verification flow containing PAN validation, PAN-name verification, Aadhaar validation, and Aadhaar-address verification, THE IdentityVerification_Service SHALL execute each applicable operation independently within one Verification Session.
6. WHEN a full identity verification workflow completes, THE IdentityVerification_Service SHALL return four separately addressable results.
7. WHEN a combined workflow contains valid inputs, THE IdentityVerification_Service SHALL apply the configured timeout and retry policy to each external operation independently.
8. IF an operation fails or times out in a combined workflow, THEN THE IdentityVerification_Service SHALL continue attempting other applicable operations.
9. WHERE all operations in a combined workflow return usable results, THE IdentityVerification_Service SHALL return the independent results, Verification Session identifier, and server UTC session timestamp.
10. WHERE all operations in a combined workflow return usable results, THE IdentityVerification_Service SHALL persist exactly one immutable Validation Record per operation.
11. WHERE one or more operations in a combined workflow fail, time out, or return `NoUsableMatch`, THE IdentityVerification_Service SHALL return each completed result independently and identify each partial failure.
12. WHERE one or more operations in a combined workflow fail, time out, or return `NoUsableMatch`, THE IdentityVerification_Service SHALL persist each completed operation without converting another operation's result.
13. IF a combined workflow contains an invalid identifier or missing required comparison input, THEN THE IdentityVerification_Service SHALL reject only the invalid operation when the request contract permits partial execution.
14. IF a combined workflow contains an invalid identifier or missing required comparison input, THEN THE IdentityVerification_Service SHALL reject the whole request before any provider call when the request contract requires atomic input validation.
15. WHEN a combined workflow completes, THE IdentityVerification_Service SHALL make the Verification Session and all child Validation Records queryable by the authorized actor.
16. WHEN a combined workflow completes, THE IdentityVerification_Service SHALL retain the parent-child relationship between the Verification Session and child Validation Records.

### Requirement 7: Manual Review and Override

**User Story:** As a System Administrator, I want to manually resolve unavailable or indeterminate verification results, so that identity verification can continue without falsely treating an unavailable comparison as a match.

#### Acceptance Criteria

1. WHEN a provider is unavailable, times out after configured retries, or returns `NoUsableMatch`, THE IdentityVerification_Service SHALL offer a Manual Override action for the affected operation.
2. WHEN a provider is unavailable, times out after configured retries, or returns `NoUsableMatch`, THE IdentityVerification_Service SHALL preserve independent results that already completed.
3. WHEN a System Administrator submits a manual PAN override, THE IdentityVerification_Service SHALL require a valid PAN identifier, submitted cardholder name, date of birth, and a supported Document File unless an approved manual-review contract supplies a Protected Document Reference.
4. WHEN a System Administrator submits a manual Aadhaar override, THE IdentityVerification_Service SHALL require a valid Aadhaar identifier, submitted cardholder name, address, and a supported Document File unless an approved manual-review contract supplies a Protected Document Reference.
5. WHEN a System Administrator manually resolves a PAN-name or Aadhaar-address comparison, THE IdentityVerification_Service SHALL record the decision, affected operation, administrator, server UTC timestamp, safe reason, and source-record relationship as an immutable Validation Record.
6. IF a manual override omits a required field, THEN THE IdentityVerification_Service SHALL return a validation error identifying the invalid input.
7. IF a manual override contains an invalid identifier, THEN THE IdentityVerification_Service SHALL return a validation error identifying the invalid identifier.
8. IF a manual override exceeds the configured 5 MB file limit, THEN THE IdentityVerification_Service SHALL return a validation error identifying the file-size violation.
9. IF a manual override supplies a file other than JPG, PNG, or PDF, THEN THE IdentityVerification_Service SHALL return a validation error identifying the unsupported file format.
10. IF a manual override fails input validation, THEN THE IdentityVerification_Service SHALL not persist the override.
11. IF a System Administrator submits a manual override after a provider returns `NonMatch`, THEN THE IdentityVerification_Service SHALL preserve the automated `NonMatch` record.
12. IF a System Administrator submits a manual override after a provider returns `NonMatch`, THEN THE IdentityVerification_Service SHALL persist the manual decision as a separate immutable record linked to the original result.
13. WHEN a System Administrator declines a manual override for a failed or unavailable operation, THE IdentityVerification_Service SHALL preserve the independent completed results.
14. WHEN a System Administrator declines a manual override for a failed or unavailable operation, THE IdentityVerification_Service SHALL persist the unavailable or indeterminate result.
15. WHEN a System Administrator declines a manual override for a failed or unavailable operation, THE IdentityVerification_Service SHALL not create a successful manual result.
16. WHEN a manual override succeeds, THE IdentityVerification_Service SHALL associate the Protected Document Reference or approved document reference with the immutable record.
17. WHEN a manual override succeeds, THE IdentityVerification_Service SHALL return the record identifier and safe metadata.
18. WHEN a manual override succeeds, THE IdentityVerification_Service SHALL not expose document bytes in logs.

### Requirement 8: External Service Reliability and Error Handling

**User Story:** As an operations stakeholder, I want predictable provider timeouts, retries, and errors, so that provider incidents do not produce false verification results or block unrelated checks.

#### Acceptance Criteria

1. WHEN an external provider call exceeds its configured timeout, THE IdentityVerification_Service SHALL cancel that call.
2. WHEN an external provider call exceeds its configured timeout during identifier validation, THE IdentityVerification_Service SHALL classify the result as `Unavailable`.
3. WHEN an external provider call exceeds its configured timeout during comparison, THE IdentityVerification_Service SHALL classify the result as `NoUsableMatch`.
4. IF an external provider call exceeds its configured timeout, THEN THE IdentityVerification_Service SHALL not return a successful match or validation result for that call.
5. WHEN an external provider call returns a configured transient network or server failure, THE IdentityVerification_Service SHALL retry according to the configured retry policy.
6. WHEN an external provider call reaches the configured maximum attempts, THE IdentityVerification_Service SHALL stop retrying that request.
7. IF an external provider returns a non-transient validation or business result, THEN THE IdentityVerification_Service SHALL not retry the same request solely because the provider returned that result.
8. IF an external provider remains unavailable after retries, THEN THE IdentityVerification_Service SHALL return a stable error contract containing an HTTP-appropriate error classification, safe message, Correlation Identifier, and Manual Override availability where applicable.
9. WHEN an external provider returns an unexpected payload, THE IdentityVerification_Service SHALL classify the result as an adapter error or `NoUsableMatch`.
10. WHEN an external provider returns an unexpected payload, THE IdentityVerification_Service SHALL avoid exposing the raw provider payload.
11. WHEN an external provider returns an unexpected payload, THE IdentityVerification_Service SHALL preserve a safe diagnostic reference for authorized operations staff.
12. IF a request is rejected by input validation, authentication, authorization, or configuration validation, THEN THE IdentityVerification_Service SHALL not invoke an external provider.

### Requirement 9: Validation Records, Persistence, Auditability, and Immutability

**User Story:** As a compliance stakeholder, I want independent verification decisions preserved immutably, so that the verification history remains auditable.

#### Acceptance Criteria

1. WHEN an automated validation or comparison operation completes with a usable or unavailable result, THE IdentityVerification_Service SHALL persist an immutable Validation Record with a unique identifier, operation type, outcome or status, method, server UTC timestamp, acting user, Correlation Identifier, and Verification Session identifier when applicable.
2. WHEN a Validation Record stores a PAN, Aadhaar, name, address, or document reference, THE IdentityVerification_Service SHALL apply the configured protection and minimization rules.
3. WHEN a Validation Record stores identity data, THE IdentityVerification_Service SHALL store only metadata needed for verification, querying, audit, or approved retrieval.
4. WHEN a Validation Record is created, THE IdentityVerification_Service SHALL set the UTC timestamp on the server.
5. WHEN a Validation Record is created, THE IdentityVerification_Service SHALL not treat a caller-supplied timestamp as the authoritative event time.
6. IF a request attempts to update or delete a Validation Record or Verification Session result, THEN THE IdentityVerification_Service SHALL reject the request with an immutability error.
7. IF a request attempts to update or delete a Validation Record or Verification Session result, THEN THE IdentityVerification_Service SHALL preserve the original record.
8. WHEN a manual decision supersedes or supplements an automated result, THE IdentityVerification_Service SHALL preserve both records.
9. WHEN a manual decision supersedes or supplements an automated result, THE IdentityVerification_Service SHALL link the manual record to the automated source record without overwriting the automated outcome.
10. WHEN a combined or full workflow creates multiple results, THE IdentityVerification_Service SHALL persist each operation independently.
11. WHEN a combined or full workflow creates multiple results, THE IdentityVerification_Service SHALL preserve the parent-child relationship needed to reconstruct the workflow and partial failures.
12. WHEN an authorized audit or query operation retrieves a Validation Record, THE IdentityVerification_Service SHALL return the operation, outcome, method, timestamps, actor reference, safe reason, and related session metadata.
13. WHEN an authorized audit or query operation retrieves a Validation Record, THE IdentityVerification_Service SHALL omit Provider Secrets, document bytes, and unmasked sensitive values from the default response.
14. IF persistence fails before a result is durably recorded, THEN THE IdentityVerification_Service SHALL return a persistence error.
15. IF persistence fails before a result is durably recorded, THEN THE IdentityVerification_Service SHALL not claim that the result was recorded.
16. IF persistence fails before a result is durably recorded, THEN THE IdentityVerification_Service SHALL not silently convert the result into a successful verification.

### Requirement 10: Query and Search Validation Records

**User Story:** As a System Administrator, I want to search and filter validation history, so that I can locate identifier validations, comparison outcomes, manual decisions, and partial workflow results.

#### Acceptance Criteria

1. WHEN a System Administrator requests validation history with a non-empty search term, THE IdentityVerification_Service SHALL return only records whose protected search representation matches the PAN, Aadhaar, record identifier, or Correlation Identifier search rule, subject to authorization and pagination.
2. WHEN a System Administrator filters by operation type, THE IdentityVerification_Service SHALL return only records for the selected identifier validation or comparison operation.
3. WHEN a System Administrator filters by validation status, THE IdentityVerification_Service SHALL return only records matching the selected Validation Status.
4. WHEN a System Administrator filters by Comparison Outcome, THE IdentityVerification_Service SHALL return only records matching the selected Comparison Outcome.
5. WHEN a System Administrator filters by Verification Method, THE IdentityVerification_Service SHALL return only records matching the selected Verification Method.
6. WHEN a System Administrator filters by date range, THE IdentityVerification_Service SHALL return records whose server UTC timestamp is within the inclusive start and exclusive end range.
7. WHEN a System Administrator filters by Verification Session identifier, THE IdentityVerification_Service SHALL return the session and related operation records according to the selected query shape.
8. WHEN a System Administrator supplies no filter, THE IdentityVerification_Service SHALL return all authorized records subject to pagination and the default privacy projection.
9. WHEN a System Administrator requests validation history, THE IdentityVerification_Service SHALL return a paginated `PaginatedResponse<T>` containing items, total count, current page number, and applied page size.
10. WHEN a System Administrator omits the page number, THE IdentityVerification_Service SHALL use page 1.
11. WHEN a System Administrator omits the page size, THE IdentityVerification_Service SHALL use page size 20.
12. IF a System Administrator supplies a page size below 1 or above 100, THEN THE IdentityVerification_Service SHALL reject the request with a validation error.
13. WHEN a requested page is beyond the last page, THE IdentityVerification_Service SHALL return an empty item collection with the correct total count, page number, and page size.
14. WHEN validation history is returned, THE IdentityVerification_Service SHALL order records by server UTC timestamp descending.
15. WHEN validation history contains equal timestamps, THE IdentityVerification_Service SHALL use record identifier descending as the deterministic secondary ordering.
16. IF a search or filter matches no records, THEN THE IdentityVerification_Service SHALL return an empty item collection with total count 0 and a successful response.
17. WHEN the UI requests a document preview or download for an authorized manual record, THE IdentityVerification_Service SHALL return the protected document using its original supported content type.
18. IF a document preview or download targets an automated record or a record without a document, THEN THE IdentityVerification_Service SHALL return a not-found result without disclosing unrelated records.

### Requirement 11: Authentication, Authorization, and Gateway Access

**User Story:** As a security stakeholder, I want identity-verification operations restricted to authorized System Administrators, so that sensitive identity information and decisions are protected.

#### Acceptance Criteria

1. IF a request to validate an identifier, invoke a comparison API, submit a manual override, retrieve a document, or query verification history has a missing, expired, malformed, or invalid JWT Bearer Token, THEN THE IdentityVerification_Service SHALL return HTTP 401.
2. IF a request has a missing, expired, malformed, or invalid JWT Bearer Token, THEN THE IdentityVerification_Service SHALL not perform the requested operation or invoke a provider.
3. IF an authenticated user lacks the `SystemAdmin` role claim, THEN THE IdentityVerification_Service SHALL return HTTP 403 for protected identity-verification operations.
4. IF an authenticated user lacks the `SystemAdmin` role claim, THEN THE IdentityVerification_Service SHALL not perform the requested operation or invoke a provider.
5. WHEN a request carries a valid JWT Bearer Token with the `SystemAdmin` role claim, THE IdentityVerification_Service SHALL authorize the operation and apply the remaining validation, privacy, and persistence requirements.
6. WHEN a request fails both authentication and role authorization, THE IdentityVerification_Service SHALL enforce authentication first and return HTTP 401.
7. WHEN a request reaches the API through `Finnova.ApiGateway`, THE gateway SHALL route the operation to `Finnova.SystemAdminService` without exposing provider hosts as UI origins.
8. WHEN the frontend invokes a protected operation, THE shared UI API client SHALL send the current JWT Bearer Token.
9. WHEN the frontend receives a 401, 403, conflict, timeout, or provider-unavailable response, THE shared UI API client SHALL surface the error through the platform's existing notification behavior.

### Requirement 12: Privacy, Security, and India-Only Validation Rules

**User Story:** As a privacy stakeholder, I want identity data and provider credentials handled safely, so that verification does not create avoidable disclosure or localization risk.

#### Acceptance Criteria

1. THE IdentityVerification_Service SHALL validate PAN identifiers as uppercase-normalized Indian PAN values with five letters, four digits, and one final letter before provider invocation.
2. THE IdentityVerification_Service SHALL validate Aadhaar identifiers as Indian values containing exactly 12 digits before provider invocation.
3. THE IdentityVerification_Service SHALL use English-only operation names, labels, validation messages, lookup values, and sample or test data.
4. THE IdentityVerification_Service SHALL not add Arabic, non-English localization, bilingual fields, or right-to-left rendering.
5. THE IdentityVerification_Service SHALL not write full PAN values, full Aadhaar values, submitted or extracted addresses, document bytes, JWT Bearer Tokens, Provider Secrets, or raw provider payloads to application logs, exception messages, telemetry, or audit messages.
6. WHEN the IdentityVerification_Service logs an identity-verification event, THE IdentityVerification_Service SHALL use masked or irreversible references, operation type, safe status, Correlation Identifier, server UTC timestamp, and actor reference rather than full sensitive values.
7. WHEN a provider request requires a sensitive value, THE IdentityVerification_Service SHALL transmit the value only over the configured protected adapter connection.
8. WHEN a provider request requires a sensitive value, THE IdentityVerification_Service SHALL omit the value from public API error details.
9. IF a request contains a Document File with an unsupported extension, MIME type, content signature, or configured size violation, THEN THE IdentityVerification_Service SHALL reject the file before provider invocation.
10. IF a request contains a rejected Document File, THEN THE IdentityVerification_Service SHALL not persist or log the rejected bytes.
11. IF secrets or protected document storage configuration is unavailable, THEN THE IdentityVerification_Service SHALL fail closed with a safe configuration error.
12. IF secrets or protected document storage configuration is unavailable, THEN THE IdentityVerification_Service SHALL not call a provider or persist unprotected secrets or document bytes.

### Requirement 13: Frontend Verification Experience

**User Story:** As a System Administrator, I want the administrative UI to submit and review each verification result separately, so that I can act on partial failures without losing completed results.

#### Acceptance Criteria

1. WHEN the IdentityVerification_Admin_UI displays the identity-verification screen, THE IdentityVerification_Admin_UI SHALL provide controls for the existing PAN identifier validation, existing Aadhaar identifier validation, `verify-pan-name`, and `verify-aadhar-address` operations.
2. WHEN the IdentityVerification_Admin_UI displays the identity-verification screen, THE IdentityVerification_Admin_UI SHALL provide controls for combined PAN, combined Aadhaar, and full identity verification workflows.
3. WHEN a System Administrator selects `verify-pan-name`, THE IdentityVerification_Admin_UI SHALL collect the submitted name and the configured document or Extracted Value inputs required by the active adapter configuration.
4. WHEN a System Administrator selects `verify-aadhar-address`, THE IdentityVerification_Admin_UI SHALL collect the submitted address and the configured document or Extracted Value inputs required by the active adapter configuration.
5. WHEN a System Administrator selects a comparison operation, THE IdentityVerification_Admin_UI SHALL not expose provider-specific fields.
6. WHEN a System Administrator submits a combined workflow, THE IdentityVerification_Admin_UI SHALL display PAN validation, PAN-name verification, Aadhaar validation, and Aadhaar-address verification as independent result cards or rows.
7. WHEN a System Administrator submits a combined workflow, THE IdentityVerification_Admin_UI SHALL display distinct status or outcome, safe reason, and timestamp for each result.
8. WHEN one operation in a combined workflow fails, times out, returns `NoUsableMatch`, or returns `NonMatch`, THE IdentityVerification_Admin_UI SHALL preserve and display completed independent results.
9. WHEN one operation in a combined workflow fails, times out, or returns `NoUsableMatch`, THE IdentityVerification_Admin_UI SHALL present Manual Override only for the affected operation when the API permits it.
10. WHEN a comparison result is `Match`, `NonMatch`, or `NoUsableMatch`, THE IdentityVerification_Admin_UI SHALL display the normalized outcome and a safe English message.
11. WHEN a comparison result is displayed, THE IdentityVerification_Admin_UI SHALL not render full PAN or Aadhaar values, document bytes, Provider Secrets, or raw provider payloads.
12. WHEN a System Administrator opens verification history, THE IdentityVerification_Admin_UI SHALL support operation, status or outcome, method, session, search, date-range, and pagination filters.
13. WHEN verification history is displayed, THE IdentityVerification_Admin_UI SHALL render masked values from the default privacy projection.
14. WHEN a System Administrator requests an authorized manual document preview or download, THE IdentityVerification_Admin_UI SHALL use the document endpoint response.
15. WHEN the IdentityVerification_Admin_UI handles an authorized manual document preview or download, THE IdentityVerification_Admin_UI SHALL not cache or write document bytes to application logs.
16. WHEN the API returns a validation, unauthorized, forbidden, timeout, provider-unavailable, conflict, or persistence error, THE IdentityVerification_Admin_UI SHALL display the shared platform error notification.
17. WHEN the API returns a correctable error, THE IdentityVerification_Admin_UI SHALL keep unsaved user input available for correction or manual retry where safe.
18. WHEN the UI uses mock mode, THE IdentityVerification_Admin_UI SHALL provide mock results for success, non-match, no-usable-match, provider-unavailable, validation-error, and partial-workflow paths.
19. WHEN the UI uses mock mode, THE IdentityVerification_Admin_UI SHALL use English-only India-specific sample data.
20. THE IdentityVerification_Admin_UI SHALL use React hooks, the shared axios instance, the feature service interface, real and mock implementations, the existing API toggle, Material UI components, and existing application route and navigation conventions.
21. THE IdentityVerification_Admin_UI SHALL not introduce Redux, RxJS, RTL behavior, or translation scaffolding.

### Requirement 14: API Contract and Response Consistency

**User Story:** As an API consumer, I want stable normalized contracts and predictable errors, so that backend and frontend clients can handle independent verification outcomes consistently.

#### Acceptance Criteria

1. THE IdentityVerification_Service SHALL expose request and response contracts that identify the operation, Verification Session when applicable, independent result, normalized Validation Status or Comparison Outcome, server UTC timestamp, record identifier when persisted, and safe message or reason.
2. WHEN an operation has no usable comparison result, THE IdentityVerification_Service SHALL return `NoUsableMatch` rather than a fabricated `Match`, fabricated `NonMatch`, or ambiguous empty success response.
3. WHEN an operation is unavailable, THE IdentityVerification_Service SHALL distinguish `Unavailable` from provider-declared `Failure` and comparison `NonMatch` in the normalized contract.
4. IF a request is malformed or fails validation, THEN THE IdentityVerification_Service SHALL return a consistent validation-error contract identifying the affected field or operation.
5. IF a request is malformed or fails validation, THEN THE IdentityVerification_Service SHALL omit full sensitive values from the validation-error contract.
6. IF a provider, adapter, persistence, or configuration failure occurs, THEN THE IdentityVerification_Service SHALL return a consistent safe error contract with an HTTP-appropriate status and Correlation Identifier.
7. IF a provider, adapter, persistence, or configuration failure occurs, THEN THE IdentityVerification_Service SHALL omit raw provider response content from the error contract.
8. WHEN an existing client calls the PAN or Aadhaar identifier validation endpoint, THE IdentityVerification_Service SHALL preserve its compatible behavior while making `verify-pan-name` and `verify-aadhar-address` available as separate operations.
9. WHEN a client calls `verify-pan-name`, THE IdentityVerification_Service SHALL return only the normalized PAN document-name comparison contract.
10. WHEN a client calls `verify-aadhar-address`, THE IdentityVerification_Service SHALL return only the normalized Aadhaar document-address comparison contract.
