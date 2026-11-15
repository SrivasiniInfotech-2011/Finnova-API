# Implementation Tasks — Resolve Build Errors and Test Cases

**Ticket:** Resolve the build errors and test cases  
**Scope:** Verification-first repair of the existing backend and frontend repositories.  
**Execution rule:** Run these tasks in order. This document-authoring workflow has not run build, restore, npm, or test commands. Preserve the current dirty worktree; do not reset, clean, delete, skip, weaken, or remove tests.

## Task 1 — CAPTURE/VERIFY the baseline first

**Requirements:** 1.1–1.5, 6.1, 6.6, 7.3–7.4, 8.1–8.2

Before editing source, capture the current branch, worktree, repository accessibility, prerequisite state, and command results. Historical reports in `.agents/tasks` are evidence only and must be labelled historical in the repair record.

Run these commands from PowerShell. Save command output and exit codes under `d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\baseline\`. Do not save a JWT key or any bearer token in the output.

```powershell
$baseline = "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\baseline"
New-Item -ItemType Directory -Force -Path $baseline | Out-Null

Set-Location "d:\Projects\Finnova\Finnova-API"
git status --short --branch 2>&1 | Tee-Object "$baseline\git-status.txt"
git diff --stat 2>&1 | Tee-Object "$baseline\git-diff-stat.txt"
Get-Content ".agents\tasks\resolve-build-errors-test-cases\findings.md" 2>&1 | Tee-Object "$baseline\findings-copy.txt"
Test-Path "d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx"
Test-Path "E:\Finnova\Finnova-UI\Finnova-UI"

$backendBuildOutput = dotnet build "d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx" 2>&1
$backendBuildOutput | Tee-Object "$baseline\backend-build-before.txt"
$backendBuildExitCode = $LASTEXITCODE
"ExitCode=$backendBuildExitCode" | Add-Content "$baseline\backend-build-before.txt"

$backendTestOutput = dotnet test "d:\Projects\Finnova\Finnova-API\Finnova.Tests\Finnova.Tests.csproj" 2>&1
$backendTestOutput | Tee-Object "$baseline\backend-test-before.txt"
$backendTestExitCode = $LASTEXITCODE
"ExitCode=$backendTestExitCode" | Add-Content "$baseline\backend-test-before.txt"
```

If authentication-dependent tests fail because `FINNOVA_TEST_JWT_KEY` is absent or blank, record that prerequisite failure without claiming the suite passed. Do not persist the key. If the UI path is reachable, inspect its actual scripts and configuration before running commands:

```powershell
Set-Location "E:\Finnova\Finnova-UI\Finnova-UI"
Get-Content ".\package.json" 2>&1 | Tee-Object "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\baseline\frontend-package.json.txt"
Get-ChildItem -Force -Name "tsconfig*.json", "vite.config.*", "vitest.config.*" 2>&1 | Tee-Object "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\baseline\frontend-config-list.txt"
```

If the UI path is unavailable, save the failed path check and mark frontend verification blocked. Do not invent a frontend diagnostic or change a frontend file. Preserve the existing modified host/gateway files and untracked Loan Account, service-auth, and test files. If an unknown error appears, record its command, path, exit code, and relevant output before editing it.

## Task 2 — CAPTURE/VERIFY and conditionally apply backend build fixes

**Requirements:** 2.1–2.6, 5.1–5.5, 7.1–7.4

Use the baseline diagnostics to decide whether a historical repair is absent in the current checkout. Do not apply a historical patch solely because a report mentions it. For every category below, modify only the named owning boundary when the current diagnostic proves it is required. If the diagnostic is absent, preserve the current source.

- **xUnit package conflict (NU1608):** inspect `Finnova.Tests/Finnova.Tests.csproj`. If the diagnostic is current and the package family is misaligned, align `xunit.extensibility.core` with the existing xUnit 2.9.2 family. Keep compatible test packages and project references; do not add a duplicate package, suppress NU1608, or remove tests.
- **Missing ASP.NET Core types:** inspect `Finnova.Service/Finnova.Service.csproj`. If the compiler identifies HTTP or hosting abstractions and the framework reference is absent, add the existing project’s `Microsoft.AspNetCore.App` framework reference inside its framework-reference item group. Do not add a reverse project reference or an unrelated package.
- **Repository/service dependency direction:** inspect `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`, repository adapters, service consumers, `Finnova.Repository/DependencyInjection.cs`, and project references. If a current diagnostic shows repository adapters depending on `Finnova.Service`, restore the shared application reference port at the repository-facing boundary and update only the affected consumers and registrations. Do not make `Finnova.Repository` reference `Finnova.Service`.
- **Malformed source and stale API categories:** use the exact compiler file and line from the baseline to inspect the amortization switch syntax, identity-verification imports and argument order, duplicate workflow coordinator declarations, exception constructors, provider executor signatures, and FluentValidation rules. Correct the owning declaration or caller to the current API contract. Do not invent replacement signatures from the historical report when the current compiler output does not identify that category.
- **Ambiguous test `Program`:** if the compiler reports an ambiguous host type, change only the affected test reference or alias to the intended host’s `Program`. Keep the host authentication and authorization pipeline unchanged.
- **Dirty in-progress work:** if the diagnostic comes from untracked Loan Account/Application Account, service-auth, or modified host/gateway work, preserve those files and record the scope conflict. Do not reset, clean, delete, overwrite, or silently absorb that work into this repair.

For each applied source change, record the exact diagnostic and file path in `d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\baseline\backend-repair-record.md`. Do not create a migration or change database schema. Run the final backend build and test only in Task 6.

## Task 3 — MODIFY `Finnova.Tests/Unit/IdentityVerificationProperties.cs` and CAPTURE/VERIFY other backend test repairs

**Requirements:** 3.1–3.9, 4.1–4.4, 5.1–5.4, 8.3–8.5

### 3.1 MODIFY the confirmed exact-type assertion

The findings identify a current test-quality defect in `InvalidRecordShapeIsRejected`: the existing catch accepts derived exception types. In `Finnova.Tests/Unit/IdentityVerificationProperties.cs`, replace the exact existing method body below, located immediately after `IdentifierAndComparisonDomainsAreExclusive`:

```csharp
    [Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
    {
        var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
        var threw = false;
        try { IdentityVerificationInvariants.EnsureResultDomain(invalid); }
        catch (InvalidOperationException) { threw = true; }
        return threw.ToProperty();
    }
```

with this complete replacement:

```csharp
    [Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
    {
        var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
        var exception = Record.Exception(() => IdentityVerificationInvariants.EnsureResultDomain(invalid));
        return (exception is not null && exception.GetType() == typeof(InvalidOperationException)).ToProperty();
    }
```

Keep `[Property(MaxTest = 100)]`, the malformed record generator, and the `IdentityVerificationInvariants.EnsureResultDomain` call. This is the only backend source/test edit specified without a current command diagnostic.

### 3.2 CAPTURE/VERIFY the other backend test categories

Use the baseline output and current contracts before editing. The exact historical names of all nine failures are not recoverable from the findings, so do not invent method names. Apply a change only to the affected method identified by the current test output, then record its path, diagnostic, and contract being preserved.

- **Multipart authorization:** inspect `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs` and the affected methods in `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`. Where the endpoint declares `multipart/form-data`, replace only the malformed request-content construction with the existing controller’s multipart field/file shape. Do not remove `[Consumes]`, loosen authorization, or use the wrong media type to obtain a 401/403 result.
- **Identity middleware response:** inspect `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` and the failing response assertion. Assert the existing stable error code and a non-empty correlation identifier from the actual response contract. Do not add a made-up response property or weaken the assertion to status-only.
- **Partial workflow result:** inspect `VerificationWorkflowResponse` and the failing full-workflow test. Assert the addressable nullable result property is present as JSON `null` when the operation did not execute. Do not fabricate a success/failure result or accept a missing property if the contract exposes the property.
- **Amortization determinism:** inspect `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs` and the calculator result types. Compare scalar values and installment values by value, not by object reference. Preserve the existing generated inputs and at least 100 property iterations.
- **Generated identity outcomes:** inspect the generated seed-to-status mapping in the failing identity property test. Assert the status produced by each generated adapter outcome, including failure and unavailable outcomes, while retaining independent adapter-call and independent-result checks.
- **Provider-neutral comparison input:** inspect the actual public members of `ProviderNeutralComparisonInput` and the reflection test. Include `ActorReference` when it exists and reject provider-, secret-, and token-specific members. Do not change the public contract merely to fit a stale test list.
- **Application not-found:** verify `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`, `ApplicationNotFoundException`, and `Finnova.Tests/ApplicationProcessing/ApplicationProcessingIntegrationTests.cs`. Preserve HTTP 404 and `ERR-APP-404`; repair the owning exception mapping or test host only if the current diagnostic proves it is missing. Do not change the expected result to 500.
- **Document extension/MIME/signature mismatch:** inspect `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs` and `Finnova.Tests/Unit/IdentityVerificationExamples.cs`. Ensure the `.png`/`application/pdf` mismatch and signature mismatch are rejected before provider or persistence work. Do not broaden accepted formats or remove validation.
- **EF InMemory test host:** if an integration startup diagnostic identifies inherited SQL/provider options, inspect `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs` and remove only the inherited provider configuration before registering the existing InMemory provider. Do not alter production SQL registration or add a migration.

Retain all affected tests, property iteration counts, authentication checks, stable error codes, and English-only India-compatible fixtures. Do not skip, delete, broaden, or weaken an assertion to make the suite green.

## Task 4 — CAPTURE/VERIFY and apply frontend build fixes after checkout access is restored

**Requirements:** 6.1–6.6, 7.2, 7.5, 8.1–8.4

The findings record `E:\Finnova\Finnova-UI\Finnova-UI` as unreachable. No frontend code change is authorized until the checkout is accessible and its actual configuration has been inspected. When access is available, run these commands and save output under `d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\frontend\`:

```powershell
$frontendRecord = "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\frontend"
New-Item -ItemType Directory -Force -Path $frontendRecord | Out-Null
Set-Location "E:\Finnova\Finnova-UI\Finnova-UI"
Get-Content ".\package.json" | Tee-Object "$frontendRecord\package.json.txt"
Get-ChildItem -Force -Name "tsconfig*.json", "vite.config.*", "vitest.config.*" | Tee-Object "$frontendRecord\config-list.txt"

$frontendBuildOutput = npm run build 2>&1
$frontendBuildOutput | Tee-Object "$frontendRecord\build-before.txt"
$frontendBuildExitCode = $LASTEXITCODE
"ExitCode=$frontendBuildExitCode" | Add-Content "$frontendRecord\build-before.txt"
```

If `package.json` names a different build script, use that checked-in script and record the difference. For each current TypeScript/Vite failure, record the exact path and diagnostic before editing, then change only the owning model, service, component, import, or configuration. Preserve React hooks/context, MUI, the shared axios instance at `src/services/api.ts`, the mock/real service toggle, existing API routes and response shapes, and English-only display values. Do not add a new HTTP client or state library. If the checkout remains unreachable, record the block and make no speculative frontend edit.

## Task 5 — CAPTURE/VERIFY and apply frontend test fixes after checkout access is restored

**Requirements:** 6.1–6.6, 7.2, 7.5, 8.1–8.4

Inspect the actual Vitest and jsdom configuration and the test script before choosing the command. Save the current result under the same frontend record directory. The expected command from the product context is `npm test`; use the repository’s actual script if `package.json` differs.

```powershell
Set-Location "E:\Finnova\Finnova-UI\Finnova-UI"
$frontendTestOutput = npm test 2>&1
$frontendTestOutput | Tee-Object "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\frontend\test-before.txt"
$frontendTestExitCode = $LASTEXITCODE
"ExitCode=$frontendTestExitCode" | Add-Content "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\frontend\test-before.txt"
```

For each current failure, identify the owning service, mock/real implementation, component/page interaction, or assertion from the test output. Repair only that owner and record the API contract being preserved. Keep React Testing Library/jsdom behavior and existing mock/real selection. Do not skip tests, remove tests, make assertions less strict, introduce an ad hoc axios client, or change a backend route/response shape without scope confirmation. If the frontend checkout remains unreachable, mark the frontend test verification blocked rather than green.

## Task 6 — VERIFY the complete platform and report current results

**Requirements:** 1.1–1.5, 2.1–2.6, 3.9, 4.4, 5.1–5.5, 6.1–6.6, 7.1–7.5, 8.1–8.5

Run the final checks only after the conditional repairs above. Supply the JWT key only for the current PowerShell process. Do not print, save, commit, or place the key in source, configuration, runsettings, logs, reports, or Git history.

```powershell
$env:FINNOVA_TEST_JWT_KEY = [Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Maximum 256) }))
$final = "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\final"
New-Item -ItemType Directory -Force -Path $final | Out-Null

Set-Location "d:\Projects\Finnova\Finnova-API"
$backendBuildOutput = dotnet build "d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx" 2>&1
$backendBuildOutput | Tee-Object "$final\backend-build.txt"
$backendBuildExitCode = $LASTEXITCODE
"ExitCode=$backendBuildExitCode" | Add-Content "$final\backend-build.txt"

$backendTestOutput = dotnet test "d:\Projects\Finnova\Finnova-API\Finnova.Tests\Finnova.Tests.csproj" 2>&1
$backendTestOutput | Tee-Object "$final\backend-test.txt"
$backendTestExitCode = $LASTEXITCODE
"ExitCode=$backendTestExitCode" | Add-Content "$final\backend-test.txt"

Remove-Item Env:FINNOVA_TEST_JWT_KEY
```

If the frontend checkout is accessible, run its actual scripts and save both results:

```powershell
Set-Location "E:\Finnova\Finnova-UI\Finnova-UI"
$frontendBuildOutput = npm run build 2>&1
$frontendBuildOutput | Tee-Object "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\final\frontend-build.txt"
$frontendBuildExitCode = $LASTEXITCODE
"ExitCode=$frontendBuildExitCode" | Add-Content "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\final\frontend-build.txt"

$frontendTestOutput = npm test 2>&1
$frontendTestOutput | Tee-Object "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\final\frontend-test.txt"
$frontendTestExitCode = $LASTEXITCODE
"ExitCode=$frontendTestExitCode" | Add-Content "d:\Projects\Finnova\Finnova-API\.agents\tasks\resolve-build-errors-test-cases\final\frontend-test.txt"
```

Report each current command, repository path, exit code, failed/skipped count, and relevant diagnostic separately from historical reports. Completion requires zero build errors, zero failed tests, and zero skipped tests for every accessible target. If a command remains blocked or fails, report the unresolved condition and stop; do not declare the ticket complete. Review the final diff to confirm unrelated dirty files remain intact, no public contract or route changed without scope confirmation, no test was removed/skipped/weakened, and no JWT key was persisted.

## Residual assumptions requiring confirmation

- The frontend checkout at `E:\Finnova\Finnova-UI\Finnova-UI` will become accessible; until then, frontend verification is blocked rather than passing.
- Existing modified host/gateway files and untracked Loan Account/Application Account, service-auth, and test files are pre-existing work to preserve, not implicit scope for this ticket.
- The clear-default bank-account contract remains unresolved: product must choose whether zero defaults are allowed, a replacement default is selected, or the operation is deferred. This repair must not silently loosen the current invariant.
- Ownership of the `JvPostingInbox` schema and migration remains unresolved. No migration is added or applied without confirmation.
- The current baseline, rather than the historical reports, determines whether any conditional backend or frontend repair is needed.
