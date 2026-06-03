# Analytics / Admin Access-Control Audit

Scope reviewed:
- API endpoint registration files
- analytics endpoints
- report/export endpoints
- action queue endpoints
- import endpoints
- worker/admin/refresh/cache endpoints
- frontend route and navigation guards

Role model used for this audit:
- Viewer: read dashboards/reports
- Analyst: create action queue items
- Manager: export reports, close/approve actions
- Admin: import, refresh, cache clear, worker/config

## Findings

| Area | Endpoint/UI | Current access | Required role | Gap | Priority |
|---|---|---|---|---|---|
| Analytics read | Frontend routes like `/analytics`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/data-quality` and the related `GET /api/analytics/*` endpoints | Open route access; analytics GET endpoints do not have an auth wrapper, only rate limiting | Viewer | Anyone who knows the URL can read dashboards and drill-downs; no route guard or API role gate | P1 |
| Analytics reports | `/analytics/supplier/report`, `/analytics/reports/pilot-intake`, `GET /api/analytics/reports/supplier-decision`, `GET /api/analytics/reports/pilot-intake` | Open route access; report endpoints are exposed without a role check | Viewer | Report pages are reachable directly and their backend report payloads are readable without explicit Viewer enforcement | P1 |
| Report export system | `POST /api/documents/generate`, `POST /api/documents/batch`, `GET /api/documents/{id}`, `GET /api/documents/{id}/print`, `GET /api/exports`, `GET /api/exports/{jobId}/status` plus export buttons in `SupplierDecisionReportActions`, `Inventory` export flows, and `PilotIntakeReportPage` | Role-based, but broader than the target model: `DocumentAccessControlService` allows `Admin`, `AnalyticsExport`, and `AnalyticsViewer`; downloads/listing are ownership-based unless elevated | Manager | Sensitive report exports are not limited to Manager; `AnalyticsViewer` can generate exports and ownership rules are used instead of a report-specific manager policy | P0 |
| Action queue read | `/analytics/actions` UI and `GET /api/analytics/actions`, `/counts`, `/{id}` | Open API; no auth wrapper on the action queue read endpoints | Viewer | Action queue content is visible to any caller; no Viewer gate | P1 |
| Action queue write | `POST /api/analytics/actions`, `POST /api/analytics/actions/status`, `PATCH /api/analytics/actions/{id}/status`, `PATCH /api/analytics/actions/{id}/outcome` and the action buttons in `AnalyticsActionsPage`, `SupplierDecisionReportActions`, `InventoryPage` | Open API; the service uses user identifiers from headers/claims, but there is no authorization check before create/update | Analyst for create, Manager for close/approve | Any caller can create or mutate queue items; create and close/approve semantics are not role-separated | P1 |
| Access import surface | `/access-import` UI and `GET /api/access-import/runtime-status`, `/batches`, `/jobs`, batch detail/logs, `POST /api/access-import/preview`, `POST /api/access-import/cleanup/preview`, `GET /api/access-import/cleanup/archive`, `POST /api/access-import/cleanup/archive/export`, `POST /api/access-import/cleanup/archive/restore-script` | Mixed. Some mutating paths use an admin-key check (`/run`, `/jobs`, `cleanup/execute`, delete batch), but many read/preview/archive endpoints are open | Admin | Import introspection, archive export, and cleanup preview are exposed without admin auth; this is a high-risk operational surface | P0 |
| Manual refresh | `POST /api/admin/run-stale-recovery`, `POST /api/workers/{workerName}/start`, `POST /api/workers/{workerName}/restart`, `POST /api/workers/control/enable` | Some worker paths use an admin-key helper; `run-stale-recovery` is currently open in `AdminConfigEndpoints` | Admin | Manual refresh/worker-trigger actions are not consistently role-protected, and the stale recovery endpoint is especially exposed | P0 |
| Worker control | `/api/workers/configuration`, `GET /api/workers/health`, `GET /api/workers/control`, `POST /api/workers/control/disable`, `/api/workers/{workerName}/stop`, `/schedule/enable`, `/schedule/disable` | Read-side endpoints are open; mutating endpoints use a header-based admin-key check in production | Admin | Operational worker state is publicly readable, and control actions rely on a header key rather than a role-based policy | P0 |
| Cache status / clear | `GET /api/analytics/cached/cache/status`, `POST /api/analytics/cached/cache/invalidate`, clear-cache button in `WorkersPanel` | Open API; cache status and cache invalidation are callable without a role check | Admin | Cache clear is a public action in code; the frontend also exposes the button in an unguarded admin page | P0 |
| Admin config / backend routing | `/admin/configuration`, `GET /api/admin/pending-batches`, `POST /api/admin/requeue-batch/{batchId}`, `POST /api/admin/run-stale-recovery`, `GET /api/admin/health-check`, `GET /api/admin/audit-log`, `GET /api/admin/workers/*`, `GET /api/admin/backend-routing`, `POST /api/admin/backend-routing`, `GET /api/admin/backend-routing/ping/{provider}` | The `/api/admin` and `/api/admin/backend-routing` groups do not have a group-level auth wrapper; some mutation helpers check the admin key, but several read routes are open | Admin | This is the clearest open dangerous area in the repo: backend routing and admin diagnostics are exposed without a consistent authorization gate | P0 |
| Redis admin toggle | `GET /api/redis/status`, `POST /api/redis/toggle`, Redis controls in `ConfigurationPage` | Open API; no auth wrapper or admin-key check | Admin | Any caller can toggle the Redis-backed cache path | P0 |
| Admin repair | `/admin/nivelacija-repair`, `GET /admin/repair/nivelacije/preflight`, `POST /admin/repair/nivelacije` | Backend uses Admin role or admin-key checks, but the frontend route is not guarded | Admin | Data-modifying repair tools are reachable by direct URL and depend on request-time key checks rather than a guarded route | P0 |
| Snapshot admin | `GET/POST /api/analytics/snapshots/*` | Guarded by `SnapshotAdminEnabled` plus Admin role or admin-key | Admin | Better than the other admin surfaces, but still header-key compatible and hidden by feature flag instead of a route policy | P2 |
| Frontend route / nav guards | `App.tsx`, `navConfig.ts`, `Sidebar.tsx`, `ConfigurationPage.tsx` | No route guard, no role-based nav filtering, and admin routes are reachable by direct URL | Viewer / Analyst / Manager / Admin depending on screen | The UI does not enforce the access model; it only surfaces links. Direct navigation works for admin and operational screens | P0 |

## Dangerous actions

P0 actions identified in this audit:
- manual refresh
- worker control
- cache clear
- import
- admin configuration
- report export if sensitive

## Notes

- The backend already has some role-aware export logic in `DocumentAccessControlService`, but it is broader than the requested Viewer/Analyst/Manager/Admin model.
- Several admin endpoints use a shared `X-Admin-Key` helper instead of a first-class authorization policy. That is better than nothing, but it is not the same as a role-based guard.
- This audit did not change runtime code. It is intended to document the current exposure first and separate the clear follow-up fixes from the lower-risk items.
