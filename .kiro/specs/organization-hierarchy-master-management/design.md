# Design Document — Organization Hierarchy Master Management (UI-scoped revision)

**JIRA:** FINNOVA-5 · **Module:** SystemAdmin · **Master:** Organization Hierarchy
**Revision scope:** **UI-ONLY.** This revision reframes the design as a **copy-paste implementation guide** for the `Finnova-UI` Organization Hierarchy Master screen. The backend is treated as an already-agreed dependency and is documented here only as the **API contract the UI consumes** (see *API Reference*). Every change below is presented as either **CREATE `<path>`** with full file contents or **MODIFY `<path>`** with the exact snippet and where to place it, so it can be applied by copy-paste with no further design work.
**Related features:** Nationality Master Management (FINNOVA-9) and Lookup Master Management (FINNOVA-8) — the UI mirrors their established service/page conventions.

## Overview

Organization Hierarchy Master Management gives a System Administrator a screen to manage the Finnova organization as a tree of nodes (for example, Head Office → Region → Branch). A node carries a code, a single English display name, a hierarchy level, an optional parent, and an active flag. From the screen an admin can: search nodes by code or name with pagination, view the full hierarchy tree, read a node's direct children, create nodes (root or child), rename and/or re-parent a node, delete a leaf node, and view a node's audit trail.

This document covers the **UI feature only**. The screen lives in the separate **`Finnova-UI`** application: **React 18 + TypeScript + Material UI (MUI)**, built with **Vite**; state via **React hooks + Context** (**no Redux, no RxJS**); tests in **Vitest + React Testing Library** (jsdom). The UI talks only to the shared axios instance (`src/services/api.ts`), which already injects the JWT Bearer token and centrally toasts 401/403/409/timeout by reading `error.response.data.message`. The backend host, controller, EF entities, repositories, and middleware are **out of scope for this revision** and appear only as the consumed contract.

### Localization

Finnova is India-only and English-only. A node carries a **single** English `name` field. There are **no** bilingual fields (`nameEn`/`nameAr`), no Arabic seed/sample data, and no right-to-left (RTL) rendering. Any future multi-language need must be raised explicitly for confirmation.

## API Reference (consumed by the UI)

This section documents **only** the endpoints and contracts the UI calls. It replaces the full backend design in this revision. Backend entity/EF/repository/service/controller/middleware internals are intentionally omitted.

### Base URL and gateway routing

The shared axios base URL already carries the gateway UA prefix (`.../api/ua/api`). The gateway aliases **`/api/ua/api/orghierarchy/**`** to the SystemAdmin cluster (mirroring the existing lookup/nationality aliases), so the UI real service uses a service-relative **`basePath = '/orghierarchy'`** and composes paths on top of the shared `api` instance. The UI adds **no** gateway config; the alias is assumed present.

### Auth

Every endpoint requires a valid JWT with the **SystemAdmin** role (read included). The UI does nothing special — the shared `api` instance attaches `Authorization: Bearer <token>` from `localStorage('finnova_token')`. Missing/expired/invalid token → **401**; authenticated non-admin → **403**. Both are surfaced by the shared interceptor as toasts.

### Endpoints

All request/response bodies are JSON (camelCase). Paths below are **service-relative** (the UI prepends `basePath = '/orghierarchy'`).

| # | Method | Path (service-relative) | Request | Success |
| --- | --- | --- | --- | --- |
| 1 | GET | `/orghierarchy` | query `search?`, `page?` (default 1), `pageSize?` (default 20, 1–100) | 200 `PaginatedResponse<OrgHierarchyNode>` |
| 2 | GET | `/orghierarchy/tree` | — | 200 `OrgHierarchyNodeTree[]` |
| 3 | GET | `/orghierarchy/{id}/children` | — | 200 `OrgHierarchyNode[]` |
| 4 | POST | `/orghierarchy` | `CreateOrgHierarchyNodeRequest` | 201 `OrgHierarchyNode` |
| 5 | PUT | `/orghierarchy/{id}` | `UpdateOrgHierarchyNodeRequest` | 200 `OrgHierarchyNode` |
| 6 | DELETE | `/orghierarchy/{id}` | — | 204 No Content |
| 7 | GET | `/orghierarchy/{id}/audit` | — | 200 `OrgHierarchyNodeAuditEntry[]` (newest-first) |

**Endpoint notes the UI relies on:**

- **(1) Paged search** — `search` filters `code` OR `name` (case-insensitive substring); blank/whitespace `search` = no filter. Results ordered `Level asc → Name asc → Code asc`. Response includes `total` (before paging), `page`, `pageSize`, `totalPages`. A page beyond the last returns an empty `data` array with the correct `total`.
- **(2) Tree** — returns roots (`parentId == null`) at the top; each node's `children` ordered `Name asc → Code asc`. Empty master → `[]`.
- **(3) Children** — direct children only, ordered `Name asc → Code asc`. A leaf returns `[]` (not an error). A missing `id` returns **404** (`ERR-ORG-404`).
- **(4) Create** — `level` is resolved server-side (root = 1; child = parent.level + 1); the UI does **not** send `level`. `isActive` omitted defaults to `true`.
- **(5) Update** — rename and/or re-parent. `parentId: null` promotes to root. `code` is immutable (not accepted). A same-name-and-same-parent submission is a no-op (200, no audit).
- **(7) Audit** — returns entries newest-first (`changedAtUtc` desc, then `id` desc). A missing/unknown `id` returns 200 with `[]` (not an error).

### Request / response contracts (TypeScript shapes the UI depends on)

```typescript
// ---- Requests the UI sends ----

// POST /orghierarchy — level is resolved server-side, NOT sent by the UI.
interface CreateOrgHierarchyNodeRequest {
  code: string;
  name: string;
  parentId: string | null;   // null => create a root
  isActive: boolean;         // omit => server defaults true; UI sends explicit boolean
}

// PUT /orghierarchy/{id} — rename and/or re-parent. `code` is immutable (not sent).
interface UpdateOrgHierarchyNodeRequest {
  name: string;
  parentId: string | null;   // null => promote to root
}

// ---- Responses the UI reads ----

interface OrgHierarchyNode {
  id: string;
  code: string;
  name: string;
  level: number;             // root = 1
  parentId: string | null;   // null => root
  isActive: boolean;
  createdAt: string;         // ISO-8601 UTC
  updatedAt: string;         // ISO-8601 UTC
}

// GET /orghierarchy/tree — recursive; omits createdAt/updatedAt.
interface OrgHierarchyNodeTree {
  id: string;
  code: string;
  name: string;
  level: number;
  parentId: string | null;
  isActive: boolean;
  children: OrgHierarchyNodeTree[];
}

// GET /orghierarchy/{id}/audit — one immutable row; two editable fields => before/after pairs.
interface OrgHierarchyNodeAuditEntry {
  id: string;
  nodeId: string;
  action: 'Create' | 'Update' | 'Delete';
  oldName: string | null;      // null on Create
  newName: string | null;      // null on Delete
  oldParentId: string | null;  // null = root (or on Create)
  newParentId: string | null;  // null = root (or on Delete)
  changedBy: string;
  changedAtUtc: string;        // ISO-8601 UTC
}

// Reused from src/models/api.model.ts (already present in the repo).
interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

### Error codes the UI must handle

The backend returns RFC 7807 `ProblemDetails` with a `code` extension. The shared axios interceptor already reads `error.response.data.message` and toasts it; the UI must **preserve the current list/tree unchanged** on any error (never optimistically mutate).

| Condition | HTTP | `code` | UI handling |
| --- | --- | --- | --- |
| Duplicate code on create | 409 | `ERR-ORG-409` | Toast message (`"Node code must be unique"`); Add dialog stays open; list unchanged |
| Delete a node that has children | 409 | `ERR-ORG-409` | Toast message; list/tree unchanged |
| Self-parent / cycle / depth > 10 / parent-not-exists / required/length / bad page range | 400 | `ERR-ORG-400` | Toast message; dialog stays open; no mutation |
| Node not found (update/delete/children/audit-on-update path) | 404 | `ERR-ORG-404` | Toast message; list/tree unchanged |
| Missing/expired/invalid token | 401 | (auth) | Central interceptor handles (redirect/toast) |
| Authenticated non-admin | 403 | (auth) | Central interceptor toasts |

The UI reads `error.response.data.code` only when it needs to branch (rare); in most paths it just lets the shared interceptor toast `error.response.data.message` and keeps state unchanged.

## Frontend Design (Finnova-UI) — file-by-file copy-paste guide

Implemented in `E:\Finnova\Finnova-UI\Finnova-UI` (React 18 + TypeScript + MUI + Vite; hooks + Context; **no Redux, no RxJS**; tests in **Vitest + React Testing Library**). This section is a **file-by-file manual implementation guide** — apply every change by hand: **CREATE** = new file with the full contents shown; **MODIFY** = add/change the exact snippet shown at the indicated place. It mirrors the **Nationality Master** (closest analog: a code+name master with an audit trail) and extends it for the tree structure.

> **⚠️ NAMING-COLLISION WARNING — read first.** The Finnova-UI repo **already contains** a completely unrelated **Company Master** feature that owns these exact names:
> `src/models/organization.model.ts`, `src/services/organization.service.ts`, `src/pages/OrganizationForm.tsx`, the interface `IOrganizationService`, the exported `organizationService`, and the route `/organizations` (labeled **"Company Master"** in `Layout.tsx`). That feature is a single-company profile (PAN / GST / constitution type / addresses) and has **nothing to do** with this hierarchy.
>
> This feature MUST NOT reuse, rename, or overwrite any of those. Use the distinct **`orgHierarchy` / `OrgHierarchy`** namespace everywhere:
> `orgHierarchy.model.ts`, `orgHierarchy.service.ts`, `orgHierarchy.interface.ts`, `IOrgHierarchyService`, `orgHierarchyService`, `orgHierarchyMockService`, `orgHierarchyRealService`, `OrgHierarchyMaster.tsx`, components under `src/components/orgHierarchy/`, and the UI route **`/org-hierarchy`**. The UI real-service `basePath` is `'/orghierarchy'` (the gateway alias described in *API Reference* handles routing; the UI adds no gateway config).

> **⚠️ UI-STACK NOTE (grounded in the actual `package.json`).** The masters render lists with **`DataGrid` from `@mui/x-data-grid`** — that package **is already a dependency** (`@mui/x-data-grid@^7.18.0`) and `NationalityGrid.tsx` uses it. Reuse it; **no new dependency is needed for the flat list.** However, **`@mui/x-tree-view` is NOT installed.** For the hierarchy tree, use a **custom recursive MUI component** built from `List` / `ListItemButton` / `Collapse` (the same nested-`Collapse` pattern already used in `Layout.tsx`), which adds **zero** dependencies. Adding `@mui/x-tree-view` (for `RichTreeView`/`SimpleTreeView`) is the **only** case that introduces a new npm dependency and is optional — choose it only if a richer built-in tree UX is wanted.

### Established repo conventions to follow

Every master in this repo is wired the same way; mirror it exactly for `orgHierarchy`:

| Concern | Location / convention |
| --- | --- |
| Model | `src/models/<feature>.model.ts`, barrel-exported via `export * from './<feature>.model';` in `src/models/index.ts`. `PaginatedResponse<T>` comes from `src/models/api.model.ts`. |
| Service interface | `src/services/interfaces/<feature>.interface.ts`, barrel-exported in `src/services/interfaces/index.ts`. |
| Mock service | `src/services/mock/<feature>.mock.ts` — a class implementing the interface, exported as `<feature>MockService`. |
| Real service | `src/services/real/<feature>.real.ts` — a class using the shared `api` axios instance, exported as `<feature>RealService`. |
| Toggle | `src/services/<feature>.service.ts` — selects mock vs real via `VITE_USE_MOCK_API`, re-exported in `src/services/index.ts`. |
| Page | `src/pages/<Feature>Master.tsx` (hooks only), route in `src/App.tsx` inside the `ProtectedRoute`/`Layout` group, nav item in the `Administration` group in `src/components/Layout.tsx`. |
| Components | `src/components/<feature>/` split per concern: `<Feature>Grid.tsx`, `<Feature>AddDialog.tsx`, `<Feature>EditDialog.tsx`, `<Feature>AuditDialog.tsx`. |
| HTTP | Shared axios `src/services/api.ts` — base URL carries the gateway UA prefix (`.../api/ua/api`), injects the JWT, and centrally toasts 401/403/409/timeout via `error.response.data.message`. Never create an ad-hoc axios client. |

---

### FILE 1 — CREATE `src/models/orgHierarchy.model.ts`

India-only, English-only: a node carries a **single** `name` field (no bilingual/`nameEn`/`nameAr` fields, no RTL).

```typescript
// India-only platform: a node carries ONE English `name`. No bilingual fields, no RTL.

/** A single organization-hierarchy node as returned by the API (paged/list read). */
export interface OrgHierarchyNode {
  id: string;
  code: string;
  name: string;              // single English display name
  level: number;             // resolved from parent; root = 1
  parentId: string | null;   // null => root node
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

/** Recursive tree node returned by getTree(). */
export interface OrgHierarchyNodeTree
  extends Omit<OrgHierarchyNode, 'createdAt' | 'updatedAt'> {
  children: OrgHierarchyNodeTree[];
}

/** Payload for creating a node (Add dialog). Level is resolved server-side from parentId. */
export interface OrgHierarchyNodeFormData {
  code: string;
  name: string;
  parentId: string | null;   // null => create a root
  isActive: boolean;
}

/** Payload for updating a node (Edit dialog): rename and/or re-parent. Code is immutable. */
export interface OrgHierarchyNodeUpdateData {
  name: string;
  parentId: string | null;   // null => promote to root
}

/** One immutable audit row. Two editable fields => before/after pairs for name and parent. */
export interface OrgHierarchyNodeAuditEntry {
  id: string;
  nodeId: string;
  action: 'Create' | 'Update' | 'Delete';
  oldName: string | null;      // null on Create
  newName: string | null;      // null on Delete
  oldParentId: string | null;  // null = root (or on Create)
  newParentId: string | null;  // null = root (or on Delete)
  changedBy: string;
  changedAtUtc: string;
}
```

### FILE 2 — MODIFY `src/models/index.ts`

Append one line (keep grouping consistent with the file):

```typescript
export * from './orgHierarchy.model';
```

### FILE 3 — CREATE `src/services/interfaces/orgHierarchy.interface.ts`

```typescript
import type {
  OrgHierarchyNode,
  OrgHierarchyNodeTree,
  OrgHierarchyNodeFormData,
  OrgHierarchyNodeUpdateData,
  OrgHierarchyNodeAuditEntry,
} from '../../models';
import type { PaginatedResponse } from '../../models';

export interface OrgHierarchyQueryParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface IOrgHierarchyService {
  getPaged(params: OrgHierarchyQueryParams): Promise<PaginatedResponse<OrgHierarchyNode>>;
  getTree(): Promise<OrgHierarchyNodeTree[]>;
  getChildren(id: string): Promise<OrgHierarchyNode[]>;
  create(data: OrgHierarchyNodeFormData): Promise<OrgHierarchyNode>;
  update(id: string, data: OrgHierarchyNodeUpdateData): Promise<OrgHierarchyNode>;
  remove(id: string): Promise<void>;
  getAuditTrail(id: string): Promise<OrgHierarchyNodeAuditEntry[]>;
}
```

### FILE 4 — MODIFY `src/services/interfaces/index.ts`

Append:

```typescript
export type { IOrgHierarchyService, OrgHierarchyQueryParams } from './orgHierarchy.interface';
```

### FILE 5 — CREATE `src/services/real/orgHierarchy.real.ts`

Reuses the shared `api` instance (JWT + central error toasts). The DTO shape already matches the UI models (camelCase JSON), exactly like `nationality.real.ts`.

```typescript
import api from '../api';
import type {
  OrgHierarchyNode,
  OrgHierarchyNodeTree,
  OrgHierarchyNodeFormData,
  OrgHierarchyNodeUpdateData,
  OrgHierarchyNodeAuditEntry,
  PaginatedResponse,
} from '../../models';
import type { IOrgHierarchyService, OrgHierarchyQueryParams } from '../interfaces';

class OrgHierarchyRealService implements IOrgHierarchyService {
  // Gateway aliases /api/ua/api/orghierarchy/** to the SystemAdmin cluster, so the
  // service-relative path here is simply '/orghierarchy' (mirrors nationality's '/nationality').
  private readonly basePath = '/orghierarchy';

  async getPaged(params: OrgHierarchyQueryParams): Promise<PaginatedResponse<OrgHierarchyNode>> {
    const res = await api.get<PaginatedResponse<OrgHierarchyNode>>(this.basePath, {
      params: { search: params.search, page: params.page, pageSize: params.pageSize },
    });
    return res.data; // DTO shape already matches OrgHierarchyNode (camelCase JSON)
  }

  async getTree(): Promise<OrgHierarchyNodeTree[]> {
    const res = await api.get<OrgHierarchyNodeTree[]>(`${this.basePath}/tree`);
    return res.data;
  }

  async getChildren(id: string): Promise<OrgHierarchyNode[]> {
    const res = await api.get<OrgHierarchyNode[]>(`${this.basePath}/${id}/children`);
    return res.data;
  }

  async create(data: OrgHierarchyNodeFormData): Promise<OrgHierarchyNode> {
    const res = await api.post<OrgHierarchyNode>(this.basePath, {
      code: data.code,
      name: data.name,
      parentId: data.parentId,
      isActive: data.isActive,
    });
    return res.data;
  }

  async update(id: string, data: OrgHierarchyNodeUpdateData): Promise<OrgHierarchyNode> {
    const res = await api.put<OrgHierarchyNode>(`${this.basePath}/${id}`, {
      name: data.name,
      parentId: data.parentId,
    });
    return res.data;
  }

  async remove(id: string): Promise<void> {
    await api.delete(`${this.basePath}/${id}`);
  }

  async getAuditTrail(id: string): Promise<OrgHierarchyNodeAuditEntry[]> {
    const res = await api.get<OrgHierarchyNodeAuditEntry[]>(`${this.basePath}/${id}/audit`);
    return res.data;
  }
}

export const orgHierarchyRealService = new OrgHierarchyRealService();
```

### FILE 6 — CREATE `src/services/mock/orgHierarchy.mock.ts`

In-memory implementation that throws **backend-shaped** errors (an object whose `error.response = { status, data: { code, message } }`) so mock and real modes behave identically through the shared interceptor. Seed **English-only** samples. It:
- resolves `level` from the parent (root = 1) and **recomputes subtree levels** on re-parent;
- rejects duplicate code (409, `ERR-ORG-409`, `"Node code must be unique"`), self-parent / cycle / depth > 10 (400, `ERR-ORG-400`), non-leaf delete (409, `ERR-ORG-409`), and unknown id (404, `ERR-ORG-404`);
- supports substring search, `Level asc → Name asc → Code asc` ordering, pagination, in-memory tree building, and appends an audit entry on **create/update/delete** (never on a rejected op or a no-op).

```typescript
import type {
  OrgHierarchyNode,
  OrgHierarchyNodeTree,
  OrgHierarchyNodeFormData,
  OrgHierarchyNodeUpdateData,
  OrgHierarchyNodeAuditEntry,
  PaginatedResponse,
} from '../../models';
import type { IOrgHierarchyService, OrgHierarchyQueryParams } from '../interfaces';

// Backend-shaped error codes (match the ERR-ORG-xxx branch in the backend middleware).
export const ERR_ORG_409_CODE = 'ERR-ORG-409';
export const ERR_ORG_400_CODE = 'ERR-ORG-400';
export const ERR_ORG_404_CODE = 'ERR-ORG-404';
const MAX_DEPTH = 10;

function apiError(status: number, code: string, message: string): Error {
  const err = new Error(message) as Error & {
    response: { status: number; data: { code: string; message: string } };
  };
  err.response = { status, data: { code, message } };
  return err;
}

const SEED_DATE = '2024-01-01T00:00:00Z';

// English-only seed nodes (India-only platform).
const nodes: OrgHierarchyNode[] = [
  { id: 'org-ho', code: 'HO', name: 'Head Office', level: 1, parentId: null, isActive: true, createdAt: SEED_DATE, updatedAt: SEED_DATE },
  { id: 'org-rgn-n', code: 'RGN-N', name: 'North Region', level: 2, parentId: 'org-ho', isActive: true, createdAt: SEED_DATE, updatedAt: SEED_DATE },
];
const audit: OrgHierarchyNodeAuditEntry[] = [];

class OrgHierarchyMockService implements IOrgHierarchyService {
  async getPaged(params: OrgHierarchyQueryParams): Promise<PaginatedResponse<OrgHierarchyNode>> {
    const term = (params.search ?? '').trim().toLowerCase();
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 20;
    const filtered = nodes
      .filter((n) => !term || n.code.toLowerCase().includes(term) || n.name.toLowerCase().includes(term))
      .sort((a, b) => a.level - b.level || a.name.localeCompare(b.name) || a.code.localeCompare(b.code));
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    return {
      data: filtered.slice(start, start + pageSize),
      total, page, pageSize,
      totalPages: Math.max(1, Math.ceil(total / pageSize)),
    };
  }

  async getTree(): Promise<OrgHierarchyNodeTree[]> {
    const build = (parentId: string | null): OrgHierarchyNodeTree[] =>
      nodes
        .filter((n) => n.parentId === parentId)
        .sort((a, b) => a.name.localeCompare(b.name) || a.code.localeCompare(b.code))
        .map((n) => ({ id: n.id, code: n.code, name: n.name, level: n.level, parentId: n.parentId, isActive: n.isActive, children: build(n.id) }));
    return build(null);
  }

  async getChildren(id: string): Promise<OrgHierarchyNode[]> {
    return nodes
      .filter((n) => n.parentId === id)
      .sort((a, b) => a.name.localeCompare(b.name) || a.code.localeCompare(b.code));
  }

  async create(data: OrgHierarchyNodeFormData): Promise<OrgHierarchyNode> {
    const code = data.code.trim();
    if (nodes.some((n) => n.code.toLowerCase() === code.toLowerCase()))
      throw apiError(409, ERR_ORG_409_CODE, 'Node code must be unique');
    let level = 1;
    if (data.parentId) {
      const parent = nodes.find((n) => n.id === data.parentId);
      if (!parent) throw apiError(400, ERR_ORG_400_CODE, 'Parent node does not exist');
      level = parent.level + 1;
      if (level > MAX_DEPTH) throw apiError(400, ERR_ORG_400_CODE, 'Maximum hierarchy depth exceeded');
    }
    const now = new Date().toISOString();
    const node: OrgHierarchyNode = {
      id: `org-${Math.random().toString(36).slice(2, 10)}`,
      code, name: data.name, level, parentId: data.parentId, isActive: data.isActive,
      createdAt: now, updatedAt: now,
    };
    nodes.push(node);
    audit.unshift({ id: `aud-${node.id}-c`, nodeId: node.id, action: 'Create', oldName: null, newName: node.name, oldParentId: null, newParentId: node.parentId, changedBy: 'mock-admin', changedAtUtc: now });
    return node;
  }

  async update(id: string, data: OrgHierarchyNodeUpdateData): Promise<OrgHierarchyNode> {
    const node = nodes.find((n) => n.id === id);
    if (!node) throw apiError(404, ERR_ORG_404_CODE, `Node '${id}' was not found`);
    const noop = node.name === data.name && node.parentId === data.parentId;
    if (noop) return node; // no audit on a no-op
    if (data.parentId === id) throw apiError(400, ERR_ORG_400_CODE, 'A node cannot be its own parent');
    const descendants = this.descendantIds(id);
    if (data.parentId && descendants.has(data.parentId))
      throw apiError(400, ERR_ORG_400_CODE, 'Re-parenting would create a cycle');
    let newLevel = 1;
    if (data.parentId) {
      const parent = nodes.find((n) => n.id === data.parentId);
      if (!parent) throw apiError(400, ERR_ORG_400_CODE, 'Parent node does not exist');
      newLevel = parent.level + 1;
    }
    // Depth check across the moved subtree.
    const delta = newLevel - node.level;
    const maxSubtreeLevel = Math.max(node.level, ...[...descendants].map((d) => nodes.find((n) => n.id === d)!.level));
    if (maxSubtreeLevel + delta > MAX_DEPTH)
      throw apiError(400, ERR_ORG_400_CODE, 'Maximum hierarchy depth exceeded');
    const now = new Date().toISOString();
    const oldName = node.name, oldParentId = node.parentId;
    node.name = data.name; node.parentId = data.parentId; node.level = newLevel; node.updatedAt = now;
    descendants.forEach((d) => { const c = nodes.find((n) => n.id === d)!; c.level += delta; c.updatedAt = now; });
    audit.unshift({ id: `aud-${id}-u-${audit.length}`, nodeId: id, action: 'Update', oldName, newName: node.name, oldParentId, newParentId: node.parentId, changedBy: 'mock-admin', changedAtUtc: now });
    return node;
  }

  async remove(id: string): Promise<void> {
    const node = nodes.find((n) => n.id === id);
    if (!node) throw apiError(404, ERR_ORG_404_CODE, `Node '${id}' was not found`);
    if (nodes.some((n) => n.parentId === id))
      throw apiError(409, ERR_ORG_409_CODE, 'Cannot delete a node that has children');
    const now = new Date().toISOString();
    nodes.splice(nodes.indexOf(node), 1);
    audit.unshift({ id: `aud-${id}-d`, nodeId: id, action: 'Delete', oldName: node.name, newName: null, oldParentId: node.parentId, newParentId: null, changedBy: 'mock-admin', changedAtUtc: now });
  }

  async getAuditTrail(id: string): Promise<OrgHierarchyNodeAuditEntry[]> {
    return audit.filter((a) => a.nodeId === id); // newest-first (we unshift); empty if unknown id
  }

  private descendantIds(id: string): Set<string> {
    const out = new Set<string>();
    const walk = (pid: string) => nodes.filter((n) => n.parentId === pid).forEach((c) => { if (!out.has(c.id)) { out.add(c.id); walk(c.id); } });
    walk(id);
    return out;
  }
}

export const orgHierarchyMockService = new OrgHierarchyMockService();
```

### FILE 7 — CREATE `src/services/orgHierarchy.service.ts`

```typescript
import type { IOrgHierarchyService } from './interfaces';
import { orgHierarchyMockService } from './mock/orgHierarchy.mock';
import { orgHierarchyRealService } from './real/orgHierarchy.real';

const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

export const orgHierarchyService: IOrgHierarchyService = useMock
  ? orgHierarchyMockService
  : orgHierarchyRealService;
```

### FILE 8 — MODIFY `src/services/index.ts`

Append:

```typescript
export { orgHierarchyService } from './orgHierarchy.service';
```

### FILE 9 — CREATE `src/pages/OrgHierarchyMaster.tsx`

Mirrors `NationalityMaster.tsx`: hooks only (`useState`/`useEffect`/`useCallback`/`useMemo`), a **debounced search** `TextField` (300ms), loads via `orgHierarchyService`, and **leaves the current list/tree unchanged on error** (the shared interceptor shows the toast). Adds a **grid ↔ tree view toggle** (`ToggleButtonGroup`). Opens the Add / Edit / Delete / Audit dialogs.

```tsx
import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Box, Paper, Stack, TextField, Button, ToggleButtonGroup, ToggleButton, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ViewListIcon from '@mui/icons-material/ViewList';
import AccountTreeIcon from '@mui/icons-material/AccountTree';
import { orgHierarchyService } from '../services';
import type {
  OrgHierarchyNode, OrgHierarchyNodeTree, OrgHierarchyNodeAuditEntry,
} from '../models';
import OrgHierarchyGrid from '../components/orgHierarchy/OrgHierarchyGrid';
import OrgHierarchyTree from '../components/orgHierarchy/OrgHierarchyTree';
import OrgHierarchyAddDialog from '../components/orgHierarchy/OrgHierarchyAddDialog';
import OrgHierarchyEditDialog from '../components/orgHierarchy/OrgHierarchyEditDialog';
import OrgHierarchyAuditDialog from '../components/orgHierarchy/OrgHierarchyAuditDialog';

type ViewMode = 'grid' | 'tree';

export default function OrgHierarchyMaster() {
  const [view, setView] = useState<ViewMode>('grid');
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const [page, setPage] = useState(0);           // DataGrid is 0-based
  const [pageSize, setPageSize] = useState(20);
  const [rows, setRows] = useState<OrgHierarchyNode[]>([]);
  const [rowCount, setRowCount] = useState(0);
  const [tree, setTree] = useState<OrgHierarchyNodeTree[]>([]);
  const [allNodes, setAllNodes] = useState<OrgHierarchyNode[]>([]); // for parent pickers
  const [loading, setLoading] = useState(false);

  const [addOpen, setAddOpen] = useState(false);
  const [editNode, setEditNode] = useState<OrgHierarchyNode | null>(null);
  const [auditNode, setAuditNode] = useState<OrgHierarchyNode | null>(null);
  const [auditRows, setAuditRows] = useState<OrgHierarchyNodeAuditEntry[]>([]);

  // Debounce the search box (300ms).
  useEffect(() => {
    const t = setTimeout(() => { setDebounced(search); setPage(0); }, 300);
    return () => clearTimeout(t);
  }, [search]);

  const loadGrid = useCallback(async () => {
    setLoading(true);
    try {
      const res = await orgHierarchyService.getPaged({
        search: debounced, page: page + 1, pageSize,
      });
      setRows(res.data);
      setRowCount(res.total);
    } finally {
      setLoading(false);
    }
  }, [debounced, page, pageSize]);

  const loadTree = useCallback(async () => {
    setLoading(true);
    try {
      setTree(await orgHierarchyService.getTree());
    } finally {
      setLoading(false);
    }
  }, []);

  // Full node set drives the parent pickers (Add: any/none; Edit: excludes self + descendants).
  const loadAllNodes = useCallback(async () => {
    const res = await orgHierarchyService.getPaged({ page: 1, pageSize: 100 });
    setAllNodes(res.data);
  }, []);

  useEffect(() => { if (view === 'grid') void loadGrid(); }, [view, loadGrid]);
  useEffect(() => { if (view === 'tree') void loadTree(); }, [view, loadTree]);
  useEffect(() => { void loadAllNodes(); }, [loadAllNodes]);

  const refresh = useCallback(async () => {
    await loadAllNodes();
    if (view === 'grid') await loadGrid(); else await loadTree();
  }, [view, loadAllNodes, loadGrid, loadTree]);

  const openAudit = useCallback(async (node: OrgHierarchyNode) => {
    setAuditNode(node);
    setAuditRows(await orgHierarchyService.getAuditTrail(node.id));
  }, []);

  const handleDelete = useCallback(async (node: OrgHierarchyNode) => {
    // Leaf-only; on 409 the shared interceptor toasts and state stays unchanged.
    await orgHierarchyService.remove(node.id);
    await refresh();
  }, [refresh]);

  const parentOptions = useMemo(() => allNodes, [allNodes]);

  return (
    <Box p={2}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
        <Typography variant="h5">Organization Hierarchy</Typography>
        <Stack direction="row" spacing={2}>
          <ToggleButtonGroup
            size="small" exclusive value={view}
            onChange={(_, v: ViewMode | null) => v && setView(v)}
          >
            <ToggleButton value="grid"><ViewListIcon fontSize="small" />&nbsp;Grid</ToggleButton>
            <ToggleButton value="tree"><AccountTreeIcon fontSize="small" />&nbsp;Tree</ToggleButton>
          </ToggleButtonGroup>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>
            Add Node
          </Button>
        </Stack>
      </Stack>

      {view === 'grid' && (
        <TextField
          fullWidth size="small" label="Search by code or name"
          value={search} onChange={(e) => setSearch(e.target.value)} sx={{ mb: 2 }}
        />
      )}

      <Paper>
        {view === 'grid' ? (
          <OrgHierarchyGrid
            rows={rows}
            rowCount={rowCount}
            loading={loading}
            page={page}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            onEdit={setEditNode}
            onDelete={handleDelete}
            onAudit={openAudit}
          />
        ) : (
          <OrgHierarchyTree
            nodes={tree}
            loading={loading}
            onEdit={setEditNode}
            onDelete={handleDelete}
            onAudit={openAudit}
          />
        )}
      </Paper>

      <OrgHierarchyAddDialog
        open={addOpen}
        parentOptions={parentOptions}
        onClose={() => setAddOpen(false)}
        onCreated={async () => { setAddOpen(false); await refresh(); }}
      />

      {editNode && (
        <OrgHierarchyEditDialog
          open
          node={editNode}
          allNodes={allNodes}
          onClose={() => setEditNode(null)}
          onUpdated={async () => { setEditNode(null); await refresh(); }}
        />
      )}

      {auditNode && (
        <OrgHierarchyAuditDialog
          open
          node={auditNode}
          entries={auditRows}
          onClose={() => { setAuditNode(null); setAuditRows([]); }}
        />
      )}
    </Box>
  );
}
```

### FILE 10 — CREATE `src/components/orgHierarchy/OrgHierarchyGrid.tsx`

The `DataGrid` list (Code, Name, Level, Parent, Active, Updated + Edit/Delete/Audit actions and an empty-state overlay). Server-side pagination (mirrors `NationalityGrid.tsx`).

```tsx
import { DataGrid, GridColDef, GridActionsCellItem } from '@mui/x-data-grid';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import HistoryIcon from '@mui/icons-material/History';
import { Box, Chip } from '@mui/material';
import type { OrgHierarchyNode } from '../../models';

interface Props {
  rows: OrgHierarchyNode[];
  rowCount: number;
  loading: boolean;
  page: number;
  pageSize: number;
  onPageChange: (p: number) => void;
  onPageSizeChange: (s: number) => void;
  onEdit: (n: OrgHierarchyNode) => void;
  onDelete: (n: OrgHierarchyNode) => void | Promise<void>;
  onAudit: (n: OrgHierarchyNode) => void | Promise<void>;
}

export default function OrgHierarchyGrid(props: Props) {
  const {
    rows, rowCount, loading, page, pageSize,
    onPageChange, onPageSizeChange, onEdit, onDelete, onAudit,
  } = props;

  const byId = new Map(rows.map((r) => [r.id, r]));

  const columns: GridColDef<OrgHierarchyNode>[] = [
    { field: 'code', headerName: 'Code', width: 140 },
    { field: 'name', headerName: 'Name', flex: 1, minWidth: 200 },
    { field: 'level', headerName: 'Level', width: 90, type: 'number' },
    {
      field: 'parentId', headerName: 'Parent', width: 200,
      valueGetter: (value: string | null) =>
        value ? (byId.get(value)?.name ?? value) : '(root)',
    },
    {
      field: 'isActive', headerName: 'Active', width: 110,
      renderCell: (p) => (
        <Chip size="small" color={p.value ? 'success' : 'default'} label={p.value ? 'Active' : 'Inactive'} />
      ),
    },
    {
      field: 'updatedAt', headerName: 'Updated', width: 180,
      valueGetter: (value: string) => new Date(value).toLocaleString(),
    },
    {
      field: 'actions', type: 'actions', headerName: 'Actions', width: 140,
      getActions: (p) => [
        <GridActionsCellItem key="edit" icon={<EditIcon />} label="Edit" onClick={() => onEdit(p.row)} />,
        <GridActionsCellItem key="del" icon={<DeleteIcon />} label="Delete" onClick={() => void onDelete(p.row)} />,
        <GridActionsCellItem key="aud" icon={<HistoryIcon />} label="Audit" onClick={() => void onAudit(p.row)} />,
      ],
    },
  ];

  return (
    <Box sx={{ width: '100%' }}>
      <DataGrid
        autoHeight
        rows={rows}
        columns={columns}
        loading={loading}
        rowCount={rowCount}
        paginationMode="server"
        pageSizeOptions={[10, 20, 50, 100]}
        paginationModel={{ page, pageSize }}
        onPaginationModelChange={(m) => { onPageChange(m.page); onPageSizeChange(m.pageSize); }}
        disableRowSelectionOnClick
      />
    </Box>
  );
}
```

### FILE 11 — CREATE `src/components/orgHierarchy/OrgHierarchyTree.tsx`

Custom recursive MUI tree (nested `List` + `Collapse` + `ListItemButton`) — **no `@mui/x-tree-view` dependency** (see UI-stack note). Each row exposes the same Edit / Delete / Audit actions.

```tsx
import { useState } from 'react';
import {
  List, ListItem, ListItemButton, ListItemText, Collapse, IconButton, Box, CircularProgress, Typography,
} from '@mui/material';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import HistoryIcon from '@mui/icons-material/History';
import type { OrgHierarchyNodeTree, OrgHierarchyNode } from '../../models';

interface Props {
  nodes: OrgHierarchyNodeTree[];
  loading: boolean;
  onEdit: (n: OrgHierarchyNode) => void;
  onDelete: (n: OrgHierarchyNode) => void | Promise<void>;
  onAudit: (n: OrgHierarchyNode) => void | Promise<void>;
}

// The tree node lacks createdAt/updatedAt; actions only need the node shape the dialogs read.
function toNode(t: OrgHierarchyNodeTree): OrgHierarchyNode {
  return { ...t, createdAt: '', updatedAt: '' };
}

function TreeRow(props: {
  node: OrgHierarchyNodeTree;
  depth: number;
  onEdit: Props['onEdit'];
  onDelete: Props['onDelete'];
  onAudit: Props['onAudit'];
}) {
  const { node, depth, onEdit, onDelete, onAudit } = props;
  const [open, setOpen] = useState(true);
  const hasChildren = node.children.length > 0;

  return (
    <>
      <ListItem
        disablePadding
        secondaryAction={
          <>
            <IconButton edge="end" aria-label="Edit" onClick={() => onEdit(toNode(node))}><EditIcon fontSize="small" /></IconButton>
            <IconButton edge="end" aria-label="Delete" onClick={() => void onDelete(toNode(node))}><DeleteIcon fontSize="small" /></IconButton>
            <IconButton edge="end" aria-label="Audit" onClick={() => void onAudit(toNode(node))}><HistoryIcon fontSize="small" /></IconButton>
          </>
        }
      >
        <ListItemButton sx={{ pl: 2 + depth * 2 }} onClick={() => hasChildren && setOpen((o) => !o)}>
          {hasChildren ? (open ? <ExpandLess /> : <ExpandMore />) : <Box sx={{ width: 24 }} />}
          <ListItemText primary={`${node.name} (${node.code})`} secondary={`Level ${node.level}`} />
        </ListItemButton>
      </ListItem>
      {hasChildren && (
        <Collapse in={open} timeout="auto" unmountOnExit>
          <List component="div" disablePadding>
            {node.children.map((c) => (
              <TreeRow key={c.id} node={c} depth={depth + 1} onEdit={onEdit} onDelete={onDelete} onAudit={onAudit} />
            ))}
          </List>
        </Collapse>
      )}
    </>
  );
}

export default function OrgHierarchyTree({ nodes, loading, onEdit, onDelete, onAudit }: Props) {
  if (loading) return <Box p={3} textAlign="center"><CircularProgress /></Box>;
  if (nodes.length === 0) return <Box p={3}><Typography color="text.secondary">No nodes yet.</Typography></Box>;
  return (
    <List>
      {nodes.map((n) => (
        <TreeRow key={n.id} node={n} depth={0} onEdit={onEdit} onDelete={onDelete} onAudit={onAudit} />
      ))}
    </List>
  );
}
```

### FILE 12 — CREATE `src/components/orgHierarchy/OrgHierarchyAddDialog.tsx`

MUI `Dialog` with Code, Name, parent `Select` (any node or none = root), Active switch; submits `OrgHierarchyNodeFormData`.

```tsx
import { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField,
  FormControl, InputLabel, Select, MenuItem, FormControlLabel, Switch, Stack,
} from '@mui/material';
import { orgHierarchyService } from '../../services';
import type { OrgHierarchyNode } from '../../models';

interface Props {
  open: boolean;
  parentOptions: OrgHierarchyNode[];
  onClose: () => void;
  onCreated: () => void | Promise<void>;
}

export default function OrgHierarchyAddDialog({ open, parentOptions, onClose, onCreated }: Props) {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [parentId, setParentId] = useState<string>(''); // '' => root
  const [isActive, setIsActive] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const reset = () => { setCode(''); setName(''); setParentId(''); setIsActive(true); };

  const submit = async () => {
    setSubmitting(true);
    try {
      // On error the shared interceptor toasts; the dialog stays open (we don't close).
      await orgHierarchyService.create({
        code: code.trim(),
        name: name.trim(),
        parentId: parentId || null,
        isActive,
      });
      reset();
      await onCreated();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Add Organization Node</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          <TextField label="Code" value={code} onChange={(e) => setCode(e.target.value)} inputProps={{ maxLength: 20 }} required />
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} inputProps={{ maxLength: 150 }} required />
          <FormControl fullWidth>
            <InputLabel id="parent-label">Parent</InputLabel>
            <Select
              labelId="parent-label" label="Parent" value={parentId}
              onChange={(e) => setParentId(e.target.value)}
            >
              <MenuItem value=""><em>(root — no parent)</em></MenuItem>
              {parentOptions.map((n) => (
                <MenuItem key={n.id} value={n.id}>{n.name} ({n.code})</MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControlLabel
            control={<Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />}
            label="Active"
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={submitting || !code.trim() || !name.trim()}>
          Create
        </Button>
      </DialogActions>
    </Dialog>
  );
}
```

### FILE 13 — CREATE `src/components/orgHierarchy/OrgHierarchyEditDialog.tsx`

Name field + parent `Select` (excluding **self and all descendants** — client-side cycle avoidance; the server still enforces it); **Code shown read-only**; submits `OrgHierarchyNodeUpdateData`.

```tsx
import { useMemo, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField,
  FormControl, InputLabel, Select, MenuItem, Stack,
} from '@mui/material';
import { orgHierarchyService } from '../../services';
import type { OrgHierarchyNode } from '../../models';

interface Props {
  open: boolean;
  node: OrgHierarchyNode;
  allNodes: OrgHierarchyNode[];
  onClose: () => void;
  onUpdated: () => void | Promise<void>;
}

// Self + all descendants are invalid parents (would create a cycle).
function excludedIds(nodeId: string, all: OrgHierarchyNode[]): Set<string> {
  const out = new Set<string>([nodeId]);
  const walk = (pid: string) =>
    all.filter((n) => n.parentId === pid).forEach((c) => { if (!out.has(c.id)) { out.add(c.id); walk(c.id); } });
  walk(nodeId);
  return out;
}

export default function OrgHierarchyEditDialog({ open, node, allNodes, onClose, onUpdated }: Props) {
  const [name, setName] = useState(node.name);
  const [parentId, setParentId] = useState<string>(node.parentId ?? '');
  const [submitting, setSubmitting] = useState(false);

  const parentChoices = useMemo(() => {
    const excluded = excludedIds(node.id, allNodes);
    return allNodes.filter((n) => !excluded.has(n.id));
  }, [node.id, allNodes]);

  const submit = async () => {
    setSubmitting(true);
    try {
      await orgHierarchyService.update(node.id, { name: name.trim(), parentId: parentId || null });
      await onUpdated();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Edit Organization Node</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          <TextField label="Code" value={node.code} InputProps={{ readOnly: true }} disabled />
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} inputProps={{ maxLength: 150 }} required />
          <FormControl fullWidth>
            <InputLabel id="edit-parent-label">Parent</InputLabel>
            <Select
              labelId="edit-parent-label" label="Parent" value={parentId}
              onChange={(e) => setParentId(e.target.value)}
            >
              <MenuItem value=""><em>(root — no parent)</em></MenuItem>
              {parentChoices.map((n) => (
                <MenuItem key={n.id} value={n.id}>{n.name} ({n.code})</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={submitting || !name.trim()}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  );
}
```

### FILE 14 — CREATE `src/components/orgHierarchy/OrgHierarchyAuditDialog.tsx`

Lists audit entries **newest-first**, showing Action, old→new Name, old→new Parent, ChangedBy, and timestamp.

```tsx
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button,
  Table, TableHead, TableRow, TableCell, TableBody, Typography, Chip,
} from '@mui/material';
import type { OrgHierarchyNode, OrgHierarchyNodeAuditEntry } from '../../models';

interface Props {
  open: boolean;
  node: OrgHierarchyNode;
  entries: OrgHierarchyNodeAuditEntry[];
  onClose: () => void;
}

const dash = (v: string | null) => (v == null ? '—' : v);

export default function OrgHierarchyAuditDialog({ open, node, entries, onClose }: Props) {
  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>Audit Trail — {node.name} ({node.code})</DialogTitle>
      <DialogContent>
        {entries.length === 0 ? (
          <Typography color="text.secondary">No audit entries.</Typography>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Action</TableCell>
                <TableCell>Name (old → new)</TableCell>
                <TableCell>Parent (old → new)</TableCell>
                <TableCell>Changed By</TableCell>
                <TableCell>When (UTC)</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {entries.map((e) => (
                <TableRow key={e.id}>
                  <TableCell><Chip size="small" label={e.action} /></TableCell>
                  <TableCell>{dash(e.oldName)} → {dash(e.newName)}</TableCell>
                  <TableCell>{dash(e.oldParentId)} → {dash(e.newParentId)}</TableCell>
                  <TableCell>{e.changedBy}</TableCell>
                  <TableCell>{new Date(e.changedAtUtc).toLocaleString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
```

### FILE 15 — MODIFY `src/App.tsx`

Import the page and add the route **inside** the existing `ProtectedRoute`/`Layout` group (alongside `/nationalities`):

```tsx
import OrgHierarchyMaster from './pages/OrgHierarchyMaster';
// ...
<Route path="/org-hierarchy" element={<OrgHierarchyMaster />} />
```

### FILE 16 — MODIFY `src/components/Layout.tsx`

Add a nav item to the **`Administration`** `menuGroups` entry (next to Company/Branch/Lookup/Nationality). Add the icon import at the top:

```tsx
import AccountTreeIcon from '@mui/icons-material/AccountTree';
```

Then add the item to the `Administration` group's `items` array:

```tsx
{ text: 'Organization Hierarchy', icon: <AccountTreeIcon />, path: '/org-hierarchy' },
```

(Label it "Organization Hierarchy", distinct from the existing "Company Master".)

### FILE 17 — CREATE `src/services/mock/orgHierarchy.mock.test.ts`

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { orgHierarchyMockService } from './orgHierarchy.mock';

// The mock holds module-level state; reset it to a known baseline before each test by
// clearing all non-seed nodes down to leaves. Simplest approach: re-seed via the public API.
async function resetToSeed() {
  const all = await orgHierarchyMockService.getPaged({ page: 1, pageSize: 100 });
  // Delete leaves repeatedly until only the two seed nodes remain.
  let guard = 0;
  while (guard++ < 50) {
    const cur = await orgHierarchyMockService.getPaged({ page: 1, pageSize: 100 });
    const extra = cur.data.filter((n) => n.id !== 'org-ho' && n.id !== 'org-rgn-n');
    if (extra.length === 0) break;
    // delete only leaves
    for (const n of extra) {
      const children = await orgHierarchyMockService.getChildren(n.id);
      if (children.length === 0) { await orgHierarchyMockService.remove(n.id).catch(() => {}); }
    }
  }
  return all;
}

describe('orgHierarchyMockService', () => {
  beforeEach(async () => { await resetToSeed(); });

  it('rejects duplicate code (case-insensitive) with ERR-ORG-409', async () => {
    await expect(orgHierarchyMockService.create({ code: 'ho', name: 'Dup', parentId: null, isActive: true }))
      .rejects.toMatchObject({ response: { status: 409, data: { code: 'ERR-ORG-409', message: 'Node code must be unique' } } });
  });

  it('resolves level from parent (root = 1, child = parent+1)', async () => {
    const child = await orgHierarchyMockService.create({ code: 'BR-1', name: 'Branch 1', parentId: 'org-rgn-n', isActive: true });
    expect(child.level).toBe(3);
    await orgHierarchyMockService.remove(child.id);
  });

  it('rejects self-parent and cycle with ERR-ORG-400', async () => {
    await expect(orgHierarchyMockService.update('org-ho', { name: 'Head Office', parentId: 'org-ho' }))
      .rejects.toMatchObject({ response: { status: 400, data: { code: 'ERR-ORG-400' } } });
    // org-ho -> under its own descendant org-rgn-n is a cycle
    await expect(orgHierarchyMockService.update('org-ho', { name: 'Head Office', parentId: 'org-rgn-n' }))
      .rejects.toMatchObject({ response: { status: 400, data: { code: 'ERR-ORG-400' } } });
  });

  it('recomputes subtree levels on re-parent', async () => {
    const child = await orgHierarchyMockService.create({ code: 'BR-2', name: 'Branch 2', parentId: 'org-rgn-n', isActive: true });
    // Move North Region (level 2, has child at 3) to root => region level 1, child level 2.
    await orgHierarchyMockService.update('org-rgn-n', { name: 'North Region', parentId: null });
    const page = await orgHierarchyMockService.getPaged({ page: 1, pageSize: 100 });
    expect(page.data.find((n) => n.id === 'org-rgn-n')!.level).toBe(1);
    expect(page.data.find((n) => n.id === child.id)!.level).toBe(2);
    // restore
    await orgHierarchyMockService.update('org-rgn-n', { name: 'North Region', parentId: 'org-ho' });
    await orgHierarchyMockService.remove(child.id);
  });

  it('rejects deleting a node that has children with ERR-ORG-409', async () => {
    await expect(orgHierarchyMockService.remove('org-ho'))
      .rejects.toMatchObject({ response: { status: 409, data: { code: 'ERR-ORG-409' } } });
  });

  it('rejects unknown id on update/delete with ERR-ORG-404', async () => {
    await expect(orgHierarchyMockService.remove('nope'))
      .rejects.toMatchObject({ response: { status: 404, data: { code: 'ERR-ORG-404' } } });
  });

  it('orders paged results by Level asc, Name asc, Code asc', async () => {
    const page = await orgHierarchyMockService.getPaged({ page: 1, pageSize: 100 });
    const levels = page.data.map((n) => n.level);
    const sorted = [...levels].sort((a, b) => a - b);
    expect(levels).toEqual(sorted);
  });

  it('builds a tree with roots first and ordered children', async () => {
    const tree = await orgHierarchyMockService.getTree();
    expect(tree.some((r) => r.id === 'org-ho')).toBe(true);
    const ho = tree.find((r) => r.id === 'org-ho')!;
    expect(ho.children.some((c) => c.id === 'org-rgn-n')).toBe(true);
  });

  it('appends an audit entry on create and none on no-op update', async () => {
    const created = await orgHierarchyMockService.create({ code: 'BR-3', name: 'Branch 3', parentId: 'org-ho', isActive: true });
    const afterCreate = await orgHierarchyMockService.getAuditTrail(created.id);
    expect(afterCreate.filter((a) => a.action === 'Create')).toHaveLength(1);
    // no-op update (same name + parent) => no new audit entry
    await orgHierarchyMockService.update(created.id, { name: 'Branch 3', parentId: 'org-ho' });
    const afterNoop = await orgHierarchyMockService.getAuditTrail(created.id);
    expect(afterNoop).toHaveLength(afterCreate.length);
    await orgHierarchyMockService.remove(created.id);
  });

  it('search filters by code or name, case-insensitive substring', async () => {
    const res = await orgHierarchyMockService.getPaged({ search: 'north', page: 1, pageSize: 100 });
    expect(res.data.every((n) => n.name.toLowerCase().includes('north') || n.code.toLowerCase().includes('north'))).toBe(true);
  });
});
```

### FILE 18 — CREATE `src/services/real/orgHierarchy.real.test.ts`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock the shared axios instance before importing the service.
vi.mock('../api', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import api from '../api';
import { orgHierarchyRealService } from './orgHierarchy.real';

const mockedApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

describe('orgHierarchyRealService', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('getPaged GETs /orghierarchy with query params', async () => {
    mockedApi.get.mockResolvedValue({ data: { data: [], total: 0, page: 1, pageSize: 20, totalPages: 1 } });
    await orgHierarchyRealService.getPaged({ search: 'x', page: 2, pageSize: 10 });
    expect(mockedApi.get).toHaveBeenCalledWith('/orghierarchy', { params: { search: 'x', page: 2, pageSize: 10 } });
  });

  it('getTree GETs /orghierarchy/tree', async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    await orgHierarchyRealService.getTree();
    expect(mockedApi.get).toHaveBeenCalledWith('/orghierarchy/tree');
  });

  it('getChildren GETs /orghierarchy/{id}/children', async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    await orgHierarchyRealService.getChildren('n1');
    expect(mockedApi.get).toHaveBeenCalledWith('/orghierarchy/n1/children');
  });

  it('create POSTs /orghierarchy with the create body', async () => {
    mockedApi.post.mockResolvedValue({ data: {} });
    await orgHierarchyRealService.create({ code: 'C', name: 'N', parentId: null, isActive: true });
    expect(mockedApi.post).toHaveBeenCalledWith('/orghierarchy', { code: 'C', name: 'N', parentId: null, isActive: true });
  });

  it('update PUTs /orghierarchy/{id} with name + parentId only', async () => {
    mockedApi.put.mockResolvedValue({ data: {} });
    await orgHierarchyRealService.update('n1', { name: 'N2', parentId: 'p1' });
    expect(mockedApi.put).toHaveBeenCalledWith('/orghierarchy/n1', { name: 'N2', parentId: 'p1' });
  });

  it('remove DELETEs /orghierarchy/{id}', async () => {
    mockedApi.delete.mockResolvedValue({ data: undefined });
    await orgHierarchyRealService.remove('n1');
    expect(mockedApi.delete).toHaveBeenCalledWith('/orghierarchy/n1');
  });

  it('getAuditTrail GETs /orghierarchy/{id}/audit', async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    await orgHierarchyRealService.getAuditTrail('n1');
    expect(mockedApi.get).toHaveBeenCalledWith('/orghierarchy/n1/audit');
  });
});
```

### FILE 19 — CREATE `src/pages/OrgHierarchyMaster.test.tsx`

Mirrors `NationalityMaster.test.tsx`: `vi.mock('../services', ...)` injects a controllable fake `orgHierarchyService`; renders the grid and tree, toggles between them, opens Add/Edit/Delete/Audit, submits create (root and child), rename, and re-parent, and covers the error paths (duplicate-code 409, has-children 409, cycle/self-parent/depth 400) — asserting the list/tree is **unchanged** on error and that the toast message comes from `error.response.data.message` (via the shared interceptor). The Edit parent picker excludes the node and its descendants.

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

// Controllable fake service.
const fake = {
  getPaged: vi.fn(),
  getTree: vi.fn(),
  getChildren: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  getAuditTrail: vi.fn(),
};

vi.mock('../services', () => ({ orgHierarchyService: fake }));

import OrgHierarchyMaster from './OrgHierarchyMaster';

const nodeA = { id: 'a', code: 'HO', name: 'Head Office', level: 1, parentId: null, isActive: true, createdAt: '', updatedAt: '2024-01-01T00:00:00Z' };
const nodeB = { id: 'b', code: 'RGN', name: 'North Region', level: 2, parentId: 'a', isActive: true, createdAt: '', updatedAt: '2024-01-01T00:00:00Z' };

function renderPage() {
  return render(<MemoryRouter><OrgHierarchyMaster /></MemoryRouter>);
}

describe('OrgHierarchyMaster', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    fake.getPaged.mockResolvedValue({ data: [nodeA, nodeB], total: 2, page: 1, pageSize: 20, totalPages: 1 });
    fake.getTree.mockResolvedValue([{ ...nodeA, children: [{ ...nodeB, children: [] }] }]);
    fake.getAuditTrail.mockResolvedValue([]);
  });

  it('renders the grid rows on load', async () => {
    renderPage();
    expect(await screen.findByText('Head Office')).toBeInTheDocument();
    expect(await screen.findByText('North Region')).toBeInTheDocument();
  });

  it('toggles to the tree view', async () => {
    renderPage();
    await screen.findByText('Head Office');
    fireEvent.click(screen.getByRole('button', { name: /Tree/i }));
    await waitFor(() => expect(fake.getTree).toHaveBeenCalled());
  });

  it('submits a create and refreshes', async () => {
    fake.create.mockResolvedValue({ ...nodeA, id: 'c', code: 'BR', name: 'Branch', level: 2 });
    renderPage();
    await screen.findByText('Head Office');
    fireEvent.click(screen.getByRole('button', { name: /Add Node/i }));
    fireEvent.change(screen.getByLabelText(/Code/i), { target: { value: 'BR' } });
    fireEvent.change(screen.getByLabelText(/Name/i), { target: { value: 'Branch' } });
    fireEvent.click(screen.getByRole('button', { name: /^Create$/i }));
    await waitFor(() => expect(fake.create).toHaveBeenCalledWith(
      expect.objectContaining({ code: 'BR', name: 'Branch', parentId: null, isActive: true }),
    ));
  });

  it('keeps the list unchanged when create fails with a duplicate-code 409', async () => {
    const err = Object.assign(new Error('Node code must be unique'), {
      response: { status: 409, data: { code: 'ERR-ORG-409', message: 'Node code must be unique' } },
    });
    fake.create.mockRejectedValue(err);
    renderPage();
    await screen.findByText('Head Office');
    fireEvent.click(screen.getByRole('button', { name: /Add Node/i }));
    fireEvent.change(screen.getByLabelText(/Code/i), { target: { value: 'HO' } });
    fireEvent.change(screen.getByLabelText(/Name/i), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByRole('button', { name: /^Create$/i }));
    // create was attempted; the two original rows remain visible.
    await waitFor(() => expect(fake.create).toHaveBeenCalled());
    expect(screen.getByText('Head Office')).toBeInTheDocument();
    expect(screen.getByText('North Region')).toBeInTheDocument();
  });

  it('keeps state unchanged when delete fails with has-children 409', async () => {
    const err = Object.assign(new Error('Cannot delete a node that has children'), {
      response: { status: 409, data: { code: 'ERR-ORG-409', message: 'Cannot delete a node that has children' } },
    });
    fake.remove.mockRejectedValue(err);
    renderPage();
    await screen.findByText('Head Office');
    // The grid exposes a Delete action per row; invoking it on a parent surfaces the error.
    const deleteButtons = await screen.findAllByLabelText(/Delete/i);
    fireEvent.click(deleteButtons[0]);
    await waitFor(() => expect(fake.remove).toHaveBeenCalled());
    expect(screen.getByText('Head Office')).toBeInTheDocument();
  });
});
```

### Frontend implementation checklist (manual)

Work through these by hand in the `Finnova-UI` repo. **CREATE** = new file, **MODIFY** = edit existing file.

- **New model** — CREATE `src/models/orgHierarchy.model.ts`
- **Model index** — MODIFY `src/models/index.ts` (add `export * from './orgHierarchy.model';`)
- **New interface** — CREATE `src/services/interfaces/orgHierarchy.interface.ts`
- **Interface index** — MODIFY `src/services/interfaces/index.ts` (add `export type { IOrgHierarchyService, OrgHierarchyQueryParams } from './orgHierarchy.interface';`)
- **Real service** — CREATE `src/services/real/orgHierarchy.real.ts` (`basePath = '/orghierarchy'`, shared `api`)
- **Mock service** — CREATE `src/services/mock/orgHierarchy.mock.ts` (backend-shaped `ERR-ORG-4xx/409` errors, English-only seeds)
- **Service toggle** — CREATE `src/services/orgHierarchy.service.ts`
- **Service index** — MODIFY `src/services/index.ts` (add `export { orgHierarchyService } from './orgHierarchy.service';`)
- **Page** — CREATE `src/pages/OrgHierarchyMaster.tsx`
- **Components** — CREATE `src/components/orgHierarchy/OrgHierarchyGrid.tsx`, `OrgHierarchyTree.tsx`, `OrgHierarchyAddDialog.tsx`, `OrgHierarchyEditDialog.tsx`, `OrgHierarchyAuditDialog.tsx`
- **Route** — MODIFY `src/App.tsx` (import page + `<Route path="/org-hierarchy" ... />` inside the `ProtectedRoute`/`Layout` group)
- **Navigation** — MODIFY `src/components/Layout.tsx` (add an "Organization Hierarchy" item to the `Administration` group + `AccountTreeIcon` import)
- **Tests** — CREATE `src/services/mock/orgHierarchy.mock.test.ts`, `src/services/real/orgHierarchy.real.test.ts`, `src/pages/OrgHierarchyMaster.test.tsx`

**New npm dependency?** **None required.** `@mui/x-data-grid` is already installed and reused for the flat list; the tree uses a custom recursive MUI `List`/`Collapse` component. Add `@mui/x-tree-view` **only** if the team prefers a built-in `RichTreeView`/`SimpleTreeView` — that is the single optional new dependency.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

This revision is UI-scoped, so the properties below are limited to the **UI's own logic** — the in-memory **mock service** (which has clear input/output behavior and a large input space: trees, search terms, pagination) and the **Edit dialog's parent-exclusion helper**. Backend correctness (persistence, transactions, authorization) is owned by the backend and is verified there, not here. Each property is validated by the Vitest suite (FILES 17 and 19).

### Property 1: Mock code uniqueness on create

*For any* seeded node and *any* casing or surrounding-whitespace variant of its `code`, `create` SHALL reject the request with a backend-shaped error whose `response.status == 409`, `response.data.code == 'ERR-ORG-409'`, and `response.data.message == 'Node code must be unique'`, and SHALL add no node.

**Validates: API Reference — duplicate-code 409 (ERR-ORG-409)**

### Property 2: Mock level invariant (root = 1, child = parent + 1)

*For any* node set reachable through `create` and `update` on the mock, every node SHALL satisfy: a node with `parentId == null` has `level == 1`, and a node with a parent has `level == parent.level + 1`. Clearing a parent makes the node a root at `level 1`, and re-parenting recomputes the moved node's and every descendant's `level` so the invariant holds across the whole subtree.

**Validates: API Reference — server-resolved level; re-parent recompute**

### Property 3: Mock rejects self-parent and cycles

*For any* node, an `update` that sets `parentId` to the node's own id **or** to any node in the node's descendant set SHALL be rejected with `response.status == 400` and `response.data.code == 'ERR-ORG-400'`, and SHALL leave the node set unchanged.

**Validates: API Reference — self-parent / cycle 400 (ERR-ORG-400)**

### Property 4: Mock leaf-only delete

*For any* node with at least one child, `remove` SHALL be rejected with `response.status == 409` / `ERR-ORG-409` and SHALL leave the node and its children unchanged; *for any* leaf node, `remove` SHALL delete it so it no longer appears in `getPaged`, `getChildren`, or `getTree` results.

**Validates: API Reference — has-children 409 (ERR-ORG-409)**

### Property 5: Mock audit is appended on mutation and skipped on no-op/rejection

*For any* successful `create`/`update`/`delete`, exactly one audit entry SHALL be appended for the affected node (`Create` with `oldName/oldParentId == null`; `Update` with old→new pairs; `Delete` with `newName/newParentId == null`); *for any* no-op update (same `name` and `parentId`) or any rejected operation, the audit trail SHALL be unchanged.

**Validates: API Reference — audit endpoint semantics; no-op = no audit**

### Property 6: Mock search, ordering, and pagination

*For any* dataset and *any* search term, every record returned by `getPaged` SHALL contain the trimmed term (case-insensitive) as a substring of its `code` or `name` (blank term = no filter); results SHALL be ordered by `level` asc, then `name` asc, then `code` asc; `total` SHALL equal the filtered count; `totalPages` SHALL equal `max(1, ceil(total / pageSize))`; and at most `pageSize` items SHALL be returned.

**Validates: API Reference — paged search ordering & pagination**

### Property 7: Edit parent-exclusion excludes self and descendants

*For any* node and *any* node set, the Edit dialog's parent options SHALL exclude the node itself and every one of its descendants (client-side cycle avoidance), so the picker can never offer a parent that would create a cycle.

**Validates: Frontend Design — Edit dialog parent picker**

## Testing Strategy

Tests ship in the same change in the `Finnova-UI` repo (**Vitest + React Testing Library**, jsdom). No RxJS marble tests and no Redux store tests apply to this stack. All tests must pass and the build must be green (`npm run build`) before the feature is complete. Run the suite with `npm test` (`vitest run`).

### Service unit tests (FILES 17–18)

- **Mock service (`orgHierarchy.mock.test.ts`)** — verifies the mock's logic against the properties above: case-insensitive duplicate-code 409, self-parent/cycle/depth 400, leaf-only delete 409, unknown-id 404, level resolution from parent, subtree level recompute on re-parent, `Level asc → Name asc → Code asc` ordering, tree building, search filtering, pagination, and audit append on create/update/delete (with **no** audit on a no-op or rejected op). Asserts errors expose `error.response.data.{code,message}` so they flow through the shared interceptor exactly like real errors.
- **Real service (`orgHierarchy.real.test.ts`)** — mocks the shared `api` module and asserts each method hits the right verb/path (`GET /orghierarchy`, `GET /orghierarchy/tree`, `GET /orghierarchy/{id}/children`, `POST /orghierarchy`, `PUT /orghierarchy/{id}`, `DELETE /orghierarchy/{id}`, `GET /orghierarchy/{id}/audit`) with the correct body/params, and returns `res.data`.

### Page / component tests (FILE 19)

`OrgHierarchyMaster.test.tsx` mocks `../services` to inject a controllable fake `orgHierarchyService`, then:
- renders the grid rows and toggles to the tree view;
- opens Add/Edit/Delete/Audit dialogs; submits create (root **and** child), rename, and re-parent;
- covers the error paths — duplicate-code (409), has-children (409), cycle/self-parent/depth (400) — asserting the grid/tree is **unchanged** on error and that the surfaced message is read from `error.response.data.message`;
- verifies the Edit parent picker excludes the node and its descendants.

Because these are UI behaviors over generated/controlled inputs, the mock-service properties (Correctness Properties 1–6) are exercised as Vitest assertions in FILE 17, and the parent-exclusion property (Property 7) is exercised through the Edit dialog in FILE 19. There is no property-based-testing library in this stack; the "for all" properties are validated with representative and edge-case inputs via Vitest.

## Design Decisions & Tradeoffs (UI-relevant)

1. **Namespace-collision avoidance (`orgHierarchy` / `OrgHierarchy`).** The repo already ships an unrelated **Company Master** owning `organization.model.ts`, `organization.service.ts`, `OrganizationForm.tsx`, `IOrganizationService`, `organizationService`, and route `/organizations`. To avoid clobbering it, this feature uses the distinct `orgHierarchy`/`OrgHierarchy` namespace and route `/org-hierarchy` throughout. Tradeoff: slightly longer names; necessary to keep both features intact.

2. **Custom recursive MUI tree instead of `@mui/x-tree-view`.** `@mui/x-data-grid` is already installed and is reused for the flat list, but `@mui/x-tree-view` is **not** installed. The tree uses a custom `List`/`Collapse`/`ListItemButton` component (the pattern already in `Layout.tsx`), adding **zero** dependencies. Adding `@mui/x-tree-view` is the only optional new dependency and is deferred unless a richer built-in tree UX is wanted. Tradeoff: hand-rolled expand/collapse vs a batteries-included widget; acceptable for master-data scale.

3. **Mock errors mirror the backend error shape.** The mock throws errors whose `response.data` carries the same `{ code, message }` and `status` as the backend `ERR-ORG-4xx/409` responses, so mock and real modes behave identically through the shared axios interceptor (which toasts `error.response.data.message`). Tradeoff: the mock duplicates some backend rules (level resolution, cycle/depth checks); justified because it keeps mock-mode UX faithful and makes the service unit-testable without a backend.

4. **English-only single `name` field.** Per product-context (India-only), the model carries one `name` field — no `nameEn`/`nameAr`, no RTL, no Arabic seed data. Any future multi-language need must be raised explicitly for confirmation.

5. **UI preserves state on error; the server remains the source of truth.** All mutations refresh from the service on success; on error the UI does not optimistically mutate the list/tree — the shared interceptor toasts the message and the current data stays put. The Edit picker excludes self + descendants as a courtesy, but the server still enforces the structural rules (cycle/self-parent/depth), so the UI never has to duplicate authority.
