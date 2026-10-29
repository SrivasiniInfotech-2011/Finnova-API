# Design Document — Customer Master / Customer Onboarding

**Feature:** `customer-master` gap-only patch  
**Backend:** `e:\Finnova\Finnova-API`  
**Frontend:** `E:\Finnova\Finnova-UI\Finnova-UI`  
**Ground truth:** `.agents\tasks\customer-master-spec\investigation.md`

## Overview

The verified Customer Onboarding vertical slice already contains the domain, contracts, persistence, write/child CQRS, API routes, gateway alias, React page, tabs, routes, navigation, and test host. This design changes only the gaps: it validates paging before repository access, makes repository search deterministic and provider-consistent, returns the exact shared `PaginatedResponse<T>` contract, adds `message` to Customer ProblemDetails, aligns real/mock UI services and page consumption, and replaces the production always-throwing India Address Control placeholder with a configurable HTTP adapter. The external provider remains a boundary: no endpoint or credential value is invented.

The locked stack is ASP.NET Core/.NET, MediatR, FluentValidation, EF Core/SQL Server, xUnit/FsCheck/Moq/WebApplicationFactory, React 18, TypeScript, Vite, MUI, Axios, Vitest, and React Testing Library. No Redux, RxJS, RTL, bilingual fields, or non-English fixtures are introduced.

## Scope and drift gate

This is deliberately a **gap-only patch**, not a self-contained recreation of all existing Customer stages. Before Task 1, the implementer must run the drift check in `tasks.md` and record `git status --short`, the current commit, and hashes of the named backend/frontend baseline files. If a file differs from the investigation in a way that changes a signature or route, stop and reconcile it before applying dependent snippets. The check proves the starting baseline for the user's implementation; it cannot prove when earlier frontend modifications occurred. Assumption A14 preserves that provenance limitation.

## Reused versus changed

| Area | Reused | Changed |
|---|---|---|
| Domain/exceptions/contracts | Existing Customer aggregate, children, enums, exceptions, `CustomerContracts.cs` | No new Customer entity; raw forbidden payload guard is added at the Customer HTTP boundary. |
| EF | Existing nine configurations, DbSets, relationships, and assembly discovery | No EF model change; read-only table/index preflight only. |
| Repository | Existing `ICustomerReadRepository` tuple and child includes/AsNoTracking | Exact-code precedence, uppercase normalization, Company filter, deterministic order, validated paging. |
| CQRS | Existing write/child handlers, mappers, validation pipeline | `QueryCustomerValidator`; query handler returns paginated envelope. |
| Address boundary | Existing `IIndiaAddressControl` and `AddressControlResult` | Replace `IndiaAddressControlDeferredAdapter` with `IndiaAddressControlHttpAdapter` using named `HttpClient`, options, timeout, safe mapping, and explicit failure semantics. |
| API/middleware/gateway | Existing routes, auth policy, alias, typed Customer exception switch | Query return type, `message` extension, and forbidden-payload guard wiring. |
| Frontend | Existing page/tabs/routes/nav and shared Axios | Typed paginated query, real/mock parity, error fallback, barrels. |
| Tests | Existing fixtures and fake | Complete integration replacement plus adapter unit tests and complete frontend page/service tests. |

## Requirements coverage map

| Requirement | Design coverage |
|---|---|
| R1 ownership/payload safety | Existing reference-only contracts, projection mapper, HTTP payload guard, no raw bytes. |
| R2 create/modify | Existing commands and unit of work plus configured address adapter before mutation. |
| R3 Address Control | `IIndiaAddressControl`, `IndiaAddressControlOptions`, HTTP request/response DTO mapping, timeout/status/JSON mapping. |
| R4 children/audit | Existing validators, mapper, audit repository, transaction, integration rollback tests. |
| R5 query | Repository change, `QueryCustomerValidator`, `QueryCustomer` envelope, UI query params. |
| R6 HTTP/security | Middleware `message`, existing JWT/SystemAdmin, YARP alias, complete ProblemDetails assertions. |
| R7 frontend | Model/interface/real/mock/selector/barrels/page and Vitest replacements. |
| R8 delivery | Drift check, table/index preflight, ordered tasks, backend/frontend tests. |

## Request flow and boundaries

UI calls the shared Axios instance. Its base path composes `/api/ua/api/customer-onboarding`; YARP rewrites the existing alias to `/api/customer-onboarding` on `systemadmin-cluster`. ASP.NET authentication and the `SystemAdmin` policy run before the controller. Customer payload guard rejects forbidden property names before controller work. MediatR validation rejects invalid paging before the repository. Query uses `ICustomerReadRepository`, maps each entity through `ICustomerProjectionMapper`, and returns the shared envelope. Create/modify use the existing reference validator, which calls `IIndiaAddressControl` for every submitted address before the aggregate is mutated.

## Data models and contracts

The existing query request remains:

```csharp
public sealed record CustomerQueryRequest(
    string? CustomerCode, string? Search, Guid? CompanyId,
    int Page = 1, int PageSize = 25);
```

The exact existing shared response is:

```csharp
public record PaginatedResponse<T>(
    List<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
```

The handler passes a projected `List<CustomerResponse>`, not an `IReadOnlyCollection<T>`. Zero rows use `TotalPages = 0`; a valid beyond-last page has empty `Data` and retains requested page metadata.

The existing `CustomerResponse` already contains `GroupName`, `IndustryName`, `GLName`, `ConstitutionName`, and `NationalityName`. System.Text.Json emits lower camel case, so the frontend declares `groupName`, `industryName`, `glName`, `constitutionName`, and `nationalityName`.

## India Address Control adapter

The internal contract remains `IIndiaAddressControl`:

```csharp
Task<AddressControlResult> ValidateAndApproveAsync(
    CustomerAddressRequest request, CancellationToken ct = default);
Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(
    CustomerAddressResponse communication, CancellationToken ct = default);
```

New internal configuration is `IndiaAddressControlOptions` with `BaseUrl`, `ValidationPath` defaulting to `/api/v1/address/validate`, `ApiKey`, and `TimeoutSeconds` defaulting to 10. The named client `IndiaAddressControl` sends JSON to `BaseUrl + ValidationPath` with `X-Api-Key` only when configured. The request JSON is the existing Indian address fields in one plain English object. The assumed provider response is `{ isValid, errors, approvedAddress }`, where `approvedAddress` has the address response fields. This shape is an explicit integration assumption, not a claim about a live provider.

The adapter maps HTTP 200 with `isValid=false` to `AddressControlResult(false, errors, null)`, HTTP 200 with valid `approvedAddress` to an approved result, and malformed success data to `CustomerBoundaryUnavailableException`. Timeout, cancellation caused by the adapter timeout, non-success status, missing BaseUrl, and transport failure map to `CustomerBoundaryUnavailableException("India Address Control")`. The adapter never logs API keys, raw payloads, or full provider responses. `CopyCommunicationToPermanentAsync` remains local and does not call the provider.

The external owner must supply the actual URL, API key/credential mechanism, final request/response mapping, certificate/proxy policy, and SLA. A configured adapter is production wiring; successful live integration still depends on those deployment values and provider compatibility.

## Repository and CQRS

`CustomerReadRepository.SearchAsync` keeps its existing includes and returns `(IReadOnlyCollection<Customer> Items, int Total)`. CompanyId is applied first. A nonblank trimmed CustomerCode is uppercased and matched exactly; Search is skipped. Otherwise a trimmed uppercase Search term is matched against uppercased CustomerCode, FirstName, and SecondName. Count occurs before `OrderBy(CustomerCode).Skip().Take()`. The validator owns bounds `Page 1..20,000,000` and `PageSize 1..100`; the repository no longer clamps invalid input.

`QueryCustomerValidator` is discovered by existing assembly scanning. `QueryCustomerHandler` calls the repository, projects each returned entity into a `List<CustomerResponse>`, computes ceiling total pages (or zero), and constructs the exact shared record. It has no writes.

## API, middleware, gateway, and payload safety

The existing controller retains all routes and only changes GET query's return type to `Task<PaginatedResponse<CustomerResponse>>`. Middleware retains typed exception mappings and sets `Extensions["code"]` and `Extensions["message"]`, with `message == detail`.

`CustomerPayloadGuardMiddleware` is path-scoped to `/api/customer-onboarding` and only inspects JSON object property names in request bodies. It rejects a case-insensitive denylist: `rawDocumentBytes`, `documentBytes`, `accessToken`, `refreshToken`, `password`, `clientSecret`, `sql`, and `externalPayload`. It returns 400 `ERR-CUSTOMER-400` with safe detail/message and does not invoke the next delegate. It buffers and rewinds allowed bodies. This is a transport guard, not a replacement for DTO validation.

## Error handling

- Invalid page/page size: FluentValidation 400, `ERR-CUSTOMER-400`; repository is never called.
- Unknown/inactive Customer references: existing typed Customer 400/409 codes; no mutation.
- Address validation rejection: 400 `ERR-CUSTOMER-ADDRESS-REF`, safe generic detail; no mutation.
- Address configuration/timeout/provider transport/non-success/malformed success: 503 `ERR-CUSTOMER-503`; retry after configuration/provider recovery.
- Forbidden payload: 400 `ERR-CUSTOMER-400`, safe detail/message; no Customer or audit rows.
- Unknown Customer: existing 404; immutable/duplicate/default-bank: existing 409.
- No query results/beyond-last: 200 envelope, not an error.
- Missing/invalid JWT: 401; valid non-admin: 403.
- Frontend errors: shared Axios handles transport/toast; page uses message, detail, then exception text and does not discard form state.

No raw provider body, credential, token, SQL, raw document, or unnecessary PII is logged or returned. EF/database constraints remain the final uniqueness authority.

## Security and privacy

JWT validation and `SystemAdmin` authorization remain unchanged. The gateway forwards Authorization. The adapter API key is configuration-only and is never placed in request contracts, logs, or ProblemDetails. The payload guard rejects secrets and external payloads. Customer audit JSON remains the existing safe snapshot. UI uses only `src/services/api.ts`.

## Migration and schema preflight

No Customer migration was found and no live database was inspected. The supported release state is an already provisioned schema owned outside this ticket. The Task 1 release-only SQL checks required table presence and required index presence/uniqueness/key-column order/normalized filter shape. It does not claim to compare columns, types, nullability, primary keys, or foreign keys; requirements are deliberately narrowed to table/index presence/shape. Any mismatch stops deployment and goes to the schema owner for a reviewed baseline/adoption migration. No `migrations add` is run against live schema state.

## Design decisions and tradeoffs

1. Gap-only scope avoids duplicating verified source while the authoritative story is absent.
2. A real configurable address adapter is chosen because successful address-bearing writes are in scope; provider details remain explicit assumptions.
3. The tuple repository boundary is retained; the handler owns projection and metadata.
4. Uppercase normalization gives SQL Server and EF InMemory equivalent matching at some index cost.
5. `message` duplicates safe `detail` to satisfy the existing Axios interceptor without abandoning RFC `detail`.
6. The catalog call is removed rather than inventing an absent backend endpoint.
7. Existing test fakes remain deterministic test doubles; they are not evidence of production provider readiness.

## Testability

Pure paging validation is unit tested. Repository behavior is exercised through the existing InMemory/WebApplicationFactory host. Integration tests cover Company isolation, exact precedence, mixed-case code/name searches, order, page metadata, 401/403, invalid paging short-circuit behavior, forbidden payload atomicity, address-bearing create/modify, and all ProblemDetails fields. Adapter tests use a fake `HttpMessageHandler` for approved, rejected, timeout, non-success, malformed, and missing configuration outcomes without a live provider. Frontend real-service tests assert exact Axios calls and legacy normalization. Mock tests assert paging, Company/search behavior, invalid paging, not-found, and complete five-field errors. Page tests use envelope fixtures for every query result and error fixtures with `message` and `detail`.

## Implementation-ordered task list

The numbering is identical to `tasks.md`; Task 1 is the first executable task.

1. Drift/baseline check, EF/DbContext verification, and release-only table/index preflight.
2. Repository exact-code precedence, normalized search, Company filtering, and deterministic paging.
3. Query validator and paginated handler.
4. Configurable India Address Control HTTP adapter, options, named client, and SystemAdmin DI wiring.
5. Customer payload guard, controller query contract, middleware message field, and gateway verification.
6. Complete backend unit/integration tests, including adapter and forbidden-payload tests.
7. Frontend model, interface, real/mock services, selector, and per-file barrels.
8. Frontend page query/error consumption.
9. Complete frontend page/service Vitest tests.

No task edits an unlisted application path, and the authoring workflow does not execute implementation commands.
