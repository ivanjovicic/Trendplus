# Backend CI Full-Suite Evidence — BCI05 after BCI09

Date: 2026-08-13
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI05` (re-entry after `BCI09` DONE)
Agent: cursor
Green GHA HEAD: `f1f5a1756399568a7c5a169d09a8fd1c1dd8d1b8`
Current origin/main: `ed0d752` (docs-only follow-ups after `f1f5a17`; no backend/workflow diff)

## Decision

`BCI05` is **DONE**.
`BCI01` is **DONE**.
`BCI06` is the next BCI READY/IN_PROGRESS prompt.
`QDB03` remains unblocked in `MASTER_ROADMAP.md` because the open BCI PARTIAL gate is cleared. The QDB owner-queue READY pointer is a separate program and is not changed here.

## Analytics safety gate

- Source of truth: GitHub Actions `analytics-tests.yml` on backend-equivalent `main`
- Contract changed? no
- Unit/denominator: not applicable (CI evidence)
- True zero case: not applicable
- Missing/unknown case: exact GHA TRX passed/failed counts were not unzipped (no `gh` auth); they are recorded as unknown, not invented
- No-baseline case: not applicable
- Freshness/fallback case: older red run `31622706051` on `2fbea01` is superseded
- Surfaces affected: CI workflow only
- Tests proving table/detail/export/action parity: not applicable
- Stop condition hit? no

## GitHub Actions proof

- Workflow: `Analytics Tests & Data Integrity`
- Workflow ID: `260581486`
- Run ID: `31674533356`
- Job ID: `94366108914`
- Head SHA: `f1f5a1756399568a7c5a169d09a8fd1c1dd8d1b8`
- Trigger: `push`
- URL: https://github.com/ivanjovicic/Trendplus/actions/runs/31674533356
- Outcomes:
  - Restore: success
  - Build: success
  - Test: success
  - Publish coverage summary: success
  - Upload test results and coverage: success
- Artifact:
  - `analytics-backend-test-results`
  - artifact ID `9171046754`
  - size 1.3 MB
- Failed-test annotations: none
- Exact TRX totals: unknown without authenticated artifact download

`git diff --name-only f1f5a17..HEAD -- Api Api.Tests Application Domain Infrastructure .github/workflows/analytics-tests.yml` is empty, so current `origin/main` is backend-equivalent to this green run.

## Local equivalent

Restore: success. Build: success.

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"
```

Result with `CI=true` on Windows: **829 total / 797 passed / 32 failed**.

All 32 failures are the same environment class:

```text
PostgreSQL integration tests are mandatory in CI, but the Testcontainers fixture could not start.
Docker is either not running or misconfigured.
```

That is not a product-assertion family. GitHub Actions provides the PostgreSQL service and is the closing proof.

## BCI02 diagnostic observation

- Green run `31674533356`: coverage summary and artifact upload succeed after a successful test step.
- Earlier red run `31622706051`: build was the primary failure; coverage/artifact steps did not invent a second root cause.

## Next

1. `BCI06` verifies Windows/Visual Studio mixed-solution compatibility.
2. `QDB03` is unblocked for the SQL Server proof connector after BCI has no remaining READY task.
3. Do not reopen BCI01 unless a later backend commit turns `analytics-tests` red.
