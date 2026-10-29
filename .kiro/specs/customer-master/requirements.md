# Requirements Document

## Introduction

Customer Master is the existing Customer Onboarding aggregate and React multi-tab screen. This specification is a **gap-only patch** over the verified implementation, not a second end-to-end rebuild: the Customer domain, contracts, EF configurations, repositories/DI, create/modify/child CQRS, controller routes, gateway alias, page, tabs, routes, and navigation already exist. The patch makes query pagination and validation executable, aligns the frontend service contract, adds compatible error fields, adds the production India Address Control HTTP boundary, and supplies complete regression tests.

The backend is `e:\Finnova\Finnova-API` and the frontend is `E:\Finnova\Finnova-UI\Finnova-UI`. Finnova is India-only and English-only. Every display concept has one plain English field. No bilingual fields, translation scaffolding, Arabic/non-English data, RTL, Redux, or RxJS is added.

The authoritative FINNOVA-51..55/FRS text was not present. The existing Customer Onboarding implementation and `investigation.md` are therefore the frozen technical contract for this patch. The missing story text remains an explicit assumption and must not be treated as recovered product intent.

## Glossary

- **Customer**: Existing persisted aggregate rooted at `Finnova.Models.Domain.Entities.Customer`.
- **Customer Onboarding**: Existing API/UI implementation selected as the meaning of Customer Master for this patch.
- **Customer Code**: Existing generated, immutable twelve-character alphanumeric identifier.
- **Customer Reference**: Code/ID resolved through Lookup, Nationality, GL, Document, Company, or India Address Control boundaries.
- **Projection**: `CustomerResponse` resolved with current master display values without persisting those names on Customer.
- **India Address Control**: External validation boundary for Indian address/contact data. This patch adds the internal HTTP adapter, but the external endpoint and credentials remain deployment configuration.
- **PaginatedResponse**: Existing shared record `PaginatedResponse<T>(List<T> Data, int Total, int Page, int PageSize, int TotalPages)`.
- **System Administrator**: Authenticated principal accepted by the existing `SystemAdmin` policy.
- **ProblemDetails**: RFC 7807 response with `status`, `title`, `detail`, and extensions `code` and `message`.

## Dependencies and Assumptions

### Existing dependencies

The existing Customer entities and child entities, enums and exceptions, request/response contracts, EF configurations and DbSets, repository interfaces and DI, write/child commands, mappers, controller routes, middleware branch, JWT policy, YARP alias, frontend page/tabs/routes/navigation, and test infrastructure are reused. Existing source is not recreated by this patch unless a task explicitly modifies a gap.

### Assumptions for confirmation

- **A1 — Scope:** “Customer Master” means the existing Customer Onboarding aggregate and screen. This is a gap-only patch; no new grid, aggregate, host, or route family is required. Customer Reference/Mind Mapping remain deferred.
- **A2 — Missing story:** FINNOVA-51 through FINNOVA-55 and the complete FRS are unavailable. Existing implementation and investigation are the frozen contract until product ownership supplies a replacement.
- **A3 — Customer code:** Existing global `CUSTOMER` numbering, twelve-character alphanumeric output, and immutability are authoritative.
- **A4 — Existing fields and invariants:** Current Customer validators define requiredness, identity normalization, capacity flags, bank rules, document metadata, and child semantics.
- **A5 — References:** Active Lookup, Nationality, GL, Document, and India Address Control boundaries remain authoritative; Customer stores references and approved address values, not authoritative display names.
- **A6 — Address boundary contract:** The external India Address Control endpoint, authentication scheme, request field mapping, response schema, retry policy, and service ownership were not supplied. The internal adapter contract in the design is the implementation boundary. Deployment must provide `IndiaAddressControl:BaseUrl` and credentials/configuration matching the documented JSON contract.
- **A7 — Address availability:** A configured and reachable boundary is required for successful address-bearing create/modify. Timeout, non-success, malformed response, or missing configuration maps to `ERR-CUSTOMER-503`/HTTP 503; validation rejection maps to `ERR-CUSTOMER-ADDRESS-REF`/HTTP 400.
- **A8 — Atomicity:** Customer, children, and business audit rows use the existing unit of work. Number allocation and external address validation are separate side effects; a number gap is allowed and the address service is called before mutation.
- **A9 — Query:** Page is `1..20,000,000`, PageSize is `1..100`, defaults are 1/25, invalid values are rejected before repository access, and results use the shared paginated envelope.
- **A10 — Search:** Nonblank CustomerCode is trimmed, case-insensitive exact lookup and takes precedence over Search. Otherwise Search is a case-insensitive substring over CustomerCode, FirstName, and SecondName. CompanyId scopes results and CustomerCode ascending is deterministic order.
- **A11 — Catalog:** No catalog endpoint is invented. The unreachable frontend catalog call is removed because the current page does not need it.
- **A12 — Errors:** `message` equals safe `detail` for compatibility with the shared Axios interceptor.
- **A13 — Schema:** Customer tables and required indexes are provisioned outside this ticket. A read-only preflight checks required table and index presence/shape only. No live-schema migration is generated by this spec.
- **A14 — Provenance:** This authoring step writes no application source. Earlier application-file provenance cannot be proved from the available Git status. The user must record a baseline status/hash before applying tasks and compare it afterward.
- **A15 — Product localization:** One English field per display concept, India-specific fixtures, no Arabic/non-English values, no RTL, no translation scaffolding.

## Requirements

### Requirement 1: Customer ownership and payload safety

**User Story:** As a System Administrator, I want Customer-owned data and stable references protected from unapproved payloads.

#### Acceptance Criteria

1. **Ubiquitous:** THE service SHALL reuse the existing Customer aggregate and SHALL not add a parallel Customer model/table.
2. **Ubiquitous:** THE service SHALL persist reference codes/IDs and approved address results, but SHALL resolve authoritative display names at read time.
3. **Event-driven:** WHEN an authorized Customer is read, THE service SHALL return current projection names without mutating Customer persistence.
4. **Ubiquitous:** THE UI SHALL send reference codes/IDs and owned values, not caller-supplied authoritative display names.
5. **Unwanted-event:** IF a Customer create/modify request contains a raw document byte field, access token, credential, SQL, or unsupported external payload property, THEN the API SHALL reject the entire request with HTTP 400 and `ERR-CUSTOMER-400`, SHALL return safe non-secret `detail` and `message` where `message == detail`, and SHALL write no Customer or audit rows.

### Requirement 2: Create and modify

1. **Event-driven:** WHEN a valid authorized request is submitted and the configured India Address Control boundary approves supplied addresses, THE service SHALL validate, persist the aggregate/children, write the existing audit action, and return `CustomerResponse`.
2. **Event-driven:** WHEN a valid modify request targets an existing Customer Code and CompanyId, THE service SHALL enforce immutable fields, validate references/addresses/children, commit permitted changes and a `Modified` audit, and return the updated projection.
3. **Ubiquitous:** THE service SHALL reject client-generated create-time Customer Codes and SHALL retain the existing generated twelve-character rule.
4. **State-driven:** WHILE the UI is in Query mode, THE UI SHALL keep inputs read-only and SHALL not invoke create or modify.
5. **Unwanted-event:** IF validation, reference resolution, address validation, duplicate detection, or persistence fails before commit, THEN no partial Customer, child, or business-audit rows SHALL remain.

### Requirement 3: India Address Control boundary

1. **Ubiquitous:** THE service SHALL call `IIndiaAddressControl.ValidateAndApproveAsync` for every supplied Customer address before aggregate mutation.
2. **Event-driven:** WHEN the configured HTTP boundary returns an approved address, THE adapter SHALL return an `AddressControlResult` containing the approved `CustomerAddressResponse`.
3. **Unwanted-event:** IF the boundary returns a validation rejection, THE service SHALL return HTTP 400 with `ERR-CUSTOMER-ADDRESS-REF` and safe errors.
4. **Unwanted-event:** IF configuration is missing, the call times out, returns a non-success status, or returns malformed data, THE service SHALL return HTTP 503 with `ERR-CUSTOMER-503` and no Customer mutation.
5. **Assumption:** External endpoint URL, credentials, exact provider SLA, and final provider JSON remain deployment-owned and unresolved until confirmed.

### Requirement 4: Child records, references, and audit

1. **Ubiquitous:** THE service SHALL enforce existing bank default/duplicate, identity, document applicability, and child invariants.
2. **Event-driven:** WHEN a Customer or supported child change commits, THE service SHALL write the existing immutable `CustomerAuditEntry` with safe JSON and authenticated actor.
3. **Unwanted-event:** IF a child or reference invariant fails, THE service SHALL preserve prior state and write no business audit row.
4. **Ubiquitous:** THE service SHALL store document metadata/DMS references only and SHALL not store raw document bytes.

### Requirement 5: Paged query and projection

1. **Event-driven:** WHEN Page/PageSize are omitted, THE service SHALL use 1/25.
2. **Event-driven:** WHEN valid paging is supplied, THE service SHALL return `PaginatedResponse<CustomerResponse>` with correct Data, Total, Page, PageSize, and TotalPages.
3. **Event-driven:** WHEN Page is beyond the last page, THE service SHALL return empty Data with requested page and correct metadata.
4. **Event-driven:** WHEN CustomerCode is nonblank, THE service SHALL apply trimmed case-insensitive exact code matching and ignore Search; otherwise Search SHALL match CustomerCode, FirstName, or SecondName case-insensitively.
5. **Ubiquitous:** THE service SHALL apply CompanyId and order by CustomerCode ascending.
6. **Unwanted-event:** IF paging is invalid, THEN the service SHALL return HTTP 400 `ERR-CUSTOMER-400` and SHALL not call the read repository.

### Requirement 6: HTTP, auth, gateway, and errors

1. **Ubiquitous:** THE existing Customer routes and SystemAdmin policy SHALL remain.
2. **Ubiquitous:** THE gateway SHALL retain the direct SystemAdmin route and Customer onboarding alias to `systemadmin-cluster`.
3. **Event-driven:** WHEN a Customer exception reaches middleware, THE response SHALL contain status, title, detail, code, and message, with message equal to detail.
4. **Unwanted-event:** IF authentication fails, THE host SHALL return 401; IF authorization fails for a valid non-admin principal, THE host SHALL return 403.

### Requirement 7: Frontend parity

1. **Ubiquitous:** THE UI SHALL retain existing create/modify/query modes, eight tabs, routes, navigation, and Query-mode save gating.
2. **Event-driven:** WHEN an existing Customer loads, THE UI SHALL query with `{ customerCode, page: 1, pageSize: 25 }` and select the first `data` item.
3. **Event-driven:** WHEN a query/save fails, THE UI SHALL prefer `message`, then `detail`, then exception text, and SHALL retain unsaved form state.
4. **Ubiquitous:** Real and mock services SHALL expose the same paginated response and backend-shaped error fields.
5. **Ubiquitous:** `CustomerProjection` SHALL declare `groupName`, `industryName`, `glName`, `constitutionName`, and `nationalityName` matching serialized backend fields.

### Requirement 8: Delivery and testing

1. **Ubiquitous:** Before applying dependent edits, THE implementer SHALL run the documented drift check and record backend/frontend status and hashes; this confirms baseline, not historical provenance.
2. **Ubiquitous:** A read-only schema preflight SHALL check required Customer table and index presence/shape only.
3. **Ubiquitous:** Backend tests SHALL cover invalid paging short-circuit, Company isolation, exact-code precedence, mixed-case code/name search, deterministic order, beyond-last metadata, 401, 403, forbidden payload atomic rejection, address adapter success/failure, and complete ProblemDetails fields.
4. **Ubiquitous:** Frontend Vitest tests SHALL cover paginated query calls, mock paging/errors, projection fields, 401/403/invalid-paging fixtures, and existing create/modify/query behavior.
5. **Complex:** WHEN auth, validation, references, configured address approval, child invariants, and persistence succeed, THE service SHALL commit Customer/children/audit together; IF any pre-commit step fails, THEN those transaction rows SHALL not persist.

## Acceptance Criteria Summary

1. Existing Customer vertical slice is preserved and only verified gaps are patched.
2. Address-bearing successful create/modify uses a real configurable adapter rather than an always-throwing production placeholder.
3. Query is validated, deterministic, Company-scoped, exact-code-precedence, and paginated.
4. ProblemDetails contains complete compatible fields.
5. Complete backend/frontend tests are supplied without inference.
6. Schema and provenance checks are explicit and safe.

## Out of Scope

- Creating a second Customer domain, page, host, route family, or catalog endpoint.
- Recovering missing FINNOVA stories or inventing unverified external address-provider details.
- Applying migrations, deployment, builds, tests, or implementation during this document-authoring step.
- Raw document/blob upload, KYC, Mind Mapping, Customer Reference, broker/outbox, AI, PayU, and non-English/RTL/localization work.
