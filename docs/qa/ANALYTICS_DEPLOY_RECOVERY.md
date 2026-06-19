# Analytics Deploy Recovery

Date: 2026-06-19 12:48:03 +02:00  
Repo: `ivanjovicic/Trendplus`

## Current Git State

Command results:

- `git status -sb` -> `## main...origin/main [ahead 1]`
- `git log --oneline -10`
  - `8cfdbe6 docs(qa): record demo verification smoke result`
  - `e9f3238 chore: retrigger deployment with valid git author`
  - `3015973 docs(qa): refresh deploy proof and queue status`
  - `242e4e2 docs(analytics): plan decision board backend aggregate path`
  - `d015022 test(analytics): cover product decision confidence edge cases`
  - `2eddd06 fix(analytics): keep executive board confidence honest`
  - `0301168 test(analytics): harden executive decision board route coverage`
  - `9a12455 fix(security): protect analytics action write endpoints`
  - `0747839 docs(qa): record demo verification smoke result`
  - `4253ec2 docs(ai): update analytics queue and deploy proof status`
- `git rev-parse HEAD` -> `8cfdbe6983adfde0b1d6e249f981f1b4c7b887b3`
- `git rev-parse origin/main` -> `e9f3238a172fe61ade3844777d8576dade270dae`

## What That Means

- Local `HEAD` is ahead of `origin/main` by one commit, so the current local tip is not yet pushed.
- The current local tip is a docs-only commit, but it still means the repository state on disk is ahead of the remote tracking branch.
- Production should not be treated as current with respect to local `HEAD` until that commit is pushed and redeployed.

## Observed Live State

### Render backend

- `GET https://trendplus-api.onrender.com/api/runtime/version`
  - `200 OK`
  - payload:
    - `service = trendplus-api`
    - `environment = Production`
    - `commitSha = e9f3238a172fe61ade3844777d8576dade270dae`
    - `buildTimeUtc = 2026-06-19T10:46:43.6241738Z`
    - `processType = web`
    - `provider = render`
- `GET https://trendplus-api.onrender.com/api/admin/demo-verification`
  - `401 Unauthorized` without admin credentials
- `GET https://trendplus-api.onrender.com/api/analytics/refresh-status?dataScope=all`
  - `200 OK`
  - honest payload, but `dataFreshnessStatus = unknown` and workers are not registered in the web process
- `GET https://trendplus-api.onrender.com/api/analytics/actions?dataScope=all`
  - `200 OK`
  - returns action data

### Vercel frontend

- `GET https://trendplus.vercel.app/analytics/pilot-readiness`
  - `200 OK`
  - serves the generic SPA shell
  - asset observed: `/assets/index-DelBmZl0.js`
  - `Last-Modified: Fri, 19 Jun 2026 10:21:26 GMT`
  - `X-Vercel-Cache: HIT`
- `GET https://trendplus.vercel.app/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`
  - `200 OK`
  - serves the generic SPA shell
  - same old bundle and cache headers as above
- `GET https://trendplus.vercel.app/analytics/decision-board`
  - `200 OK`
  - serves the generic SPA shell

## Failure Reason Assessment

Confirmed:

- `stale deployment` on the frontend is still present.
- `origin/main` is behind local `HEAD`, so the current local tip has not been pushed.

Not confirmed as the root issue:

- `wrong root/build/output config`
  - `vercel.json` already points at `Klijent/clientapp`, uses `cd Klijent/clientapp && npm run build`, and writes to `Klijent/clientapp/dist`.
- `wrong branch`
  - Render is configured for `branch: main` in `render.yaml`.
  - Vercel output is consistent with a `main`-tracked static deploy, not a branch mismatch.
- `missing environment variable`
  - The backend runtime version endpoint is live and returns a commit SHA.
  - The admin demo-verification endpoint exists and returns `401` when unauthenticated, which is consistent with auth gating rather than a missing route.
- `GitHub email privacy problem`
  - There is no direct evidence of this in the current local checks.

## Exact Recovery Steps

1. Push the current local `HEAD` if the docs-only commit should also be reflected on remote tracking.
2. Redeploy the Render service from `main` if the current remote deployment is not already tied to the latest pushed commit.
3. Redeploy the Vercel project from the latest pushed `main` commit so the frontend bundle advances past `index-DelBmZl0.js`.
4. Confirm the live runtime SHA after redeploy matches the intended commit.
5. Confirm the frontend route manifest now renders real analytics pages instead of the generic shell.

## Verification Commands After Redeploy

Backend:

```powershell
curl.exe -i -sS https://trendplus-api.onrender.com/api/runtime/version
curl.exe -i -sS https://trendplus-api.onrender.com/api/admin/demo-verification
curl.exe -i -sS "https://trendplus-api.onrender.com/api/analytics/refresh-status?dataScope=all"
curl.exe -i -sS "https://trendplus-api.onrender.com/api/analytics/actions?dataScope=all"
```

Frontend:

```powershell
curl.exe -i -sS https://trendplus.vercel.app/analytics/pilot-readiness
curl.exe -i -sS "https://trendplus.vercel.app/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all"
curl.exe -i -sS https://trendplus.vercel.app/analytics/decision-board
```

Expected after redeploy:

- `GET /api/runtime/version` should return `200` with the intended commit SHA.
- `GET /api/admin/demo-verification` should remain auth-gated, and when called with valid admin credentials it should return a non-secret payload.
- `GET /api/analytics/refresh-status?dataScope=all` should still return honest freshness metadata.
- `GET /api/analytics/actions?dataScope=all` should still return action data.
- `/analytics/pilot-readiness`, `/analytics/reports/pilot-intake`, and `/analytics/decision-board` should render their actual route content, not the generic SPA shell.

## Notes

- This document records recovery steps and proof targets only.
- It does not claim production is fixed.
- The live backend is closer than earlier proofs because `/api/runtime/version` now exists, but the frontend still shows a stale deploy.
