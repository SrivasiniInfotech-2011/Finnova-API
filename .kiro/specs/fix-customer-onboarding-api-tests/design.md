# Design Document

## Overview

The fix keeps the existing Finnova layering and repairs the Customer Onboarding path at the smallest boundaries that explain the failures. The API host remains `Finnova.SystemAdminService`; MediatR remains the request dispatcher; EF Core repositories remain the persistence boundary; the test host remains an in-memory override of the production composition root.

The design has four parts:

1. Align Customer handlers with `Finnova.Repository.Interfaces` and preserve the two interface fixes already required by the compiler.
2. Supply the missing Customer application adapters: aggregate mapping, current-master projection, CustomerCode generation, account/contract boundary, and child mapper.
3. Make the test host deterministic and compatible with EF Core InMemory transactions, then exercise the existing HTTP contract.
4. Keep the reviewed clear-default behavior deferred. The current bank invariant remains exactly one default for every non-empty set, and no public route is added for clearing all defaults.

## What is reused and what is new

### Reused

| Existing asset | Use |
| --- | --- |
| `Finnova.SystemAdminService` | Existing authenticated Customer Onboarding host. |
| `CustomerOnboardingController` | Existing create, modify, query, and child route boundary. |
| `Finnova.Repository.Interfaces` | Single repository and unit-of-work contract family consumed by handlers. |
| `FinnovaDbContext` and existing EF repositories | Customer persistence, master reads, and audit persistence. |
| `INumberingSchemeRepository.IssueNextAsync` | Production CustomerCode generation boundary. |
| `ExceptionHandlingMiddleware` | Existing semantic HTTP/problem-code mapping. |
| `CustomerLookupTypes` | Existing bounded Lookup module/type constants used by the test seam. |
| `IIndiaAddressControl` | Existing external address boundary, replaced only by the deterministic test fake in the fixture. |
| JWT SystemAdmin policy | Existing authorization behavior. |
| xUnit, `WebApplicationFactory`, FsCheck, EF Core InMemory | Existing test and verification stack. |

### New or modified

- Remove the duplicate Customer repository/unit-of-work declarations from `Finnova.Service/Customers/CustomerPorts.cs` if they remain.
- Add repository-interface imports to Customer command/query files that currently bind to the duplicate contracts.
- Add focused aggregate factory and mutation methods to the Customer aggregate and owned children so production mappers do not use reflection or test-only mutation.
- Add `CustomerAggregateMapper`, `CustomerProjectionMapper`, and `CustomerCodeGenerator` under `Finnova.Service/Customers`.
- Register the Customer application ports and child adapters in `Finnova.SystemAdminService/Program.cs`.
- Configure the InMemory transaction warning and seed the global English `CUSTOMER` numbering scheme in `SystemAdminAppFactory`.
- Keep `CustomerChildCommandHandler` as `async Task` for the no-result clear-default command, but defer its unusable zero-default operation rather than changing the invariant.
- Add or preserve the targeted integration assertions and the focused rule/contract tests.

## Requirements coverage map

| Requirement | Design coverage |
| --- | --- |
| R1 handler return type | `CustomerChildCommandHandler` implements `IRequestHandler<ClearDefaultBankAccountCommand>` with `Task Handle`. |
| R2 nationality repository | `InMemoryNationalityRepository.GetActiveByCodeAsync` mirrors active, trimmed, case-insensitive clone semantics. |
| R3 one repository contract | Service handlers import `Finnova.Repository.Interfaces`; duplicate Service declarations are removed. |
| R4 handler activation | SystemAdmin DI registers aggregate/projection/code/child/account adapters and repository implementations. |
| R5 generated code | `CustomerCodeGenerator` calls Document Number Control with `CUSTOMER` global scope and validates length/content. |
| R6 master validation | Existing reference validator and repository boundaries run before aggregate mutation. |
| R7 projection-only names | `CustomerProjectionMapper` resolves current Lookup/Nationality/GL values without adding persisted name properties. |
| R8 atomic writes | Existing Customer unit-of-work and middleware transaction behavior remain in place; InMemory warnings are ignored only by the test host. |
| R9 HTTP/auth/error contract | Existing controller, JWT policy, middleware, and route are retained. |
| R10 clear-default decision | Exactly-one-default validation remains for non-empty sets; clear-default is not exposed or reinterpreted in this ticket. |

## Architecture and request flow

```text
HTTP client
   -> CustomerOnboardingController
   -> JWT/SystemAdmin policy
   -> MediatR command/query
   -> FluentValidation + Customer reference validator
   -> aggregate mapper / code generator
   -> Customer repository + unit of work + audit repository
   -> FinnovaDbContext

Query path:
HTTP -> query handler -> Customer repository -> current-master projection mapper
```

Create and modify validate all requested master references before calling the aggregate mapper. The mapper persists codes and identifiers, not display names. The code generator issues a value through the existing numbering repository and validates the result before the aggregate is saved. The unit of work commits the aggregate and audit row together.

Query uses the persisted Customer reference values and calls the active master repositories for current labels. The projection mapper is asynchronous so a renamed active Lookup row appears in a later query without changing the Customer row.

## Data models

### Persisted Customer aggregate

The existing Customer aggregate remains the source of truth for Customer-owned values and stable references. It contains `CustomerCode`, `CompanyId`, flags, names, approved Customer fields, reference codes/IDs, and child collections. It does not contain `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName`.

Owned child entities use explicit initialization/mutation methods compatible with EF Core private setters:

- `CustomerAddress` stores approved address values returned by India Address Control.
- `IndividualCustomerDetails` and `NonIndividualCustomerDetails` store Customer-owned detail values.
- `BankAccount` stores account data and the `IsDefault` flag.
- `CustomerDocumentReference` stores document-definition IDs/codes and DMS metadata, not raw binary.
- `CustomerTrackEntry` and `FactoringSubLimit` store their approved Customer-owned values.
- `CustomerAuditEntry` stores a safe action snapshot.

### Response projections

`CustomerResponse` may include nested current display objects, `GroupName`, `NationalityName`, and `GLName` because these are response fields. The projection mapper builds those fields from current master rows. The response is not passed back into persistence as an entity.

### Test-only seed data

The test fixture seeds only English values and India-compatible data:

- Lookup labels such as `Group One`, `Industry One`, `Individual`, `Mr`, and `Single`.
- Nationality label `India`.
- GL label `Customer Posting Group`.
- A global `CUSTOMER` numbering scheme with a format that emits 12 alphanumeric characters.
- Optional applicable Document Master rows such as `PAN`.

## Contracts and interfaces

The implementation preserves these signatures:

```csharp
public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(string scope, int length, CancellationToken ct = default);
}

public interface ICustomerProjectionMapper
{
    Task<CustomerResponse> ProjectAsync(Customer customer, CancellationToken ct = default);
}

public sealed record ClearDefaultBankAccountCommand(
    string CustomerCode,
    string ActorId) : IRequest;
```

The repository-owned Customer interfaces are imported from `Finnova.Repository.Interfaces`. No parallel Service-owned `ICustomerRepository` or `ICustomerUnitOfWork` contract remains.

### CustomerCode generation contract

`CustomerCodeGenerator` adapts the existing numbering repository. It requests the next global `CUSTOMER` value, applies the existing formatter/boundary convention, and calls `CustomerCodeRules.ValidateGenerated`. A missing or malformed value throws a safe application exception before persistence. It does not accept a code from the request.

### Projection contract

`CustomerProjectionMapper` accepts a cancellation token and resolves current active names through existing repository methods. A missing or inactive master follows the existing query projection policy and does not write a fallback name into Customer.

## Component and interface descriptions

### `CustomerChildCommandHandler`

Handles child commands and keeps the no-response command signature as `async Task`. The clear-default branch remains internally guarded by the existing invariant. Because no public route uses it and the review finding requires a product decision about zero-default state, the ticket does not make that operation successful by weakening `BankAccountRules.Validate`.

### `CustomerAggregateMapper`

Converts validated request records and normalized reference results into aggregate factory/mutation calls. It owns no master lookup. It creates safe audit snapshots and maps the persisted aggregate into a response using already resolved reference data for write responses.

### `CustomerProjectionMapper`

Loads current names from Lookup, Nationality, and GL repositories. It does not mutate the aggregate and does not add navigation properties solely to hold display names.

### `CustomerCodeGenerator`

Uses `INumberingSchemeRepository` and the existing Document Number Control format. It validates exactly 12 alphanumeric characters and propagates cancellation.

### `CustomerChildMapper`

Maps bank, document, track, factoring, and address-copy requests into explicit entity mutation methods. It is registered in the SystemAdmin host so child handlers can activate in integration tests.

### `AccountContractBoundary`

Uses the existing account repository for track prerequisites. Credit operations retain the existing `CustomerBoundaryUnavailableException` behavior because the test fixture must not fabricate account/contract credit data.

### `SystemAdminAppFactory`

Replaces the SQL provider with EF Core InMemory for the test host, ignores only `InMemoryEventId.TransactionIgnoredWarning`, and seeds an idempotent global numbering scheme. It leaves production registrations and transactions unchanged.

## Error handling

- Missing active Lookup, Nationality, GL, or applicable Document reference: existing Customer 400 problem code.
- Invalid default-bank collection: existing 409 `ERR-CUSTOMER-DEFAULT-BANK`.
- Unknown Customer modify: existing 404 `ERR-CUSTOMER-404`, with the requested code omitted from the safe response body.
- Missing/malformed generated code: safe Customer validation/conflict error before persistence.
- Handler activation or missing DI adapter: fixed by composition-root registration, not converted into a business error.
- InMemory transaction warning: suppressed only in the fixture because the provider ignores transactions; SQL Server production behavior is unchanged.

All exceptions avoid raw identity-document values, access tokens, secrets, SQL, or external payloads.

## Security

The existing SystemAdmin JWT policy remains the only authorization gate for Customer endpoints. Repository and application services keep scoped lifetimes. The test fake replaces only India Address Control and does not bypass authorization or Customer reference validation. Generated CustomerCode is server-owned and immutable. Display names are resolved from authoritative masters rather than accepted from the caller. Test seed data contains no production PII or non-English values.

## Design decisions and tradeoffs

### Repository contract unification

Using the repository-owned interfaces is safer than adapting every concrete repository to a duplicate Service contract. It removes an activation ambiguity and preserves the existing repository registration boundary. The tradeoff is a small source-level namespace cleanup in Customer handlers.

### Async projection

Making projection asynchronous matches the repository APIs and allows query-time current names. A synchronous projection would either block, return stale values, or require a second persistence model. The tradeoff is a small handler/interface signature change with no HTTP contract change.

### Numbering repository versus local counter

The existing Document Number Control repository is the approved source and preserves production uniqueness/scope. A local counter would make tests pass but would create a production correctness gap. The tradeoff is test fixture setup for a global `CUSTOMER` scheme.

### Clear-default behavior

The design deliberately does not reinterpret an exactly-one-default rule as a zero-or-one rule. That would change business behavior and conflict with the existing create/modify invariant. The current ticket keeps the command unexposed and records the required product decision for a later ticket. If product later approves zero defaults, that later change must update the invariant, handler, API contract, and tests together.

### Test-host transaction warning

Ignoring `InMemoryEventId.TransactionIgnoredWarning` is limited to the in-memory test provider. Removing transactions from production code would be a broader and unsafe behavior change, so it is not chosen.

## Implementation-ordered task list

1. Align repository contracts and retain the two reported interface fixes.
2. Add explicit Customer aggregate factory/update methods required by the mapper.
3. Add Customer request/response aggregate mapper and safe audit mapping.
4. Add CustomerCodeGenerator using Document Number Control.
5. Add asynchronous current-master projection and update query handler.
6. Add/register AccountContractBoundary and CustomerChildMapper.
7. Register all Customer application ports in SystemAdmin and configure test-host InMemory transactions/numbering seed.
8. Preserve and update the targeted integration tests, including the 409 default-bank expectation and current-name projection assertion.
9. Add focused unit tests for repository contract semantics, CustomerCode validation, projection non-persistence, and clear-default deferral.
10. Later, run the targeted integration class, affected test project, and solution build; record exact results in the task evidence.
