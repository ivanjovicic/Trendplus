# Analytics / Admin Access Control Audit

Scope reviewed:

- API endpoint registration files
- analytics endpoints
- reports / export endpoints
- action queue endpoints
- import / access import endpoints
- worker / admin / config endpoints
- frontend route guards and navigation

## What the repo currently does

- `Api/Program.cs` maps the in-scope endpoint sets into the app pipeline and calls `UseAuthorization()`, but this repo scan did not find a global `AddAuthentication()` / `AddAuthorization()` / `RequireAuthorization()` policy that would make the route layer self-documenting.
- Most analytics routes are exposed as open minimal API routes with rate limiting only.
- Some admin flows rely on handler-local checks such as `X-Admin-Key`, `Admin:ApiKey`, `ADMIN_API_KEY`, or `User.IsInRole("Admin")`.
- The frontend router and sidebar do not enforce role-based route guards. Pages are reachable if the user knows the URL.

## Role model to document

- `Viewer`: read dashboards / reports
- `Analyst`: create action items
- `Manager`: export reports, close / approve actions
- `Admin`: import, refresh, cache clear, worker control, config

## Audit

| Area | Endpoint/UI | Current access | Required role | Gap | Priority |
|---|---|---|---|---|---|
| Analytics read dashboards | `/analytics`, `/analytics/insight-studio`, `/api/analytics/cached/*`, `/api/analytics/intelligence/*`, `/api/analytics/advanced/*` | Open route/UI; backend handlers use rate limiting only | Viewer | No route guard or API auth metadata; read surfaces are exposed to any caller who can reach the app | P2 |
| Product decisions | `/analytics/products`, `/api/analytics/cached/products/decision-center` | Open route/UI; no role filter in `App.tsx` or sidebar | Viewer | Decision pages are readable without a role gate; the page can also launch action creation flows | P2 |
| Supplier scorecard | `/analytics/supplier`, `/api/analytics/advanced/supplier-scorecard`, `/analytics/supplier/report` | Open route/UI; report page has export controls but no route-level guard | Viewer | Scorecard / report content is reachable without a viewer role; export controls sit on top of an open page | P1 |
| Inventory analytics | `/analytics/inventory`, `/api/analytics/cached/inventory/*`, `/api/analytics/intelligence/inventory-risk` | Open route/UI; backend has rate limits only | Viewer | Inventory decision pages are exposed without a viewer gate | P2 |
| Data quality | `/analytics/data-quality`, `/api/analytics/data-quality/*`, `/api/analytics/refresh-status` | Open route/UI; refresh status is also open read-only | Viewer | Data quality and refresh state are visible without a role guard | P2 |
| Reports / export read path | `/analytics/reports/pilot-intake`, `/analytics/supplier/report`, `/api/analytics/reports/*` | Open route/UI; no route authorization metadata | Viewer | Report pages are readable without any role gate | P1 |
| Reports / export download path | `/api/documents/*`, `/api/exports/*` | Handler-local document security only. `DocumentUserContextAccessor` defaults to `anonymous` + `AnalyticsExport` when no claims/headers exist; ownership checks allow bypass for elevated roles | Manager | Export access is role-shaped in code, but not enforced by route auth and can be driven by headers / default context; this is sensitive if exports contain pilot data | P0 |
| Action queue create | `/api/analytics/actions` `POST`, `/api/analytics/actions/status`, UI: `/analytics/actions` and report-to-queue buttons | Open route/UI; handlers use user claims only for attribution, not authorization | Analyst | Any caller can create queue items if the API is reachable | P1 |
| Action queue update / close / approve | `/api/analytics/actions/{id}/status`, `/api/analytics/actions/{id}/outcome`, UI: `/analytics/actions` | Open route/UI; no `RequireAuthorization` / role policy | Manager | Status/outcome mutation is not role-gated; approval and close actions are only checked by handler logic | P1 |
| Manual refresh | `/api/analytics/snapshots/batches`, `/api/analytics/snapshots/batches/{id}/generate`, `/api/analytics/snapshots/batches/{id}/activate`, `/api/analytics/snapshots/batches/{id}/deactivate`, `/api/admin/run-stale-recovery` | Handler-local admin checks only. Snapshot endpoints require `SnapshotAdminEnabled` plus `Admin` role or `X-Admin-Key`; admin recovery uses `IsAdminRequest` | Admin | Dangerous refresh actions are not obvious from route registration and are not protected by route-level RBAC; some rely on header-key fallback | P0 |
| Clear analytics cache | `/api/analytics/cached/cache/invalidate`, `/api/redis/toggle`, UI: workers / cache controls in `ConfigurationPage` and `WorkersPanel` | `cache/invalidate` and `redis/toggle` are exposed without route auth metadata; UI buttons are visible in the admin pages | Admin | Cache clear / toggle is reachable without an explicit route guard; this is a direct freshness / consistency risk | P0 |
| Worker control | `/api/workers/control/enable`, `/api/workers/control/disable`, `/api/workers/{workerName}/start`, `/stop`, `/restart`, `/schedule/*`, UI: worker panels in `ConfigurationPage` / `WorkersPanel` | Mixed. `WorkerConfigurationEndpoints` fail open in development and also when no admin key is configured; `AdminConfigEndpoints` fail closed when no key exists but still use header-key checks | Admin | Worker control is not centralized in RBAC and one control surface is fail-open by default when no admin key is configured | P0 |
| Import / access import | `/api/access-import/runtime-status`, `/scope-options`, `/preview`, `/batches`, `/jobs`, `/run`, `/cleanup/preview`, `/cleanup/archive`, `/cleanup/archive/export`, `/cleanup/execute` | Mixed. Read/discovery endpoints are open; job/run/delete/cleanup actions use handler-local `IsAdminRequest` checks with development bypass and `X-Admin-Key` fallback | Admin | One feature mixes unauthenticated discovery with admin-only mutation; the protection is not obvious from the route registration and is easy to misread | P0 |
| Admin configuration | `/admin/configuration`, `/configuration`, `/api/admin/*`, `/api/admin/backend-routing/*` | Frontend route is open with no guard. Backend admin endpoints rely on local key checks or development mode; `backend-routing` endpoints do not declare authorization metadata | Admin | Configuration and backend routing are reachable without a frontend role gate, and backend protection is header-key based rather than route-based | P0 |

## Notes

- The current codebase already uses role names in a few places (`Admin`, `AnalyticsExport`, `AnalyticsViewer`, `AnalyticsAdmin`, `DocumentAdmin`), but they are not enforced consistently at the route level.
- I did not implement RBAC changes here because the task asked for an audit only unless an endpoint was trivially fixable and clearly dangerous.
- If you want the next step, the safest order is:
  1. lock `worker control`, `manual refresh`, `cache clear`, `import`, and `admin config`
  2. then tighten report export roles
  3. then add frontend route guards / hidden nav items for admin pages
