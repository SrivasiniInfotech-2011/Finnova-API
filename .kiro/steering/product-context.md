---
inclusion: always
---

# Product Context

## Market and Localization

Finnova is a financial platform built exclusively for the **India** market. All
components, modules, and features are India-specific.

Apply these rules to all requirements, designs, and implementation:

- **English only.** Display values, lookup values, labels, and user-facing text are in
  English. Do NOT add bilingual fields (e.g., separate English/Arabic columns), secondary
  language variants, or translation scaffolding.
- **No right-to-left (RTL) support.** Do NOT add RTL text direction handling, `dir="rtl"`
  rendering, or bidirectional layout concerns.
- **No Arabic / non-English localization.** Do NOT introduce Arabic (or other non-English)
  seed data, sample values, contract fields, validators, or tests.

When a data model needs a single display value, use one plain field (e.g. `Value`) rather
than language-suffixed fields (e.g. `ValueEn`/`ValueAr`).

If a future requirement genuinely needs multi-language support, raise it explicitly for
confirmation rather than assuming it.


## Repository Layout

Finnova is split across two repositories:

- **Backend API** (this workspace): `e:\Finnova\Finnova-API` - the .NET solution
  (`Finnova.Backend.slnx`) containing all services, domain, repository, and API hosts.
- **Frontend UI**: `E:\Finnova\Finnova-UI\Finnova-UI` - the React + TypeScript (Vite) app
  where all user-facing UI is implemented.

UI work belongs in the Finnova-UI repository, not in the backend workspace. When a spec
includes frontend tasks, implement them in the Finnova-UI repo at the path above; keep the
backend repo API-only.


## Frontend Architecture (Finnova-UI)

The UI is **React 18 + TypeScript + Material UI (MUI)**, built with **Vite**. It does NOT use
Redux or RxJS. Follow the established patterns:

- **State**: React hooks (`useState`/`useEffect`/`useCallback`) for page/component state and
  React Context (`src/context`) for cross-cutting concerns (auth, theme). Do NOT introduce
  Redux, Redux Toolkit, redux-observable, or RxJS.
- **HTTP**: the shared axios instance in `src/services/api.ts`. It already injects the JWT
  Bearer token from `localStorage('finnova_token')` and centrally handles 401/403/409/timeout
  with toast notifications (reading `error.response.data.message`). Reuse it - do not create
  ad-hoc axios clients.
- **Service layer**: each feature has a service interface (`src/services/interfaces`), a mock
  implementation (`src/services/mock`), a real axios implementation (`src/services/real`), and
  a toggle module selecting between them via `VITE_USE_MOCK_API`. New features mirror this
  (see `location.service.ts` / `lookup.service.ts` as the reference pattern).
- **Tables/dialogs**: use MUI (`@mui/material`, `@mui/x-data-grid`). Mirror an existing master
  page (e.g. `LocationMaster.tsx`, `LookupMaster.tsx`) for new CRUD screens.

If a spec's design assumes a different frontend stack (e.g. Redux/RxJS), implement the
equivalent behavior in this repo's actual architecture rather than introducing the other stack.

## Testing Standards

Every feature ships with tests in the same change:

- **Backend (.NET)**: xUnit in the `Finnova.Tests` project. Use FsCheck.Xunit for
  property-based tests of pure service-layer logic (min 100 iterations), plain xUnit
  facts/theories for example/edge cases with a mocked or in-memory repository, and
  `WebApplicationFactory<Program>` integration tests for host wiring, authorization, and
  end-to-end flows. Run with `dotnet test`.
- **Frontend (Finnova-UI)**: **Vitest + React Testing Library** (with `jsdom`). Unit-test
  services (mock/real behavior), and component/page behavior (rendering, user interactions,
  success/error paths). Tests live next to the code or under a `__tests__`/`*.test.tsx`
  convention and run via `npm test`. Do NOT use RxJS marble tests or Redux store tests - they
  do not apply to this stack.

All tests must pass and the build must be green (`dotnet build` / `npm run build`) before a
feature is considered complete.

## API Gateway Routing (Finnova.ApiGateway)

The YARP gateway (`Finnova.ApiGateway`, :5000) fronts all backend hosts and is the only origin
the UI talks to. Routes/clusters live in `Finnova.ApiGateway/appsettings.json`:

- `/api/ua/**`       -> `ua-cluster` (Finnova.UAService, :5010)
- `/api/accounts/**` -> `accounts-cluster` (Finnova.AccountsService, :5020)
- `/api/systemadmin/**` -> `systemadmin-cluster` (Finnova.SystemAdminService, :5030)

CORS allows the UI origin (`AllowedOrigins`, default `http://localhost:3000`) with any header
and method, so the `Authorization` header passes through; YARP forwards it by default.

**When adding a new backend host/service:** add a cluster + route here AND confirm how the UI
composes URLs. The shared UI axios base URL currently includes a gateway prefix
(`.../api/ua/api`). If a feature's service lives on a different host, either point the UI service
at the correct gateway prefix or add a gateway alias route that forwards the UI-composed path to
the correct cluster (e.g. the Lookup feature: `/api/ua/api/lookup/**` is aliased to
`systemadmin-cluster` and rewritten to `/api/lookup/**`). Keep host separation intact; do not
collapse services into one host just to match a URL prefix.
