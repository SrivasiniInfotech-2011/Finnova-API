# Backend Build Fixes

## Scope

This repair covers the backend repository at `d:\Projects\Finnova\Finnova-API`. No frontend repository or source files were changed. Backend verification did not identify a frontend dependency.

The checkout already contained unrelated modified host, configuration, repository-context, and gateway files, plus untracked Demand Processing implementation and test files. Those changes were preserved. No reset, clean, deletion, or line-ending normalization was performed.

## Diagnosis

The baseline solution build failed before tests ran. `Finnova.Repository` imported `Finnova.Service` types even though the project graph points in the opposite direction: `Finnova.Service` references `Finnova.Repository`, while `Finnova.Repository` references only `Finnova.Models`. This produced five `CS0234` errors in the repository dependency-injection file, repository interface, and repository implementation.

The Demand Processing integration test also used an unqualified `Program` marker even though the test project references multiple web hosts. The test factory needed to identify the Accounts host explicitly.

After the dependency-direction correction exposed the next layer of compilation, the service project also needed the QuestPDF package because its PDF renderer is compiled in `Finnova.Service`. The service build then exposed a validator overload issue, missing service namespace imports, and an unqualified `Document` reference that conflicted with the existing `Finnova.Service.Document` namespace.

A runtime composition check exposed one additional issue: SystemAdmin scanned the Demand Processing handlers but did not have an `IDemandWorkQueue` registration. The shared service composition now registers the queue, while the Accounts host retains its hosted worker registration.

## Fixes

- Made `Finnova.Repository.Interfaces.IDemandRepository` the complete repository contract. It now owns run lookup, authorized row/batch queries, search, persistence, save, and transaction operations.
- Removed the reverse dependency from repository code to `Finnova.Service.DemandProcessing.Ports`.
- Updated Demand command consumers to use the repository-owned contract and retained the service ports for service-side dependencies.
- Registered `IDemandRepository` to `DemandRepository` in the repository composition root.
- Moved Demand Processing adapters, lease store, and clock registrations into the service composition root.
- Registered the shared Demand work queue in the service composition root so every host that scans the handlers can validate its service graph. The Accounts worker remains host-specific.
- Removed the duplicate explicit Accounts lease-store registration.
- Added QuestPDF to the service project, where the renderer is compiled.
- Corrected the Demand frequency validator to use the property-value overload.
- Qualified the QuestPDF `Document.Create` call to avoid collision with the service's `Document` namespace.
- Bound Demand Processing integration tests to `WebApplicationFactory<Finnova.AccountsService.Controllers.DemandProcessingController>`.

No production behavior outside the affected Demand Processing composition and compilation boundaries was changed.

## Verification

Commands and results:

| Command | Result |
|---|---|
| `dotnet build Finnova.Backend.slnx --no-restore` | Baseline failed with the five repository `CS0234` errors described above. |
| `dotnet test Finnova.Tests/Finnova.Tests.csproj --no-restore` | Baseline was blocked by the same repository compile errors; no tests ran. |
| `dotnet build Finnova.Repository/Finnova.Repository.csproj` | Passed. |
| `dotnet build Finnova.Service/Finnova.Service.csproj` | Passed after the scoped QuestPDF, registration, validator, and namespace fixes. |
| `dotnet build Finnova.Tests/Finnova.Tests.csproj` | Passed. |
| `dotnet test Finnova.Tests/Finnova.Tests.csproj --logger "console;verbosity=normal"` | Passed: 634 passed, 0 failed, 0 skipped. |
| `dotnet build Finnova.SystemAdminService/Finnova.SystemAdminService.csproj` | Passed. |
| `dotnet build Finnova.Backend.slnx` | Passed with 2 pre-existing/non-blocking compiler-analyzer warnings in the test code. |
| `dotnet test Finnova.Tests/Finnova.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | Passed: 634 passed, 0 failed, 0 skipped. |

### Host checks

Using the checked-in launch profiles:

- Accounts host: `GET http://localhost:5020/health` returned HTTP 200.
- SystemAdmin host: `GET http://localhost:5030/health` returned HTTP 200 after registering the shared Demand work queue.

Temporary host processes were stopped after each check. Health checks do not require a data-backed request, so no SQL Server or LocalDB connection blocker was encountered. Connection strings and secrets are intentionally not reproduced here.

### Warnings

The final build/test output contains non-blocking existing warnings, including nullable-analysis warnings in Identity Verification, an unread Demand lease-store option parameter, a nullable test-double property, and xUnit analyzer warning xUnit1031 in an existing Demand test. They do not cause build or test failure.

`git diff --check` still reports carriage-return characters as trailing whitespace on lines added in pre-existing dirty CRLF files, primarily `Finnova.AccountsService/Finnova.AccountsService.csproj` and Demand policy/queue lines in `Finnova.AccountsService/Program.cs`. The files were not mass-converted because doing so would alter unrelated user changes.
