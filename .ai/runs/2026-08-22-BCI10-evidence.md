Task ID: BCI10
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-08-22
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: pending
Main verification: not run - commit/push pending
Evidence state: pending

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
- `git diff --check` -> not run yet after the final doc updates
- GitHub Actions run inspection for the final pushed SHA -> not run yet until commit/push is complete

## Documentation impact
- Backend CI queue and addendum will be updated to mark `BCI10` DONE.
- MASTER_ROADMAP will be synchronized to show the backend gate is closed again.

## What was missed
- Exact pushed `main` SHA verification is still pending.

## Risks
- Release gate is green locally, but it still needs to be synchronized to the pushed `main` SHA before the queue can be closed with full delivery truth.

## Next
- Commit, push, and then update this run log with the delivered `main` SHA and verification result.
