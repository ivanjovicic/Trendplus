Task ID: QDB04
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Cursor
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: skipped; user did not request commit or push

## What was done
- Claimed `QDB04` and added environment-backed named source profiles under `DataSources:Sources`.
- Added admin-only discovery endpoints: list sources, test connection, list tables/schemas, list columns.
- Responses omit connection strings and credentials; failed tests return safe categories and a generic message.
- Connection tests use the existing `strict` rate-limit policy; discovery actions are audit-logged without secrets.
- First provider is SQL Server via `SourceSessionFactory`. No mapping, preview writes, or sync jobs.

## Files changed
- Api/Services/DataSources/DataSourceConnectorOptions.cs
- Api/Services/DataSources/SourceSessionFactory.cs
- Api/Services/DataSources/NamedSourceDiscoveryService.cs
- Api/Services/DataSources/ISourceDataSession.cs
- Api/Services/DataSources/AccessSourceDataSessionAdapter.cs
- Api/Endpoints/DataSourceDiscoveryEndpoints.cs
- Api/Program.cs
- Api.Tests/DataSourceDiscoveryAuthorizationTests.cs
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
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceDiscovery|FullyQualifiedName~SourceDataSessionAdapterTests -> pass (12/12)
- node scripts/check-agent-instructions.mjs --self-test -> pass
- node scripts/check-agent-instructions.mjs -> pass
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass

## Validation not run
- full Api.Tests suite - out of QDB04 focused proof
- npm frontend checks - no UI in scope
- durable secret storage - explicitly out of scope

## What was missed
- Mapping profile / bounded preview (`QDB05`)
- Access named-source discovery
- Admin UI

## Risks
- Connection strings must be supplied through environment/config; committed appsettings should not hold real secrets.
- Test-connection rate limiting shares the global `strict` policy with other admin endpoints.

## Next
- QDB05 - Add deterministic mapping profile and bounded preview
