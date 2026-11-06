# Resolve the nine remaining API test failures

The working-tree changes address the nine failures reported by the build-repair pass across identity-verification validation, workflow serialization, application-processing host wiring, and stale property expectations. The production changes are narrow: they align multipart binding with domain validation, enforce extension/MIME agreement, and remove the conflicting EF provider registration from the integration factory. The coder's report includes successful test-project and solution builds plus a process-scoped-key run of all 555 tests with 555 passed, 0 failed, and 0 skipped. **Watch for:** **confirmed** the `InvalidRecordShapeIsRejected` property was changed from FsCheck's typed throw assertion to a catch that accepts any subtype of `InvalidOperationException`; restore an exact-type assertion before merging.

**Verdict**: NEEDS_CHANGES

## High-level view

The identity-verification fixes preserve the existing trust boundaries. Authorization tests now submit the media type required by the endpoints, missing multipart text is allowed to reach the existing domain validation, and document validation now requires the filename extension to match the supplied MIME type. These are targeted corrections rather than new provider behavior.

The remaining test updates correct stale assumptions about nullable workflow JSON, record-with-list equality, generated identifier outcomes, and the current provider-neutral execution context. The application-processing factory removes the SQL Server options configuration before adding its in-memory provider, which lets the existing not-found exception path produce the tested 404.

The verification evidence is sufficient for the reported nine failures: the report records successful builds, focused passing groups, and a final 555-test run with no skips. The gate remains blocked by one weakened assertion in the working-tree test diff, not by a reported runtime failure.

<details>
<summary>Issues (1)</summary>

1. **Broad exception assertion** — **confirmed**: `IdentityVerificationProperties.InvalidRecordShapeIsRejected` now catches `InvalidOperationException`, which also accepts derived exception types and weakens the previous typed assertion. Restore an exact-type assertion, such as the original FsCheck assertion if it remains supported or an explicit `ex.GetType() == typeof(InvalidOperationException)` check.

</details>

<details>
<summary>Details</summary>

### Multipart validation and document-type alignment

The authorization fixtures use empty `MultipartFormDataContent`, matching the endpoints' `Consumes("multipart/form-data")` contract so authorization is tested instead of request negotiation. Making `ComparisonForm.SubmittedValue` nullable only changes the binding boundary; the controller still maps missing input to an empty string and the existing command validation rejects it. **confirmed**, the production change does not bypass required-value validation. The validator also now matches the filename extension to the supplied MIME type, so the failing `.png`/`application/pdf` case is rejected without adding a new format policy. **confirmed**.

### Updated test contracts

**confirmed**, the workflow assertion now matches the nullable result contract's default JSON `null`, the deterministic property compares schedule values and lines rather than interface-list record equality, and the generated identifier property checks the status it generated. The provider-neutral field assertion includes the existing `ActorReference` execution-context field and still excludes provider, secret, and token names.

The application-processing factory removes both EF options registrations before adding InMemory. **confirmed**, this prevents the SQL Server provider configuration from surviving in the test host and allows the existing `ERR-APP-404` exception branch to produce the tested 404.

### Test assertion precision

The current `InvalidRecordShapeIsRejected` change replaces the prior typed FsCheck throw assertion with a manual boolean. **confirmed**, `catch (InvalidOperationException)` treats a derived exception as equivalent to the required type. This violates the requirement not to weaken tests; restore an exact-type assertion.

### Verification evidence

The coder report records successful builds for the test project and complete backend solution, focused passing runs for each affected group, and a final `Finnova.Tests` run with 555 passed, 0 failed, and 0 skipped under a process-scoped `FINNOVA_TEST_JWT_KEY`. It reports no frontend changes, skipped tests, or deleted tests.

</details>

<details>
<summary>File map</summary>

- `Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs` — requires document extension and MIME type to agree.
- `Finnova.SystemAdminService/Controllers/IdentityVerificationController.cs` — lets missing multipart text reach domain validation.
- `Finnova.Tests/Integration/IdentityVerificationApiTests.cs` — aligns authorization and workflow assertions with endpoint contracts.
- `Finnova.Tests/Unit/IdentityVerificationProperties.cs` — updates stale adapter/property expectations; contains the weakened exception assertion described above.
- `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs` — compares deterministic schedule values and lines.
- `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs` — removes the SQL Server EF configuration before installing InMemory.

The full working-tree diff is available from `git -C d:/Projects/Finnova/Finnova-API diff` and the untracked application-processing test files are included in the coder's reported pre-existing feature work.

</details>
