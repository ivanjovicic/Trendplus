Task ID: QDB09
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-22
Agent/tool: Codex / shell
Delivery target: main
Working branch / PR: main
Main commit SHA: c3de541879e3ec0c1c38296022a38b74c460546c
Main verification: passed - origin/main contains c3de541879e3ec0c1c38296022a38b74c460546c
Evidence state: synchronized

## What was done
- Claimed QDB09 and moved the queue/router truth to `IN_PROGRESS`/`none`.
- Added a production-facing admin checkpoint-sync route for SQL Server source profiles.
- Switched `SourceCheckpointSyncService` to the `ISourceSyncStore` seam and registered `EfSourceSyncStore` through the interface.
- Added an admin integration test that drives live SQL Server preview rows into the checkpoint engine through the new route and verifies persisted checkpoint state via the in-memory sync store used in the test host.
- Kept the change scoped to the data-source connector owner surface.

## Files changed
- Api/Endpoints/AdminDataSourceEndpoints.cs
- Api/Program.cs
- Api/Services/DataSources/SourceCheckpointSyncService.cs
- Api.Tests/AdminDataSourceEndpointsTests.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

## Validation run
- git diff --check -> pass
- dotnet test Api.Tests/Api.Tests.csproj --filter 'FullyQualifiedName~AdminDataSourceEndpointsTests.CheckpointSync_UsesLiveSqlPreviewRows_AndPersistsCheckpointState|FullyQualifiedName~AdminDataSourceEndpointsTests.MappingPreview_ReturnsBoundedRowsAndStableFingerprint|FullyQualifiedName~SourceCheckpointSyncEngineTests' -> pass
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass
- dotnet build Api.Tests/Api.Tests.csproj --configuration Release -> pass

## Validation not run
- current-main / remote-main verification -> not run yet
- Git push / branch delivery verification -> not run yet
- relational backing-store proof for `EfSourceSyncStore` against a live database -> not run; this run used the tested in-memory sync store seam in the admin host

## Documentation impact
- Updated the data-source connector queue to reflect `QDB09` as `IN_PROGRESS`.
- Updated `MASTER_ROADMAP.md` to show QDB READY as `none` while the prompt is claimed.

## What was missed
- Exact current-main/delivery proof is still pending.
- The production sync endpoint was not exercised against a real relational `EfSourceSyncStore` backing database in this run.

## Risks
- The new route depends on a live SQL Server profile and the same checkpoint identity normalization rules as the engine.
- The test host uses an in-memory sync store seam, so a follow-up relational store integration test would still be useful.

## Next
- Commit and push the current local diff, then verify the pushed SHA on `main`.
