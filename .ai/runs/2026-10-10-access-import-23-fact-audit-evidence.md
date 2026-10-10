Task ID: ACCESS-23-FACT-AUDIT
Queue: direct-user-request; RQ605 registered as an unclaimed follow-up
Date: 2026-10-10
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: `9de51b8814e63eb99ef5cd31955337de1e73dc01`
Main verification: fresh `origin/main` equals `main` at `9de51b8814e63eb99ef5cd31955337de1e73dc01`; it contains the audit and RQ605 registration.
Evidence state: synchronized
Ownership transfer: none

## What was done

- Refreshed `origin/main` before inspection; current pre-delivery base was `858656ae0c8ba8f3f414b0d3d80570ecc7b308e1`.
- Inspected the canonical RQ queue, active threads/worktrees, branches and task locks. No competing active Access/fact-audit owner or RQ605 duplicate was found. The current task is a direct user request, so no existing queue claim was taken over.
- Performed read-only PostgreSQL checks against Docker container `trendplus-postgres`, database `trendplus`.
- Proved Access batch #23 header/line completeness and exact line amount parity; classified unmatched facts, identity-less rows, RQ407 fixtures, dimension orphans and legacy cache/fact impact.
- Reviewed `ExecuteImportBatchAsync`, `RetriableDbContextTransaction.ExecuteAsync`, `PersistBatchProgressAsync`, `RunBatchHeartbeatLoopAsync`, `AccessImportBackgroundWorker.ProcessJobAsync` and `AccessImportJobQueue.ClaimNextAsync`, plus focused import/queue tests.
- Registered the non-duplicative independent remediation prompt `RQ605` as READY and unclaimed. No product code or database data was changed.

## Files changed

- `tools/access-import-23-fact-audit.sql`
- `docs/qa/ACCESS_IMPORT_23_FACT_AUDIT_2026-10-10.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-10-access-import-23-fact-audit-evidence.md`

## Validation run

- Docker PostgreSQL read-only batch, population, identity, amount, scope, orphan, aggregate and lock queries -> pass.
- `pg_stat_activity`/`pg_locks` current snapshot -> pass; no active blocking chain and no long open transaction observed.
- Access #23: 5,530/5,530 headers, 67,092/67,092 lines, quantity/unit-price/line-total deltas zero -> pass.
- Independent canonical retail reconciliation with DUG/KOREKCIJA exclusion and signed values -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AccessImportHeartbeatPostgresIntegrationTests --no-restore --nologo` -> pass, 3/3; real PostgreSQL lock-contention/cancellation heartbeat proof.
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AccessImportJobQueueTests --no-build --no-restore --nologo` -> pass, 8/8.
- `node scripts/check-prompt-queues.mjs` -> pass, 726 tasks.
- `node scripts/check-agent-instructions.mjs` -> pass, 18 canonical files.
- `node scripts/check-planning-architecture.mjs` -> pass, 80 planning tasks.
- `git diff --check` -> pass.

## Validation not run

- RQ605 implementation tests -> not run; the prompt was registered but not claimed or implemented.
- Full backend/frontend test suites -> not run; this turn is read-only audit plus queue/evidence delivery.
- Production database/provider verification -> not run and not authorized.
- Any data repair, cache rebuild, migration, Access import or deletion -> intentionally not run.

## Documentation impact

- Added the repeatable read-only SQL evidence pack and the durable Access #23 audit.
- Added RQ605 to the canonical analytics reliability queue and roadmap without changing formulas or claiming the remediation complete.
- The `analytics-nivelacija` guidance was applied to stratify impact by day/store/dataScope and to separate true population effects from false-positive “overcount” claims.

## What was missed

- No RQ605 runtime fix or PostgreSQL regression test was implemented in this audit.
- Installed `analytics_intel` signal views and materialized-view refresh behavior were inspected for source risk but not changed or certified.

## Risks

- Legacy `/api/analytics/cached/sales/daily`, `/api/analytics/sales/comparison`, stale aggregate and fact-based signal paths can expose non-canonical existing facts until RQ605 is implemented.
- `AnalyticsDailySummary` contains 2,697,200.00 RSD of existing legacy/demo revenue and must not be treated as standard retail truth.
- No business data was deleted or rewritten; the legacy rows remain available for an approved, separately backed-up repair decision.

## Post-close routing recovery

- Not applicable for direct-user-request audit. RQ605 was registered, not claimed or closed.

## Next

- Claim and implement RQ605 only after the normal fresh collision check; require the real PostgreSQL regression matrix in its acceptance before any remediation is called complete.
