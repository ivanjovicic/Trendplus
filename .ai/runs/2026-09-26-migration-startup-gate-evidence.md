Task ID: DIRECT-MIGRATION-STARTUP-GATE-20260926
Queue: direct-user-request
Date: 2026-09-26
Agent/tool: Cursor
Delivery target: main
Working branch / PR: main
Main commit SHA: 067cd68b6d88ee6e5e24f6e9e2046f675a3ed85e
Main verification: passed - `origin/main` contains 067cd68b6d88ee6e5e24f6e9e2046f675a3ed85e
Evidence state: synchronized

## What was done
- Confirmed the production cause: Render's web service did not set `Database__AutoMigrate`, so `DeferredStartupTasksHostedService` was not registered in the production web process; the worker process also does not own deferred initialization while workers are enabled.
- Configured the Render web service as the single migration owner with `Database__AutoMigrate=true`, `DatabaseInitialization__FailFast=true`, explicit `StartupTasks__RunDatabaseInitialization=true` and `/ready` as the health check.
- Added a readiness gate so the service cannot become ready before required database initialization completes successfully.

## Files changed
- render.yaml
- Api/Program.cs
- Api/Services/Startup/DeferredStartupTasksHostedService.cs
- Api/Services/Startup/StartupReadinessState.cs
- Api.Tests/ProductionMigrationStartupContractTests.cs
- .ai/runs/2026-09-26-migration-startup-gate-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ProductionMigrationStartupContractTests|FullyQualifiedName~StartupReadinessStateTests" --verbosity minimal` -> pass (6 passed, 0 failed, 0 skipped)
- `git diff --check` on changed migration/startup files -> pass

## Validation not run
- Live Render redeploy and production migration application -> not run; this workspace has no production deployment permission/secret and must not mutate the production database directly.
- Full backend suite -> not run; focused startup contract covered the changed behavior.

## Documentation impact
- Render configuration now documents the migration owner and fail-fast/readiness contract inline.

## What was missed
- The actual Render deploy and applied migration history still require an owner-triggered redeploy and provider-log verification.

## Risks
- The deployment will fail closed if the configured database credentials, migration history or migration SQL are invalid; this is intentional and prevents serving against a drifted schema.

## Next
- Redeploy `trendplus-api` from `main`, verify `/ready` transitions from 503 to 200 only after migration completion, then verify `/api/runtime/version`, Supplier and Shoe Type endpoints.
