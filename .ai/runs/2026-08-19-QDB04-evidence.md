Task ID: QDB04
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-19
Agent/tool: Codex
Model: GPT-5 via Codex
Delivery target: main
Main commit SHA: f7cc23ca0d7fe6cf4e88139ce9061621573ad892
Main verification: git rev-parse origin/main -> a9b156bc70678a313470b5a9cef43e2c06d14a5b; git rev-parse HEAD -> f7cc23ca0d7fe6cf4e88139ce9061621573ad892

## What was done
- Added admin data-source discovery endpoints for named profiles, connection tests, schemas, tables and columns.
- Kept secrets out of responses and mapped connection failures to safe categories.
- Wired the new routes into the existing admin config endpoint group.
- Added integration coverage for authorization, safe profile listing, and SQL Server discovery behavior.
- Updated the live prompt queue so QDB04 is done and QDB05 is ready.

## Files changed
- Api/Endpoints/AdminConfigEndpoints.cs
- Api/Endpoints/AdminDataSourceEndpoints.cs
- Api.Tests/AdminDataSourceEndpointsTests.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md

## Validation run
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AdminDataSourceEndpointsTests -> pass
- dotnet build Api.Tests/Api.Tests.csproj --configuration Release -> pass
- git diff --check -> pass
- node scripts/check-prompt-queues.mjs -> pass

## Validation not run
- dotnet build -> not run separately; the targeted Release build already compiled the Api and Api.Tests projects.
- dotnet test -> not run separately; the targeted integration test covered the changed surface.
- npm run check:analytics-guardrails -> not run; backend-only prompt.
- npm run build -> not run; backend-only prompt.

## What was missed
- No durable secret store was introduced; the prompt stayed within environment-backed named profiles.
- Non-SQL Server provider discovery remains intentionally unsupported in this prompt.

## Risks
- SQL Server discovery integration tests still depend on a reachable local SQL Server instance.
- The new admin discovery surface is limited to safe metadata reads and does not yet cover durable mapping or sync workflows.

## Next
- QDB05 - Add deterministic mapping profile and bounded preview
