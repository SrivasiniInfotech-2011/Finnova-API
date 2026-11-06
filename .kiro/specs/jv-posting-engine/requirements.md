# Requirements Document

## Introduction

The JV Posting Engine (JVE) is a scheduled financial-processing capability for Finnova. An approved .NET API initiates a journal-voucher posting process by creating or submitting a posting-inbox request and marking the request eligible for processing. The .NET API does not perform journal posting. A Python 3.11 worker, started by an operating-system CRON job, claims eligible records, parses and validates the JSON, resolves the applicable accounting rule from the future Cashflow Master contract, validates the resulting voucher, and posts the journal in SQL Server. Requests that cannot be completed are retried according to the baseline policy and then retained in a dead-letter store for investigation and controlled recovery.

The feature must protect accounting integrity. A successfully posted journal voucher must have valid GL references, valid dimensions, exactly one debit-or-credit side per line, equal debit and credit totals, deterministic journal identity, a resolved posting actor, and an auditable relationship between the source request, the initiating user, the posting attempt, the resulting journal, and any failure. Reprocessing the same source request must not create a duplicate journal entry. The canonical journal records, durable source link, resolved posting actor, posting audit data, and completion of the posting request must be committed in one SQL Server transaction or approved stored procedure.

The execution model is deliberately different from the existing ASP.NET Core request path. The approved .NET API is the submission boundary only; the Python CRON worker is the processing and actual journal-posting boundary. The worker uses SQL Server as its durable work store and must not expose an unapproved public submission endpoint. Source systems must not write canonical journal tables directly. Finnova's existing .NET services remain the platform boundary for authentication, master-data contracts, approved submission, operational access, and any journal or GL contracts already owned by those services. The Python engine must use versioned, configurable Cashflow and GL adapter contracts, must fail closed when no Cashflow rule is available, and must not duplicate Cashflow Master rules or silently invent journal-table ownership.

The baseline feature is India-specific and English-only. Baseline configuration values are Python 3.11, CRON every 1 minute, a batch size of 50, a 10-minute claim lease and processing deadline, retry delays of 1, 5, and 30 minutes for up to 3 attempts, Asia/Kolkata business timezone, INR with two-decimal round-half-up precision, and platform-approved retention, observability, and security defaults. Approved configuration may override these values without changing the domain requirements. The feature adds no bilingual fields, non-English values, RTL handling, or translation framework, and includes no Finnova-UI work or browser journal/JVE admin screen.

This document is based on Jira FINNOVA-25 and the supplied stakeholder request. Decisions not specified by the request are recorded explicitly as Assumptions for stakeholder confirmation. This document contains requirements only. It does not authorize source-code implementation, database migration execution, build execution, or test execution.

## Dependencies and Assumptions

- **Dependency (SQL Server):** The Finnova platform uses SQL Server as the durable store. The JV Posting Inbox, posting-attempt history, dead-letter records, feature audit records, and canonical journal records are persisted in the same SQL Server database for this feature's atomic posting transaction.
- **Assumption A1 (Python runtime and deployment):** The baseline worker runtime is Python 3.11. A supported dependency installation method, process identity, CRON host, deployment topology, and approved secret-injection mechanism are available outside the .NET solution. Packaging, deployment, operating-system account, and secret-provider details remain implementation decisions.
- **Assumption A2 (CRON cadence and batch):** The baseline worker invocation is every 1 minute with a bounded batch size of 50. Approved configuration may override the cadence or batch size. Multiple worker instances may run concurrently, subject to the claim and transaction controls in this document.
- **Assumption A3 (Submission boundary):** The approved .NET API is the only submission boundary for this feature. The API creates or submits the inbox request and marks it eligible; it does not parse the business payload, resolve rules, validate the voucher, or post the journal. The Python CRON worker performs those operations. The worker does not expose an unapproved public submission endpoint, and source systems do not write canonical journal tables directly.
- **Assumption A4 (Journal table ownership and actor mapping):** Canonical Journal Header, Journal Line, and source-link tables remain owned by the existing accounting/GL platform and are written through the approved Journal Writer contract. The exact canonical table names, column names, and field mappings for `InitiatingUserId`, `PostingUserId`/`PostedByUserId`, source metadata, and journal status remain to be confirmed.
- **Assumption A5 (Persistence boundary):** Feature-owned inbox records and canonical journal records use the same SQL Server database and are committed by one SQL transaction or approved stored procedure. The baseline contains no remote-writer, transactional-outbox, or equivalent distributed-transaction alternative.
- **Dependency (GL Master and GL validation):** The engine depends on the active GL Master and its validation rules. Every journal line must resolve to a postable GL account and satisfy the applicable system-journal validation permissions and required dimensions. The existing in-process JV validation contract is the default adapter boundary; transport and schema details remain configurable.
- **Dependency (Cashflow Master):** Cashflow Master is the future authoritative owner of event-to-accounting rules, posting-group mappings, required dimensions, effective dates, and transaction-specific debit/credit derivation. This feature consumes a versioned, configurable rule-resolution contract and does not create, seed, manage, or duplicate Cashflow Master rules. Cashflow Master may be developed later; the JVE fails closed when no applicable rule is available.
- **Assumption A6 (Cashflow rule contract):** A request identifies a Cashflow event or rule key and supplies the business context required for rule resolution. The default adapter contract expects Cashflow Master to return one active rule version effective for the request's value date, including generated or expected journal lines, required dimensions, permitted currency, and rule version identifier. The transport and exact schema are configurable defaults and are not production blockers; the canonical contract must remain versioned.
- **Assumption A7 (JSON envelope and schema):** The baseline envelope contains `schemaVersion`, `requestId`, `sourceSystem`, `eventType` or `ruleKey`, `companyId`, `branchId`, `valueDate`, optional `postingDate`, optional `currencyCode`, optional `description`, `initiatingUserId` when a user initiated the process, and a `data` object containing source-specific values used by Cashflow Master. The request identifier is a GUID or equivalent globally unique value. The exact JSON names, required fields, maximum nesting, schema registry, and version-compatibility policy remain configurable contract details.
- **Assumption A8 (JSON size and content):** The baseline maximum raw JSON payload is 1 MB and JSON is UTF-8. Unknown top-level fields are retained only when permitted by the approved schema policy. Payloads do not contain executable content, credentials, or unnecessary sensitive personal information. Approved configuration may change the size and unknown-field policy.
- **Assumption A9 (Statuses):** The proposed inbox lifecycle is `Ready`, `Processing`, `RetryPending`, `Posted`, `DeadLetter`, and `Cancelled`. `Processing` carries a worker identity and lease expiry. Cancellation behavior and any final journal status vocabulary remain implementation contract details.
- **Assumption A10 (Retry policy):** The baseline transient-failure policy permits up to 3 total attempts with delays of 1 minute, 5 minutes, and 30 minutes. Permanent validation, schema, accounting-balance, authorization, or missing-rule failures go directly to dead letter. Approved configuration may override the retry limits and delays without weakening failure classification or data-integrity requirements.
- **Assumption A11 (Dead-letter recovery):** Authorized operations personnel may inspect a dead-letter record and request controlled reprocessing. Reprocessing retains the original request identifier and full attempt history, creates no replacement source request, and re-enters the normal idempotent flow. The approval role, whether payload correction is permitted, and the reprocess command contract remain to be confirmed.
- **Assumption A12 (Claim lease and deadline):** The baseline claim lease and processing deadline are both 10 minutes. An uncompleted `Processing` record becomes eligible for reclaim after lease expiry. Approved configuration may override the duration; heartbeat behavior and the exact recovery command remain to be confirmed.
- **Assumption A13 (Idempotency key):** `requestId` is the primary idempotency key and is unique across the configured company scope. A source request with the same key and equivalent immutable payload is treated as the same work item. A reused key with a different payload is rejected and placed in dead letter without changing the original request.
- **Assumption A14 (Statuses on the journal):** A successful engine run creates a final posted journal entry. Draft, parked, or partially posted journals are not produced by this feature. The canonical journal status vocabulary remains to be confirmed with the Journal Writer contract.
- **Assumption A15 (Accounting invariants):** A voucher contains at least two lines unless the approved accounting contract explicitly permits a single-line zero-net posting. Every line has a positive amount rounded to two decimals using round-half-up for the baseline INR configuration, exactly one of debit or credit, and a valid GL account. Total debits equal total credits to the smallest configured currency unit. The minimum line count and any non-INR precision remain contract details.
- **Assumption A16 (Company, branch, and currency scope):** Each voucher belongs to one company and one branch. All lines in a voucher use one accounting currency. The baseline currency is INR with two-decimal precision. Cross-company, cross-branch, multi-currency, foreign-exchange, and inter-branch clearing behavior are out of scope until separately approved. The existing GL `CurrencyId` contract remains authoritative for account compatibility.
- **Assumption A17 (Period and date handling):** `valueDate` is required and is interpreted in the baseline Business Timezone of Asia/Kolkata. `postingDate` defaults to `valueDate` when omitted. The engine resolves the accounting period from the company fiscal calendar, proposed as the Indian financial year from 1 April through 31 March, and rejects closed or invalid periods. The fiscal-calendar source, future-date policy, and back-dated posting policy remain to be confirmed.
- **Assumption A18 (Reversals):** The engine does not create automatic reversals. A reversal is a new source request that references the original journal and is evaluated by an approved Cashflow Master reversal rule. Automatic or scheduled reversals require a separately approved contract.
- **Assumption A19 (Audit retention):** Inbox records, journal links, status transitions, processing attempts, dead-letter details, and immutable audit entries are retained for the platform-approved financial-record retention period. The retention duration, archival mechanism, and purge authority use the platform-approved defaults and remain to be confirmed where the platform has not already defined them.
- **Assumption A20 (Operational surface and UI exclusion):** A protected operational API or equivalent command interface is required for queue status, request inspection, stale-record recovery, failed-record investigation, and dead-letter reprocessing. Finnova-UI work is out of scope. A browser journal viewer and a browser JVE admin screen are excluded from this feature; a separate Journal Viewing UI will be developed later.
- **Assumption A21 (Security model):** Source submission, operational inspection, and dead-letter recovery use separate least-privilege permissions. The Python worker uses a database/service identity that can claim inbox rows and write approved journal/audit records but cannot alter master definitions or authentication data. The worker resolves the posting actor from the approved request context or seeded SystemUser configuration and cannot invent a user identity. Role names, JWT boundary, database principals, and SystemUser seed/configuration details remain to be confirmed.
- **Assumption A22 (SystemUser seed and actor resolution):** A seeded, configured `SystemUser` identity exists for automation jobs. A user-initiated process supplies `InitiatingUserId`, and the resolved posting actor is that user's `UserId`. An automation-initiated process resolves the configured seeded `SystemUser`. The worker must pass the resolved actor to the Journal Writer as `PostingUserId`/`PostedByUserId` according to the canonical schema and must fail closed when the actor is missing, invalid, or ambiguous. The exact seed identifier and canonical column mapping remain to be confirmed.
- **Assumption A23 (Failure visibility):** The engine emits structured logs, metrics, and correlation identifiers to the platform-approved observability system. Baseline platform-approved security, retention, and observability defaults apply; alert thresholds, dashboard ownership, and notification channels remain to be confirmed where not already defined.
- **Note (Localization):** All JSON labels, operational messages, status labels, error messages, and reference values are English. No Arabic or other non-English values and no RTL presentation are introduced.

## Glossary

- **JV (Journal Voucher):** A balanced accounting instruction containing a header and one or more debit and credit journal lines.
- **JV Posting Engine (JVE):** The Python worker that claims eligible posting requests, deserializes and validates payloads, resolves Cashflow Master rules, creates a journal, and records the processing outcome.
- **JV Posting Inbox:** The feature-owned SQL Server table containing source-submitted posting requests and their durable lifecycle state.
- **Posting Request:** One immutable source instruction in the JV Posting Inbox, identified by a unique request identifier and carrying a JSON payload. A Posting Request also records the initiating actor context required to resolve the journal posting actor.
- **Initiating User:** The authenticated person whose action through the approved .NET API starts a user-initiated posting process. The person's `UserId` is captured as `InitiatingUserId`.
- **Posting User / Posted-By User:** The resolved identity that the Journal Writer receives and persists as `PostingUserId` or `PostedByUserId` according to the canonical journal schema. For a user-initiated process, the Posting User is the Initiating User. For an automation job, the Posting User is the configured seeded SystemUser.
- **SystemUser:** A seeded, configured service identity used as the posting actor for approved automation jobs. The JVE must resolve the configured SystemUser and must not invent or substitute an identity.
- **Source System:** A Finnova module or approved external integration that submits a Posting Request for journal processing.
- **Ready:** The proposed inbox status indicating that a Posting Request is complete and eligible for claiming by the JVE.
- **Processing:** The proposed inbox status indicating that a worker has claimed a Posting Request under a lease.
- **Retry Pending:** The proposed inbox status indicating that a transient failure occurred and the request is eligible after its next retry time.
- **Posted:** The proposed inbox status indicating that the request produced one final journal entry and the posting outcome is durable.
- **Dead Letter:** A terminal failure state for a request that cannot be processed under the approved retry and validation policy. Dead-letter data includes the original payload and failure history.
- **Claim:** An atomic database operation that changes an eligible request to Processing and assigns a worker identity and lease expiry so another worker cannot process the same request concurrently.
- **Lease:** A time-bounded ownership period for a claimed request. An expired lease can be reclaimed according to the approved recovery policy.
- **Idempotency Key:** The stable identifier used to ensure that repeated delivery of one source request produces at most one journal entry. The baseline key is `requestId`.
- **Cashflow Master:** The future authoritative master that defines transaction/event posting rules, account mappings, dimensions, effective dates, and reversal behavior consumed by the JVE.
- **Cashflow Posting Rule:** A versioned Cashflow Master definition that resolves a source event and context into the journal lines and controls required for posting.
- **Journal Header:** The canonical journal record containing voucher identity, company, branch, dates, currency, source reference, narration, rule version, resolved posting actor, and posting metadata.
- **Journal Line:** A canonical journal detail record containing a GL account, debit or credit amount, currency, and applicable cost-centre, profit-centre, and sub-ledger references.
- **Journal Writer:** The approved same-database persistence boundary that creates a Journal Header, its Journal Lines, and the canonical source link in the active SQL transaction, receives the resolved Posting User, persists the actor according to the canonical schema, and returns the canonical journal identifier.
- **Canonical Journal Source Link:** The durable canonical relationship between a Posting Request and its final journal, including the idempotency key and source metadata required for reconciliation.
- **Approved .NET Submission API:** The protected API boundary that authenticates the caller, creates or submits the JV Posting Inbox request, captures actor context, and marks the request eligible. The API does not perform journal posting.
- **Baseline Configuration:** The approved default values for runtime and processing policy, including Python 3.11, one-minute CRON cadence, batch size 50, 10-minute lease and deadline, 1/5/30-minute retry delays, Asia/Kolkata timezone, INR, and two-decimal round-half-up precision. Approved configuration may override baseline values.
- **Posting Attempt:** An immutable record of one worker attempt, including start/end timestamps, worker identity, status outcome, error class, error code, and safe error details.
- **Audit Entry:** An immutable record of a material request lifecycle or journal-posting event, including actor or worker identity, correlation data, before/after state, and UTC timestamp.
- **Permanent Failure:** A failure that cannot be corrected by retrying the same payload, such as invalid JSON, an unsupported schema, an unbalanced voucher, an invalid account, or an unavailable Cashflow rule.
- **Transient Failure:** A failure that may succeed later without changing the source payload, such as a temporary database lock, network timeout, or dependency outage.
- **Accounting Period:** The company-controlled period to which a journal is posted, derived from the approved fiscal calendar and posting date.
- **Business Timezone:** The timezone used to interpret value dates, posting dates, period boundaries, and CRON business scheduling. The proposed default is Asia/Kolkata.
- **System Journal:** A journal generated by an approved source process through the JVE rather than manually entered by an end user.
- **Reversal:** A new journal instruction that reverses a previously posted journal under an approved business rule; a reversal is not an update to the original journal.
- **Operations User:** An authenticated and authorized user or service principal permitted to inspect processing outcomes or recover dead-letter requests.
- **JVE_Service:** The protected operational boundary exposing queue status, request inspection, stale-record recovery, and dead-letter recovery operations for the JVE. The worker itself remains a Python CRON process.
- **JVE_Admin_UI:** A browser-based React/TypeScript/MUI operational screen. JVE_Admin_UI is explicitly excluded from this feature; Finnova-UI work and journal viewing are deferred to separate work.
- **EARS:** Easy Approach to Requirements Syntax, using Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, and Complex patterns.

## Requirements

### Requirement 1: Receive and Persist JV Posting Requests

**User Story:** As a Source System, I want to submit the data required for a journal posting into a durable inbox, so that the JVE can process the request asynchronously without losing the source instruction.

#### Acceptance Criteria

1. WHEN an approved caller submits a Posting Request through the Approved .NET Submission API with a unique request identifier, syntactically valid JSON payload, source system identifier, approved initial processing state, and actor context, THE JV Posting Inbox SHALL persist the request with its raw JSON payload, schema version, received timestamp in UTC, immutable request identifier, and captured initiating actor context.
2. WHEN a Posting Request is accepted by the JV Posting Inbox, THE JV Posting Inbox SHALL store the source system, event or rule key, company identifier, branch identifier, value date, optional posting date, optional currency code, correlation identifier, and `InitiatingUserId` separately from the raw JSON when those fields are present in the approved envelope.
3. WHERE a Posting Request omits the posting date, THE JV Posting Inbox SHALL represent the posting date as absent and SHALL allow the JVE to apply the approved posting-date default during validation.
4. WHEN a submission uses a request identifier that already exists and has an equivalent immutable payload, THE JV Posting Inbox SHALL return the existing request identity and SHALL NOT create a second eligible work item.
5. IF a submission uses a request identifier that already exists with a different immutable payload, THEN THE JV Posting Inbox SHALL reject the submission, SHALL preserve the original request unchanged, and SHALL record a security or integrity event for the conflicting submission.
6. IF a submission omits a required inbox field or supplies an invalid initial state, THEN THE JV Posting Inbox SHALL reject the submission with a field-specific error and SHALL NOT create a processable record.
7. WHILE a persisted Posting Request is within the approved retention period, THE JV Posting Inbox SHALL preserve the original raw JSON payload and SHALL NOT overwrite the payload during normal processing or recovery.
8. WHEN the Approved .NET Submission API accepts a Posting Request, THE Approved .NET Submission API SHALL mark the request eligible for JVE processing and SHALL not parse the business payload, resolve Cashflow rules, validate the voucher, or post a canonical journal.
9. IF a caller or Source System attempts to write a canonical Journal Header, Journal Line, or Canonical Journal Source Link directly instead of submitting through the Approved .NET Submission API, THEN the platform SHALL reject the write and SHALL preserve the canonical journal records unchanged.

### Requirement 2: Deserialize and Version the JSON Envelope

**User Story:** As the JV Posting Engine, I want a versioned and validated JSON envelope, so that source systems can provide flexible business data while the engine maintains a stable processing contract.

#### Acceptance Criteria

1. WHEN the JVE claims a Posting Request whose raw payload is valid UTF-8 JSON and declares a supported schema version, THE JVE SHALL deserialize the payload according to that schema version before resolving a Cashflow Posting Rule.
2. WHEN a supported payload contains the baseline envelope, THE JVE SHALL make `schemaVersion`, `requestId`, `sourceSystem`, at least one of `eventType` or `ruleKey`, `companyId`, `branchId`, `valueDate`, the `data` object, and `initiatingUserId` when supplied for a user-initiated process available to rule resolution, actor resolution, and validation.
3. WHEN a supported payload contains optional baseline envelope fields, THE JVE SHALL make `postingDate`, `currencyCode`, and `description` available to rule resolution and validation when those fields are present.
4. IF the raw payload cannot be decoded as UTF-8 or is not valid JSON, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record a schema-validation error, and SHALL not invoke the Journal Writer.
5. IF the raw payload omits a required baseline envelope field or supplies a field with a value that does not conform to the registered schema version, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record a field-specific schema-validation error, and SHALL not invoke the Journal Writer.
6. IF the raw payload omits `schemaVersion` or declares a schema version that is not supported, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record the declared version when present and an unsupported-or-missing-version error, and SHALL not invoke the Journal Writer.
7. IF the raw payload exceeds 1 megabyte or contains a credential or executable-content field prohibited by the approved schema and content policy, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record a payload-policy error, and SHALL not invoke the Journal Writer.
8. WHEN a supported schema version is deserialized successfully, THE JVE SHALL preserve that schema version on the posting attempt, journal source metadata, and audit trail.
9. WHERE the approved schema policy permits forward-compatible fields, THE JVE SHALL retain permitted unknown fields in the original payload and SHALL exclude those fields from rule evaluation until a schema version defines their meaning.

### Requirement 3: Validate Posting Requests and Resolve Cashflow Rules

**User Story:** As a Financial Controller, I want every posting request checked against master data and Cashflow rules, so that only authorized and financially meaningful instructions reach the journal.

#### Acceptance Criteria

1. WHEN the JVE receives a structurally valid Posting Request, THE JVE SHALL evaluate Cashflow Posting Rule applicability using the event or rule key, company context, branch context, and effective value date, and SHALL select a rule only when exactly one active rule is applicable.
2. WHEN Cashflow Master returns exactly one applicable active Cashflow Posting Rule, THE JVE SHALL use that rule's version, account mappings, required dimensions, currency restrictions, and debit or credit derivation as the authoritative posting instructions.
3. IF Cashflow Master returns no active rule for the supplied event or rule key, company context, branch context, and effective value date, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record a missing-rule error identifying the event or rule key and value date, and SHALL not create a journal.
4. IF Cashflow Master returns more than one applicable active rule for the supplied event or rule key, company context, branch context, and effective value date, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL record a rule-ambiguity error, and SHALL not create a journal.
5. IF Cashflow Master is unavailable or does not respond within the configured integration timeout, THEN THE JVE SHALL classify the failure as transient, SHALL apply the approved retry policy, and SHALL preserve the raw Posting Request payload without modification.
6. IF a Posting Request omits a required company identifier, branch identifier, value date, source system, event or rule key, or request identifier, or supplies a value that fails the validation rule for any of those fields, THEN THE JVE SHALL classify the request as a permanent failure, SHALL identify every invalid or missing field, and SHALL not invoke the Journal Writer.
7. IF a Posting Request identifies a company or branch outside the JVE's authorized processing scope, THEN THE JVE SHALL reject the request as an authorization failure, SHALL preserve the request's failure state and audit information, and SHALL not invoke the Journal Writer.
8. WHEN the JVE prepares a voucher from a resolved Cashflow Posting Rule, THE JVE SHALL validate every resolved GL account and required dimension against the active GL Master and SHALL apply the approved System Journal or Manual Journal validation mode.
9. IF any resolved GL account is missing, inactive, outside its effective date range, incompatible with the voucher currency, or missing a required cost-centre, profit-centre, or sub-ledger reference, THEN THE JVE SHALL classify the Posting Request as a permanent failure, SHALL identify each failed account or dimension validation, and SHALL not create a journal.
10. THE JVE SHALL use the versioned, configurable Cashflow Posting Rule adapter as the authoritative rule boundary, SHALL not maintain a separately administered duplicate of any Cashflow Posting Rule, and SHALL not hard-code event-to-accounting posting rules.
11. IF the versioned Cashflow Posting Rule adapter returns no applicable rule, THEN THE JVE SHALL fail closed, classify the request as a permanent failure, and SHALL not infer or create a posting rule from the source payload.

### Requirement 4: Construct Balanced Journal Vouchers

**User Story:** As a Financial Controller, I want each generated voucher to obey double-entry accounting rules, so that the ledger remains mathematically balanced and auditable.

#### Acceptance Criteria

1. WHEN the JVE constructs a voucher from a valid Cashflow Posting Rule and the approved accounting contract does not permit a single-line zero-net posting, THE JVE SHALL create at least two Journal Lines.
2. WHEN the JVE constructs a voucher from a valid Cashflow Posting Rule and the approved accounting contract permits a single-line zero-net posting, THE JVE SHALL create one Journal Line whose net amount is zero.
3. WHEN the JVE constructs a Journal Line, THE JVE SHALL assign exactly one positive debit amount or one positive credit amount, and THE JVE SHALL represent the other amount as zero or absent according to the canonical Journal Writer contract.
4. WHEN the JVE constructs a voucher in one accounting currency, THE JVE SHALL round every monetary amount to the configured smallest supported currency unit before comparing debit and credit totals.
5. WHEN the JVE completes voucher construction, THE JVE SHALL require the debit total to equal the credit total to the configured smallest supported currency unit.
6. WHEN a voucher passes structural validation, THE JVE SHALL preserve deterministic Journal Line ordering based on the Cashflow Posting Rule and source line identifiers.
7. IF a voucher contains no debit amount or no credit amount, THEN THE JVE SHALL reject the voucher as a permanent accounting-validation failure and SHALL not create a journal.
8. IF a voucher's debit total differs from its credit total after rounding to the configured smallest supported currency unit, THEN THE JVE SHALL reject the voucher as a permanent accounting-validation failure, SHALL record the debit total, credit total, and difference, and SHALL not create a journal.
9. IF a voucher contains a non-positive amount, more than one non-zero amount side on a Journal Line, a duplicate line identifier, or an amount with decimal precision that is invalid for the accounting currency, THEN THE JVE SHALL reject the voucher as a permanent accounting-validation failure and SHALL not create a journal.
10. IF the JVE cannot construct a complete voucher from the Cashflow Posting Rule and request data, THEN THE JVE SHALL classify the request as a permanent failure, SHALL preserve the source payload for investigation, and SHALL not create a journal.

### Requirement 5: Apply Company, Branch, Currency, and Period Rules

**User Story:** As a Financial Controller, I want every journal assigned to the correct organizational and accounting context, so that reports and controls remain consistent.

#### Acceptance Criteria

1. WHEN the JVE accepts a Posting Request, THE JVE SHALL assign the resulting voucher exactly one company identifier and exactly one branch identifier, and each identifier SHALL equal the corresponding value in the approved request context.
2. IF a Posting Request omits its currency code and the resolved Cashflow Posting Rule and GL Master both permit INR, THEN THE JVE SHALL use INR as the voucher's accounting currency.
3. IF a Posting Request specifies a currency other than the approved currency for the voucher or any account line, AND the approved multi-currency contract does not explicitly support that combination, THEN THE JVE SHALL reject the request as a permanent validation failure and SHALL not create a journal.
4. WHEN the JVE evaluates a Posting Request, THE JVE SHALL interpret the value date and posting date in the approved Business Timezone and SHALL derive exactly one Accounting Period corresponding to the posting date in the approved company fiscal calendar.
5. IF a Posting Request omits the posting date and the approved Cashflow Posting Rule does not supply a different permitted posting date, THEN THE JVE SHALL use the value date as the posting date.
6. IF a Posting Request omits the posting date and the approved Cashflow Posting Rule supplies a different permitted posting date, THEN THE JVE SHALL use the rule-supplied date as the posting date.
7. IF a Posting Request contains an invalid calendar date, a posting date earlier than the permitted back-date boundary, a posting date later than the permitted future-date boundary, or a posting date in a closed Accounting Period, THEN THE JVE SHALL reject the request as a permanent failure and SHALL not create a journal.
8. IF the company, branch, period, or currency value differs between any two of the voucher envelope, resolved Cashflow Posting Rule, GL validation result, and Journal Writer request, THEN THE JVE SHALL reject the request as a permanent consistency failure and SHALL not create a journal.
9. THE JVE SHALL use English display values in operational errors and responses.
10. THE JVE SHALL use the approved INR formatting convention for monetary values in operational errors and responses.
11. THE JVE SHALL represent accounting fields with English-only values and SHALL not create bilingual or right-to-left accounting fields.



### Requirement 6: Persist the Journal and Complete the Inbox Atomically

**User Story:** As a Financial Controller, I want a journal and its source request outcome committed together, so that a successful inbox record never lacks its journal and a failed transaction never leaves a partial journal.

#### Acceptance Criteria

1. WHEN a voucher passes all applicable validation rules and the JVE resolves a valid Posting User, THE JVE SHALL persist one Journal Header, all validated Journal Lines, the Canonical Journal Source Link, the source request identifier, the Cashflow rule version, the resolved `PostingUserId` or `PostedByUserId`, and the posting audit record as one atomic SQL Server posting operation.
2. WHEN the atomic SQL transaction or approved stored procedure commits successfully, THE JVE SHALL mark the corresponding Posting Request as Posted and SHALL store its canonical journal identifier and posted timestamp as part of the same operation.
3. IF any required journal, source-link, actor-attribution, audit, or Posting Request write fails before the atomic SQL transaction or approved stored procedure commits, THEN THE JVE SHALL report the posting failure, SHALL leave the Posting Request not Posted, and SHALL leave no journal, source link, actor attribution, or related record from that operation available as persisted data.
4. IF the canonical journal records and the JV Posting Inbox are not available within the same SQL Server transaction or approved stored procedure, THEN THE JVE SHALL prevent production posting until the same-database transaction boundary is restored and verified.
5. WHEN a Posting Request is marked Posted, THE JVE SHALL store the final debit total, final credit total, currency, company, branch, period, Cashflow rule version, resolved `PostingUserId` or `PostedByUserId`, and canonical journal identifier required to reconcile the request with its journal.
6. IF a Posting Request is already linked to a final journal with the same idempotency key, THEN THE JVE SHALL retain or return the Posted outcome with the existing canonical journal identifier and SHALL NOT create another Journal Header or Journal Line set.
7. WHEN the process was initiated by an authenticated user, THE JVE SHALL resolve that person's `UserId` as the Posting User and SHALL pass the resolved identity to the Journal Writer.
8. WHEN the process was initiated by an automation job, THE JVE SHALL resolve the configured seeded SystemUser as the Posting User and SHALL pass that identity to the Journal Writer.
9. IF the JVE cannot resolve exactly one authorized Posting User from the captured `InitiatingUserId` or configured seeded SystemUser, THEN THE JVE SHALL classify the request as a permanent authorization or integrity failure, SHALL not invoke the Journal Writer, and SHALL not mark the request Posted.
10. THE JVE SHALL not invent, substitute, or derive a Posting User from the worker process identity, database principal, CRON account, or an arbitrary default user.



### Requirement 7: Claim Work Safely Under Concurrency

**User Story:** As an Operations User, I want multiple worker invocations to process work safely, so that concurrent CRON executions cannot post one request twice.

#### Acceptance Criteria

1. WHEN a JVE worker selects an eligible Posting Request, THE JVE SHALL atomically change the request status to Processing and record one worker identity, one claim timestamp, and one lease expiry for that claim.
2. WHEN two or more JVE workers attempt to claim the same eligible Posting Request concurrently, THE JV Posting Inbox SHALL grant the claim to no more than one worker and SHALL leave the request with no more than one active claim.
3. WHILE a Posting Request has an unexpired Processing lease, THE JV Posting Inbox SHALL reject claim attempts from every other worker and SHALL preserve the existing worker identity, claim timestamp, and lease expiry.
4. WHEN a Processing lease expires before the request reaches a terminal outcome, THE JVE SHALL make the request eligible for recovery according to the approved lease-recovery policy and SHALL retain the abandoned worker identity and all Posting Attempt history.
5. IF a worker cannot renew the Processing lease before the approved processing deadline, THEN THE JVE SHALL stop creating new journal writes for that request and SHALL release or expire the claim according to the approved recovery policy.
6. IF a worker cannot complete the request before the approved processing deadline, THEN THE JVE SHALL stop creating new journal writes for that request and SHALL release or expire the claim according to the approved recovery policy.
7. WHEN a worker begins processing a claimed Posting Request, THE JVE SHALL create one Posting Attempt linked to the request identifier and SHALL assign the attempt a correlation identifier that remains unchanged for the lifetime of that attempt.
8. WHEN the JVE processes a claimed batch of Posting Requests, THE JVE SHALL process the requests in ascending received-timestamp order and SHALL use the request identifier as the tie-breaker when two requests have the same received timestamp.
9. WHEN the JVE processes the same claimed batch more than once with the same request data and approved ordering policy, THE JVE SHALL select the requests in the same order each time.



### Requirement 8: Guarantee Idempotent and Deterministic Processing

**User Story:** As a Source System, I want safe retries and duplicate delivery handling, so that network failures or repeated CRON runs cannot create duplicate financial postings.

#### Acceptance Criteria

1. WHEN the JVE receives a Posting Request with an idempotency key that is already associated with a final journal, THE JVE SHALL return the existing journal identity or record it in the posting result, and SHALL not create a second journal.
2. WHEN the JVE processes the same immutable request with the same Cashflow rule version, value date, and source context on repeated attempts, THE JVE SHALL produce the same voucher identity decision, line ordering, debit total, credit total, and journal-link outcome for every attempt.
3. IF a request identifier is reused with a changed payload, changed company, changed branch, or changed source context, THEN THE JVE SHALL reject the newer content as an idempotency conflict and SHALL preserve the original posting outcome without modification.
4. WHEN a transient failure occurs before a journal is committed, THE JVE SHALL leave no final journal for that failed attempt and SHALL allow a subsequent attempt using the unchanged source payload to recompute the voucher deterministically.
5. IF processing ends with an uncertain external or database response, THEN THE JVE SHALL check whether the idempotency key or canonical source-to-journal link already identifies a final journal before creating a new journal, and SHALL return or record the existing journal identity when one is found.
6. THE JVE SHALL maintain the invariant that each approved idempotency key and each canonical source-to-journal link is associated with at most one final journal.
7. WHEN the JVE repeats processing for the same immutable request, THE JVE SHALL resolve the same Posting User from the immutable `InitiatingUserId` or the configured seeded SystemUser and SHALL pass the same actor identity to the Journal Writer.
8. IF repeated processing cannot resolve the same Posting User or detects a changed actor context, THEN THE JVE SHALL classify the condition as an idempotency or integrity failure and SHALL not create a second journal.



### Requirement 9: Retry Failures and Manage the Dead-Letter Queue

**User Story:** As an Operations User, I want recoverable failures retried and unrecoverable requests isolated, so that one bad posting does not stop the processing queue or disappear without trace.

#### Acceptance Criteria

1. WHEN a Posting Request fails because of a classified transient failure before a terminal outcome, THE JVE SHALL record a Posting Attempt containing the failure, increment the request attempt count, set the request to Retry Pending, and set its next eligible retry time according to the approved backoff policy.
2. WHILE a Posting Request is in Retry Pending and its next eligible retry time has not arrived, THE JVE SHALL exclude the request from processing claims.
3. WHEN a Posting Request reaches the approved maximum attempt count, THE JVE SHALL move the request to Dead Letter, preserve its original payload, and record the terminal failure reason.
4. WHEN a Posting Request has a classified permanent failure before a journal write, THE JVE SHALL move the request to Dead Letter and SHALL not create or attempt a journal write for that request.
5. WHEN a Posting Request moves to Dead Letter, THE JVE SHALL create a dead-letter record containing the request identifier, raw payload reference, failure category, safe error code, safe error message, attempt count, last worker identity, and UTC timestamps.
6. IF one Posting Request fails during a CRON invocation, THEN THE JVE SHALL record the failure, preserve the failed request according to its failure state, and SHALL continue processing other independently claimable requests in that invocation.
7. WHEN an authorized Operations User requests reprocessing of a Dead Letter record, THE JVE SHALL preserve all previous Posting Attempts, create a new recovery attempt, return the request to the approved eligible status, and preserve the original payload unless payload correction is explicitly authorized.
8. IF a Dead Letter reprocess request lacks the required authorization or references a record that is not in Dead Letter, THEN THE JVE SHALL reject the request with an error indication and SHALL leave the referenced record's dead-letter state, payload, and Posting Attempts unchanged.
9. WHEN Dead Letter reprocessing identifies an existing final journal for the same idempotency key, THE JVE SHALL reconcile the request to Posted, record the existing journal identity as the request's journal outcome, and SHALL not create a duplicate journal.
10. THE JVE SHALL not silently discard, overwrite, or permanently delete a failed Posting Request before the approved retention and archival process runs.



### Requirement 10: Record Immutable Audit and Reconciliation Data

**User Story:** As an Auditor, I want every request, attempt, rule resolution, journal link, and recovery action traceable, so that financial postings can be reconciled to their source and processing history.

#### Acceptance Criteria

1. WHEN a Posting Request is received, claimed, retried, posted, cancelled, or moved to Dead Letter, THE JVE SHALL append one immutable lifecycle audit entry containing the request identifier, prior state or an indication that no prior state existed, new state, resolved actor or worker identity, correlation identifier, and UTC timestamp.
2. WHEN the JVE completes posting a voucher, THE JVE SHALL append a reconciliation record containing the source request identifier, source system, Cashflow rule key, Cashflow rule version, company, branch, value date, posting date, Accounting Period, currency, debit total, credit total, `InitiatingUserId` when present, resolved `PostingUserId` or `PostedByUserId`, and canonical journal identifier.
3. WHEN a Posting Attempt fails, THE JVE SHALL append an attempt outcome containing a stable error category and code, attempt number, worker identity, a dependency name when a dependency caused the failure, and a diagnostic message that excludes secrets, credentials, raw authentication tokens, and unnecessary personal information.
4. WHEN an authorized Operations User successfully reprocesses or cancels a Dead Letter record, THE JVE SHALL append a recovery audit entry containing the authorized user identity, action reason, prior terminal state, new state, and UTC timestamp.
5. THE JVE SHALL retain lifecycle audit entries, reconciliation records, and Posting Attempt outcomes as append-only records, and normal processing operations SHALL neither modify nor delete any recorded historical outcome.
6. WHEN an authorized reconciliation query requests a Posting Request, THE JVE SHALL return its current state, every recorded Posting Attempt outcome, its canonical journal identifier when it is Posted, and its final failure information when it is Dead Lettered.
7. IF a Posting Request is Posted without a journal identifier or without a corresponding journal source link, THEN the reconciliation surface SHALL report an operational integrity alert that identifies the request and the missing journal information, and SHALL leave the Posting Request in its Posted state.



### Requirement 11: Enforce Security and Access Boundaries

**User Story:** As a Security Stakeholder, I want submission, processing, and recovery privileges separated, so that financial postings cannot be altered by an untrusted caller or worker.

#### Acceptance Criteria

1. IF an unauthenticated caller or a caller without the required privilege attempts to submit through the Approved .NET Submission API, inspect through the JVE_Service, cancel, or reprocess a Posting Request through a protected Finnova boundary, THEN the platform SHALL reject the operation with an authentication or authorization failure indication and SHALL not create or modify a Posting Request, Posting Attempt, journal, journal link, actor attribution, or other financial record.
2. THE JVE worker SHALL use a distinct least-privilege service identity to authenticate to SQL Server and required dependency boundaries, with credentials supplied through the approved secret mechanism and excluded from source code, persisted business records, logs, and user-visible responses.
3. THE JVE worker identity SHALL be permitted to claim eligible inbox work, read the required master and versioned Cashflow/GL adapter data, read the configured seeded SystemUser identity needed for actor resolution, create approved journal records through the Journal Writer, write audit records, and update permitted inbox statuses, and SHALL not be permitted to modify GL Master, Cashflow Master, user identities, SystemUser seed/configuration, or authentication configuration.
4. IF the JVE worker cannot authenticate to SQL Server or a required dependency, THEN THE JVE SHALL not create or modify a journal or other financial record and SHALL expose only a safe failure indication that excludes credentials and authentication tokens.
5. WHEN the JVE emits a log, metric, error, or dead-letter diagnostic, THE JVE SHALL exclude secrets, credentials, raw authentication tokens, raw request payloads, customer identity numbers, contact details, and financial account details from the observable message.
6. IF a submitted Posting Request contains a company or branch that is not included in the company and branch scope assigned to the caller or source system, THEN the submission boundary SHALL reject the request with an authorization failure indication and SHALL not make the request eligible for processing.
7. WHEN an Operations User requests to read a Posting Request, THE operational surface SHALL allow the request only when the user has the read privilege and the requested company and branch are within the user's assigned scope.
8. WHEN an Operations User requests to cancel, recover, or reprocess a Posting Request, THE operational surface SHALL allow the request only when the user has the corresponding action privilege and the requested company and branch are within the user's assigned scope.
9. IF an Operations User lacks the required read, cancel, recovery, or reprocessing privilege, or the requested company or branch is outside the user's assigned scope, THEN the operational surface SHALL reject the operation with an authorization failure indication and SHALL leave the Posting Request state and financial records unchanged.
10. THE JVE feature SHALL use English-only labels and messages and SHALL not add bilingual fields, secondary-language values, or right-to-left presentation for security or operational information.



### Requirement 12: Provide Operational Monitoring and Recovery

**User Story:** As an Operations User, I want to monitor queue health and recover stuck or failed records, so that the scheduled engine can be operated safely in production.

#### Acceptance Criteria

1. WHEN an authorized Operations User requests queue status for a permitted company scope, THE JVE_Service SHALL return a non-negative count and the oldest-record age for each of Ready, Processing, Retry Pending, Posted, and Dead Letter, and SHALL return no oldest-record age when the corresponding count is zero.
2. WHEN an authorized Operations User requests a Posting Request by identifier, THE JVE_Service SHALL return its identifier, source metadata, company and branch scope, current state, UTC creation and update timestamps, lease details when a lease exists, every recorded attempt outcome, safe failure details when a failure exists, and the journal identifier when the request is Posted.
3. WHEN an authorized Operations User requests a Processing record whose lease has expired, THE JVE_Service SHALL return the worker identity, UTC claim time, UTC lease-expiry time, and the most recent UTC heartbeat or progress time when that value exists.
4. IF an authorized Operations User requests recovery of a Processing record with an expired lease, THEN THE JVE_Service SHALL perform the approved claim-recovery action, preserve the expired claim and its attempt history, and reject any concurrent recovery that does not win the single recovery decision without changing the record twice.
5. IF a CRON invocation cannot reach SQL Server or an approved Cashflow or GL dependency, THEN THE JVE SHALL emit one structured failure event identifying the unavailable dependency, the invocation, the UTC occurrence time, and the failure category, and SHALL leave every unclaimed Posting Request eligible for a later invocation.
6. THE JVE_Service SHALL expose only operational details permitted for the requesting Operations User, SHALL redact credentials, authentication tokens, and sensitive payload values by default, and SHALL expose an unredacted sensitive value only when the requesting user has the explicitly approved permission for that value.
7. WHERE the operational API or equivalent command interface is used, WHEN an authorized Operations User requests queue status, request detail, stale-record inspection, or recovery, THE JVE_Service SHALL return only the permitted operational information and SHALL not provide browser journal viewing or browser-based JVE administration.
8. THE feature SHALL provide a protected operational API or equivalent command interface for queue status, request detail, stale-record inspection, and authorized recovery, and SHALL not add a React/MUI page, browser journal viewer, or other Finnova-UI work to this feature.

### Requirement 13: Emit Observability Signals and Alerts

**User Story:** As a Platform Operator, I want reliable processing signals, so that latency, failures, stuck claims, and accounting-integrity issues are detected before they affect operations.

#### Acceptance Criteria

1. WHEN a CRON invocation starts, THE JVE SHALL emit exactly one structured invocation-start log containing the worker identity, invocation correlation identifier, and UTC start timestamp.
2. WHEN a CRON invocation ends, THE JVE SHALL emit exactly one structured invocation-end log containing the worker identity, the same invocation correlation identifier as the invocation-start log, UTC start and end timestamps, and non-negative integer counts for claimed, posted, retried, dead-lettered, and failed requests.
3. WHEN the JVE reaches a Posting Request processing milestone of claim, validation, rule resolution, journal persistence, or final outcome, THE JVE SHALL emit a structured signal containing the Posting Request identifier, invocation correlation identifier, milestone name, and UTC timestamp.
4. WHEN the JVE performs a recovery action for a Posting Request, THE JVE SHALL emit a structured recovery signal containing the Posting Request identifier, the action name, the associated invocation correlation identifier when one exists, and a UTC timestamp.
5. WHEN a CRON invocation ends, THE JVE SHALL publish numeric metrics for queue depth in each of the Ready, Processing, Retry Pending, Posted, and Dead Letter states; oldest eligible request age; processing duration; attempt count; Cashflow rule failures; GL validation failures; journal-posting failures; duplicate deliveries; and dead-letter count.
6. IF an approved threshold for stale Processing records, queue age, repeated dependency failure, dead-letter growth, or debit-credit inconsistency is exceeded, THEN the observability boundary SHALL emit an alert signal identifying the exceeded threshold category, the affected company, branch, Posting Request, or invocation scope when applicable, and the correlation identifier associated with the triggering signal when one exists.
7. WHEN the JVE emits an error signal, THE JVE SHALL include a category and code that identify exactly one of the following failure classes: schema, validation, authorization, dependency, persistence, concurrency, idempotency, or accounting-integrity; repeated emissions for the same failure classification SHALL use the same category and code.
8. WHEN the JVE persists a technical observability signal, THE JVE SHALL record its technical timestamp in UTC.
9. WHEN the JVE presents calendar or accounting-period context in an observability signal, THE JVE SHALL express that context using the approved Business Timezone.



### Requirement 14: Handle Reversals and Corrections as New Requests

**User Story:** As a Financial Controller, I want corrections to preserve the original journal history, so that posted accounting records are not silently mutated.

#### Acceptance Criteria

1. WHEN a source system submits a reversal request with a request identifier that has not been used by any prior Posting Request and with an explicit reference to the original journal or Posting Request, THE JVE SHALL process the reversal as a separate Posting Request and SHALL leave the original journal and Posting Request unchanged.
2. IF a reversal request omits the original journal or Posting Request reference, or uses a request identifier already assigned to another Posting Request, THEN THE JVE SHALL reject the reversal request, SHALL indicate that the request is invalid, and SHALL not create a reversal journal or alter the original journal or Posting Request.
3. WHEN the approved Cashflow Posting Rule permits a reversal and the referenced original journal is Posted and has not already been reversed under that rule, THE JVE SHALL validate the reversal as a new voucher whose total debits equal its total credits and SHALL link the resulting journal to the original journal in the reconciliation data.
4. IF the approved Cashflow Posting Rule does not permit the reversal, or if the referenced original journal does not exist, is not Posted, or has already been reversed under that rule, THEN THE JVE SHALL classify the reversal request as a permanent failure, SHALL indicate the reason for the failure, SHALL not create a reversal journal, and SHALL leave the original journal and Posting Request unchanged.
5. IF a source system attempts to modify or delete a Posted journal through the JVE, THEN THE JVE SHALL reject the operation, SHALL indicate that Posted journals cannot be modified or deleted, and SHALL preserve the original journal and Posting Request unchanged.
6. IF automatic or scheduled reversal processing is requested without the separately approved Cashflow Master reversal contract, THEN THE JVE SHALL reject the reversal request, SHALL classify it as a permanent failure, and SHALL not infer reversal behavior from the source payload or create a reversal journal.
7. WHERE automatic or scheduled reversals are enabled by a separately approved Cashflow Master reversal contract, WHEN an automatic or scheduled reversal becomes due, THE JVE SHALL apply only the reversal behavior defined by that contract and SHALL create the reversal as a new Posting Request.



### Requirement 15: Support Schema Evolution, Retention, and Data Integrity

**User Story:** As a Platform Owner, I want the JV posting data model to evolve safely, so that historical financial records remain readable and processing contracts remain controlled.

#### Acceptance Criteria

1. THE JV Posting Inbox SHALL store a schema version, created timestamp, updated timestamp, immutable request identifier, and immutable captured `InitiatingUserId` when a user initiated the process for every Posting Request.
2. IF a request identifier, created timestamp, schema version, or captured `InitiatingUserId` is changed after a Posting Request is created, THEN THE JVE SHALL reject the change and SHALL leave the Posting Request and its audit history unchanged.
3. IF a Posting Request uses a schema version that is not explicitly registered for processing, THEN THE JVE SHALL reject the request before processing and SHALL report that the schema version is unsupported.
4. THE JVE SHALL preserve the schema version used by every Posting Request that reaches Completed, Retried, or Dead Letter status.
5. WHEN a database schema or contract migration is introduced, THE feature SHALL keep every retained Posting Request readable and reconcilable with its audit history; a successfully posted request SHALL remain linked to its canonical journal identifier and resolved `PostingUserId` or `PostedByUserId`, and a request without a canonical journal SHALL not be assigned a fabricated identifier.
6. IF a retention or archival operation targets a Posted or Dead Letter record before the approved retention period expires, THEN THE retention boundary SHALL reject the operation and SHALL leave the targeted record, its actor attribution, and its audit history unchanged.
7. THE feature-owned records SHALL use only status values from the approved status vocabulary, and each request's attempt numbers SHALL be positive and strictly increasing for successive attempts.
8. IF an operation would create a duplicate request identifier or a second canonical source-to-journal link for a successfully posted request, THEN THE feature SHALL reject the operation and SHALL preserve the existing record, link, and audit history.
9. WHEN an operational lookup requests eligible work, idempotency data, status and next-retry data, journal links, or audit and reconciliation data, THE feature SHALL return all records matching the lookup criteria within the approved operational performance target.
10. WHEN an integrity check detects an orphaned journal line, a duplicate source link, a status without a matching attempt, or an unbalanced canonical journal, THE operational surface SHALL report the detected inconsistency and SHALL not modify or repair financial data automatically.



## Stakeholder Confirmation Checklist

The following decisions remain genuinely open and should be confirmed before design and implementation:

1. Exact canonical Journal Header, Journal Line, and source-link table names, column names, status values, and field mappings, including the canonical mapping for `InitiatingUserId`, `PostingUserId`, and `PostedByUserId`.
2. The seeded SystemUser identifier, seed/migration ownership, activation rules, and configuration source used for automation jobs.
3. The exact JSON envelope field names, registered schema versions, schema registry ownership, maximum nesting, forward-compatibility policy, and payload content policy.
4. The Cashflow Master and GL adapter transport, request/response schemas, effective-date behavior, required dimensions, rule-version contract, timeout values, and dependency failure classification. The adapters must remain versioned and configurable and must not create a duplicate Cashflow rules table.
5. Inbox status vocabulary details, cancellation behavior, final canonical journal status, and the exact stale-claim recovery and heartbeat command contract.
6. Dead-letter reprocessing authorization, checker approval requirements, whether payload correction is allowed, and the exact protected command/API contract.
7. Minimum voucher lines, single-line zero-net policy, non-INR precision, debit/credit tolerance, and the final canonical representation of zero or absent debit/credit amounts.
8. Company and branch scope, fiscal-calendar source, closed-period behavior, future-date and back-date rules, and any separately approved multi-currency or reversal contract.
9. Platform-approved retention, archival and purge controls, operational roles, database principals, observability alert thresholds, dashboard ownership, and notification channels where those platform defaults are not already defined.

The following decisions are confirmed baseline scope and are not open checklist items: Python 3.11; one-minute CRON cadence; batch size 50; 10-minute claim lease and processing deadline; retry delays of 1, 5, and 30 minutes with up to 3 attempts; Asia/Kolkata timezone; INR with two-decimal round-half-up precision; same-database SQL transaction or approved stored procedure; the .NET API-only submission boundary; Python worker posting; seeded SystemUser/user actor resolution; protected operational API or command surface; and exclusion of Finnova-UI, browser journal viewing, and a browser JVE admin screen.
