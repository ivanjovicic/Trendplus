# Direct claim-collision remediation evidence

```text
Task ID: direct-claim-collision-remediation
Queue: direct-user-request
Date: 2026-10-14
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 408d1b81062e5d4c114960d69ab51d4d239407e4
Main verification: passed - fresh origin/main contains 408d1b81062e5d4c114960d69ab51d4d239407e4; exact-main Analytics Tests 38542983060 are green
Evidence state: synchronized
Ownership transfer: Codex RQ647 close-out -> current direct agent; authority/evidence: explicit user instruction of 2026-10-14 to repair the collision and its incomplete RQ647 evidence
Claim reservation: direct-user-request/not applicable
```

## What was done

- Reviewed the RQ647/RQ649 collision evidence, branches, worktrees, and current `main` before changing source or queue-governance rules.
- Strengthened the canonical protocol so a formal queue task must publish a visible, pushed reservation on refreshed `origin/main` before a worktree, branch, source edit, or implementation commit can begin.
- Repaired RQ647's benchmark: its moderate population is now the shared July fixture plus 600 deterministic sale lines, the report excludes the machine name, and CI has a named benchmark-report artifact.
- Reclassified the two RQ647 performance skips from `genuinely-uncovered` to `legacy-superseded` with their executable benchmark and CI-artifact proof.
- Re-ran the real PostgreSQL/Testcontainers benchmark and refreshed its committed JSON evidence.
- Removed the confirmed clean abandoned RQ649 worktree, its local branch, and its remote branch only after confirming that the direct-main RQ649 delivery was complete and no open PR existed.
- Inspected exact-main Analytics Tests `38542983060`: 2,076 passed, 43 skipped, 0 failed; benchmark artifact `rq647-analytics-endpoint-benchmark-38542983060` (ID `11884041409`) uploaded successfully.

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
- `.ai/runs/2026-10-14-RQ647-evidence.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`
- `MASTER_ROADMAP.md`

## Validation run

- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --disable-build-servers -m:1 -clp:ErrorsOnly` — pass (0 warnings, 0 errors).
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true; TRENDPLUS_BENCHMARK_REPORT_PATH=TestResults/rq647-analytics-endpoint-benchmark-remediated.json; dotnet test Api.Tests/Api.Tests.csproj --no-build --no-restore --configuration Release --filter FullyQualifiedName~AnalyticsEndpointBenchmarkPostgresTests` — pass (1 passed, 0 skipped, 0 failed); the report has 624 sales headers/lines and no `machineName` field.
- Exact-main GitHub Actions Analytics Tests `38542983060` on `408d1b81062e5d4c114960d69ab51d4d239407e4` — pass (2,076 passed, 43 skipped, 0 failed); RQ453 certification passed and artifact `11884041409` contains the explicit RQ647 report.
- Governance validators — pass: agent instruction self-test + validation, prompt-queue self-test + validation, and planning architecture self-test + validation.

## Validation not run

- Production/provider latency and load validation — intentionally out of scope for a deterministic non-gating benchmark.

## Documentation impact

- The canonical queue protocol, entrypoint, repository rules, evidence standard, and template now use one visible cross-agent reservation sequence and one matching evidence record.

## What was missed

- none known.

## Risks

- The benchmark is deterministic and non-gating for latency, but machine performance still varies; the report is evidence rather than a production SLO.

## Post-close routing recovery

- not applicable for direct-user-request work; the RQ647 formal post-close cascade is recorded in `.ai/runs/2026-10-14-RQ647-evidence.md`.

## Next

- none; subsequent formal queue work must begin with the new pushed shared-reservation sequence.
