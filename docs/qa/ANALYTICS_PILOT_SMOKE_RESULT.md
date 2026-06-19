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
- A newer 2026-06-19 recheck is documented below: Render now exposes `GET /api/runtime/version` with `commitSha=e9f3238a172fe61ade3844777d8576dade270dae`, but Vercel still serves the older SPA shell bundle.
- A later 2026-06-19 redeploy proof is documented in `docs/qa/VERCEL_FRONTEND_REDEPLOY_PROOF.md`; it shows the required analytics routes rendering real content on `/assets/index-BxfHyN7W.js`.
- The full live smoke pass after the redeploy is documented in `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`.

## 2026-06-19 Live Smoke Recheck

### Current status

- Local `HEAD` is `930b724e76b84fad2b021c94ffca37caf77b3719`.
- Remote `origin/main` is `e9f3238a172fe61ade3844777d8576dade270dae`.
- Local `HEAD` is ahead of `origin/main` by 2 commits.
- Render now returns `200 OK` for `GET /api/runtime/version` and reports:
  - `service = trendplus-api`
  - `environment = Production`
  - `commitSha = e9f3238a172fe61ade3844777d8576dade270dae`
  - `buildTimeUtc = 2026-06-19T10:46:43.6241738Z`
  - `processType = web`
  - `provider = render`
- `GET /api/admin/demo-verification` returns `401 Unauthorized` without an admin credential, which is expected for the protected admin route.
- `GET /api/analytics/refresh-status?dataScope=all` returns `200 OK` with honest `dataFreshnessStatus = unknown`.
- `GET /api/analytics/actions?dataScope=all` returns `200 OK` with action data.
- Vercel still serves the older bundle and generic SPA shell for the required routes:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake`
  - `/analytics/decision-board`
- The Vercel HTML still points at `/assets/index-DelBmZl0.js` with the older `Last-Modified` timestamp from `2026-06-19 10:21:26 GMT`.

### Recheck result

- Backend deploy proof is improved because Render now exposes the runtime version endpoint.
- Frontend deploy drift remains unresolved because the required analytics routes still do not render their intended content on Vercel.
- This is a partial smoke recheck, not a full production sign-off.

## 2026-06-19 Current Deploy Proof Check

### Before status

- Local `HEAD` is `242e4e24e885ebe7eb6d8ababc535a99551a5bfe`.
- Remote `origin/main` is `4253ec2fc99bb4c2b1fe65291de708145ef66ea1`.
- The live Vercel HTML for `/analytics/pilot-readiness` still serves `index-XONGNubS.js` with `Last-Modified: Wed, 17 Jun 2026 08:14:08 GMT`.
- The current workspace tip is ahead of `origin/main`, so the live Vercel site is not serving the bundle produced from the current workspace state.
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

- Local HEAD: `242e4e24e885ebe7eb6d8ababc535a99551a5bfe`
- Remote `origin/main`: `4253ec2fc99bb4c2b1fe65291de708145ef66ea1`
- Vercel deployed SHA: not publicly visible from the live surface
- Render deployed SHA: not publicly visible from the live surface

### Remaining warnings

- Vercel is still serving an older bundle than the current workspace tip, so the production analytics shell does not match the current route registration.
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

- Current local workspace HEAD is `242e4e24e885ebe7eb6d8ababc535a99551a5bfe` and current `origin/main` is `4253ec2fc99bb4c2b1fe65291de708145ef66ea1`.
- Current public Vercel HTML for `/analytics/pilot-readiness` still serves `index-XONGNubS.js`.
- That mismatch confirms the live Vercel site is still serving an older deployment, not the current workspace tip that contains the pilot routes.
- The same old bundle remained live after the previous deploy and an additional wait, so this is not just a short CDN propagation delay.
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
- The latest recheck shows the backend moved forward, but the frontend alias still points to the older bundle, so the remaining blocker is a stale Vercel deployment rather than a route-definition regression.

## Recommendation

Do not use the current Vercel deployment as the final pilot smoke sign-off until `/analytics/pilot-readiness` and `/analytics/reports/pilot-intake` render the expected route content again.
Next verification step: confirm that the next Vercel deploy is triggered from `origin/main` and that the production alias moves to a bundle that includes the pilot routes.

## 2026-06-19 Redeploy Success

### Current status

- Local `HEAD` and `origin/main` both point at `e2c2901c8589be4f5cbf9c066b6f5fc74ddd3288`.
- `git push origin main` completed successfully and triggered the Vercel deployment.
- Vercel now serves `/assets/index-BxfHyN7W.js` for the analytics shell.
- The live routes now render the intended UI content:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`
  - `/analytics/decision-board`

### Route render evidence

- `/analytics/pilot-readiness`
  - `Pilot spremnost`
  - `PILOT READINESS CHECKLIST`
  - `Spremnost nije potvrđena`
- `/analytics/reports/pilot-intake`
  - `Pilot izveštaj kvaliteta podataka`
  - `Status pilota`
  - `Skor spremnosti podataka: 77/100`
- `/analytics/decision-board`
  - `Izvršni board odluka`
  - `URGENTNE ODLUKE`
  - `Top 5 urgentnih odluka`

### Verdict

- The required production analytics routes are now live and rendering their real content.
- The frontend deploy drift is resolved for this smoke check.
- The live smoke sign-off is PASS for the required routes.

## 2026-06-19 Post-Redeploy Watch

- A later recheck after the redeploy soak window still shows the same live bundle hash: `/assets/index-BxfHyN7W.js`.
- The required routes continue to render real content:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake`
  - `/analytics/decision-board`
- This confirms the redeploy stayed live through the watch window and did not regress back to the generic shell.

## 2026-06-19 Vercel Frontend Redeploy Proof

### Current status

- Local `HEAD` is `9851c8c08beb8c9dae558e61f3b6b61a4bbef236`.
- Remote `origin/main` is `e2c2901c8589be4f5cbf9c066b6f5fc74ddd3288`.
- Local `HEAD` is ahead of `origin/main` by 2 commits.
- The live Vercel HTML now serves `/assets/index-BxfHyN7W.js` with `Last-Modified: Fri, 19 Jun 2026 10:59:03 GMT`.
- The required routes render real content again:
  - `/analytics/pilot-readiness`
  - `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`
  - `/analytics/decision-board`

### Recheck result

- The frontend redeploy proof is PASS for the required analytics routes.
- The live Vercel alias is no longer stuck on the generic SPA shell.
- The remaining caveat is operational, not functional: the current workspace tip is still ahead of `origin/main` by two docs-only commits, so push those before treating the local tip as fully reflected in production.
