# API test failure resolution report

## Scope and working-tree handling

The task was limited to the API repository at `d:\Projects\Finnova\Finnova-API`. The frontend repository was not touched. The initial `git status --short` showed substantial existing modified and untracked work, including the prior build-repair changes and the application-processing feature. Those changes were preserved. No commit or push was performed.

No `.agents/tasks/resolve-api-test-failures/review.json` existed at the start, so this was handled as the first iteration.

The source tree already contained the minimal fixes corresponding to the nine failures listed in `.agents/tasks/fix-api-build/report.md`. After rebuilding from source, the focused tests and the complete suite pass. No additional production or test source change was necessary in this iteration. The report itself is the only file created by this iteration.

## Reported failures and root causes

The prior report grouped the remaining failures as follows. The current working-tree fixes that resolve them are recorded here for verification:

- Identity-verification authorization tests sent JSON to endpoints declared with `Consumes("multipart/form-data")`. The protected-request fixtures now send multipart content, allowing authentication and authorization middleware to run before model binding. The comparison form also normalizes a missing submitted value to an empty string so validation returns the identity-verification 400 contract instead of an unsupported-media-type response.
- The identity middleware response contract requires the stable `code` extension and correlation identifier. The middleware now emits those properties, and the identity integration assertion reads those contract properties rather than an absent property.
- A partial full-workflow response has addressable result properties even when an operation is not run. The partial-input assertion now verifies that `aadhaarAddressResult` exists with a JSON null value, matching the response record shape and preserving coverage of independent results.
- The amortization deterministic property compared installment objects with reference equality. It now compares the deterministic scalar summary values and the installment sequence, which is meaningful for the current result type.
- The identifier-independence property constructed failure and unavailable adapter results but always expected success for PAN. It now expects the generated PAN and Aadhaar statuses, while still checking one call per adapter and independent result instances.
- The provider-neutral comparison input contract exposes `ActorReference`, not the removed cancellation-token property. The reflection assertion now matches the current public contract and rejects provider-, secret-, and token-specific fields.
- Application not-found exceptions were falling through to the generic 500 branch. Accounts middleware now maps `ERR-APP-404` to HTTP 404 and preserves the stable error code in the response.
- Document validation checked the accepted MIME type, extension, and file signature together. The `.png`/`application/pdf` mismatch therefore fails the format check even when the supplied bytes look like a PDF, satisfying the unsupported-extension/MIME example.

## Files associated with the fixes already present in the working tree

These files were already modified before this iteration and were not overwritten or reverted:

- `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`
- `Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs`
- `Finnova.Service/IdentityVerification/Commands/ComparisonCommandHandlers.cs`
- `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs`
- `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs`
- `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`
- `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingIntegrationTests.cs`
- `Finnova.Tests/Integration/IdentityVerificationApiTests.cs`
- `Finnova.Tests/Unit/IdentityVerificationExamples.cs`
- `Finnova.Tests/Unit/IdentityVerificationProperties.cs`

No additional source files were changed by this iteration. The completion report was created at `.agents/tasks/resolve-api-test-failures/report.md`.

## Verification

All commands below were run from `d:\Projects\Finnova\Finnova-API`. The JWT key was assigned only inside each PowerShell process invocation. It was not persisted in the environment, source, configuration, or report.

1. Initial inspection:
   - `git status --short; git diff --stat`
   - Confirmed the existing modified/untracked worktree and reviewed `.agents/tasks/fix-api-build/report.md` plus the current diff.
2. Reproduction with the required ephemeral key:
   - `$env:FINNOVA_TEST_JWT_KEY='ephemeral-development-key-for-test-process-only-32chars'; dotnet test "Finnova.Tests\\Finnova.Tests.csproj" --no-build --no-restore --verbosity:minimal`
   - Result: **555 total, 555 passed, 0 failed, 0 skipped**.
3. Rebuild from current source:
   - `dotnet build "Finnova.Tests\\Finnova.Tests.csproj" --no-restore --verbosity:minimal`
   - Result: **Build succeeded** with 5 existing nullable/preview warnings and no errors.
4. Focused verification:
   - `$env:FINNOVA_TEST_JWT_KEY='ephemeral-development-key-for-test-process-only-32chars'; dotnet test "Finnova.Tests\\Finnova.Tests.csproj" --no-build --no-restore --filter "FullyQualifiedName~IdentityVerification|FullyQualifiedName~ApplicationProcessing|FullyQualifiedName~AmortizationCalculator" --verbosity:minimal`
   - Result: **92 total, 92 passed, 0 failed, 0 skipped**.
5. Complete post-build verification:
   - `$env:FINNOVA_TEST_JWT_KEY='ephemeral-development-key-for-test-process-only-32chars'; dotnet test "Finnova.Tests\\Finnova.Tests.csproj" --no-build --no-restore --verbosity:minimal`
   - Result: **555 total, 555 passed, 0 failed, 0 skipped**.

## Remaining failures

None. The complete `Finnova.Tests` suite passes after rebuilding from the current source. The build retains five warnings, but no build or test failures remain.
