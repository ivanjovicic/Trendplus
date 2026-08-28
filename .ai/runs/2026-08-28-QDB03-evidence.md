# QDB03 evidence log

Prompt: QDB03 - Add a read-only SQL Server proof connector
Date: 2026-08-28
Repo: Trendplus
Status: DONE
Local head: `e3933c0d966ff244e5bf3b05f9e8d21953a8d62f`

Changed files:
- Api/Api.csproj
- Api/Services/DataSources/ISourceDataSession.cs
- Api/Services/DataSources/AccessSourceDataSessionAdapter.cs
- Api/Services/DataSources/SqlServerSourceDataSession.cs
- Api.Tests/Api.Tests.csproj
- Api.Tests/SqlServerSourceDataSessionTests.cs
- Api.Tests/SqlServerSourceDataSessionIntegrationTests.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

Runtime import behavior changed: yes
Existing Access Windows/CLI internals rewritten: no
UI or persisted source credentials added: no

Validation:
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release -nodeReuse:false` - pass
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release -nodeReuse:false --filter "FullyQualifiedName~SqlServerSourceDataSessionTests|FullyQualifiedName~SqlServerSourceDataSessionIntegrationTests"` - pass (11/11)
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release -nodeReuse:false --filter "FullyQualifiedName~SourceDataSessionAdapterTests|FullyQualifiedName~DataSourceConnectorContractTests|FullyQualifiedName~AccessReadQueryPushdownTests"` - pass (16/16)
- `node scripts/check-agent-instructions.mjs --self-test` - pass
- `node scripts/check-agent-instructions.mjs` - pass
- `node scripts/check-prompt-queues.mjs --self-test` - pass
- `node scripts/check-prompt-queues.mjs` - pass
- `node scripts/check-planning-architecture.mjs --self-test` - pass
- `node scripts/check-planning-architecture.mjs` - pass
- `git diff --check` - pass aside from existing LF->CRLF warnings in the dirty worktree

Notes:
- Real-engine proof used `Testcontainers.MsSql` against `mcr.microsoft.com/mssql/server:2022-latest`.
- Coverage includes reserved identifiers, Unicode names/values, nulls, decimals, deterministic full and incremental reads, timeout handling, cancellation, and log redaction of credentials.
- The initial integration seed script failed because `CREATE SCHEMA` shared a batch with later `CREATE TABLE` statements; tests were fixed to execute setup statements separately.

Next:
- QDB04 READY
