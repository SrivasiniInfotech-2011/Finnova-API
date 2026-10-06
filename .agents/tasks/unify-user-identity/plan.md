# Implementation Plan — unify-user-identity

> STATUS: Implemented and verified. The consolidation refactor already exists in the
> working tree of `d:\Projects\Finnova\Finnova-API` (branch
> `feature/FINNOVA-10-user-management`, on top of commit `bffcf97`). Build is green
> (0 warnings / 0 errors) and all 379 tests pass. The original worktree
> `.worktrees\unify-user-identity` was removed after the FINNOVA-10 UserAccount
> feature was committed, so the authoritative base is now the main workspace.
>
> This plan records WHAT was changed, WHY it is correct against the task brief, and
> HOW it was verified, plus the one deferred operational step. Each item below is
> already satisfied by code on disk; the "Verify" lines are the commands that were
> run (and can be re-run) to confirm the state.

## Context discovered during exploration

- Base now contains the committed `UserAccount` / `user_accounts` feature (commit
  `bffcf97`), which earlier existed only as uncommitted work — that was the original
  blocker, now resolved.
- Build command: `dotnet build Finnova.Backend.slnx` (net10.0, 8 projects).
- Test command: `dotnet test Finnova.Backend.slnx` (xUnit, Finnova.Tests, 379 tests).
- Auth previously authenticated `User` by Email; it now authenticates `UserAccount`
  by `UserName`.
- The UA host MediatR marker is `typeof(LoginCommand).Assembly` (in Finnova.Service),
  so removing the old Users CQRS slice does not break MediatR registration — the
  brief's concern about a `CreateUserCommand` marker does not apply to the current code.
- `DevJwt` (Finnova.Tests/Integration) mints role claims directly and is independent
  of the login credential, so the Email->UserName switch does not affect it.

## Plan items (all satisfied; verification = real build/test commands)

- [x] 1. Add `UserName` (login credential) and `Role` to the `UserAccount` entity and
      its EF configuration; keep `UserCode` as a separate unique field.
      Files: `Finnova.Models/Domain/Entities/UserAccount.cs`,
      `Finnova.Repository/Configuration/UserAccountConfiguration.cs`
      Details: `UserName` IsRequired, maxlen 30, UNIQUE index; `Role` stored as string
      (HasConversion) maxlen 50 using enum `Finnova.Models.Domain.Enums.UserRole`.
      Verify: `dotnet build Finnova.Backend.slnx` — succeeds.

- [x] 2. Seed data: backfill the 5 existing seed rows (USR001-USR005) with
      `UserName` usr001..usr005 and `Role` User; add the admin seed row (Id
      `00000000-0000-0000-0002-000000000000`, UserCode `ADMIN`, UserName `admin`,
      Role Admin, PBKDF2 hash of `Admin@123`, IsActive true) so admin/Admin@123 stays
      loginable after the users table is dropped.
      Files: `Finnova.Repository/Configuration/UserAccountConfiguration.cs`
      Verify: inspected migration seed values (item 7) + `dotnet test` login assertions.

- [x] 3. Repoint the token service to `UserAccount`: `GenerateToken(UserAccount)`,
      claims from `UserAccount`, Email claim omitted when null, Admin still also gets
      the extra `SystemAdmin` role claim.
      Files: `Finnova.Service/Auth/ITokenService.cs`,
      `Finnova.Service/Auth/TokenService.cs`
      Verify: `dotnet build` + authorization integration tests pass.

- [x] 4. Repoint the auth user mapper: `UserAccount.ToAuthUser()`, `Name = UserAccount.Name`,
      `Email = UserAccount.Email ?? ""`, same UserRole->UI-role mapping.
      Files: `Finnova.Service/Auth/AuthUserMapper.cs`
      Verify: `dotnet build` — succeeds.

- [x] 5. Switch login to UserName: `LoginCommand(UserName, Password)`, validator requires
      `UserName` (no email-format rule), handler looks up `UserAccount` by `UserName`,
      checks `IsActive`, verifies the password; `LoginRequest(UserName, Password)`;
      `AuthController` builds `new LoginCommand(request.UserName, ...)` and returns the
      401 message "Invalid username or password."
      Files: `Finnova.Service/Auth/Commands/Login/LoginCommand.cs`,
      `.../LoginCommandHandler.cs`, `.../LoginCommandValidator.cs`,
      `Finnova.Models/Contracts/Auth/LoginRequest.cs`,
      `Finnova.UAService/Controllers/AuthController.cs`
      Verify: `dotnet test` — login/auth tests pass.

- [x] 6. Repository + DI: add
      `Task<UserAccount?> GetByUserNameAsync(string, CancellationToken)` to
      `IUserManagementRepository` + `UserManagementRepository` and use it from the login
      handler; remove `IUserRepository`/`UserRepository` and their DI registration;
      remove `DbSet<User> Users` and the `Organization.Users` navigation + FK config
      from the context/entity (keep `UserAccounts` and `Organizations`).
      Files: `Finnova.Repository/Interfaces/IUserManagementRepository.cs`,
      `Finnova.Repository/Repositories/UserManagementRepository.cs`,
      deleted `Finnova.Repository/Interfaces/IUserRepository.cs`,
      deleted `Finnova.Repository/Repositories/UserRepository.cs`,
      `Finnova.Repository/DependencyInjection.cs`,
      `Finnova.Repository/Context/FinnovaDbContext.cs`,
      `Finnova.Models/Domain/Entities/Organization.cs`,
      `Finnova.Service/Mappers/OrganizationMapper.cs`
      Verify: `git grep -n "IUserRepository\|DbSet<User>"` over `*.cs` returns no live
      code (only frozen historical migration snapshots).

- [x] 7. Remove the old `User` entity + its EF config + the entire old Users CQRS slice
      and contracts and controller.
      Files (deleted): `Finnova.Models/Domain/Entities/User.cs`,
      `Finnova.Repository/Configuration/UserConfiguration.cs`,
      `Finnova.Service/Mappers/UserMapper.cs`,
      `Finnova.Service/Users/**` (CreateUser/UpdateUser/GetAllUsers/GetUserById),
      `Finnova.Models/Contracts/Users/**`,
      `Finnova.UAService/Controllers/UsersController.cs`
      Note: enum `UserRole` kept (still used); `UserStatus` left as-is if still
      referenced — do not delete enums still referenced elsewhere.
      Verify: `dotnet build` — succeeds with no missing-type errors.

- [x] 8. One EF migration `UnifyUserIdentity`: drops `users`; adds `UserName` + `Role`
      columns to `user_accounts`; creates the unique index on `UserName`; backfills the
      5 seed rows; inserts the admin seed row. Model snapshot regenerated accordingly.
      Files: `Finnova.Repository/Migrations/20261004161229_UnifyUserIdentity.cs`,
      `..._UnifyUserIdentity.Designer.cs`,
      `Finnova.Repository/Migrations/FinnovaDbContextModelSnapshot.cs`
      Verify: migration reviewed — `Up` drops `users`, adds the two columns + unique
      index, UpdateData for usr001..usr005, InsertData for admin; `Down` restores
      `users`. `dotnet build` includes the migration with no model-diff warnings.

- [x] 9. Tests align to UserName login and the removed `User` entity; InMemory UM
      repository supports `GetByUserNameAsync`.
      Files: `Finnova.Tests/Infrastructure/InMemoryUserManagementRepository.cs`
      (+ any Integration tests touching login/DevJwt).
      Verify: `dotnet test Finnova.Backend.slnx --no-build` —
      Passed: 379, Failed: 0, Skipped: 0.

## Full-suite verification (run against the current working tree)

- `dotnet build Finnova.Backend.slnx` -> Build succeeded, 0 Warning(s), 0 Error(s)
  (all 8 projects, incl. Finnova.Tests).
- `dotnet test Finnova.Backend.slnx --no-build` -> Passed: 379, Failed: 0, Skipped: 0.

## Deferred operational step (do NOT run as part of this task)

- Applying the migration to a real database (`dotnet ef database update`) is
  intentionally NOT performed per the task brief. On a fresh/existing DB this
  migration drops `users` and adds the `UserName`/`Role` columns + admin seed; run it
  only in the target environment when ready. After it is applied, log in as
  `admin` / `Admin@123`.

## Notes / assumptions

- Historical migration `*.Designer.cs` snapshots still mention
  `Finnova.Models.Domain.Entities.User`. These are immutable point-in-time snapshots
  and are intentionally left untouched; editing them would corrupt migration history.
  Only the current `FinnovaDbContextModelSnapshot.cs` reflects the post-refactor model
  (no `User`).
- There is an unrelated enum type `Finnova.Models/Domain/Enums/UserConfiguration.cs`
  (used by UserManagement list queries); it is distinct from the deleted EF
  `UserConfiguration` class and is correctly retained.
