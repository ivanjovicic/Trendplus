# Post-RQ616 blocked-work re-evaluation and precise next prompts

Date: 2026-10-11
Scope: read-only inspection of GitHub main, recent RQ614/RQ615/RQ616 evidence, source-code test/database safety, current canonical queue/roadmap. GitHub commits contain documentation/queue changes only; code and business DB were NOT changed in this audit.

## Baseline and confirmed completions
- Initial current GitHub main: f77d0763d55f3c3fe7fff856fe88fb4e7bc98451 (docs queue recovery).
- RQ614 DONE at 918fdd4e (disposable PostgreSQL intelligence smoke safety).
- RQ615 DONE at 4eb58461 (startup SQL hash / materialized cache freshness).
- RQ616 DONE at f1200dff (retail population, UTC sale date, nullable unknown risk through API/UI). Evidence .ai/runs/2026-10-11-RQ616-evidence.md reports 13/13 focused PostgreSQL tests and 18/18 frontend; full backend suite was NOT rerun for the RQ616 close.
- RQ610 BLOCKED: local primary checkout reported dirty RQ610 queue/test/evidence and one concrete failing ledger assertion. Queue-recovery evidence: .ai/runs/2026-10-11-queue-recovery-evidence.md. A PostgreSQL identity/sequence ID can advance even when an earlier insert is rolled back; hard-coding ledger_id=1 does not reliably identify a committed row. An isolated test correction cannot prove absent POS SQL writer, source lineage or cross-writer stock mutation generation, so do not promote RQ610 to DONE.

## New confirmed safety problem -> RQ617
- Api.Tests/DnevnikPromenaReadServiceTests.cs::TryCreateContexts reads appsettings.json and appsettings.Development.json DefaultConnection and AnalyticsConnection.
- IntegrationDbGuard.TryResolveConnectionString and TryEnsureAvailable check presence/connectivity, not fixture ownership.
- The test methods then call SaveChangesAsync with Artikli/DnevnikPromena and ProductsDim/InventoryMovementFacts, deleting test records in finally. This is DML on a configured database that could be local business trendplus, not an isolated test instance; cleanup and Random.Shared.Next() do not prove rollback/isolation.
- When connections are missing, each test returns before assertions, i.e. possible false-pass.
- RQ614 did not own this Dnevnik test. Promoted a separate path-disjoint RQ617 READY P0 in docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md.

## New bounded SQL correctness hypothesis -> RQ618
- RQ616 changed sale day extraction to (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date in Database/Analytics/Intelligence/021/022/024.
- In 022_inventory_risk_signals_v1.sql, movement day remains DATE_TRUNC('day', imf."Datum")::date and movement time-bound predicate remains session-date cast. DATE_TRUNC applied to timestamptz is session-timezone-sensitive.
- 021/022/024 settings rely on CURRENT_DATE, also session timezone dependent. This can disagree with the explicitly UTC sale-day label around calendar boundary; the original RQ616 oracle seeded UTC sales but did not independently compare movement dates across multiple session timezones.
- RQ618 READY P1 demands a failing disposable PostgreSQL cross-timezone oracle and type check before runtime changes, with view/materialized-cache parity using RQ615. If no mismatch is reproduced, do not change code. Does not overlap RQ617 files.

## Existing external gates unchanged
- RQ601 PARTIAL: authentic 20 original purchase calculations.
- RQ602/RQ592: historical date coverage is not available, future qualified treatment/control cohort/owner sign-off needed; RQ611 future coverage does not retroactively certify batch 23.
- RQ606 PARTIAL: original byte-identical MDB to reconcile 244 sales lines / 20 receipts / 168 journal entries.
- RQ610 BLOCKED: generation/provenance across Access, manual, transfer, POS and the POS SQL function inspection. Preserve currently dirty local RQ610 paths; its provisional test must locate actual committed ledger row instead of ledger_id=1, pass 0-skipped isolated PostgreSQL, then still await all-writer contract proof.
- STAB16 and dependent RQ128/RQ140/RQ454/RQ565/RQ566: approved deployed/live evidence; do not assume production is current or ready from /ready alone.
- RQ139 PARTIAL: previous 999 sentinel no longer current; need a reproducible numeric-state case, not speculative rewrites.
- RQ472/RQ530/RQ531/RQ558/RQ559, MT/QDB/GAI/RL remaining gates require named source/owner or prospective data evidence.

## Deliverables and validation limitations
- Registered RQ617 READY P0 and RQ618 READY P1 in the existing canonical Test Hardening addendum and updated Master Roadmap routing. No duplicate reopening of RQ613..RQ616.
- GitHub file update commits: 89497d58f693c0a7547cc2a5c37c55a480371dc0, 7dc77b88c7c0c69a8c718b6128db55a8395298b0.
- Safety: neither test nor SQL was executed against any database here. No local repo/worktree access; no local governance validators/Release builds/PostgreSQL runs performed in this review. The next implementing agent MUST run all validators and deterministic fail-closed PostgreSQL tests; do not call these docs-only commits test-certified.
- No production deploy/import/migration or price/inventory modification authorized.
