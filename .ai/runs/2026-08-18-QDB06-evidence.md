Task ID: QDB06
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-18
Agent/tool: Cursor Auto
Delivery target: main
Working branch / PR: main
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Owner 2026-08-18 approved the QDB06 database migration and ran it before RQ96.
- Added durable checkpoint identity `ConnectionId + MappingProfileId + SourceStream` with `TenantScope = n/a_dedicated`.
- Checkpoint advances only after destination staging rows are committed for the same batch.
- Proved crash-before-commit, crash-after-destination-before-checkpoint recovery without duplicates, timestamp overlap + external-key dedup, non-colliding connection/mapping identities, schema-fingerprint drift block, and read/inserted/updated/skipped/rejected metrics.
- Destination for this slice is `SourceSyncAppliedRows` staging, not Artikli/Prodaja upsert.
- Restored `RQ96` as current execution READY and queued `RQ106` Decision Pulse as WAITING after QDB06 and RQ96.

## Files changed
- Domain/Model/SourceSyncCheckpoint.cs
- Domain/Model/SourceSyncAppliedRow.cs
- Api/Services/DataSources/SourceCheckpointSyncContracts.cs
- Api/Services/DataSources/SourceCheckpointSyncEngine.cs
- Api/Services/DataSources/InMemorySourceSyncStore.cs
- Api/Services/DataSources/EfSourceSyncStore.cs
- Api/Services/DataSources/SourceCheckpointSyncService.cs
- Api/Services/DataSources/SourceMappingProfileId.cs
- Api.Tests/SourceCheckpointSyncEngineTests.cs
- Infrastructure/Migrations/20260818120000_AddSourceSyncCheckpoints.cs
- Infrastructure/Migrations/TrendplusDbContextModelSnapshot.cs
- Infrastructure/DbContexts/TrendplusDbContext.cs
- Api/Program.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md
- docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md
- MASTER_ROADMAP.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
- docs/ai/ANALYTICS_TEST_STRATEGY.md
- docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
- .ai/runs/2026-08-18-QDB06-evidence.md

## Validation run
- git diff --check -> pass
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceCheckpointSyncEngineTests --no-restore -> pass (8/8)
- node scripts/check-agent-instructions.mjs --self-test -> pass
- node scripts/check-agent-instructions.mjs -> pass (8 canonical files)
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass (261 tasks)
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass (71 planning tasks)

## Validation not run
- full Api.Tests suite - out of QDB06 focused proof
- live SQL Server e2e through SourceCheckpointSyncEngine - later commercial gate
- npm frontend checks - no UI in scope
- EF crash-split simulation - production EF store commits rows and checkpoint in one transaction

## Documentation impact
- QDB06 closed DONE; QDB current READY is none; QDB07 remains WAITING on authorization/release gates
- RQ96 restored as current execution READY
- RQ106 Decision Pulse queued WAITING after QDB06 and RQ96
- Connector contract/roadmap Phase 5 recorded as delivered with staging-row destination bound

## What was missed
- SQL Server end-to-end import into Artikli/Prodaja through this engine
- Durable mapping-profile store (MappingProfileId is a hash of the mapping document)
- Admin connector UI (QDB07)
- Worker job that actually pulls SQL Server batches into SourceCheckpointSyncService

## Risks
- Tables exist after migration but stay unused until a worker/e2e path applies batches
- Split-commit crash proof is in-memory; EF path cannot reproduce that failure mode because it uses one transaction
- Caller headers are not tenant authority; dedicated scope is hardcoded n/a_dedicated

## Next
- RQ96 - Canonical observed daily inventory snapshot foundation
- After RQ96: RQ106 Decision Pulse
- Later QDB: SQL Server e2e through this engine, then QDB07
