# API build repair report

## Scope

Repaired the .NET API solution at `d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx`. The frontend repository was not touched. The working tree already contained unrelated modified and untracked files before this task; those changes were preserved.

No `review.json` existed at `.agents/tasks/fix-api-build/review.json`, so this was treated as the first iteration.

## Files changed by this task

- `Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs`
- `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs` (new)
- `Finnova.Repository/Repositories/ApplicationReferenceAdapters.cs`
- `Finnova.Repository/Repositories/LookupReferenceAdapter.cs`
- `Finnova.Service/Finnova.Service.csproj`
- `Finnova.Service/Applications/Validation/ApplicationValidation.cs`
- `Finnova.Service/Applications/Ports/ApplicationReferencePorts.cs`
- `Finnova.Service/Applications/References/ApplicationReferenceValidator.cs`
- `Finnova.Service/Applications/Queries/ReferenceQueries.cs`
- `Finnova.Service/IdentityVerification/Commands/ComparisonCommandHandlers.cs`
- `Finnova.Service/IdentityVerification/External/ConfiguredIdentifierAdapter.cs`
- `Finnova.Service/IdentityVerification/External/IProviderCallExecutor.cs`
- `Finnova.Service/IdentityVerification/External/ProviderCallExecutor.cs`
- `Finnova.Service/IdentityVerification/Storage/ProtectedDocumentLifecycle.cs`
- `Finnova.Service/IdentityVerification/Workflow/IVerificationWorkflowCoordinator.cs`
- `Finnova.Service/IdentityVerification/Workflow/VerificationWorkflowCoordinator.cs`
- `Finnova.AccountsService/Controllers/ApplicationProcessingController.cs`
- `Finnova.SystemAdminService/Program.cs`
- `Finnova.Tests/Finnova.Tests.csproj`
- `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs`
- `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`
- `Finnova.Tests/Integration/ProtectedDocumentTransactionTests.cs`
- `Finnova.Tests/Unit/IdentityVerificationProperties.cs`

## Root causes and fixes

- `Finnova.Tests.csproj` explicitly requested `xunit.extensibility.core` 2.9.3 while `xunit` 2.9.2 and `xunit.core` required exactly 2.9.2. The explicit reference was aligned to 2.9.2, removing the NU1608 restore warnings.
- `IdentityVerificationPersistenceException` initialized a property from a constructor parameter in a member initializer. The parameter is out of scope there, producing CS0103. The property is now assigned in the constructor. The validation exception also received the collection-based constructor used by existing callers.
- `Finnova.Service` used ASP.NET Core HTTP and hosting abstractions but was an ordinary SDK class library without the ASP.NET Core framework reference. `Microsoft.AspNetCore.App` was added as a framework reference. The redundant `Microsoft.Extensions.Options.ConfigurationExtensions` package reference was removed after the framework reference caused NU1510.
- Repository adapters implemented interfaces declared only in `Finnova.Service`, while `Finnova.Service` already referenced `Finnova.Repository`. That one-way dependency made the repository project unable to compile. The four persistence-facing reference ports were moved to `Finnova.Repository.Interfaces`; service consumers and adapter registrations now use the shared repository contracts.
- `AmortizationCalculator` had a missing comma in its switch expression after the `Annual` arm.
- Two identity-verification workflow contracts declared the same `IVerificationWorkflowCoordinator` type. The obsolete contract was retained under `ILegacyVerificationWorkflowCoordinator`, while the controller-facing `ExecuteAsync` contract remains the active one.
- Several identity service and host files had missing imports or stale method argument ordering. These were corrected without changing the active API route shapes.
- `UpdateApplicationCommandValidator` used FluentValidation's removed `Transform` API. Its create-command rules were expressed directly for the update command instead.
- Tests referenced stale adapter enum/configuration names and an ambiguous `Program` type because both API hosts are referenced. Tests now use the model enums/current adapter signature and target the Accounts controller as the application-processing factory entry point. Test-only FsCheck and lifecycle call sites were updated to match the current APIs.

## Verification commands and results

1. `git -C d:\Projects\Finnova\Finnova-API status --short --branch`
   - Confirmed the repository was on `main` with pre-existing unrelated changes.
2. `dotnet restore d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx`
   - Initial restore succeeded with two NU1608 xUnit version warnings.
   - Final restore succeeded with no dependency warnings.
3. `dotnet build d:\Projects\Finnova\Finnova-API\Finnova.Backend.slnx --no-restore --verbosity:minimal`
   - Final result: **Build succeeded**.
   - The build still reports four existing warnings: three nullable warnings in identity-verification service code and one nullable connection-string warning in `Finnova.UAService/Program.cs`.
4. `dotnet build d:\Projects\Finnova\Finnova-API\Finnova.Tests\Finnova.Tests.csproj --no-restore --verbosity:minimal`
   - Result: **Build succeeded**.
   - The dedicated test-project build reported warnings only.
5. `dotnet test d:\Projects\Finnova\Finnova-API\Finnova.Tests\Finnova.Tests.csproj --no-build --no-restore --verbosity:minimal`
   - Result: failed because `FINNOVA_TEST_JWT_KEY` was not set. It reported 555 tests, 14 failures.
6. `$env:FINNOVA_TEST_JWT_KEY='<ephemeral development key>'; dotnet test d:\Projects\Finnova\Finnova-API\Finnova.Tests\Finnova.Tests.csproj --no-build --no-restore --verbosity:minimal`
   - Result: 555 tests, 546 passed, 9 failed.
   - The ephemeral key was process-scoped and was not written to the repository or configuration.

## Remaining blockers

The solution and test project now compile. The test suite still has nine behavioral/environment failures that are outside the dependency/build repair:

- Three identity-verification integration assertions receive `UnsupportedMediaType` instead of the expected authorization statuses, and one middleware assertion cannot find the expected response property.
- The full-workflow partial-input assertion reports an unexpected result field.
- `AmortizationCalculatorProperties.Deterministic` fails because the result comparison is not a meaningful value equality check for the current result type.
- `IdentityVerificationProperties.ValidIdentifiersPreserveOperationIndependence` fails for a generated seed because the test expects `ValidationStatus.Success` while the generated failure case returns another status.
- `IdentityVerificationProperties.ComparisonInputContainsOnlyProviderNeutralFields` fails because the current neutral input contract's public-field expectation differs from the test assertion.
- `ApplicationProcessingIntegrationTests.AuthorizedUnknownIdReturns404AndStableCode` receives HTTP 500 instead of 404.
- `IdentityVerificationExamples.UnsupportedDocumentExtensionOrMimeTypeIsRejected` does not throw for the `.png`/`application/pdf` case.

These failures require product/test-behavior decisions rather than dependency or compilation changes, so they were not changed as part of this build repair. No commit was created and nothing was pushed.
