# Render Backend Version Triage

Date: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Service: `trendplus-api`

## Summary

Render production backend is not showing the old analytics-route 404 behavior anymore.

Confirmed live on production:
- `GET /health` -> `200`
- `GET /ready` -> `200`
- `GET /api/analytics/refresh-status?dataScope=all` -> `200`
- `GET /api/analytics/actions?dataScope=all` -> `200`
- `GET /api/analytics/cached/products/decision-center?...` -> `200`

That means the deployed backend is at least new enough to include the analytics endpoint mapping fixes for:
- `MapAnalyticsRefreshStatusEndpoints()`
- `MapAnalyticsActionsEndpoints()`
- `MapCachedAnalyticsEndpoints()`

What is still **not publicly provable** from the current repo + public production endpoints:
- exact Render deployed commit SHA

There is no public version endpoint exposing commit SHA, and Render dashboard/log access was not available in this task context.

## Expected Commit

- Local `HEAD`: `b69637c323aaab726cfeb976f796c95476c1dfae`
- Local branch: `main`
- Remote `origin/main`: `b69637c323aaab726cfeb976f796c95476c1dfae`

Conclusion:
- expected deploy target on Render branch `main` is `b69637c323aaab726cfeb976f796c95476c1dfae`

## Render Config

Source: [render.yaml](/Users/Ivan/source/repos/Trendplus2/render.yaml)

- Service name: `trendplus-api`
- Branch: `main`
- Auto deploy: `true`
- Runtime: native Render `.NET` runtime
- Build command: `dotnet publish Api/Api.csproj -c Release -o out`
- Start command: `dotnet out/Api.dll`
- Health check path: `/health`
- Process type env: `PROCESS_TYPE=web`
- Prewarm disable env: `AnalyticsPrewarm__Enabled=false`

## Runtime / Service Root

Render is configured to build the API directly from:
- project path: `Api/Api.csproj`

`Dockerfile` exists in repo, but Render is **not** configured to use Docker for `trendplus-api`.

Conclusion:
- intended service root is correct
- current Render config uses native dotnet publish/start, not Docker

## Backend Route Map In Code

Source: [Program.cs](/Users/Ivan/source/repos/Trendplus2/Api/Program.cs)

Confirmed mapped:
- `app.MapGet("/health", ...)`
- `app.MapGet("/ready", ...)`
- `app.MapAnalyticsRefreshStatusEndpoints()`
- `app.MapCachedAnalyticsEndpoints()`
- `app.MapAnalyticsActionsEndpoints()`

## Startup / Prewarm

Source: [AnalyticsCachePrewarmHostedService.cs](/Users/Ivan/source/repos/Trendplus2/Api/Services/Startup/AnalyticsCachePrewarmHostedService.cs)

Current code has startup-safe prewarm behavior:
- best-effort only
- waits for `/ready` with fallback to `/health`
- skips with one concise warning if local API is not ready
- can be disabled with `AnalyticsPrewarm__Enabled=false`

Render config already sets:
- `AnalyticsPrewarm__Enabled=false`

## Production Route Smoke

Checked on 2026-06-17 against:
- `https://trendplus-api.onrender.com/health`
- `https://trendplus-api.onrender.com/ready`
- `https://trendplus-api.onrender.com/api/analytics/refresh-status?dataScope=all`
- `https://trendplus-api.onrender.com/api/analytics/actions?dataScope=all`
- `https://trendplus-api.onrender.com/api/analytics/cached/products/decision-center?fromDate=2026-05-01&toDate=2026-05-31&top=5&dataScope=all`

Results:

| URL | Status | Result |
|---|---:|---|
| `/health` | `200` | healthy, provider `render`, `ready=true` |
| `/ready` | `200` | healthy, DB probe OK, includes `startedAtUtc` and `readyAtUtc` |
| `/api/analytics/refresh-status?dataScope=all` | `200` | route exists and returns refresh payload |
| `/api/analytics/actions?dataScope=all` | `200` | route exists and returns action items |
| `/api/analytics/cached/products/decision-center?...` | `200` | route exists; response returned analytics payload with meta/error instead of `404` |

Important note:
- product decision center returned a payload with `meta.success=false` and analytics error metadata, but **not** `404`
- this is consistent with route exposure being present even if underlying data/query state is not healthy

## Deployed Commit SHA

Status: **unverified from public surface**

What we could verify:
- production backend is serving the fixed route families
- production `/ready` reports current live process startup timestamps
- Render config targets branch `main`

What we could not verify:
- exact deployed SHA from Render logs/dashboard

Reason:
- no public version endpoint in API
- no Render dashboard session/log export was available in this task context

## Is Render Stale?

Best current conclusion:
- **not stale in the old 404 sense**
- **exact SHA still unknown**

Because all previously failing route families now respond on production, Render is clearly not running the old backend that lacked those endpoint mappings.

## Manual Redeploy / Verification Steps

If dashboard access is available, verify exact commit like this:

1. Open Render service `trendplus-api`.
2. Check latest deploy entry.
3. Confirm deployed commit SHA matches expected `b69637c323aaab726cfeb976f796c95476c1dfae`.
4. Confirm branch is `main`.
5. Confirm runtime is native `.NET`, not Docker.
6. Confirm build command is `dotnet publish Api/Api.csproj -c Release -o out`.
7. Confirm start command is `dotnet out/Api.dll`.

If SHA is older than expected:

1. Open Render service `trendplus-api`.
2. Choose `Manual Deploy`.
3. Deploy latest commit from branch `main`.
4. Wait for `/health` and `/ready` to return `200`.
5. Re-run the five production smoke URLs above.

## Gaps / Next Hardening Step

The remaining observability gap is now addressed by the runtime version endpoint.

## Runtime Version Verification

`GET /api/runtime/version` now exposes a read-only payload with:

- `service`
- `environment`
- `commitSha`
- `buildTimeUtc`
- `processType`
- `provider`

Verification steps:

1. Call `GET /api/runtime/version`.
2. Confirm `service` is `trendplus-api`.
3. Confirm `provider` matches the runtime surface, typically `render` in production and `local` in developer/test hosts.
4. Confirm `commitSha` matches `RENDER_GIT_COMMIT`, `GIT_COMMIT_SHA`, or `SOURCE_VERSION` when one is present.
5. Confirm `buildTimeUtc` is populated and remains read-only.
6. Keep `/health` and `/ready` behavior unchanged.

If the commit environment variables are absent, `commitSha` falls back to `unknown`, which is still safe but less useful for deploy correlation.
