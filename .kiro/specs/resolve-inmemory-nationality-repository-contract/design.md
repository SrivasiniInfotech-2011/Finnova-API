# Design Document — Resolve In-Memory Nationality Repository Contract

## Overview

Add one method to `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs` so the test double fulfills `INationalityRepository`. The method is a synchronous list lookup wrapped in `Task.FromResult`, matching the implementation style already used by the class.

The behavior is copied from the production repository rather than invented: trim the requested code, require `IsActive`, compare codes case-insensitively, return a cloned entity, and return `null` when no active row matches. The interface, production repository, resolver, domain model, persistence schema, API hosts, gateway, and frontend remain unchanged.

The compiler error occurs because C# interface implementation requires both the parameter contract and the return type to match exactly. Adding the method to the test double is the smallest compatible correction.

## Confirmed findings

- `INationalityRepository.GetActiveByCodeAsync` declares `Task<Nationality?> GetActiveByCodeAsync(string code, CancellationToken ct = default)`.
- `NationalityRepository` trims the input, filters `IsActive`, compares `Code`, and returns `Nationality?` through EF Core.
- `InMemoryNationalityRepository` already has the `Clone` helper and uses `Task.FromResult` for synchronous list operations.
- `NationalityReferenceResolver` awaits the repository method and treats `null` as an active-reference-not-found condition.
- Existing in-memory uniqueness logic already uses `StringComparison.OrdinalIgnoreCase`, establishing the test-double convention for case-insensitive code matching.
- Existing tests mock the interface method and assert that an active nationality is projected by the resolver. The missing fake method prevents the test project from compiling even though the interface contract is already consumed correctly.

## What is reused vs what is new

### Reused

| Existing asset | Design use |
| --- | --- |
| `INationalityRepository.GetActiveByCodeAsync` | Exact method signature and nullable return contract. |
| `NationalityRepository.GetActiveByCodeAsync` | Production behavior reference: trim, active-only, case-insensitive code match, null when absent. |
| `InMemoryNationalityRepository.Clone` | Prevents mutations to stored test data through returned entities. |
| `Task.FromResult` methods in `InMemoryNationalityRepository` | Keeps the fake's synchronous-in-memory implementation style. |
| `NationalityReferenceResolver` | Existing consumer and null/error behavior remain unchanged. |

### New or modified

- **Modify** `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs` by adding the missing `GetActiveByCodeAsync` method.
- No new file, dependency, interface, test project reference, migration, generated artifact, API route, or frontend file is required.

## Requirements coverage map

| Requirement | Design coverage |
| --- | --- |
| R1 exact interface member | The method uses `Task<Nationality?>`, `string code`, and `CancellationToken cancellationToken = default`. |
| R2 active code lookup | The method trims input, filters active rows, compares with `OrdinalIgnoreCase`, and returns `Clone(found)`. |
| R3 active-only state | `x.IsActive` is part of the predicate; no matching active row produces `null`. |
| R4 unwanted mutation/exception | Successful results are cloned; no match uses `Task.FromResult<Nationality?>(null)` through the conditional expression. |
| R5 optional test-double use | No consumer or registration changes are needed; callers can use the existing fake through the interface. |
| R6 complex resolver behavior | `NationalityReferenceResolver` remains untouched and continues to interpret the result. |
| Non-functional minimality | One existing source file changes, with no production or generated artifacts. |
| Frontend compatibility | No UI or gateway path is involved; frontend impact is NONE. |

## Data models and contracts

There are no data-model or external-contract changes. The existing internal repository contract is:

```csharp
Task<Nationality?> GetActiveByCodeAsync(string code, CancellationToken ct = default);
```

The implementation may name the token `cancellationToken` because parameter names are already varied across the repository interface implementations; the type, order, optional default, and return type are the compatibility requirements.

The method's logical operation is:

```text
normalizedCode = code.Trim()
found = first stored row where IsActive && Code equals normalizedCode, OrdinalIgnoreCase
return found == null ? null : Clone(found)
```

No new DTO, entity field, persistence mapping, or serialization behavior is introduced.

## Component and interface design

### `InMemoryNationalityRepository`

Add the method in the `INationalityRepository` implementation section, alongside `GetPagedAsync` and `ExistsByCodeAsync`.

```csharp
public Task<Nationality?> GetActiveByCodeAsync(
    string code, CancellationToken cancellationToken = default)
{
    var normalizedCode = code.Trim();
    var found = _store.FirstOrDefault(x =>
        x.IsActive &&
        string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

    return Task.FromResult(found is null ? null : Clone(found));
}
```

The method intentionally does not mutate `_store`, sort the result, or add a second normalization policy. It follows the fake's existing convention of not separately polling cancellation tokens for synchronous list operations. `code.Trim()` also preserves the production behavior for the non-null interface input.

### Consumers

No consumer changes are designed. `NationalityReferenceResolver` already awaits `GetActiveByCodeAsync` and maps a non-null row to `NationalityReferenceProjection`. A null result continues to produce `CustomerNationalityReferenceNotFoundException`.

### Frontend

**Frontend impact: NONE.** The issue is inside backend test infrastructure. No file under `E:\Finnova\Finnova-UI\Finnova-UI` is touched, and no gateway alias or UI service URL changes.

## Error handling

- A matching active row returns a completed task with a clone.
- No matching active row returns a completed task containing `null`; the repository does not throw a domain exception.
- A null `code` would violate the non-null interface contract. Calling `Trim()` preserves the production implementation's null-input behavior rather than introducing a different fallback.
- Cancellation is accepted as required by the interface. The in-memory operation has no I/O or await point, consistent with the other methods in this fake, so it does not add a new cancellation exception path.
- No exception middleware, HTTP status, error code, or logging behavior changes.

## Security

The method reads only test data already held by the in-memory fake. It introduces no authentication, authorization, credential, logging, or input-boundary change. The method does not expose the backing list because it returns a clone.

## Testing and verification strategy

No additional test source is required for this minimal compiler fix. Existing code establishes the contract through the interface and resolver usage, while existing in-memory property tests compile against `InMemoryNationalityRepository` once the missing member is supplied.

After the user copies the task, the narrowest verification is the test project build, because the changed file belongs to `Finnova.Tests` and the build compiles its project references:

```powershell
dotnet build Finnova.Tests\Finnova.Tests.csproj
```

A full test run is optional follow-up validation, not part of the minimal compiler proof:

```powershell
dotnet test Finnova.Tests\Finnova.Tests.csproj
```

This document-authoring pass runs neither command.

## Design decisions and tradeoffs

### Use `Task.FromResult` rather than making the method `async`

The in-memory store is synchronous and all neighboring methods use `Task.FromResult`. This avoids an unnecessary state machine while satisfying the asynchronous interface. The tradeoff is that the token is not polled, which is already the fake's established convention.

### Use explicit `OrdinalIgnoreCase`

The production query relies on SQL Server's case-insensitive collation, while in-memory C# comparisons are otherwise ordinal and case-sensitive. Explicit `OrdinalIgnoreCase` keeps tests aligned with production and matches `ExistsByCodeAsync` in the same class. The tradeoff is that this models the current database collation assumption, which is already documented by the repository code.

### Return a clone

The class's read methods clone entities to model `AsNoTracking`. Returning `found` directly would create an inconsistent mutation leak. Cloning adds one small allocation but preserves the established test-double contract.

### Do not add a new test or refactor the fake

The requested correction is one missing interface member. Adding a separate test or refactoring neighboring methods would widen the change without being needed to prove interface compilation. The existing consumers and repository implementation already define the expected behavior.

## Implementation-ordered task list

1. Modify `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs` by inserting the exact `GetActiveByCodeAsync` implementation in the `INationalityRepository` section after `ExistsByCodeAsync` and before the class closing brace.
2. After applying the source task, run `dotnet build Finnova.Tests\Finnova.Tests.csproj` from `e:\Finnova\Finnova-API` to verify the interface implementation compiles. Do not change generated artifacts.
