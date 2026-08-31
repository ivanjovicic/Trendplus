Task ID: mapping-preview-storage-followup
Queue: direct-user-request
Date: 2026-08-31
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: 80a2888bdadcab2e53272b8b65750cad521f38d6
Main verification: passed - origin/main contains 80a2888bdadcab2e53272b8b65750cad521f38d6
Evidence state: synchronized

## What was done
- Added an isolated in-process regression test that inspects both public and admin mapping-preview endpoint metadata.
- The test requires the endpoint names `PreviewDataSourceMapping` and `PreviewAdminDataSourceMapping` to remain distinct, preventing a repeat of the `/ready` routing collision.
- Added an operational PostgreSQL storage-limit runbook for the observed Npgsql SQLSTATE `53100` / 512 MB provider limit.
- Documented the separation between the routing incident and the external provider capacity incident, including safe triage, read-only measurement and recovery evidence.

## Files changed
- Api.Tests/EndpointNameUniquenessTests.cs
- docs/ops/POSTGRES_STORAGE_LIMIT_RUNBOOK.md

## Validation run
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> pass (0 errors; existing warnings remain)
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EndpointNameUniquenessTests.MappingPreviewEndpointNames_AreUniqueAcrossPublicAndAdminRoutes"` -> pass (1 test)
- `git diff --check` -> pass

## Validation not run
- Full `dotnet test` -> not run; this follow-up has a focused metadata test and does not change runtime business behavior.
- Frontend checks -> not run; no frontend files changed.
- Live Neon/provider verification -> not run; provider console/database access and any capacity change require operational owner approval.

## Documentation impact
- Added `docs/ops/POSTGRES_STORAGE_LIMIT_RUNBOOK.md` as the durable operational guidance for the observed provider-capacity failure.

## What was missed
- The provider storage limit itself was not changed from the repository; it must be resolved in the provider console or through an approved retention/archive action.
- The runbook does not claim that deleting records is safe; retention, backup and owner approval remain required.

## Risks
- Existing API and test-project analyzer/package warnings remain outside this focused change.
- A live provider outage can still prevent diagnostic persistence until storage capacity is restored.

## Next
- Restore provider capacity and complete the post-recovery live endpoint checks when the authorized operational access is available.
