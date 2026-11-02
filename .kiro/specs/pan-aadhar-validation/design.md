# Design Document: PAN-Aadhaar Validation

## Overview

This design updates FINNOVA-15 to the finalized identity-verification requirements. It keeps the existing PAN identifier and Aadhaar identifier validations as separate operations and adds two independent comparison APIs:

- `verify-pan-name`: compares a submitted name with the usable name represented by a PAN document or configured extracted value.
- `verify-aadhar-address`: compares a submitted address with the usable address represented by an Aadhaar document or configured extracted value.

The feature has four independent automated operations:

1. `PanIdentifierValidation` — identifier validation through the existing PAN validation operation.
2. `AadhaarIdentifierValidation` — identifier validation through the existing Aadhaar validation operation.
3. `PanNameVerification` — PAN document-name comparison through a configured adapter.
4. `AadhaarAddressVerification` — Aadhaar document-address comparison through a configured adapter.

The comparison adapters are provider-neutral at the public boundary. This design does not name a government provider, invent a provider request body, or assume undocumented response fields. Each adapter owns its provider-specific DTOs, serialization, response mapping, and configured input mapping.

The feature is hosted in `Finnova.SystemAdminService`, persisted through the existing EF Core repository layer, and routed through the existing `Finnova.ApiGateway` SystemAdmin cluster. The frontend is a React 18 + TypeScript + MUI feature in the separate Finnova-UI repository.

## Architecture

```text
React/MUI Identity Verification page
        |
        | shared axios + JWT bearer token
        v
Finnova.ApiGateway
  /api/systemadmin/**
  optional /api/ua/api/identity-verification/** compatibility aliases
        |
        v
Finnova.SystemAdminService
  IdentityVerificationController
        |
        v
MediatR commands, queries, validators, workflow coordinator
  |                 |                         |
  |                 |                         +-- IProtectedDocumentStore
  |                 +-- IIdentityVerificationRepository
  +-- four operation adapter ports
        |
        +-- identifier adapters (existing PAN/Aadhaar operation boundaries)
        +-- comparison adapters (PAN name, Aadhaar address)
        +-- retry/timeout executor
        |
        v
Configured external providers, accessed only through adapters

Finnova.Repository / FinnovaDbContext
  VerificationSessions
  ValidationRecords (append-only)
  ProtectedDocumentReferences (metadata only)
  protected document bytes through the approved protected store
```

The controller does not call providers or inspect provider DTOs. Validators reject malformed identifiers, missing configured inputs, and invalid files before an adapter is called. The coordinator creates one `VerificationSession` for a combined workflow, runs each requested operation independently, persists each completed result, and returns an operation-keyed response.

### 1.2 Operation and result separation

Identifier operations use `ValidationStatus`:

- `Success` — the configured identifier provider accepted the identifier.
- `Failure` — the provider returned a usable invalid, not-found, or business-failure result.
- `Unavailable` — the provider could not be reached or completed after the configured timeout and retry policy.

Comparison operations use `ComparisonOutcome`:

- `Match` — usable source values were compared and matched under the configured comparison rules.
- `NonMatch` — usable source values were compared and did not match.
- `NoUsableMatch` — the adapter could not obtain reliable source data or a reliable comparison decision.

The two domains are deliberately not represented by one ambiguous Boolean. A normalized operation result contains exactly one applicable status field: identifier results contain `validationStatus`, while comparison results contain `comparisonOutcome`.

## 2. Repository conventions reused

The design follows the files and patterns inspected in the backend repository:

| Concern | Existing convention | Design use |
|---|---|---|
| Host | `Finnova.SystemAdminService/Program.cs` | Register services, MediatR handlers, FluentValidation, authentication, authorization, repository, and options here. |
| Controller | `Finnova.SystemAdminService/Controllers/LookupController.cs` | Thin `[ApiController]` with MediatR dispatch and `[Authorize(Policy = "SystemAdmin")]`. |
| Middleware | `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` | Add typed identity-verification branches while retaining RFC 7807 `ProblemDetails` and the stable `code` extension. |
| Models | `Finnova.Models/Domain/Entities`, `Domain/Enums`, and `Contracts/*` | Add feature entities, enums, exceptions, and transport-neutral records in the existing folders. |
| EF | `Finnova.Repository/Context/FinnovaDbContext.cs` and `Configuration/*Configuration.cs` | Add `DbSet` properties and `IEntityTypeConfiguration<T>` classes discovered by `ApplyConfigurationsFromAssembly`. |
| Repositories | `Finnova.Repository/Interfaces` and `Repositories/*Repository.cs` | Add feature-specific query methods and append-only methods alongside the existing repository interfaces. |
| Application layer | `Finnova.Service/<Feature>` | Use MediatR requests/handlers, FluentValidation, mapping helpers, and explicit service ports. |
| Pagination | `Finnova.Models.Contracts.Common.PaginatedResponse<T>` | Use the existing `Data`, `Total`, `Page`, `PageSize`, and `TotalPages` response shape. |
| Auth | `Program.cs` JWT setup and `SystemAdmin` policy | Reuse the existing bearer scheme and role policy. Do not create a second token or authorization mechanism. |
| Tests | `Finnova.Tests`, xUnit, Moq, FsCheck.Xunit, `WebApplicationFactory<Program>` | Add property, example, and host integration coverage without running it during specification authoring. |

No new backend host is required. The UI path supplied in the product context was not available in this workspace, so the frontend paths below follow the required existing `services/interfaces`, `services/real`, `services/mock`, toggle, React hooks, MUI, route, and navigation patterns and must be reconciled with the UI repository before implementation.

## 3. Reused versus new components

### 3.1 Reused

- `Finnova.SystemAdminService` and its existing `SystemAdmin` authorization policy.
- JWT bearer authentication configured in `Finnova.SystemAdminService/Program.cs`.
- `MediatR` assembly registration and `ValidationBehavior<,>`.
- FluentValidation validators and the existing middleware pipeline.
- `FinnovaDbContext`, SQL Server provider, repository registration extension, and EF configuration discovery.
- `PaginatedResponse<T>` and the existing default page conventions.
- `Finnova.ApiGateway` `systemadmin-route` and `systemadmin-cluster`.
- The shared UI axios instance, bearer-token interceptor, error notification behavior, mock/real/toggle service pattern, React hooks, MUI tables/dialogs, and application route/navigation conventions.

### 3.2 New

- Four-operation domain model and normalized result types.
- `VerificationSession`, immutable `ValidationRecord`, `SourceSummary`, and `ProtectedDocumentReference`.
- Identifier and comparison request/response contracts.
- Neutral adapter ports and configured comparison input mappings.
- Provider-specific adapter implementations kept behind the ports.
- Protected document-store port and metadata repository strategy.
- Workflow coordinator for PAN+name, Aadhaar+address, and full four-operation flows.
- Manual review/override commands, query handlers, and operation-specific result projections.
- Identity-verification exceptions and middleware branches.
- Frontend model, service implementations, verification page, result cards, history filters, and tests.

## 4. Requirements coverage map

The implementation locations below cover every finalized requirement without binding the public contract to a provider wire schema.

| Requirement | Design coverage |
|---|---|
| 1. Existing PAN identifier validation | `PanIdentifierValidation` operation, PAN identifier validator, existing endpoint-compatible controller action, identifier adapter port, normalized `ValidationStatus`, append-only record. |
| 2. Existing Aadhaar identifier validation | `AadhaarIdentifierValidation` operation, Aadhaar validator, existing endpoint-compatible controller action, independent adapter port, normalized `ValidationStatus`, append-only record. |
| 3. `verify-pan-name` | `PanNameVerification` request/action, configured source validation, PAN comparison adapter, `ComparisonOutcome`, source summary, manual-review affordance. |
| 4. `verify-aadhar-address` | `AadhaarAddressVerification` request/action, configured source validation, Aadhaar comparison adapter, `ComparisonOutcome`, source summary, manual-review affordance. |
| 5. Provider-neutral configurable adapter contract | `ProviderNeutralComparisonInput`, `ComparisonInputMappingOptions`, adapter ports, isolated provider DTOs, pre-call configuration validation, protected document boundary. |
| 6. Combined/full workflows | `VerificationSession`, workflow request mode (`Partial` or `Atomic`), independent operation execution, operation-keyed response, partial-failure handling, parent-child persistence. |
| 7. Manual review and override | Manual PAN/Aadhaar requests, comparison decision requests, protected document validation/storage, linked immutable manual records, decline handling, no automatic conversion of `NonMatch`. |
| 8. Reliability and errors | Per-operation timeout, transient retry executor, cancellation, normalized unavailable/no-usable mapping, safe diagnostic references, no-call rejection path. |
| 9. Persistence/audit/immutability | Append-only `ValidationRecord`, session parent, server UTC timestamps, protected/minimized fields, source links, repository mutation rejection, transaction and persistence-error handling. |
| 10. Query/search history | Paged query repository methods, operation/status/outcome/method/session/date/search filters, deterministic ordering, privacy projection, protected document endpoint. |
| 11. Auth/gateway access | Existing JWT and `SystemAdmin` policy, controller authorization, gateway SystemAdmin route and optional UI compatibility aliases, shared axios bearer/error behavior. |
| 12. Privacy/security/India-only rules | PAN/Aadhaar validators, English-only contracts and fixtures, structured redaction, protected transport/storage, file signature checks, fail-closed configuration. |
| 13. Frontend verification experience | Interface/real/mock/toggle service, four operation forms, workflow result rows, override affordances, history grid, masked values, MUI route and navigation. |
| 14. API contract consistency | Separate identifier/comparison result contracts, stable error envelope with correlation ID, safe messages, existing identifier compatibility, comparison-only endpoint responses. |

## Data Models

### 5.1 Enums

The enums belong in `Finnova.Models/Domain/Enums` and are serialized by the existing `JsonStringEnumConverter` in the SystemAdmin host.

```text
VerificationOperationType
  PanIdentifierValidation
  AadhaarIdentifierValidation
  PanNameVerification
  AadhaarAddressVerification

ValidationStatus
  Success
  Failure
  Unavailable

ComparisonOutcome
  Match
  NonMatch
  NoUsableMatch

VerificationMethod
  Auto
  ManualOverride

VerificationWorkflowType
  Single
  PanAndName
  AadhaarAndAddress
  Full

WorkflowInputMode
  Partial
  Atomic

VerificationSourceType
  None
  DocumentFile
  ExtractedValues
  DocumentAndExtractedValues
```

The operation spelling uses `Aadhaar` in domain names and user-facing text. The public route retains the requested `aadhar` spelling for `verify-aadhar-address`.

### 5.2 Correlation identifier

`CorrelationId` is a server-generated opaque identifier, represented internally as a `Guid` or validated opaque string and returned in normalized responses and error envelopes. A workflow uses one correlation identifier for the session and its child operations. Provider calls receive that identifier through `ProviderNeutralComparisonInput` or the identifier adapter context. It is never derived from PAN, Aadhaar, a document name, or a provider secret.

### 5.3 Verification session

`VerificationSession` is the immutable parent context for one combined or full request. A single operation may also create a session when the API contract requires an auditable parent.

```text
VerificationSession
  Id: Guid
  CorrelationId: string
  WorkflowType: VerificationWorkflowType
  ActorReference: string              // stable user reference, not a token
  CreatedUtc: DateTime                // assigned by server
  RequestedOperations: set<VerificationOperationType>
  InputMode: WorkflowInputMode
```

The session has no aggregate success flag. Aggregate flags would collapse independent outcomes and would be misleading after partial failure. Its child records are the authoritative operation results.

### 5.4 Immutable validation record

`ValidationRecord` is append-only. A new manual decision supplements or supersedes an automated result by creating another row, never by updating the source row.

```text
ValidationRecord
  Id: Guid
  SessionId: Guid?
  CorrelationId: string
  OperationType: VerificationOperationType
  ValidationStatus: ValidationStatus?       // identifier operations only
  ComparisonOutcome: ComparisonOutcome?     // comparison operations only
  Method: VerificationMethod
  CreatedUtc: DateTime                       // server-assigned UTC
  ActorReference: string
  SafeReasonCode: string?
  SafeMessage: string?
  SourceSummary: SourceSummary
  ProtectedDocumentReferenceId: Guid?
  SourceRecordId: Guid?                      // manual record links to automated record
  MaskedSubjectReference: string?            // masked PAN/Aadhaar or irreversible search reference
  ProtectedSearchReference: string?          // keyed/irreversible lookup token
  DiagnosticReference: string?               // safe operations-only reference
```

The domain invariant is exactly one result domain per record: identifier records have one `ValidationStatus` and no `ComparisonOutcome`; comparison records have one `ComparisonOutcome` and no `ValidationStatus`. The model does not store provider payloads, raw error bodies, JWTs, secrets, or document bytes.

### 5.5 Source summary

`SourceSummary` records what was used without retaining unnecessary identity values.

```text
SourceSummary
  SourceType: VerificationSourceType
  MappingProfile: string?                   // configured profile name, not provider schema
  DocumentProvided: bool
  DocumentContentType: string?
  DocumentSizeBytes: long?
  ProtectedDocumentReferenceId: Guid?
  ExtractedValueKeys: IReadOnlyList<string>  // allow-listed semantic keys only
  ExtractionCompleted: bool
```

The submitted comparison value is not stored in clear text. If exact search is approved, the repository stores a keyed search representation. Default responses contain only a masked value or a stable irreversible reference.

### 5.6 Protected document reference

Document bytes cross the controller/service boundary as a stream and are stored through `IProtectedDocumentStore`. The persisted reference contains metadata only:

```text
ProtectedDocumentReference
  Id: Guid
  StorageKey: string                         // opaque protected-store key
  ContentType: string                        // image/jpeg, image/png, application/pdf
  SizeBytes: long
  ContentHash: string                        // integrity check, not a display value
  CreatedUtc: DateTime
  RetentionUntilUtc: DateTime?
```

The design does not require a particular external object store or database BLOB implementation. A platform-approved protected store can be selected during implementation. If SQL Server BLOB storage remains the selected implementation, bytes are still isolated from `ValidationRecord`, query projections, logs, and provider response contracts.

## Components and Interfaces

### 6.1 Adapter ports

The service layer owns the ports. Provider-specific DTOs and HTTP details remain in adapter implementations under the service layer.

```text
IIdentifierValidationAdapter
  OperationType SupportedOperation
  ValidateAsync(IdentifierValidationInput input, CancellationToken token)
    -> IdentifierAdapterResult

IComparisonAdapter
  OperationType SupportedOperation
  VerifyAsync(ProviderNeutralComparisonInput input, CancellationToken token)
    -> ComparisonAdapterResult

IComparisonAdapterResolver
  Resolve(VerificationOperationType operation, ProviderConfiguration configuration)
    -> IComparisonAdapter
```

`IdentifierValidationInput` contains only the normalized identifier, operation type, actor/correlation context, and cancellation token. `IdentifierAdapterResult` contains provider-independent `Success`, `Failure`, or `Unavailable` classification and a safe reason. It does not expose a provider response DTO.

`ProviderNeutralComparisonInput` is the required neutral boundary:

```text
ProviderNeutralComparisonInput
  OperationType: PanNameVerification | AadhaarAddressVerification
  SubmittedValue: string                  // name or address, held in memory only where possible
  ProtectedDocumentReference: ProtectedDocumentReference?
  ProtectedDocumentStream: protected stream? // only while a provider requires streaming
  ExtractedValues: IReadOnlyDictionary<string, string>
  CorrelationId: string
  MappingProfile: string
```

The adapter receives the submitted value only when the configured provider mapping requires it. It receives a protected document reference or protected stream, never a public document URL or unprotected storage key. `ExtractedValues` contains only configured semantic keys such as `documentName` or `documentAddress`; provider-specific field names remain inside the adapter.

### 6.2 Configurable input mapping

The options section is configuration metadata, not a provider wire schema:

```text
IdentityVerificationOptions
  MaxDocumentBytes: 5 MB default
  AcceptedDocumentTypes: JPG, PNG, PDF
  Retry: max attempts, base delay, jitter, retryable failures
  Operations:
    PanIdentifierValidation: IdentifierProviderOptions
    AadhaarIdentifierValidation: IdentifierProviderOptions
    PanNameVerification: ComparisonProviderOptions
    AadhaarAddressVerification: ComparisonProviderOptions

ComparisonProviderOptions
  Enabled: bool
  AdapterName: string
  EndpointBaseAddress: URI?                 // secret-free deployment configuration
  TimeoutSeconds: int
  InputMapping: ComparisonInputMapping

ComparisonInputMapping
  RequiresDocumentFile: bool
  RequiresSubmittedValue: bool
  RequiredExtractedValueKeys: list<string>
  AllowsDocumentAndExtractedValues: bool
  MappingProfile: string
```

The mapping says which neutral inputs are required and which adapter profile to use. It does not specify JSON property names, multipart field names, authentication headers, provider status codes, confidence fields, or response payloads. Those details are private to the selected adapter and its configuration.

Configuration is validated at startup where possible and again before each comparison operation where input-dependent consistency must be checked. A missing adapter, disabled operation, required source mismatch, or invalid mapping returns a safe configuration error and makes no provider call.

### 6.3 Response mapping

Each adapter maps provider-specific responses into one of these internal categories:

```text
IdentifierAdapterResult
  Success(safeReference?, safeMessage?)
  Failure(safeReasonCode, safeMessage)
  Unavailable(safeReasonCode, safeMessage)

ComparisonAdapterResult
  Match(safeMessage?)
  NonMatch(safeMessage?)
  NoUsableMatch(safeReasonCode, safeMessage)
  Unavailable(safeReasonCode, safeMessage) // coordinator normalizes to NoUsableMatch
```

The coordinator converts `Unavailable` from a comparison adapter to `NoUsableMatch`, preserving a safe reason such as `ProviderUnavailable`. It never converts an identifier outage to `Success` and never converts a comparison outage or unusable extraction to `Match`.

## 7. Request and response contracts

Contracts belong in `Finnova.Models/Contracts/IdentityVerification`. The models project must not depend on `IFormFile`; the controller converts multipart input into a service-owned stream/reference contract after validating the file.

### 7.1 Single operation requests

```text
ValidatePanRequest
  Pan: string
  SessionId?: Guid

ValidateAadhaarRequest
  AadhaarNumber: string
  SessionId?: Guid

VerifyPanNameRequest
  SubmittedName: string
  DocumentFile?: multipart file
  ExtractedValues?: map<string,string>
  MappingProfile?: string
  SessionId?: Guid

VerifyAadharAddressRequest
  SubmittedAddress: string
  DocumentFile?: multipart file
  ExtractedValues?: map<string,string>
  MappingProfile?: string
  SessionId?: Guid
```

The existing PAN and Aadhaar request shapes and compatible response fields remain available on their existing endpoint actions. The new comparison actions return only comparison contracts and do not add identifier fields or provider payloads.

### 7.2 Normalized responses

```text
IdentifierValidationResult
  OperationType: PanIdentifierValidation | AadhaarIdentifierValidation
  ValidationStatus: Success | Failure | Unavailable
  RecordId?: Guid
  SessionId?: Guid
  CorrelationId: string
  TimestampUtc: DateTime
  SafeMessage: string
  ManualOverrideAvailable: bool
  MaskedSubjectReference?: string

ComparisonVerificationResult
  OperationType: PanNameVerification | AadhaarAddressVerification
  ComparisonOutcome: Match | NonMatch | NoUsableMatch
  RecordId?: Guid
  SessionId?: Guid
  CorrelationId: string
  TimestampUtc: DateTime
  SafeMessage: string
  ManualOverrideAvailable: bool
  MaskedSubjectReference?: string
```

`RecordId` is present only after durable persistence. A persistence failure returns an error contract instead of claiming that a record was written. A comparison response cannot contain `ValidationStatus`; an identifier response cannot contain `ComparisonOutcome`.

### 7.3 Workflow request and response

```text
VerificationWorkflowRequest
  WorkflowType: PanAndName | AadhaarAndAddress | Full
  InputMode: Partial | Atomic = Partial
  PanIdentifier?: string
  PanSubmittedName?: string
  PanDocumentFile?: multipart file
  PanExtractedValues?: map<string,string>
  AadhaarIdentifier?: string
  AadhaarSubmittedAddress?: string
  AadhaarDocumentFile?: multipart file
  AadhaarExtractedValues?: map<string,string>

VerificationWorkflowResponse
  SessionId: Guid
  CorrelationId: string
  TimestampUtc: DateTime
  PanIdentifierResult?: IdentifierValidationResult
  PanNameResult?: ComparisonVerificationResult
  AadhaarIdentifierResult?: IdentifierValidationResult
  AadhaarAddressResult?: ComparisonVerificationResult
  PartialFailures: IReadOnlyList<WorkflowFailureSummary>
```

Results remain addressable by operation. The workflow does not return one aggregate status that could hide a successful operation or a `NoUsableMatch` result. In `Partial` mode, an invalid operation is rejected while other independently valid operations may run. In `Atomic` mode, all input validation runs first and any invalid operation prevents every provider call.

### 7.4 Manual contracts

Manual PAN and Aadhaar override requests require the identifier, the relevant entered identity data, and either a supported document upload or an approved protected-document reference. A comparison manual decision includes the affected operation, decision (`Match`/`NonMatch` for comparison or `Success`/`Failure` for identifier), safe reason, and source record ID where applicable.

Manual contracts never accept provider-specific fields. They do not replace the automated record. A successful manual action returns a new record ID, operation type, normalized result, correlation ID, and safe metadata.

### 7.5 Error contract

The middleware returns RFC 7807 `ProblemDetails` with:

```text
status, title, detail, code, correlationId, fieldErrors?
```

`detail` and `fieldErrors` contain safe messages and field names only. They do not contain full identifiers, addresses, document bytes, Provider Secrets, tokens, or raw provider bodies. Representative code families are `ERR-IVF-400` validation, `ERR-IVF-401` authentication, `ERR-IVF-403` authorization, `ERR-IVF-404` protected-document/record not found, `ERR-IVF-409` immutable/conflict, `ERR-IVF-413` size, `ERR-IVF-415` format, `ERR-IVF-503` unavailable/configuration, and `ERR-IVF-500` persistence/unexpected errors.

## 8. Workflows and independent results

### 8.1 PAN plus PAN-name

1. Authenticate and authorize the request.
2. Create one session and correlation identifier.
3. Validate the PAN format locally.
4. Validate comparison source configuration and document inputs locally.
5. Invoke PAN identifier validation and PAN-name comparison as separate operations.
6. Apply timeout/retry independently to each call.
7. Persist one record per completed result, including `Unavailable` or `NoUsableMatch` where applicable.
8. Return `panIdentifierResult` and `panNameResult` separately.

### 8.2 Aadhaar plus Aadhaar-address

The same sequence applies to `AadhaarIdentifierValidation` and `AadhaarAddressVerification`. The Aadhaar identifier operation never depends on the address operation's result, and the address comparison never changes the identifier status.

### 8.3 Full four-operation workflow

The coordinator runs all applicable operations independently. `Task.WhenAll` or an equivalent bounded coordinator may be used, but each operation must own its cancellation token, retry budget, adapter call, result normalization, and persistence action. If one adapter fails, times out, returns `NonMatch`, or returns `NoUsableMatch`, the remaining operations continue unless atomic input validation failed before any provider call.

A result that has already been normalized and persisted remains in the response even if another operation later fails. If a persistence action fails, the affected result is returned as a persistence error rather than as a successful recorded result; other durable records remain unchanged.

### 8.4 Manual review transitions

- `Unavailable` identifier result: offer manual identifier override.
- `NoUsableMatch` comparison result: offer manual comparison override.
- Automated `NonMatch`: preserve the automated non-match and allow an authorized manual decision as a separate linked record; never rewrite the automated result.
- Declined review: retain the original unavailable or indeterminate record and do not create a successful manual record.
- Successful review: create a new `ManualOverride` record with the source record ID and protected document reference.

## 9. Persistence and repository strategy

### 9.1 EF model and tables

Add the following `DbSet` properties to `Finnova.Repository/Context/FinnovaDbContext.cs` and add configurations under `Finnova.Repository/Configuration`:

- `VerificationSessions` / `verification_sessions`.
- `ValidationRecords` / `validation_records`.
- `ProtectedDocumentReferences` / `protected_document_references`.

A document store implementation may use a separate protected table or approved object store. If bytes use SQL Server, the reference table remains separate and query projections never include the byte column.

Recommended indexes are:

- `ValidationRecords (CreatedUtc DESC, Id DESC)` for deterministic history ordering.
- `ValidationRecords (OperationType, ValidationStatus, ComparisonOutcome, Method)` for filters.
- `ValidationRecords (SessionId, CreatedUtc)` for workflow reconstruction.
- `ValidationRecords (ProtectedSearchReference)` for authorized exact search.
- `VerificationSessions (CorrelationId)`.
- `ProtectedDocumentReferences (ContentHash)` only if duplicate detection is approved.

### 9.2 Repository interfaces

`IIdentityVerificationRepository` should expose append-only methods rather than generic mutation methods for feature records:

```text
AppendSessionAsync(session)
AppendRecordAsync(record)
AppendRecordsAsync(records)
GetSessionAsync(sessionId)
GetRecordAsync(recordId)
GetPagedAsync(filters, page, pageSize)
GetBySessionAsync(sessionId)
GetProtectedDocumentReferenceAsync(recordId)
```

The repository may inherit shared read methods where useful, but its `UpdateAsync` and `DeleteAsync` implementations must throw the typed identity-verification immutability exception. The controller exposes no update/delete action for validation records or sessions.

Each append operation sets timestamps on the server and uses an explicit transaction when a record and a new protected-document reference must be committed together. A provider result is not returned with `RecordId` until `SaveChangesAsync` succeeds.

### 9.3 Search and privacy projections

The query repository filters by operation, either result domain, method, session, UTC range, and exact authorized search representation. It orders by `CreatedUtc` descending, then `Id` descending. The default projection includes operation, normalized outcome/status, method, timestamps, actor reference, safe reason, session metadata, and masked/irreversible subject reference. It omits secrets, document bytes, raw submitted names/addresses, and unmasked identifiers.

Document preview/download first verifies that the record is authorized, manual, and linked to a protected document. Automated records and records without documents return the same safe not-found result without exposing unrelated data.

## Error Handling

The service normalizes provider, adapter, configuration, persistence, and document failures before they reach the public contract. It never treats an unavailable or indeterminate operation as a successful verification.

### Validation, document handling, and reliability

### 10.1 Pure validation rules

Keep deterministic rules in a pure service helper, for example `Finnova.Service/IdentityVerification/IdentityVerificationRules.cs`:

- Normalize PAN to uppercase, then require exactly five letters, four digits, and one final letter.
- Require Aadhaar to contain exactly 12 digits.
- Require non-empty submitted name/address after trimming.
- Validate comparison mapping requirements before any provider call.
- Validate document extension, declared MIME type, content signature, and configured maximum size.
- Allow only JPG, PNG, and PDF.

The validators must not log rejected values and must not call an adapter. The same rules are used by single-operation, workflow, and manual commands.

### 10.2 Protected document flow

1. Inspect length before buffering; reject over-limit input.
2. Stream to a bounded validator that checks extension, MIME, and file signature.
3. If rejected, discard bytes and return a safe validation error. Do not log or persist them.
4. Validate provider configuration and required mapping before writing protected storage.
5. Write accepted bytes to `IProtectedDocumentStore`, receiving an opaque reference.
6. Pass the protected reference or protected stream only to the adapter.
7. Persist reference metadata with the immutable record when the operation result is recorded.
8. On a persistence failure, perform a best-effort protected-store cleanup and return a persistence error without claiming durable recording.

### 10.3 Timeout and retry

Use a service-owned `IProviderCallExecutor` around adapter calls. It creates a linked cancellation token, applies the configured per-operation timeout, retries only configured transient network/server failures, and applies bounded exponential backoff with jitter. It does not retry provider-declared validation/business results, malformed payloads, configuration errors, or input validation errors.

The retry count, delay, timeout, and retryable classifications come from options. The design does not hard-code a provider policy. After the retry budget:

- identifier call -> `Unavailable`;
- comparison call -> `NoUsableMatch` with an unavailable safe reason.

Cancellation must reach the underlying HTTP request/stream. Diagnostic logs contain only operation type, safe classification, correlation ID, attempt number, and an opaque diagnostic reference.

## 11. Security, authentication, middleware, and gateway

### 11.1 Authentication and authorization

Every identity-verification controller action uses `[Authorize(Policy = "SystemAdmin")]`. The existing JWT setup in `Finnova.SystemAdminService/Program.cs` remains the source of authentication and the existing role claim remains `SystemAdmin`. Authentication is evaluated before authorization, returning 401 before 403. Provider calls occur only after both checks succeed.

### 11.2 Middleware

Extend `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` with typed identity-verification exception branches selected by the `/api/identity-verification` path. Identity-verification exception messages must already be safe because this middleware writes the exception detail into `ProblemDetails`. Add correlation ID to the response header and problem extension. Do not use message inspection to classify provider payloads.

The middleware must map validation, unsupported file, file-size, immutable, not-found, unavailable/configuration, persistence, and unexpected cases to stable `ERR-IVF-*` codes. It must never serialize provider response bodies or exception data containing sensitive values.

### 11.3 Gateway

The existing `systemadmin-route` in `Finnova.ApiGateway/appsettings.json` already matches `/api/systemadmin/{**catch-all}`, removes `/api/systemadmin`, and forwards to the `systemadmin-cluster`. No new provider cluster is required and provider hosts must never become UI origins.

The new direct gateway paths are:

| UI/gateway path | SystemAdmin service path | Purpose |
|---|---|---|
| `POST /api/systemadmin/identity-verification/verify-pan-name` | `POST /api/identity-verification/verify-pan-name` | PAN name comparison |
| `POST /api/systemadmin/identity-verification/verify-aadhar-address` | `POST /api/identity-verification/verify-aadhar-address` | Aadhaar address comparison |
| `POST /api/systemadmin/identity-verification/workflows/pan-and-name` | matching service path | Combined workflow |
| `POST /api/systemadmin/identity-verification/workflows/aadhaar-and-address` | matching service path | Combined workflow |
| `POST /api/systemadmin/identity-verification/workflows/full` | matching service path | Four-operation workflow |
| `POST /api/systemadmin/identity-verification/manual/*` | matching service path | Manual review/override |
| `GET /api/systemadmin/identity-verification/records` | matching service path | Paged history |
| `GET /api/systemadmin/identity-verification/documents/{recordId}` | matching service path | Authorized document retrieval |

Existing PAN and Aadhaar identifier route names remain unchanged. If the UI shared axios base requires `/api/ua/api`, add the established pair of alias routes under `/api/ua/api/identity-verification` with the same transform pattern as the existing lookup aliases. Do not route provider paths directly.

## 12. Frontend design

The UI implementation belongs at `E:\Finnova\Finnova-UI\Finnova-UI` and follows the existing feature pattern described in product context.

### 12.1 Service layer

Add an identity-verification model and service interface under the existing model/service barrels. The interface exposes:

```text
validatePan(request)
validateAadhaar(request)
verifyPanName(request)
verifyAadharAddress(request)
runPanAndNameWorkflow(request)
runAadhaarAndAddressWorkflow(request)
runFullWorkflow(request)
submitManualOverride(request)
getRecords(filters)
getDocument(recordId)
```

Add one real axios implementation using `src/services/api.ts`, one mock implementation, and the existing `VITE_USE_MOCK_API` toggle module. The real service uses the normalized endpoint contracts and never maps provider fields. The mock service includes success, identifier failure, identifier unavailable, match, non-match, no-usable-match, validation error, and partial-workflow scenarios using English-only India-specific sample data.

### 12.2 Page and components

Add a page following the existing `LocationMaster.tsx`/`LookupMaster.tsx` style:

- operation selector and single-operation forms;
- source controls driven by neutral mapping metadata, not provider field names;
- combined PAN/name, Aadhaar/address, and full workflow submission forms;
- four independent result cards or rows, each showing operation, status/outcome, safe reason, timestamp, and manual-review availability;
- manual override dialog scoped to the affected operation;
- paged history grid with operation, status/outcome, method, session, search, date range, and pagination filters;
- protected document preview/download using the document endpoint and no local logging or long-lived byte cache.

The page uses React hooks and MUI. It does not introduce Redux, RxJS, RTL handling, translation scaffolding, or provider-specific form fields. Correctable API errors keep safe unsaved form input available.

### 12.3 Routes and navigation

Add the page route in `App.tsx` and the SystemAdmin navigation entry in `components/Layout/Layout.tsx` using the existing route and permission conventions. Use the direct SystemAdmin gateway base or the configured alias required by the existing axios setup; do not expose provider URLs in browser code.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system. These properties bridge the requirements and executable tests. Property tests use FsCheck.Xunit with at least 100 iterations for pure logic or mocked service boundaries.*

### Property 1: Result domains are mutually exclusive

For any normalized operation result, an identifier operation SHALL contain exactly one `ValidationStatus` from `Success`, `Failure`, or `Unavailable` and no comparison outcome, while a comparison operation SHALL contain exactly one `ComparisonOutcome` from `Match`, `NonMatch`, or `NoUsableMatch` and no validation status.

**Validates: Requirements 1.2, 2.2, 3.2, 3.3, 3.4, 4.2, 4.3, 4.4, 8.2, 8.3, 14.1, 14.2, 14.3**

### Property 2: Rejected input makes no provider call

For any request rejected by identifier validation, comparison source validation, mapping/configuration validation, authentication/authorization gate, or document validation, the service SHALL invoke zero external provider adapters and SHALL not persist rejected document bytes.

**Validates: Requirements 1.3, 2.3, 5.3, 5.5, 5.9, 7.6, 7.7, 7.8, 7.9, 8.12, 11.2, 11.4, 12.9, 12.10**

### Property 3: Valid identifiers preserve operation independence

For any valid PAN and Aadhaar identifiers and any combination of independent adapter results, each requested identifier operation SHALL receive its own adapter call, retry budget, normalized result, and record; a failure or timeout in one operation SHALL not change the other operation's result.

**Validates: Requirements 1.6, 1.7, 2.6, 2.7, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.11, 6.12**

### Property 4: Comparison source mapping is provider-neutral

For any valid comparison configuration, the adapter input SHALL contain only the operation, submitted value when required, protected document input when required, configured extracted-value keys, mapping profile, and correlation identifier; provider-specific field names and DTOs SHALL not appear in the public contract.

**Validates: Requirements 3.6, 3.7, 4.6, 4.7, 5.1, 5.2, 5.4, 5.6, 5.7, 5.8, 5.11, 14.9, 14.10**

### Property 5: Retry and timeout classification is safe

For any transient failure sequence, the executor SHALL stop at the configured maximum attempts; for any timeout, identifier operations SHALL produce `Unavailable` and comparison operations SHALL produce `NoUsableMatch`; non-transient business results SHALL not be retried and no timeout result SHALL be successful.

**Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9**

### Property 6: Workflow results and persistence remain independent

For any partial or full workflow result set, every completed operation SHALL remain addressable with its own normalized result, and every durably completed operation SHALL have exactly one child `ValidationRecord` linked to the same `VerificationSession`; one failed or unavailable operation SHALL not convert or remove another operation's result.

**Validates: Requirements 6.9, 6.10, 6.11, 6.12, 6.15, 6.16, 9.1, 9.8, 9.9, 9.10, 9.11, 9.14, 9.15, 9.16**

### Property 7: Manual decisions append rather than overwrite

For any automated record and valid manual decision, the automated record SHALL remain unchanged, the manual decision SHALL be a separate immutable record, and the manual record SHALL reference the source record and any protected document reference.

**Validates: Requirements 7.2, 7.5, 7.11, 7.12, 7.16, 9.6, 9.7, 9.8, 9.9**

### Property 8: Sensitive data never enters logs or public errors

For any PAN, Aadhaar, submitted name, address, document bytes, Provider Secret, JWT, or arbitrary provider payload, structured logs and public error/record projections SHALL contain only masked or irreversible references, safe operation/status metadata, correlation ID, server UTC timestamp, actor reference, and safe diagnostic references.

**Validates: Requirements 3.6, 4.6, 8.10, 8.11, 9.2, 9.3, 9.12, 9.13, 12.5, 12.6, 12.7, 12.8, 12.10, 14.5, 14.6, 14.7**

### Property 9: Validation records are immutable

For any persisted `ValidationRecord` or `VerificationSession`, every update or delete attempt SHALL be rejected with an immutability error and the original persisted values SHALL remain unchanged.

**Validates: Requirements 9.1, 9.4, 9.5, 9.6, 9.7**

### Property 10: Query filters and ordering are deterministic

For any authorized history query, every returned record SHALL satisfy all supplied operation/status/outcome/method/session/search/date filters, total count SHALL be calculated before pagination, and results SHALL be ordered by server UTC timestamp descending then record ID descending.

**Validates: Requirements 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.12, 10.13, 10.14, 10.15, 10.16**

### Property 11: Manual decline cannot create success

For any unavailable, indeterminate, or non-match operation, declining manual review SHALL preserve the source result and SHALL not create a successful manual result.

**Validates: Requirements 3.8, 4.8, 7.1, 7.13, 7.14, 7.15**

### Property 12: Frontend projection preserves independent safe results

For any normalized workflow response, the UI service/page model SHALL render one result row per returned operation, preserve completed rows when another operation fails, render only normalized statuses/outcomes and safe messages, and expose manual review only for affected operations.

**Validates: Requirements 13.5, 13.6, 13.7, 13.8, 13.9, 13.10, 13.11, 13.13, 13.15, 13.18**

## Testing Strategy

### 14.1 Backend property tests

Use FsCheck.Xunit with a minimum of 100 iterations per property. Keep generators and tested logic free of real network and file-system dependencies. Use spies/mocks for adapters, repositories, document store, clock, retry delay, and logger. Required property test themes are:

- mutual-exclusive identifier statuses and comparison outcomes;
- invalid identifiers, mappings, files, auth gates, and configuration make no provider call;
- provider-neutral source mapping and source summaries;
- retry/timeout classification;
- independent four-operation workflow results and record links;
- no sensitive logging/error projection;
- append-only immutable persistence;
- deterministic query filtering and ordering.

Each test is tagged in the test name or comment as `Feature: pan-aadhar-validation, Property N: <property text>`.

### 14.2 Backend example and edge tests

Use xUnit facts/theories with Moq or EF Core InMemory for:

- existing PAN/Aadhaar endpoint-compatible fields;
- `Match`, `NonMatch`, `NoUsableMatch`, `Failure`, and `Unavailable` response examples;
- missing mapping, missing required document, missing extracted value, and malformed adapter result;
- 5 MB boundary and unsupported extension/MIME/signature;
- manual PAN and Aadhaar required fields;
- manual review after `NonMatch`, unavailable, and no-usable results;
- persistence failure and safe diagnostic reference;
- default page/page size, no-match page, document-not-found behavior;
- middleware error-code and correlation-ID mapping.

### 14.3 Backend integration tests

Use `WebApplicationFactory<Program>` for representative end-to-end cases. Replace external adapters and persistence with test registrations. Cover:

- missing/invalid JWT -> 401 and no provider call;
- authenticated non-SystemAdmin -> 403 and no provider call;
- SystemAdmin single operations through controller and middleware;
- PAN+name, Aadhaar+address, and full workflow partial success;
- query filters and protected manual-document retrieval;
- existing gateway route and any UI compatibility alias;
- CORS/bearer forwarding without exposing provider hosts.

### 14.4 Frontend Vitest tests

Use Vitest and React Testing Library with `jsdom` for:

- service interface real/mock/toggle selection;
- four operation controls and neutral source inputs;
- independent result rendering for all normalized states;
- partial workflow preservation and affected-operation manual override;
- history filters, pagination, masked values, and document endpoint invocation;
- shared error notification and preservation of correctable form input;
- absence of raw provider/sensitive data in rendered output and client logs.

## 15. Design decisions and tradeoffs

1. **Separate status and outcome domains.** Two result domains prevent `Unavailable`, `Failure`, and `NonMatch` from collapsing into a false Boolean. The tradeoff is more contract types and UI branches.
2. **Adapter-owned provider schemas.** This avoids an undocumented external wire contract and keeps provider changes localized. The tradeoff is adapter configuration and mapping work per provider.
3. **Append-only records.** Manual review remains auditable and partial workflows remain reconstructable. The tradeoff is that history contains multiple linked decisions instead of one mutable current row.
4. **Protected document references instead of public file URLs.** This minimizes disclosure and supports either protected SQL storage or an approved object store. The tradeoff is an extra storage abstraction and lifecycle cleanup.
5. **Default partial workflow execution.** Independent results are more useful during provider incidents. Callers that require all-or-nothing input validation can select `Atomic`; even atomic validation never converts a provider failure into success.
6. **Server timestamps and correlation IDs.** This makes audit ordering and diagnostics trustworthy. Caller-supplied timestamps and identity-derived correlation IDs are ignored as authoritative values.
7. **No new host or provider gateway route.** Existing SystemAdmin routing preserves deployment separation. The tradeoff is a possible compatibility alias when the UI's existing base URL requires `/api/ua/api`.

## 16. Implementation-ordered task list

This is the dependency order that the tasks phase must follow. It is intentionally listed here without changing `tasks.md`.

1. **Backend domain:** add operation, status, outcome, method, workflow, source, and document-reference enums; add `VerificationSession`, immutable `ValidationRecord`, `SourceSummary`, and protected document metadata entities.
2. **Backend exceptions:** add typed validation, configuration, provider-unavailable, document, persistence, not-found, and immutability exceptions with safe messages and stable `ERR-IVF-*` codes.
3. **Backend contracts:** add single-operation requests/results, comparison source requests, workflow requests/responses, manual override contracts, history filters, and safe error/projection contracts.
4. **Backend EF configuration:** add session, record, and protected-document configurations; add `FinnovaDbContext` `DbSet`s; add indexes, restricted relationships, and the migration command/instruction.
5. **Backend repositories and protected storage:** add append-only session/record repository interfaces and implementations, paged query methods, document-reference methods, protected-store port/implementation, and DI registrations in `Finnova.Repository/DependencyInjection.cs`.
6. **Backend pure helpers:** add PAN/Aadhaar validators, document signature/size validators, source-mapping validation, result-domain invariants, masking/search-reference helpers, and server-clock/correlation abstractions.
7. **Backend adapters and resilience:** add identifier adapter ports preserving existing operations; add PAN-name and Aadhaar-address comparison ports; add configured options/mappings; add provider-specific implementations behind those ports; add timeout/retry executor and safe response mapping.
8. **Backend CQRS/service slice:** add single-operation commands, workflow coordinator/commands, manual override commands, history/document queries, FluentValidation validators, result mappers, and independent persistence/error handling.
9. **Backend controller, middleware, host, and gateway:** add protected controller actions for the four operations, three workflows, manual review, history, and documents; extend `ExceptionHandlingMiddleware`; register options/adapters/repositories in `Program.cs`; retain existing routes and add only required gateway aliases.
10. **Backend tests:** add FsCheck property tests, xUnit example/edge tests, and `WebApplicationFactory<Program>` authorization, middleware, workflow, persistence, and gateway integration tests.
11. **Frontend model and services:** add TypeScript models, service interface, real axios service, mock service, toggle selection, and barrel exports under the existing service conventions.
12. **Frontend page and components:** add the identity-verification page, four operation forms, neutral document/extracted-value controls, independent result cards, manual override dialog, history grid, route, and SystemAdmin navigation entry.
13. **Frontend tests:** add Vitest/React Testing Library tests for service selection, forms, normalized results, partial failures, manual review, history, document retrieval, privacy projection, and shared error behavior.
