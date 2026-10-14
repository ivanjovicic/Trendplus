# Direct claim-collision remediation evidence

```text
Task ID: direct-claim-collision-remediation
Queue: direct-user-request
Date: 2026-10-14
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: Codex RQ647 close-out -> current direct agent; authority/evidence: explicit user instruction of 2026-10-14 to repair the collision and its incomplete RQ647 evidence
Claim reservation: direct-user-request/not applicable
```

## What was done

- Reviewed the RQ647/RQ649 collision evidence, branches, worktrees, and current `main` before changing source or queue-governance rules.
- Strengthened the canonical protocol so a formal queue task must publish a visible, pushed reservation on refreshed `origin/main` before a worktree, branch, source edit, or implementation commit can begin.
- Repaired RQ647's benchmark: its moderate population is now the shared July fixture plus 600 deterministic sale lines, the report excludes the machine name, and CI has a named benchmark-report artifact.
- Reclassified the two RQ647 performance skips from `genuinely-uncovered` to `legacy-superseded` with their executable benchmark and CI-artifact proof.
- Re-ran the real PostgreSQL/Testcontainers benchmark and refreshed its committed JSON evidence.

## Files changed

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `Api.Tests/AnalyticsEndpointBenchmarkPostgresTests.cs`
- `.github/workflows/analytics-tests.yml`
- `scripts/ci/backend-skip-inventory.json`
- `.ai/runs/2026-10-14-RQ647-report.json`
- `.ai/runs/2026-10-14-direct-claim-collision-remediation-evidence.md`

## Validation run

- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --disable-build-servers -m:1 -clp:ErrorsOnly` — pass (0 warnings, 0 errors).
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true; TRENDPLUS_BENCHMARK_REPORT_PATH=TestResults/rq647-analytics-endpoint-benchmark-remediated.json; dotnet test Api.Tests/Api.Tests.csproj --no-build --no-restore --configuration Release --filter FullyQualifiedName~AnalyticsEndpointBenchmarkPostgresTests` — pass (1 passed, 0 skipped, 0 failed); the report has 624 sales headers/lines and no `machineName` field.

## Validation not run

- Current-main CI and its named RQ647 artifact — pending delivery of this change.
- Governance validators — pending the queue/roadmap close-out edits that follow the current-main CI result.

## Documentation impact

- The canonical queue protocol, entrypoint, repository rules, evidence standard, and template now use one visible cross-agent reservation sequence and one matching evidence record.

## What was missed

- RQ647 queue status/evidence synchronization and RQ649 abandoned-worktree removal remain pending exact-main CI inspection and targeted cleanup.

## Risks

- The benchmark is deterministic and non-gating for latency, but machine performance still varies; the report is evidence rather than a production SLO.

## Post-close routing recovery

- not applicable until the direct remediation is delivered; RQ647's formal queue close-out will be refreshed from post-delivery `origin/main`.

## Next

- Deliver the remediation to `main`, inspect the exact-main CI report artifact, synchronize RQ647 closure, and remove only the confirmed clean abandoned RQ649 worktree/branch.
