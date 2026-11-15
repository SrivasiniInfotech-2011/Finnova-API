# Requirements Document

## Introduction

This specification addresses the ticket **“Resolve the build errors and test cases.”** It defines a verification-first repair for both sides of Finnova: the .NET backend at `d:\Projects\Finnova\Finnova-API` and the React frontend at `E:\Finnova\Finnova-UI\Finnova-UI`.

The backend target is `Finnova.Backend.slnx`, including all projects in that solution and the `Finnova.Tests` xUnit/FsCheck test project. The frontend targets the repository's existing `npm run build` and Vitest-backed `npm test` scripts. This workflow does not claim that the current tree is green. The investigation deliberately ran no build, restore, npm, test, migration, database, or HTTP command. The implementer must capture an honest baseline before changing files whenever the failure is not already established by the findings or another available artifact.

The findings confirm historical backend build-repair categories including xUnit package skew, a missing ASP.NET Core framework reference, invalid dependency direction, malformed or stale C# APIs, duplicate identity-verification contracts, and FluentValidation API incompatibility. They also record historical behavioral failures involving multipart authorization fixtures, middleware response properties, nullable workflow results, amortization value comparison, generated identity outcomes, provider-neutral contract shape, application not-found mapping, and document extension/MIME mismatch. A later review confirms that the current `InvalidRecordShapeIsRejected` property still accepts derived `InvalidOperationException` types and must be narrowed to an exact-type assertion. Other current status is unverified.

Finnova remains India-only and English-only. This repair adds no product data, bilingual fields, non-English values, Arabic content, or RTL behavior. The frontend continues to use React hooks, MUI, the shared axios instance, and the existing Vitest/React Testing Library stack. Redux and RxJS are not introduced.

## Glossary

- **Backend solution**: `Finnova.Backend.slnx` and its eight existing projects: Models, Repository, Service, AccountsService, SystemAdminService, UAService, ApiGateway, and Tests.
- **Backend test suite**: `Finnova.Tests`, containing xUnit example, unit, property-based, and integration tests.
- **Frontend**: `E:\Finnova\Finnova-UI\Finnova-UI`, the React 18 + TypeScript + MUI + Vite application using Vitest and React Testing Library.
- **Baseline**: The captured command, working-tree context, output, exit code, and environment prerequisites recorded before a repair is applied.
- **Confirmed finding**: A problem or source condition explicitly recorded in `findings.md` or directly observable from inspected source, without claiming current runtime status.
- **Historical result**: A build or test result recorded by an earlier task artifact. It describes that earlier checkout/run and is not proof that the current dirty worktree passes.
- **Build repair**: A correction to restore package, project-reference, source, API, or host-wiring compatibility so the relevant build completes.
- **Test repair**: A correction to production behavior, test fixture, test host, or assertion that makes a test accurately verify the intended contract. It does not mean deleting or weakening a test.
- **Exact-type assertion**: A check that the runtime exception type equals `typeof(InvalidOperationException)`, rather than accepting a subtype through a broad catch.
- **Process-scoped JWT key**: An ephemeral `FINNOVA_TEST_JWT_KEY` supplied only to a test process. It must not be persisted or committed.
- **Stable error contract**: Existing HTTP status, error code, message/detail, and correlation properties exposed by the host middleware.
- **Provider-neutral contract**: The identity-verification comparison input that must not expose provider-, secret-, or token-specific public fields.

## Dependencies and Assumptions

### Dependencies

- The backend solution remains `Finnova.Backend.slnx` and the test project remains `Finnova.Tests/Finnova.Tests.csproj`.
- Backend tests continue to use xUnit, FsCheck.Xunit, Moq, EF Core InMemory, and `WebApplicationFactory` as already configured.
- The frontend continues to use its checked-in `package.json` scripts, React hooks, MUI, the shared axios instance at `src/services/api.ts`, Vitest, React Testing Library, and `jsdom`.
- Authentication-dependent backend tests require the existing `FINNOVA_TEST_JWT_KEY` prerequisite. The key is supplied at execution time only.
- Existing host separation, YARP gateway routes, JWT policies, API contracts, and localization rules remain authoritative unless a current baseline proves a concrete break.

### Assumptions for confirmation

- **A1 — Frontend availability:** `E:\Finnova\Finnova-UI\Finnova-UI` is the intended checkout. It was unreachable during the investigation, so no current frontend error, script, package, TypeScript contract, or Vitest failure is confirmed. The implementation either makes that path available or records frontend verification as blocked rather than treating it as green.
- **A2 — Baseline authority:** If a current build or test failure cannot be determined from `findings.md`, source inspection, or another checked-in artifact, the implementer captures the command and output before changing the relevant files. The captured output becomes authoritative for that repair iteration.
- **A3 — Current dirty worktree:** Existing modified host/gateway files and untracked Loan Account, service-auth, and test files are pre-existing work to preserve. They are not silently reset, deleted, or included as new feature scope. If they cause a current failure, the owner decides whether they belong to this ticket.
- **A4 — Historical repair preservation:** The historical repairs listed in the findings are intended to remain in the current tree. If a current baseline shows that one is absent, it is restored only to the extent required by its original contract.
- **A5 — Clear-default behavior:** The current customer invariant requires exactly one default bank account when accounts exist, while `ClearDefaultBankAccountCommand` clears every default. Product must choose whether clearing is allowed to leave zero defaults, whether another account becomes default, or whether the operation is deferred. The implementation must not silently weaken the invariant.
- **A6 — Migration ownership:** The owner of the `JvPostingInbox` schema and migration is unresolved. No migration is added or applied as part of this repair unless the owner and scope are confirmed.
- **A7 — Test count:** The historical report's 555 passing tests is evidence only. Completion means every currently discovered test passes with zero failures and zero skipped tests, unless the ticket owner explicitly approves a documented test-scope change.
- **A8 — Frontend commands:** The implementation uses the actual frontend `package.json` scripts after access is restored. Old artifact names such as `npm test -- --run` are not assumed to be the only valid invocation.
- **A9 — Product context:** All repair fixtures, sample values, messages, and test data remain English-only and India-compatible. No RTL, Arabic, or other non-English content is introduced.
- **A10 — No destructive cleanup:** No reset, clean, deletion, database update, migration application, secret persistence, or unrelated refactor is authorized by this ticket.

## Requirements

### Requirement 1: Capture a trustworthy baseline (Complex)

**User Story:** As an implementer, I want the current backend and frontend failures recorded before edits, so that fixes target actual diagnostics and do not confuse historical results with current behavior.

#### Acceptance Criteria

1. BEFORE applying a repair for any failure that cannot be determined from the findings or an available artifact, THE implementer SHALL capture the relevant command, repository path, branch/worktree status, prerequisite environment conditions, stdout/stderr, and exit code.
2. WHEN a baseline command cannot run because a repository, dependency, or required environment value is unavailable, THE implementer SHALL record the blocking condition and SHALL NOT report the component as passing.
3. WHEN historical results are used to identify likely repairs, THE implementation record SHALL label them as historical and SHALL separately identify any command run against the current worktree.
4. THE baseline SHALL preserve existing modified and untracked files and SHALL not use reset, clean, deletion, or another destructive cleanup to make the starting state appear reproducible.
5. WHEN the backend or frontend baseline reveals an error not covered by the findings, THE implementer SHALL add that concrete diagnostic to the repair record before changing code for it.

### Requirement 2: Restore backend solution and test-project buildability (Ubiquitous)

**User Story:** As a backend maintainer, I want the solution and test project to compile with compatible dependencies and project boundaries, so that test execution is possible.

#### Acceptance Criteria

1. THE backend repair SHALL preserve a dependency graph in which every project in `Finnova.Backend.slnx`, including `Finnova.Tests`, compiles for its configured target framework without compiler errors or restore-time package conflicts.
2. THE test project SHALL keep compatible xUnit package versions, including the confirmed alignment of `xunit.extensibility.core` with the existing xUnit 2.9.2 family, unless a current package baseline proves a different compatible version is required.
3. THE service project SHALL retain the ASP.NET Core framework reference required by its HTTP and hosting abstractions.
4. THE repository project SHALL not reference the service project merely to implement repository adapters; shared application reference ports SHALL remain on the repository-facing boundary used by both adapters and service consumers.
5. THE repair SHALL correct, where a current baseline confirms they remain, the documented compiler categories: malformed amortization syntax, missing identity imports or stale argument order, duplicate workflow coordinator contracts, incompatible FluentValidation `Transform` usage, exception-constructor issues, and ambiguous test `Program` references.
6. THE backend build verification SHALL report zero errors for the solution and the test project, and SHALL not achieve that result by excluding source files, suppressing errors, or deleting tests.

### Requirement 3: Preserve and correct backend behavioral tests (Event-driven)

**User Story:** As a backend maintainer, I want failing tests repaired at the correct production, fixture, host, or assertion boundary, so that green tests continue to prove real behavior.

#### Acceptance Criteria

1. WHEN an identity-verification authorization test invokes an endpoint declared as `multipart/form-data`, THE test fixture SHALL send multipart content so that authentication and authorization are evaluated against the intended request shape.
2. WHEN identity middleware handles a validation failure, THE response test SHALL verify the existing stable error code and non-empty correlation identifier rather than a property that the current response contract does not expose.
3. WHEN a full identity workflow runs in partial-input mode and an operation has not executed, THE test SHALL verify that the addressable result property is present with JSON `null` according to the current nullable response contract.
4. WHEN the amortization calculator is called twice with identical inputs, THE deterministic test SHALL compare scalar values and installment values by value rather than relying on object reference equality.
5. WHEN identity property tests generate success, failure, or unavailable adapter outcomes, THE tests SHALL assert the generated status and retain the expected independent-call and independent-result checks.
6. WHEN the provider-neutral comparison input contract is reflected, THE test SHALL match its current public fields, including `ActorReference` when present, and SHALL reject provider-, secret-, or token-specific public fields.
7. WHEN an authenticated request uses an unknown application identifier, THE Accounts host test SHALL receive HTTP 404 with stable code `ERR-APP-404`, not a generic HTTP 500.
8. WHEN document validation receives a `.png` filename with `application/pdf`, or another extension/MIME/signature mismatch, THE validator test SHALL confirm rejection before provider or persistence work.
9. THE implementation SHALL resolve a test failure by correcting the owning behavior or an inaccurate test expectation, and SHALL never make a test green by deleting it, skipping it, broadening an expected exception, weakening authorization, or accepting an invalid contract.

### Requirement 4: Enforce exact identity exception coverage (Unwanted-event)

**User Story:** As a domain maintainer, I want malformed identity records to be tested with exact exception semantics, so that future subtype or contract drift cannot pass unnoticed.

#### Acceptance Criteria

1. WHEN `IdentityVerificationInvariants.EnsureResultDomain` receives an identifier-validation record containing a comparison outcome, THE `InvalidRecordShapeIsRejected` property SHALL fail unless the thrown runtime type is exactly `InvalidOperationException`.
2. IF no exception is thrown, THEN `InvalidRecordShapeIsRejected` SHALL fail.
3. IF a subtype of `InvalidOperationException` is thrown, THEN `InvalidRecordShapeIsRejected` SHALL fail rather than accepting the subtype through `catch (InvalidOperationException)`.
4. THE repair SHALL retain the malformed record generator, the property-based test, and at least 100 generated cases, and SHALL not replace the assertion with a weaker base-type or message-only check.

### Requirement 5: Preserve security and stable failure handling (State-driven)

**User Story:** As a security stakeholder, I want build/test repairs to preserve authentication, authorization, validation, and host-specific error mappings, so that test fixes do not hide security regressions.

#### Acceptance Criteria

1. WHILE `FINNOVA_TEST_JWT_KEY` is absent or blank, THE JWT-dependent test host SHALL fail clearly with a setup/prerequisite error, and THE repair SHALL not add a committed signing secret, disable JWT validation, or remove authorization tests.
2. WHILE an identity or application request is unauthenticated or has the wrong role, THE existing pipeline SHALL continue to produce its established 401 or 403 result, and multipart fixture changes SHALL not bypass those checks.
3. WHILE document input is oversized, unsupported, extension/MIME-inconsistent, or signature-invalid, THE validation path SHALL reject it before provider or persistence work and SHALL retain the existing document error family.
4. WHILE a known identity validation error reaches SystemAdmin middleware or `ERR-APP-404` reaches Accounts middleware, THE response SHALL retain the existing status/code contract and the identity correlation identifier where applicable.
5. THE repair SHALL preserve the existing JWT policies, gateway host separation, Authorization header forwarding, role claims, and authentication-before-authorization middleware ordering.

### Requirement 6: Verify the frontend build and test boundary (Event-driven)

**User Story:** As a frontend maintainer, I want the current UI build and Vitest suite checked after backend repair work, so that the ticket covers the complete platform without inventing UI changes.

#### Acceptance Criteria

1. WHEN the frontend checkout is accessible, THE implementer SHALL inspect its actual `package.json`, TypeScript/Vite configuration, and Vitest configuration before choosing the build and test invocations.
2. WHEN the implementer runs the frontend build script, THE React + TypeScript + Vite application SHALL complete `npm run build` with zero errors, unless the actual script is different and the difference is recorded from `package.json`.
3. WHEN the implementer runs the frontend test script, THE Vitest + React Testing Library suite SHALL complete with zero failed tests and zero skipped tests, using the repository's configured jsdom environment.
4. IF frontend diagnostics identify a genuine API contract mismatch, THEN THE implementer SHALL record the mismatch and obtain scope confirmation before changing backend or frontend contracts; a build repair SHALL not silently alter routes or response shapes.
5. THE frontend verification SHALL continue to use React hooks and existing context patterns, MUI, the shared axios instance, and the existing mock/real service toggle, and SHALL not introduce Redux or RxJS.
6. IF the frontend checkout remains unreachable, THEN THE implementation record SHALL mark frontend build/test verification blocked and SHALL not claim that the UI passes.

### Requirement 7: Preserve platform and repository boundaries (Ubiquitous)

**User Story:** As a platform owner, I want a bounded repair that leaves unrelated work and public contracts stable, so that resolving build errors does not become an unauthorized feature change.

#### Acceptance Criteria

1. THE repair SHALL not add or change business entities, API routes, gateway clusters, database schema, migrations, or frontend features unless a captured current diagnostic proves that an existing repair contract cannot compile or execute without that change.
2. THE repair SHALL preserve current backend host separation among UA, Accounts, and SystemAdmin and SHALL preserve the shared frontend axios gateway composition.
3. THE repair SHALL preserve existing public request/response shapes and stable error codes unless a test artifact proves that the current test expectation contradicts the established contract; any such decision SHALL be recorded.
4. THE repair SHALL leave unrelated Loan Account/Application Account, JV Posting, service-auth, and gateway working-tree changes intact unless the ticket owner explicitly confirms they are in scope.
5. THE repair SHALL introduce no bilingual fields, localization scaffolding, Arabic or other non-English values, RTL handling, Redux, or RxJS.

### Requirement 8: Complete verification and reporting (Complex)

**User Story:** As a release owner, I want the repaired platform verified in dependency order, so that a successful build is not mistaken for a passing test suite.

#### Acceptance Criteria

1. WHEN the implementation is complete, THE implementer SHALL verify the backend solution build, the `Finnova.Tests` project/build prerequisites, focused affected backend tests, the complete backend test suite, the frontend build, and the frontend Vitest suite where the repositories are accessible.
2. THE verification sequence SHALL capture failures with their command, path, exit code, and relevant output, and SHALL distinguish current results from findings and historical reports.
3. THE complete backend and frontend suites SHALL report zero failed tests and zero skipped tests for the current checked-out scope, or the implementation record SHALL identify the unresolved failure and stop rather than declaring completion.
4. THE final verification SHALL confirm that no test was removed, skipped, weakened, or changed to accept an invalid behavior solely to turn the suite green.
5. THE implementation SHALL not persist `FINNOVA_TEST_JWT_KEY` in source, configuration, runsettings, logs, reports, or Git history.

## Out of Scope

- Running any build, test, restore, npm, migration, database, or HTTP command during this document-authoring workflow.
- Adding a new business feature, changing API routes or gateway clusters, changing database schema, applying migrations, or changing seed data.
- Resolving the unresolved customer clear-default product decision without confirmation.
- Assigning ownership or adding a migration for `JvPostingInbox` without confirmation.
- Resetting, cleaning, deleting, or overwriting existing modified or untracked worktree changes.
- Treating historical green reports as proof of the current worktree's health.
- Weakening or deleting tests, disabling authentication, broadening exception assertions, suppressing diagnostics, or changing production contracts merely to obtain green output.
- Adding frontend state libraries or transport clients. The UI remains on React hooks, MUI, shared axios, Vitest, and React Testing Library.
- Introducing bilingual/localized fields, non-English or Arabic content, RTL handling, Redux, or RxJS.
