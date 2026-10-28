# Implementation Tasks — Resolve In-Memory Nationality Repository Contract

**Scope:** Minimal backend test-infrastructure compiler correction. Apply the source task from `e:\Finnova\Finnova-API`; do not change production repositories, generated artifacts, or the frontend.

## Task 1 — MODIFY `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs`

In the `// ---- INationalityRepository ----` section, insert the following method immediately after the existing `ExistsByCodeAsync` method and before the final closing brace of `InMemoryNationalityRepository`.

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

This exact implementation satisfies `INationalityRepository.GetActiveByCodeAsync` with return type `Task<Nationality?>`. It trims the requested code, matches only active rows using a case-insensitive comparison, returns a clone so callers cannot mutate stored test data, and returns `null` when no active match exists. It follows the existing in-memory repository convention of wrapping synchronous operations with `Task.FromResult` and accepting the cancellation token without separately polling it.

Do not modify `INationalityRepository`, `NationalityRepository`, `NationalityReferenceResolver`, tests, generated artifacts, API hosts, gateway configuration, or any frontend file.

## Task 2 — TARGETED VERIFICATION

After applying Task 1, run this command from `e:\Finnova\Finnova-API`:

```powershell
dotnet build Finnova.Tests\Finnova.Tests.csproj
```

This is the narrowest build that compiles the changed test infrastructure and its project references. Record the command's result. Do not run migration, frontend, or unrelated commands for this ticket.

## Expected result

- `InMemoryNationalityRepository` now implements `INationalityRepository`.
- The compiler error for `GetActiveByCodeAsync(string, CancellationToken)` is absent.
- Active, trimmed, case-insensitive code lookups return cloned `Nationality` values.
- Inactive or missing matches return `null`.
- No production or generated artifact changes are made.
