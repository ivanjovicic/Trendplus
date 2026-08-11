# Backend CI Full-Suite Evidence — BCI05

Date: 2026-08-10  
Repo: `ivanjovicic/Trendplus`  
Prompt: `BCI05`  
Local HEAD / worktree: `9e53f2cc` + uncommitted RQ89/RQ90/outcome hardening changes  
Agent: cursor

## Decision

`BCI05` is **PARTIAL**.  
`BCI01` remains **PARTIAL**. No green GitHub Actions `test=success` evidence exists yet.

## Prerequisites confirmed

- `RQ89` Status: DONE (focused inventory-list tests green in prior session)
- `RQ90` Status: DONE (focused `AnalyticsActionsCriticalWorkflowTests` 13/13 green in prior session)

## Local full-suite evidence (Docker available)

Command:

```powershell
$env:CI='true'
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"
```

Environment:

- Docker Desktop server `29.2.0`
- Testcontainers PostgreSQL used by `PostgresContainerFixture`
- Log: `.ai/runs/2026-08-10-BCI05-full-suite-docker.log`
- TRX: `TestResults/analytics-tests.trx`

Totals:

| Metric | Value |
|---|---|
| Total tests | 809 |
| Passed | 801 |
| Failed | 8 |
| Exit code | 1 |

Coverage note:

- Local Coverlet emitted a deterministic/lcov reporter `NotSupportedException` and did **not** produce `coverage.cobertura.xml` in this Windows run.
- TRX artifact was produced.
- Do not treat this local coverage gap as proof that the GHA coverage pipeline is broken; GHA still needs a green test step before Cobertura success can be claimed.

### First local attempt without Docker

With `CI=true` and Docker stopped, 33 failures were almost entirely Testcontainers mandatory-CI fixtures. That run is environment noise and is not used for product root-cause ownership.

## Remaining product failure families

### Family A — Top offenders SQL references missing `DataOrigin` column

Owner prompt: `RQ91`

Failing tests (5):

- `DataQualityPostgresIntegrationTests.TopOffenders_ImportedScopeDoesNotLeakExistingRowsAndHonorsLimit`
- `DataQualityPostgresIntegrationTests.TopOffenders_ExistingScopeExcludesImportedHeaderSalesOnExistingArticle`
- `DataQualityPostgresIntegrationTests.TopOffenders_ComputesRevenueImpactPercentOrderingAndActionUrl`
- `DataQualityPostgresIntegrationTests.TopOffenders_AllScopeStillIncludesCrossOriginSalesTotals`
- `DataQualityPostgresIntegrationTests.TopOffenders_MissingCost_ReturnsProductsWithoutPurchaseCost_EvenWithSupplier`

Signature:

```text
Npgsql.PostgresException : 42703: column p.DataOrigin does not exist
```

at `AnalyticsDataQualityHealthService.GetTopOffendersAsync`.

### Family B — Data Quality issues list returns empty for seeded cases

Owner prompt: `RQ92`

Failing tests (2):

- `DataQualityIssuesHandlerTests.Handle_SupportsPagination_AndSorting` (expected count `2`, actual `0`)
- `DataQualityIssuesHandlerTests.Handle_ReturnsMissingShoeType_Items` (empty collection)

### Family C — `not_measured` clears entity fields but resolution snapshot keeps measured amount

Owner prompt: `RQ93`

Failing test (1):

- `AnalyticsActionItemServiceTests.UpdateOutcomeAsync_NotMeasuredClearsMeasuredFields_AndMeasuredDateFilterSkipsIt`

Signature:

```text
Assert.Null() Failure
Expected: null
Actual:   999
```

on `ResolutionSnapshot.MeasuredImpactRsd` while the task claimed RQ81 DONE.

## GitHub Actions evidence on current main

Workflow: `Analytics Tests & Data Integrity` (`analytics-tests.yml`)

| Field | Value |
|---|---|
| Commit | `9e53f2ccaa7325e1e270409f367ae849234f483c` |
| Run ID | `31378849007` |
| Job ID | `93424204254` |
| Job URL | https://github.com/ivanjovicic/Trendplus/actions/runs/31378849007/job/93424204254 |
| Restore | success |
| Build | success |
| Test | failure |
| Publish coverage summary | success |
| Upload test results and coverage | success |
| Artifact | `analytics-backend-test-results` id `9059086207` |

### BCI02 diagnostic observation on this red run

- Primary red cause remained the test step (`failure`).
- Coverage summary step stayed `success` after test failure (does not invent a secondary coverage root failure).
- Artifact upload stayed `success` with `if: always()`.
- Annotations contain a generic `Process completed with exit code 1` failure plus unrelated analyzer warnings; no misleading “missing coverage after failed tests” error annotation was observed.

Live annotation-shape proof for a **green** run is still unavailable because no green test run exists yet.

## Why BCI01 is not DONE

Original BCI01 acceptance requires restore/build/test success with exact GHA IDs and totals. Current main and the Docker-backed local suite are still red.

## Next execution order

1. `RQ91` READY — DataOrigin SQL for top offenders
2. `RQ92` WAITING — DQ issues empty-list contract
3. `RQ93` WAITING — `not_measured` resolution-snapshot clear
4. Return to `BCI05` / full suite + GHA green proof before changing `BCI01`
