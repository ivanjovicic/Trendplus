# QDB05 evidence log

Prompt: QDB05 - Add deterministic mapping profile and bounded preview
Date: 2026-08-28
Repo: Trendplus
Status: DONE
Local head: `e3933c0d966ff244e5bf3b05f9e8d21953a8d62f`

Changed files:
- Api/Config/DataSourceOptions.cs
- Api/Endpoints/DataSourceMappingPreviewEndpoints.cs
- Api/Models/DataSourceMappingPreviewModels.cs
- Api/Program.cs
- Api/Services/DataSources/AccessSourceDataSessionAdapter.cs
- Api/Services/DataSources/DataSourceMappingPreviewService.cs
- Api/Services/DataSources/ISourceDataSession.cs
- Api/Services/DataSources/SqlServerSourceDataSession.cs
- Api.Tests/DataSourceMappingPreviewEndpointsTests.cs
- Api.Tests/DataSourceMappingPreviewServiceTests.cs
- Api.Tests/SqlServerSourceDataSessionIntegrationTests.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

Runtime behavior changed: yes
Durable business writes added: no
Database migration added: no
UI added: no

Validation:
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release -nodeReuse:false` - pass
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DataSourceMappingPreviewServiceTests|FullyQualifiedName~DataSourceMappingPreviewEndpointsTests|FullyQualifiedName~SqlServerSourceDataSessionIntegrationTests"` - pass (9/9)
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DataSourceDiscoveryEndpointsTests|FullyQualifiedName~DataSourceConnectorContractTests|FullyQualifiedName~SourceDataSessionAdapterTests|FullyQualifiedName~SqlServerSourceDataSessionTests"` - pass (22/22)
- `node scripts/check-agent-instructions.mjs` - pass
- `node scripts/check-prompt-queues.mjs` - pass
- `node scripts/check-planning-architecture.mjs` - pass
- `git diff --check` - pass (LF/CRLF warnings only from existing dirty worktree files)

Notes:
- Mapping preview is admin-only and runs against environment-backed named source profiles from QDB04.
- Preview sampling is bounded and does not write mappings, checkpoints, or business entities.
- Schema fingerprints are deterministic over normalized column names, source types, and nullability where available.
- SQL Server now exposes column type/nullability metadata; Access remains supported through compatibility metadata with unknown type/nullability fallback.
- Explicit external keys are required for sync-readiness and preview rejects null or invalid cursor values on sampled rows.

Next:
- QDB06 remains WAITING until the owner approves a database migration.
