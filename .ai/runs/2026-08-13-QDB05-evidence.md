Task ID: QDB05
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Cursor
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: skipped; user did not request commit or push

## What was done
- Claimed `QDB05` and added admin-only `POST /api/data-sources/{name}/mapping-preview`.
- Preview is request-scoped: explicit table/entity/key/cursor/field mapping, schema fingerprint, field rejection reasons, and a bounded sample of mapped targets only.
- Canonical aliases are suggestions only; unmapped required fields are rejected with `target_required_unmapped` and never auto-applied.
- No durable writes, no Trendplus business persistence, no mapping storage, no sync/checkpoints.
- Responses omit credentials; preview rows are not audit-logged.

## Files changed
- Api/Services/DataSources/SourceMappingPreviewService.cs
- Api/Services/DataSources/SourceMappingPreviewDtos.cs
- Api/Services/DataSources/CanonicalSourceEntities.cs
- Api/Services/DataSources/SourceSchemaFingerprint.cs
- Api/Services/DataSources/NamedSourceDiscoveryService.cs
- Api/Endpoints/DataSourceDiscoveryEndpoints.cs
- Api/Program.cs
- Api.Tests/SourceMappingPreviewTests.cs
- docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md
- docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
- docs/ai/ANALYTICS_TEST_STRATEGY.md
- docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md

## Validation run
- git diff --check -> pass
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceMappingPreviewTests --no-build --no-restore -> pass (6/6)
- node scripts/check-agent-instructions.mjs --self-test -> pass
- node scripts/check-agent-instructions.mjs -> pass
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass

## Validation not run
- full Api.Tests rebuild - failed in this dirty tree on unrelated InventoryEndpoints.cs compile errors (CS1503, CS7036); QDB05 files were not the failing sources
- full Api.Tests suite - out of QDB05 focused proof
- npm frontend checks - no UI in scope
- durable mapping persistence / checkpoint sync - explicitly out of scope and gated behind QDB06 owner migration approval

## What was missed
- Mapping is not persisted; operators must resend the explicit mapping each preview.
- QDB06 checkpointed incremental sync still needs an owner-approved database migration.
- No admin UI for mapping.

## Risks
- Preview returns a bounded mapped sample to authorized admins; rows are not stored or audit-logged.
- Schema fingerprint is name-based (provider + table + sorted columns), not a full type/nullability digest.
- QDB06 was not auto-promoted to READY.
- Current working tree cannot rebuild Api because of unrelated InventoryEndpoints changes; QDB05 tests were proven on the existing Debug binary.

## Next
- QDB06 after the owner approves a database migration
- Owner may instead promote RQ100 now that QDB exclusive work is clear
