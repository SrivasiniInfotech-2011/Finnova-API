# API dependency and build repair

The change repairs restore and compile-time compatibility for `Finnova.Backend.slnx`. It aligns the xUnit package versions, adds the ASP.NET Core framework reference required by the service project, moves persistence-facing application reference contracts into the repository layer, and updates affected identity-verification callers and tests. The coder's report supplies restore, solution-build, test-project-build, and test-run evidence and states that the frontend repository was not touched. It also records nine behavioral test failures, so the evidence supports a build repair but not a green full-suite claim.

Watch for: **confirmed** the report records 555 tests with 546 passing and 9 failing after the ephemeral JWT key was supplied. The failures are outside the stated dependency/restore/build scope according to the report, but they need separate triage before the repository is described as fully healthy.

**Verdict**: APPROVED

## High-level view

The dependency fixes address reported compiler and restore causes: `Finnova.Service` now references `Microsoft.AspNetCore.App`, the redundant options package is removed, and `xunit.extensibility.core` matches the existing xUnit versions. The report says final restore completed without dependency warnings and both the solution and test project built successfully.

The application reference contracts now live in `Finnova.Repository.Interfaces`, allowing repository adapters and service consumers to share them without reversing the project dependency. Identity-verification edits repair stale signatures, the duplicate coordinator declaration, missing imports, and compatibility overloads while retaining the active controller-facing contract.

The repair is limited to the API repository. The report identifies unrelated working-tree changes and says they were preserved; no frontend path appears in status. The known behavioral failures remain visible rather than being suppressed.

<details>
<summary>Issues (1)</summary>

1. **Known behavioral test failures** — **confirmed**: the report records 9 failures after the test project was rebuilt and run with an ephemeral JWT key. Triage them, or establish and document a baseline, before treating the entire solution as behaviorally green; they do not block approval of this dependency/restore/build repair.

</details>

<details>
<summary>Details</summary>

### Dependency graph repaired at the correct boundaries

`Finnova.Tests/Finnova.Tests.csproj` changes `xunit.extensibility.core` from 2.9.3 to 2.9.2, matching the explicitly requested `xunit` and `xunit.core` versions described in the report. `Finnova.Service` adds the `Microsoft.AspNetCore.App` framework reference and removes `Microsoft.Extensions.Options.ConfigurationExtensions`, which the report identifies as redundant after that reference was added. These changes address NU1608 and NU1510 directly. No warning suppression, warning-as-error relaxation, or code removal appears in the reported repair diff.

The application reference-port move fixes the dependency direction instead of adding a repository-to-service project reference. Repository adapters implement contracts under `Finnova.Repository.Interfaces`, and service consumers use those contracts through the existing repository reference.

### Compile-time compatibility fixes preserve the active API boundary

The identity-verification changes correct `IProviderCallExecutor` argument ordering, use the protected-document lookup method exposed by the repository, and add the non-generic `ProtectedDocumentLifecycle.UploadAndExecuteAsync` overload used by controller callbacks. The exception constructors assign constructor-dependent properties inside the constructor, fixing the out-of-scope parameter error while retaining the existing messages and error codes. The duplicate workflow contract is retained as `ILegacyVerificationWorkflowCoordinator`; the active `IVerificationWorkflowCoordinator.ExecuteAsync` contract remains the one registered by `Finnova.SystemAdminService`.

The test edits follow the current model enum and adapter signatures. The property-based exception assertion becomes equivalent explicit exception capture, and no assertion is removed or relaxed in the reviewed diff. This supports a compile repair rather than masking a failing behavior.

### Verification is strong for compilation, bounded for runtime behavior

The report gives final evidence for `dotnet restore`, the solution build with `--no-restore`, and the dedicated test-project build with `--no-build --no-restore`. It reports no final dependency warnings, successful builds, and four remaining nullable warnings. It does not claim those warnings were fixed or suppressed.

**Confirmed** by the report, the test evidence is not fully green. Without `FINNOVA_TEST_JWT_KEY`, 14 tests failed because of the missing environment prerequisite; with a process-scoped ephemeral key, 546 of 555 passed and 9 failed. The listed failures cover identity-verification response expectations, an amortization property, an identity property, and application-processing behavior. Given the stated build-only scope and the report's explicit disclosure, this is a follow-up runtime-verification concern rather than a blocker for this gate.

</details>

<details>
<summary>File map</summary>

The repair touches `Finnova.Service/Finnova.Service.csproj`, `Finnova.Tests/Finnova.Tests.csproj`, identity-verification compatibility code under `Finnova.Models` and `Finnova.Service/IdentityVerification`, application reference contracts and adapters under `Finnova.Repository` and `Finnova.Service/Applications`, the `Finnova.SystemAdminService` registration, and affected tests under `Finnova.Tests`. The report lists the task-owned files and identifies unrelated working-tree changes separately. The full working-tree diff is available with `git -C d:\Projects\Finnova\Finnova-API diff`.

</details>
