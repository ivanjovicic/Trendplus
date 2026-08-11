# Backend CI Full-Suite Evidence — BCI05 re-entry

Date: 2026-08-10  
Repo: `ivanjovicic/Trendplus`  
Prompt: `BCI05` (re-entry after RQ91/RQ92/RQ93)  
Worktree: `9e53f2cc` + uncommitted RQ89-RQ93 repairs  
Agent: cursor

## Decision

`BCI05` remains **PARTIAL**.  
`BCI01` remains **PARTIAL**.

Local Docker suite is nearly green (`808/809`), but one contract-test family remains and no green GitHub Actions `test=success` run exists on a pushed commit.

## Prerequisites

- `RQ89`, `RQ90`, `RQ91`, `RQ92`, `RQ93` Status: DONE

## Local full-suite evidence (Docker available)

Command:

```powershell
$env:CI='true'
dotnet build Api.Tests/Api.Tests.csproj --configuration Release -p:UseSharedCompilation=false
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"
```

Environment:

- Docker Desktop server `29.2.0`
- Logs: `.ai/runs/2026-08-10-BCI05-reentry-build.log`, `.ai/runs/2026-08-10-BCI05-reentry-suite.log`
- TRX: `TestResults/analytics-tests.trx`

Totals:

| Metric | Value |
|---|---|
| Total tests | 809 |
| Passed | 808 |
| Failed | 1 |
| Exit code | 1 |

Coverage note:

- Local Coverlet again did not emit `coverage.cobertura.xml` on this Windows run (same deterministic/lcov reporter issue as the earlier BCI05 pass).
- TRX artifact was produced.

## Remaining failure family

### Family D — TopOffenders data-scope contract still asserts legacy quoted `DataOrigin`

Owner prompt: `RQ94`

Failing test:

- `DataScopeConsistencyContractTests.TopOffendersSql_ScopesSales30dBySaleHeader_AndArticlesByDataOrigin`

Signature:

```text
Assert.Contains() Failure
Not found: "p.\"DataOrigin\" = 'access'"
```

Interpretation:

- RQ91 correctly changed sale-header scoping to `p.data_origin` to match EF `HasColumnName("data_origin")`.
- This companion unit contract was not updated and still expects the legacy quoted PascalCase column.

## GitHub Actions evidence on current main

| Field | Value |
|---|---|
| Commit | `9e53f2cc` (does not include uncommitted RQ89-RQ93 repairs) |
| Run ID | `31378849007` |
| Job ID | `93424204254` |
| Restore | success |
| Build | success |
| Test | failure |
| Coverage summary | success |
| Artifact upload | success (`9059086207`) |

No new green GHA run can be claimed until the repair worktree is committed and pushed.

## Next execution order

1. `RQ94` READY — align data-scope consistency contract with `prodaja_zaglavlje.data_origin`
2. Re-enter `BCI05` after RQ94
3. Commit/push repair worktree, then record green GHA run IDs before marking `BCI01` DONE
