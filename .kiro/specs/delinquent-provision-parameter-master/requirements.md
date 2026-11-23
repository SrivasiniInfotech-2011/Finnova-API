# Requirements Document

## Introduction

FINNOVA-15 introduces the **Delinquent Provision Rate & Parameter Master** in the existing SystemAdmin domain. The feature lets an authorized Risk & Policy Administrator maintain English-only, India-specific provision parameter sets for delinquency buckets and provision rates. An administrator can create, modify, and query parameter sets scoped to a Company and either a specific Line of Business (LOB) or the global/All LOB scope.

The feature is configuration master data. It does not appropriate receipts, calculate or post canonical journals, or change Receipt Processing behavior. A future approved consumer, such as Demand Processing or a loan/accounting provision calculation, may call the read-only resolver contract defined by this feature. If a future consumer must create accounting entries, the consumer must cross the existing JVE submission boundary; FINNOVA-15 never writes canonical journals.

The supplied `Delinquent Parameter` screenshot is authoritative for the administrative UI shape. The UI uses the labels `Delinquent Type`, `Line of Business`, `Period Type`, `Active`, `Cut of Month`, `Effective Start Date`, `Effective End Date`, `Ageing Days`, and the provision bucket grid columns `Category`, `Start Date`, `End Date`, `Age of arrears (in Months)`, `Base Provision %`, and `Cash Provision %`. The canonical internal property is `CutOffMonth`; the screenshot label remains `Cut of Month`.

The backend is hosted in the existing `Finnova.SystemAdminService`, reached through `Finnova.ApiGateway`. The frontend is implemented in the separate `E:\Finnova\Finnova-UI\Finnova-UI` repository with React 18, TypeScript, Vite, and Material UI. The feature reuses authoritative Company and Line of Business masters and does not create duplicate Company or LOB records.

## Glossary

- **Provision Parameter Master**: The FINNOVA-15 administrative catalog of delinquency provision parameter sets and ordered bucket rows.
- **Provision Parameter Set**: A versioned configuration for one Company, one Delinquent Type, one LOB scope, one Period Type, an Active state, ageing controls, and ordered provision buckets.
- **Company**: The authoritative Company master record identified by `CompanyId`; FINNOVA-15 stores a reference and does not duplicate Company master data.
- **Line of Business / LOB**: The authoritative Line of Business master record identified by `LineOfBusinessId`. A null LOB reference represents the global/All LOB scope.
- **Delinquent Type**: The provision regime selected by an administrator: `Statutory` or `Company`.
- **Period Type**: The unit for ageing ranges: `Month` or `Days`. `AgeFrom` and `AgeTo` use the selected unit.
- **CutOffMonth**: The canonical internal integer field represented by the UI label `Cut of Month`; it identifies the monthly cut-off day used by an approved ageing consumer.
- **Ageing Days**: A non-negative integer configuration used by an approved consumer when deriving delinquency age; FINNOVA-15 stores and validates the parameter but does not calculate demand or post accounting entries.
- **Provision Bucket**: An ordered row within a Provision Parameter Set containing a category, optional row dates, an age range, and base and cash provision percentages.
- **Category**: The English provision bucket label. India-safe examples are `Standard`, `Special Mention`, `Sub-Standard`, `Doubtful`, and `Loss`; the category catalog is a compatibility gate rather than an assumed new master.
- **Age Range**: The inclusive interval `[AgeFrom, AgeTo]`; `AgeTo = null` represents an open-ended final range only.
- **Effective Date Range**: The calendar interval beginning at `EffectiveStartDate` and ending at `EffectiveEndDate`, inclusive. A null end date means open-ended.
- **Effective Scope**: The combination of Company, Delinquent Type, LOB scope, and effective date range used to determine applicability.
- **Applicable Set**: The one active Provision Parameter Set selected for a Company, Delinquent Type, LOB, and evaluation date.
- **Exact LOB Precedence**: The rule that an active set scoped to the requested LOB wins over an otherwise matching global/All LOB set.
- **Version**: The monotonically increasing integer revision of a Provision Parameter Set. Every successful modification advances Version.
- **Optimistic Concurrency**: The requirement that an update supplies the version/row token read by the administrator; a stale token is rejected without a write.
- **Idempotency Key**: A caller-supplied key that makes a repeated create or update request return the original result when the request fingerprint is identical and reject a conflicting reuse.
- **Audit Entry**: An immutable record of a successful create, modify, activation, or deactivation, including actor, timestamp, version, and before/after snapshots.
- **Provision Parameter Resolver**: The read-only `IProvisionParameterResolver` contract used by approved future consumers to resolve an applicable version and bucket list for an evaluation date.
- **SystemAdmin/Risk & Policy Administrator**: An authenticated actor whose trusted JWT identity and authorization claims satisfy the FINNOVA-15 administration policy. The exact policy/role naming is a prerequisite compatibility gate.
- **JVE Boundary**: The existing journal-voucher-entry submission boundary. FINNOVA-15 has no journal-writing capability and must not bypass this boundary.
- **ProblemDetails**: The platform's stable RFC 7807 error response with a machine-readable `code` extension and English detail.

## Dependencies and Assumptions

- **Assumption A1, host and route**: FINNOVA-15 uses the existing `Finnova.SystemAdminService` and the gateway's SystemAdmin route. The UI may use the established `/api/ua/api/...` alias convention; no new backend host is created.
- **Assumption A2, authoritative references**: Company and Line of Business records already exist in authoritative masters. FINNOVA-15 validates referenced identifiers through existing reference ports/repositories and never creates duplicate Company or LOB rows.
- **Assumption A3, authorization names**: The final role/policy names are `SystemAdmin` and/or `RISK_POLICY_ADMINISTRATOR` only if the compatibility gate confirms them. Until that gate passes, implementation must use the existing platform policy name that represents SystemAdmin/Risk & Policy Administrator access. No broad authenticated-user access is assumed.
- **Assumption A4, global scope representation**: `LineOfBusinessId = null` represents the UI's `All`/global selection. A specific LOB is represented by its authoritative identifier.
- **Assumption A5, effective dating enablement**: Effective dating is enabled when `EffectiveStartDate` or `EffectiveEndDate` is supplied. A non-dated set has both dates null and is treated as open-ended. An end date without a start date is invalid. A dated set requires `EffectiveStartDate <= EffectiveEndDate`.
- **Assumption A6, date boundaries**: Effective date ranges and bucket row date ranges are inclusive. Two ranges overlap when their inclusive intervals share a calendar date. Null end dates are open-ended.
- **Assumption A7, bucket row dates**: Bucket `Start Date` and `End Date` are optional overrides. When both are null, the row inherits the set's effective date range. If a row date is supplied, the row interval must be valid and must be contained by the set interval when the set is dated. This inheritance rule is subject to confirmation.
- **Assumption A8, age boundaries**: `AgeFrom` and `AgeTo` are inclusive integers. `AgeFrom >= 0`, `AgeTo >= AgeFrom` when present, and only the final ordered bucket may have a null `AgeTo`. Thus `[0,30]` and `[15,45]` overlap.
- **Assumption A9, percentages**: Base and cash provision percentages are decimal values from `0` through `100`, inclusive, with at most two decimal places. `CashProvisionPercentage <= BaseProvisionPercentage` is required by default because cash provision cannot exceed base provision; this business rule must be confirmed at the compatibility gate.
- **Assumption A10, CutOffMonth semantics**: `CutOffMonth` is an integer from `1` through `31` representing a calendar cut-off day. The field is required for a dated set and optional for a non-dated set unless the compatibility gate confirms a stricter rule. The UI label remains exactly `Cut of Month`.
- **Assumption A11, ageing semantics**: `AgeingDays` is a non-negative integer, with a proposed upper bound of `36500` days. FINNOVA-15 validates storage input but does not derive DPD, ageing, demand, or accounting postings.
- **Assumption A12, categories**: The five English/India regulatory-style categories in examples are `Standard`, `Special Mention`, `Sub-Standard`, `Doubtful`, and `Loss`. Whether categories are a fixed enum or an existing configurable catalog is a compatibility gate; no non-India, Arabic, bilingual, or translation fields may be added.
- **Assumption A13, active overlap**: At most one active applicable set may exist for Company + Delinquent Type + LOB scope + evaluation date. A non-dated active set occupies the full calendar domain. Inactive sets remain queryable by administrators but cannot be returned by the resolver.
- **Assumption A14, precedence**: A specific LOB set takes precedence over a global/All LOB set. If two exact-Lob or two global sets match the same evaluation date, the resolver fails closed with an ambiguity error rather than selecting by insertion order.
- **Assumption A15, versioning model**: A successful update mutates the logical set, increments `Version`, refreshes its concurrency token, and records a before/after audit snapshot. Prior audit snapshots remain immutable. Full historical set-row snapshots are not exposed as consumer versions unless the compatibility gate requires append-only version rows.
- **Assumption A16, idempotency**: Create and modify accept an `Idempotency-Key` header. Reuse with the same normalized request fingerprint returns the original success response; reuse with a different fingerprint returns a conflict and writes no data. A missing key is a validation error for mutation endpoints.
- **Assumption A17, audit timing**: Successful create and modify, including Active state changes, write exactly one immutable audit entry in the same transaction as the master mutation. Rejected requests write no audit entry.
- **Assumption A18, consumer ownership**: FINNOVA-15 owns the read-only resolver contract and adapter. Demand Processing, loan/accounting provision calculation, or another approved consumer owns usage. Receipt Processing is not a consumer unless a future approved contract explicitly requires it.
- **Assumption A19, JVE boundary**: FINNOVA-15 has no journal entity writes, no receipt appropriation calls, and no direct accounting-posting side effect. Any future accounting consumer must submit through the existing JVE boundary.
- **Assumption A20, localization**: All labels, categories, error messages, examples, seed values, test values, and lookup values are English and India-specific. INR is used wherever an amount is ever introduced. No RTL or translation scaffolding is in scope.
- **Compatibility gates before coding**: Confirm the Company and LOB source names/ports, authorization policy/role names, effective-date and boundary semantics, category catalog ownership, resolver/consumer ownership, and future JVE integration contract. A failed gate blocks implementation rather than silently introducing a parallel convention.

## Requirements

### Requirement 1: Display the Delinquent Parameter administration screen

**User Story:** As a Risk & Policy Administrator, I want a screen that matches the approved Delinquent Parameter layout, so that I can manage provision parameter sets without translating between UI and domain terminology.

#### Acceptance Criteria

1. WHEN an authorized administrator opens the feature, THE Provision Parameter Admin UI SHALL display the title `Delinquent Parameter` and the footer actions `Save`, `Clear`, and `Cancel`.
2. WHEN the form is displayed, THE Provision Parameter Admin UI SHALL display `Delinquent Type` radio options `Statutory` and `Company` with exactly one selected value.
3. WHEN the form is displayed, THE Provision Parameter Admin UI SHALL display a `Line of Business` select containing an `All`/global option and active authoritative LOB values.
4. WHEN the form is displayed, THE Provision Parameter Admin UI SHALL display a `Period Type` select containing `Month` and `Days`, an `Active` checkbox, and a field labeled `Cut of Month` that maps to canonical `CutOffMonth`.
5. WHEN the form is displayed, THE Provision Parameter Admin UI SHALL display `Effective Start Date`, `Effective End Date`, and `Ageing Days`, followed by a bucket grid with columns `Category`, `Start Date`, `End Date`, `Age of arrears (in Months)`, `Base Provision %`, and `Cash Provision %`.
6. WHEN an administrator selects `Clear`, THE Provision Parameter Admin UI SHALL restore the create form to its documented defaults without submitting a request.
7. WHEN an administrator selects `Cancel`, THE Provision Parameter Admin UI SHALL leave persisted data unchanged and return to the configured master navigation destination.
8. **Assumption:** The UI uses a single English layout with no bilingual fields, no Arabic values, and no RTL direction handling.

### Requirement 2: Resolve dependent Company and Line of Business references

**User Story:** As a Risk & Policy Administrator, I want Company and LOB values to come from their authoritative masters, so that provision parameters use valid organizational scopes.

#### Acceptance Criteria

1. WHEN the create or modify screen loads, THE Provision Parameter Admin UI SHALL request active Company and active LOB options from the existing authoritative reference services.
2. WHEN an administrator selects `All` for Line of Business, THE Provision Parameter Admin UI SHALL submit a null `LineOfBusinessId` and SHALL display the selection as global scope.
3. IF a submitted Company identifier does not exist in the authoritative Company master, THEN THE Provision Parameter Service SHALL reject the mutation with a 400 ProblemDetails response and SHALL not persist a set or audit entry.
4. IF a submitted non-null LineOfBusinessId does not exist or is inactive in the authoritative LOB master, THEN THE Provision Parameter Service SHALL reject the mutation with a 400 ProblemDetails response and SHALL not persist a set or audit entry.
5. THE Provision Parameter Service SHALL store CompanyId and optional LineOfBusinessId references and SHALL not create, update, or delete Company or LOB master records.
6. **Assumption:** The authoritative Company and LOB repository/port names and active-state rules are resolved by the compatibility gate before implementation.

### Requirement 3: Create a provision parameter set

**User Story:** As a Risk & Policy Administrator, I want to create a provision parameter set from bucket definitions and rates, so that approved delinquency ranges can be used by future provision consumers.

#### Acceptance Criteria

1. WHEN an authenticated authorized administrator submits a valid create request containing CompanyId, DelinquentType, PeriodType, Active, ageing controls, and at least one valid bucket, THE Provision Parameter Service SHALL persist the set and ordered buckets and SHALL return the generated set identifier, Version, concurrency token, and normalized values.
2. WHEN a create request omits Active, THE Provision Parameter Service SHALL default Active to true.
3. WHEN a valid create request succeeds, THE Provision Parameter Service SHALL preserve bucket order and return each bucket's Category, StartDate, EndDate, AgeFrom, AgeTo, BaseProvisionPercentage, and CashProvisionPercentage.
4. IF a create request contains no buckets, THEN THE Provision Parameter Service SHALL reject the request with a validation ProblemDetails response and SHALL not persist the set.
5. IF a create request contains a missing CompanyId, DelinquentType, PeriodType, AgeingDays, or required effective-date value, THEN THE Provision Parameter Service SHALL reject the request with field-level validation details and SHALL not persist the set.
6. WHEN a create request succeeds, THE Provision Parameter Service SHALL create the set and its buckets in one transaction and SHALL create exactly one immutable create Audit Entry.
7. **Assumption:** The first set Version is `1`; the generated identifier is a GUID; date/time persistence uses UTC where a timestamp is required.

### Requirement 4: Support Statutory and Company provision types

**User Story:** As a Risk & Policy Administrator, I want to distinguish Statutory and Company provision regimes, so that consumers can apply the correct policy.

#### Acceptance Criteria

1. THE Provision Parameter Service SHALL accept exactly the DelinquentType values `Statutory` and `Company`.
2. WHEN a query or resolver request specifies DelinquentType, THE Provision Parameter Service SHALL match the requested value exactly after enum normalization.
3. IF a create or modify request supplies an unsupported DelinquentType value, THEN THE Provision Parameter Service SHALL reject the request with a validation ProblemDetails response and SHALL not persist a set or audit entry.
4. WHEN two sets have otherwise identical Company, LOB scope, and effective dates but different DelinquentType values, THE Provision Parameter Service SHALL treat the sets as separate scopes.

### Requirement 5: Support Company/global and exact LOB scope

**User Story:** As a Risk & Policy Administrator, I want to configure a global Company policy and LOB-specific overrides, so that an exact LOB can override the global policy without duplicating masters.

#### Acceptance Criteria

1. WHEN a set is created with LineOfBusinessId null, THE Provision Parameter Service SHALL store the set as the Company's global/All LOB scope.
2. WHEN a set is created with a LineOfBusinessId, THE Provision Parameter Service SHALL store the exact authoritative LOB reference.
3. WHEN an active resolver request matches both an exact LOB set and a global set for the same Company, DelinquentType, and evaluation date, THE Provision Parameter Resolver SHALL return the exact LOB set.
4. WHEN an active resolver request matches two exact LOB sets or two global sets for the same evaluation date, THE Provision Parameter Resolver SHALL fail closed with an ambiguity error and SHALL not return a bucket list.
5. IF a mutation would create or activate an active set whose effective scope overlaps another active set at the same Company, DelinquentType, and LOB scope, THEN THE Provision Parameter Service SHALL reject the mutation with a conflict response and SHALL not persist an audit entry.
6. THE Provision Parameter Service SHALL not infer a Company or LOB from display text; resolution SHALL use authoritative identifiers.

### Requirement 6: Validate Period Type and ageing controls

**User Story:** As a Risk & Policy Administrator, I want ageing controls validated against their selected period unit, so that bucket ranges have a consistent meaning.

#### Acceptance Criteria

1. WHEN PeriodType is `Month`, THE Provision Parameter Service SHALL interpret AgeFrom and AgeTo as non-negative whole months and SHALL expose the period type in the response.
2. WHEN PeriodType is `Days`, THE Provision Parameter Service SHALL interpret AgeFrom and AgeTo as non-negative whole days and SHALL expose the period type in the response.
3. IF AgeFrom is negative, AgeTo is less than AgeFrom, or a non-final bucket has a null AgeTo, THEN THE Provision Parameter Service SHALL reject the mutation with field-level validation details.
4. IF more than one bucket has a null AgeTo, THEN THE Provision Parameter Service SHALL reject the mutation.
5. IF AgeingDays is negative or exceeds the configured maximum of 36500, THEN THE Provision Parameter Service SHALL reject the mutation.
6. IF `CutOffMonth` is outside the inclusive range 1 through 31, THEN THE Provision Parameter Service SHALL reject the mutation.
7. **Assumption:** CutOffMonth is required for effective-dated sets and optional for non-dated sets; the compatibility gate may confirm a stricter requirement.

### Requirement 7: Validate bucket age ranges and date ranges

**User Story:** As a Risk & Policy Administrator, I want overlapping or invalid bucket ranges rejected, so that one delinquency age cannot resolve to conflicting rates.

#### Acceptance Criteria

1. WHEN a bucket list is submitted, THE Provision Parameter Service SHALL validate age ranges within the same provision parameter set and effective scope using inclusive boundaries.
2. IF a bucket list contains `[0,30]` and `[15,45]`, THEN THE Provision Parameter Service SHALL reject the mutation with the exact English message `Provision rate ranges must not overlap`.
3. IF two bucket age ranges touch only at the next non-overlapping boundary, THE Provision Parameter Service SHALL accept them; for inclusive boundaries, `[0,30]` followed by `[31,60]` is valid.
4. IF a bucket row has StartDate after EndDate, THEN THE Provision Parameter Service SHALL reject the mutation with a date-range validation error.
5. IF two bucket row date intervals overlap within a scope where row dates are supplied, THEN THE Provision Parameter Service SHALL reject the mutation with a date-overlap validation error.
6. IF a supplied bucket row date falls outside a dated set's effective range, THEN THE Provision Parameter Service SHALL reject the mutation.
7. WHEN bucket row dates are both null, THE Provision Parameter Service SHALL evaluate the row against the parent set's effective date range.
8. **Assumption:** Age range boundaries are inclusive and only the final ordered bucket may be open-ended. This assumption is visible in the UI and API validation behavior.

### Requirement 8: Validate provision percentages and rates

**User Story:** As a Risk & Policy Administrator, I want provision percentages validated, so that consumers never receive impossible rates.

#### Acceptance Criteria

1. WHEN a bucket is submitted, THE Provision Parameter Service SHALL accept BaseProvisionPercentage and CashProvisionPercentage values from 0 through 100 inclusive with at most two decimal places.
2. IF either provision percentage is negative, greater than 100, or has more than two decimal places, THEN THE Provision Parameter Service SHALL reject the mutation and SHALL identify the offending bucket and field.
3. IF CashProvisionPercentage is greater than BaseProvisionPercentage, THEN THE Provision Parameter Service SHALL reject the mutation with a rate-order validation error.
4. WHEN a valid percentage is stored, THE Provision Parameter Service SHALL preserve the decimal value without binary floating-point rounding.
5. **Assumption:** CashProvisionPercentage <= BaseProvisionPercentage is required; the compatibility gate may confirm whether a separate cash-rate policy permits exceptions.

### Requirement 9: Effective dating and applicable-set resolution

**User Story:** As an approved provision consumer, I want a deterministic set for an evaluation date, so that only rates effective on that date are used.

#### Acceptance Criteria

1. WHEN an effective-dated set is queried for an evaluation date inside its inclusive range, THE Provision Parameter Resolver SHALL consider the set applicable.
2. WHEN an effective-dated set is queried for an evaluation date before its start or after its end, THE Provision Parameter Resolver SHALL exclude the set.
3. WHEN a non-dated active set is queried, THE Provision Parameter Resolver SHALL treat the set as open-ended for date matching.
4. WHEN no active set matches Company, DelinquentType, LOB precedence, and evaluation date, THE Provision Parameter Resolver SHALL fail closed with a no-match error and SHALL not return rates.
5. WHEN a matching set is inactive, THE Provision Parameter Resolver SHALL exclude the set even if the date matches.
6. WHEN an administrator queries the master, THE Provision Parameter Service SHALL allow inactive and historical configuration rows to be queried with explicit filters, while the consumer resolver SHALL return active applicable rows only.
7. **Assumption:** Start and end dates are inclusive; a null EffectiveEndDate is open-ended.

### Requirement 10: Modify, activate, and deactivate a parameter set

**User Story:** As a Risk & Policy Administrator, I want to modify rates and lifecycle state safely, so that configuration changes become effective with a traceable version.

#### Acceptance Criteria

1. WHEN an authorized administrator submits a valid modification with the current Version or concurrency token, THE Provision Parameter Service SHALL save the changed set and bucket values, increment Version by one, refresh the concurrency token, and return the new version.
2. WHEN a modification changes a provision rate, THE Provision Parameter Service SHALL record the previous and new rate values in exactly one immutable Audit Entry.
3. WHEN a modification changes Active from true to false or false to true, THE Provision Parameter Service SHALL record the lifecycle change in exactly one immutable Audit Entry.
4. WHEN an administrator deactivates a set, THE Provision Parameter Resolver SHALL stop returning that set for subsequent evaluations while the set remains queryable to administrators.
5. IF an update supplies a stale Version or RowVersion, THEN THE Provision Parameter Service SHALL reject the update with a conflict response, SHALL preserve the stored version, and SHALL write no audit entry.
6. IF a modification produces an invalid range, date overlap, active-scope overlap, or percentage, THEN THE Provision Parameter Service SHALL reject the modification atomically and SHALL preserve the prior version.
7. **Assumption:** Modify updates the logical set in place while Version and audit snapshots preserve the change history; append-only version rows may replace this model only after the compatibility gate approves it.

### Requirement 11: Query sets, versions, buckets, and audits

**User Story:** As a Risk & Policy Administrator, I want to query provision parameters and their audit history, so that I can review current and historical configuration.

#### Acceptance Criteria

1. WHEN an authorized administrator queries provision parameters, THE Provision Parameter Service SHALL support filters for CompanyId, LineOfBusinessId/global scope, DelinquentType, PeriodType, Active, and evaluation date.
2. WHEN a query has no filters, THE Provision Parameter Service SHALL return a deterministic paginated result ordered by CompanyId, DelinquentType, LOB scope, effective start, and Version descending.
3. WHEN a query requests a specific set identifier, THE Provision Parameter Service SHALL return the set, Version, concurrency token, and ordered buckets.
4. WHEN an administrator requests audit history, THE Provision Parameter Service SHALL return immutable audit entries ordered newest first with actor identity, UTC timestamp, action, version, and before/after snapshots.
5. IF a query requests an unknown set identifier, THEN THE Provision Parameter Service SHALL return a 404 ProblemDetails response.
6. WHEN a query matches no rows, THE Provision Parameter Service SHALL return an empty successful paginated result with total count zero.
7. THE Provision Parameter Service SHALL expose no endpoint that updates or deletes an Audit Entry.

### Requirement 12: Version, concurrency, and idempotency

**User Story:** As a Risk & Policy Administrator, I want repeated submissions and concurrent edits handled deterministically, so that a browser retry cannot duplicate or overwrite another administrator's change.

#### Acceptance Criteria

1. WHEN a create or modify request includes an Idempotency-Key and a request fingerprint identical to a previously completed request, THE Provision Parameter Service SHALL return the original response without creating a second set, version, or audit entry.
2. IF an Idempotency-Key is reused with a different request fingerprint, THEN THE Provision Parameter Service SHALL return a conflict ProblemDetails response and SHALL not change master or audit data.
3. IF a mutation omits the required Idempotency-Key, THEN THE Provision Parameter Service SHALL reject the request before persistence.
4. WHEN a modification succeeds, THE Provision Parameter Service SHALL advance Version exactly once.
5. IF two modifications use the same current concurrency token, THE Provision Parameter Service SHALL commit at most one modification and SHALL reject the other with a concurrency conflict.
6. THE Provision Parameter Service SHALL calculate request fingerprints from normalized semantic values, including ordered buckets, rather than raw JSON property order.

### Requirement 13: Record an immutable audit trail

**User Story:** As a Risk & Policy Administrator, I want all accepted configuration changes audited, so that rate changes remain accountable.

#### Acceptance Criteria

1. WHEN a create, modify, activate, or deactivate operation commits, THE Provision Parameter Service SHALL write exactly one immutable Audit Entry in the same transaction.
2. THE Audit Entry SHALL contain SetId, Version, action, trusted actor identity, UTC timestamp, correlation identifier, request idempotency key, and before/after snapshots sufficient to identify changed rates and parameters.
3. IF validation, conflict, authorization, reference lookup, or idempotency processing rejects a mutation, THEN THE Provision Parameter Service SHALL write no Audit Entry.
4. WHEN an administrator reads audit history, THE Provision Parameter Service SHALL return stored actor identity from the trusted JWT context and SHALL not accept actor identity from the request body.
5. THE Provision Parameter Service SHALL reject any attempted audit update or delete with a stable immutable-audit conflict error.
6. **Assumption:** Audit data is append-only and retained with no FINNOVA-15 deletion path; retention/legal policy is a platform concern outside this ticket.

### Requirement 14: Enforce authentication, authorization, and stable errors

**User Story:** As a security stakeholder, I want only authorized administrators to manage provision rates, so that policy configuration cannot be changed by ordinary users.

#### Acceptance Criteria

1. IF a create, modify, query, audit, or resolver-administration request has a missing, expired, malformed, or invalid JWT, THEN THE Provision Parameter Service SHALL return HTTP 401 with a stable English authentication error and SHALL not mutate data.
2. IF an authenticated user lacks the confirmed SystemAdmin/Risk & Policy Administrator policy, THEN THE Provision Parameter Service SHALL return HTTP 403 and SHALL not mutate data.
3. WHEN a valid authorized administrator submits a request, THE Provision Parameter Service SHALL derive actor identity from trusted JWT claims and SHALL not trust a caller-supplied actor field.
4. WHEN a domain or validation error occurs, THE Provision Parameter Service SHALL return RFC 7807 ProblemDetails with a stable `code`, English `detail`, correlation identifier, and appropriate HTTP status.
5. THE Provision Parameter Service SHALL use the exact conflict detail `Provision rate ranges must not overlap` for overlapping age ranges.
6. WHEN the gateway forwards UI traffic, THE API Gateway SHALL preserve the Authorization header and route the feature to the SystemAdmin cluster without collapsing host boundaries.
7. **Assumption:** Authentication precedes authorization so a request failing both checks returns 401 rather than 403.

### Requirement 15: Expose a read-only resolver and preserve accounting boundaries

**User Story:** As a future Demand Processing or loan/accounting provision consumer, I want a stable read-only resolver, so that provision rates can be evaluated without coupling the consumer to admin persistence.

#### Acceptance Criteria

1. THE Provision Parameter Resolver SHALL expose a read-only `IProvisionParameterResolver` contract accepting CompanyId, DelinquentType, optional LineOfBusinessId, and evaluation date.
2. WHEN the resolver finds one applicable set, THE Provision Parameter Resolver SHALL return the selected SetId, Version, PeriodType, ageing parameters, effective range, scope, and ordered bucket rates.
3. WHEN exact LOB and global sets both match, THE Provision Parameter Resolver SHALL return the exact LOB result and SHALL not merge buckets from both sets.
4. WHEN no match or ambiguity exists, THE Provision Parameter Resolver SHALL fail closed with a typed no-match or ambiguity error.
5. THE Provision Parameter Resolver SHALL not expose mutation methods, receipt appropriation methods, or journal-writing methods.
6. FINNOVA-15 SHALL not connect to Receipt Processing or change receipt appropriation behavior unless a future approved consumer contract explicitly requires a read-only resolver call.
7. IF a future consumer posts accounting entries based on resolved rates, THEN THE consumer SHALL use the existing JVE Boundary and THE Provision Parameter Resolver SHALL not write canonical journals.

### Requirement 16: Test and integrate the complete feature

**User Story:** As a delivery team, I want automated coverage for configuration rules and host/UI wiring, so that provision master changes remain safe.

#### Acceptance Criteria

1. THE backend test suite SHALL cover validation, inclusive overlap detection, effective-date matching, exact-LOB precedence, ambiguity fail-closed behavior, percentage rules, lifecycle, versioning, idempotency, audit immutability, and no-journal/no-receipt boundaries.
2. THE backend property-based tests SHALL use FsCheck.Xunit with at least 100 iterations for each pure range, effective-date, precedence, and normalization property.
3. THE backend integration tests SHALL use `WebApplicationFactory<Program>` to verify authentication, authorization, gateway-compatible routes, ProblemDetails, create/modify/query flows, and concurrency/idempotency behavior with representative examples.
4. THE frontend Vitest and React Testing Library tests SHALL cover screenshot-aligned rendering, dependent lookup loading, create/modify/query interactions, Clear/Cancel, overlap error display, active lifecycle, and English-only labels.
5. THE test suite SHALL assert that FINNOVA-15 does not create journal records, invoke receipt appropriation, or bypass the existing JVE boundary.
6. THE implementation SHALL preserve the compatibility gates in Dependencies and Assumptions before code is merged.
