# Backend CI Full-Suite Evidence — BCI05 re-entry after RQ94

Date: 2026-08-11  
Repo: `ivanjovicic/Trendplus`  
Prompt: `BCI05` (re-entry after RQ94)  
Worktree: `9e53f2cc` + uncommitted RQ89-RQ94 repairs  
Agent: cursor

## Decision

`BCI05` remains **PARTIAL**.  
`BCI01` remains **PARTIAL**.

Local Docker suite is still red (`802/809` on this run). The RQ94 contract failure is gone. Remaining durable product/test family is a mojibake assertion in outcome ledger tests. Six WorkerRuntime failures in the same run were Testcontainers Docker-pipe timeouts and did not reproduce on an immediate focused re-check while Docker remained healthy.

No green GitHub Actions `test=success` run exists on a pushed commit containing these repairs.

## Prerequisites

- `RQ89`–`RQ94` Status: DONE

## Local full-suite evidence

Command:

```powershell
$env:CI='true'
dotnet build Api.Tests/Api.Tests.csproj --configuration Release -p:UseSharedCompilation=false
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"
```

Environment:

- Docker Desktop server `29.2.0`
- Logs: `.ai/runs/2026-08-11-BCI05-reentry2-build.log`, `.ai/runs/2026-08-11-BCI05-reentry2-suite.log`
- TRX: `TestResults/analytics-tests.trx`
- Cobertura: not produced locally (same Windows Coverlet lcov deterministic issue)

Totals:

| Metric | Value |
|---|---|
| Total tests | 809 |
| Passed | 802 |
| Failed | 7 |
| Exit code | 1 |

## Failure families

### Family E — mojibake expected string in resolution-snapshot merge test (durable)

Owner prompt: `RQ95`

Failing test:

- `AnalyticsActionItemServiceTests.UpdateOutcomeAsync_MergesResolutionSnapshot_WithoutOverwritingCreationSnapshot`

Signature:

```text
Expected: "PotvrÄen rezultat"
Actual:   "Potvrđen rezultat"
```

Interpretation:

- Input notes use correct `Potvrđen rezultat`.
- The assert still expects the mojibake form `PotvrÄen rezultat`.
- This is a test-encoding contract failure, not a runtime ledger regression.

Focused re-check: still fails the same way.

### Family F — WorkerRuntimePolicy Testcontainers Docker-pipe timeout (environment flake)

Failing tests (6) in the full-suite log:

- `WorkerRuntimePolicyServiceTests.*` (schema/defaults/missing-table/manual-stop family)

Signature:

```text
PostgreSQL integration tests are mandatory in CI, but the Testcontainers fixture could not start.
TimeoutException: NamedPipeClientStream ... Docker.DotNet
```

Focused re-check with Docker healthy: `GetPolicyAsync_CreatesDefaults_WhenRowDoesNotExist` passed. Do **not** open a product repair prompt for this flake.

## GitHub Actions on committed HEAD

| Field | Value |
|---|---|
| Commit | `9e53f2cc` (does not include uncommitted repairs) |
| Latest analytics-tests run | still failure on that HEAD |
| Green GHA proof | blocked until repair worktree is committed/pushed |

## Next execution order

1. `RQ95` READY — fix mojibake expected resolution note in outcome service tests
2. Re-enter `BCI05`
3. Commit/push repair worktree, then record green GHA run IDs before marking `BCI01` DONE
