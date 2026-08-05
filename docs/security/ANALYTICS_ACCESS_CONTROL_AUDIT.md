# Analytics Access Control Audit

Datum: 2026-06-14
Updated: 2026-08-05 (STAB03 pointer)
Scope: `Api/Endpoints/*`, `Api/Program.cs`, `Klijent/clientapp/src/App.tsx`, `Klijent/clientapp/src/layout/navConfig.ts`, `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`

## 2026-08-05 runtime boundary update (STAB03)

Canonical Phase 1 decision and endpoint-family matrix:

- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`

Summary:

- Production still has **no** `AddAuthentication` / `UseAuthentication` pipeline.
- Effective admin control is **`X-Admin-Key` via `AdminAccessControl`** (authenticated `Admin` role branch is unreachable in production).
- Phase 1 pattern: **explicit internal API-key admin mode** while external auth remains disabled.
- Next code task: **STAB04** — protect admin operational read surfaces.

## Sažetak

- U `Api/Program.cs` nema `AddAuthentication()`/`UseAuthentication()` para, pa su endpointi u praksi zaštićeni samo ad-hoc proverama i/ili uopšte nisu zaštićeni.
- U frontend-u nema `ProtectedRoute` ili sličnog guard sloja; `App.tsx` definiše rute direktno, a `navConfig.ts` prikazuje admin stavke zajedno sa običnim analytics linkovima.
- Read-only analytics površine su uglavnom javne.
- Najveći P0 rizici su write/destructive endpointi: action queue, cache invalidate, manual refresh/repair, worker control, import/access import, admin configuration, export/report i log clear.

## Audit tabela

| Area | Endpoint/UI | Current access | Required role | Gap | Priority |
|---|---|---|---|---|---|
| Frontend route guards/navigation | `Klijent/clientapp/src/App.tsx`, `Klijent/clientapp/src/layout/navConfig.ts`, `Klijent/clientapp/src/pages/ConfigurationPage.tsx` | Nema protected route sloja; admin linkovi su vidljivi u istoj navigaciji kao i obični analytics linkovi | Viewer za read rute, Admin za admin površine | P0: nema route-level guard-a niti segregacije admin navigacije | P0 |
| Analytics read dashboard | `/analytics`, `/api/analytics/cached/dashboard/bootstrap`, `/api/analytics/dashboard/advanced`, `/api/analytics/refresh-status` | Javne rute; nema globalnog auth middleware-a | Viewer | P1: read-only dashboard je otvoren bez role gate-a | P1 |
| Product decision center | `/analytics/products`, `/api/analytics/cached/products/decision-center`, `/api/analytics/products/decision-center` | Javne rute; direktno dostupne iz `App.tsx` | Analyst | P1: prikaz odluka je otvoren, ali write akcije se moraju odvojeno zaštititi | P1 |
| Supplier scorecard | `/analytics/supplier`, `/analytics/supplier/report`, `/api/analytics/advanced/supplier-scorecard`, `/api/analytics/suppliers/decision-hub/*` | Javne rute; bez frontend guard-a | Viewer | P1: supplier signal i report su dostupni bez role razdvajanja | P1 |
| Inventory analytics | `/analytics/inventory`, `/api/analytics/inventory/*`, `/api/analytics/cached/inventory/*` | Javne rute; direktno dostupne iz navigacije | Analyst | P1: inventory insights i export surface su otvoreni | P1 |
| Data quality | `/analytics/data-quality`, `/api/analytics/data-quality/*` | Javne rute | Viewer | P1: read-only signal je otvoren, ali je i dalje bez role gate-a | P1 |
| Refresh status | `AnalyticsTrustHeader`, `WorkersPanel`, `/api/analytics/refresh-status` | Javna read-only ruta | Viewer | P1: freshness signal je izložen bez kontrole pristupa | P1 |
| Reports / export | `/api/analytics/reports/*`, `/api/exports`, `/api/documents`, `/api/analytics/inventory/export` | Route je direktno dostupan; document servis radi ownership filtriranje, ali nema globalni auth sloj | Manager | P0: export može sadržati klijentske podatke i treba eksplicitan role gate | P0 |
| Action queue create | `/api/analytics/actions` (POST) | Backend now requires admin authorization or the `X-Admin-Key` compatibility path | Analyst | P0: protection exists, but the app still lacks a shared RBAC middleware | P0 |
| Action queue update/status/outcome | `/api/analytics/actions/{id}/status`, `/api/analytics/actions/{id}/outcome` | Backend now requires admin authorization or the `X-Admin-Key` compatibility path | Manager | P0: protection exists, but the app still lacks a shared RBAC middleware | P0 |
| Manual analytics refresh | `/api/analytics/optimize`, `/api/admin/run-analytics-optimization`, `/api/admin/sync-analytics-db`, `/api/admin/init-scoring-tables` | Neke rute su potpuno javne; ostale su bez jedinstvenog role modela | Admin | P0: refresh/repair akcije mogu menjati analytics state bez konzistentne zaštite | P0 |
| Clear analytics cache | `WorkersPanel` dugme `Očisti analytics cache`, `/api/analytics/cached/cache/invalidate` | Backend now requires admin authorization or `X-Admin-Key`; read-only analytics stays public | Admin | Implemented: cache invalidation is no longer public | P0 |
| Worker control | `/api/workers/control`, `/api/workers/control/enable`, `/api/workers/control/disable`, `/api/workers/configuration`, `/api/workers/{workerName}/start|stop|restart|schedule/*` | Read rute su javne; write rute koriste `X-Admin-Key` u prod-u, dok je dev otvoren | Admin | P0: runtime kontrola zavisi od shared key-a, ne od role auth-a | P0 |
| Import / access import | `/access-import`, `/api/access-import/*`, `/api/access-import/cleanup/execute`, `/api/access-import/cleanup/archive/export`, `/api/access-import/batches/{id}` delete/cancel | Status/read rute su javne; destruktivne rute koriste `X-Admin-Key` u prod-u, dev je otvoren | Admin | P0: import i cleanup menjaju core podatke i treba jaču zaštitu | P0 |
| Admin configuration | `/admin/configuration`, `/api/admin/backend-routing`, `/api/redis/toggle`, `/api/admin/pending-batches`, `/api/admin/audit-log` | Frontend ruta je javna; backend je mešavina javnih i admin-key ruku | Admin | P0: backend routing, Redis toggle i admin dijagnostika su previše izloženi | P0 |
| Performance | `/performance`, `/api/performance` | Javna read-only površina | Viewer ili Analyst | P1: interni performance signal je otvoren, ali nije destruktivan | P1 |
| Logs | `/logs`, `/api/logs`, `/api/logs/{id}`, `/api/logs/clear` | Read rute su javne; clear ruta koristi `X-Admin-Key` u prod-u | Manager za read, Admin za clear | P0: logovi često nose internu/PII informaciju i ne bi trebalo da budu javni | P0 |

## P0 gapovi

- Nema zajedničkog auth middleware-a; zaštita je fragmentisana po handlerima.
- Nema frontend protected route sloja, pa su admin rute direktno dostupne i vidljive u navigaciji.
- `POST /api/analytics/actions` i `PATCH /api/analytics/actions/*` su admin-gated write endpointi, ali zaštita je i dalje endpoint-level umesto shared RBAC middleware-a.
- Cache invalidate, manual refresh/repair, admin routing, Redis toggle, worker control i import/cleanup moraju imati eksplicitnu admin zaštitu.
- Export/report i logs surface treba da budu ograničeni na Manager/Admin.

## Zaključak

Za pilot spremnost je prioritet da se prvo zatvore write/destructive rute, pa tek onda da se uvede konzistentan Viewer/Analyst/Manager/Admin model za read površine.
## Implemented Status

- `POST /api/analytics/cached/cache/invalidate` is now protected in the backend by admin authorization or the `X-Admin-Key` compatibility path.
- `POST /api/analytics/actions` and `PATCH /api/analytics/actions/*` now require admin authorization or the `X-Admin-Key` compatibility path.
- Missing credential returns `401`; present but insufficient credential returns `403`.
