# Integration Test Fix Verification

## Prior review iteration

`.agents/tasks/fix-integration-tests/review.json` was checked before diagnosis and was not present, so this was treated as the first iteration.

## Reproduction before the fix

Command:

```powershell
dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~JvPostingApiTests" --no-restore --logger "console;verbosity=detailed"
```

Result: failed. The test run discovered one test and reported:

- `Finnova.Tests.JVPosting.JvPostingApiTests.Submission_requires_authentication` — failed
- Exception: `System.InvalidOperationException: The entity type 'JvPostingInbox' requires a primary key to be defined.`
- The exception occurred while `SystemAdminAppFactory` initialized the EF Core InMemory database during `TestApplicationFactory.Create()`, before the request could reach authentication.
- Summary: `total: 1, failed: 1, succeeded: 0, skipped: 0`

## Root cause

`FinnovaDbContext` exposed `DbSet<JvPostingInbox>`, so EF Core included `JvPostingInbox` in the model. The entity's key property is named `RequestId`, not `Id`, and no EF configuration declared it as the primary key. EF model validation therefore failed when the integration fixture built the InMemory model.

## Fix

Added `JvPostingInboxConfiguration`, discovered by the existing `ApplyConfigurationsFromAssembly` call, with `RequestId` explicitly configured as the primary key. This is the smallest change required to make the existing host fixture construct a valid EF model. No controller, authentication, authorization, or test behavior was changed.

## Verification after the fix

Targeted command:

```powershell
dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~JvPostingApiTests" --no-restore --logger "console;verbosity=minimal"
```

Result: passed. `total: 1, failed: 0, succeeded: 1, skipped: 0`.

Broader affected integration scope:

```powershell
dotnet test Finnova.Tests/Finnova.Tests.csproj --filter "FullyQualifiedName~Finnova.Tests.Integration" --no-restore --logger "console;verbosity=minimal"
```

Result: passed. `total: 248, failed: 0, succeeded: 248, skipped: 0`.

The runs completed with existing compiler warnings, but no test failures.

## Files changed for this fix

- `Finnova.Repository/Configuration/JvPostingInboxConfiguration.cs` — added explicit `RequestId` primary-key mapping.
- `.agents/tasks/fix-integration-tests/verification.md` — this verification record.
