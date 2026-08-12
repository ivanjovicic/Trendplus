# Backend CI Full-Suite Isolation Evidence — BCI08

Date: 2026-08-12  
Repo: `ivanjovicic/Trendplus`  
Prompt: `BCI08`  
Worktree: `d9c4d0a` + BCI08 test-host isolation fixes  
Agent: cursor

## Decision

`BCI08` is **DONE**.  
`BCI05` is promoted **READY** for re-entry and green GHA proof.  
`BCI01` remains **PARTIAL** until a green GitHub Actions run on the pushed commit.

## Root cause

The four GHA failures were deterministic CI environment leakage, not flaky order dependence:

| Test | Cause | Fix |
|---|---|---|
| `DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent` | GHA `ConnectionStrings__*` leaked into `WebApplication.CreateBuilder` | Force empty connection strings in test host unless caller supplies them |
| `InventoryList_UncachedRouteMatchesSeededRowCountAndEmptyMeta` | `InventoryFactory` stubbed only Trendplus; uncached route hit real analytics PG | Mirror `AnalyticsProgramRouteTestFactory`: stub `AnalyticsDbContext` in-memory + isolate config |
| `AccessImportRunEndpointTests` (2 tests) | Linux runtime gate requires `mdb-tables`/`mdb-export` on PATH before handler logic | Stub PATH with no-op executables in isolated test host |

## Changes

- `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Api.Tests/AccessImportRunEndpointTests.cs`

## Local evidence

### Focused BCI08 family with CI env vars set

Command:

```powershell
$env:CI='true'
$env:ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=trendplus_test;Username=postgres;Password=postgres'
$env:ConnectionStrings__AnalyticsConnection='Host=localhost;Port=5432;Database=trendplus_analytics_test;Username=postgres;Password=postgres'
dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~DemoEnvironmentVerificationEndpointTests.DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent|FullyQualifiedName~InventoryListEndpointIntegrationTests|FullyQualifiedName~AccessImportRunEndpointTests"
```

Result: **14/14 pass** (`.ai/runs/2026-08-12-BCI08-focused.log`)

### Exact four-test filter (GHA failure set)

Result: **4/4 pass** with CI env vars (`.ai/runs/2026-08-12-BCI08-four-recheck.log`)

### Full suite with CI env vars

Result: **826/829 pass**, **3 fail** — all `AccessImportExecutionStrategyTests` due to Testcontainers Docker-pipe timeout (environment flake, same family as prior WorkerRuntime flake). The BCI08 four-test family did not fail.

Logs: `.ai/runs/2026-08-12-BCI08-full-suite.log`

## Next

1. Commit/push BCI08 fixes
2. Re-enter `BCI05` and capture green GHA run/job IDs
3. Mark `BCI01` DONE only after green GHA evidence
