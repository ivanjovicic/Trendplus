Task ID: BCI13
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-28
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: b28010740b484e378315b8c8b9e90762d5a0fa1d
Main verification: `git rev-parse origin/main` returned `b28010740b484e378315b8c8b9e90762d5a0fa1d`; implementation SHA is the exact current `origin/main` tip.
Evidence state: synchronized

## What was done
- Reproduced the three BCI13 endpoint classes in one combined focused run.
- Updated the legacy `DataSourceDiscoveryEndpointsTests` host and expectations to the current `NamedSourceDiscoveryService`/`DataSourceConnectorOptions` routes and DTOs.
- Fixed admin discovery schema/table matching for bracketed SQL Server identifiers by using the shared identifier parser.
- Preserved admin-key rejection, strict rate limiting, safe unsupported-provider output and secret redaction.

## Files changed
- Api.Tests/DataSourceDiscoveryEndpointsTests.cs
- Api/Endpoints/AdminDataSourceEndpoints.cs
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-28-BCI13-evidence.md

## Validation run
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceDiscoveryEndpointsTests|FullyQualifiedName~AdminDataSourceEndpointsTests|FullyQualifiedName~DemoEnvironmentVerificationEndpointTests"` -> pass, 36 passed, 0 failed, 0 skipped.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceDiscoveryAuthorizationTests"` -> pass, 7 passed, 0 failed, 0 skipped.
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass, 12 canonical files checked.
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 600 tasks checked.
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 new planning tasks checked.

## Validation not run
- Fresh broad GitHub Actions backend suite -> not run locally; BCI14 owns the next broad residual classification after this focused repair.
- GitHub Actions Analytics Tests & Data Integrity run `36454389512` on `b28010740b484e378315b8c8b9e90762d5a0fa1d` -> `in_progress` when inspected; not awaited.
- GitHub Actions Planning Governance run `36454421776` on closure SHA `01e7cbb078aed0d8f915cdfe09f6c8b3db4b99b4` -> `in_progress` when inspected; prior run `36454389679` on the implementation SHA was green.

## Documentation impact
- Synchronized the BCI13 queue status, completion evidence, and master roadmap routing. BCI14 remains WAITING rather than being promoted without fresh broad-suite evidence.

## What was missed
- The fresh broad backend suite and its residual provider/order-isolation classification remain for BCI14.

## Risks
- Focused endpoint/auth proof is green, but the overall backend current-main gate remains unresolved until BCI14 and the reopened BCI10 gate receive fresh broad evidence. Remote current-main runs were still in progress at inspection.

## Next
- BCI14 - isolate remaining PostgreSQL/provider/order-dependent broad-suite failures after a fresh broad run.
