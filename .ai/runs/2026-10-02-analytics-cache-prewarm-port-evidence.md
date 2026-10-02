Task ID: analytics-cache-prewarm-port
Queue: direct-user-request
Date: 2026-10-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: `17875d5a83eea9d49ce5faf52735b393e2fa34c2`
Main verification: pass - fresh `git fetch origin main` resolved `origin/main` to `17875d5a83eea9d49ce5faf52735b393e2fa34c2`; `git merge-base --is-ancestor` confirms the fix commit is contained in current `origin/main`.
Evidence state: synchronized

## What was done

- Traced the startup warning to the best-effort analytics prewarm probe. `Program.cs` binds Kestrel to `PORT` when present, while the prewarm resolver previously preferred `ASPNETCORE_URLS`; conflicting values could make its loopback health probe target a port with no listener and skip all cache warming.
- Changed prewarm URI resolution to honor explicit `AnalyticsPrewarm:BaseUrl`, then the actual Kestrel `PORT`, then `ASPNETCORE_URLS` and the default.
- Added a regression test for the conflicting `PORT=10000` and `ASPNETCORE_URLS=:8080` case.

## Files changed

- `Api/Services/Startup/AnalyticsCachePrewarmHostedService.cs`
- `Api.Tests/AnalyticsCachePrewarmHostedServiceTests.cs`
- `.ai/runs/2026-10-02-analytics-cache-prewarm-port-evidence.md`

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~AnalyticsCachePrewarmHostedServiceTests --logger "console;verbosity=minimal"` -> pass, 4 passed / 0 failed / 0 skipped.
- `git diff --check` -> pass.
- `gh run list --commit 17875d5a --limit 5 --json databaseId,name,status,conclusion,headSha,createdAt,url` -> no matching current-main Actions runs were discoverable at inspection time.

## Validation not run

- Full backend suite -> not run; the targeted unit suite covers probe retry behavior and local listener port resolution.
- Deployed startup -> not run; the log did not include the runtime `PORT` value and no production restart/probe was performed.

## Documentation impact

- No product docs changed; the source comment explains why `PORT` must precede `ASPNETCORE_URLS` for the prewarm probe.

## What was missed

- The captured warning proves connection refusal at `127.0.0.1:8080`, but does not include the runtime `PORT` or listener log; a port mismatch is a confirmed code-path risk, not confirmed runtime configuration.

## Risks

- If the service is not listening yet for a different startup reason, aligning the probe port will not eliminate the warning; it remains a best-effort skip and does not itself fail API startup.

## Next

- None for the code change. On a runtime that still logs connection refusal, compare `Configured to listen on port` with the prewarm `BaseUrl` and inspect startup readiness/listener errors.
