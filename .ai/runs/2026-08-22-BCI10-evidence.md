Task ID: BCI10
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-08-22
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: `42f2d640fe0e11d15f87c81080018af86802ed83`
Main verification: `git rev-parse origin/main -> 42f2d640fe0e11d15f87c81080018af86802ed83`
Evidence state: done

## What was done
- Reproduced the previously red backend suite truth on the current `main` branch.
- Isolated the remaining failure family to admin config/demo verification endpoint routing, where `MapAdminConfigEndpoints()` pulled `checkpoint-sync` into the test host without a registered `SourceCheckpointSyncService` graph.
- Applied the smallest same-owner repair by registering the in-memory checkpoint-sync service stack in the affected test hosts.
- Re-ran the targeted admin/demo tests and the full `Api.Tests` Release suite to prove the backend gate is green again.

## Files changed
- Api.Tests/AdminConfigOperationalReadsAuthorizationTests.cs
- Api.Tests/DemoEnvironmentVerificationEndpointTests.cs
- docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdminConfigOperationalReadsAuthorizationTests"` -> pass
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~DemoEnvironmentVerificationEndpointTests"` -> pass
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release` -> pass (`1016 passed / 0 failed`)

## Validation not run
- GitHub Actions run inspection for the code-fix push -> queued at record time; completion still pending

## Documentation impact
- Backend CI queue and addendum are updated to mark `BCI10` DONE.
- MASTER_ROADMAP is synchronized to show the backend gate is closed again.

## What was missed
- The queued GitHub Actions run for `42f2d640fe0e11d15f87c81080018af86802ed83` had not completed at record time.

## Risks
- Release gate is green locally, but the corresponding GitHub Actions workflow was still queued when this record was written.

## Next
- Recheck the queued workflow if you need remote CI completion before declaring the backend gate fully green.
