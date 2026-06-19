# Analytics Pilot Smoke Result

Date: 2026-06-18  
Repo: `ivanjovicic/Trendplus`  
Frontend base: `https://trendplus.vercel.app`  
Backend base: `https://trendplus-api.onrender.com`

## Summary

- Backend smoke checks passed for health, readiness, refresh status, action list, and cached product decision routes.
- Most frontend analytics routes rendered real content and showed honest state.
- Two required frontend routes did not render their expected content in production:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake`
- The supplier report route rendered an explicit unavailable/expired report state, which is acceptable as a warning, not a false ready state.
- `GET /api/runtime/version` returned `404` on production backend during this run, so the exact deployed SHA is still not publicly verifiable from the live surface.

## 2026-06-19 Deploy Proof Check

### Before status

- Local `HEAD` is `d6e120768c7aefdf8016a56c593ee49e2af4e182`.
- Remote `origin/main` is `9563b99b94138391bd473465478550bf2e465af6`.
- The live Vercel HTML for `/analytics/pilot-readiness` still serves `index-XONGNubS.js` with `Last-Modified: Wed, 17 Jun 2026 08:14:08 GMT`.
- The current local production build serves `index-Dl6Nn41t.js`, so the live Vercel site is not serving the bundle produced from the current workspace state.
- Render still returned `404` for `GET /api/runtime/version`, so the deployed backend SHA remained unverified from the public surface.

### After status

- `GET https://trendplus.vercel.app/analytics/pilot-readiness` still returned the generic SPA shell instead of the pilot readiness checklist.
- `GET https://trendplus.vercel.app/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` still returned the generic SPA shell instead of the pilot intake report.
- `GET https://trendplus-api.onrender.com/api/runtime/version` still returned `404`.
- The route definitions in source are present in the current workspace:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake`
  - `/api/runtime/version`
- That points to deploy drift, not a missing route definition in source.

### Deployed SHA

- Local HEAD: `d6e120768c7aefdf8016a56c593ee49e2af4e182`
- Remote `origin/main`: `9563b99b94138391bd473465478550bf2e465af6`
- Vercel deployed SHA: not publicly visible from the live surface
- Render deployed SHA: not publicly visible from the live surface

### Remaining warnings

- Vercel is still serving an older bundle than the current workspace build, so the production analytics shell does not match the current route registration.
- Render still needs the runtime version endpoint deployed before exact backend SHA can be verified publicly.
- Until the next successful deploy, these route misses should be treated as deployment drift, not as current-source route regressions.

## 2026-06-18 Deploy Proof Check

### Before status

- Earlier smoke evidence from the same day still showed:
  - `/analytics/pilot-readiness` rendered the generic shell with `No routes matched location`.
  - `/analytics/reports/pilot-intake` rendered the generic shell with `No routes matched location`.
  - `GET /api/runtime/version` returned `404`.
- Vercel response headers for the affected routes showed `Last-Modified: Wed, 17 Jun 2026 08:14:08 GMT` and `X-Vercel-Cache: HIT`, which is older than the current local `main` HEAD.

### After status

- Current local and pushed `main` HEAD is `9563b99b94138391bd473465478550bf2e465af6`.
- Current public Vercel HTML for `/analytics/pilot-readiness` serves a different bundle fingerprint than the local build:
  - production: `index-XONGNubS.js`
  - local build: `index-DAjqjWu_.js`
- That mismatch confirms the live Vercel site is still serving an older deployment, not the current local bundle that contains the pilot routes.
- The same old bundle remained live after the `9563b99` push and an additional wait, so this is not just a short CDN propagation delay.
- Render still returns `404` for `GET /api/runtime/version`, so the exact deployed backend SHA remains unverified from the public surface.

### Deployed SHA

- Local HEAD / expected deployment target: `9563b99b94138391bd473465478550bf2e465af6`
- Vercel deployed SHA: not publicly visible from the live surface
- Render deployed SHA: not publicly visible from the live surface

### Remaining warnings

- `/analytics/pilot-readiness` still needs a fresh Vercel deployment to serve the real checklist.
- `/analytics/reports/pilot-intake` still needs a fresh Vercel deployment to serve the real report route.
- `/api/runtime/version` still needs a fresh Render deployment to expose the read-only version proof.
- Until those deploys move to the current `main` HEAD, smoke results should be treated as deploy-drift warnings, not as route-definition regressions in source.

## Results

| Item | PASS / WARN / FAIL | Status code | Visible UI state | Correlation ID | Evidence / note |
|---|---|---:|---|---|---|
| `GET /health` | PASS | `200` | `{"status":"healthy","provider":"render","ready":true,...}` | `00-ed8642135996b85f635993744c4611d6-458816443cc95164-00` | Healthy Render health payload, no fake green issue. |
| `GET /ready` | PASS | `200` | `{"status":"healthy","provider":"render","ready":true,"db":{"ok":true,...},...}` | `00-2b08488baf18a13f2a526db8951bc832-2a4682a0ee023081-00` | Readiness payload shows DB probe OK and `startedAtUtc` / `readyAtUtc`. |
| `GET /api/analytics/refresh-status?dataScope=all` | PASS | `200` | Refresh payload returned; `dataFreshnessStatus":"unknown"` and no hidden stale state. | `00-4609351683c7f98c10ae06720ea2c317-01dac6ec2cfa8f4c-00` | Route exists and returns honest freshness metadata. |
| `GET /api/analytics/actions?dataScope=all` | PASS | `200` | Action list payload returned with items. | `00-898382073ed6c9b96caf8fd14bd99f99-cdeffd7596212836-00` | Route exists and returns data instead of `404` or fake empty success. |
| `GET /api/analytics/cached/products/decision-center?...` | PASS | `200` | Decision payload returned with real summary/rows/meta. | `00-88c62a6fac23439a0bc8d69402a199a4-94b39e1f2b289175-00` | Cached analytics route exists and serves data instead of `404`. |
| `/analytics` | PASS | `200` | Dashboard rendered with trust/navigation shell and analytics sections. | Not captured | Visible dashboard content; no blank screen. |
| `/analytics/pilot-readiness` | FAIL | `200` | Rendered the generic home shell / backoffice view, not the pilot readiness checklist. Browser console showed: `No routes matched location "/analytics/pilot-readiness"`. | `200` page load; shell-only API calls `GET /api/workers/health` `00-c6e7df8930e79e80aa0381f2f4de78ab-d05a93809090d48a-00`, `GET /api/trends/seasonal-images` `00-ddf44c3716d563020e82607dd0033594-bb0952381856eb05-00` | Local screenshot captured during run. |
| `/analytics/products` | PASS | `200` | Product decision center rendered real recommendations, period context, and data quality messaging. | Not captured | Honest state visible; no fake ready/green issue. |
| `/analytics/supplier` | PASS | `200` | Supplier analytics rendered with concrete sections, periods, and tabs. | Not captured | Real content visible; no blank screen. |
| `/analytics/inventory` | PASS | `200` | Inventory decision cockpit rendered with actionable sections and warnings. | Not captured | Real content visible; no fake zero/state issue. |
| `/analytics/data-quality` | PASS | `200` | Data quality page rendered explicit insufficient-data messaging. | Not captured | Honest warning state, not a fake ready state. |
| `/analytics/actions` | PASS | `200` | Action queue page rendered with real content. | Not captured | No blank screen; route loaded normally. |
| `Pilot intake report route` (`/analytics/reports/pilot-intake?...`) | FAIL | `200` | Rendered the generic home shell / backoffice view, not the pilot intake report. Browser console showed: `No routes matched location "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all"`. | `200` page load; shell-only API calls `GET /api/trends/seasonal-images` `00-9d246790a379b5af4cf185735016e9b3-018fa116c189b41c-00`, `GET /api/workers/health` `00-af079b5ddee683e881cfbac3a3ad4b6e-410f4afbb7ee3926-00` | Local screenshot captured during run. |
| `Supplier decision report route` (`/analytics/supplier/report?...`) | WARN | `200` | Report page rendered an explicit unavailable state: report unavailable / preview-expired messaging. | Not captured | Warning is acceptable because missing report data is shown honestly and not as a ready report. |

## Notes

- The two `No routes matched location` console warnings mean the production Vercel deployment is not serving the expected frontend route registration for pilot readiness and pilot intake report.
- The supplier report warning is not a fail because the UI is explicit that the report is unavailable/expired rather than showing a false green ready state.
- `GET /api/runtime/version` returned `404` during this run, so exact live backend SHA still could not be verified from the public surface.
- Production HTML and JS were both served with `Last-Modified` timestamps from `2026-06-17`, while current `origin/main` is `9563b99b94138391bd473465478550bf2e465af6` from `2026-06-18`. That means the public Vercel site is serving an older production deployment, not the current branch tip.
- The frontend build config in [vercel.json](/C:/Users/Ivan/source/repos/Trendplus2/vercel.json) points to `Klijent/clientapp` and the local build output contains the expected pilot routes, so this does not look like a repo build-root or route-manifest bug.

## Recommendation

Do not use the current Vercel deployment as the final pilot smoke sign-off until `/analytics/pilot-readiness` and `/analytics/reports/pilot-intake` render the expected route content again.
Next verification step: confirm that the next Vercel deploy is triggered from `origin/main` and that the production alias moves to a bundle that includes the pilot routes.
