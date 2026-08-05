# Analytics P0 Access-Control Implementation Plan

Date: 2026-06-17
Scope: analytics and admin endpoint protection before external pilot/customer use

## Purpose

This plan turns the existing audit into a small-step implementation path for P0 endpoint protection.

It does not:

- introduce a new auth system
- refactor all routing at once
- implement code in this task

It does:

- define the minimum roles
- define which endpoint groups are P0
- define where server-side enforcement must live
- define what the frontend must hide
- define the tests needed for each group

## Source Inputs

- [ANALYTICS_ACCESS_CONTROL_AUDIT.md](./ANALYTICS_ACCESS_CONTROL_AUDIT.md)
- [RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md](./RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md) (STAB03 Phase 1 decision: API-key admin mode)
- [TENANT_SAFETY_CHECKLIST.md](./TENANT_SAFETY_CHECKLIST.md)
- current endpoint registration under `Api/Endpoints/*`
- current routing/navigation under:
  - `Klijent/clientapp/src/App.tsx`
  - [navConfig.ts](c:/Users/Ivan/source/repos/Trendplus2/Klijent/clientapp/src/layout/navConfig.ts)
  - [ConfigurationPage.tsx](c:/Users/Ivan/source/repos/Trendplus2/Klijent/clientapp/src/pages/ConfigurationPage.tsx)

## Current Constraint

The app does not yet have a shared `AddAuthentication()` / `UseAuthentication()` flow with route policies, so Phase 1 must work with the current reality:

- backend enforcement must be explicit per endpoint group
- frontend visibility must be best-effort only
- backend must remain the source of truth for authorization

## Minimal Role Model

### Viewer

Can:

- open read-only analytics screens
- view refresh status
- view trust/freshness/data-quality context
- open non-destructive reports that are explicitly allowed

Cannot:

- trigger refresh
- clear cache
- import data
- control workers
- update actions
- change admin configuration

### Analyst

Can:

- do everything Viewer can
- create analytics action items
- use operational analytics surfaces needed for internal analysis

Cannot:

- clear cache
- control workers
- run import/reset/cleanup flows
- change admin/runtime configuration
- approve destructive outcome/status changes reserved for management

### Manager

Can:

- do everything Analyst can
- update action status/outcome
- access customer-facing report/export flows
- trigger selected operational actions only if explicitly approved in policy

Cannot:

- control low-level worker/runtime settings
- clear cache globally
- run import cleanup or destructive admin repair flows unless explicitly promoted to Admin

### Admin

Can:

- do everything Manager can
- run imports and cleanup
- control workers
- clear analytics cache
- run manual refresh/repair
- change backend/runtime/admin configuration

## Phase 1 Security Principle

Phase 1 should implement these rules:

1. Read-only analytics is allowed to `Viewer`.
2. Dangerous or state-changing actions are restricted to `Admin` or `Manager`, depending on business impact.
3. Frontend hides dangerous controls for unauthorized users.
4. Backend enforces regardless of UI.
5. If the backend cannot prove authorization, it must deny the action.

## Authorization Building Blocks For Phase 1

Do not refactor auth globally yet. Reuse a minimal, consistent pattern:

1. Introduce one shared backend helper for role evaluation.
2. Accept current `X-Admin-Key` fallback only as a temporary compatibility layer.
3. Prefer authenticated user role checks where available.
4. Return `401` or `403` consistently for protected endpoints.
5. Add one frontend capability source for hiding admin/manager controls.

Recommended backend shape for follow-up tasks:

- `RequireViewer`
- `RequireAnalyst`
- `RequireManager`
- `RequireAdmin`

Recommended frontend shape for follow-up tasks:

- capability hook or auth context
- route visibility helper for nav items
- page-level feature flags for destructive controls

## P0 Endpoint Group Plan

### 1. Manual Analytics Refresh

Scope:

- `/api/analytics/optimize`
- `/api/admin/run-analytics-optimization`
- `/api/admin/sync-analytics-db`
- `/api/admin/init-scoring-tables`
- worker-triggered manual analytics refresh controls where refresh changes analytics state

Current access:

- mixed
- some routes are effectively public
- some admin flows depend on ad-hoc key checks

Required role:

- `Admin`

Backend enforcement location:

- endpoint registration handlers in [AllEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AllEndpoints.cs)
- any related admin endpoint wrappers
- shared refresh authorization helper once introduced

Frontend visibility rule:

- do not show refresh/repair buttons to `Viewer`, `Analyst` or `Manager`
- keep refresh status visible to `Viewer`
- show manual refresh controls only in admin surfaces

Tests required:

- integration test: unauthorized request gets `401` or `403`
- integration test: authorized admin request reaches handler
- frontend test: refresh button hidden without admin capability

### 2. Clear Analytics Cache

Scope:

- `/api/analytics/cached/cache/invalidate`
- any UI button that clears analytics cache

Current access:

- backend route is currently not role-protected
- admin-style UI exists but backend does not enforce the same boundary

Required role:

- `Admin`

Backend enforcement location:

- [CachedAnalyticsEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/CachedAnalyticsEndpoints.cs)

Frontend visibility rule:

- cache clear control hidden for all non-admin users
- cache status read surface may remain visible to `Admin` and optionally `Manager`, but clear action stays admin-only

Tests required:

- integration test: `POST /api/analytics/cached/cache/invalidate` fails for non-admin
- integration test: admin request succeeds
- frontend test: cache clear CTA not rendered without admin capability

### 3. Import / Access-Import

Scope:

- `/access-import`
- `/api/access-import/run`
- `/api/access-import/jobs`
- `/api/access-import/batches/{id}` delete
- `/api/access-import/cleanup/*`
- `/api/access-import/cleanup/archive/export`
- `/api/access-import/cleanup/archive/restore-script`
- admin stale recovery and requeue flows related to import

Current access:

- read/status flows are broadly visible
- destructive flows use ad-hoc `X-Admin-Key` checks in some places
- cleanup/restore surfaces are too sensitive for broad availability

Required role:

- `Admin`

Backend enforcement location:

- [AccessImportEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AccessImportEndpoints.cs)
- [AccessImportRestoreEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AccessImportRestoreEndpoints.cs)
- [AdminConfigEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AdminConfigEndpoints.cs)

Frontend visibility rule:

- `/access-import` route hidden for non-admin in primary navigation
- import execution, cleanup, archive export and restore controls hidden for non-admin
- read-only batch status should also be treated as admin-only in Phase 1 because it reveals data origin and operational internals

Tests required:

- integration tests for run, delete, cleanup execute, archive export and restore-script protection
- integration test for stale recovery/requeue admin protection
- frontend route/nav test: Access Import link hidden without admin capability

### 4. Worker Control

Scope:

- `/api/workers/control/enable`
- `/api/workers/control/disable`
- `/api/workers/{workerName}/start`
- `/api/workers/{workerName}/stop`
- `/api/workers/{workerName}/restart`
- `/api/workers/{workerName}/schedule/*`
- admin worker configuration surfaces

Current access:

- write routes rely on ad-hoc admin-key checks
- read/control status routes are publicly reachable
- worker controls are visible in configuration surfaces

Required role:

- `Admin`

Backend enforcement location:

- [AllEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AllEndpoints.cs)
- [WorkerConfigurationEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/WorkerConfigurationEndpoints.cs)
- [AdminConfigEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AdminConfigEndpoints.cs)

Frontend visibility rule:

- hide worker control panels for non-admin
- worker health may stay visible to `Admin`; do not expose runtime control toggles outside admin configuration

Tests required:

- integration tests for worker enable/disable and start/stop/restart/schedule routes
- frontend test: worker control buttons hidden without admin capability

### 5. Admin Configuration

Scope:

- `/admin/configuration`
- `/api/admin/backend-routing`
- `/api/redis/toggle`
- `/api/admin/pending-batches`
- `/api/admin/audit-log`
- `/api/admin/health-check`
- backend routing ping/update surfaces

Current access:

- frontend configuration route is publicly routable
- backend routing endpoints are not consistently protected
- runtime/admin diagnostics are too exposed

Required role:

- `Admin`

Backend enforcement location:

- [AdminBackendRoutingEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AdminBackendRoutingEndpoints.cs)
- [AdminConfigEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AdminConfigEndpoints.cs)
- Redis/admin handlers in [AllEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AllEndpoints.cs)

Frontend visibility rule:

- hide `/admin/configuration` nav entry for non-admin
- redirect or block direct route access for non-admin in Phase 1 frontend
- keep admin diagnostics panels invisible without admin capability

Tests required:

- integration tests for backend-routing update and admin diagnostics routes
- integration tests for Redis toggle protection
- frontend route smoke test for unauthorized route handling
- frontend nav test for configuration/admin links

### 6. Destructive Action Updates

Scope:

- `POST /api/analytics/actions`
- `PATCH /api/analytics/actions/{id}/status`
- `PATCH /api/analytics/actions/{id}/outcome`

Current access:

- create and update routes are public write surfaces

Required role:

- create action: `Analyst`
- update status/outcome: `Manager`

Backend enforcement location:

- [AnalyticsActionsEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AnalyticsActionsEndpoints.cs)

Frontend visibility rule:

- `Viewer` can read action lists only
- `Analyst` sees create action controls
- `Manager` sees status/outcome update controls
- if capability is missing, hide buttons instead of rendering disabled destructive UI without explanation

Tests required:

- integration test: viewer cannot create action
- integration test: analyst can create but cannot update manager-only transitions if policy requires it
- integration test: manager can update status/outcome
- frontend test: create button hidden for viewer
- frontend test: status/outcome controls hidden for non-manager

### 7. Report / Export Endpoints With Customer Data

Scope:

- `/api/analytics/reports/*`
- `/api/documents/*`
- `/api/exports`
- `/api/exports/{jobId}/status`
- inventory export/document generation routes
- print/download URLs that expose customer-facing report payloads

Current access:

- analytics report routes are readable without explicit role boundary
- document generation/list/download relies on document context and ownership logic, but not a clear role policy
- export metadata and customer-facing report content are still too open for external pilot use

Required role:

- `Manager`

Backend enforcement location:

- [AnalyticsReportsEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AnalyticsReportsEndpoints.cs)
- [DocumentEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/DocumentEndpoints.cs)
- [InventoryEndpoints.cs](c:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/InventoryEndpoints.cs)

Frontend visibility rule:

- report/export buttons visible to `Manager` and `Admin`
- read-only analytics pages may remain visible to `Viewer`, but export/print/download CTAs must be hidden without report/export capability

Tests required:

- integration test: non-manager cannot generate document export
- integration test: non-manager cannot list/export customer document jobs beyond allowed ownership policy
- integration test: manager can generate and access report/export flow
- frontend test: export/print buttons hidden for viewer/analyst

## Supporting Read-Only Surfaces In Phase 1

These are not the P0 dangerous groups, but the Phase 1 policy must be clear:

| Surface | Phase 1 role |
|---|---|
| dashboard, product decisions, supplier analytics, inventory, data quality, refresh status | `Viewer` |
| action queue read-only list | `Viewer` |
| admin/ops/performance/log operational internals | `Admin` now, reconsider later |

Recommendation:

- keep general analytics read surfaces open to `Viewer`
- keep internal observability, logs and operational diagnostics out of Viewer scope for now

## Frontend Phase 1 Plan

### Navigation

Update [navConfig.ts](c:/Users/Ivan/source/repos/Trendplus2/Klijent/clientapp/src/layout/navConfig.ts):

- hide `/access-import` for non-admin
- hide `/admin/common-products` and admin-only repair/config links for non-admin
- hide admin group entirely for non-admin
- keep analytics read routes visible to `Viewer`

### Routes

Update [App.tsx](c:/Users/Ivan/source/repos/Trendplus2/Klijent/clientapp/src/App.tsx):

- add a small route-level guard wrapper for admin routes in Phase 1
- do not refactor lazy routing structure
- guard at least:
  - `/admin/configuration`
  - `/admin/nivelacija-repair`
  - `/access-import`

### Page Controls

Update page-level visibility only after backend enforcement exists:

- `ConfigurationPage` admin panels
- `WorkersPanel` control buttons
- Access Import execution/cleanup controls
- analytics action create/update controls
- report export/print/download buttons

## Backend Phase 1 Plan

### Minimal shared helper

Create one shared authorization helper for follow-up tasks so we stop duplicating:

- `IsViewer`
- `IsAnalyst`
- `IsManager`
- `IsAdmin`

Phase 1 compatibility:

- allow current admin-key fallback only for `Admin`
- do not use the admin key as a substitute for Manager/Analyst/Viewer role logic

### Response behavior

Standardize:

- `401 Unauthorized` when the user is unauthenticated or no credential is present
- `403 Forbidden` when authenticated but role is insufficient

## Test Plan By Layer

### Backend integration tests

Add route-level tests for each P0 group:

- protected route returns `401` or `403` for insufficient access
- same route does not return `404`
- authorized request reaches expected result path

Priority order:

1. action writes
2. cache invalidate
3. import cleanup/run/delete
4. worker control
5. admin configuration
6. report/export generation
7. manual refresh/repair

### Frontend tests

Add focused tests around visibility rules:

- sidebar/nav items hidden for unauthorized roles
- admin routes blocked or redirected
- destructive buttons absent for unauthorized roles
- read-only analytics routes remain available to `Viewer`

### Regression tests

For every implementation group:

- one backend access test minimum
- one frontend visibility test if a visible control exists

## Recommended Implementation Order

1. Shared backend authorization helper and first protected group: cache invalidate
2. Action queue write protection
3. Access-import and cleanup protection
4. Worker control and admin configuration
5. Report/export protection
6. Frontend nav/route/page visibility polish across the same groups

## Out Of Scope For This Plan

- full tenant-scoped RBAC architecture
- identity provider integration
- replacing every legacy admin-key path in one commit
- cross-repo auth refactor

## Definition Of Done For Phase 1

Phase 1 is complete when:

1. all P0 destructive/state-changing analytics/admin routes have explicit backend role enforcement
2. frontend hides dangerous controls for unauthorized roles
3. read-only analytics remains available to `Viewer`
4. route-level tests cover each protected group
5. no dangerous operation depends on “hidden in UI” alone
