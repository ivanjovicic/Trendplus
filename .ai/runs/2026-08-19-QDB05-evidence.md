Task ID: QDB05
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-19
Agent/tool: Codex / shell
Model: GPT-5
Delivery target: main
Main commit SHA: 9b1ac68d212a487b6c6694eb74bcb61e8ac60c0b
Main verification: git rev-parse HEAD -> 9b1ac68d212a487b6c6694eb74bcb61e8ac60c0b

## What was done
- Added deterministic source-mapping preview request/response models.
- Added a SQL Server-backed admin mapping-preview endpoint with explicit source table, external keys, cursor selection, deterministic alias resolution, schema fingerprinting, field-level validation and bounded preview rows.
- Added unit tests for preview projection, rejection reasons and fingerprint stability.
- Added integration tests for auth gating and bounded preview behavior through the admin endpoint.
- Added a contract doc for the mapping-preview behavior and updated the prompt queue status to DONE.

## Files changed
- Api/Models/SourceMappingPreviewModels.cs
- Api/Services/DataSources/SourceMappingPreviewService.cs
- Api/Endpoints/AdminDataSourceEndpoints.cs
- Api.Tests/SourceMappingPreviewServiceTests.cs
- Api.Tests/AdminDataSourceEndpointsTests.cs
- docs/architecture/DATA_SOURCE_MAPPING_PREVIEW_CONTRACT.md
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md

## Validation run
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceMappingPreviewServiceTests -> pass
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AdminDataSourceEndpointsTests.MappingPreview -> pass
- git diff --check -> pass
- node scripts/check-agent-instructions.mjs --self-test -> pass
- node scripts/check-agent-instructions.mjs -> pass
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass

## Validation not run
- dotnet build - not run separately because the targeted dotnet test commands compiled the changed projects.
- dotnet test - not run separately because the focused test commands covered the changed surface.
- npm run check:analytics-guardrails - not applicable to this backend/data-source prompt.
- npm run build - not applicable to this backend/data-source prompt.

## What was missed
- QDB06 remains WAITING until the owner approves the database migration.

## Risks
- The preview path currently supports the SQL Server proof connector path only.
- The bounded preview is capped at 25 rows, so larger source samples are intentionally truncated.

## Next
- QDB06 - Add idempotent checkpointed incremental synchronization
