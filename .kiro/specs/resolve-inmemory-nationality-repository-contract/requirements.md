# Requirements Document — Resolve In-Memory Nationality Repository Contract

## Introduction

This specification resolves the compiler error reported for `Finnova.Tests.Infrastructure.InMemoryNationalityRepository`: the class implements `INationalityRepository` but does not provide `GetActiveByCodeAsync(string, CancellationToken)`. The change is limited to the test infrastructure implementation in the backend repository. It adds the missing method with the same return type and lookup semantics as the EF-backed `NationalityRepository`.

The repository interface requires `Task<Nationality?>`. The production implementation trims the supplied code, matches the active row, and returns `null` when no active row matches. The in-memory implementation SHALL reproduce those semantics using the existing list-backed store and SHALL clone a returned entity, matching the existing no-tracking behavior of its other read methods. No production API, database schema, generated artifact, frontend file, or unrelated test behavior changes.

The frontend repository at `E:\Finnova\Finnova-UI\Finnova-UI` is in scope for impact assessment only. This compiler correction has no frontend impact.

## Glossary

- **Active nationality**: A `Nationality` whose `IsActive` property is `true`.
- **Active-code lookup**: `INationalityRepository.GetActiveByCodeAsync`, which finds one active nationality by code.
- **In-memory repository**: `Finnova.Tests.Infrastructure.InMemoryNationalityRepository`, a test double backed by a `List<Nationality>`.
- **Production repository**: `Finnova.Repository.Repositories.NationalityRepository`, the EF Core implementation used as the behavior reference.
- **Clone**: A new `Nationality` instance containing the stored entity's values, preventing callers from mutating the in-memory store through a returned object.
- **Case-insensitive match**: Code comparison using `StringComparison.OrdinalIgnoreCase`, matching the SQL Server case-insensitive behavior relied on by the production repository.

## Dependencies and Assumptions

### Dependencies

- `Finnova.Repository.Interfaces.INationalityRepository` remains the source-of-truth interface contract.
- `Finnova.Models.Domain.Entities.Nationality` remains the entity returned by both repository implementations.
- `Finnova.Tests.Infrastructure.InMemoryNationalityRepository.Clone` remains the established read-copy helper.
- The existing `NationalityRepository.GetActiveByCodeAsync` remains the production behavior reference.
- `NationalityReferenceResolver.ResolveActiveAsync` continues to use the repository method and throws its existing not-found exception when the method returns `null`.
- The existing .NET project and test-project references remain unchanged.

### Assumptions for confirmation

- **A1 — Scope:** This is a compiler-correction ticket, not a new nationality feature. Only the missing method is added to the existing in-memory test double.
- **A2 — Return contract:** The exact required return type is `Task<Nationality?>`, as declared by `INationalityRepository` and consumed by `NationalityReferenceResolver`.
- **A3 — Code normalization:** The lookup trims the input code before comparing it, as the production repository does. Stored codes are not rewritten.
- **A4 — Case behavior:** The lookup is case-insensitive, matching the production repository's SQL Server collation semantics and the in-memory repository's existing explicit case-insensitive uniqueness behavior.
- **A5 — Read isolation:** A matching result is cloned before it is returned, consistent with `GetByIdAsync`, `GetAllAsync`, and paged reads in the same class.
- **A6 — Cancellation:** The in-memory repository follows its existing convention of accepting but not separately polling the cancellation token for synchronous list operations. The token remains part of the exact interface signature.
- **A7 — Platform scope:** No UI, gateway, API contract, localization, seed data, or non-English value is introduced. Finnova remains India-specific and English-only.

## Functional Requirements

### Requirement 1: Implement the exact interface member (Ubiquitous)

THE `InMemoryNationalityRepository` SHALL implement `GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default)` with return type `Task<Nationality?>`.

THE implementation SHALL compile as an `INationalityRepository` without changing the interface or production repository.

### Requirement 2: Resolve an active code (Event-driven)

WHEN `GetActiveByCodeAsync` is called with a code that matches a stored active nationality after trimming and case-insensitive comparison, THE method SHALL return a completed task containing a clone of that nationality.

THE method SHALL preserve all values copied by the existing `Clone` helper, including `Id`, `Code`, `Name`, `IsActive`, `CreatedAt`, and `UpdatedAt`.

### Requirement 3: Respect active-only lookup state (State-driven)

WHILE the store contains both active and inactive nationalities with the same code, THE method SHALL return the matching active nationality and SHALL NOT return an inactive nationality.

WHILE the store contains no active nationality matching the normalized code, THE method SHALL return `null`.

### Requirement 4: Avoid unwanted store mutation (Unwanted-event)

IF a caller changes the `Nationality` returned by a successful lookup, THEN the stored entity SHALL remain unchanged.

IF the lookup finds no match, THEN the method SHALL return `null` rather than throwing a not-found exception; the existing resolver remains responsible for translating `null` into its domain exception.

### Requirement 5: Preserve optional test-double usage (Optional-feature)

WHERE a unit or property test supplies `InMemoryNationalityRepository` to a service or resolver through `INationalityRepository`, THE active-code lookup SHALL be available without requiring EF Core, SQL Server, a new adapter, or a changed test registration.

### Requirement 6: Preserve repository and resolver behavior together (Complex)

WHEN `NationalityReferenceResolver.ResolveActiveAsync` receives a code and uses `InMemoryNationalityRepository`, THE repository SHALL apply active-code matching first and the resolver SHALL continue to return the active nationality projection for a match or throw the existing `CustomerNationalityReferenceNotFoundException` for a missing active match.

No change SHALL be made to resolver exception mapping, HTTP contracts, or production persistence behavior.

## Non-Functional Requirements

- **Compatibility:** The implementation SHALL use the existing C#/.NET style and helper methods in the test repository.
- **Minimality:** Only `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs` is modified for the fix. No generated artifact is changed.
- **Isolation:** Returned entities SHALL be clones; the internal list SHALL not be exposed through the new method.
- **Consistency:** Matching semantics SHALL agree with `NationalityRepository.GetActiveByCodeAsync`: trim input, require `IsActive`, and compare code case-insensitively.
- **Cancellation contract:** The method SHALL accept the interface's `CancellationToken` parameter even though the synchronous in-memory operation follows existing repository-double behavior and does not perform asynchronous I/O.
- **Localization:** No user-facing text, localization data, bilingual field, RTL handling, or non-English sample value is added.

## Acceptance Criteria

1. `InMemoryNationalityRepository` contains a method with the exact `Task<Nationality?> GetActiveByCodeAsync(string, CancellationToken)` contract.
2. A trimmed, case-insensitive code match returns an active nationality clone.
3. An inactive row is never returned by the active-code lookup.
4. A missing active match returns `null`.
5. Mutating a returned result does not mutate the repository's stored snapshot.
6. No file under `Finnova-API` other than the specified test infrastructure source is changed by the implementation task, and no generated artifact is changed.
7. The narrow verification command `dotnet build Finnova.Tests\Finnova.Tests.csproj` completes successfully after the user applies the task. This specification authoring pass does not run the build or tests.
8. Frontend impact is **NONE**. No file under `E:\Finnova\Finnova-UI\Finnova-UI` is changed.

## Out of Scope

- Changes to `INationalityRepository`, `NationalityRepository`, `NationalityReferenceResolver`, domain entities, controllers, middleware, migrations, or API contracts.
- New production behavior beyond matching the existing production repository semantics.
- New test infrastructure abstractions or a broad refactor of in-memory repositories.
- Frontend models, services, pages, routes, navigation, tests, localization, or RTL handling.
- Running `dotnet build`, `dotnet test`, `npm run build`, `npm test`, migrations, or deployment commands during spec authoring.
