# Requirements Document

## Introduction

The Customer Onboarding, Customer Reference, and Mind Mappings feature (Customer Onboarding stories FINNOVA-51 through FINNOVA-55, plus the Customer Reference feature) provides a controlled customer-data foundation for the India-only Finnova platform. The feature stores the customer attributes approved by FINNOVA-51..55, supports the customer-reference relationships required by the business, and persists structured Mind Mappings that can later be consumed by a separate AI Agent to identify relationships between one Customer, another Customer, and a Location. "Customer Onboarding" is the application-processing umbrella over the customer-record persistence that earlier material referred to as "Customer Master"; the name "Customer Onboarding" supersedes the earlier "Customer Master" naming throughout this document, and the backend application service previously referred to as `Customer_Service` is now named `CustomerOnboarding_Service`.

The supplied Smart Solutions Customer Master Functional Requirement Specification (FRS) is the source of truth for the concrete customer-record fields, screen modes, tabs, field behavior, and customer workflow contract added in Requirements 18–26. The feature name remains **Customer Onboarding**. The FRS title "Customer Master" is retained only as source terminology and does not rename the feature or service. Oman-specific examples in the FRS, including Oman nationality, NID, and labour card, are source behavior examples only; Finnova SHALL map those examples to approved India-compatible identity and document rules without adding Oman or other non-English lookup values.

The CustomerOnboarding_Service is the authoritative source for customer identity and lifecycle data and, when required or supplied by the approved FINNOVA-51..55 contract, coordinates Aadhaar and PAN validation through the existing Finnova-Document-Verifier API. The CustomerOnboarding_Service stores only the minimum approved verification outcome and metadata; it does not become a document repository. The existing Location Master is the authoritative source for location identity; this feature stores validated references to existing Location records rather than copying location names or hierarchy data. Customer References represent explicit, business-maintained relationships and are kept distinct from Mind Mappings. Mind Mappings are persisted relationship facts or relationship candidates with stable identifiers, normalized endpoints, provenance, lifecycle state, and timestamps so a future AI Agent can consume them without requiring a schema migration. The future AI Agent that retrieves Customer Information or performs relationship inference is explicitly outside this feature.

This feature preserves future requirements for three asynchronous, out-of-request-path capabilities: Mind Mapping generation through a MassTransit message broker and separate consumer, Customer KYC verification delegated to PayU and orchestrated by a scheduled Python Cron Job during Application Processing, and training of an open-source Customer Behavior Model. Customer Reference persistence/management and MassTransit asynchronous generation are deferred follow-up stories for the current implementation slice; their requirements remain in this document for future traceability. PayU/Python orchestration, behavior-model training, and future AI inference retain their existing boundaries and are not expanded by this clarification.

This feature covers both repositories:

- **Backend:** `e:\Finnova\Finnova-API`, using the existing .NET layered service conventions, EF Core persistence, API contracts, authorization, audit handling, and gateway routing.
- **Frontend:** `E:\Finnova\Finnova-UI\Finnova-UI`, using React 18, TypeScript, Material UI, the shared axios instance, feature service interfaces, real/mock implementations, the `VITE_USE_MOCK_API` toggle, and the existing master-data page patterns.

FINNOVA-51..55 are the authoritative business scope. Their full acceptance criteria and field definitions were not available in the workspace when this document was prepared. This document therefore does not invent story-specific customer fields, workflows, statuses, uniqueness keys, role permissions, or identity-document verification rules. Those unavailable details are listed as explicit assumptions and confirmation points. The baseline requirements below define the cross-cutting behavior needed for a safe customer-data foundation, the required boundary to the existing Finnova-Document-Verifier API, and the additional relationship/mapping capabilities requested by this feature; story-specific behavior must be reconciled against the source stories before implementation.

The retrieval/inference Customer Information AI Agent is not implemented, exposed, invoked, or evaluated by this feature. This document preserves future traceability for asynchronous mind-mapping and KYC work, the PayU boundary through a Python Cron Job, and the Customer Behavior Model training pipeline; the current implementation slice does not implement Customer Reference persistence/management or MassTransit asynchronous Mind Mapping generation, and the trained model's consumption by an AI Agent remains deferred to a separate future story.

## Glossary

- **Customer Onboarding**: The application-processing umbrella capability of this feature, covering customer-record persistence (previously called Customer Master), application processing, KYC orchestration, asynchronous mind-mapping, and behavior-model training. The name "Customer Onboarding" supersedes the earlier "Customer Master" naming.
- **Customer**: A persisted customer record containing only the attributes approved by FINNOVA-51..55, a stable identifier, lifecycle state, and audit timestamps.
- **Application**: A unit of Customer Onboarding work that processes a Customer through onboarding, including KYC verification. An Application is distinct from the bare customer record; its fields, states, and lifecycle are defined by FINNOVA-51..55 and remain unconfirmed.
- **Application Number**: The stable identifier generated for an Application once it is created or submitted for processing. Its format, generation rule, and lifecycle are defined by FINNOVA-51..55 and remain unconfirmed.
- **Customer Identifier**: The immutable system-generated identifier of a Customer. The identifier is the primary reference used by Customer References and Mind Mappings.
- **Customer Business Key**: A customer code or other business identifier used to locate a Customer. The exact key, format, uniqueness scope, and mutability are defined by FINNOVA-51..55 and remain unconfirmed until the stories are supplied.
- **Identity Document**: An Aadhaar or PAN document supplied or required under the approved FINNOVA-51..55 Customer contract.
- **Finnova-Document-Verifier API**: The existing integration used by CustomerOnboarding_Service to validate applicable Aadhaar and PAN documents; CustomerOnboarding_Service does not reimplement document validation locally.
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
- **CustomerOnboarding_Service**: The backend API/application service responsible for Customer Onboarding operations (customer-record persistence, application processing, and the request-path publishing of messaging events). Previously named `Customer_Service`.
- **CustomerReference_Service**: The backend API/application service responsible for Customer Reference operations.
- **MindMapping_Service**: The backend API/application service responsible for Mind Mapping persistence and authorized retrieval.
- **Customer_Admin_UI**: The React/TypeScript administrative UI for Customer Onboarding, Customer References, and Mind Mappings.
- **PaginatedResponse**: The platform response contract containing page items, total count, page number, and page size, with any additional fields established by the existing shared contract.
- **Audit Entry**: An immutable record describing a permitted create, update, lifecycle change, or relationship change, including the affected record, changed values, actor/provenance, and UTC timestamp.
- **PII**: Personally identifiable information or other customer data that can identify or materially describe a person or organization.
- **Finnova Platform**: The combined Finnova backend services, API gateway, and authorized frontend clients that enforce the platform's shared security and data-contract conventions.
- **MassTransit**: The proposed .NET messaging abstraction/library used to publish and consume domain-event messages over a message broker. The specific library choice is proposed and requires confirmation.
- **Message Broker**: The external messaging infrastructure (for example RabbitMQ or Azure Service Bus) that transports published messages to consumers. The specific broker is unconfirmed.
- **Publisher**: The component in the request path (within CustomerOnboarding_Service) that publishes a domain-event message to the Message Broker when a triggering business change commits.
- **Consumer**: A separate worker component that subscribes to published messages and performs asynchronous work, such as mind-mapping generation or triggering KYC.
- **Customer Onboarding Worker**: The proposed new .NET worker-service host that runs the message Consumer for asynchronous mind-mapping generation. Its exact host, deployment, and name require confirmation.
- **Transactional Outbox**: A delivery pattern in which a published message is persisted in the same transaction as the triggering business change so that a committed change is reliably published. Whether a transactional outbox or an at-least-once delivery guarantee is used is unconfirmed.
- **PayU**: The external KYC provider to which Customer KYC verification is delegated. PayU's API contract, authentication, endpoints, status vocabulary, retry/timeout/idempotency behavior, and credentials are unconfirmed integration blockers.
- **Video KYC**: A KYC check, delegated to PayU, that verifies customer identity through a video-based process. Its contract and status vocabulary are unconfirmed.
- **CIBIL**: A credit-information check performed as part of KYC, delegated to PayU. Its contract and status vocabulary are unconfirmed.
- **Sanctions/PEP Screening**: A KYC check for sanctions-list and Politically Exposed Person status, delegated to PayU. Its contract and status vocabulary are unconfirmed.
- **Due Diligence**: An additional KYC check ("Other Due Diligence") performed as part of KYC, delegated to PayU. Its contract and status vocabulary are unconfirmed.
- **KYC Outcome**: The minimum approved, persisted result of KYC verification, including per-check status, provider reference, masked or non-sensitive metadata, and UTC timestamps, together with a derivable aggregate KYC status. The exact fields and status vocabulary are unconfirmed.
- **KYC Cron Service**: The proposed separate Python service/component that runs a scheduled Cron Job to orchestrate the PayU-delegated KYC checks during Application Processing. Its schedule, trigger mechanism, and platform authentication are unconfirmed.
- **Cron Job**: A scheduled, offline process that performs work outside the synchronous request path. The KYC Cron Service runs as a Cron Job; the schedule and trigger mechanism are unconfirmed.
- **Customer Behavior Model**: The open-source model trained by this feature's training pipeline on approved customer-behavior data for later use by a future AI Agent. The specific model/framework is unconfirmed.
- **Customer Payment Pattern**: A derived characteristic describing how a Customer's payments behave over time; a change in Customer Payment Pattern is one of the training-data inputs for the Customer Behavior Model.
- **Delinquency**: A tracked state describing missed or overdue customer payments; Delinquency Tracking is one of the training-data inputs for the Customer Behavior Model.
- **Training Pipeline**: The offline/batch process that sources approved data, trains the Customer Behavior Model, and versions the produced artifact. It does not run inside the synchronous request path.
- **Model Artifact**: The versioned, produced output of the Training Pipeline that a future AI Agent may consume. Its registry, storage, and serving boundary are unconfirmed.
- **Customer Code**: The 12-character, system-generated, globally unique business code displayed for a Customer. Customer Code is generated through the approved Document Number Control definition, is immutable, and is not manually entered or deleted from the frontend.
- **Customer Capacity**: The controlled set of Customer, Guarantor, and Co-applicant capacity flags assigned to a Customer record.
- **Customer Mode**: One of Customer Create, Customer Modify, or Customer Query. Customer Query is read-only.
- **Customer Master FRS**: The supplied Smart Solutions Functional Requirement Specification titled "Customer Master". It is the source for the concrete Customer Onboarding customer-record fields and workflow behavior in Requirements 18–26.
- **Customer Track**: The modify-mode customer history view containing approved classifications such as Black Listed, Legal, Defaulter, and Repossessed.
- **Constitution Document Definition**: A configured document rule loaded from Constitution Code and Document Master configuration, including category, code, mandatory flag, image requirement, and collection behavior.
- **Document Metadata Reference**: The approved DMS path or reference and related metadata stored by the Customer record without storing raw document binaries in the Customer record.
- **Conditional Applicability Matrix**: The approved, explicit mapping of Customer Type, occupation, SS Employee Indicator, business type/sub-type, factoring applicability, Constitution Code, and other operational conditions to required, optional, read-only, and unavailable fields.
- **Default Bank Account**: The single Bank Details row used for payment and ECS mapping. A Customer may have multiple bank accounts but exactly one active default account.
- **Factoring Sub-Limit**: The approved factoring limit assigned per factoring entity and Customer and used by invoice-loading rules.
- **Approved India Identity Rule**: A confirmed Indian identity or document type, applicability rule, format, uniqueness rule, and verification boundary that replaces jurisdiction-specific FRS examples.
- **Account/Contract Boundary**: The approved service or data boundary that supplies account, contract, limit, payment, ECS, and related derived values to Customer Onboarding.
- **Chart of Accounts**: The configured SystemAdmin GL account catalogue used to resolve approved Customer Posting Group values.
- **DMS**: The configured document-management service and path used to upload scanned documents and return approved metadata references.
- **SS Employee Indicator**: The approved conditional employment indicator that controls fields in the Non-Individual applicability matrix when the source workflow requires it.
- **EARS**: Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.
- **Lookup Master**: The authoritative controlled-value boundary backed by `LookupValue` and `ILookupRepository.GetActiveByModuleAndTypeAsync`. Lookup Master supplies active Customer lookup values and their codes.
- **Lookup Type Constant**: A named, approved module/type contract key used to retrieve one controlled value family from Lookup Master. Final Customer lookup module/type constants remain confirmation items.
- **Nationality Master**: The authoritative nationality boundary backed by `Nationality` and `INationalityRepository`. Nationality Master supplies selectable active nationality rows and `NationalityCode`.
- **GL Master**: The authoritative general-ledger boundary backed by `GLAccount` and `IGLAccountRepository`. GL Master supplies Customer Posting Group references that are valid for the applicable Company scope.
- **Document Master**: The authoritative document-definition boundary backed by `Document` and `DocumentApplicabilityRule`. Document Master supplies Constitution Document definitions and applicability conditions.
- **India Address Control**: The shared India address boundary that owns address validation and address lookup/reference semantics. CustomerOnboarding_Service consumes its approved contract and does not duplicate its ownership.
- **Master Reference**: A persisted code, identifier, or approved address reference owned by a master-data boundary. A Master Reference is stored by Customer without copying the master's authoritative display values.
- **Read Projection**: An authorized response shape that resolves current display values from master boundaries for UI or consumer use without making those values Customer-owned write data.
- **Customer Reference Slice**: The preserved Customer Reference persistence/management requirements in this document. Customer Reference persistence/management is deferred to a follow-up story for the current implementation slice.
- **MassTransit Generation Slice**: The preserved future asynchronous Mind Mapping generation requirements using MassTransit and a Message Broker. MassTransit asynchronous generation is deferred to a follow-up story for the current implementation slice.

## Dependencies and Assumptions

### Authoritative Master-Data Ownership Boundaries

- **Lookup Master boundary:** CustomerOnboarding_Service SHALL use `LookupValue` through `ILookupRepository.GetActiveByModuleAndTypeAsync` for Group, Industry, Constitution, Title, Marital Status, Customer Business Type, Customer Business Sub-Type, Industry Sector, Sub-Sector, Track Type, and other controlled Customer values. Customer create/modify validation SHALL use named Lookup Type Constants or equivalent contract seams. The final module/type constants and controlled values remain confirmation items.
- **Document Master boundary:** CustomerOnboarding_Service SHALL use `Document` and `DocumentApplicabilityRule` for Constitution Document definitions and applicability. Customer records may store the selected document metadata/reference, but Customer records SHALL not store raw document binaries or become the authoritative document-definition repository.
- **Nationality Master boundary:** CustomerOnboarding_Service SHALL use `Nationality` through `INationalityRepository` for `NationalityCode`. Only active Nationality Master rows are selectable. Customer persistence SHALL store the nationality code/reference only; nationality names SHALL be resolved in authorized read projections.
- **GL Master boundary:** CustomerOnboarding_Service SHALL use `GLAccount` through `IGLAccountRepository` for `CustomerPostingGroupCode`. The reference SHALL resolve to an active, approved, postable GL account under the applicable Company scope unless the existing GL contract defines a different rule. Customer persistence SHALL store the GL identifier/code only and SHALL not duplicate GL names.
- **India Address Control boundary:** Communication Address and Permanent Address SHALL be supplied and validated through the shared India Address Control contract. CustomerOnboarding_Service SHALL reuse established Company/Organization address field conventions where applicable, store only approved address values/references, and SHALL not duplicate address lookup ownership or validation logic. The exact interface/path remains a confirmation item.
- **Reference-only persistence:** Customer stores codes, IDs, and approved address references and resolves display values through read projections. Customer SHALL not copy `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, `GLName`, bank master values, or address lookup values as authoritative Customer fields. The same rule applies to any other display value owned by a master boundary.

### Dependencies

- **Smart Solutions Customer Master FRS:** The supplied FRS titled "Customer Master" is the source for concrete Customer Onboarding fields, modes, tabs, field rules, and workflow behavior in Requirements 18–26. The feature name remains Customer Onboarding.
- **Document Number Control:** Customer Code generation depends on the approved Document Number Control definition and its configured 12-character format and uniqueness scope.
- **Lookup Master:** Customer create/modify behavior depends on active `LookupValue` rows returned through `ILookupRepository.GetActiveByModuleAndTypeAsync` for all controlled Customer values. The final module/type constants, code lengths, and approved values require confirmation.
- **Nationality Master:** NationalityCode selection and validation depend on active `Nationality` rows through `INationalityRepository`; Customer persistence does not duplicate nationality names.
- **Document Master:** Constitution document rows and applicability depend on `Document` plus `DocumentApplicabilityRule`, selected by the Lookup-backed ConstitutionCode and applicable Customer role/type.
- **GL Master and Company scope:** CustomerPostingGroupCode depends on `GLAccount` rows returned through `IGLAccountRepository`, with active, approved, postable, Company-scoped validity unless the existing GL contract defines another rule. The CompanyId source requires confirmation.
- **India Address Control:** Communication and Permanent Address depend on the shared India Address Control contract. CustomerOnboarding_Service reuses established Company/Organization address field conventions where applicable and does not own address lookup or duplicate validation.
- **Bank and account/contract boundaries:** Bank Code resolution, duplicate bank detection, Contract Number, Sub-Contract Number, payment/ECS mapping, and derived account values depend on approved bank and account/contract contracts. Customer does not duplicate bank master values as authoritative fields.
- **DMS:** Scanned Constitution Documents depend on the configured DMS upload path and metadata/reference contract. Customer records do not store raw document binaries.
- **Credit, factoring, and downstream rules:** Customer Credit Details, Factoring Sub-Limit, invoice loading, blacklisting, delinquency, legal, and repossession behavior depend on approved account, limit, factoring, and downstream credit contracts. External integrations remain deferred, not removed.
- **FINNOVA-51..55 business source:** Customer Onboarding stories FINNOVA-51 through FINNOVA-55 define the authoritative customer fields, workflows, validations, lifecycle, uniqueness, and acceptance behavior. The story text was unavailable during requirements preparation and must be supplied before implementation planning is finalized.
- **Finnova-Document-Verifier integration:** Customer create/update behavior depends on the existing Finnova-Document-Verifier API when Aadhaar or PAN is required or supplied by the approved FINNOVA-51..55 contract. The verifier's endpoint, schema, authentication, status vocabulary, matching rules, timeout, cancellation, retry, and idempotency behavior require confirmation.
- **Location Master:** Customer-to-Location References and Mind Mappings depend on the existing Location Master and its stable `Location.Id` identifier. The feature does not create, modify, duplicate, or infer Location records.
- **Authentication and authorization:** The feature depends on the JWT Bearer authentication scheme and the SystemAdmin authorization policy used by the platform. Exact role/policy names and whether authorized non-admin consumers may read projections require confirmation against the deployed host configuration.
- **Audit infrastructure:** Existing master specifications use feature-scoped immutable audit trails. This feature depends on the platform's established audit conventions or introduces equivalent scoped audit persistence for Customer, Customer Reference, and Mind Mapping changes.
- **Backend host and gateway:** The feature is expected to live in the existing SystemAdmin host and be reachable through the Finnova.ApiGateway. The final route prefix and any UI-facing alias must follow the deployed gateway configuration.
- **MassTransit and message broker:** Asynchronous Mind Mapping generation and KYC triggering depend on a MassTransit-based Publisher/Consumer path over a Message Broker. The broker choice (for example RabbitMQ or Azure Service Bus), MassTransit as the library, the Customer Onboarding Worker consumer host, the message schema/versioning, the retry/dead-letter policy, and the delivery guarantee (transactional outbox versus at-least-once) require confirmation.
- **PayU external KYC provider:** Customer KYC verification depends on the external PayU provider. PayU's API contract, authentication, endpoints, status vocabulary, retry/timeout/idempotency behavior, and credentials are unconfirmed integration blockers and must be confirmed before design finalization.
- **Python runtime and cron host:** The KYC Cron Service depends on a Python runtime and a cron/scheduling host that runs outside the .NET request path. The cron schedule, trigger mechanism (scheduled versus message-driven), and how the Python service authenticates to the platform require confirmation.
- **Model training data sources:** The Customer Behavior Model Training Pipeline depends on approved, authorized data sources for Customer Payments, Customer Preference tracking, Customer Payment Pattern changes, and Delinquency Tracking. The specific data sources/feature store, the open-source model/framework, the model registry, the training cadence, and evaluation metrics require confirmation and must come from approved, minimized sources.
- **Frontend repository:** UI artifacts belong in `E:\Finnova\Finnova-UI\Finnova-UI`; no UI source is part of this requirements-only deliverable.

### Assumptions for Confirmation

- **A1 — Story details unavailable:** FINNOVA-51..55 are authoritative, but their field lists and acceptance criteria were not available in the workspace. No implementation SHALL finalize story-specific fields or behavior until the stories are supplied and traced to these requirements.
- **A2 — Customer scope:** The baseline Customer Onboarding capability supports the create, modify, query/read, and lifecycle operations explicitly present in FINNOVA-51..55. The exact operation-to-story mapping is unconfirmed; hard deletion, deactivation, approval, merge, duplicate detection, and bulk import SHALL not be assumed unless a story requires them.
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
- **A17 — Integration contract:** Future AI consumption is expected to use versioned API contracts or read-only projections containing stable IDs and provenance. This assumption is narrowed, not removed: the MassTransit messaging path for asynchronous mind-mapping generation is preserved as a future/deferred boundary for traceability, while the current task list defers Customer Reference persistence/management and MassTransit asynchronous Mind Mapping generation to follow-up stories. The PayU/Python KYC boundary and approved open-source Customer Behavior Model Training Pipeline retain their separate deferred/confirmation boundaries in Requirements 14 and 15. Direct AI inference, vector stores, embeddings, retrieval-augmented generation, agent orchestration, and model-serving infrastructure remain out of scope unless separately approved.
- **A18 — Localization:** Finnova remains India-only and English-only. Customer labels, reference types, statuses, validation messages, sample data, and tests use English values only. No bilingual fields, translation scaffolding, Arabic/non-English values, or RTL behavior are included.
- **A19 — Verifier contract unknown:** The Finnova-Document-Verifier API endpoint, request/response schema, authentication mechanism, status vocabulary, document matching rules, verifier reference format, and whether Aadhaar, PAN, or both documents are mandatory are unknown. These values SHALL be confirmed from the existing integration and FINNOVA-51..55 before design finalization; this requirements document intentionally does not invent them.
- **A20 — Verification dependency policy unknown:** The approved timeout, cancellation behavior, retry/backoff policy, idempotency key or deduplication behavior, transient-versus-permanent failure classification, and safe error-code mapping for Finnova-Document-Verifier are unknown and require confirmation. A required validation failure, rejection, invalid result, mismatched result, timeout, cancellation, or unavailable dependency will not create or update a Customer as verified until the approved policy is defined.
- **A21 — Verification retention and presentation:** The approved Verification Outcome fields, retention period, masking rules, authorized readers, audit treatment, and UI status labels require confirmation. The baseline is to persist and display only minimum non-sensitive outcome data and a safe retry path, without raw Aadhaar/PAN content or unnecessary full document numbers.
- **A22 — Asynchronous mind-mapping messaging:** Mind Mapping generation is proposed to run asynchronously via a MassTransit Publisher in the request path and a separate Consumer (Customer Onboarding Worker, a new .NET worker service). The broker choice (for example RabbitMQ or Azure Service Bus) is unconfirmed; MassTransit is the proposed library. The exact message schema and versioning, the retry/dead-letter policy, the outbox mechanism, and the exact delivery guarantee (transactional outbox versus at-least-once) require confirmation. These unknowns SHALL NOT be invented before confirmation.
- **A23 — PayU KYC during Application Processing:** Customer KYC verification is proposed to be delegated to PayU and orchestrated by a scheduled Python Cron Job (KYC Cron Service) during Application Processing, triggered once an Application Number is generated via a MassTransit Publisher/Consumer. PayU's contract, authentication, endpoints, status vocabulary, retry/timeout/idempotency behavior, and credentials are unknown integration blockers (same discipline as the Finnova-Document-Verifier) and SHALL NOT be invented. The Application and Application Number concept and lifecycle are assumed to exist or be defined by FINNOVA-51..55 and require confirmation. The Python cron schedule, the trigger mechanism (scheduled versus message-driven), whether the cron polls for pending applications or is purely event-driven, and how the Python service authenticates to the platform require confirmation.
- **A24 — Behavior-model training pipeline:** The Customer Behavior Model is proposed to be an open-source model (to be selected and confirmed) trained by an offline/batch Python pipeline on Customer Payments, Customer Preference tracking, Customer Payment Pattern changes, and Delinquency Tracking. The specific open-source model/framework, the feature store/data sources (which must come from approved, authorized, minimized datasets), the training cadence, the evaluation metrics, the model registry, and the serving boundary require confirmation. This requirement covers the Training Pipeline and training-data boundary only; the trained model's use by an AI Agent remains out of scope.
- **A25 — Training-versus-serving boundary:** The Customer Behavior Model Training Pipeline is in scope; the retrieval/inference AI Agent that consumes the Model Artifact is out of scope and deferred to the separate future story. The boundary between the in-scope training pipeline and out-of-scope agent inference/serving requires confirmation.

- **A26 — FRS source title:** The supplied Smart Solutions FRS is titled "Customer Master" and is authoritative for concrete customer-record behavior. The feature and service names remain Customer Onboarding and CustomerOnboarding_Service.
- **A27 — India lookup vocabularies:** Final Customer Type, Title, Nationality, Gender, Marital Status, Account Type, Constitution, posting-group, industry, business-type, factoring-type, track-classification, and related LOV vocabularies require product approval. Only English, India-compatible values may be seeded or displayed; the FRS examples do not authorize Oman or other non-English values.
- **A28 — Customer Code configuration:** Customer Code is assumed to be generated by the approved Document Number Control definition as a 12-character globally unique code. The exact definition key, sequence scope, concurrency behavior, and whether global means platform-wide or tenant-wide require confirmation.
- **A29 — Mutability conflict:** Customer Code is immutable. The FRS excludes Customer Type where specified, Gender, Date of Birth, and Age from Modify, while other FRS sections may imply broader approved-field modification. The exact mutable/read-only matrix, including system-derived fields, requires confirmation before implementation.
- **A30 — Omitted field lengths:** Where screenshots do not state a length, the implementation SHALL use a confirmed field-length decision rather than infer a database or UI default. Explicit source limits are requirements below; unconfirmed limits remain checklist items.
- **A31 — Conditional applicability:** The Customer Type, occupation, SS Employee Indicator, business type/sub-type, factoring applicability, Constitution Code, and operational-rule applicability matrix must be explicit, versioned, and testable before implementation. The listed fields are not all applicable to every Customer.
- **A32 — India identity mapping:** Oman nationality, NID, labour card, civil number, and any "all countries" language in the FRS are jurisdiction-specific source behavior. They SHALL be mapped to Approved India Identity Rules; this document does not add Oman values or assume that an Oman document is valid for Finnova.
- **A33 — Integration boundaries:** DMS, bank, account/contract, Chart of Accounts/GL, credit, factoring, downstream blacklist, and invoice-loading integrations remain boundary contracts with explicit future owners where not supplied by the current implementation slice. Document Master remains the authoritative source for Constitution Document definitions and applicability; Customer does not duplicate Document Master ownership. Boundary clarification does not remove the customer-record fields or master-reference requirements.
- **A34 — Derived calculation ownership:** Ownership and formulas for Age, Total Net Worth, Remaining Amount, Factoring Sub-Limit, credit utilization, and aggregate downstream credit outcomes require confirmation. CustomerOnboarding_Service SHALL not invent a calculation owner or formula.
- **A35 — Source values and errors:** Exact source-specific values, labels, error messages, duplicate-alert text, and continue/abandon choices require confirmation. User-facing values and errors remain English only.
- **A36 — Master ownership and repository contracts:** Lookup Master is authoritative for controlled Customer values through `LookupValue` and `ILookupRepository.GetActiveByModuleAndTypeAsync`; Nationality Master is authoritative for active `Nationality` rows through `INationalityRepository`; GL Master is authoritative for `GLAccount` through `IGLAccountRepository`; and Document Master is authoritative for `Document` plus `DocumentApplicabilityRule`. Customer stores codes/IDs or approved references and resolves names through projections. Customer does not persist copied GroupName, IndustryName, ConstitutionName, NationalityName, GLName, bank master values, or address lookup values as authoritative fields.
- **A37 — Lookup type constants:** The final Lookup Master module/type constants for Group, Industry, Constitution, Title, Marital Status, Customer Business Type, Customer Business Sub-Type, Industry Sector, Sub-Sector, Track Type, and related controlled values are unconfirmed. The implementation must use named constants or equivalent contract seams and must not invent Oman or non-English values.
- **A38 — Nationality selection:** Only active Nationality Master records are selectable. Customer responses may resolve and include a NationalityName projection for an authorized consumer, while Customer persistence stores NationalityCode or the approved nationality identifier only.
- **A39 — GL scope and validity:** CustomerPostingGroupCode must resolve through GL Master to an active, approved, postable GL account under the applicable Company scope unless the existing GL contract states otherwise. The CompanyId source/resolution contract and the exact active/approved/postable rule require confirmation.
- **A40 — Constitution document applicability:** ConstitutionCode and Constitution Document rows are resolved through Lookup Master plus Document Master and DocumentApplicabilityRule. The exact customer role/type applicability dimensions and final Document Master contract require confirmation.
- **A41 — Address Control contract:** Communication Address and Permanent Address use the shared India Address Control contract. The exact interface/path, approved address reference shape, and fields that remain Customer-owned values require confirmation; CustomerOnboarding_Service does not reimplement address lookup or validation ownership.
- **A42 — Current implementation slice:** Customer Reference persistence/management and MassTransit asynchronous Mind Mapping generation are deferred follow-up stories for this implementation slice. Requirements 5–9 remain preserved for future traceability, but the current task list SHALL not implement Customer Reference persistence/management or MassTransit asynchronous generation. PayU, Python scheduling, behavior-model training, and future AI inference preserve their existing deferred boundaries and SHALL not be pulled into this slice.
- **A43 — Projection-only display fields:** The final list of authorized display-name projections requires confirmation. At minimum, GroupName, IndustryName, ConstitutionName, NationalityName, GLName, and other master-owned display values are projection-only and are not authoritative Customer write fields.

## Requirements

### Current Implementation Slice

- This requirements document preserves Customer Reference persistence/management and MassTransit asynchronous Mind Mapping generation in Requirements 5–9 for future traceability.
- Customer Reference persistence/management and MassTransit asynchronous generation are deferred follow-up stories for the current implementation slice. The current task list SHALL not implement either capability.
- The current slice SHALL preserve the existing PayU/Python KYC, Customer Behavior Model training, future AI inference, DMS, bank, account/contract, GL, credit, and factoring boundaries as documented. Their existing requirements remain traceability constraints and are not expanded by this clarification.

### Requirement 1: Preserve FINNOVA-51..55 as the Customer Onboarding Authority

**User Story:** As a product owner, I want Customer Onboarding behavior to follow FINNOVA-51..55, so that implementation does not introduce unsupported customer fields or workflows.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL treat FINNOVA-51..55 as the authority for Customer fields, operations, lifecycle states, and validations.
2. WHEN the complete FINNOVA-51..55 acceptance criteria are supplied, THE CustomerOnboarding_Service SHALL map every story acceptance criterion to at least one numbered requirement before implementation tasks are created.
3. IF a Customer behavior is absent from FINNOVA-51..55 and absent from an approved assumption, THEN THE CustomerOnboarding_Service SHALL exclude that behavior from the implementation scope.
4. IF a FINNOVA-51..55 story detail conflicts with a baseline requirement, THEN THE CustomerOnboarding_Service SHALL apply the story detail only after recording the conflict and revising the affected requirement.
5. THE CustomerOnboarding_Service SHALL use one stable Customer Identifier for every Customer Reference and Mind Mapping Customer relationship.

**Unresolved story details:** The complete field list, operation list, lifecycle model, business-key rules, validation limits, duplicate behavior, authorization matrix, and audit expectations for FINNOVA-51..55 must be provided for confirmation.

### Requirement 2: Create and Maintain Customer Records

**User Story:** As an authorized administrator, I want to maintain approved Customer records, so that business modules use consistent customer data.

#### Acceptance Criteria

1. WHEN an authorized caller submits a Customer create request containing all fields required by FINNOVA-51..55, THE CustomerOnboarding_Service SHALL validate the request before persistence.
2. WHEN a valid Customer create request is accepted, THE CustomerOnboarding_Service SHALL persist exactly one Customer record.
3. WHEN a Customer create succeeds, THE CustomerOnboarding_Service SHALL return the persisted Customer with its stable Customer Identifier, resolved lifecycle values, and audit timestamps.
4. WHEN an authorized caller submits an update for an existing Customer with fields approved by FINNOVA-51..55, THE CustomerOnboarding_Service SHALL validate the update before persistence.
5. WHEN a valid Customer update is accepted, THE CustomerOnboarding_Service SHALL persist all approved changes atomically.
6. WHEN a Customer update succeeds, THE CustomerOnboarding_Service SHALL return the resulting Customer with its stable Customer Identifier and audit timestamps.
7. IF a Customer create or update request contains an omitted, empty, malformed, over-length, unsupported, or mutually inconsistent value defined as invalid by FINNOVA-51..55, THEN THE CustomerOnboarding_Service SHALL return field-level validation errors and SHALL persist no partial change.
8. IF a Customer update targets a Customer Identifier that does not exist, THEN THE CustomerOnboarding_Service SHALL return a not-found error and SHALL preserve all existing Customer records.
9. WHERE FINNOVA-51..55 defines a default Customer field value, THE CustomerOnboarding_Service SHALL apply the default only when the request omits that field.
10. WHERE FINNOVA-51..55 defines an immutable Customer field, THE CustomerOnboarding_Service SHALL reject any request that changes that field.
11. WHEN a Customer create or update succeeds, THE CustomerOnboarding_Service SHALL store and return timestamps in the platform's established UTC representation.
12. WHERE an Aadhaar or PAN document is required or supplied by the approved FINNOVA-51..55 contract, THE CustomerOnboarding_Service SHALL apply the validation, persistence, and failure rules defined in Requirement 17 before persisting a new Customer or applying a Customer update.
13. IF an applicable Aadhaar or PAN validation fails, is rejected, returns an invalid or mismatched result, or cannot be completed under the approved verifier dependency policy, THEN THE CustomerOnboarding_Service SHALL apply the failure behavior defined in Requirement 17 and SHALL not mark the Customer as verified.
14. WHEN the approved Finnova-Document-Verifier validation succeeds, THE CustomerOnboarding_Service SHALL persist only the approved Verification Outcome fields and SHALL not persist raw document content or authentication tokens.
15. THE CustomerOnboarding_Service SHALL accept Customer master references as codes, identifiers, or approved address references owned by the applicable master boundary and SHALL resolve authorized display values through read projections.
16. IF a Customer create or update request supplies GroupName, IndustryName, ConstitutionName, NationalityName, GLName, bank master display values, address lookup display values, or another master-owned display value as an authoritative write field, THEN THE CustomerOnboarding_Service SHALL return a field-level validation error, SHALL not use the supplied display value as authoritative, and SHALL use only the resolved master reference.
17. WHEN an authorized Customer response is returned, THE CustomerOnboarding_Service MAY include resolved display names and other master-owned display values in a Read Projection, but THE CustomerOnboarding_Service SHALL not treat those values as Customer-owned persistence fields.
18. IF a Customer create or update request contains an unknown or inactive Lookup, Nationality, GL, Document, or India Address Control reference, THEN THE CustomerOnboarding_Service SHALL return field-level errors, SHALL persist no Customer or child-row change, and SHALL create no business-change audit entry.

**Assumption:** This requirement deliberately leaves Customer field names, defaults, lengths, identity keys, lifecycle operations, and the exact Aadhaar/PAN verification contract to FINNOVA-51..55 and the existing Finnova-Document-Verifier integration rather than inventing them.

### Requirement 3: Customer Identity, Uniqueness, and Lifecycle Integrity

**User Story:** As a data owner, I want Customer identity rules enforced consistently, so that relationships do not point to ambiguous or duplicated Customers.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL enforce the Customer business-key uniqueness rule defined by FINNOVA-51..55.
2. THE CustomerOnboarding_Service SHALL apply the business-key normalization and uniqueness scope defined by FINNOVA-51..55.
3. IF a Customer create or update request conflicts with an existing Customer under the approved business-key rule, THEN THE CustomerOnboarding_Service SHALL return the platform's conflict or validation error.
4. IF a Customer create or update request conflicts with an existing Customer, THEN THE CustomerOnboarding_Service SHALL preserve the existing Customer records.
5. IF concurrent Customer create requests resolve to the same approved unique business key, THEN THE CustomerOnboarding_Service SHALL persist no more than one Customer for that key.
6. IF a concurrent Customer create request loses the approved unique-key conflict, THEN THE CustomerOnboarding_Service SHALL return the same deterministic conflict outcome as any other duplicate request.
7. WHERE FINNOVA-51..55 defines a deactivation or archival lifecycle, THE CustomerOnboarding_Service SHALL retain the Customer Identifier and relationship history when applying that lifecycle state.
8. IF a requested Customer lifecycle transition is not allowed by FINNOVA-51..55, THEN THE CustomerOnboarding_Service SHALL reject the transition and SHALL preserve the existing Customer state.
9. IF FINNOVA-51..55 does not explicitly require hard deletion, THEN THE CustomerOnboarding_Service SHALL not expose a hard-delete operation for Customer records.

**Assumption:** A stable identity and history-preserving lifecycle are required for reliable Customer References and future Mind Mapping consumption, but the exact business-key and lifecycle rules remain unconfirmed.

### Requirement 4: Query and Search Customer Records

**User Story:** As an authorized consumer, I want to find Customers using approved search fields, so that I can select the correct Customer when maintaining relationships.

#### Acceptance Criteria

1. WHEN an authorized caller supplies an approved Customer search term, THE CustomerOnboarding_Service SHALL search only fields approved for Customer search by FINNOVA-51..55.
2. WHEN an authorized caller supplies an approved Customer search term, THE CustomerOnboarding_Service SHALL apply the matching semantics defined by FINNOVA-51..55.
3. WHEN an authorized caller requests Customers without a search term, THE CustomerOnboarding_Service SHALL apply the approved lifecycle filters.
4. WHEN an authorized caller requests Customers, THE CustomerOnboarding_Service SHALL return a `PaginatedResponse` containing the page items, total count, page number, and page size.
5. IF a Customer query contains an invalid page number, page size, filter, or search value under the approved query contract, THEN THE CustomerOnboarding_Service SHALL return a validation error.
6. IF a Customer query contains an invalid query parameter, THEN THE CustomerOnboarding_Service SHALL not return a partial result for that request.
7. WHEN a Customer query matches no records, THE CustomerOnboarding_Service SHALL return a successful empty page with a total count of zero.
8. THE CustomerOnboarding_Service SHALL order Customer query results deterministically using fields approved by FINNOVA-51..55.
9. THE CustomerOnboarding_Service SHALL use the stable Customer Identifier as a tie-breaker when approved ordering fields contain equal values.
10. IF an authorized caller requests a Customer by an unknown Customer Identifier, THEN THE CustomerOnboarding_Service SHALL return the platform's not-found response.
11. IF a caller supplies a malformed Customer Identifier, THEN THE CustomerOnboarding_Service SHALL return the platform's validation response.
12. IF a caller requests a Customer by an unknown or malformed Customer Identifier, THEN THE CustomerOnboarding_Service SHALL not disclose unrelated Customer data.

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

### Requirement 9: Asynchronous Mind Mapping Generation via Messaging

**User Story:** As a data platform owner, I want Mind Mapping generation to run asynchronously through messaging rather than inside the request that changes customer data, so that customer operations stay responsive and mind-mapping work is processed reliably out of the request path.

#### Acceptance Criteria

1. WHEN a customer change that affects Mind Mappings commits, THE CustomerOnboarding_Service SHALL publish a domain-event message to the Message Broker and SHALL NOT perform Mind Mapping generation synchronously within the request that changes customer data.
2. THE CustomerOnboarding_Service SHALL host a Publisher in the request path that publishes Mind Mapping domain-event messages over MassTransit.
3. THE Customer Onboarding Worker SHALL host a Consumer, separate from the request path, that performs Mind Mapping generation asynchronously from messages received over MassTransit.
4. WHEN Customer information is created or updated, THE CustomerOnboarding_Service SHALL publish a Mind Mapping domain-event message carrying the change type.
5. WHEN a Customer Reference is changed or removed, THE CustomerOnboarding_Service SHALL publish a Mind Mapping domain-event message carrying the change type.
6. WHEN a Customer Address is changed, THE CustomerOnboarding_Service SHALL publish a Mind Mapping domain-event message carrying the change type.
7. WHEN a Customer is purged or removed from the system, THE CustomerOnboarding_Service SHALL publish a Mind Mapping domain-event message carrying the change type.
8. THE CustomerOnboarding_Service SHALL include in each published message the stable identifiers required for the change, including the CustomerId and the changed reference, address, or mapping identifiers, a change-type discriminator, provenance, a correlation identifier, and a UTC timestamp.
9. THE CustomerOnboarding_Service SHALL exclude raw PII, raw documents, and secrets from each published message, carrying only the minimum identifiers needed for the change.
10. WHEN the Customer Onboarding Worker receives a redelivered message that was already processed, THE Customer Onboarding Worker SHALL process the message idempotently and SHALL NOT create duplicate Mind Mappings.
11. WHEN the Customer Onboarding Worker completes processing a message, THE Customer Onboarding Worker SHALL record the processing outcome.
12. IF the Customer Onboarding Worker fails to process a message, THEN THE Customer Onboarding Worker SHALL apply the approved retry and dead-letter policy and SHALL preserve the change event without loss.
13. THE CustomerOnboarding_Service SHALL make publication of the Mind Mapping message consistent with the commit of the triggering business change, so that a committed change is not left unpublished and an unpublished event does not claim a committed change.
14. IF the triggering business change is rolled back, THEN THE CustomerOnboarding_Service SHALL NOT publish the corresponding Mind Mapping message.

**Assumptions:** The broker choice (for example RabbitMQ or Azure Service Bus) is unconfirmed; MassTransit is the proposed messaging library; the Consumer host is a new .NET worker service (the Customer Onboarding Worker); and the exact message schema and versioning, the retry/dead-letter policy, and the outbox mechanism require confirmation. The exact delivery guarantee — transactional outbox versus at-least-once delivery — is flagged for confirmation. These unknowns SHALL NOT be invented before confirmation. This preserves the future MassTransit messaging boundary for traceability while the current task list defers Customer Reference persistence/management and MassTransit asynchronous Mind Mapping generation to follow-up stories; direct AI inference remains out of scope.

### Requirement 10: Audit Customer, Reference, and Mapping Changes

**User Story:** As a security and data-governance stakeholder, I want changes to Customer relationships and mappings to be traceable, so that the platform can investigate who changed relationship data and why.

#### Acceptance Criteria

1. WHEN the CustomerOnboarding_Service persists a Customer create, update, deactivation, archival, or other approved lifecycle change, THE CustomerOnboarding_Service SHALL persist exactly one immutable Audit Entry for that change.
2. WHEN a Customer change is audited, THE CustomerOnboarding_Service SHALL include the affected Customer Identifier, action, changed fields or approved field snapshot, acting identity, provenance, and UTC timestamp in the Audit Entry.
3. WHEN the CustomerReference_Service persists a Customer Reference create, update, deactivation, or archival, THE CustomerReference_Service SHALL persist exactly one immutable Audit Entry for that change.
4. WHEN a Customer Reference change is audited, THE CustomerReference_Service SHALL include the reference identifier, normalized endpoints, action, changed values, acting identity, provenance, and UTC timestamp in the Audit Entry.
5. WHEN the MindMapping_Service persists a Mind Mapping create, update, status transition, supersession, or archival, THE MindMapping_Service SHALL persist exactly one immutable Audit Entry for that change.
6. WHEN a Mind Mapping change is audited, THE MindMapping_Service SHALL include the mapping identifier, endpoint identifiers, action, changed values, provenance, contract version, acting identity where applicable, and UTC timestamp in the Audit Entry.
7. IF a Customer, Customer Reference, or Mind Mapping request is rejected by authentication, authorization, validation, uniqueness, referential-integrity, or lifecycle rules, THEN the relevant service SHALL persist no business-data change.
8. IF a Customer, Customer Reference, or Mind Mapping request is rejected, THEN the relevant service SHALL persist no ordinary change Audit Entry for that rejected operation.
9. IF a business-data change and its required Audit Entry cannot be persisted atomically, THEN the relevant service SHALL roll back the business-data change.
10. IF a business-data change and its required Audit Entry cannot be persisted atomically, THEN the relevant service SHALL return an error indicating that the change was not recorded.
11. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL prevent modification of previously recorded Audit Entries through feature APIs.
12. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL prevent deletion of previously recorded Audit Entries through feature APIs.
13. WHEN an authorized auditor requests history for a Customer, Customer Reference, or Mind Mapping, THE relevant service SHALL return entries in deterministic reverse chronological order.
14. WHEN audit entries have equal timestamps, THE relevant service SHALL use the stable audit identifier as the ordering tie-breaker.

**Assumption:** Create and lifecycle changes are audited in addition to field updates. Read/access logging, failed-request security events, privacy events, and retention schedules require separate confirmation.

### Requirement 11: Authentication, Authorization, and Privacy Enforcement

**User Story:** As a security stakeholder, I want Customer data and relationship data protected by authentication, authorization, and minimization, so that sensitive information is disclosed only for an approved purpose.

#### Acceptance Criteria

1. IF a Customer, Customer Reference, Mind Mapping, or audit request is received without a valid JWT Bearer token, THEN the applicable service SHALL return an unauthorized response.
2. IF a request is received without a valid JWT Bearer token, THEN the applicable service SHALL disclose no Customer, relationship, mapping, or audit data.
3. IF an authenticated caller lacks the role or policy required for the requested operation, THEN the applicable service SHALL return a forbidden response.
4. IF an authenticated caller lacks the role or policy required for the requested operation, THEN the applicable service SHALL disclose no protected data.
5. WHEN a request fails both authentication and authorization, THE Finnova Platform SHALL return the authentication failure response before role authorization evaluation.
6. THE CustomerOnboarding_Service SHALL authorize Customer management operations separately from Customer read operations.
7. THE CustomerReference_Service SHALL authorize Customer Reference management operations separately from Customer Reference read operations.
8. THE MindMapping_Service SHALL authorize Mind Mapping management operations separately from Mind Mapping read operations.
9. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL collect only fields approved for the request purpose and caller authorization scope.
10. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL return only fields approved for the request purpose and caller authorization scope.
11. IF a response contains Customer PII or sensitive relationship metadata, THEN the applicable service SHALL apply the approved field-level minimization and masking rules before serialization.
12. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL not place secrets, raw authentication tokens, unbounded Customer documents, or unnecessary PII in audit entries, provenance fields, error messages, or logs.
13. WHEN a caller requests a Customer Reference or Mind Mapping, THE applicable service SHALL enforce authorization checks for every referenced Customer and Location before returning endpoint details.
14. WHERE approved retention, legal hold, correction, or erasure rules apply to Customer data, THE applicable service SHALL execute only the approved governance action.
15. WHERE approved retention, legal hold, correction, or erasure rules apply to Customer data, THE applicable service SHALL preserve records required by audit or legal-hold policy.
16. WHEN the CustomerOnboarding_Service calls the Finnova-Document-Verifier API, THE CustomerOnboarding_Service SHALL use the approved service authentication mechanism and SHALL transmit only the minimum document data required by the approved verifier contract.
17. IF a verifier request, response, error, or log contains Aadhaar/PAN data or verifier credentials, THEN the Finnova Platform SHALL prevent raw document content, unnecessary full document numbers, authentication tokens, and unbounded PII from appearing in Customer responses, audit entries, error messages, or logs.
18. IF a caller lacks authorization to initiate, view, or retry identity-document verification, THEN the applicable service SHALL return the approved unauthorized or forbidden response without disclosing document data or verifier details.
19. WHEN a verification dependency failure is returned to an authorized caller, THE applicable service SHALL expose only a safe retryable status and approved English error message without revealing PII or internal verifier response content.

**Assumptions:** System Administrators manage all three data sets and applicable document-verification workflows; broader read access is opt-in and policy-based; and privacy, retention, verifier credential, and field-level masking rules will be confirmed with the product, security, and compliance owners before implementation.

### Requirement 12: Backend Contracts, API Consistency, and Integration Boundaries

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
9. THE backend SHALL use the fixed Customer feature error-code family `ERR-CUSTOMER`, with `ERR-CUSTOMER-400`, `ERR-CUSTOMER-404`, `ERR-CUSTOMER-409`, `ERR-CUSTOMER-500`, and `ERR-CUSTOMER-503` for generic status families and the approved typed semantic codes for Customer reference, immutability, duplicate, and default-bank failures.
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

**Assumption:** Customer, Customer Reference, Mind Mapping, and identity-document verification APIs will be hosted with SystemAdmin functionality and will use gateway aliases if the shared UI axios prefix requires them; the final route names, verifier integration contract, and dependency policy require design confirmation. Customer errors use the fixed `ERR-CUSTOMER` family and canonical status and semantic codes defined in the design.

### Requirement 13: Customer Administration and Relationship UI

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

**Assumption:** The final UI may use separate Customer Onboarding, Customer References, and Mind Mapping views or a tabbed/detail flow; the exact navigation, editable mapping workflow, verification status labels, safe document-entry behavior, and retry interaction require confirmation after FINNOVA-51..55 and the verifier contract are available.

### Requirement 14: Customer KYC Verification via PayU During Application Processing

**User Story:** As a compliance and onboarding owner, I want KYC verification delegated to PayU and orchestrated by a scheduled Python Cron Job during Application Processing, so that the platform relies on an authorized external provider instead of reimplementing KYC checks inline.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL delegate KYC verification to PayU and SHALL NOT reimplement KYC checks locally.
2. THE CustomerOnboarding_Service SHALL treat the PayU API contract, authentication, endpoints, status vocabulary, retry, timeout, idempotency behavior, and credentials as unconfirmed integration blockers to be confirmed before design finalization, and SHALL NOT invent those values.
3. THE KYC Cron Service SHALL perform KYC during Application Processing against an Application and SHALL NOT perform KYC against a bare customer record that has no Application.
4. THE KYC Cron Service SHALL run as a scheduled Cron Job implemented in Python, separate from the .NET request path, and SHALL NOT perform KYC inline within the .NET request path.
5. THE KYC Cron Service SHALL perform all of the following checks through PayU: Video KYC, CIBIL, Sanctions/PEP Screening, and Other Due Diligence.
6. WHEN the KYC Cron Service completes a KYC check, THE KYC Cron Service SHALL record that check's own status, outcome, and UTC timestamps.
7. THE CustomerOnboarding_Service SHALL derive a single aggregate KYC status from the recorded per-check outcomes.
8. WHEN an Application Number is generated, THE CustomerOnboarding_Service SHALL publish a message via a Publisher over MassTransit that triggers or queues the KYC workflow.
9. WHEN the KYC-trigger message is consumed, THE Consumer SHALL initiate the PayU-delegated KYC workflow performed by the KYC Cron Service.
10. THE CustomerOnboarding_Service SHALL persist only the minimum approved KYC Outcome, including per-check status, provider reference, masked or non-sensitive metadata, and UTC timestamps.
11. THE CustomerOnboarding_Service SHALL NOT persist, log, or return raw PayU payloads, raw documents or video, credentials, or unnecessary PII.
12. IF PayU returns a failure, is unavailable, or times out, THEN THE KYC Cron Service SHALL record a safe retryable status without leaking PII and SHALL NOT mark KYC as passed.

**Assumptions:** The PayU contract is unknown and SHALL NOT be invented. The Application and Application Number concept and lifecycle are assumed to exist or be defined by FINNOVA-51..55 and require confirmation. The Python cron schedule, the trigger mechanism (scheduled versus message-driven), whether the cron polls for pending applications or is purely event-driven, and how the Python service authenticates to the platform require confirmation. The provider mapping and status vocabulary for Video KYC, CIBIL, Sanctions/PEP Screening, and Other Due Diligence, and the aggregate-status rule, require confirmation.

### Requirement 15: Customer Behavior Model Training

**User Story:** As a data platform owner, I want to train an open-source Customer Behavior Model from approved customer-behavior data, so that a future AI Agent can consume the trained model without this feature implementing the agent itself.

#### Acceptance Criteria

1. THE Training Pipeline SHALL train an open-source Customer Behavior Model that a future AI Agent can consume.
2. THE Training Pipeline SHALL implement only model training and the training-data boundary and SHALL NOT implement the retrieval or inference AI Agent.
3. THE Training Pipeline SHALL support training on Customer Payments, Customer Preference tracking, changes in Customer Payment Pattern, and Delinquency Tracking.
4. THE Training Pipeline SHALL source training data only from approved, authorized, and minimized datasets.
5. THE Training Pipeline SHALL exclude or mask raw PII and sensitive identifiers in training data according to the approved privacy policy.
6. WHEN the Training Pipeline produces a trained model, THE Training Pipeline SHALL record an auditable account of which data and which model version were used.
7. THE Training Pipeline SHALL run as an offline or batch process and SHALL NOT run inside the synchronous request path.
8. WHEN the Training Pipeline produces a model, THE Training Pipeline SHALL assign a version to the produced Model Artifact.

**Assumptions:** The specific open-source model and framework are unconfirmed. The feature store and data sources for payments, preferences, payment patterns, and delinquency are unconfirmed and must come from approved, authorized, minimized sources. The training cadence, evaluation metrics, model registry, and serving boundary require confirmation. The trained model's use by an AI Agent remains out of scope here (see Requirement 16).

### Requirement 16: Out-of-Scope AI Agent Inference Boundary

**User Story:** As a product owner, I want the retrieval/inference Customer Information AI Agent kept separate from this feature, so that this feature delivers reliable persistence, messaging, KYC orchestration, and model training without prematurely implementing AI inference or serving.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL not implement an AI Agent that retrieves Customer Information.
2. THE CustomerReference_Service SHALL not implement an AI Agent that retrieves Customer Information.
3. THE MindMapping_Service SHALL not implement an AI Agent that retrieves Customer Information.
4. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL not perform AI inference, ranking, retrieval, retrieval-augmented generation, or agent orchestration as part of this feature.
5. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL not generate embeddings as part of this feature.
6. THE CustomerOnboarding_Service, CustomerReference_Service, and MindMapping_Service SHALL not infer relationships, rank Customers, or make automated relationship decisions as part of this feature.
7. THE MindMapping_Service SHALL persist provenance that distinguishes explicit business mappings from asynchronously generated or future AI-proposed mappings when the approved mapping contract supports both sources.
8. THE MindMapping_Service SHALL persist Mapping Status values that distinguish approved, proposed, rejected, superseded, or archived mappings when those values are approved.
9. THE Training Pipeline defined in Requirement 15 SHALL remain in scope as the training-pipeline boundary, and THE feature SHALL NOT implement the AI Agent that consumes the produced Model Artifact.
10. WHEN a future AI Agent consumes Mind Mappings or the Customer Behavior Model, THE future AI Agent SHALL use the approved versioned read contract and SHALL remain responsible for retrieval, inference, ranking, explanation, model serving, and model-specific behavior.
11. THE asynchronous Mind Mapping messaging path (Requirement 9), the PayU-delegated KYC workflow via the Python Cron Job (Requirement 14), and the Customer Behavior Model Training Pipeline (Requirement 15) SHALL remain documented as separate future or deferred boundaries and SHALL NOT be treated as AI inference, ranking, retrieval, or agent orchestration. The current task list SHALL not implement the deferred Customer Reference persistence/management or MassTransit asynchronous Mind Mapping generation stories.
12. IF a requested change requires AI inference, model serving, ranking, retrieval, retrieval-augmented generation, or agent orchestration that consumes the trained model, THEN the requested change SHALL be deferred to a separate future story.
13. IF a requested change requires AI inference, model serving, ranking, retrieval, retrieval-augmented generation, or agent orchestration, THEN the Customer Onboarding, Customer Reference, Mind Mapping, messaging, KYC, and training-pipeline implementation scope SHALL not be expanded by that change.

### Requirement 17: Validate Customer Identity Documents Through the Existing Verifier

**User Story:** As a data owner, I want Aadhaar and PAN validated by the existing document-verification service, so that Customer records are not marked verified using locally reimplemented or untrusted identity checks.

#### Acceptance Criteria

1. WHEN a Customer create or update request requires or supplies an Aadhaar or PAN document under the approved FINNOVA-51..55 contract, THE CustomerOnboarding_Service SHALL request validation for each applicable document from the existing Finnova-Document-Verifier API before persistence or update completion.
2. THE CustomerOnboarding_Service SHALL delegate Aadhaar and PAN document validation to the existing Finnova-Document-Verifier API and SHALL not reimplement document-validation rules locally.
3. WHEN the Finnova-Document-Verifier API returns an approved valid and matched result for every required or supplied document, THE CustomerOnboarding_Service SHALL persist or update the Customer with only the approved Verification Outcome and SHALL expose the approved verification status.
4. IF the Finnova-Document-Verifier API returns a failed, rejected, invalid, or mismatched result for a required or supplied document, THEN THE CustomerOnboarding_Service SHALL preserve the existing Customer on update or persist no new Customer, and SHALL not persist or expose the Customer as verified.
5. IF the Finnova-Document-Verifier API is unavailable, times out, is cancelled, returns an ambiguous result, or fails under the approved dependency policy, THEN THE CustomerOnboarding_Service SHALL preserve the existing Customer on update or persist no new Customer, and SHALL return a safe retryable validation or dependency error without leaking PII.
6. WHEN the CustomerOnboarding_Service calls the Finnova-Document-Verifier API, THE CustomerOnboarding_Service SHALL authorize the call using the approved integration mechanism and SHALL enforce the approved timeout and cancellation bounds.
7. WHEN the CustomerOnboarding_Service retries an eligible verifier call, THE CustomerOnboarding_Service SHALL use the approved idempotency or deduplication behavior where the verifier integration supports it.
8. THE CustomerOnboarding_Service SHALL persist only the minimum approved Verification Outcome: verification status, verifier reference, masked or non-sensitive metadata, and timestamps; raw Aadhaar/PAN images or documents, authentication tokens, and unnecessary full document numbers SHALL not be persisted unless FINNOVA-51..55 explicitly requires them.
9. WHEN the CustomerOnboarding_Service returns a verification failure or dependency error, THE CustomerOnboarding_Service SHALL return only the approved safe English error code/message and SHALL not return raw verifier response content, raw document data, authentication tokens, or unnecessary full document numbers.
10. WHERE FINNOVA-51..55 does not require Aadhaar or PAN and the caller supplies neither document, THE CustomerOnboarding_Service SHALL complete the Customer operation without an identity-document verifier call.
11. IF only one of Aadhaar or PAN is required or supplied under the approved contract, THEN THE CustomerOnboarding_Service SHALL apply validation and persistence rules to that document without assuming that the other document is mandatory.
12. WHEN an existing Customer has a prior Verification Outcome, THE CustomerOnboarding_Service SHALL replace or retain that outcome only according to the approved document-change, revalidation, and verifier-status policy.

**Assumptions:** The verifier endpoint, request/response schema, authentication mechanism, status vocabulary, matching rules, retry policy, idempotency behavior, timeout/cancellation bounds, safe error mapping, and whether both Aadhaar and PAN are mandatory remain unknown and require confirmation. The future Customer Information AI Agent and all relationship inference remain outside this requirement.

### Requirement 18: Customer Onboarding Modes, Tabs, and Record Workflow

**User Story:** As an authorized administrator, I want Customer Create, Modify, and Query modes to expose the approved Customer Master FRS workflow, so that customer records can be created, maintained, and inspected safely.

#### Acceptance Criteria

1. WHEN an authorized user selects Customer Create mode, THE Customer_Admin_UI SHALL provide Basic/Address Details, Personal Details for Individuals, Personal Details for Non-Individuals, Bank Details, Constitution Document Details, Customer Track, Customer Credit Details, and Factoring Sub-Limit sections or tabs according to the Conditional Applicability Matrix.
2. WHEN an authorized user selects Customer Modify mode, THE Customer_Admin_UI SHALL require an existing Customer Code before loading the saved Customer record and SHALL expose only fields approved as mutable by the confirmed mutability matrix.
3. WHEN an authorized user selects Customer Query mode, THE Customer_Admin_UI SHALL require a Customer Code selection and SHALL display the complete authorized saved Customer record in read-only form without save or edit actions.
4. WHEN Customer Create succeeds, THE CustomerOnboarding_Service SHALL return the newly generated Customer Code and the persisted Customer record.
5. WHEN Customer Modify succeeds, THE CustomerOnboarding_Service SHALL return the modified Customer record with the same Customer Code supplied for the existing record.
6. IF Customer Modify receives an unknown Customer Code, THEN THE CustomerOnboarding_Service SHALL return a not-found error and SHALL preserve all Customer records.
7. THE CustomerOnboarding_Service SHALL treat Customer Code as immutable and SHALL reject any request that changes or deletes Customer Code.
8. WHERE the confirmed source mutability matrix excludes Customer Type, Gender, Date of Birth, or Age from modification, THE CustomerOnboarding_Service SHALL reject changes to each excluded field and THE Customer_Admin_UI SHALL render each excluded field read-only.
9. IF the FRS mutability rule for Customer Type conflicts with the approved FINNOVA-51..55 rule, THEN THE feature SHALL defer the conflict to the confirmation checklist rather than silently choosing a mutability behavior.
10. WHILE Customer Track or Customer Credit Details has no applicable existing account, contract, or limit, THE Customer_Admin_UI SHALL keep the section disabled and SHALL not permit a save through that section.

### Requirement 19: Basic Customer and Address Contract

**User Story:** As an authorized administrator, I want the basic Customer and address fields to follow the Customer Master FRS contract, so that every Customer record has consistent identity, classification, and contact data.

#### Acceptance Criteria

1. WHEN an authorized user creates a Customer, THE CustomerOnboarding_Service SHALL require Customer Capacity values for the approved Customer, Guarantor, and Co-applicant checkboxes and SHALL persist the selected capacity flags.
2. WHEN a Customer is created, THE CustomerOnboarding_Service SHALL generate a 12-character Customer Code through the approved Document Number Control definition, SHALL enforce the approved global uniqueness scope, SHALL display the code read-only, and SHALL not accept manual Customer Code entry.
3. IF a frontend request attempts to supply, overwrite, or delete Customer Code, THEN THE CustomerOnboarding_Service SHALL reject the request and THE Customer_Admin_UI SHALL not provide a manual-entry or delete action.
4. WHEN a Customer is created, THE CustomerOnboarding_Service SHALL require Customer Type, CustomerPostingGroupCode, ConstitutionCode, NationalityCode, TitleCode, and Customer Name.
5. THE CustomerOnboarding_Service SHALL resolve Customer Type, ConstitutionCode, NationalityCode, TitleCode, and related controlled values through their authoritative master boundaries, SHALL select only active values, SHALL resolve CustomerPostingGroupCode through GL Master, SHALL require an active approved postable GL account under the applicable Company scope unless the existing GL contract states otherwise, and SHALL not hard-code the final vocabulary in the Customer contract. Source examples such as Individual, Non-Individual, Ministry, LLC, and Partnership are illustrative only until the approved India-compatible lists are confirmed.
6. WHEN Customer Name is entered, THE CustomerOnboarding_Service SHALL support filtering and selecting an existing Customer Name to detect duplicates, SHALL support First Name, Second Name, Third Name, Fourth Name, Fifth Name, and Sixth Name components as applicable to the approved Customer Type, and SHALL enforce the approved duplicate-selection flow that allows no more than one selected Customer Code at a time.
7. WHERE GroupCode is supplied, THE CustomerOnboarding_Service SHALL accept only a user-entered numeric value of no more than 4 digits, SHALL validate GroupCode through Lookup Master using the configured Group lookup type when Group is lookup-backed, SHALL enforce the approved uniqueness rule, and SHALL resolve GroupName as a display projection.
8. WHERE GroupCode is configured as a Lookup Master reference, THE CustomerOnboarding_Service SHALL not accept GroupName as an authoritative write value. WHERE the approved group workflow permits creation of a new group code/name, THE CustomerOnboarding_Service SHALL apply that separately approved workflow; otherwise THE CustomerOnboarding_Service SHALL reject unknown or inactive GroupCode values.
9. WHERE Customer Type is Non-Individual, THE CustomerOnboarding_Service SHALL require IndustryCode according to the Conditional Applicability Matrix, SHALL preserve the FRS maximum of 3 characters and numeric/alphanumeric format rule as confirmed, SHALL validate IndustryCode through Lookup Master where configured, and SHALL resolve IndustryName as a display projection. Whether Industry permits new-code creation remains a confirmation item.
10. THE CustomerOnboarding_Service SHALL resolve ConstitutionCode through Lookup Master and SHALL use ConstitutionCode to determine the applicable Constitution Document definitions from Document Master and DocumentApplicabilityRule; ConstitutionName SHALL be a resolved display projection rather than an independently persisted authoritative value.
11. THE CustomerOnboarding_Service SHALL display English values in left-to-right layouts and SHALL not add Oman, Arabic, bilingual, non-English, or RTL values to any basic Customer field.
12. WHEN NationalityCode is supplied, THE CustomerOnboarding_Service SHALL resolve it through `INationalityRepository`, SHALL permit only an active Nationality Master row, SHALL store the code/reference only, and MAY include NationalityName only in an authorized Read Projection.

### Requirement 20: Communication and Permanent Address Contract

**User Story:** As an authorized administrator, I want Communication Address and Permanent Address to share validated fields and support copying, so that Customer contact information is complete and consistent.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL obtain Communication Address and Permanent Address through the shared India Address Control contract using the established Company/Organization address field conventions where applicable, including House Number, Block Number, Postal/PIN Code, City, State, Country, P.O. Box, Way Number, Telephone, Residence Fax, Mobile, Email, Website, Contact Person, Contact Person Phone Number, Contact Person Flat Number, Area/Sheik, and Location/Landmark.
2. WHEN an authorized user invokes the address copy action, THE Customer_Admin_UI SHALL copy the approved Communication Address values into Permanent Address values through the Customer address contract without changing Customer Code or unrelated fields.
3. THE CustomerOnboarding_Service SHALL apply the India Address Control contract's approved validation, wrapping, lookup, and field-length behavior and SHALL not duplicate address lookup ownership or validation logic.
4. THE CustomerOnboarding_Service SHALL accept only address values and references approved by India Address Control and SHALL return an English field-level validation error for an unknown, inactive, malformed, or unauthorized address reference.
5. THE CustomerOnboarding_Service SHALL apply the approved Indian phone-number format and source special-character restrictions for Telephone, Residence Fax, Mobile, and Contact Person Phone Number through the address contract when confirmed.
6. THE CustomerOnboarding_Service SHALL validate Email using the approved email format and Website using the approved website format before persistence, while preserving India Address Control ownership of address validation.
7. THE CustomerOnboarding_Service SHALL validate Postal/PIN Code through the approved India Address Control parameter configuration and SHALL reject a value that fails the configured Indian postal validation.
8. IF an address value or reference exceeds its approved maximum, contains a prohibited character, fails a configured format rule, or is stale/inactive in India Address Control, THEN THE CustomerOnboarding_Service SHALL return field-level errors and SHALL persist no partial address change or audit entry.
9. WHEN an authorized Customer response is returned, THE CustomerOnboarding_Service MAY include resolved address display or lookup values in a Read Projection, but THE CustomerOnboarding_Service SHALL not persist copied address lookup values as authoritative Customer fields.



### Requirement 21: Individual Personal Details

**User Story:** As an authorized administrator, I want individual Customer personal details to follow approved Indian applicability and validation rules, so that personal records are complete without importing Oman-specific source values.

#### Acceptance Criteria

1. WHEN Customer Type is Individual, THE CustomerOnboarding_Service SHALL require GenderCode and Date of Birth and SHALL resolve GenderCode from the approved India-compatible Lookup Master controlled list.
2. THE CustomerOnboarding_Service SHALL accept Date of Birth in valid DD/MM/YYYY form, SHALL reject today and future dates, and SHALL derive Age using the approved age-calculation rule for display only.
3. IF Date of Birth is malformed, today, or in the future, THEN THE CustomerOnboarding_Service SHALL return an English field-level validation error and SHALL persist no partial personal-details change.
4. THE CustomerOnboarding_Service SHALL support MaritalStatusCode through Lookup Master, optional Wedding Anniversary Date, and conditional Spouse Name when the approved status is Married.
5. THE CustomerOnboarding_Service SHALL support numeric Children and Total Dependents values using the confirmed source two-digit constraint and SHALL reject negative or malformed values.
6. WHERE House/Flat and Own are applicable, THE CustomerOnboarding_Service SHALL resolve both fields from approved Lookup Master controlled values or the approved address boundary.
7. WHERE Current Market Value, Remaining Loan Value, or Total Net Worth is applicable, THE CustomerOnboarding_Service SHALL accept only non-negative numeric values within the confirmed source size constraints and SHALL apply the approved calculation ownership for derived values.
8. WHEN Customer Type is Individual, THE CustomerOnboarding_Service SHALL require Qualification and Profession as free-text fields of no more than 40 characters each.
9. WHERE an identity or document field is applicable, THE CustomerOnboarding_Service SHALL support Driving License Number, Visa Number, an India-approved NID-equivalent, an India-approved labour-card-equivalent, an India-approved civil-number-equivalent, Passport Issue Date, and Passport Expiry Date only according to the Conditional Applicability Matrix and Document Master definitions.
10. THE CustomerOnboarding_Service SHALL validate identity/document formats, uniqueness, date ordering, and India applicability through Approved India Identity Rules and SHALL not seed or require Oman NID, Oman labour card, Oman civil number, or other jurisdiction-specific values.
11. IF an individual-only field is submitted for a non-Individual Customer or a field required by the applicability matrix is omitted, THEN THE CustomerOnboarding_Service SHALL return a conditional-applicability validation error and SHALL persist no partial change.
12. WHEN an authorized Customer response includes GenderName, MaritalStatusName, or another Lookup-owned display value, THE CustomerOnboarding_Service MAY return the value in a Read Projection, but Customer write models SHALL store the code/reference only.

### Requirement 22: Non-Individual Personal and Business Details

**User Story:** As an authorized administrator, I want Non-Individual Customer business details to follow explicit conditional rules, so that organization records contain only applicable and validated information.

#### Acceptance Criteria

1. WHEN Customer Type is Non-Individual, THE CustomerOnboarding_Service SHALL resolve Public/Closely Held through the configured Lookup Master type and SHALL enforce the configured applicability rule.
2. THE CustomerOnboarding_Service SHALL accept Number of Directors/Partners as a numeric value of no more than 2 digits and SHALL reject negative or malformed values.
3. THE CustomerOnboarding_Service SHALL support Key Decision Maker, Listed at Stock Exchange, Paid-up Capital, Face Value of Shares, Book Value of Shares, Business Profile, Monthly Business Income, Geographical Coverage, Number of Branches, Government/Institutional Participation, Promoter Stake Percentage, JV Partner Name, JV Partner Stake, CEO/MD Name, Age of CEO/MD, Experience in Years, Residential Address, Wedding Anniversary Date, CustomerBusinessTypeCode, CustomerBusinessSubTypeCode, IndustrySectorCode, SubSectorCode, C.R. Number, C.R. Issue Date, C.R. Expiry Date, Business Activities, Factoring Applicable, Factoring Type, Factoring Limit, Covenants, and Covenants Clause according to the Conditional Applicability Matrix.
4. THE CustomerOnboarding_Service SHALL enforce a maximum of 240 characters for Business Profile, 120 for Geographical Coverage, 5 digits for Number of Branches, 120 for JV Partner Name, 50 for CEO/MD Name, 3 digits for Age of CEO/MD, 3 digits for Experience in Years, 300 for Residential Address, and 100 for Business Activities.
5. THE CustomerOnboarding_Service SHALL enforce the approved numeric format, non-negative rule, percentage range, and source size constraints for Paid-up Capital, share values, Monthly Business Income, Number of Branches, Promoter Stake Percentage, JV Partner Stake, Age of CEO/MD, Experience in Years, and Factoring Limit.
6. WHEN C.R. dates are supplied, THE CustomerOnboarding_Service SHALL validate C.R. Issue Date and C.R. Expiry Date and SHALL enforce the approved date ordering; the same rule SHALL apply to other document issue and expiry dates.
7. WHERE CustomerBusinessTypeCode, CustomerBusinessSubTypeCode, occupation, SS Employee Indicator, IndustrySectorCode, SubSectorCode, TrackTypeCode, or Factoring Applicable controls a field, THE CustomerOnboarding_Service SHALL resolve controlled codes through Lookup Master using named Lookup Type Constants and SHALL apply the corresponding required, optional, read-only, or unavailable state from the Conditional Applicability Matrix.
8. IF a Non-Individual request supplies an unavailable field, omits a required conditional field, or violates a conditional rule, THEN THE CustomerOnboarding_Service SHALL return a field-level applicability error and SHALL persist no partial change.
9. THE CustomerOnboarding_Service SHALL treat exact source values, occupation rules, SS Employee Indicator semantics, business classification vocabulary, Lookup Type Constants, and error messages as confirmation items and SHALL not invent unapproved values.
10. WHEN an authorized Non-Individual response includes CustomerBusinessTypeName, CustomerBusinessSubTypeName, IndustrySectorName, SubSectorName, or another Lookup-owned display value, THE CustomerOnboarding_Service MAY return the value in a Read Projection, but Customer write models SHALL store the code/reference only.

### Requirement 23: Bank Details and Default Account Integrity

**User Story:** As an authorized administrator, I want to maintain validated Customer bank accounts, so that payment and ECS mapping use one unambiguous default account.

#### Acceptance Criteria

1. WHEN a bank account is added, THE CustomerOnboarding_Service SHALL require AccountTypeCode and Account Number and SHALL resolve AccountTypeCode from Lookup Master using the approved Account Type lookup type, including approved equivalents of Current Account, Savings Account, and Others.
2. THE CustomerOnboarding_Service SHALL accept Account Number as alphanumeric text of no more than 16 characters and SHALL enforce the approved bank-account uniqueness rule for the Customer.
3. THE CustomerOnboarding_Service SHALL resolve Bank Name and Bank Branch from Bank Code through the approved Bank Master boundary and SHALL enforce a maximum of 40 characters for each displayed projection without persisting copied bank master names as authoritative Customer fields.
4. THE CustomerOnboarding_Service SHALL enforce a maximum of 10 characters and the approved special-character restrictions for MICR Code, validate Clearing Date, and enforce a maximum of 300 characters for Address through the approved bank/address boundaries.
5. WHERE Contract Number or Sub-Contract Number is supplied by the account/contract boundary, THE Customer_Admin_UI SHALL display each value read-only and THE CustomerOnboarding_Service SHALL enforce a maximum of 15 characters for each.
6. THE CustomerOnboarding_Service SHALL support Default Account as a checkbox and SHALL enforce exactly one default account for a Customer when at least one account is active.
7. WHEN a new account is marked default, THE CustomerOnboarding_Service SHALL clear the prior default assignment atomically and SHALL map payment/ECS behavior to the resulting default account.
8. IF an account request duplicates an existing Customer bank account under the approved duplicate key, THEN THE CustomerOnboarding_Service SHALL return a duplicate-account error and SHALL preserve existing accounts.
9. WHEN an authorized user adds, modifies, or clears a bank-grid row, THE Customer_Admin_UI SHALL preserve the source grid behavior, SHALL validate the row before save, and SHALL refresh the saved account list after success.
10. IF a bank-account operation would leave multiple defaults or no default where a default is required, THEN THE CustomerOnboarding_Service SHALL reject the operation and SHALL persist no partial bank change.
11. WHEN an authorized Customer response includes BankName, BankBranch, or another Bank Master display value, THE CustomerOnboarding_Service MAY return the value in a Read Projection, but Customer write models SHALL store BankCode or the approved bank identifier/reference only.

### Requirement 24: Constitution Document Details and DMS References

**User Story:** As an authorized administrator, I want Constitution Documents to follow configured definitions and India-approved identity rules, so that required documents are collected consistently without storing raw binaries in Customer records.

#### Acceptance Criteria

1. WHEN a Customer Create or Modify screen loads Constitution Document Details, THE CustomerOnboarding_Service SHALL resolve ConstitutionCode through Lookup Master and SHALL load active document definitions from Document Master and DocumentApplicabilityRule for the selected Constitution and applicable Customer role/type.
2. THE Customer_Admin_UI SHALL display each configured document row with Document Category, Document Code/Description, Mandatory/Optional, Image Required/Not Required, Remarks, Collected Yes/No, Scanned Yes/No, Values, File Upload, and View controls as permitted by the active Document Master definition.
3. WHERE Document Category is Customer Identity Details, THE CustomerOnboarding_Service SHALL permit Customer-entered document details subject to the approved India Identity Rule; WHERE another category is configured, THE CustomerOnboarding_Service SHALL apply the configured Document Master definition without allowing unauthorized category-specific edits.
4. THE CustomerOnboarding_Service SHALL enforce the configured maximum for document Values, using the source example of 20 characters only until an approved definition supplies a different limit.
5. WHEN an identity document is entered, THE CustomerOnboarding_Service SHALL enforce uniqueness for the Customer and SHALL detect duplicates across Customers using the approved normalized identity key.
6. IF an identity document duplicates another Customer, THEN THE Customer_Admin_UI SHALL display the related Customer Code, Customer Name, and approved duplicate details and SHALL offer only the confirmed alert/continue or abandon flow.
7. WHEN a scanned document is uploaded, THE CustomerOnboarding_Service SHALL send it through the configured DMS path and SHALL store only approved selected document metadata and references in the Customer record.
8. THE CustomerOnboarding_Service SHALL not store raw document binaries in the Customer record and SHALL not expose raw document content through Customer, audit, error, log, or messaging contracts.
9. IF an active applicable mandatory configured document is missing, an image-required document lacks its required scan, or a document reference is unknown, inactive, or invalid, THEN THE CustomerOnboarding_Service SHALL return field-level document-validation errors and SHALL persist no incomplete Customer document state or audit entry.
10. THE CustomerOnboarding_Service SHALL treat the DMS, Lookup Master ConstitutionCode type, Document Master, DocumentApplicabilityRule, identity mapping, duplicate-alert text, and document error vocabulary as deferred integration/configuration contracts requiring confirmation.
11. WHEN an authorized Customer response includes DocumentName, DocumentDescription, or another Document Master display value, THE CustomerOnboarding_Service MAY return the value in a Read Projection, but Customer write models SHALL store the selected document identifier/code and approved metadata/reference only.

### Requirement 25: Customer Track and Classification Controls

**User Story:** As an authorized administrator, I want Customer Track classifications to be maintained only after account or contract activity exists, so that downstream credit controls reflect the approved customer history.

#### Acceptance Criteria

1. WHILE Customer Create mode is active, THE Customer_Admin_UI SHALL disable Customer Track and SHALL not permit track-row creation or editing.
2. WHEN Customer Modify mode loads a Customer with existing accounts or contracts, THE Customer_Admin_UI SHALL enable Customer Track and SHALL display Line of Business, Account Number, Type, Date, Reason, By Login, Release Date, Released By, and Black Listed indicator.
3. THE CustomerOnboarding_Service SHALL resolve TrackTypeCode from Lookup Master using the approved Track Type lookup type, including Black Listed, Legal, Defaulter, and Repossessed plus other confirmed values, without hard-coding a final vocabulary.
4. THE CustomerOnboarding_Service SHALL associate Account Number with no more than one applicable classification at a time and SHALL reject duplicate Account Number classification rows across categories.
5. WHEN a Customer is Black Listed, THE Account/Contract Boundary SHALL prevent new account or contract creation until an authorized release is recorded.
6. WHEN a Customer is classified as Delinquent, Defaulter, Repossessed, Legal, or another approved credit-impacting type, THE CustomerOnboarding_Service SHALL publish or expose the classification through the approved downstream credit-rule boundary without implementing deferred external integration inline.
7. WHEN a track classification is released, THE CustomerOnboarding_Service SHALL require Release Date and Released By according to the approved release policy and SHALL preserve the track history.
8. IF a track row has an unknown Account Number, duplicate classification, invalid date, unknown or inactive TrackTypeCode, or unapproved Type, THEN THE CustomerOnboarding_Service SHALL return field-level errors and SHALL preserve existing track history and audit state.

### Requirement 26: Customer Credit Details and Factoring Sub-Limit

**User Story:** As an authorized administrator, I want credit and factoring details to reflect approved account and limit boundaries, so that Customer limits are visible without allowing unauthorized derived-value edits.

#### Acceptance Criteria

1. WHILE Customer Create mode is active or no Customer/account limit exists, THE Customer_Admin_UI SHALL disable Customer Credit Details.
2. WHEN Customer Modify mode has an existing Customer/account limit, THE Customer_Admin_UI SHALL display Line of Business, Product Group, Sanctioned Amount, Approved Amount, Utilized Amount, Remaining Amount, Valid Up To, and Facility Type.
3. WHEN Customer Query mode is used, THE Customer_Admin_UI SHALL display the complete authorized Customer Credit Details projection as read-only.
4. THE CustomerOnboarding_Service SHALL derive or retrieve Sanctioned Amount, Approved Amount, Utilized Amount, Remaining Amount, and Facility Type from approved account/limit boundaries and SHALL not accept direct edits to those values.
5. WHERE the approved modify contract permits it, THE CustomerOnboarding_Service SHALL permit only Valid Up To to be user-modified and SHALL validate its date format and approved date range.
6. THE CustomerOnboarding_Service SHALL support Factoring Applicable, Factoring Type, Factoring Sub-Limit/Limit, Covenants, Covenant Clause, and Cut-Off Date according to the Conditional Applicability Matrix.
7. WHEN Factoring Applicable is true, THE CustomerOnboarding_Service SHALL require an approved Factoring Type and Customer/entity sub-limit and SHALL apply the approved invoice-loading boundary to enforce that sub-limit.
8. IF a user edits a derived credit amount, an unavailable factoring field, or a date/value outside the approved rule, THEN THE CustomerOnboarding_Service SHALL return a validation error and SHALL preserve the approved derived values.
9. THE CustomerOnboarding_Service SHALL defer exact credit calculations, factoring calculation ownership, approval workflow, entity/customer allocation, invoice-loading integration, and downstream credit enforcement to the confirmed account, limit, and factoring contracts.
10. THE CustomerOnboarding_Service SHALL resolve FactoringType and other controlled factoring/classification values through Lookup Master using named Lookup Type Constants and SHALL reject unknown or inactive codes.
11. WHEN an authorized Customer response includes derived credit display values or Lookup-owned factoring names, THE CustomerOnboarding_Service MAY return those values in a Read Projection, but Customer write models SHALL not accept caller-supplied derived values or master-owned display names as authoritative.

### Requirement 27: Cross-Cutting Customer Validation, Security, Audit, and Outputs

**User Story:** As a product and security owner, I want every Customer Onboarding operation to enforce the complete field and access contract, so that Customer data remains valid, traceable, and protected.

#### Acceptance Criteria

1. THE CustomerOnboarding_Service SHALL enforce required fields, maximum lengths, numeric formats, non-negative values, date ordering, future-date restrictions, email and website formats, special-character restrictions, duplicate code/group/industry/bank/document rules, and conditional applicability before persistence.
2. IF any validation, uniqueness, referential, authorization, or integration-boundary check fails, THEN THE CustomerOnboarding_Service SHALL return an approved English error contract and SHALL persist no partial Customer, bank, document, track, credit, or factoring change.
3. THE CustomerOnboarding_Service SHALL require authenticated and authorized access separately for Customer Create, Modify, Query, and related sections, and SHALL preserve the existing JWT, authorization, privacy, and audit requirements.
4. WHEN a Customer Create or Modify operation succeeds, THE CustomerOnboarding_Service SHALL create the approved audit entry and SHALL return the Customer Code and authorized saved Customer projection.
5. WHEN a Customer Query operation succeeds, THE CustomerOnboarding_Service SHALL return the complete authorized saved Customer record without a save or edit side effect.
6. THE Customer_Admin_UI SHALL preserve loading, empty, success, validation-error, authorization-error, duplicate-warning, integration-deferred, and retryable-error states using English user-facing text.
7. THE Customer_Admin_UI SHALL use the shared axios instance and existing service-toggle architecture when calling Customer Onboarding APIs and SHALL not create an alternate authentication or HTTP boundary.
8. THE CustomerOnboarding_Service SHALL publish only minimum approved identifiers and metadata to existing asynchronous messaging boundaries and SHALL not include raw PII, raw document binaries, secrets, or unbounded Customer data.
9. THE CustomerOnboarding_Service SHALL preserve the existing deferred boundaries for PayU KYC, Python scheduling, DMS, account/contract, bank, GL, credit, factoring, and future AI consumption; deferral SHALL not remove the corresponding field or workflow contract. Customer Reference persistence/management and MassTransit asynchronous Mind Mapping generation are deferred follow-up stories for this implementation slice, and the current task list SHALL not implement those two capabilities.
10. THE Customer_Admin_UI SHALL render English, India-compatible values in left-to-right layouts and SHALL not add bilingual fields, Arabic/non-English values, Oman seed data, or RTL behavior.
11. IF a Customer create or modify request contains an unknown, inactive, malformed, unauthorized, or stale Lookup Master reference, THEN THE CustomerOnboarding_Service SHALL return field-level validation errors and SHALL persist no Customer or child-row change.
12. IF a Customer create or modify request contains an unknown or inactive Nationality Master reference, THEN THE CustomerOnboarding_Service SHALL return a field-level validation error and SHALL persist no Customer or child-row change.
13. IF a Customer create or modify request contains a CustomerPostingGroupCode that does not resolve through GL Master to an active, approved, postable GL account under the applicable Company scope, THEN THE CustomerOnboarding_Service SHALL return a field-level validation error and SHALL persist no Customer or child-row change.
14. IF a Customer create or modify request contains a ConstitutionCode or selected Document Master reference that is unknown, inactive, or not applicable under DocumentApplicabilityRule for the selected Constitution and Customer role/type, THEN THE CustomerOnboarding_Service SHALL return field-level document errors and SHALL persist no Customer or child-row change.
15. IF a Customer create or modify request contains an unknown, inactive, malformed, or unapproved India Address Control value or reference, THEN THE CustomerOnboarding_Service SHALL return field-level address errors and SHALL persist no Customer or child-row change.
16. IF any unwanted master-reference event in criteria 11 through 15 occurs, THEN THE CustomerOnboarding_Service SHALL roll back the complete operation atomically and SHALL create no business-change Audit Entry.
17. THE CustomerOnboarding_Service SHALL store only master-owned codes, identifiers, approved document metadata/references, and approved address values/references in Customer write models and entities; THE CustomerOnboarding_Service SHALL resolve names through authorized Read Projections.
18. WHEN an authorized Customer response is returned, THE CustomerOnboarding_Service MAY include resolved GroupName, IndustryName, ConstitutionName, NationalityName, GLName, bank names, address lookup values, and other display fields explicitly allowed by the read contract, but caller-supplied display names SHALL not override master resolutions.
19. THE CustomerOnboarding_Service SHALL resolve MaritalStatusCode, CustomerBusinessTypeCode, CustomerBusinessSubTypeCode, IndustrySectorCode, SubSectorCode, TrackTypeCode, TitleCode, and related controlled fields through Lookup Master named Lookup Type Constants or equivalent contract seams. Exact constants and values remain confirmation items.
20. THE CustomerOnboarding_Service SHALL preserve the FRS numeric, length, date, conditional-applicability, Customer Onboarding naming, India-only, English-only, and no-RTL requirements while applying the master-data ownership boundaries in this document.

## Confirmation Checklist Before Design


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
11. Confirm that the retrieval/inference Customer Information AI Agent remains a separate story and that this feature delivers persistence, authorized contracts, the approved document-verification boundary, the asynchronous mind-mapping messaging path, the PayU-delegated KYC workflow, and the Customer Behavior Model training pipeline.
12. Confirm the Message Broker choice (for example RabbitMQ or Azure Service Bus), that MassTransit is the approved messaging library, the Customer Onboarding Worker consumer host, and the delivery/outbox policy (transactional outbox versus at-least-once) and retry/dead-letter policy for asynchronous mind-mapping.
13. Confirm the exact message payloads for the four mind-mapping trigger events (customer created/updated, reference changed/removed, address changed, and customer purged/removed), including which identifiers, change-type discriminator, provenance, correlation id, and timestamp each carries, and the minimum-identifier/no-PII rule.
14. Confirm the PayU KYC integration contract, credentials, authentication, endpoints, status vocabulary, and retry/timeout/idempotency behavior.
15. Confirm the Application and Application Number concept and lifecycle, including whether they are defined by FINNOVA-51..55 and how Application Number generation triggers KYC.
16. Confirm the Python KYC Cron Service trigger mechanism (scheduled versus message-driven), the cron schedule, whether the cron polls for pending applications or is purely event-driven, and how the Python service authenticates to the platform.
17. Confirm the providers and status vocabulary for the four KYC checks (Video KYC, CIBIL, Sanctions/PEP Screening, and Other Due Diligence) and the rule for deriving the aggregate KYC status.
18. Confirm the open-source Customer Behavior Model choice/framework, the approved training data sources for Customer Payments, Customer Preference tracking, Customer Payment Pattern changes, and Delinquency Tracking, the model registry, and the training cadence.
19. Confirm the boundary between the in-scope Customer Behavior Model training pipeline and the out-of-scope AI Agent inference/serving that consumes the trained Model Artifact.

20. Confirm the final India-compatible Customer Type vocabulary, Title vocabulary, Nationality lookup values, Gender values, Marital Status values, Account Type values, Constitution values, Customer Posting Group/GL mapping, Industry values, business classifications, Track Type classifications, and all other LOV vocabularies. Do not add Oman, Arabic, or other non-English values.
21. Confirm the exact Customer Code Document Number Control definition, 12-character formatting, generation transaction, concurrency behavior, and uniqueness scope, including whether "globally unique" means platform-wide or tenant-wide.
22. Confirm the exact Customer Modify mutability matrix, including the source exclusion of Customer Code, Customer Type where specified, Gender, Date of Birth, and Age, and resolve any conflict between FRS sections or FINNOVA-51..55.
23. Confirm maximum lengths and numeric precision for every field where the screenshots omit a size, including address fields, Customer Name components, Group Name, values, financial amounts, percentages, account fields, and document metadata.
24. Confirm the complete Conditional Applicability Matrix for Customer Type, occupation, SS Employee Indicator, Customer Business Type/Sub-type, Factoring Applicable, Constitution Code, account/contract state, and all required, optional, read-only, or unavailable fields.
25. Confirm the India identity-document mapping for the FRS examples of Oman nationality, NID, labour card, civil number, passport, visa, and driving licence, including approved formats, applicability, uniqueness, masking, verification, and retention. Do not map an Oman example directly into Finnova seed data.
26. Confirm DMS upload/reference behavior, Constitution Code and Document Master contracts, document categories, mandatory/image rules, maximum document-value lengths, identity duplicate behavior, related-customer alert details, and the exact alert/continue or abandon flow.
27. Confirm bank master/Bank Code resolution, bank-account duplicate key, MICR and phone formats, default-account transition behavior, Clearing Date rules, and account/contract ownership of Contract Number and Sub-Contract Number.
28. Confirm Chart of Accounts ownership of Customer Posting Group and GL codes, and the bank, account, contract, payment, and ECS integration contracts.
29. Confirm Customer Track classification vocabulary, release permissions, Account Number uniqueness across classifications, blacklist enforcement boundary, and downstream Legal/Defaulter/Repossessed/Delinquent credit-rule integration behavior.
30. Confirm credit and factoring calculation ownership and formulas for Age, Total Net Worth, Remaining Amount, Factoring Sub-Limit/Limit, utilization, Cut-Off Date, invoice-loading enforcement, and aggregate downstream credit outcomes. Confirm that only Valid Up To is editable unless an approved future change expands mutability.
31. Confirm source-specific labels, field captions, duplicate messages, validation messages, continue/abandon wording, and integration-deferred error messages. All Finnova values and messages remain English only.
32. Confirm that external DMS, Document Master, bank, account/contract, Chart of Accounts, credit, factoring, PayU, and downstream integrations remain deferred contracts with explicit future owners and do not get silently removed from scope.
33. Confirm the exact Lookup Master module/type constants and active-value contract for Group, Industry, Constitution, Title, Marital Status, Customer Business Type, Customer Business Sub-Type, Industry Sector, Sub-Sector, Track Type, Account Type, and every other controlled Customer field.
34. Confirm whether Group and Industry are pure Lookup Master references or whether an approved workflow permits new-code creation. If new-code creation is permitted, confirm its owner, uniqueness rule, approval path, and whether the created value is written to Lookup Master rather than Customer.
35. Confirm how CompanyId is resolved for CustomerPostingGroupCode validation and persistence, including the applicable Company scope source, tenant/user context, request field policy, and fallback behavior.
36. Confirm the exact GL Master validity rule for CustomerPostingGroupCode: active, approved, postable, Company-scoped, and any additional account-status or effective-date conditions. Confirm whether the existing GL contract changes any part of this rule.
37. Confirm Document Master and DocumentApplicabilityRule applicability dimensions, including ConstitutionCode, Customer role, Customer Type, business type/sub-type, document category, mandatory/image flags, and whether the Constitution Document UI is generated only from active applicable definitions.
38. Confirm the India Address Control contract interface/path, request and response shape, approved address reference semantics, validation ownership, and the established Company/Organization field conventions that Customer must reuse.
39. Confirm which display-name fields are projection-only, including GroupName, IndustryName, ConstitutionName, NationalityName, GLName, bank names, address lookup values, document names/descriptions, and controlled-value names, and confirm that write models/entities accept codes/IDs/references only.
40. Confirm that stale or inactive Lookup, Nationality, GL, Document, or India Address Control references fail atomically with field-level errors, no business-data change, and no business-change audit entry.
