# Analytics Deploy Proof

Date: 2026-06-19
Repo: `ivanjovicic/Trendplus`
Frontend base: `https://trendplus.vercel.app`
Backend base: `https://trendplus-api.onrender.com`

## Snapshot

- Local workspace `HEAD`: `242e4e24e885ebe7eb6d8ababc535a99551a5bfe`
- Remote `origin/main`: `4253ec2fc99bb4c2b1fe65291de708145ef66ea1`
- Vercel is still serving `index-XONGNubS.js` for `/analytics/pilot-readiness`
- Vercel response headers still show `Last-Modified: Wed, 17 Jun 2026 08:14:08 GMT`
- Current workspace `HEAD` is ahead of `origin/main`, so the live Vercel site is not serving the current branch tip
- Render still returns `404` for `GET /api/runtime/version`

## 2026-08-05 Current Live Recheck

- Local workspace `HEAD`: `2ab36cb376d236cdde98fbb4d31e5316bf9cdd4d`
- Remote `origin/main`: `2ab36cb376d236cdde98fbb4d31e5316bf9cdd4d`
- Vercel now serves `/assets/index-Aftw1akq.js`
- Headless Chrome renders the real pilot readiness checklist at `/analytics/pilot-readiness`
- Headless Chrome renders the real pilot intake report at `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`
- Render now exposes `GET /api/runtime/version` with `commitSha=e9f3238a172fe61ade3844777d8576dade270dae`
- The current public production surfaces match the route definitions in source, so the deploy drift blocker is resolved

## Interpretation

- The frontend failure is deploy drift, not a missing route definition in source.
- The repo already maps `/analytics/pilot-readiness` and `/analytics/reports/pilot-intake`.
- `vercel.json` already points builds at `Klijent/clientapp` and rewrites non-asset routes to `index.html`.
- The current workspace tip differs from the live Vercel bundle, so the public site is serving an older deploy.
- The backend failure is also deploy drift: the live surface does not yet expose the runtime version endpoint.
- A later live recheck now shows both the frontend routes and the runtime version endpoint are live again.

## Surface Table

| Surface | Expected | Observed | Status | Next action |
|---|---|---|---|---|
| `Vercel /analytics/pilot-readiness` | Real pilot readiness checklist from the latest `main` deployment | Generic SPA shell, `index-XONGNubS.js`, `X-Vercel-Cache: HIT`, stale `Last-Modified` header | FAIL | Trigger a fresh Vercel deploy from current `main` and verify the route renders the checklist. |
| `Vercel /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | Real pilot intake report route from the latest `main` deployment | Generic SPA shell, `index-XONGNubS.js`, `X-Vercel-Cache: HIT`, stale `Last-Modified` header | FAIL | Trigger a fresh Vercel deploy from current `main` and verify the report route renders. |
| `Render /api/runtime/version` | `200` with `service`, `environment`, `commitSha`, `buildTimeUtc`, `processType`, `provider` | `404 Not Found` | FAIL | Deploy the latest backend to Render; keep this as deploy drift until the version endpoint is live. |

## Current Production Outcome

| Surface | Status | Evidence |
|---|---|---|
| `Vercel /analytics/pilot-readiness` | PASS | Headless Chrome shows the real readiness checklist, including `Pilot spremnost`, `Pilot readiness checklist`, and the readiness cards. |
| `Vercel /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | PASS | Headless Chrome shows the real pilot intake report, including `Pilot izveštaj kvaliteta podataka` and the empty-state messaging. |
| `Render /api/runtime/version` | PASS | Returns `200` with `service=trendplus-api`, `environment=Production`, `commitSha=e9f3238a172fe61ade3844777d8576dade270dae`, `processType=web`, `provider=render`. |

## Exact Remediation

1. Redeploy Vercel from the current `main` branch tip if the alias drifts again.
2. Verify that the alias continues serving `/assets/index-Aftw1akq.js` or a newer bundle from the same branch tip.
3. Confirm the live readiness and intake pages still render their route content in headless Chrome.
4. Keep Render on the deployed backend that returns `/api/runtime/version` with the live commit SHA.
5. If the next Vercel deploy does not move, inspect the project binding and GitHub integration logs for stale deployment handling.

## Notes

- This is a deploy proof document only.
- It does not change analytics behavior.
- It treats the current production drift as a blocker, not a success signal.
