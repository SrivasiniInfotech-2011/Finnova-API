# EF model key configuration for JVPosting integration startup

The change adds an EF Core configuration that declares `JvPostingInbox.RequestId` as the primary key. This addresses the model-validation failure that occurred while the existing `SystemAdminAppFactory` built its InMemory database, before the unauthenticated request could reach the controller. The edit is limited to the missing model metadata and does not change the endpoint, authentication policy, fixture behavior, or test assertions. The verification note records both the focused JVPosting test and the broader integration-test scope passing after the change.

Watch for: no blocking concerns identified. The recorded evidence supports the fix without weakening or disabling the test.

**Verdict**: APPROVED

## High-level view

The integration fixture includes `JvPostingInbox` in the EF model through `FinnovaDbContext`, but convention cannot infer its key because the property is named `RequestId` rather than `Id`. The new `IEntityTypeConfiguration<JvPostingInbox>` is discovered by the repository's existing `ApplyConfigurationsFromAssembly` call and supplies exactly that missing metadata.

The fix leaves the authentication test and host wiring intact. The verification record shows the test failing with the key-validation exception before the fix, then passing afterward, with all 248 tests in the affected integration namespace also passing.

<details>
<summary>Issues (0)</summary>

</details>

<details>
<summary>Details</summary>

## EF model validation at test-host startup

The recorded pre-fix failure identifies the changed model boundary: `FinnovaDbContext` exposes `JvPostingInbox`, while the entity's key property is `RequestId`. The added configuration declares that property as the key, and the existing assembly-wide configuration discovery loads it without additional host or fixture wiring. This is the smallest edit that makes the existing InMemory and SQL-backed EF models valid; changing the test, bypassing database initialization, or changing authentication would have hidden the failure rather than fixing its cause.

## Verification and test integrity

The verification note records a focused run of `JvPostingApiTests` passing with one test, after reproducing the exact model-validation failure before the fix. It also records the broader `Finnova.Tests.Integration` filter passing all 248 discovered tests. The test still creates the normal application factory, sends an unauthenticated POST to `/api/jv-posting/requests`, and asserts `401 Unauthorized`, so the fix does not weaken the behavior under test.

## Scope and preservation of surrounding changes

The fix commit changes only `Finnova.Repository/Configuration/JvPostingInboxConfiguration.cs` plus the verification record. It does not modify the JVPosting controller, service, authorization policies, gateway routes, `FinnovaDbContext`, or the integration fixture. The separate working-tree changes for the JVPosting feature and other work remain outside this fix, preserving those user changes.

</details>

<details>
<summary>Files changed</summary>

- `Finnova.Repository/Configuration/JvPostingInboxConfiguration.cs` — declares `RequestId` as the `JvPostingInbox` primary key.
- `.agents/tasks/fix-integration-tests/verification.md` — records the pre-fix reproduction, root cause, and post-fix test results.

Full diff: `git show HEAD`.

</details>
