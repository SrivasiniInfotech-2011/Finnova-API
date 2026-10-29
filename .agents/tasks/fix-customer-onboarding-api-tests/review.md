# Customer onboarding integration-test fixes

The change repairs the two reported interface contracts and supplies the repository, handler, DI, controller, gateway, test-host, aggregate, numbering, and projection wiring needed by `CustomerOnboardingApiTests`. The recorded evidence shows the targeted class passing 8/8, the full test project passing 452/452 after the FsCheck input correction, and the solution build passing with only existing warnings. Persisted Customer data keeps master references as codes while query-time projection resolves current English master names, and the change introduces no generated `bin` or `obj` artifacts. [confirmed] The added child-command handler contains one unaddressed behavioral failure outside the targeted HTTP cases.

Watch for: [confirmed] `ClearDefaultBankAccountCommand` clears every default flag and then invokes an invariant requiring exactly one default whenever accounts exist, so every clear-default request for a non-empty account collection rolls back with `ERR-CUSTOMER-DEFAULT-BANK`; define the zero-default contract or remove/defer the operation before treating the child-command slice as complete.

**Verdict**: NEEDS_CHANGES

## High-level view

The two original compiler failures are addressed with focused contract changes. `Handle(ClearDefaultBankAccountCommand, CancellationToken)` returns `Task`, matching `IRequestHandler<ClearDefaultBankAccountCommand>`, and `InMemoryNationalityRepository` now implements `GetActiveByCodeAsync` with the production repository's trimmed, active, case-insensitive lookup and cloned result. Removing the duplicate service-side repository contracts also makes the Customer handlers consume the interfaces implemented and registered by `Finnova.Repository`.

The integration path is wired through the existing SystemAdmin JWT policy and gateway alias. Create and modify persist reference codes and approved address values, customer codes come from the existing global `CUSTOMER` numbering boundary, and query projection resolves current Lookup, Nationality, and GL names without adding bilingual or persisted display-name fields. The evidence in `plan.md` records the targeted and final solution checks requested by the coder; this review did not rerun them.

[confirmed] The remaining gap is localized to the new `CustomerChildCommandHandler`: its clear-default operation is structurally registered and now compiles, but it cannot succeed for any customer that has a bank account. The current controller does not expose that command as an HTTP route, which explains why the eight targeted tests do not detect it, but the MediatR request remains part of the application surface.

<details>
<summary>Issues (1)</summary>

1. **Clear-default invariant makes the command unusable** — [confirmed] `ClearDefaultBankAccountCommand` sets all accounts to `IsDefault = false`, then `BankAccountRules.Validate` rejects every non-empty collection unless exactly one account is default. Define whether a customer may have zero defaults and adjust the invariant and handler together, or remove/defer this command instead of leaving a registered operation that always rolls back.

</details>

<details>
<summary>Details</summary>

### Repository contracts and recorded verification

The handler declaration now uses `IRequestHandler<ClearDefaultBankAccountCommand>` and its implementation is `async Task`, so the return type matches MediatR's no-result contract. The in-memory nationality fake adds `GetActiveByCodeAsync`, trims the input, filters inactive rows, compares codes case-insensitively, and returns a clone, matching the production repository boundary and preserving test isolation.

`CustomerPorts.cs` no longer declares a second set of Customer repository, audit, read, unit-of-work, and transaction interfaces. The command and query handlers import `Finnova.Repository.Interfaces`, so their constructor dependencies match the concrete registrations. The commit evidence and the end of `plan.md` record a successful targeted build, 8 passing `CustomerOnboardingApiTests`, 452 passing tests after the bounded FsCheck `DateOnly` generator fix, and a successful solution build. No build or test command was run during this review.

### Customer integration topology and data boundaries

The implementation connects the gateway alias to the authorized SystemAdmin controller and then to the MediatR Customer handlers. The test host replaces only the India Address Control boundary and ignores the EF Core InMemory transaction warning; production still uses the SQL Server repository and transaction implementation. The test data is English-only and India-specific, and the projection test verifies that renaming a Lookup row changes the response name without adding `GroupName` to `Customer`.

The added `CustomerChildMapper` and `AccountContractBoundary` registrations close the handler activation gap identified in the plan. The account/contract adapter uses the existing account repository for track prerequisites and preserves unavailable credit operations through `CustomerBoundaryUnavailableException` rather than fabricating account or contract data.

### Clear-default operation contract

[confirmed] In `CustomerChildCommandHandler.Handle(ClearDefaultBankAccountCommand, CancellationToken)`, `mapper.ClearDefault(customer)` sets every bank account's default flag to false. The handler immediately maps those rows back to requests and calls `BankAccountRules.Validate`. That validator throws `CustomerDefaultBankAccountException` whenever the collection is non-empty and the count of default rows is not exactly one. The transaction therefore rolls back for every customer with one or more accounts, making the command impossible to complete and producing the conflict code intended for invalid multi-default submissions.

The controller exposes set-default but not clear-default, so this gap is outside the recorded integration class. It remains a contract defect in an added MediatR operation, not a false alarm from an untested code path. The implementation needs a product decision: permit and test a zero-default state, change the command to select a replacement default, or defer/remove the request and its handler until its invariant is defined.

### Scope and localization

The reviewed implementation commits contain no `bin` or `obj` artifacts. The Customer test fixtures use English labels such as `India`, `Delhi`, `Single`, and `Customer Posting Group`; no bilingual fields, non-English seed values, translation scaffolding, or RTL handling were introduced.

</details>

<details>
<summary>File map</summary>

- `Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs` — implements the Customer child commands, including the corrected no-result signature and the clear-default path.
- `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs` — implements the missing active nationality lookup contract.
- `Finnova.Service/Customers/CustomerPorts.cs` and Customer handlers — unify application dependencies on repository-owned interfaces.
- `Finnova.Models/Domain/Entities/*` and `Finnova.Service/Customers/CustomerAggregateMapper.cs` — provide aggregate construction and mutation boundaries.
- `Finnova.Service/Customers/CustomerProjectionMapper.cs` and `CustomerCodeGenerator.cs` — resolve current master names and issue customer codes.
- `Finnova.Service/Customers/CustomerChildAdapters.cs` and `Finnova.SystemAdminService/Program.cs` — activate child dependencies and preserve account/contract boundary behavior.
- `Finnova.Tests/Integration/SystemAdminAppFactory.cs` and `CustomerOnboardingApiTests.cs` — configure and exercise the in-memory Customer host.
- `Finnova.SystemAdminService/Controllers/CustomerOnboardingController.cs` and `Finnova.ApiGateway/appsettings.json` — expose and route the Customer API.
- `Finnova.Tests/Unit/CustomerOnboardingRulesTests.cs` — covers Customer rules, including the corrected bounded FsCheck date inputs.

The full committed change reviewed here is the Customer implementation range from `16a9edd^..HEAD`; the current working-tree spec edits are not part of that committed implementation diff.

</details>
