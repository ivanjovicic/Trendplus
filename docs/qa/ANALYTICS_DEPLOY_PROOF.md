# Analytics Deploy Proof

Date: 2026-06-19
Repo: `ivanjovicic/Trendplus`
Frontend base: `https://trendplus.vercel.app`
Backend base: `https://trendplus-api.onrender.com`

## Snapshot

- Local `HEAD`: `fd90480eecb9e89ba3d29bd3deab8623635d528f`
- Remote `origin/main`: `fd90480eecb9e89ba3d29bd3deab8623635d528f`
- Vercel is still serving `index-XONGNubS.js` for `/analytics/pilot-readiness`
- Vercel response headers still show `Last-Modified: Wed, 17 Jun 2026 08:14:08 GMT`
- Current local build serves `index-Dl6Nn41t.js`
- Render still returns `404` for `GET /api/runtime/version`

## Interpretation

- The frontend failure is deploy drift, not a missing route definition in source.
- The repo already maps `/analytics/pilot-readiness` and `/analytics/reports/pilot-intake`.
- `vercel.json` already points builds at `Klijent/clientapp` and rewrites non-asset routes to `index.html`.
- The current local build fingerprint differs from the live Vercel bundle, so the public site is serving an older deploy.
- The backend failure is also deploy drift: the live surface does not yet expose the runtime version endpoint.

## Surface Table

| Surface | Expected | Observed | Status | Next action |
|---|---|---|---|---|
| `Vercel /analytics/pilot-readiness` | Real pilot readiness checklist from the latest `main` deployment | Generic SPA shell, `index-XONGNubS.js`, `X-Vercel-Cache: HIT`, stale `Last-Modified` header | FAIL | Trigger a fresh Vercel deploy from current `main` and verify the route renders the checklist. |
| `Vercel /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all` | Real pilot intake report route from the latest `main` deployment | Generic SPA shell, `index-XONGNubS.js`, `X-Vercel-Cache: HIT`, stale `Last-Modified` header | FAIL | Trigger a fresh Vercel deploy from current `main` and verify the report route renders. |
| `Render /api/runtime/version` | `200` with `service`, `environment`, `commitSha`, `buildTimeUtc`, `processType`, `provider` | `404 Not Found` | FAIL | Deploy the latest backend to Render; keep this as deploy drift until the version endpoint is live. |

## Exact Remediation

1. Redeploy Vercel from the current `main` branch tip.
2. Verify that the alias moves away from `index-XONGNubS.js` to a bundle produced by the current workspace.
3. Confirm the live HTML for `/analytics/pilot-readiness` and `/analytics/reports/pilot-intake` no longer shows the generic shell.
4. Deploy the latest Render backend so `GET /api/runtime/version` returns `200`.
5. If the next Vercel deploy does not move, inspect the project binding and GitHub integration logs for stale deployment handling. There is no evidence here that the build root or output directory is wrong.

## Notes

- This is a deploy proof document only.
- It does not change analytics behavior.
- It treats the current production drift as a blocker, not a success signal.
