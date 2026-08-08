# Analytics Live Smoke Result

> [!WARNING]
> **HISTORICAL SNAPSHOT — deployment evidence only.**
> Snapshot date: **2026-06-19**. Do not treat the PASS verdict below as current production or pilot readiness. Use `MASTER_ROADMAP.md`, the current STAB queue, and `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS_2026-08-06.md` for newer release evidence. The body below is intentionally preserved as the June live-smoke snapshot.

Date: 2026-06-19 11:17:49 UTC  
Repo: `ivanjovicic/Trendplus`  
Frontend base: `https://trendplus.vercel.app`  
Backend base: `https://trendplus-api.onrender.com`

## Summary

- Live backend health, readiness, runtime version, refresh status, action list, and cached decision-center checks all returned successfully.
- Live frontend analytics routes rendered real content from the current Vercel bundle `index-BxfHyN7W.js`.
- `GET /api/admin/demo-verification` returned `401 Unauthorized` without credentials, which is the expected auth gate for the admin route.
- The live analytics smoke is PASS for the production surfaces that matter for the pilot.
- Vercel deployment SHA is not publicly visible from the checked live surface, so the observable proof is the current bundle hash and rendered route content.

## Environment

- Local `HEAD`: `db3731cceee5c4bb862cc050c180180e7bc84eb3`
- `origin/main`: `e2c2901c8589be4f5cbf9c066b6f5fc74ddd3288`
- Local `HEAD` is ahead of `origin/main` by 3 docs-only commits.
- Render runtime version SHA: `e9f3238a172fe61ade3844777d8576dade270dae`
- Vercel bundle observed live: `/assets/index-BxfHyN7W.js`

## Live Smoke Table

| Surface | URL/path | Expected | Observed | Status | Evidence | Next action |
| --- | --- | --- | --- | --- | --- | --- |
| Backend | `https://trendplus-api.onrender.com/health` | Healthy response | `200 OK`, `provider=render`, `ready=true` | PASS | Healthy JSON payload returned. | Recheck only if Render redeploys. |
| Backend | `https://trendplus-api.onrender.com/ready` | Ready response | `200 OK`, DB probe OK, ready metadata present | PASS | Readiness JSON returned with timestamps. | Recheck only if Render redeploys. |
| Backend | `https://trendplus-api.onrender.com/api/runtime/version` | Runtime version proof | `200 OK`, `commitSha=e9f3238a172fe61ade3844777d8576dade270dae` | PASS | Version JSON returned. | Recheck after the next backend deploy. |
| Backend | `https://trendplus-api.onrender.com/api/admin/demo-verification` | Auth gate without credentials | `401 Unauthorized` | PASS | Protected admin route blocked anonymously. | Do not document credentials; keep auth gate intact. |
| Backend | `https://trendplus-api.onrender.com/api/analytics/refresh-status?dataScope=all` | Honest freshness state | `200 OK`, `dataFreshnessStatus=unknown`, workers disabled, in-memory cache warning present | PASS | Honest metadata returned, not a fake green response. | Recheck after refresh/worker changes. |
| Backend | `https://trendplus-api.onrender.com/api/analytics/actions?dataScope=all` | Real action list | `200 OK`, action items returned | PASS | Action payload returned successfully. | Recheck after action-write changes. |
| Backend | `https://trendplus-api.onrender.com/api/analytics/cached/products/decision-center?fromDate=2026-05-19&toDate=2026-06-17&top=10&dataScope=all` | Real cached decision data | `200 OK`, summary and rows returned | PASS | Cached decision-center payload returned successfully. | Recheck after analytics cache changes. |
| Frontend | `/analytics` | Real analytics shell | `200 OK`, real dashboard content rendered | PASS | Body text shows production analytics shell, not a blank page. | Recheck on the next frontend deploy. |
| Frontend | `/analytics/pilot-readiness` | Pilot readiness checklist | `200 OK`, `Pilot spremnost` and `PILOT READINESS CHECKLIST` rendered | PASS | Current bundle `index-BxfHyN7W.js` rendered the real page. | Recheck on the next frontend deploy. |
| Frontend | `/analytics/products` | Product decisions page | `200 OK`, real recommendations and trust content rendered | PASS | Current bundle `index-BxfHyN7W.js` rendered the real page. | Recheck on the next frontend deploy. |
| Frontend | `/analytics/actions` | Action queue page | `200 OK`, real actions content rendered | PASS | Current bundle `index-BxfHyN7W.js` rendered the real page. | Recheck on the next frontend deploy. |
| Frontend | `/analytics/decision-board` | Executive decision board | `200 OK`, `Izvršni board odluka` and `URGENTNE ODLUKE` rendered | PASS | Current bundle `index-BxfHyN7W.js` rendered the real page. | Recheck on the next frontend deploy. |
| Frontend | `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | Pilot intake report | `200 OK`, report content rendered with honest warning state | PASS | Current bundle `index-BxfHyN7W.js` rendered the real report page. | Recheck on the next frontend deploy. |

## Verdict

- PASS: the production analytics pilot is usable on the checked surfaces.
- PASS: the live Vercel frontend is no longer serving only the generic stale shell for the required analytics routes.
- PASS: backend and frontend work together on the live deploy for the required smoke path.
- Caveat: the exact Vercel deployment SHA was not visible from the live surface, so the bundle hash is the observable frontend proof.
