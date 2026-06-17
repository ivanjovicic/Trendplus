# Render Analytics Deploy Triage

Date: 2026-06-17

## Summary

- `trendplus-api` on Render is reachable and healthy on core health endpoints.
- Production Render currently returns `404` for analytics routes that exist in repository `main`.
- This points to a stale or partially outdated Render deployment, not a current `main` code absence.
- No public deployment/version endpoint is currently available, so exact deployed SHA cannot be proven from production responses alone.

## Files Reviewed

- `render.yaml`
- `Dockerfile`
- `.github/workflows/deploy-render-manual.yml`
- `.github/workflows/analytics-quality-gates.yml`
- `Api/Program.cs`
- `Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `docs/DEPLOY_TO_FLY.md`

## Render Configuration Findings

### Branch and runtime

- Render service `trendplus-api` is configured from `main` in `render.yaml`.
- Runtime is native Render `dotnet`, not Docker.
- Build root is correct: `dotnet publish Api/Api.csproj -c Release -o out`
- Start command is correct: `dotnet out/Api.dll`

### Deployment path

- Repository contains a manual fallback workflow: `.github/workflows/deploy-render-manual.yml`
- Repository docs in `docs/DEPLOY_TO_FLY.md` describe Render as a manual fallback deployment, not the primary continuously deployed backend.
- This makes Render drift/staleness plausible even though `render.yaml` shows `autoDeploy: true`.

## Local Main vs Production Route Check

Current repository `main` at review time:

- `main` SHA: `783adbc`

Routes present in `main`:

- `Program.cs` maps:
  - `app.MapAnalyticsRefreshStatusEndpoints();`
  - `app.MapCachedAnalyticsEndpoints();`
  - `app.MapAnalyticsActionsEndpoints();`
- `Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs` exposes:
  - `GET /api/analytics/refresh-status`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` exposes:
  - `GET /api/analytics/cached/products/decision-center`
  - `GET /api/analytics/cached/dashboard/bootstrap`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs` exposes:
  - `GET /api/analytics/actions`

Relevant route introduction history:

- `c7b81d0` introduced `/api/analytics/refresh-status`
- `fba9486` introduced `/api/analytics/cached/products/decision-center`
- `412d6f7` introduced `/api/analytics/actions`

## Production Smoke Results

Checked against:

- `https://trendplus-api.onrender.com/health`
- `https://trendplus-api.onrender.com/ready`
- `https://trendplus-api.onrender.com/health/dependencies`
- `https://trendplus-api.onrender.com/api/analytics/refresh-status`
- `https://trendplus-api.onrender.com/api/analytics/cached/dashboard/bootstrap`
- `https://trendplus-api.onrender.com/api/analytics/cached/products/decision-center`
- `https://trendplus-api.onrender.com/api/analytics/actions`

Observed on 2026-06-17:

| URL | Status | Finding |
| --- | --- | --- |
| `/health` | `200` | Service is alive on Render. |
| `/ready` | `200` | App startup completed; this is not a cold-start-only failure. |
| `/health/dependencies` | `200` | Both default and analytics DB checks are healthy. |
| `/api/analytics/refresh-status` | `404` | Route missing on deployed backend. |
| `/api/analytics/cached/dashboard/bootstrap` | `200` | Older cached analytics family is present. |
| `/api/analytics/cached/products/decision-center` | `404` | Route missing on deployed backend. |
| `/api/analytics/actions` | `404` | Route missing on deployed backend. |

Important note:

- During early warm-up, `/api/analytics/refresh-status` briefly returned startup `503` because API traffic was gated.
- After `/ready` became healthy, the same endpoint returned `404`.
- That confirms the steady-state production issue is missing route registration in the deployed artifact, not startup warm-up.

## Swagger Footprint Check

Production Swagger endpoint:

- `https://trendplus-api.onrender.com/swagger/v1/swagger.json`

Observed:

- Present in production Swagger:
  - `/api/analytics/cached/dashboard/bootstrap`
- Missing from production Swagger:
  - `/api/analytics/refresh-status`
  - `/api/analytics/cached/products/decision-center`
  - `/api/analytics/actions`

This matches the live `404` behavior and reinforces that Render is serving an older backend surface than repository `main`.

## Deployment Identity Gap

No existing public deployment/version endpoint was found for:

- commit SHA
- build time
- deployed environment identity beyond health provider name

No existing code pattern was found for `AssemblyInformationalVersion`, `SourceRevisionId`, or a dedicated version endpoint.

Impact:

- Exact deployed SHA on Render cannot currently be confirmed from production alone.
- Route footprint comparison is the strongest available evidence.

Recommended future hardening:

- Add a small non-sensitive deployment identity endpoint such as `/api/system/version` with:
  - commit SHA
  - build time
  - environment

## Conclusion

Most likely status:

- Render backend is stale relative to repository `main`.

Why this is the most likely conclusion:

- `render.yaml` points to the correct project and branch.
- Repository `main` already contains all missing endpoint mappings.
- Production health and DB checks are green, so this is not a generic startup or database outage.
- Production serves one older analytics cached route but not the newer analytics route families.

This does **not** look like:

- a data-quality/no-data issue
- an analytics algorithm failure
- a database connectivity issue
- a wrong project root in `render.yaml`

## Redeploy Runbook

1. Confirm target ref is merged to `main`.
2. Trigger a Render redeploy for `trendplus-api`.
   - Preferred: Render dashboard manual deploy of the linked `main` branch.
   - Fallback: GitHub Actions workflow `Manual: Deploy to Render (fallback)`.
3. Wait until:
   - `/health` returns `200`
   - `/ready` returns `200`
   - `/health/dependencies` returns `200`
4. Recheck:
   - `/api/analytics/refresh-status`
   - `/api/analytics/cached/products/decision-center`
   - `/api/analytics/actions`
5. If any still return `404`, inspect the actual Render service settings:
   - linked repo/branch
   - service blueprint sync state
   - whether the service was created from an older snapshot and not updated from current repo config

## Caveat About Manual Workflow

`.github/workflows/deploy-render-manual.yml` accepts a `ref` input, but the workflow currently triggers a service deploy through Render API without passing a git ref in the request.

Operationally this means:

- treat the workflow as "redeploy the service's linked branch state"
- do not rely on it to deploy an arbitrary SHA/ref unless the Render service itself is already pointed at that branch/ref

## Recommended Next Step

- Redeploy Render from current `main` and immediately rerun the three analytics route probes.
