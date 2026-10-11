# Latest commit follow-up: RQ618 CI fixture and RQ615 overlapping startups

Date: 2026-10-11
Task: direct owner request to review/fix recent commits against their canonical acceptance contracts.
Audit base before writes: e628f67afa3cbadf55c74e9c396ff2a0e6916995.
Scope: GitHub connector static audit and minimal CI/test safety fix. No local .NET runtime, source business DB, provider access, import or deployment.

## Verified latest state
- Latest 31 commits since earlier RQ613 handoff deliver RQ614/RQ615/RQ616/RQ617/RQ618 and a separate isolated RQ610 ledger experiment. RQ614-RQ618 are terminal DONE per owning queue; RQ610 remains BLOCKED for all-writer source provenance.
- Current-main prior full backend CI was RED: Analytics Tests & Data Integrity workflow 38177795288 on RQ618 implementation b8e1c08f5ebea55319b61ce0e11a3dc8f51a7e03, 1,959 passed, 2 failed, 42 skipped. Both RQ618 UTC tests passed. The two failures were Rq610StockMutationLedgerPostgresTests failing fixture initialization at 127.0.0.1:5434 (connection refused).
- The only reported CI exception is an environment mismatch: .github/workflows/analytics-tests.yml provisions pgvector/pgvector:pg16 on localhost:5432 with user postgres and password postgres_password. It declared RQ601 and RQ607 connection variables but not RQ610_LEDGER_TEST_POSTGRES.
- Api.Tests/Rq610StockMutationLedgerPostgresTests.cs previously silently defaulted to Host=127.0.0.1;Port=5434;Database=postgres;Username=postgres;Password=postgres if RQ610_LEDGER_TEST_POSTGRES was absent. Unlike disposable fixture ownership, this could make a test create/drop databases in a developer's local cluster without explicit selection.
- Runtime code review of RQ615: DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync marks caches pending with a new build_generation_id BEFORE fire-and-forget RunAnalyticsIntelligenceBuildAsync obtains advisory lock 987654323. A second instance may overwrite pending owner generation while the first builds; the second returns without the lock; first skips Refresh/MarkReady on generation mismatch, potentially stranding a cache pending until a later startup. RQ615's existing AdvisoryLockContention test proves *manual retry* after contention but has no deterministic two-preparation interleaving. This is a code-derived concurrency risk requiring PostgreSQL proof, not yet an observed production defect.
- Current 021/022/024 SQL has the RQ613 SaleId join, RQ616 canonical DUG/KOREKCIJA exclusion and RQ618 UTC date attribution. No additional rewrite was justified in this static pass.

## Changes delivered to main
1. .github/workflows/analytics-tests.yml: add RQ610_LEDGER_TEST_POSTGRES pointing only to CI localhost:5432 PostgreSQL service with existing CI credentials. Commit bfa1b36327a05f62870cdc5172c9dbbadd396a8d.
2. Api.Tests/Rq610StockMutationLedgerPostgresTests.cs: remove implicit localhost:5434/developer postgres fallback and require explicit configured disposable test server (fail closed). Commit 4c6038d5aaadd2118489a96d84ff3f2352f7b8b7.
3. Register RQ619 READY P1 with exact PostgreSQL two-startup reproducer and scoped cache generation fix, keeping RQ615 DONE and RQ610 BLOCKED. Queue commit 49e9cfa5e3a3e2e8e6df0bb921a4775af14ec592.
4. Update MASTER_ROADMAP current READY and previously stale RQ618 IN_PROGRESS text to RQ618 DONE, RQ619 READY. Roadmap commit 61a9317bdd0b6d551f662677a6f5068c1a091e4f.

## Validation boundary and next
- Static diff/source inspection confirms RQ610 env variable name matches its C# fixture; CI port 5432, credentials and Database=postgres agree with provisioned PostgreSQL service. RQ610 fixture still creates/drops only unique test databases and fails explicitly if the server cannot be reached.
- No executable .NET test or governance validator was run in this GitHub-only session: the local container has no .NET SDK or repository and network access to GitHub is unavailable. CI must confirm actual 2/2 RQ610 fixtures and full backend pass on the new commit; no green result is invented.
- RQ619 is only READY, NOT implemented. Must reproduce overlapping startup generation ownership on disposable PostgreSQL before modifying initializer. Never claim runtime code is fixed without test.
- STAB16 remains the production/deployed parity authority. Render automatic deployment must remain disabled; no production DB changes, manual deploy, import, new migration or direct business DB DDL was performed.
- Preserve unrelated worktrees and dirty/untracked local files; none were accessed through the GitHub connector.
