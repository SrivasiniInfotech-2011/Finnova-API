# Requirements Document

## Introduction

This specification resolves the backend Customer Onboarding integration-test failures in `e:\Finnova\Finnova-API`. The targeted acceptance surface is `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`, hosted by the existing SystemAdmin `WebApplicationFactory` and reached through the Customer Onboarding controller and gateway-compatible route.

The work covers the two reported compile-contract failures and the complete request path needed by the targeted integration class: authorized create, modify, query, master-reference validation, generated CustomerCode, atomic rollback, current-master projection, and safe error responses. It remains backend-only because the failing test class is a backend integration suite. No frontend file, generated artifact, migration, or unrelated master-data refactor is part of this feature.

Finnova remains India-only and English-only. The implementation stores master references as codes or identifiers and resolves display names at query time. It adds no bilingual fields, non-English seed values, translation infrastructure, RTL handling, or alternate frontend state-management library.

The review iteration identified one separate operation-contract gap: `ClearDefaultBankAccountCommand` clears every default flag and then invokes an invariant requiring exactly one default whenever accounts exist. The current Customer Onboarding controller does not expose that command as an HTTP operation. This specification preserves the current public behavior by deferring the command until product confirms whether zero defaults are valid. The targeted create and modify flows continue to require exactly one default when bank accounts exist.

## Glossary

- **Customer Onboarding API**: The SystemAdmin-hosted HTTP endpoints for Customer create, modify, and query behavior.
- **Targeted integration class**: `CustomerOnboardingApiTests` in `Finnova.Tests/Integration`.
- **Customer aggregate**: `Customer` and its owned address, bank, document, track, factoring, and detail records.
- **Repository contract**: The interfaces in `Finnova.Repository.Interfaces` implemented by repository classes and registered by repository dependency injection.
- **Service port**: An application-layer interface owned by `Finnova.Service`, such as an aggregate mapper, projection mapper, code generator, or child adapter.
- **Master reference**: A Customer code or identifier validated against an authoritative Lookup, Nationality, GL, or Document repository.
- **Read projection**: A response assembled from persisted Customer data plus current master display values. A projection does not write those display values back to Customer.
- **CustomerCode**: The generated immutable 12-character alphanumeric Customer business code.
- **Default bank invariant**: A non-empty bank-account collection has exactly one default account during create, modify, add, and set-default operations.
- **Clear-default command**: The internal `ClearDefaultBankAccountCommand`, which currently has no public Customer Onboarding route.
- **Atomic request**: A request whose Customer, child rows, and audit rows either all commit or all roll back.
- **Test host**: `CustomerOnboardingAppFactory`, derived from `SystemAdminAppFactory`, using EF Core InMemory and the deterministic India address fake.
- **EARS**: Easy Approach to Requirements Syntax, using ubiquitous, event-driven, state-driven, unwanted-event, optional-feature, and complex forms.

## Dependencies and Assumptions

### Dependencies

- `Finnova.Backend.slnx` remains the solution entry point.
- `Finnova.SystemAdminService` remains the host for Customer Onboarding.
- `Finnova.Repository` remains the owner of EF Core context, repository implementations, and repository DI registration.
- `Finnova.Service` remains the owner of MediatR handlers, service ports, validators, mappers, and application adapters.
- `Finnova.Tests` remains the xUnit integration and unit-test project.
- `ILookupRepository`, `INationalityRepository`, `IGLAccountRepository`, and the Document repository remain the authoritative master-data boundaries.
- The existing Document Number Control repository remains the source for CustomerCode generation. No GUID or local in-memory counter is introduced in production.
- The existing JWT SystemAdmin policy, exception middleware, gateway routing convention, and shared repository/service DI conventions remain authoritative.
- EF Core InMemory is used only by the test host. Production transactions and SQL Server behavior remain unchanged.

### Assumptions for confirmation

- **A1 — Backend-only scope:** No frontend change is required because the failing class exercises the backend host directly. The UI repository remains unchanged.
- **A2 — Existing implementation seam:** The two reported interface fixes are required and must remain: `CustomerChildCommandHandler.Handle(ClearDefaultBankAccountCommand, CancellationToken)` returns `Task`, and `InMemoryNationalityRepository.GetActiveByCodeAsync` returns an active, case-insensitive, trimmed-code clone.
- **A3 — Repository contract authority:** The duplicate Customer repository/unit-of-work declarations in `Finnova.Service` are removed or unused. Customer handlers use the interfaces in `Finnova.Repository.Interfaces`, which are the contracts implemented and registered by `Finnova.Repository`.
- **A4 — Customer code source:** A global `CUSTOMER` numbering scheme is available to the integration fixture. The scheme format produces exactly 12 alphanumeric characters, for example `CUST{SEQ:8}`. Production code rejects missing or malformed output.
- **A5 — Test data:** Seeded lookup, nationality, GL, document, address, and numbering values are English-only and India-compatible. Test data is scoped to each in-memory host and is idempotent where repeated tests can reuse the same database.
- **A6 — Default-bank decision:** The public create/modify contract continues to require exactly one default when bank rows exist. The internal clear-default command is deferred because making zero defaults valid would change the bank invariant and may change business behavior. No HTTP route is added for it in this ticket.
- **A7 — Projection policy:** Query responses resolve current active master names. Persisted Customer rows contain codes/IDs only and never `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName` properties.
- **A8 — Error contract:** The existing middleware mapping is authoritative. Reference errors use HTTP 400 and Customer reference codes; unknown Customer modify uses HTTP 404; the default-bank invariant uses HTTP 409 with `ERR-CUSTOMER-DEFAULT-BANK`.
- **A9 — No schema change beyond the already planned Customer slice:** If the repository already contains the Customer aggregate and EF configuration from the implementation plan, this ticket wires and validates that slice rather than adding unrelated entities. Any required migration is outside the test-host fix and must be handled separately.

## Functional Requirements

### Requirement 1: Preserve the reported MediatR handler contract (Ubiquitous)

THE Customer child handler SHALL implement `IRequestHandler<ClearDefaultBankAccountCommand>` with `Handle(ClearDefaultBankAccountCommand, CancellationToken)` returning `Task`.

THE implementation SHALL not return `Task<Unit>` or another response-shaped task for a command declared as `IRequest`.

### Requirement 2: Preserve the nationality repository contract (Ubiquitous)

THE in-memory nationality repository SHALL implement `GetActiveByCodeAsync(string, CancellationToken)` with the same observable semantics as the production repository: trim the requested code, ignore inactive rows, compare codes case-insensitively, return null when no active row exists, and return a detached clone when a row exists.

### Requirement 3: Use one Customer repository contract (State-driven)

WHILE Customer handlers are resolved by the SystemAdmin host, their repository and unit-of-work constructor parameters SHALL use the interfaces implemented by `Finnova.Repository` and registered by its DI extension.

THE service project SHALL not leave a duplicate repository contract that makes the registered repository implementation unassignable to a handler constructor.

### Requirement 4: Compose the Customer request path (Event-driven)

WHEN an authorized caller posts a valid Customer create request, THE host SHALL construct the controller, MediatR handler, reference validator, aggregate mapper, code generator, unit of work, audit repository, and required child adapters from DI and SHALL reach the intended create behavior.

WHEN an authorized caller modifies an existing Customer, THE host SHALL resolve the same repository-owned contracts and SHALL preserve the generated CustomerCode.

WHEN an authorized caller queries an existing Customer, THE host SHALL return a response projection rather than fail during mapper activation.

### Requirement 5: Generate and preserve CustomerCode (Event-driven)

WHEN a Customer is created, THE application SHALL request the next value from the existing global `CUSTOMER` numbering boundary and SHALL validate that the result is exactly 12 alphanumeric characters.

WHEN a Customer is modified, THE application SHALL keep the existing CustomerCode unchanged regardless of other editable fields.

THE application SHALL not use a request-supplied code, GUID formatting, or a production-local counter as the CustomerCode source.

### Requirement 6: Validate master references before mutation (Event-driven)

WHEN create or modify receives a Lookup, Nationality, GL, or applicable Document reference, THE reference validator SHALL resolve it against the active authoritative repository before changing the Customer aggregate.

THE validator SHALL reject inactive or unknown references with the existing semantic Customer error code family and SHALL not rely on syntactic code shape alone.

### Requirement 7: Keep write data separate from display projections (Ubiquitous)

THE Customer aggregate SHALL persist reference codes/IDs and approved Customer-owned values only.

THE Customer response MAY contain current Group, Industry, Constitution, Nationality, and GL display names resolved at query time.

WHEN a master display value changes after Customer creation, THE next query SHALL return the current name while the stored Customer reference remains unchanged.

### Requirement 8: Make create and modify atomic (Unwanted-event)

IF child validation, reference validation, mapping, audit persistence, or database persistence fails, THEN the request SHALL roll back Customer, child, and Customer audit changes as one unit.

IF two bank rows are submitted as default, THEN the request SHALL return HTTP 409 with `ERR-CUSTOMER-DEFAULT-BANK` and SHALL add neither the Customer nor an audit row.

### Requirement 9: Preserve response and authorization behavior (Complex)

WHEN the targeted integration class runs, THE API SHALL preserve JWT SystemAdmin authorization, route behavior, response contracts, middleware problem details, safe not-found behavior, and the existing gateway-compatible path.

Unauthorized create SHALL return 401. Unknown or inactive references SHALL return the existing 400 problem contract. Unknown modify SHALL return 404 without echoing the requested CustomerCode in the response body.

### Requirement 10: Preserve the unresolved clear-default boundary (Optional-feature)

WHERE a caller attempts to use `ClearDefaultBankAccountCommand` internally, THE current exactly-one-default invariant SHALL remain unchanged for non-empty bank collections until product confirms whether a zero-default state is valid.

THE current ticket SHALL not expose a new clear-default HTTP route or silently change the invariant. A later ticket MAY either define a valid zero-default state and update validation, or remove/defer the command and its registration.

## Non-Functional Requirements

- The targeted integration class SHALL be runnable with the existing xUnit/WebApplicationFactory stack.
- The targeted implementation SHALL use cancellation tokens on repository and projection calls where existing contracts support them.
- Error messages and audit snapshots SHALL not include raw document content, secrets, tokens, SQL, or unnecessary PII.
- EF Core InMemory transaction warnings SHALL be handled only in the test host; production SQL transactions SHALL remain enabled.
- No generated `bin` or `obj` file is a deliverable.
- No frontend file, Redux store, RxJS stream, bilingual field, non-English seed value, or RTL behavior is added.

## Acceptance Criteria

1. The two reported interface diagnostics are absent when the affected solution/test project is later built.
2. The SystemAdmin test host can activate the Customer Onboarding controller and all handlers used by the targeted class.
3. `CustomerOnboardingApiTests` covers and passes unauthorized create, unknown lookup, generated immutable code, atomic default-bank failure, current-name projection, inactive/unknown references, missing applicable document, and unknown modify.
4. The generated CustomerCode is 12 alphanumeric characters and remains unchanged through modify.
5. A successful create followed by a master rename returns the renamed display value without adding a persisted master-name property.
6. Failed requests do not add Customer or CustomerAuditEntry rows.
7. The default-bank failure remains HTTP 409 with `ERR-CUSTOMER-DEFAULT-BANK`.
8. The clear-default review finding is explicitly deferred or resolved without changing the public behavior in this ticket.
9. The implementation adds no frontend or generated-artifact changes.
10. Later user-run verification records the narrow targeted test command, affected-project build/test result, full-project status if run, and any environmental blocker in the task evidence.

## Out of Scope

- Frontend Customer pages, services, routes, navigation, or Vitest tests.
- New Customer Reference, Mind Mapping, PayU, Python Cron, MassTransit, AI, or behavior-model features.
- Changing JWT policy, gateway host separation, or public route shape.
- Making zero-default bank accounts valid without explicit product confirmation.
- Modifying generated `bin` or `obj` artifacts.
- Unrelated master-data refactors, migrations, or broad repository cleanup.
