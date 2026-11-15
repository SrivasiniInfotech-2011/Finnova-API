# Backend build and test repair

The reviewed change repairs the Demand Processing backend build by restoring the repository/service dependency boundary, moving service registrations to the service composition root, adding QuestPDF, and binding the integration test to the Accounts host explicitly. `docs/backend-build-fixes.md` records the final solution build, test run, and host health checks. The reviewed commit does not introduce frontend changes, and the fixes document identifies the checkout's pre-existing uncommitted Demand work as preserved.

Watch for: confirmed non-blocking CRLF whitespace warnings are recorded in the fixes document; no build, test, or host-health blocker is recorded.

**Verdict**: APPROVED

## High-level view

The repair establishes `IDemandRepository` in the Repository project and keeps service-only Demand ports in the Service project. Hosts retain separate worker and authorization responsibilities, while the fixes document provides the recorded build, test, and health evidence.

<details>
<summary>Issues (0)</summary>

No blocking concerns were found in the reviewed task changes.

</details>

<details>
<summary>Details</summary>

### Dependency boundary restored between Repository and Service

Confirmed: the repository project no longer needs service-layer types for the Demand repository contract or registration. `IDemandRepository` now owns the persistence and query surface in `Finnova.Repository.Interfaces`, `DemandRepository` implements that contract, and `Finnova.Repository/DependencyInjection.cs` registers it without importing `Finnova.Service`. The service command layer imports the repository-owned contract while retaining Demand Processing ports for service-side dependencies.

Confirmed: the commit adds the QuestPDF package to `Finnova.Service`, qualifies `Document.Create` in the renderer, and corrects the validator overload described by the fixes record. The recorded repository, service, test-project, and solution builds provide evidence that the reported compile failures were addressed in the current checkout.

### Demand host composition and authentication boundary

Confirmed: shared service registration now includes the Demand adapters, lease store, clock, processor, queue, PDF export service, and renderer. The Accounts host retains the hosted worker and defines the Demand policies needed by its controller. The recorded Accounts and SystemAdmin `/health` checks verify host startup after the composition change.

Confirmed: the integration test factory is anchored to `Finnova.AccountsService.Controllers.DemandProcessingController`, avoiding the two-host global `Program` ambiguity. The tests assert unauthorized responses for missing and invalid tokens and reject the attempted generation bypass route, giving direct coverage for the newly wired authentication boundary.

### Verification record and change scope

Confirmed: `docs/backend-build-fixes.md` states the exact solution and test commands, reports the baseline compiler failure, records `634 passed, 0 failed, 0 skipped`, and records HTTP 200 responses from both host health endpoints. It also states that temporary host processes were stopped, secrets were not reproduced, and no SQL Server or LocalDB blocker occurred. This satisfies the task's required proof without requiring a rerun during this review.

Confirmed: the commit diff against `origin/main` is limited to the repository contract and registration, Demand service composition and renderer dependency, Accounts host wiring, the Demand integration test, and the fixes document. The current checkout contains additional uncommitted Demand implementation/configuration files; the fixes document identifies them as pre-existing and preserved, so they are not treated as unrelated modifications made by this repair.

The fixes document records `git diff --check` CRLF warnings on dirty files and explains why the files were not mass-converted. Those warnings are non-blocking and do not contradict the recorded successful build or test results.

</details>

<details>
<summary>File map</summary>

- `Finnova.AccountsService/Program.cs` — Demand authorization policies and Accounts worker wiring.
- `Finnova.Repository/DependencyInjection.cs` — repository registration for `IDemandRepository`.
- `Finnova.Repository/Interfaces/IDemandRepository.cs` — repository-owned Demand contract.
- `Finnova.Repository/Repositories/DemandRepository.cs` — Demand query and persistence implementation.
- `Finnova.Service/DemandProcessing/Commands/DemandCommands.cs` — service handlers consuming the repository contract.
- `Finnova.Service/DemandProcessing/PdfExport/QuestPdfRenderer.cs` — QuestPDF renderer dependency qualification.
- `Finnova.Service/DemandProcessing/Ports/DemandPorts.cs` — service-side Demand ports.
- `Finnova.Service/DependencyInjection.cs` — shared Demand service composition.
- `Finnova.Service/Finnova.Service.csproj` — QuestPDF package reference.
- `Finnova.Tests/DemandProcessing/DemandProcessingIntegrationTests.cs` — explicit Accounts host test factory.
- `docs/backend-build-fixes.md` — diagnosis, fixes, and verification record.

Full diff: `git diff origin/main...HEAD` from `d:\Projects\Finnova\Finnova-API`.

</details>
