Task ID: RQ610-technical-deblock
Queue: direct-user-request; canonical owner is RQ610 in BLOCKED state
Date: 2026-10-11
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq610-ledger-test-fix / no PR
Main commit SHA: d7d4caec07449ed42b1283564c6f27947c37c3c7
Main verification: fresh fetch resolved origin/main to d7d4caec07449ed42b1283564c6f27947c37c3c7 and confirmed it contains the implementation commit
Evidence state: synchronized
Ownership transfer: none

## What was done

- Reviewed the latest origin/main, the existing RQ610 test/evidence artifacts, queue ownership state, locks, branches, worktrees and open PRs. No pre-existing RQ610-specific lock, branch, worktree or PR was found. The unrelated P-UI-43 lock and all primary-checkout edits/artifacts were preserved.
- Reproduced the identity assumption in the second ledger test: its rolled-back insert consumes a PostgreSQL identity value, but the later append-only assertion targeted ledger_id=1.
- Changed the assertion to select the committed ledger row by transaction_id and article_id, then target that actual ledger_id. The primary dirty checkout was not reset or pulled; the repair and its test run were isolated in a new worktree based on origin/main ed88a14f0a02456b1f493e6408be3dce525874cc.
- Ran both tests against a newly created, loopback-only PostgreSQL 16 container with a fresh ephemeral volume and per-test databases. The container and its volume were removed after the run.
- RQ610 remains BLOCKED. The test simulates writer-origin contexts and cannot prove a shared production generation contract across real Access, POS, manual-edit and transfer writers.

## Files changed

- Api.Tests/Rq610StockMutationLedgerPostgresTests.cs
- .ai/runs/2026-10-11-RQ610-stock-ledger-experiment-evidence.md

## Validation run

- Earlier attempt recorded before this follow-up: focused run against the unavailable default server at 127.0.0.1:5434 failed closed with total=2, failed=2, passed=0, skipped=0. No false pass was recorded.
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~Rq610StockMutationLedgerPostgresTests --logger "trx;LogFileName=rq610-ledger-isolated.trx" --results-directory .ai/test-results/rq610-isolated, with RQ610_LEDGER_TEST_POSTGRES pointing only to the new disposable PostgreSQL container -> passed; total=2, executed=2, passed=2, failed=0, skipped=0, notExecuted=0.
- TRX inspection of .ai/test-results/rq610-isolated/rq610-ledger-isolated.trx -> total=2, passed=2, failed=0, skipped=0, notExecuted=0.
- The two passing tests assert atomic rollback; concurrent independent transactions; same-value retry suppression; quantity changes including zero, negative and NULL; store change; transfer rows; and append-only rejection using the identified committed row.
- node scripts/check-prompt-queues.mjs after implementation delivery -> pass; 739 tasks validated.
- git diff --cached --check -> pass. Static inspection confirms the assertion no longer assumes ledger_id=1.

## Validation not run

- Full backend suite, Release build and remote CI -> not run; this scoped change affects only the focused test and its evidence.
- A shared production stock-generation contract across the real Access, POS, manual-edit and transfer writers -> not proven by this synthetic trigger experiment; the POS SQL function remains unavailable for source inspection.
- Production database writes, business imports, production migrations, stock changes and historical backfills -> not run.

## Documentation impact

- Updated this existing RQ610 evidence file; no duplicate was opened.
- No queue or roadmap status was changed. RQ610 remains BLOCKED because the all-writer generation/provenance and inspectable POS acceptance is still unmet.

## What was missed

- The real writer paths do not yet have proven common generation/provenance behavior. The Access import, POS SQL, manual quantity/store edits and transfers must be shown to advance the same durable generation atomically.

## Risks

- This is a test-only PostgreSQL trigger experiment, not production ledger implementation or certification.
- Current RQ610 source evidence says manual edits/transfers do not advance a common audited generation and the POS SQL writer is unavailable. The passing synthetic tests do not close those gaps.

## Post-close routing recovery

- Not applicable as a queue close: this was direct-user-request work and no prompt status changed.
- Post-delivery recovery base: origin/main SHA d7d4caec07449ed42b1283564c6f27947c37c3c7.
- Rechecked the complete program READY table in MASTER_ROADMAP.md and current-pointer headers across the prompt-queue documents. RQ617 is still the primary P0 READY candidate; RQ618 is also READY as an independent P1 candidate. No other program has a READY candidate.
- RQ617's Dnevnik test/fixture paths and RQ618's intelligence SQL/dedicated-test paths are disjoint from this RQ610 test/evidence change. The local task-lock scan found only the unrelated P-UI-43 lock; no RQ617/RQ618 lock, branch or open PR was found. Neither candidate was claimed or changed.
- node scripts/check-prompt-queues.mjs after delivery -> pass; 739 tasks validated.
- This is a direct-user-request delivery, not a queue close; no Zero-READY claim is made.

## Next

- Keep RQ610 BLOCKED until an inspectable POS writer and one durable generation/provenance contract are proven across Access, POS, manual quantity/store edits and transfers.
- The remaining READY candidates are RQ617 and RQ618; this task did not claim them.
