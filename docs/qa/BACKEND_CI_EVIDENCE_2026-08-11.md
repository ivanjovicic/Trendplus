# Backend CI Full-Suite Evidence

Date: 2026-08-11
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI05`
Agent: Cursor
Local HEAD at evidence time: `9e53f2c` plus uncommitted RQ89 inventory-list runtime fixes

## Preconditions confirmed

- `RQ89` Status: DONE (focused InventoryListEndpointIntegrationTests green 7/7)
- `RQ90` Status: DONE (AnalyticsActionsCriticalWorkflowTests green 13/13; already satisfied on main)

## Local restore/build/test (exact BCI01 commands)

```text
dotnet restore Api.Tests/Api.Tests.csproj
dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release
$env:CI='true'
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release `
  --verbosity normal `
  --collect:"XPlat Code Coverage" `
  --settings Api.Tests/coverage.runsettings `
  --results-directory TestResults `
  --logger "trx;LogFileName=analytics-tests.trx"
```

Results:

| Step | Result |
|---|---|
| Restore | success |
| Build | success (0 warnings / 0 errors) |
| Test | failure |
| Totals | `805` total, `770` passed, `35` failed |
| TRX | `TestResults/analytics-tests.trx` present |
| Coverage | collection requested; suite ended red |

RQ89/RQ90 regression checks inside the same TRX:

- `InventoryListEndpointIntegrationTests`: failed `0`, passed `7`
- `AnalyticsActionsCriticalWorkflowTests`: failed `0`, passed `13`

## Failure classification

### Family A — local CI=true without Docker/Testcontainers (32)

Representative message:

```text
PostgreSQL integration tests are mandatory in CI, but the Testcontainers fixture could not start.
Docker is either not running or misconfigured.
```

Affected classes (counts):

- DataQualityPostgresIntegrationTests (9)
- AccessImportJobQueueTests (6)
- WorkerRuntimePolicyServiceTests (6)
- AccessImportExecutionStrategyTests (3)
- AnalyticsAggregationWorkerTests (3)
- AccessImportForeignKeyGuardTests (3)
- DatabaseInitializerP0IntegrationTests (2)

Interpretation: local Windows agent without Docker is not equivalent to GitHub Actions Postgres service/Testcontainers. Do not invent product patches from this family. Needs GHA run after commit/push.

### Family B — product/test contract (3)

1. `CachedAnalyticsCriticalEndpointsIntegrationTests.InventoryBalance_UnknownSupplierReturnsExplicitEmptyMeta`
   - Expected `meta.dataQualityStatus=insufficient_data`, actual `null`
   - Same `AnalyticsResponseMetaFactory.Empty(..., null)` antipattern previously fixed for inventory list in RQ89
   - Follow-up: `RQ91`

2. `CachedAnalyticsCriticalEndpointsIntegrationTests.TopProducts_UsesIndependentRevenueAndUnitsRankings`
   - Expected `200 OK`, actual `500`
   - Runtime: EF Core could not translate `DbSet<ProdajaStavka>()` LINQ for InMemory provider on `/api/analytics/cached/sales/top-products`
   - Follow-up: `RQ92`

3. `DemoEnvironmentVerificationEndpointTests.RequeueBatch_AllowsRequestWithAdminKey_AndInvokesQueue`
   - Expected queue enqueue count `1`, actual `0` after admin requeue
   - Follow-up: `BCI07` (admin/demo verification; not analytics correctness)

## GitHub Actions

Not recorded in this session:

- `gh` requires `GH_TOKEN` in this environment
- RQ89 runtime fix is still uncommitted, so no new main commit exists for a post-fix GHA proof

Therefore BCI05 cannot mark BCI01 `DONE`.

## Decision

- `BCI05` = `PARTIAL`
- `BCI01` remains `PARTIAL`
- Next READY for execution: `RQ91` (inventory empty-meta follow-up)
- `RQ92` and `BCI07` stay WAITING until RQ91 closes or owner reprioritizes
