# Customer Master Spec Design Review

**Scope reviewed:** `requirements.md`, `design.md`, `tasks.md`, `.config.kiro`, and the ground-truth `investigation.md`.

**Review constraints:** This review read the relevant backend and frontend source and repository status. It did not run builds, tests, migrations, or application commands.

**Verdict:** CHANGES_REQUESTED. The documents contain blocking HIGH and MEDIUM findings below.

## Findings

1. **HIGH — `tasks.md` is not executable for the required backend and frontend test coverage.**
   **Location:** `tasks.md`, Task 5 and Task 8; `design.md`, Testability strategy; `requirements.md`, Requirement 10.

   Task 5 says to “add” CompanyId isolation, exact-code precedence, mixed-case search, ordering, and beyond-last-page tests, but supplies no code or exact insertion snippets for those tests. Task 8 similarly says to wrap every existing projection array and change rejected fixtures, but does not show the resulting snippets for each occurrence. The current integration test still deserializes query results as `CustomerResponse[]`, and the current page test still mocks `query` as an array and calls it with a string. A user following the file must infer the test implementations and locations, which violates the copy/paste requirement and leaves the required behavior untested.

   **Concrete fix:** Provide the complete replacement for `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`, or exact uniquely anchored insertion/replacement blocks containing all required query scenarios and the complete problem-shape assertions. Provide the complete replacement for `CustomerOnboarding.test.tsx`, or exact blocks for every array fixture, service call assertion, and error fixture. The code must include the CompanyId, exact-code-over-search, mixed-case code/name, deterministic-order, beyond-last metadata, 401, 403, and invalid-paging cases listed in the requirements.

2. **HIGH — The tasks do not cover the complete end-to-end implementation scope requested by the review criteria.**
   **Location:** `tasks.md`, “Unchanged prerequisites”; `design.md`, “Reused versus new or modified” and implementation-ordered task list.

   The document only asserts that backend entities, enums, exceptions, contracts, EF configuration/DbSets, repositories and DI, helpers, existing CQRS slices, and frontend components, routes, and navigation already exist. It provides no executable verification or copy/paste task for those required stages. The same omission applies to the requested frontend page/components/routes/navigation coverage. This may be acceptable for a gap-only patch, but it is not a self-contained task file that covers the end-to-end customer-master work specified by the review request. It also gives the user no exact action for detecting drift between the investigation and the current files before applying dependent edits.

   **Concrete fix:** Either add dependency-ordered, exact verification/modify tasks for each reused stage, with complete source where a change is needed, or explicitly narrow the ticket and its requirements to a gap-only patch. If the intended scope remains end-to-end, include concrete tasks for the domain, contracts, EF/DbSets, repository/DI, helpers, CQRS, controller/middleware/gateway, backend tests, frontend model/services/toggle/barrels, page/components/routes/navigation, and Vitest tests. A prerequisite list alone is not an executable task.

3. **HIGH — The production address boundary makes the promised successful Customer create/modify flow unavailable.**
   **Location:** `requirements.md`, Requirements 2 and 3; `design.md`, request flow and reused boundaries; `tasks.md`, unchanged prerequisites and Task 5.

   The actual `Finnova.SystemAdminService/Program.cs` registers `IndiaAddressControlDeferredAdapter`. Its `ValidateAndApproveAsync` always throws `CustomerBoundaryUnavailableException`. `CreateCustomerCommand` and `ModifyCustomerCommand` call the customer reference validator, which validates every supplied address through that adapter. The integration test works only because `CustomerOnboardingAppFactory` replaces the adapter with `IndiaAddressControlTestFake`. The spec nevertheless promises that a valid authorized create/modify request validates and persists and describes the existing India Address Control adapter as reusable, without adding a production implementation or an explicit operational dependency. The requested end-to-end behavior therefore cannot succeed in the real host for normal requests containing addresses.

   **Concrete fix:** Choose one explicit contract. Add a task for the real India Address Control adapter, its contract/configuration/DI registration, and its failure/error tests; or state that address-dependent create/modify is intentionally blocked until a separately delivered boundary and remove successful address-flow acceptance from this ticket. Do not use the integration-only fake as evidence of production readiness.

4. **MEDIUM — The implementation order is internally contradictory.**
   **Location:** `tasks.md` opening instruction, Task 1, Task 2; `design.md`, implementation-ordered task list.

   `tasks.md` says “Apply the tasks in order,” but places the repository change in Task 1 and the validation/DbContext preflight in Task 2. It then says to execute Task 2 before Task 1. `design.md` repeats the reversed order by saying Task 2 executes first and Task 1 follows. A literal copy/paste execution and the stated dependency order disagree.

   **Concrete fix:** Renumber and reorder the headings so the preflight/DbContext verification is the first executable task, followed by the repository change, validator/handler, API, tests, and frontend tasks. Use the same numbering and sequence in `design.md` and `tasks.md`, and make the opening instruction unambiguous.

5. **MEDIUM — The backend paginated-response contract in `design.md` does not match the real shared contract or the task code.**
   **Location:** `design.md`, Data models and contracts; `Finnova.Models/Contracts/Common/PaginatedResponse.cs`; `tasks.md`, Task 3.

   The design declares `PaginatedResponse<T>` with `IReadOnlyCollection<T> Data`, while the verified shared record is `PaginatedResponse<T>(List<T> Data, int Total, int Page, int PageSize, int TotalPages)`. The Task 3 implementation happens to pass a `List<CustomerResponse>`, so it follows the source, but the design's contract is not copyable or authoritative. This creates an avoidable mismatch for repository consumers, tests, and any future task author.

   **Concrete fix:** Document the exact existing declaration, including `List<T> Data`, and state that the handler passes the projected `List<CustomerResponse>`. If an interface-level read-only collection is desired, define a separate mapping rule rather than changing the documented shared record implicitly.

6. **MEDIUM — The schema preflight does not verify the structural differences that the requirements say must block deployment.**
   **Location:** `requirements.md`, Requirement 10; `design.md`, Migration and deployment concerns; `tasks.md`, Task 2 SQL.

   The SQL detects missing expected tables and missing expected indexes, including index uniqueness, key-column order, and normalized filters. It does not compare the expected table columns, column types, nullability, lengths, primary keys, or foreign keys. Therefore an existing `Customers` table with the wrong columns or constraints can pass the stated preflight even though Requirement 10 says a structurally different Customer table must stop deployment.

   **Concrete fix:** Either narrow the requirement/design wording to “required table and index presence/shape” or add explicit expected column/constraint metadata and queries against `sys.columns`, `sys.types`, `sys.indexes`, `sys.index_columns`, and foreign-key metadata. The preflight output must fail closed on any required structural mismatch.

7. **MEDIUM — Requirement 1 leaves security behavior ambiguous for forbidden document and external payload content.**
   **Location:** `requirements.md`, Requirement 1, Acceptance Criterion 5.

   “The service SHALL reject or omit that content according to the existing Customer contracts” gives two different valid behaviors and does not define an HTTP status, error code, or whether the entire request is atomic. The existing contracts include several document metadata fields and the boundary does not itself establish a raw-byte rejection rule. This cannot be implemented or tested consistently from the requirement.

   **Concrete fix:** Select one behavior. For example: “IF a request contains a raw document byte field, token, credential, SQL, or unsupported external payload, THEN the API SHALL reject the entire request with HTTP 400 and `ERR-CUSTOMER-400`, SHALL return safe `detail`/`message`, and SHALL write no Customer or audit rows.” Add the corresponding backend and frontend tests.

8. **MEDIUM — The no-source-modification claim is not independently verifiable from the supplied evidence.**
   **Location:** `requirements.md`, Assumption A22; `design.md`, Review resolution and Source boundary.

   The current backend status shows no application-source change, but the frontend repository status contains modified tracked application files and untracked Customer application files. Repository status alone does not establish whether those files predated this authoring step. The documents state that the frontend changes were pre-existing, but do not provide a baseline commit, timestamped status, or hash to prove it. This matters because the review explicitly requires confirmation that the authoring step modified no application source.

   **Concrete fix:** Record the frontend and backend baseline commit/status before document authoring and attach the application-file comparison, or have the workflow owner confirm and preserve the pre-existing changes separately. The review record should distinguish “no source was written by this review” from “no source changed during the earlier spec-authoring step.”

9. **NIT — Several top-level task headings do not meet the required path marker format.**
   **Location:** `tasks.md`, Task 5, Task 6, Task 8, and “MODIFY frontend barrels.”

   The actual file subheadings mostly contain paths, but the top-level executable steps are labelled as broad task names and one marker says `MODIFY frontend barrels` rather than `MODIFY <path>`. This makes automated or manual task tracking less reliable and is inconsistent with the stated marker rule.

   **Concrete fix:** Give every executable edit its own `CREATE <path>` or `MODIFY <path>` heading, including each test file and each barrel. Keep release-only SQL as a separately labelled non-source operation beneath its dependency task.

## Verified Assumptions

- `requirements.md` contains an Introduction, Glossary, Dependencies and Assumptions, numbered EARS-style requirements, optional/complex criteria, and explicit A1–A23 assumptions. It states India-only, English-only, no bilingual fields, no RTL, no Redux, and no RxJS.
- The investigation and source confirm that Customer entities, child entities, customer enums/exceptions, contracts, EF configurations, Customer DbSets, repository interfaces/DI, existing CQRS write/child slices, controller routes, middleware branch, gateway alias, and the existing React page/tab/routes/navigation are present.
- The source confirms that `QueryCustomer` currently returns `IReadOnlyCollection<CustomerResponse>`, `CustomerReadRepository` currently clamps invalid paging and applies both filters independently, and the controller currently returns an array-like collection. The proposed query change is therefore aimed at a real gap.
- The source confirms that `PaginatedResponse<T>` uses `List<T> Data`, not the `IReadOnlyCollection<T>` declaration shown in the design.
- The source confirms that middleware currently emits `status`, `title`, `detail`, and the `code` extension, but not the `message` extension. The gateway contains the documented Customer alias and direct SystemAdmin route.
- The source confirms that the frontend Customer service currently has an unreachable `loadCatalog` call, array-shaped query behavior, no common-barrel exports, and a mock that does not match the backend error/paging contract. The proposed service changes address real mismatches.
- `.config.kiro` is valid JSON, has the requested workflow/spec type, and its GUID is not duplicated by another `.config.kiro` found in the workspace.
- No build, test, migration, or deployment command was run during this review. Only document/source reads, repository-status checks, and the two review-output writes were performed.

## Unverified/Wrong Assumptions

- Assumption A22 says frontend application changes predated spec authoring, but the available Git status cannot prove that provenance. The status does show frontend application modifications/untracked files, so this remains an external baseline-verification item.
- Assumption A18 selects an already-provisioned Customer schema as the supported release state, but no live database was inspected during this review. The supplied SQL is the intended preflight, not evidence that the target database passes it.
- The design treats the current India Address Control boundary as reusable for successful writes, but the registered production implementation is an explicit deferred adapter that always throws. This is a wrong implementation assumption unless the boundary is delivered separately or the scope is narrowed.
- The absent authoritative FINNOVA-51 through FINNOVA-55/FRS text remains an explicit scope assumption rather than verified product intent. The documents choose the existing Customer Onboarding slice, but that choice still needs product ownership outside this review if the missing story is later recovered.
