# Requirements Document

## Introduction

The GL Master Management feature (Module: SystemAdmin, Master: GL Master / General Ledger Account Master) provides a centralized administrative capability for defining and maintaining the General Ledger (GL) accounts used across the Finnova Financial Accounting system. The GL Master is the backbone of the accounting platform: it underpins posting rules, account grouping, sub-ledger (SL) mapping, and integration with downstream modules such as Customer Master, Asset Master, LPO, Billing, Receipts, Payments, and Cashflow Master. GL account definitions must be standardized in one place rather than duplicated per module.

A System Administrator can create a GL account (GL Code, GL Name, Account Type, Posting Group, Currency, Status, journal-permission flags, linked SL accounts, and effective dates), modify the non-financial attributes of an existing GL account, and search GL accounts by code, name, account type, or status with pagination. Account Type, Currency, and Posting Group are reference data loaded from the existing Lookup Master (Module `SystemAdmin`) rather than from fixed enums or hardcoded sets; the GL account stores the corresponding reference code and the service validates each code against the active lookup values. The User Branch is a Level-5 Location drawn from the Branch/Location Master. The GL Code uniquely and immutably identifies a GL account; the Account Type code is a financial attribute fixed at creation. Because GL accounts drive accounting integrity, the service enforces structural validity (code format, uniqueness, effective-date ordering, posting-group mapping) and exposes whether a GL account is postable at a given date, while the actual transaction-time posting enforcement remains the responsibility of consuming modules. Every create and modify operation is captured in an audit trail so that changes to the chart of accounts are traceable. All management operations are restricted to System Administrators; non-admin users are denied access.

This master-data feature mirrors the established master-data features (for example, Nationality Master and Lookup Master). It follows the platform's layered CQRS conventions (ASP.NET Core Web API, MediatR, FluentValidation `ValidationBehavior` pipeline, EF Core + SQL Server, generic `IRepository<T>` + `RepositoryBase<T>`, record-based contracts with `.ToResponse()` mappers, `PaginatedResponse<T>`) and is hosted in the existing `Finnova.SystemAdminService` behind the `Finnova.ApiGateway` YARP reverse proxy. Domain exceptions are mapped to RFC 7807 ProblemDetails by the existing `ExceptionHandlingMiddleware` using a `code` extension; this feature uses an `ERR-GL-xxx` error-code family. The user-facing screen is implemented in the separate Finnova-UI repository (React + TypeScript + Material UI), mirroring the `LookupMaster.tsx` pattern (service interface + mock + real implementation + `VITE_USE_MOCK_API` toggle). This spec covers both the backend and the frontend.

This document captures the requirements derived from the GL Master source material. Where the source material is silent, decisions are recorded explicitly as **Assumptions** within the relevant requirement so they can be confirmed during review.

### Dependencies and Cross-Cutting Assumptions

- **Assumption (Host placement):** GL Master is a SystemAdmin concern and is placed in the existing `Finnova.SystemAdminService` host, reached through the gateway prefix `/api/systemadmin/**`. A UI-facing gateway alias route `/api/ua/api/glaccount/**` will be added (rewritten to the SystemAdmin cluster), mirroring the existing nationality/lookup aliases. This is confirmed during design.
- **Assumption (Authentication dependency):** The `Finnova.SystemAdminService` host already configures JWT Bearer authentication and a `SystemAdmin` authorization policy. This feature reuses that scheme and policy. Token issuance (login) ownership is external to this feature.
- **Assumption (Audit trail scope):** This feature introduces a GL-scoped audit trail (Requirement 9) consistent with the nationality-scoped audit trail. Whether the audit trail should be promoted to a shared, platform-wide capability is flagged for confirmation in design.
- **Dependency (Lookup Master reference data):** GL Master depends on the existing Lookup Master (the FINNOVA Lookup feature, Module `SystemAdmin`) as the authoritative source of its reference data: the `GL_ACCOUNT_TYPE`, `CURRENCY`, and `GL_POSTING_GROUP` lookup types. The GL account stores reference codes and the GL_Service validates each code against the active lookup values of the corresponding lookup type.
- **Dependency (Branch / Location Master):** GL Master depends on the existing Location Master for branches. In this platform a branch is a Level-5 Location; the GL account stores `UserBranchId` mapping to `Location.Id` and the GL_Service validates it against an existing active Level-5 Location.
- **Assumption (Reference-data seeding):** The `GL_ACCOUNT_TYPE`, `CURRENCY`, and `GL_POSTING_GROUP` lookup types and their required seed values are provisioned via seed data as part of this feature (see Requirement 11). This is confirmed in design.
- **Assumption (Default currency):** The default currency `INR` is itself a seeded `CURRENCY` lookup value and is applied when a create request omits Currency. This replaces the earlier notion of a separately configured platform default currency and is confirmed during review.
- **Dependency (Posting-group consumers):** The actual consumers of posting-group-to-GL mapping (Customer Master, LPO Accounting, Billing, Receipts, Payments, Cashflow Master) are treated as EXTERNAL modules that may not yet exist. GL Master exposes and validates the mapping; it does not build those consumers. These integration points are captured as assumptions, not hard build dependencies, within Requirements 6 and 7.
- **Dependency (Transaction-time posting enforcement):** Enforcement that every posted transaction references a valid, active, in-effective-range GL account at posting time belongs to the consuming accounting modules. Within this feature, GL Master exposes the derived postability of a GL account at a given date (Requirement 7); it does not post transactions.
- **Dependency (Sub-ledger / SL accounts):** Linked SL Accounts are referenced by identifier/code. The authoritative SL account catalog is external; this feature stores and returns the linkage as provided and does not validate SL account existence against an external catalog unless that catalog contract is confirmed in design.
- **Note (Localization):** Finnova is an India-only, English-only platform. Each GL account carries a single English display name (`GLName`); there are no bilingual fields, no Arabic content, and no right-to-left rendering.

## Glossary

- **GL Master**: The centralized administrative catalog (chart of accounts) of General Ledger account records used across the Financial Accounting system.
- **GL Account (GL Account Record)**: A single record in the GL Master, consisting of a GL Code, GL Name, Account Type, Posting Group, Currency, Status, Allow Manual Journal flag, Allow System Journal flag, zero or more Linked SL Accounts, an Effective Start Date, an optional Effective End Date, and audit timestamps.
- **GL Code**: A short, unique, alphanumeric, machine-readable identifier for a GL account (for example, `A01`, `AA1`, `GL1001`), unique across the GL Master and immutable once created.
- **GL Name**: The single English display name of a GL account.
- **Account Type**: The financial classification of a GL account, a value loaded from the Lookup Master (LookupType `GL_ACCOUNT_TYPE`); the seeded values are `Asset`, `Liability`, `Income`, and `Expense`. The GL account stores the Account Type code; the code is immutable once created.
- **Posting Group**: A classification of a GL account used to drive posting mapping, a value loaded from the Lookup Master (LookupType `GL_POSTING_GROUP`); the GL account stores the Posting Group code. The seeded posting groups used for the posting-group-to-GL mapping are `Customer`, `Dealer`, `Insurance`, `Unearned Income`, and `Lease Receivable`.
- **Currency**: The currency associated with a GL account, a value loaded from the Lookup Master (LookupType `CURRENCY`); the GL account stores the Currency code. The default currency `INR` is a seeded `CURRENCY` lookup value.
- **Lookup Master (reference-data source)**: The existing FINNOVA Lookup feature (Module `SystemAdmin`) that holds reference-data lookup types and their lookup values. GL Master reads the active lookup values of `GL_ACCOUNT_TYPE`, `CURRENCY`, and `GL_POSTING_GROUP` to validate and resolve the display of the corresponding GL account codes.
- **Branch (Location Level 5)**: A branch in this platform, represented as a Level-5 Location in the Branch/Location Master. GL Master stores `UserBranchId` mapping to `Location.Id`.
- **Status**: The lifecycle state of a GL account; one of `Active` or `Inactive`. Defaults to `Active`.
- **Allow Manual Journal**: A boolean flag indicating whether manual journal entries may post to the GL account.
- **Allow System Journal**: A boolean flag indicating whether system-generated journal entries may post to the GL account.
- **Linked SL Account**: An optional reference (by SL account identifier/code) associating a sub-ledger account with the GL account. A GL account may have zero or more Linked SL Accounts.
- **Effective Start Date**: The date from which the GL account is effective. Required.
- **Effective End Date**: The optional date after which the GL account is no longer effective.
- **Effective Date Range**: The inclusive period bounded by Effective Start Date and (when present) Effective End Date during which the GL account is in effect.
- **Postable**: A derived condition indicating a GL account may be used for posting at a given date, true only when Status is Active and the given date falls within the Effective Date Range.
- **Posting-Group-to-GL Mapping**: The association that resolves a required posting group to a valid GL account used by consuming modules for posting.
- **Reserved Code**: The GL Code value `000`, reserved and not permitted as a GL Code.
- **Financial Attribute**: An attribute whose change would alter accounting classification or identity; specifically GL Code and the Account Type code, both immutable after creation.
- **Non-Financial Attribute**: An editable attribute; specifically GL Name, Status, Posting Group, Allow Manual Journal, Allow System Journal, Effective Start Date, Effective End Date, and Linked SL Accounts.
- **Company Code**: The company context value automatically associated with the GL account from the request context.
- **User Branch**: A Level-5 Location (branch) from the Branch/Location Master, resolved from the request context; the GL account stores `UserBranchId` mapping to `Location.Id`.
- **Audit Entry**: An immutable record capturing a create or modify action on a GL account, including the affected GL account identifier, the action performed, the changed fields' before and after values, the acting System Administrator's identifier, and the timestamp of the change.
- **Audit Trail**: The ordered collection of Audit Entries recorded for the GL Master.
- **System Administrator**: An authenticated user whose token carries the SystemAdmin role claim, authorized to manage GL account records.
- **Consuming Module**: A business module (for example, Customer Master, LPO Accounting, Billing, Receipts, Payments, Cashflow Master) that reads GL accounts and posting-group mappings to drive posting.
- **EARS**: Easy Approach to Requirements Syntax; a set of structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.
- **GL_Service**: The backend service/API responsible for GL account management and retrieval.
- **GL_Admin_UI**: The React-based GL Master administrative screen used by System Administrators (implemented in the Finnova-UI repository).

## Requirements

### Requirement 1: Create GL Account

**User Story:** As a System Administrator, I want to create a new GL account with its code, name, account type, and posting attributes, so that the account becomes available in the chart of accounts for posting and mapping.

#### Acceptance Criteria

1. WHEN a System Administrator submits a create request containing a GL Code, a GL Name, an Account Type, a Posting Group, and an Effective Start Date, THE GL_Service SHALL persist a new GL account record and return the created record including a system-generated identifier and the resolved Status, Allow Manual Journal, Allow System Journal, and Currency values.
2. WHERE Status is not provided on a create request, THE GL_Service SHALL default Status to `Active`.
3. IF a create request provides a Status value that is not one of `Active` or `Inactive`, THEN THE GL_Service SHALL reject the request with a validation error identifying Status and SHALL NOT persist any record.
4. WHERE Allow Manual Journal is not provided on a create request, THE GL_Service SHALL default Allow Manual Journal to `true`.
   - **Assumption:** The source does not specify the default for Allow Manual Journal; `true` is proposed for confirmation.
5. WHERE Allow System Journal is not provided on a create request, THE GL_Service SHALL default Allow System Journal to `true`.
   - **Assumption:** The source does not specify the default for Allow System Journal; `true` is proposed for confirmation.
6. WHERE Currency is not provided on a create request, THE GL_Service SHALL default the Currency code to the seeded default `CURRENCY` lookup value (`INR`); IF a create request provides a Currency code that does not reference an active `CURRENCY` lookup value, THEN THE GL_Service SHALL reject the request with a validation error identifying Currency and SHALL NOT persist any record.
7. WHEN a System Administrator submits a create request, THE GL_Service SHALL associate the Company Code derived from the request context and capture the User Branch as a `UserBranchId` resolved from the request context, without requiring the System Administrator to supply those values; IF the resolved `UserBranchId` does not reference an existing active Level-5 Location in the Branch/Location Master, THEN THE GL_Service SHALL reject the request with a validation error identifying the User Branch and SHALL NOT persist any record.
8. WHERE Linked SL Accounts are not provided on a create request, THE GL_Service SHALL persist the GL account with an empty Linked SL Accounts collection.
9. IF a create request omits GL Code, GL Name, Account Type, Posting Group, or Effective Start Date, or provides any of GL Code, GL Name, Account Type, or Posting Group as an empty or whitespace-only value, THEN THE GL_Service SHALL reject the request with a validation error identifying each missing or empty field and SHALL NOT persist any record.
10. IF a create request provides a GL Name that, after trimming, exceeds 100 characters, or a Posting Group that, after trimming, exceeds 50 characters, THEN THE GL_Service SHALL reject the request with a validation error identifying the offending field and SHALL NOT persist any record.
    - **Assumption:** The maximum GL Name length (100) and Posting Group length (50) are not specified in the source; these values are proposed for confirmation.
11. IF a create request provides an Account Type code that does not reference an active `GL_ACCOUNT_TYPE` lookup value, THEN THE GL_Service SHALL reject the request with a validation error identifying Account Type and SHALL NOT persist any record.
12. IF a create request provides an Effective Start Date that is not a valid calendar date, THEN THE GL_Service SHALL reject the request with a validation error identifying the Effective Start Date and SHALL NOT persist any record.
13. WHEN a GL account record is created with Status set to `Active`, THE GL_Service SHALL include the record in the results of the GL account query (Requirement 8) without requiring a service restart.
14. IF a create request provides a Posting Group code that does not reference an active `GL_POSTING_GROUP` lookup value, THEN THE GL_Service SHALL reject the request with a validation error identifying Posting Group and SHALL NOT persist any record.

### Requirement 2: GL Code Validation and Uniqueness

**User Story:** As a System Administrator, I want the system to enforce a valid, unique GL Code, so that each GL account is uniquely and reliably identified in the chart of accounts.

#### Acceptance Criteria

1. THE GL_Service SHALL treat GL Code as unique across the GL Master, comparing codes case-insensitively (for example, `a01` and `A01` are treated as the same code) after trimming leading and trailing whitespace.
2. IF a create or update request provides a GL Code that is empty or contains only whitespace, THEN THE GL_Service SHALL reject the request with a validation error indicating the GL Code is required and SHALL make no change to the GL Master.
3. IF a create or update request provides a GL Code that, after trimming, contains any character other than ASCII letters (`A`–`Z`, `a`–`z`) or digits (`0`–`9`), THEN THE GL_Service SHALL reject the request with a validation error indicating the GL Code must be alphanumeric and SHALL make no change to the GL Master.
4. IF a create or update request provides a GL Code that, after trimming, equals the Reserved Code `000`, THEN THE GL_Service SHALL reject the request with a validation error indicating the GL Code `000` is reserved and SHALL make no change to the GL Master.
5. IF a create or update request provides a GL Code that, after trimming, exceeds 20 characters, THEN THE GL_Service SHALL reject the request with a validation error identifying the GL Code and SHALL make no change to the GL Master.
   - **Assumption:** The maximum GL Code length (20) is not specified in the source; this value is proposed for confirmation.
6. IF a create request provides a GL Code that, after trimming and case-insensitive comparison, matches the GL Code of an existing GL account, THEN THE GL_Service SHALL reject the request with a validation error indicating the GL Code already exists, SHALL create no new record, and SHALL retain the existing record unchanged.

### Requirement 3: Immutability of Financial Attributes

**User Story:** As a System Administrator, I want GL Code and the Account Type code to be fixed once a GL account is created, so that accounting identity and classification remain stable across the chart of accounts.

#### Acceptance Criteria

1. IF an update request submits a GL Code that, after trimming leading and trailing whitespace and applying case-insensitive comparison, differs from the current GL Code of the GL account being updated, THEN THE GL_Service SHALL reject the request, SHALL return a validation error indicating the GL Code is immutable, and SHALL preserve the existing record unchanged with no persisted field modifications.
2. IF an update request submits an Account Type code that, after trimming leading and trailing whitespace and applying case-insensitive comparison, differs from the current Account Type code of the GL account being updated, THEN THE GL_Service SHALL reject the request, SHALL return a validation error indicating the Account Type is immutable, and SHALL preserve the existing record unchanged with no persisted field modifications.
3. WHEN an update request submits a GL Code that equals the current GL Code of the GL account being updated after trimming leading and trailing whitespace and applying case-insensitive comparison, THE GL_Service SHALL treat the GL Code as unchanged and SHALL NOT reject the request on the grounds of GL Code immutability.
4. WHEN an update request submits an Account Type code that, after trimming leading and trailing whitespace and applying case-insensitive comparison, equals the current Account Type code of the GL account being updated, THE GL_Service SHALL treat the Account Type as unchanged and SHALL NOT reject the request on the grounds of Account Type immutability.
5. IF an update request omits the GL Code field or submits it as null or empty after trimming, THEN THE GL_Service SHALL treat the GL Code as unchanged, SHALL NOT reject the request on the grounds of immutability, and SHALL preserve the existing GL Code.

### Requirement 4: Modify GL Account Non-Financial Attributes

**User Story:** As a System Administrator, I want to update the non-financial attributes of an existing GL account, so that the chart of accounts stays accurate as business needs change.

#### Acceptance Criteria

1. WHEN a System Administrator submits an update to an existing GL account's Non-Financial Attributes (GL Name, Status, Posting Group, Allow Manual Journal, Allow System Journal, Effective Start Date, Effective End Date, Linked SL Accounts) and the submitted values satisfy every validation rule in this requirement and in Requirements 2, 3, and 5, THE GL_Service SHALL persist the changes, refresh the record's UpdatedAt timestamp, and return the updated record reflecting the submitted values.
2. IF a System Administrator submits an update where GL Name is empty or contains only whitespace, THEN THE GL_Service SHALL reject the request with a validation error identifying GL Name as required and SHALL preserve the existing record unchanged.
3. IF a System Administrator submits an update where GL Name, after trimming, exceeds 100 characters, THEN THE GL_Service SHALL reject the request with a validation error identifying GL Name and SHALL preserve the existing record unchanged.
   - **Assumption:** The maximum GL Name length (100) is not specified in the source; this value is proposed for confirmation.
4. IF a System Administrator submits an update that sets Status to a value other than `Active` or `Inactive`, THEN THE GL_Service SHALL reject the request with a validation error identifying Status and SHALL preserve the existing record unchanged.
5. IF a System Administrator submits an update where Posting Group is empty or contains only whitespace, or provides a Posting Group code that does not reference an active `GL_POSTING_GROUP` lookup value, THEN THE GL_Service SHALL reject the request with a validation error identifying Posting Group and SHALL preserve the existing record unchanged.
6. IF a System Administrator submits an update whose Linked SL Accounts collection contains more than 100 entries, THEN THE GL_Service SHALL reject the request with a validation error identifying Linked SL Accounts and SHALL preserve the existing record unchanged.
   - **Assumption:** The maximum Linked SL Accounts count (100) is not specified in the source; this value is proposed for confirmation.
7. IF a System Administrator attempts to update a GL account whose identifier does not exist, THEN THE GL_Service SHALL return a not-found error and SHALL make no change to the GL Master.
8. WHEN a System Administrator submits an update whose Non-Financial Attribute values all equal the record's current values, THE GL_Service SHALL treat the update as a successful no-op and return the existing record.

### Requirement 5: Effective Date Range Validity

**User Story:** As a System Administrator, I want effective dates validated, so that each GL account carries a coherent effective period.

#### Acceptance Criteria

1. IF a create or update request omits the Effective Start Date, THEN THE GL_Service SHALL reject the request with a validation error indicating the Effective Start Date is required and SHALL make no change to the GL Master.
2. WHERE a create or update request omits the Effective End Date, THE GL_Service SHALL treat the resulting GL account as having no end bound and SHALL accept the request subject to the remaining validation rules.
3. IF a create or update request provides both an Effective Start Date and an Effective End Date where the Effective End Date, compared by calendar date, is earlier than the Effective Start Date, THEN THE GL_Service SHALL reject the request with a validation error indicating the Effective End Date must be on or after the Effective Start Date and SHALL make no change to the GL Master.
4. WHERE a create or update request provides both an Effective Start Date and an Effective End Date that are equal by calendar date, THE GL_Service SHALL accept the dates as a valid single-day effective range subject to the remaining validation rules.

### Requirement 6: Posting-Group-to-GL Mapping Rules

**User Story:** As a System Administrator, I want required posting groups to resolve to a valid GL account, so that consuming modules can post against a consistent mapping.

#### Acceptance Criteria

1. THE GL_Service SHALL maintain, for each required Posting Group among the active `GL_POSTING_GROUP` lookup values (seeded as `Customer`, `Dealer`, `Insurance`, `Unearned Income`, and `Lease Receivable`), at most one Posting-Group-to-GL Mapping that resolves the posting group to exactly one GL account.
2. WHEN a System Administrator sets a Posting-Group-to-GL Mapping for a required Posting Group to an existing Active GL account, THE GL_Service SHALL persist the mapping, overwriting any previously stored mapping for that Posting Group, and return the resulting mapping.
3. IF a request to set a Posting-Group-to-GL Mapping specifies a Posting Group code that does not reference an active `GL_POSTING_GROUP` lookup value, THEN THE GL_Service SHALL reject the request with a validation error identifying the Posting Group and SHALL make no change to the mapping.
4. IF a request to set a Posting-Group-to-GL Mapping references a GL account identifier that does not exist in the GL Master, THEN THE GL_Service SHALL reject the request with a validation error indicating the mapped GL account was not found and SHALL make no change to the mapping.
5. IF a request to set a Posting-Group-to-GL Mapping references a GL account whose Status is `Inactive`, THEN THE GL_Service SHALL reject the request with a validation error indicating an Inactive GL account cannot be mapped and SHALL make no change to the mapping.
6. WHEN a System Administrator requests the Posting-Group-to-GL Mapping, THE GL_Service SHALL return each required Posting Group together with the mapped GL account where a mapping exists, and SHALL indicate the absence of a mapping for any required Posting Group that is not yet mapped.
   - **Assumption:** The consumers of this mapping (Customer Master, LPO Accounting, Billing, Receipts, Payments, Cashflow Master) are external modules that may not yet exist. This feature exposes and validates the mapping; it does not implement the consumers. The required posting groups are the active `GL_POSTING_GROUP` lookup values seeded by this feature (`Customer`, `Dealer`, `Insurance`, `Unearned Income`, `Lease Receivable`) rather than a hardcoded literal set, and are confirmed in design.

### Requirement 7: GL Account Postability

**User Story:** As a System Administrator, I want to know whether a GL account is postable at a given date, so that consuming modules can rely on the GL Master to express posting eligibility.

#### Acceptance Criteria

1. WHEN a System Administrator requests the postability of a GL account for a specified date, THE GL_Service SHALL return Postable as `true` only where the GL account's Status is `Active` and the specified date falls on or after the Effective Start Date and, when an Effective End Date is present, on or before the Effective End Date.
2. WHILE a GL account's Status is `Inactive`, THE GL_Service SHALL return Postable as `false` for that GL account for every requested date.
3. WHEN a System Administrator requests the postability of a GL account for a date earlier than the Effective Start Date or, when an Effective End Date is present, later than the Effective End Date, THE GL_Service SHALL return Postable as `false`.
4. IF a System Administrator requests the postability of a GL account whose identifier does not exist, THEN THE GL_Service SHALL return a not-found error and SHALL NOT return a postability result.
   - **Assumption (dependency):** Enforcement that a posted transaction references a Postable GL account at posting time belongs to the consuming accounting modules (including system journals governed by Cashflow Master mapping rules). GL Master exposes the derived Postable condition; it does not post transactions. This delegation is confirmed in design.
5. IF a System Administrator requests the postability of a GL account without a specified date or with a date value that is absent or not a valid calendar date, THEN THE GL_Service SHALL return a validation error indicating the date is required and must be a valid calendar date, and SHALL NOT return a postability result.

### Requirement 8: Query and Search GL Accounts

**User Story:** As a System Administrator, I want to search GL accounts by code, name, account type, or status with pagination, so that I can locate the correct account in the chart of accounts.

#### Acceptance Criteria

1. WHEN a System Administrator requests GL accounts with a non-empty search term, THE GL_Service SHALL return only the records whose GL Code or GL Name contains the search term as a substring, matched case-insensitively, subject to the applied Account Type filter, Status filter, and pagination parameters.
2. WHEN a System Administrator requests GL accounts with an Account Type filter, THE GL_Service SHALL return only the records whose Account Type equals the specified Account Type, subject to the remaining filters and pagination parameters.
3. WHEN a System Administrator requests GL accounts with a Status filter, THE GL_Service SHALL return only the records whose Status equals the specified Status, subject to the remaining filters and pagination parameters.
4. WHERE a search term, Account Type filter, or Status filter is not provided, THE GL_Service SHALL omit that criterion from the query and return GL accounts subject to the remaining filters and pagination parameters.
5. WHEN a System Administrator requests GL accounts with a search term that, after trimming leading and trailing whitespace, is empty, THE GL_Service SHALL treat the request as having no search term and return the GL accounts subject to the applied filters and pagination parameters.
6. WHEN a System Administrator requests GL accounts, THE GL_Service SHALL return the results as a paginated response using the platform `PaginatedResponse<T>` contract, including the page items, total count, current page number, and applied page size.
7. WHEN a System Administrator requests GL accounts without specifying a page number, THE GL_Service SHALL apply a default page number of 1.
8. WHEN a System Administrator requests GL accounts without specifying a page size, THE GL_Service SHALL apply a default page size of 20.
9. IF a System Administrator requests a page number less than 1, THEN THE GL_Service SHALL reject the request and return a validation error indicating the page number is out of range, without returning any records.
10. IF a System Administrator requests a page size less than 1 or greater than 100, THEN THE GL_Service SHALL reject the request and return a validation error indicating the page size is out of range, without returning any records.
11. IF a System Administrator requests GL accounts with a search term that, after trimming, exceeds 100 characters, THEN THE GL_Service SHALL reject the request with a validation error indicating the search term is too long, without returning any records.
12. WHEN a System Administrator requests a page number beyond the last available page, THE GL_Service SHALL return an empty item collection while reporting the correct total count, current page number, and applied page size.
13. WHEN returning GL accounts, THE GL_Service SHALL order the records by GL Code ascending within the applied filter, using a deterministic case-insensitive comparison.
14. IF a search request matches no GL accounts, THEN THE GL_Service SHALL return an empty item collection with a total count of 0 and a success status.
15. IF a System Administrator requests GL accounts with an Account Type filter value (a code) that does not reference an active `GL_ACCOUNT_TYPE` lookup value, or a Status filter value that is not one of `Active` or `Inactive`, THEN THE GL_Service SHALL reject the request with a validation error identifying the offending filter and SHALL NOT return any records.

### Requirement 9: Record Audit Trail on Create and Modify

**User Story:** As a System Administrator, I want every GL account create and modify captured in an audit trail, so that changes to the chart of accounts are traceable.

#### Acceptance Criteria

1. WHEN the GL_Service creates a GL account (per Requirement 1), THE GL_Service SHALL record exactly one Audit Entry containing the created GL account identifier, the create action indicator, the resulting field values, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC) at millisecond resolution.
2. WHEN the GL_Service persists a change to a GL account's Non-Financial Attributes (per Requirement 4), THE GL_Service SHALL record exactly one Audit Entry containing the affected GL account identifier, the update action indicator, the prior and new values of each changed field, the acting System Administrator's identifier, and the timestamp of the change in Coordinated Universal Time (UTC) at millisecond resolution.
3. IF the GL_Service cannot record the Audit Entry for a create or update that would otherwise succeed, THEN THE GL_Service SHALL NOT persist the GL account create or modify, such that a change is never persisted without its corresponding Audit Entry.
4. IF a create or update request is rejected by validation, by the uniqueness rule (Requirement 2), or by the immutability rule (Requirement 3), THEN THE GL_Service SHALL NOT record an Audit Entry for that rejected request, and SHALL leave the count of persisted Audit Entries unchanged.
5. WHEN a System Administrator requests the Audit Entries for a specified GL account, THE GL_Service SHALL return the Audit Entries for that GL account ordered by change timestamp descending, and for entries sharing an identical timestamp SHALL apply a deterministic secondary ordering by audit entry identifier descending.
6. IF a System Administrator requests the Audit Entries for a GL account identifier that does not exist, THEN THE GL_Service SHALL return an empty result set and SHALL NOT return an error.
7. THE GL_Service SHALL retain each Audit Entry without modifying it after it is recorded.
8. IF a request is received to update or delete a previously recorded Audit Entry, THEN THE GL_Service SHALL reject the request with an error indicating that audit entries are immutable.

### Requirement 10: Authorization and Authentication Enforcement

**User Story:** As a security stakeholder, I want GL account management and query operations restricted to System Administrators, so that only authorized personnel can view or change the chart of accounts.

#### Acceptance Criteria

1. IF a request to create, update, query, read postability, read the posting-group mapping, or read the audit trail of a GL account is received with a missing, malformed, signature-invalid, or expired JWT Bearer token (evaluated with a clock-skew tolerance of 60 seconds applied to token expiry), THEN THE GL_Service SHALL reject the request with an unauthorized (401) response, SHALL NOT create, modify, or delete any stored GL account record, and SHALL return an error indication stating that authentication is required.
2. IF an authenticated user whose token lacks the SystemAdmin role claim attempts to create, update, query, read postability, read the posting-group mapping, or read the audit trail of a GL account, THEN THE GL_Service SHALL reject the request with a forbidden (403) response, SHALL NOT create, modify, or delete any stored GL account record, and SHALL return an error indication stating that SystemAdmin authorization is required.
3. WHEN a request carrying a JWT Bearer token that is valid and unexpired (evaluated with a clock-skew tolerance of 60 seconds applied to token expiry) and bearing the SystemAdmin role claim invokes a create, update, query, postability-read, posting-group-mapping-read, or audit-trail-read operation, THE GL_Service SHALL authorize the operation and execute it subject to the validation rules in Requirements 1 through 9.
4. WHEN the GL_Service evaluates authentication and authorization for any create, update, query, postability-read, posting-group-mapping-read, or audit-trail-read request, THE GL_Service SHALL enforce authentication before authorization such that a request failing both token validity and SystemAdmin role membership is rejected with the unauthorized (401) response rather than the forbidden (403) response.
5. **Assumption (dependency):** THE `Finnova.SystemAdminService` host that exposes the GL_Service SHALL reuse the JWT Bearer authentication scheme and the `SystemAdmin` authorization policy (evaluated via the role claim) already established for the SystemAdmin host. Establishing that scheme is a prerequisite already satisfied by existing SystemAdmin master-data features.
   - **Assumption:** Read access (query, postability, posting-group mapping, and audit-trail read) is restricted to System Administrators. If broader read access is desired for consuming modules, that is flagged for confirmation in design.

### Requirement 11: Reference Data Seeding and Resolution

**User Story:** As a System Administrator, I want the GL reference data (account types, currencies, posting groups) seeded in the Lookup Master and resolved from it, so that GL accounts reference standardized, maintainable values rather than hardcoded enums.

#### Acceptance Criteria

1. THE GL_Service SHALL seed the `GL_ACCOUNT_TYPE` lookup values `Asset`, `Liability`, `Income`, and `Expense` in the Lookup Master.
2. THE GL_Service SHALL seed the `CURRENCY` lookup values including the default `INR` in the Lookup Master.
3. THE GL_Service SHALL seed the `GL_POSTING_GROUP` lookup values `Customer`, `Dealer`, `Insurance`, `Unearned Income`, and `Lease Receivable` in the Lookup Master.
4. THE GL_Service SHALL seed the `GL_ACCOUNT_TYPE`, `CURRENCY`, and `GL_POSTING_GROUP` lookup values deterministically with stable identifiers such that re-applying the seed produces the same records without creating duplicates.
5. THE GL_Service SHALL seed each reference-data lookup value with a single English display value and SHALL NOT seed any non-English or bilingual value.
6. WHEN the GL_Service returns a GL account or GL account query result, THE GL_Service SHALL resolve the Account Type, Currency, and Posting Group display values from the active values of the corresponding Lookup Master lookup type and the User Branch display from the Branch/Location Master.
7. IF a create or update request provides an Account Type code, Currency code, or Posting Group code that is not present among the active lookup values of its corresponding lookup type, THEN THE GL_Service SHALL reject the request with a validation error identifying the offending field and SHALL make no change to the GL Master.
8. IF a create or update request resolves a `UserBranchId` that does not reference an existing active Level-5 Location in the Branch/Location Master, THEN THE GL_Service SHALL reject the request with a validation error identifying the User Branch and SHALL make no change to the GL Master.
   - **Assumption:** These reference-data seeds are delivered via EF Core `HasData` in the Lookup configuration, mirroring the existing `lookup_values` seed pattern. This is confirmed in design.
   - **Assumption:** Branches already exist in the Location Master; no branch seeding is owned by this feature. This is flagged for confirmation.
