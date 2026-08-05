# Current Main Deploy Evidence — 2026-08-05

Repo: `ivanjovicic/Trendplus`  
Task: `STAB01`  
Inspected at (UTC): `2026-08-05T10:20:00Z`  
Result: **WARN**

This document replaces older June 2026 smoke/deploy notes as the current-main release-truth snapshot. Historical documents under `docs/qa/` remain archive evidence only.

## Exact SHA under inspection

| Surface | Value |
|---|---|
| Local / `origin/main` HEAD | `a1b9231a6910ab2209b5e7d79db0f2bd42cf8a04` |
| Commit subject | `feat(analytics): honor data scope and expose recommendation limits across surfaces` |
| Author | `Ivan Jovicic <ivanjovicic1986@gmail.com>` |

Working tree at inspection time also contained uncommitted inventory reliability follow-ups (RQ58–RQ61). Local frontend checks below therefore include that dirty tree; they are not identical to a clean checkout of `a1b9231`.

## Provider / CI status for `a1b9231`

| Check | State | Evidence |
|---|---|---|
| GitHub combined commit status | `success` | Public status API for SHA `a1b9231` |
| Vercel status context | `success` — “Deployment has completed” | `https://vercel.com/ivans-projects-8c8927b4/trendplus/5uev73iaQrSLKCUNaUWc6RVaduu7` |
| GitHub Actions `Analytics Tests & Data Integrity` | `failure` | Run `30992652247` / job `92262163416` |
| Actions failure step | `Restore dependencies` failed; Build and test steps skipped | NuGet/restore infrastructure failure, not a compiled analytics assertion failure |
| Path-filter note | Analytics quality-gates workflow is path-filtered; docs-only commits may produce no frontend gate run | `.github/workflows/analytics-quality-gates.yml` |

### Vercel failure classification (current SHA)

Previous STAB01 draft evidence for SHA `66084a7` reported Vercel failure. For current `a1b9231` the Vercel status is **success**.

Root-cause class for the remaining red CI signal:

- **provider/environment / CI restore failure** (NuGet restore on Actions), not a proven frontend source/build/type error for the Vercel deploy of `a1b9231`.

## Local frontend checks (dirty working tree)

| Command | Result |
|---|---|
| `npm run check:analytics-guardrails` | PASS (encoding + guardrails + `tsc -b`) |
| `npm run build` | PASS |
| `npm run test:analytics` | FAIL — `11` files / `22` tests failed, `204` passed (`226` total) |

Local production asset after dirty-tree build: `assets/index-BPsshHSg.js`  
Live Vercel asset at smoke time: `assets/index-DNe0JCOl.js`  
Difference is expected while uncommitted local inventory work exists.

`npm ci` was not re-run in this session; dependencies were already installed and the guardrail/build path completed cleanly.

## Live smoke — backend (`https://trendplus-api.onrender.com`)

| Endpoint | HTTP | Observation | Verdict |
|---|---|---|---|
| `/health` | `200` | `status=healthy`, `provider=render`, `ready=true` | PASS |
| `/ready` | `200` | DB probe `ok=true`, distinct ready metadata | PASS |
| `/api/runtime/version` | `200` | `commitSha=e9f3238a172fe61ade3844777d8576dade270dae` | WARN — backend SHA ≠ current `main` `a1b9231` |
| `/api/analytics/refresh-status?dataScope=all` | `200` | `dataFreshnessStatus=unknown`, workers not claiming fresh | PASS — honest non-green freshness |
| `/api/admin/demo-verification` | `401` | Anonymous admin gate intact | PASS |

## Live smoke — frontend (`https://trendplus.vercel.app`)

Checked routes (all returned SPA shell `200`, `id="root"` present, HTML length `640`):

- `/analytics`
- `/analytics/products`
- `/analytics/inventory`
- `/analytics/actions`
- `/analytics/decision-board`
- `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`

| Observation | Value |
|---|---|
| `Last-Modified` | `Wed, 05 Aug 2026 10:19:42 GMT` |
| Main JS bundle | `/assets/index-DNe0JCOl.js` |
| Cache-Control on `index.html` | `no-cache, no-store, must-revalidate` |
| Browser content assertion | Not fully executed (no authenticated browser session); only HTML shell + asset registration verified |

Verdict: **WARN** — frontend deploy is live and recent, but this smoke does not prove authenticated page bodies beyond the SPA shell.

## Overall release-truth result

**WARN** for current `main` SHA `a1b9231`.

Reasons WARN (not PASS):

1. Production backend runtime SHA is `e9f3238…`, not `a1b9231`.
2. GitHub Actions analytics suite failed at NuGet restore for `a1b9231`.
3. Frontend route smoke verified SPA shell/assets only, not authenticated page content.
4. Local working tree was dirty relative to `origin/main` during guardrail/build/test runs.

Reasons not BLOCKED:

1. Vercel reports successful deployment for `a1b9231`.
2. Backend liveness/readiness are healthy and distinct.
3. Freshness metadata remains explicitly `unknown` (no fake green).
4. Anonymous admin endpoint remains `401`.

## Required next steps before claiming production PASS

1. Redeploy or verify Render/Fly backend so `/api/runtime/version.commitSha` matches the intended release SHA.
2. Re-run or fix GitHub Actions NuGet restore for `Analytics Tests & Data Integrity` on that SHA.
3. Run authenticated browser smoke for the pilot analytics routes and one durable report.
4. Re-check with a clean working tree matching the deployed SHA.

## What this document does not prove

- Full authenticated pilot UX sign-off.
- Backend and frontend SHA parity.
- That June 2026 smoke documents still describe August 2026 production.
- That local dirty-tree analytics test failures are identical to CI failures on clean `a1b9231`.
