# Demo Verification Smoke Result

Date: 2026-06-19 12:21:54 +02:00
Repo: `ivanjovicic/Trendplus`
Local HEAD: `e9f3238a172fe61ade3844777d8576dade270dae`
Backend base URL: `https://trendplus-api.onrender.com`
Endpoint tested: `GET /api/admin/demo-verification`

## Summary

- The source route exists in `Api/Endpoints/AdminConfigEndpoints.cs` and is registered as `GET /api/admin/demo-verification`.
- Local integration tests in `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs` cover the expected behavior:
  - environment name contains `demo` => `demoSafe=true`
  - `AnalyticsDemo:Enabled=true` => `demoSafe=true`
  - demo database marker in the connection string => `demoSafe=true`
  - no proof inputs => `demoSafe=false`
  - secret values are not exposed in the response body
- Live public access to the backend endpoint returned `401 Unauthorized` without admin credentials.
- Because the live surface is admin-gated, this run does not prove `demoSafe` on production from the public surface.

## Source Registration Evidence

- `Api/Endpoints/AdminConfigEndpoints.cs` maps `/api/admin/demo-verification` inside the `/api/admin` group.
- The handler returns `DemoEnvironmentVerificationResponse` with `demoSafe`, `reasons`, `warnings`, `environment`, and `checkedAtUtc`.
- The implementation checks:
  - environment name for `demo`
  - `AnalyticsDemo:Enabled`
  - analytics/default connection string markers
- The response path does not include raw connection strings or secrets.

## Test Evidence

- `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs` verifies the expected `demoSafe` cases.
- The same test file verifies unauthorized and forbidden access behavior.
- The same test file verifies the response body does not expose the raw connection string, password, host, or application name values.
- Targeted command run:
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DemoEnvironmentVerification"`
  - Result: pass

## Live Smoke Result

| Environment | URL | HTTP status | demoSafe | Safe to reset? | Evidence | Next action |
| --- | --- | ---: | --- | --- | --- | --- |
| Public production backend | `https://trendplus-api.onrender.com/api/admin/demo-verification` | `401` | `unknown` | `WARN` - auth required, so demo safety is not verifiable from the public surface | Public request returned `401 Unauthorized`; source registration exists; local tests confirm expected semantics and secret redaction | Re-run the smoke with a legitimate admin credential in a dedicated demo environment before any reset decision |

## Interpretation

- This run is a partial verification, not a green light for demo reset.
- The endpoint is present in source and covered by tests, but the live backend requires authorization.
- No secrets or raw connection strings were documented here.
- The current public result is safe in the narrow sense that it does not expose demo-reset controls to anonymous users, but it does not prove `demoSafe=true` for the live environment.

## Notes

- No destructive operation was added or performed.
- This document intentionally treats auth gating as a blocker for a full live verdict rather than a success signal.
