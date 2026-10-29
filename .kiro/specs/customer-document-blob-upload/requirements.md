# Requirements Document

## Introduction

The Customer Onboarding Document Blob Upload feature provides an authenticated way to upload scanned customer onboarding documents to Azure Blob Storage and associate the stored object with the corresponding Customer Document Reference. The feature extends the existing Customer Onboarding capability rather than making the Customer record a binary-document repository. Customer document definitions remain owned by Document Master, customer ownership remains owned by Customer Onboarding, and Azure Blob Storage owns the document bytes.

A System Administrator can select an applicable document for an existing customer, upload one file, and receive the stored document metadata. The service validates the customer, document reference, configured file type, and configured size before writing. It stores only a non-sensitive blob reference and approved metadata with the Customer Document Reference. The service does not place raw document bytes in SQL Server, API response bodies, audit rows, logs, or messages. An authorized caller can retrieve the latest uploaded file through the API; the first release does not expose a customer-document delete operation unless product confirms that requirement.

The backend uses the existing `Finnova.SystemAdminService` Customer Onboarding route, MediatR/CQRS conventions, EF Core Customer Document Reference model, exception middleware, and provider-agnostic `IBlobStorageService`. Azure Blob Storage is the selected provider for this feature, using the existing Azure implementation and configuration model. The frontend uses the existing React 18 + TypeScript + Vite + Material UI Customer Onboarding screen, shared axios instance, service interface, real/mock implementations, and `VITE_USE_MOCK_API` toggle. The upload is a separate multipart operation after a Customer exists; it is not embedded as binary content in the JSON Customer Create or Modify request.

Finnova is India-only and English-only. Document labels, validation messages, seeded examples, file metadata, and UI text are English. This feature adds no bilingual fields, translation scaffolding, Arabic or other non-English values, RTL handling, or right-to-left layout behavior.

Where the request or existing code is silent, decisions are recorded explicitly as **Assumptions for confirmation**. The assumptions below define the proposed first-release behavior so that the design and task documents can remain implementable without silently changing the Customer Onboarding or Document Master contracts.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Storage provider):** Azure Blob Storage is the provider for the first release. The feature reuses `IBlobStorageService` and `AzureBlobStorageService`; it does not create a second Azure client or bypass the existing provider abstraction. AWS S3 support remains available to other callers but is not selected for this feature.
- **Assumption (Storage configuration):** The SystemAdmin host binds the existing `BlobStorage` section, sets `BlobStorage:Provider` to `Azure`, supplies `BlobStorage:Azure:ConnectionString` through deployment configuration or a secret store, and adds a customer-document container setting under that section. The connection string and any account key are never committed to source, returned to clients, or written to logs. The exact container-setting property name is confirmed during design, with `CustomerDocuments` as the proposed container name.
- **Assumption (Host and route):** Customer document upload and retrieval remain in `Finnova.SystemAdminService` under the existing Customer Onboarding controller and `/api/customer-onboarding/**` route. The existing `CustomerOnboardingAliasRoute` continues to expose the UI-composed `/api/ua/api/customer-onboarding/**` path through `Finnova.ApiGateway` to the SystemAdmin cluster. A new host or cluster is not introduced.
- **Assumption (Metadata persistence):** Document metadata is persisted in the database and linked to the existing `CustomerDocumentReference`. At minimum, the persisted metadata identifies the Customer, Document Reference, blob container/key, original file name, validated content type, byte length, upload timestamp in UTC, and uploading actor. Raw bytes are never persisted in SQL Server. Whether these fields reuse `DmsReference`/`ViewMetadata` or require a dedicated upload metadata entity is an implementation design choice, but the observable metadata contract is fixed by these requirements.
- **Assumption (Document definition authority):** The active Document Master definition and its applicability rule remain authoritative for the selected `DocumentDefinitionId`. Its `AllowedMimeTypes`, `MaxFileSizeMb`, active/lifecycle status, mandatory/image flags, and customer applicability are used for validation. The existing Document model's configured type family is PDF, JPG, and PNG; no unsupported type is silently added.
- **Assumption (Allowed types and size):** The first release permits PDF, JPEG/JPG, and PNG content only when the applicable active Document Master definition permits that type. The service validates both the declared MIME type and file signature where practical, rejects mismatches, and enforces the configured per-document `MaxFileSizeMb`. A global hard ceiling of 25 MiB applies even if a future definition is misconfigured; the existing Document Master bounds of 1–25 MiB and default of 5 MiB are the proposed baseline. The precise MIME aliases and whether a definition may permit a subset require confirmation.
- **Assumption (Upload cardinality):** Each request uploads exactly one file for one existing Customer Document Reference. Multi-file upload, bulk upload, archive upload, and upload of a document before the Customer exists are outside the first release. The UI queues or disables file upload until the Customer create operation returns a Customer Code and the selected document reference exists.
- **Assumption (Upload replacement/versioning):** Uploading a new file for the same Customer Document Reference replaces the current file logically. The service uses a generated, collision-resistant blob name and updates the reference to the new object; it does not overwrite a caller-supplied blob path. Previous blob versions are not exposed or retained as an application history in the first release; cleanup of a superseded object is required after the new metadata is safely stored. A later version-history requirement would need an explicit audit and retention decision.
- **Assumption (Idempotency):** No separate public idempotency-key contract is added in the first release. A repeated request is treated as a new upload and produces a new generated object key unless the implementation uses a documented request identifier internally. The design must prevent a partially failed upload from leaving an active database reference to a missing object.
- **Assumption (Ownership and access):** A document belongs to exactly one Customer and one Customer Document Reference. Upload, metadata read, and download require the existing authenticated `SystemAdmin` policy in the first release. The API validates that the route Customer Code, document reference, and authenticated caller's permitted scope all identify the same customer. Blob containers are private; clients never receive storage credentials or an unauthenticated public URL. Broader role-based customer access remains a future authorization decision.
- **Assumption (Download behavior):** Authenticated API download of the latest file is included because the existing Customer Onboarding document UI has a View control. The API streams the blob with the persisted safe content type and original file name and does not expose a raw Azure URL. Range requests, thumbnail generation, online preview conversion, and public/anonymous download are outside the first release.
- **Assumption (Delete behavior):** The first release has no customer-document delete endpoint or UI delete action. A user may replace the latest file according to the replacement rule, but cannot remove the document reference or blob independently. Hard delete, soft delete, retention expiry, legal hold, and administrator purge require a separate confirmed requirement. Failed replacement cleanup may be handled by an internal compensating cleanup path and must not make an unreferenced object downloadable.
- **Assumption (Atomicity boundary):** Database changes and Azure Blob operations cannot share one EF transaction. The service follows a compensating sequence: validate before writing, upload the new object, persist the metadata, and delete the prior object only after the new metadata commits. If database persistence fails, the newly uploaded object is deleted best-effort and the prior metadata remains unchanged. If cleanup fails after a successful replacement, the new reference remains authoritative and the cleanup failure is logged for operational remediation without exposing it to the user as an upload failure.
- **Assumption (Security and privacy):** Raw file contents, storage connection strings, SAS tokens, bearer tokens, and unnecessary PII are excluded from logs, exceptions, audit details, and telemetry. Malware scanning, DLP classification, OCR, encryption-key ownership, and content redaction are not added unless the platform supplies an approved boundary; the design must identify this residual risk for confirmation.
- **Assumption (Frontend scope):** The first release includes frontend changes in `E:\Finnova\Finnova-UI\Finnova-UI` as well as backend changes. The UI extends the existing Constitution Documents tab, shared Customer Onboarding service, model, real service, mock service, route, and test conventions. It does not introduce Redux, RxJS, a second axios client, or a new localization system.
- **Assumption (Configuration and local development):** Local development and tests may use Azurite through the existing Azure connection-string option. Production uses an Azure Storage account configured outside source control. A test double for `IBlobStorageService` is used in unit and integration tests rather than requiring a live cloud account.

## Glossary

- **Customer Onboarding**: The SystemAdmin capability that creates, modifies, queries, and maintains customer-owned onboarding data, including Customer Document References.
- **Customer**: The persisted customer aggregate identified by an immutable system-generated Customer Code and internal identifier.
- **Customer Document Reference**: The existing customer-owned row that links a Customer to an active Document Master definition and stores approved document details and storage metadata.
- **Document Master**: The authoritative catalog of configured document definitions, including document code, allowed MIME types, maximum file size, mandatory/image flags, lifecycle status, and applicability rules.
- **Document Definition**: One active Document Master record selected by its identifier for a Customer Document Reference.
- **Blob**: The binary object stored in Azure Blob Storage. A blob is not stored in the Customer database row.
- **Blob Storage Service**: The provider-agnostic `IBlobStorageService` boundary used by the application to upload, download, check, and delete blobs.
- **Azure Blob Storage**: The selected first-release storage provider, accessed through the existing `AzureBlobStorageService` implementation and private container.
- **Blob Container**: The private Azure container holding customer document objects. Its name is configuration, not caller input.
- **Blob Key**: The generated object name inside the configured container. It contains no unsanitized caller path and is not accepted from the request.
- **Original File Name**: The client-supplied name retained as display metadata after path stripping and safe validation. It is never used as the blob key without sanitization.
- **Content Type**: The validated media type associated with the uploaded file, such as `application/pdf`, `image/jpeg`, or `image/png`.
- **File Signature**: The initial bytes or other trusted content inspection used to detect a declared MIME type that does not match the actual file format.
- **Latest File**: The single current blob referenced by a Customer Document Reference after the most recent successful upload.
- **Upload Metadata**: The non-binary information persisted for a successful upload, including ownership, blob key, original file name, content type, size, actor, and UTC timestamp.
- **Replacement**: A successful new upload for a Customer Document Reference that changes the latest file reference from an older blob to a newly generated blob.
- **Download**: An authenticated API response that streams the latest blob without exposing storage credentials or a public storage URL.
- **Uploader**: The authenticated actor recorded against a successful upload, normally the System Administrator identity from the JWT.
- **DMS Reference**: The existing Customer Document Reference field used by current onboarding contracts for a document-management reference. It may carry the approved blob reference, but it must not contain raw content.
- **API Gateway**: `Finnova.ApiGateway`, the YARP reverse proxy and the only origin used by the UI for backend calls.
- **CustomerOnboarding_Service**: The backend application/API boundary responsible for validating customer document ownership, coordinating Blob Storage, and persisting metadata.
- **Customer_Document_UI**: The Constitution Documents area of the React Customer Onboarding screen that selects a file, uploads it, displays status, and invokes View.
- **System Administrator**: An authenticated user whose JWT satisfies the existing `SystemAdmin` authorization policy.
- **EARS**: Easy Approach to Requirements Syntax, using ubiquitous, event-driven, state-driven, unwanted-event, optional-feature, and complex patterns.

## Requirements

### Requirement 1: Validate an Upload Request (Event-driven and Unwanted-event)

**User Story:** As an authorized Customer Onboarding user, I want invalid document uploads rejected before storage, so that only applicable India customer documents enter the platform.

#### Acceptance Criteria

1. WHEN an authenticated System Administrator submits one file for an existing Customer Code and Customer Document Reference, THE CustomerOnboarding_Service SHALL validate the route Customer, document reference, active Document Master definition, applicability rule, original file name, content type, file signature, and byte length before writing a blob or changing database metadata.
2. IF the Customer Code is missing, malformed, or does not identify an existing Customer, THEN THE CustomerOnboarding_Service SHALL reject the request with a not-found or validation response, SHALL not call Blob Storage, and SHALL not change any Customer Document Reference.
3. IF the document reference is missing, does not belong to the route Customer, or its Document Definition is unknown, inactive, or not applicable to the Customer, THEN THE CustomerOnboarding_Service SHALL reject the request with the existing Customer document-reference error family, SHALL not call Blob Storage, and SHALL not change any database row.
4. IF the request contains no file, more than one file, an empty file, an unsafe file name, or a file name containing a path component, THEN THE CustomerOnboarding_Service SHALL reject the request with a field-level English validation error and SHALL not persist or retain an upload object.
5. IF the file's declared or inspected content type is not one of the permitted configured types, or the declared type does not match the file signature, THEN THE CustomerOnboarding_Service SHALL reject the request with a validation error identifying the file type and SHALL not persist or retain an upload object.
6. IF the file exceeds the applicable configured `MaxFileSizeMb` or the 25 MiB global ceiling, THEN THE CustomerOnboarding_Service SHALL reject the request with a validation error identifying the maximum permitted size and SHALL not call Blob Storage.
7. THE CustomerOnboarding_Service SHALL normalize only the approved MIME aliases and file-name metadata needed for the contract, SHALL preserve one plain English display value, and SHALL not add bilingual document-name fields, Arabic values, non-English seed data, or RTL metadata.

### Requirement 2: Store the Document in Azure Blob Storage (Event-driven and State-driven)

**User Story:** As an authorized Customer Onboarding user, I want a valid file stored in private Azure Blob Storage, so that customer document bytes are durable without being stored in the relational database.

#### Acceptance Criteria

1. WHEN a valid upload request passes Requirement 1, THE CustomerOnboarding_Service SHALL upload the file through `IBlobStorageService` using the configured private customer-document container and a service-generated collision-resistant Blob Key.
2. THE CustomerOnboarding_Service SHALL never accept a container name, Blob Key, absolute storage URI, connection string, SAS token, or storage credential from the caller as authoritative input.
3. WHILE a Customer Document Reference has a successful current upload, THE CustomerOnboarding_Service SHALL treat the persisted Blob Key and ownership metadata as the only authoritative pointer to the latest file.
4. WHEN a replacement upload succeeds for an existing Customer Document Reference, THE CustomerOnboarding_Service SHALL point the reference to the new generated Blob Key and SHALL not overwrite a caller-selected object or expose the prior object as the latest file.
5. THE CustomerOnboarding_Service SHALL pass the validated content type to Blob Storage and SHALL preserve the measured byte length returned by the storage operation in the upload metadata.
6. IF Azure Blob Storage is unavailable, rejects the upload, times out, or returns an unexpected provider failure, THEN THE CustomerOnboarding_Service SHALL return a recoverable service-unavailable or storage-failure response, SHALL not change the existing database metadata, and SHALL log a correlation identifier and provider-safe failure category without logging file bytes, credentials, or tokens.

### Requirement 3: Persist and Return Upload Metadata (Event-driven and Complex)

**User Story:** As an authorized Customer Onboarding user, I want the uploaded file linked to its Customer Document Reference, so that the onboarding record can identify and retrieve the current document.

#### Acceptance Criteria

1. WHEN Blob Storage confirms a successful upload and the metadata transaction commits, THE CustomerOnboarding_Service SHALL persist metadata linking exactly one Customer, one Customer Document Reference, and one current Blob Key, together with the sanitized original file name, validated content type, byte length, uploader identity, and UTC upload timestamp.
2. THE CustomerOnboarding_Service SHALL store a reference or metadata representation only; it SHALL not store raw document bytes, base64 content, a public storage URL, a bearer token, or a SAS token in Customer entities, contracts, audit rows, logs, or messages.
3. WHEN an upload succeeds, THE CustomerOnboarding_Service SHALL return the Customer Document response with the current upload status and approved metadata but SHALL not return raw bytes or storage credentials.
4. WHEN a replacement upload succeeds, THE CustomerOnboarding_Service SHALL update the latest metadata as one logical Customer Document change and SHALL make the new file available to a subsequent authorized download request.
5. IF metadata persistence fails after a new Blob has been uploaded, THEN THE CustomerOnboarding_Service SHALL leave the prior Customer Document Reference unchanged, SHALL attempt compensating deletion of the new Blob, SHALL return an error to the caller, and SHALL record a provider-safe operational log if compensation fails.
6. IF a replacement's old-Blob cleanup fails after the new metadata commits, THEN THE CustomerOnboarding_Service SHALL keep the new metadata authoritative, SHALL return the successful upload result, and SHALL emit an operational warning containing no raw content or secret.
7. THE CustomerOnboarding_Service SHALL preserve the existing Customer Onboarding rule that Document Master display values are resolved through read projection; it SHALL not copy a Document Master name as an independently authoritative customer field.

### Requirement 4: Retrieve the Latest Customer Document (Optional-feature and Event-driven)

**User Story:** As an authorized Customer Onboarding user, I want to view the latest uploaded document, so that I can verify the document collected for a customer.

#### Acceptance Criteria

1. WHERE the first-release download option is enabled, WHEN an authenticated System Administrator requests the file for an existing Customer Code and Customer Document Reference, THE CustomerOnboarding_Service SHALL verify ownership and return a stream of the latest Blob with the persisted safe content type and sanitized original file name.
2. WHERE the first-release download option is enabled, IF the Customer Document Reference has no current upload, THEN THE CustomerOnboarding_Service SHALL return a not-found or document-not-available response and SHALL not return an empty or fabricated file.
3. WHERE the first-release download option is enabled, IF the persisted Blob is missing from Azure Blob Storage, THEN THE CustomerOnboarding_Service SHALL return a document-not-available error, SHALL not expose the storage provider's raw exception, and SHALL log the missing-object category with a correlation identifier.
4. THE CustomerOnboarding_Service SHALL not redirect the caller to an unauthenticated storage URL and SHALL not return a SAS token in a response.
5. **Assumption:** Authenticated API download is part of the first release to support the existing Customer Onboarding View control. Range requests, thumbnails, conversion, and anonymous/public download are not part of this requirement.

### Requirement 5: Define Replacement and Delete Semantics (State-driven and Unwanted-event)

**User Story:** As a product owner, I want replacement and deletion behavior explicit, so that a failed upload cannot orphan or remove the wrong customer document.

#### Acceptance Criteria

1. WHILE a Customer Document Reference has no current Blob, THE first successful valid upload SHALL create its current upload metadata and make the file retrievable under the ownership rules.
2. WHILE a Customer Document Reference has a current Blob, a successful valid upload SHALL replace the current pointer with a new generated Blob Key and SHALL preserve the document reference itself.
3. IF a caller supplies a Blob Key, container, storage URI, or path intended to overwrite another customer's object, THEN THE CustomerOnboarding_Service SHALL ignore it as non-authoritative or reject the request and SHALL never write outside the service-generated customer/document scope.
4. IF a replacement fails before metadata commit, THEN THE CustomerOnboarding_Service SHALL preserve the previous current pointer and SHALL not report the new Blob as the current document.
5. THE first release SHALL not expose a public or UI delete operation for a Customer Document Reference or its Blob. Any later delete, retention, legal hold, or purge behavior SHALL be specified with explicit authorization and audit requirements before implementation.
6. **Assumption:** The proposed first-release replacement policy is latest-file replacement with no user-visible version history. A versioned document history or hard/soft delete policy requires product confirmation.

### Requirement 6: Enforce Authentication, Authorization, and Ownership (Ubiquitous and Unwanted-event)

**User Story:** As a security stakeholder, I want customer document bytes and metadata protected by the existing Customer Onboarding policy, so that one customer cannot access another customer's documents.

#### Acceptance Criteria

1. IF an upload or download request has a missing, expired, malformed, or signature-invalid JWT Bearer token, THEN THE CustomerOnboarding_Service SHALL reject it with HTTP 401, SHALL not call Blob Storage, and SHALL not modify database metadata.
2. IF an authenticated caller lacks the `SystemAdmin` role required by the existing Customer Onboarding policy, THEN THE CustomerOnboarding_Service SHALL reject the request with HTTP 403, SHALL not call Blob Storage, and SHALL not modify database metadata.
3. IF an authenticated System Administrator requests a document reference that belongs to a different Customer Code, THEN THE CustomerOnboarding_Service SHALL reject the request as forbidden or not found according to the platform's privacy policy, SHALL not reveal the other customer's existence or metadata, and SHALL not call Blob Storage.
4. THE CustomerOnboarding_Service SHALL enforce authorization before file access and ownership validation before download, and SHALL not rely on a client-supplied customer identifier, file name, or blob path as proof of ownership.
5. THE CustomerOnboarding_Service SHALL preserve the existing SystemAdmin JWT scheme, `SystemAdmin` policy, Customer Onboarding route, and middleware ProblemDetails contract. Any new storage/document error code SHALL be stable, English, and free of raw provider details.
6. WHEN an authorized upload succeeds, THE CustomerOnboarding_Service SHALL record the authenticated actor identifier and UTC event time in the approved metadata/audit boundary, without recording the file content or secret material.

### Requirement 7: Provide Customer Onboarding UI Upload Behavior (Event-driven and State-driven)

**User Story:** As a System Administrator, I want to upload and view a customer document from the Constitution Documents tab, so that I can complete onboarding without manually using storage tools.

#### Acceptance Criteria

1. WHEN the Customer_Document_UI displays an existing Customer Document Reference, THE UI SHALL show the English document code/description, configured allowed type and size guidance, current upload status, and Upload and View controls according to the document definition and current Customer Onboarding mode.
2. WHILE Customer Onboarding is in create mode and no Customer Code exists, THE UI SHALL disable the upload action or queue the selected file without sending it until the Customer create request succeeds and the document reference is available.
3. WHEN a System Administrator selects one file and confirms upload, THE UI SHALL perform client-side checks for one file, non-empty content, supported configured type, and maximum size, then SHALL send multipart form data through the existing Customer Onboarding service and shared axios instance.
4. IF client-side validation fails, THE UI SHALL display an English field-level message, SHALL not send a request, and SHALL preserve the other Customer form values.
5. WHEN the upload request is in progress, THE UI SHALL show a busy state for that document row, prevent duplicate submission for the same row, and SHALL not block unrelated read-only document metadata from rendering.
6. WHEN the upload succeeds, THE UI SHALL update the row with the current file name, size/type/status, enable View when permitted, and display a success indication without exposing a Blob Key, storage URI, SAS token, or raw content in the page state.
7. IF the upload or View request fails, THE UI SHALL display the shared API error message or an English fallback, SHALL preserve the prior row state, and SHALL offer a retry path for recoverable failures.
8. WHEN the UI calls the backend, THE UI SHALL use the existing `customerOnboardingService`, `VITE_USE_MOCK_API` selection, shared axios JWT interceptor, and existing Customer Onboarding route composition; it SHALL not create an alternate axios client or authentication mechanism.
9. THE Customer_Document_UI SHALL render English text in the existing left-to-right Material UI layout and SHALL not add bilingual labels, Arabic/non-English values, translation scaffolding, or RTL handling.
10. **Assumption:** The first release includes UI upload and View behavior in the existing Constitution Documents tab. Delete, version-history browsing, drag-and-drop, progress percentage, preview conversion, and bulk selection are not required unless confirmed.

### Requirement 8: Preserve Error, Reliability, and Privacy Boundaries (Complex and Unwanted-event)

**User Story:** As an operations and compliance stakeholder, I want predictable failure behavior, so that storage failures do not create misleading or unsafe customer records.

#### Acceptance Criteria

1. IF any input validation, customer/document lookup, authorization, Blob Storage upload, metadata persistence, or compensating cleanup operation fails, THEN THE CustomerOnboarding_Service SHALL return the platform's stable ProblemDetails shape with an English safe detail and SHALL not expose a stack trace, connection string, token, SAS token, raw provider response, or file bytes.
2. IF a failure occurs before Blob Storage receives the file, THEN THE service SHALL persist no upload metadata and SHALL leave any previous current document unchanged.
3. IF a failure occurs after Blob Storage receives a new file but before metadata commits, THEN THE service SHALL attempt to delete the new file and SHALL leave no database pointer to that file.
4. IF the service cannot prove that a Customer Document Reference belongs to the route Customer, THEN THE service SHALL fail closed and SHALL not read, write, list, or delete any Blob for that request.
5. THE CustomerOnboarding_Service SHALL use cancellation tokens for request-bound storage, database, and stream operations, and SHALL stop work when the HTTP request is cancelled where the underlying provider supports cancellation.
6. THE service SHALL constrain upload processing to the configured file-size limit, stream content without requiring an unbounded in-memory copy, and SHALL avoid logging file names when the name could contain sensitive customer data beyond the approved metadata contract.
7. THE feature SHALL retain raw document bytes only in the configured private Azure container and SHALL not include them in EF Core change tracking snapshots, Customer API projections, audit entries, integration messages, frontend local storage, or test fixtures.

## Non-Functional Requirements

- The API SHALL use the existing .NET 10, ASP.NET Core, MediatR, FluentValidation, EF Core, and `IBlobStorageService` conventions already present in the backend repository.
- The API SHALL use the existing `Finnova.ApiGateway` as the UI origin and SHALL preserve the existing Customer Onboarding alias route to the SystemAdmin cluster.
- Upload and download operations SHALL be cancellation-aware and SHALL avoid unbounded memory allocation; the configured maximum file size is an enforced resource boundary, not only a UI hint.
- Azure credentials and connection strings SHALL come from deployment configuration or a secret store. No credential, SAS token, or raw document content may appear in source control, test output, API errors, logs, or frontend storage.
- Unit tests SHALL use a fake `IBlobStorageService` for validation, key generation, replacement, compensation, and mapping behavior. Integration tests SHALL verify host authorization, multipart binding, gateway-compatible route behavior, database metadata, and storage-failure responses without requiring a live Azure account.
- Frontend tests SHALL use Vitest and React Testing Library to cover file validation, upload busy/success/error states, mock/real service contracts, and View behavior. No Redux or RxJS test infrastructure is introduced.
- User-facing values, labels, errors, mock data, and tests SHALL be English-only, India-compatible, and left-to-right.

## Acceptance Criteria

1. A valid single PDF, JPEG/JPG, or PNG upload for an existing applicable Customer Document Reference is stored through the existing Azure Blob Storage abstraction and returns safe upload metadata.
2. Invalid customer/document ownership, inactive or inapplicable definitions, unsupported MIME types, signature mismatches, empty files, unsafe names, and over-limit files are rejected before a Blob or database metadata change is made.
3. The Customer database stores only the approved current Blob reference and metadata; it stores no raw bytes or base64 content.
4. A successful replacement updates the current reference to a newly generated object, preserves the prior reference on failure, and attempts cleanup according to the compensating sequence.
5. Authenticated SystemAdmin upload and download requests succeed only for the matching Customer Document Reference; unauthenticated and unauthorized requests fail with the existing 401/403 behavior.
6. A download streams the latest file through the API with the safe persisted content type and original file name and never returns a public Azure URL or SAS token.
7. A missing or unavailable Blob returns a stable safe error and does not expose provider internals.
8. The Customer Onboarding Constitution Documents UI supports one-file client validation, upload, busy, success, retryable error, and View states through the shared service/toggle/axios patterns.
9. Backend tests cover validation, authorization, ownership, metadata persistence, replacement compensation, missing blobs, and host wiring; frontend Vitest tests cover service and UI behavior.
10. The feature introduces no non-English or bilingual fields, no RTL behavior, no raw document data in logs/contracts/tests, no alternate HTTP client, and no source changes outside the documented backend and frontend task files.
11. The final implementation confirms or explicitly revises the storage configuration, metadata schema, allowed MIME aliases, 25 MiB hard ceiling, replacement/no-version policy, SystemAdmin access scope, download inclusion, delete deferral, and frontend-inclusion assumptions recorded above.

## Out of Scope

- AWS S3 implementation changes, a new storage provider, a new API host, or a replacement for the existing `IBlobStorageService` abstraction.
- Storing raw document bytes, base64 content, thumbnails, OCR output, extracted text, document previews, or binary data in SQL Server, Customer API responses, logs, audit entries, or messages.
- Multi-file, bulk, archive, drag-and-drop, resumable, chunked, or background upload workflows.
- Anonymous/public download, direct browser access to Azure, SAS-token issuance, range requests, thumbnail generation, file conversion, or online document editing.
- User-facing document version history, hard delete, soft delete, retention expiry, legal hold, administrator purge, or restore workflows.
- Malware scanning, DLP, OCR, document classification, redaction, or external DMS/Document-Verifier integration unless a separate approved boundary and contract is provided.
- Changes to Document Master lifecycle, applicability, allowed-type taxonomy, or customer identity-document uniqueness rules beyond consuming the existing authoritative definitions.
- Changing Customer Onboarding authorization, Customer Code generation, Customer create/modify JSON contracts, bank/default rules, or unrelated onboarding sections.
- Redux, Redux Toolkit, RxJS, a second axios client, translation infrastructure, Arabic/non-English seed data, or RTL support.
- Changes to generated `bin`/`obj` artifacts, deployment secrets, or source files outside the feature files listed in the design and tasks documents.
