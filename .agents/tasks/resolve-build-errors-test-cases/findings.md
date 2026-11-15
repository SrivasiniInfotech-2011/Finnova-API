# Findings: Resolve build errors and test cases

## Scope and method

- **Ticket:** Resolve the build errors and test cases.
- **Backend inspected:** `d:\Projects\Finnova\Finnova-API`.
- **Backend solution:** `d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx`.
- **Frontend requested:** `E:\Finnova\Finnova-UI\Finnova-UI`.
- **Inspection performed:** read-only inspection of repository files, project files, specifications, task reports, source references, and current worktree status.
- **Commands deliberately not run:** `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet ef`, `npm install`, `npm run build`, `npm test`, migrations, database access, and HTTP requests.
- **Confidence labels:**
  - **Confirmed from artifact:** explicitly recorded in a task report or review artifact.
  - **Inferred from source:** observable in the current source without executing it.
  - **Unknown:** cannot be established from files alone.

The current backend worktree is dirty on `main` (`main...origin/main`). The status lists modified host/gateway files and untracked Loan Account, service-auth, and test files. Those changes were not altered.

## Repository and build configuration

### Finding 1 — Backend solution and project topology

- **Path:** `d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx:2-9`.
- **Error/test name:** None; repository topology.
- **Evidence:** The solution contains eight projects: `Finnova.AccountsService`, `Finnova.ApiGateway`, `Finnova.Models`, `Finnova.Repository`, `Finnova.Service`, `Finnova.Tests`, `Finnova.SystemAdminService`, and `Finnova.UAService`.
- **Confidence:** Confirmed from source.

The relevant directory structure is:

- `Finnova.Models`: `Contracts`, `Domain/Entities`, `Domain/Enums`, `Domain/Exceptions`, `Domain/Numbering`, and `Domain/Roles`.
- `Finnova.Repository`: `Configuration`, `Context`, `Interfaces`, `Repositories`, and `Migrations`.
- `Finnova.Service`: MediatR handlers and validators grouped by feature, including `Auth`, `Customers`, `IdentityVerification`, `JVPosting`, `LoanAccounts`, `Nationality`, `OrganizationHierarchy`, `UserManagement`, and other feature slices.
- `Finnova.SystemAdminService`: authorized API host with `Controllers`, `Middleware`, `ServiceAuth`, and `Program.cs`.
- `Finnova.AccountsService`: Accounts/Application Processing/Loan Accounts host with `Controllers`, `Middlewares`, and `Program.cs`.
- `Finnova.UAService`: user-authentication host with `Controllers` and `Program.cs`.
- `Finnova.ApiGateway`: YARP host with `Program.cs`, `appsettings.json`, and development settings.
- `Finnova.Tests`: `ApplicationProcessing`, `Infrastructure`, `Integration`, `JVPosting`, `LoanAccounts`, and `Unit` test areas.

No root `AGENTS.md`, `CONTRIBUTING.md`, or root `README.md` was found. The only README located in the backend workspace is `k8s/README.md`. The mandatory steering files are `.kiro/steering/product-context.md` and `.kiro/steering/spec-workflow.md`.

### Finding 2 — Backend target frameworks and package/test configuration

- **Paths:** all eight `*.csproj` files and `Finnova.Tests/Finnova.Tests.runsettings`.
- **Error/test name:** Build and test configuration.
- **Evidence:** Every project targets `net10.0` with nullable enabled. `Finnova.Service` has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. The web hosts use `Microsoft.NET.Sdk.Web`.
- **Confidence:** Confirmed from source.

`Finnova.Tests/Finnova.Tests.csproj` contains:

- `Microsoft.NET.Test.Sdk` 17.11.1.
- `xunit` 2.9.2.
- `xunit.runner.visualstudio` 2.8.2.
- `xunit.extensibility.core` 2.9.2.
- `Moq` 4.20.72.
- `FsCheck.Xunit` 2.16.6.
- `FluentValidation` 11.11.0.
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.11.
- `Microsoft.EntityFrameworkCore.InMemory` 10.0.0.
- JWT package `System.IdentityModel.Tokens.Jwt` 8.19.2.
- Project references to `Finnova.Service`, `Finnova.Models`, `Finnova.Repository`, `Finnova.SystemAdminService`, and `Finnova.AccountsService`.
- `RunSettingsFilePath` set to `Finnova.Tests.runsettings`.

`Finnova.Tests/Finnova.Tests.runsettings` supplies `FINNOVA_TEST_JWT_KEY` as a test-run environment variable. The build-repair report nevertheless records a missing-key failure for one invocation, so the effective test runner/environment behavior must be verified by an actual test run.

`Finnova.Repository` references EF Core 10.0.0, Relational, SQL Server, and the Models project. `Finnova.Service` references Models and Repository and uses MediatR 12.4.1, FluentValidation 11.11.0, and ASP.NET Core abstractions. The three API hosts reference Service, Models, and Repository. The gateway uses YARP 2.3.0.

### Finding 3 — Frontend repository is unreachable

- **Path:** `E:\Finnova\Finnova-UI\Finnova-UI`.
- **Error/test name:** Frontend build and test configuration unavailable.
- **Evidence:** A read-only path check returned `FRONTEND_UNREACHABLE`. No frontend directory listing, `package.json`, `vite.config.*`, `tsconfig.*`, or Vitest configuration could be read.
- **Confidence:** Confirmed for this inspection; the reason for inaccessibility is unknown.

The frontend stack is recorded from the product steering and prior artifacts as React 18 + TypeScript + MUI + Vite, with Vitest + React Testing Library and `jsdom`. The expected commands recorded in those artifacts are `npm run build` and `npm test`/`npm test -- --run`, but their current scripts cannot be confirmed from the unavailable repository.

**Assumption:** The frontend path will be reachable to the implementation workflow, or frontend verification will be explicitly marked blocked. No frontend type mismatch, mock-service gap, Vite issue, or Vitest failure can be confirmed from this workspace.

## Specification structure and related documents

### Finding 4 — Reference specification structure and EARS style

- **Paths:**
  - `.kiro/specs/nationality-master-management/requirements.md`
  - `.kiro/specs/nationality-master-management/design.md`
  - `.kiro/specs/nationality-master-management/tasks.md`
  - `.kiro/specs/nationality-master-management/.config.kiro`
  - `.kiro/specs/organization-hierarchy-master-management/requirements.md`
  - `.kiro/specs/organization-hierarchy-master-management/design.md`
  - `.kiro/specs/organization-hierarchy-master-management/.config.kiro`
- **Error/test name:** Specification layout and documentation completeness.
- **Evidence:** Nationality has four files. Organization hierarchy has requirements, design, and config but **no `tasks.md`**; the attempted read of `.kiro/specs/organization-hierarchy-master-management/tasks.md` returned “Path does not exist.”
- **Confidence:** Confirmed from source.

The exact reference requirements layout is:

1. `# Requirements Document`
2. `## Introduction`
3. `### Dependencies and Cross-Cutting Assumptions`
4. `## Glossary`
5. `## Requirements`
6. Repeated `### Requirement N: ...` sections, each with `**User Story:**` and `#### Acceptance Criteria`.

Acceptance criteria use EARS trigger words and service names, including `WHEN ... THE ... SHALL`, `WHERE ... THE ... SHALL`, `IF ... THEN THE ... SHALL`, and direct ubiquitous `THE ... SHALL` statements. Silent decisions are labelled `**Assumption:**` or listed in the dependencies section. The product rules are explicit: India-only, English-only, one plain display field, no Arabic/non-English values, and no RTL.

The nationality design starts with `# Design Document — ...`, JIRA/module metadata, `## Overview`, reuse/new tables, a requirements coverage map, architecture diagrams, request lifecycle, data models, contracts, and error/security details. Its tasks file starts with `# Implementation Plan: ...`, metadata, `## Overview`, then dependency-ordered checked tasks with complete code blocks and requirement references.

The organization-hierarchy design is explicitly a UI-scoped revision. It includes an API reference, gateway routing, auth, endpoint/contract tables, error codes, frontend architecture, file-by-file `CREATE`/`MODIFY` implementation instructions, and frontend test guidance. Its missing tasks file is a documentation gap that must not be copied into a new complete spec.

The two config examples are one-line JSON objects containing a fresh `specId`, `"workflowType": "requirements-first"`, and `"specType": "feature"`.

### Finding 5 — `application-account-creation/tasks.md` is an unexecuted, incomplete task script

- **Path:** `.kiro/specs/application-account-creation/tasks.md:1-21, 24 onward, 3963-3964`.
- **Error/test name:** Potential build/test breakage from unfinished application-account work.
- **Evidence:** The file states: “Nothing here has been applied, built, or tested.” Every task heading inspected is unchecked (`- [ ]`). Its final checkpoint says to run `dotnet build Finnova.Backend.slnx`, then `dotnet test Finnova.Tests`, after the feature tasks are applied.
- **Confidence:** Confirmed from artifact.

Relevant dependencies and gaps recorded in that task script:

- It references a worker at `e:\Finnova\Finnova-API\workers\account-creation-worker`; the current backend root listing has no `workers` directory. The worker may be a separate repository/path, but that cannot be established here.
- It references the separate frontend repository and a full frontend test/build phase, which cannot currently be inspected.
- It introduces many new types and files such as `LoanAccount`, `AccountCreationRequest`, `AccountKycJob`, `AccountKycHistoryEntry`, `AccountKycResolutionRequest`, `AccountAuditEntry`, `AppRole`, `ServiceCredential`, internal contracts, repositories, and worker boundaries. The current source already contains several corresponding types, including `Finnova.Models/Domain/Roles/AppRoleCatalog.cs`, `Finnova.Models/Domain/Exceptions/LoanAccountExceptions.cs`, `Finnova.AccountsService/Controllers/LoanAccountsController.cs`, and `Finnova.Service/LoanAccounts/*`; this conflicts with the task file's statement that nothing was applied and indicates the spec is stale relative to the current worktree.
- The script requires a migration and explicitly says not to update the database. No migration command was run for this investigation.
- The script records a startup seeder for the system user/service credential and a gateway internal-path block. These are architectural dependencies, not proof that the current implementation is complete.
- Tests are required, not optional, but no test result is embedded in this task file.

### Finding 6 — Existing spec review artifacts identify documentation/test gaps

- **Paths:** `.agents/tasks/customer-master-spec/design-review.json`, `.agents/tasks/customer-master-spec/investigation.md`.
- **Error/test name:** Customer master spec test/build readiness.
- **Evidence:** The design review requests changes because tasks did not provide executable code for required backend/frontend tests, the end-to-end scope was not self-contained, and the real `IndiaAddressControlDeferredAdapter` throws while the test factory substitutes a fake. It also records an incorrect `PaginatedResponse` type in the design and contradictory task ordering.
- **Confidence:** Confirmed from artifact; current runtime status unknown.

This is not a direct current build diagnostic, but it is relevant to the ticket because it identifies likely test-readiness problems: missing exact integration scenarios, reliance on a test-only address adapter, and a contract mismatch between documented `IReadOnlyCollection<T>` and the actual `List<T>` response shape.

## Historical build errors and test failures

### Finding 7 — API build repair fixed restore/compiler errors, but initially left nine runtime failures

- **Primary report:** `.agents/tasks/fix-api-build/report.md`.
- **Review:** `.agents/tasks/fix-api-build/review.md` and `review.json`.
- **Error/test names:** NU1608, CS0103, dependency-direction compiler errors, missing ASP.NET Core framework types, missing imports/stale signatures, FluentValidation `Transform` API incompatibility, duplicate `IVerificationWorkflowCoordinator`, and nine behavioral test failures.
- **Confidence:** Confirmed from artifact; current build status unknown because no build was run now.

Concrete reported build causes and status:

- `Finnova.Tests/Finnova.Tests.csproj`: explicit `xunit.extensibility.core` 2.9.3 conflicted with xUnit 2.9.2 and produced NU1608. **Current source check:** the package is now 2.9.2 at line 15. **Status:** source-level fix present; runtime restore not verified.
- `Finnova.Service/Finnova.Service.csproj`: ASP.NET Core HTTP/hosting abstractions were used without a framework reference. **Current source check:** `Microsoft.AspNetCore.App` is present in the project file. **Status:** source-level fix present.
- `Finnova.Repository` adapters implemented contracts declared only in Service, creating an invalid one-way dependency. The report says reference ports were moved to `Finnova.Repository.Interfaces`. **Current source check:** `Finnova.Repository/DependencyInjection.cs` imports Repository interfaces only and has no Service project reference. **Status:** reported fix is present in the current structure.
- `AmortizationCalculator` had a missing comma in a switch expression. The report says it was fixed. **Status:** not independently rechecked beyond the report.
- Identity-verification files had missing imports, stale method argument ordering, a duplicate workflow coordinator contract, and exception-constructor issues. The report says they were fixed. **Status:** not all independently rechecked.
- `UpdateApplicationCommandValidator` used FluentValidation's removed `Transform` API. The report says it was rewritten using direct rules. **Status:** not independently rechecked.
- Tests had stale adapter enum/configuration names and an ambiguous `Program` type. The report says they were corrected. **Status:** not independently rechecked.

The report recorded these post-build-repair failures after an ephemeral JWT key was supplied:

1. Three identity-verification integration authorization assertions received `UnsupportedMediaType` instead of the expected auth statuses. Exact test method names were not included in the report.
2. One identity middleware assertion could not find the expected response property. Exact test method name was not included.
3. A full-workflow partial-input assertion saw an unexpected result field. Exact test method name was not included.
4. `Finnova.Tests.ApplicationProcessing.AmortizationCalculatorProperties.Deterministic` failed because result objects were compared by reference rather than meaningful values.
5. `Finnova.Tests.Unit.IdentityVerificationProperties.ValidIdentifiersPreserveOperationIndependence` expected `ValidationStatus.Success` for a generated failure/unavailable result.
6. `Finnova.Tests.Unit.IdentityVerificationProperties.ComparisonInputContainsOnlyProviderNeutralFields` expected a public field shape different from the current provider-neutral input contract.
7. `Finnova.Tests.ApplicationProcessing.ApplicationProcessingIntegrationTests.AuthorizedUnknownIdReturns404AndStableCode` received HTTP 500 instead of HTTP 404.
8. `Finnova.Tests.Unit.IdentityVerificationExamples.UnsupportedDocumentExtensionOrMimeTypeIsRejected` did not reject the `.png`/`application/pdf` mismatch.

The report describes nine failures although the categories above combine multiple identity integration assertions. Therefore the exact complete nine-method list cannot be recovered from that artifact alone.

### Finding 8 — The later API failure report says the nine failures were fixed, but its review found a weakened assertion

- **Paths:** `.agents/tasks/resolve-api-test-failures/report.md`, `.agents/tasks/resolve-api-test-failures/review.md`, `.agents/tasks/resolve-api-test-failures/review.json`.
- **Error/test name:** `IdentityVerificationProperties.InvalidRecordShapeIsRejected`.
- **Evidence:** The review states that the test was changed to catch `InvalidOperationException`, which also accepts derived exception types, and gives the exact required repair: restore an exact-type assertion.
- **Confidence:** Confirmed from artifact and inferred from current source.

The report claims all 555 tests passed after rebuilding. Current source inspection confirms several reported fixes:

- `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs:47` maps `ERR-APP-404` to HTTP 404.
- `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs:12` requires an accepted content type, `HasAllowedExtension`, and a matching signature.
- `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs:16-17` declares `Consumes("multipart/form-data")` and normalizes a null submitted value before command validation.
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingIntegrationTests.cs:28` still asserts `AuthorizedUnknownIdReturns404AndStableCode` and `ERR-APP-404`.

However, the reviewed assertion remains weakened in the current source:

```csharp
// Finnova.Tests/Unit/IdentityVerificationProperties.cs:35-42
catch (InvalidOperationException) { threw = true; }
return threw.ToProperty();
```

This is a **confirmed current test-quality issue** even if the test currently passes. It permits derived exception types and does not prove an exact `InvalidOperationException` result. The review JSON is `CHANGES_REQUESTED` for this finding.

### Finding 9 — Customer onboarding targeted tests passed, but clear-default remains behaviorally impossible for non-empty accounts

- **Paths:** `.agents/tasks/fix-customer-onboarding-api-tests/plan.md`, `review.md`, `review.json`, plus current source files below.
- **Error/test name:** `ClearDefaultBankAccountCommand`; `CustomerDefaultBankAccountException`.
- **Evidence:** The review says `CustomerChildCommandHandler` clears every default flag and then invokes a rule requiring exactly one default whenever accounts exist. Current source confirms:
  - `Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs:66-73` calls `mapper.ClearDefault(customer)` and immediately validates all accounts.
  - `Finnova.Service/Customers/Validation/CustomerRules.cs:44-46` throws when a non-empty account collection does not contain exactly one default.
- **Confidence:** Confirmed from current source and review artifact.

The targeted `CustomerOnboardingApiTests` result recorded in the plan was 8 passed, 0 failed. The full suite was initially 452 total with five unrelated failures, then the review iteration recorded 452 passed after a bounded FsCheck `DateOnly` input correction. The review still returned `CHANGES_REQUESTED` because the clear-default MediatR command cannot succeed when a customer has one or more bank accounts. The current controller does not expose the command as an HTTP route, so the targeted HTTP suite does not cover it; the registered request handler remains part of the application surface.

The report also says these earlier interface/DI issues were fixed:

- `CustomerChildCommandHandler.Handle(ClearDefaultBankAccountCommand, CancellationToken)` now returns `Task`; current source line 66 is `public async Task Handle(...)`.
- `InMemoryNationalityRepository` now implements `GetActiveByCodeAsync`; current source contains it at line 134 onward.
- Duplicate Customer repository contracts were removed from Service in favor of Repository interfaces.
- Customer mapper, projection mapper, code generator, child mapper, and account-contract registrations were added.
- `SystemAdminAppFactory` ignores `InMemoryEventId.TransactionIgnoredWarning` at lines 40-41.

### Finding 10 — JV Posting integration startup failure was fixed, and current source contains the named repairs

- **Paths:** `.agents/tasks/fix-integration-tests/verification.md`, `.agents/tasks/fix-integration-tests/review.md`, `.agents/tasks/jv-posting-submission-service/report.md`, and current source paths below.
- **Error/test names:** `Finnova.Tests.JVPosting.JvPostingApiTests.Submission_requires_authentication`; EF error “The entity type 'JvPostingInbox' requires a primary key to be defined.”; unresolved `TestApplicationFactory`; invalid Repository registration of `JvPostingSubmissionService`.
- **Evidence:** The integration verification records the pre-fix failure during `SystemAdminAppFactory` startup and the fix through `JvPostingInbox.RequestId` key configuration. Current source confirms:
  - `Finnova.Repository/Configuration/JvPostingInboxConfiguration.cs:7-12` implements `IEntityTypeConfiguration<JvPostingInbox>` and calls `builder.HasKey(x => x.RequestId)`.
  - `Finnova.Tests/Integration/TestApplicationFactory.cs:7-9` defines the missing compatibility factory and returns `SystemAdminAppFactory`.
  - `Finnova.Repository/DependencyInjection.cs` contains no `JvPostingSubmissionService` registration, avoiding the Repository-to-Service dependency error.
  - `Finnova.SystemAdminService/Program.cs:78` registers `Finnova.Service.JVPosting.JvPostingSubmissionService` in the host that references Service.
- **Confidence:** Confirmed from current source and artifact.

The recorded post-fix verification was one JV Posting test passing and 248 integration tests passing. No current command was run, so those historical results are not a current green-suite proof.

The JV Posting static report additionally identified unresolved design/test risks that are visible without executing code: no successful authenticated submission coverage beyond the 401 test, no checked-in migration evidence for `JvPostingInbox`, partial idempotency hash coverage, untyped expected-error mappings, unsafe `Guid.Parse` for person actors, and unimplemented recovery controller methods. These are broader than the immediate compile repair and need explicit scope decisions.

### Finding 11 — Other task artifacts record green historical verification, not current verification

- **Paths:** `.agents/tasks/branch-access-location-fk/*`, `.agents/tasks/lob-program-fk/*`, `.agents/tasks/unify-user-identity/plan.md`, `.agents/tasks/task-screen-permissions/features/FEAT-001.json`, `.agents/tasks/task-screen-permissions/features/FEAT-002.json`.
- **Error/test name:** Historical cross-feature build/test status.
- **Evidence:**
  - Branch access verification recorded `dotnet build` with 0 errors, 212 targeted backend tests passing, `npm run build` passing, and six frontend files/27 tests passing. Review verdict: `APPROVED`.
  - LOB/program FK verification recorded backend build passing, 212 targeted tests passing, frontend build passing, and six frontend files/20 tests passing. Review verdict: `APPROVED`, with two low notes about a Guid passed to a string-shaped branch-tree parameter and a retained catalog coupling.
  - User identity plan says build passed with 0 warnings/errors and 379 tests passed.
  - Screen-permissions FEAT-001 records backend build passing and 383 tests passing; FEAT-002 records frontend build passing and 184 tests passing.
- **Confidence:** Confirmed from artifacts; current state unknown.

These artifacts are useful for locating prior fixes and conventions, but they cannot prove the current dirty worktree is green. They also show that several earlier workflows intentionally did not commit or rerun all suites after later changes.

### Finding 12 — Screen-permissions planning identified an earlier UA auth and login contract mismatch

- **Paths:** `.agents/tasks/screen-permissions-plan.md:18-66`, `.agents/tasks/task-screen-permissions/context.json`.
- **Error/test name:** UA JWT authorization and frontend login type mismatch.
- **Evidence:** The plan recorded that `Finnova.UAService/Program.cs` initially lacked `AddAuthentication`, `AddJwtBearer`, `UseAuthentication`, and `UseAuthorization`, and that the UI `LoginRequest` sent `{ email, password }` while the backend expected `{ userName, password }`. The FEAT artifacts later mark both changes completed and record green historical verification.
- **Confidence:** Confirmed from historical artifact; current runtime/type status unknown because the frontend is unreachable and no build/test was run.

## Current source-level cross-checks

### Finding 13 — Previously reported missing JV Posting symbols are no longer missing in source

- **Paths:** `Finnova.Tests/JVPosting/JvPostingApiTests.cs:12`, `Finnova.Tests/Integration/TestApplicationFactory.cs:7-9`, `Finnova.Repository/Configuration/JvPostingInboxConfiguration.cs:7-12`.
- **Error/test name:** `TestApplicationFactory.Create`; missing `JvPostingInbox` primary key.
- **Evidence:** The test still calls `TestApplicationFactory.Create()`, and the current source now defines that method. The current EF configuration explicitly maps `RequestId` as the key.
- **Confidence:** Inferred from current source; historical post-fix pass is confirmed separately.

No source-level absence remains for these two reported symbols. The actual compile and InMemory model construction still require a build/test run to prove.

### Finding 14 — Current source has two confirmed test/behavior issues requiring a decision

- **Paths:**
  - `Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs:66-73`
  - `Finnova.Service/Customers/Validation/CustomerRules.cs:44-46`
  - `Finnova.Tests/Unit/IdentityVerificationProperties.cs:35-42`
- **Error/test names:** `ClearDefaultBankAccountCommand`; `InvalidRecordShapeIsRejected`.
- **Evidence:** See Findings 8 and 9. One is a behaviorally impossible command under the current invariant; the other weakens exact exception-type coverage.
- **Confidence:** Confirmed from current source.

These are the strongest residual problems visible without running commands. The first needs a product contract decision: allow zero defaults, select a replacement default, or defer/remove the clear operation. The second needs a narrow test-only correction to assert exact exception type.

### Finding 15 — Current dirty Loan Account/Application Account source is unverified

- **Paths:** current untracked files under `Finnova.Service/LoanAccounts`, `Finnova.AccountsService/Controllers/LoanAccountsController.cs`, `Finnova.SystemAdminService/Controllers/InternalControllers.cs`, `Finnova.SystemAdminService/ServiceAuth`, and `Finnova.Tests/LoanAccounts`.
- **Error/test name:** No exact current diagnostic available.
- **Evidence:** `git status --short --branch` shows these files as untracked, while modified host/gateway files are also present. The source contains references such as `AccountCreationKycOptions`, `LoanAccountException`, `AppRoleCatalog`, and `ServiceCredential` across this in-progress slice, but no build was run to validate project references, registrations, generated code, or host wiring.
- **Confidence:** Confirmed that the files are untracked; compile status is unknown.

This is a current risk, not a confirmed compiler error. It overlaps the unexecuted `application-account-creation` task script and should be included in the implementation scope only after deciding whether these uncommitted changes are part of the ticket or pre-existing work to preserve.

## What cannot be determined without running commands

The following cannot be established by read-only inspection:

1. Whether `Finnova.Backend.slnx` currently restores and builds from the current worktree.
2. The current compiler error list, warning list, generated-source errors, and exact file/line diagnostics.
3. Whether project assets and package versions restore successfully, including whether the runsettings JWT environment variable is honored by the selected test invocation.
4. Whether the complete current xUnit/FsCheck suite passes, the exact discovered test count, or any current failures beyond the historical reports.
5. Whether `SystemAdminAppFactory` can build the complete EF InMemory model after all current uncommitted Loan Account/Application Account entities are included.
6. Whether every current MediatR handler, repository, mapper, and DI registration can be activated at runtime.
7. Whether EF migrations compile, whether the model snapshot is current, and whether any migration would be generated from current model drift.
8. Whether the frontend repository has the expected `package.json` scripts, Vite/Vitest configuration, TypeScript settings, service interfaces, real/mock implementations, and current type/test failures.
9. Whether backend and frontend camelCase contracts match in the current frontend checkout.
10. Whether gateway routes, forwarded auth, host startup, database connectivity, and end-to-end HTTP behavior work.
11. The exact names of all nine historical failures from `fix-api-build`; the report omits exact names for several combined identity-integration categories.
12. Whether the historical fixes were made on the same source revision as the current worktree. The reports and current files are evidence, but no commit comparison or runtime verification was performed.

## Proposed assumptions for confirmation

1. **Frontend availability:** `E:\Finnova\Finnova-UI\Finnova-UI` is the intended frontend checkout and will be made reachable before frontend implementation/verification. Until then, frontend findings are unknown rather than assumed green.
2. **Scope of uncommitted Loan Account work:** The current untracked Loan Account/Application Account files and modified host/gateway files are pre-existing work to preserve, not an implicit part of this repair, unless the ticket owner explicitly includes them.
3. **Build/test repair scope:** The implementation should address both compile/restore failures and behavioral test failures documented in the reports, rather than treating a successful build alone as completion.
4. **Clear-default contract:** Product must choose whether a customer may have zero default bank accounts. The current validator requires exactly one whenever accounts exist; implementation should not silently loosen that rule.
5. **Exact exception testing:** `InvalidRecordShapeIsRejected` should require `ex.GetType() == typeof(InvalidOperationException)` or an equivalent exact-type assertion, not merely catch the base type.
6. **Historical report status:** Historical “pass” results are treated as evidence of the state at the time of each report, not as evidence that the current dirty worktree is green.
7. **JV Posting persistence:** The intended owner of the `JvPostingInbox` schema and migration must be confirmed before adding or changing migrations; the static report found no checked-in migration evidence for it.
8. **Frontend test commands:** The implementation should use the actual `package.json` scripts after the frontend repository becomes accessible, rather than relying solely on commands recorded in old task artifacts.
9. **Localization:** All repair work remains India-only and English-only, with no bilingual fields, non-English seed/sample data, or RTL behavior.
10. **No destructive cleanup:** Existing modified/untracked work is preserved; no reset, clean, deletion, migration application, build, test, or database update is part of this investigation.
