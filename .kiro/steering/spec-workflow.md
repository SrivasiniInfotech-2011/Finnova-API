---
inclusion: always
---

# Spec Delivery Workflow (Documents Only)

For any new feature/ticket (e.g. a FINNOVA Jira story), the deliverable is a **spec of three
documents only**. Do NOT implement the code, run builds, or run tests. The user copy-pastes the
code from the tasks file and drives execution. This is a deliberate cost-saving convention agreed
with the user.

## What to produce

Create these three files under `.kiro/specs/<feature-slug>/`:

1. **`requirements.md`** — EARS-format requirements (Ubiquitous / Event-driven / State-driven /
   Unwanted-event / Optional-feature / Complex). Include an Introduction, Glossary, and
   Dependencies/Assumptions section. Record silent decisions explicitly as **Assumptions** for
   confirmation. Mirror the structure of existing specs (nationality, organization-hierarchy).

2. **`design.md`** — architecture and design: overview, what-is-reused vs what-is-new, a
   requirements-coverage map, data models, contracts, component/interface descriptions, error
   handling, security, and design decisions/tradeoffs. End the design with an
   **implementation-ordered task list** (the sequence in which the tasks file should be applied).
   The design explains the "why" and shape; it does not need to inline every file's full code.

3. **`tasks.md`** — the actual code to **copy and paste**, one task per step, in implementation
   order, each marked **CREATE `<path>`** or **MODIFY `<path>`** with the complete code block (for
   MODIFY, show the exact snippet and where it goes). This is the file the user executes from.

Also write the spec config `.config.kiro` with a fresh `specId` GUID and
`{"workflowType": "requirements-first", "specType": "feature"}`.

## Scope

Every spec covers **both** sides of the platform:

- **Backend** — `e:\Finnova\Finnova-API` (.NET solution `Finnova.Backend.slnx`).
- **Frontend** — `E:\Finnova\Finnova-UI\Finnova-UI` (React + TypeScript + Vite).

Unless the user explicitly says a ticket is API-only or UI-only, produce tasks for both.

## Implementation order (default for a SystemAdmin master-data feature)

The tasks file should sequence work in dependency order:

1. Backend domain: entities + enums
2. Backend domain: exceptions
3. Backend contracts (request/response records)
4. Backend EF configuration + `FinnovaDbContext` DbSets + migration command
5. Backend repositories + interfaces + DI registration
6. Backend pure/domain helpers (if any)
7. Backend service CQRS slice (commands, queries, validators, mappers)
8. Backend controller + `ExceptionHandlingMiddleware` branch + API gateway alias
9. Backend tests (unit / property / integration)
10. Frontend model + service interface + real + mock + toggle + barrels
11. Frontend page + components + route (`App.tsx`) + nav (`Layout.tsx`)
12. Frontend Vitest tests

Adjust the order to fit the feature, but keep dependencies before dependents.

## Boundaries

- Do NOT create or edit source files in `Finnova-API` or `Finnova-UI` for the feature.
- Do NOT run `dotnet build`/`dotnet test`/`npm run build`/`npm test`.
- The three spec documents are the whole deliverable. Start only when the user provides a ticket.
- Follow all existing steering (e.g. product-context: India-only, English-only, no RTL) inside the
  documents you produce.
