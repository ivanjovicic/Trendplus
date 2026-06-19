# Demo Verification Smoke Result

Date: 2026-06-19
Repo: `ivanjovicic/Trendplus`
Local HEAD: `242e4e24e885ebe7eb6d8ababc535a99551a5bfe`
Backend base URL tested: `https://trendplus-api.onrender.com`
Endpoint tested: `GET /api/admin/demo-verification`

## Source Registration Evidence

- The route is mapped in [Api/Endpoints/AdminConfigEndpoints.cs](C:/Users/Ivan/source/repos/Trendplus2/Api/Endpoints/AdminConfigEndpoints.cs#L49) and handled by `DemoVerification` in the same file.
- The handler returns `401` when the admin credential is missing and `403` when the credential is invalid.
- The response logic is read-only and only returns `demoSafe`, `reasons`, `warnings`, `environment`, and `checkedAtUtc`.
- Integration tests exist in [Api.Tests/DemoEnvironmentVerificationEndpointTests.cs](C:/Users/Ivan/source/repos/Trendplus2/Api.Tests/DemoEnvironmentVerificationEndpointTests.cs).

## Test Evidence

- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DemoEnvironmentVerification"` passed.
- Covered cases:
  - environment name contains demo -> `demoSafe=true`
  - `AnalyticsDemo:Enabled=true` -> `demoSafe=true`
  - demo database marker -> `demoSafe=true`
  - no machine proof -> `demoSafe=false`
  - raw connection string / secret redaction
  - missing admin key -> `401`
  - wrong admin key -> `403`

## Live Smoke Result

| Environment | URL | HTTP status | demoSafe | Safe to reset? | Evidence | Next action |
| ----------- | --- | ----------: | -------- | -------------- | -------- | ----------- |
| Production Render backend | `https://trendplus-api.onrender.com/api/admin/demo-verification` | `404` | not returned | No | Live endpoint is missing on the deployed backend; this matches the Render deploy drift already documented in [ANALYTICS_DEPLOY_PROOF.md](C:/Users/Ivan/source/repos/Trendplus2/docs/qa/ANALYTICS_DEPLOY_PROOF.md). | Redeploy Render from current `main` and re-run the smoke check. |

## Result

- The smoke check is **not** a PASS because the live endpoint is not deployed.
- The current production backend is **not** verified as demo-safe from the public surface.
- There is no secret exposure in the observed response because the endpoint returned `404`.

## Notes

- This doc records smoke evidence only.
- It does not add reset/delete logic.
- It does not claim demo reset readiness until the live endpoint returns `200` and a safe response can be evaluated.
