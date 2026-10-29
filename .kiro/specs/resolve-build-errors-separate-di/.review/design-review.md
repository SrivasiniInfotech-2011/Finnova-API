# Design Review — Resolve Build Errors and Separate Repository/Service DI

## Verdict basis

No HIGH or MEDIUM findings were identified. The specification is **APPROVED**.

## Findings

No findings. The numbered requirements use the requested EARS categories: Ubiquitous (Requirements 1–2), Event-driven (Requirement 3), Unwanted-event (Requirement 4), State-driven (Requirement 5), Complex (Requirement 6), and Optional-feature (Requirement 7). The requirements include the requested Introduction, Glossary, Dependencies/Assumptions, acceptance criteria, non-functional requirements, and out-of-scope boundary. The design covers the architecture, reused and new assets, requirements mapping, interfaces, errors, security, frontend compatibility, tradeoffs, and an implementation-ordered task list. The tasks contain separate repository and service registration entry points, all three host call sites, the nullable configuration fix, the service-project package change, the repository guard, and the descriptor test.

## Verified Assumptions

- All four requested specification files exist in the target directory. The configuration contains a syntactically valid GUID and exactly the requested workflow and spec type fields alongside `specId`. A workspace search found no duplicate of the proposed GUID.
- The actual repository boundary is `Finnova.Repository/DependencyInjection.cs`. It currently owns `FinnovaDbContext`, SQL Server setup, repository registrations, and repository-local adapters. It has no `Finnova.Service` reference. The proposed `ArgumentException.ThrowIfNullOrWhiteSpace` guard is compatible with its `net10.0` target and does not change the registration signature.
- The actual service project already references `Finnova.Repository`, and the three API projects already reference both `Finnova.Service` and `Finnova.Repository`. The service project currently lacks `FluentValidation.DependencyInjectionExtensions`, while all three hosts currently use `AddValidatorsFromAssembly` with version `11.11.0`. The package addition in Task 2 is therefore grounded in the current project files.
- The proposed service registration types exist in the source at the namespaces used by Task 1: the Login command marker, JWT settings and token service, password policy, asset generator, validation behavior, customer applicability evaluator, seven GL development stubs, lookup validation adapter, document usage stub, and blob-storage extension.
- The current host programs contain the duplicated registration blocks described by the design. SystemAdmin currently scans MediatR twice and directly registers the customer evaluator and document ports; Accounts owns the GL stubs and validation pipeline; UA owns the token, asset, blob, and password registrations. The task replacements remove those host-owned duplicates while retaining host authentication, authorization, middleware, controller, OpenAPI, and health setup.
- The current repository registrations include both `INationalityRepository`/`NationalityRepository` and `IUserManagementRepository`/`UserManagementRepository`, so the proposed descriptor assertions refer to real existing types. The test only inspects descriptors and does not resolve the EF context or contact SQL Server.
- The gateway configuration has separate UA, Accounts, and SystemAdmin clusters and existing aliases; the design makes no route or contract change. The design explicitly states frontend impact is NONE and records that compatibility consequence.
- The tracked evidence cited by the specification is historical evidence, not a claim that this review or the document-authoring pass ran a build, test, or migration. The specification explicitly identifies its build diagnostic basis as an existing nullable `CS8604` at the SystemAdmin connection-string call and gives a concrete fix for it in Tasks 3 and 6. No build, test, migration, frontend, or application-source command was run for this review.

## Unverified/Wrong Assumptions

None identified. The remaining choices, including shared registration of lazy dependencies in every host, the service assembly scan marker, blob-storage ownership, fail-fast connection configuration, and no API/UI contract change, are explicitly labeled as assumptions for confirmation in `requirements.md` and `design.md` rather than presented as confirmed facts. The task file gives exact current snippets and locations for the MODIFY operations and places build/test commands only in the later user-run section.
