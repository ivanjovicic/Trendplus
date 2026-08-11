# Backend CI Full-Suite Evidence (BCI05 resume)

Date: 2026-08-11
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI05` (resume after RQ91 / RQ92 / BCI07)
Agent: Cursor
Local HEAD at evidence time: `9e53f2c` plus uncommitted product/test repairs (RQ89–RQ92, BCI07)

## Preconditions confirmed

- `RQ89` Status: DONE — InventoryListEndpointIntegrationTests green
- `RQ90` Status: DONE — AnalyticsActionsCriticalWorkflowTests green
- `RQ91` Status: DONE — inventory empty-meta contract
- `RQ92` Status: DONE — cached top-products InMemory ranking
- `BCI07` Status: DONE — admin requeue demo host InMemory DB sharing
- Focused combined filter (41 tests spanning the above classes): pass

## Local restore/build/test (exact BCI01 commands)

```text
dotnet restore Api.Tests/Api.Tests.csproj
dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release
$env:CI='true'
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release `
  --verbosity minimal `
  --collect:"XPlat Code Coverage" `
  --settings Api.Tests/coverage.runsettings `
  --results-directory TestResults `
  --logger "trx;LogFileName=analytics-tests-bci05-20260811b.trx"
```

Results:

| Step | Result |
|---|---|
| Restore | success |
| Build | success |
| Test | failure |
| Totals | `805` total, `773` passed, `32` failed |
| Delta vs prior BCI05 | +3 passed / -3 failed (product family closed) |
| TRX | `TestResults/analytics-tests-bci05-20260811b.trx` present |
| Coverage | collection requested; suite ended red (local) |
| Docker daemon | not running (`dockerDesktopLinuxEngine` pipe missing) |

Previously failing product tests inside the same TRX:

- `InventoryBalance_UnknownSupplierReturnsExplicitEmptyMeta` => Passed
- `TopProducts_UsesIndependentRevenueAndUnitsRankings` => Passed
- `RequeueBatch_AllowsRequestWithAdminKey_AndInvokesQueue` => Passed

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
- AccessImportForeignKeyGuardTests (3)
- AnalyticsAggregationWorkerTests (3)
- DatabaseInitializerP0IntegrationTests (2)

Interpretation: local Windows agent without Docker is not equivalent to GitHub Actions Postgres service/Testcontainers. Do not invent product patches from this family.

### Family B — product/test contract

None remaining in this run.

## GitHub Actions

Not recorded in this session:

- `gh auth status`: not logged into any GitHub hosts
- `GH_TOKEN` / `GITHUB_TOKEN`: missing
- Product/test repairs remain uncommitted on `main` (`9e53f2c` is HEAD without those fixes)
- No push performed (commit/push required for a post-fix GHA proof; not authorized as a silent side effect without an explicit commit request)

Therefore BCI05 still cannot mark BCI01 `DONE`.

## Decision

- `BCI05` = `PARTIAL`
- `BCI01` remains `PARTIAL`
- No new product repair prompt opened (Family B empty)
- Blocker for DONE: owner commit + push of repair commits, then GHA run with restore/build/test success + coverage/artifacts
- Current READY after this note: none (BCI blocked on commit/push + `gh` auth); resume `BCI05` when unblocked
