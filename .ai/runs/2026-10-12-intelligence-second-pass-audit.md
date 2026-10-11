# Intelligence second-pass correctness audit and queue refill

Date: 2026-10-12 (Europe/Belgrade)
Repository: ivanjovicic/Trendplus
Read-only source base before writes: `9cc108173321c6c84b1e8cd8582467abb0f0b086`
Scope: deep static review of new demand, inventory, price and trend SQL/API/cache, plus the existing queue. This audit registers prompts only; **does not claim a PostgreSQL reproduction, executable local .NET tests, production availability or a deployment**.

## Prior work that must not be duplicated

- RQ613 DONE: SalesFacts/SalesLineFacts source identity by `SaleId` rather than surrogate Id.
- RQ615 DONE: startup hash-based cache refresh for 021/022/024, as certified on isolated SQL fixture; RQ619 separately READY for multi-startup race.
- RQ616 DONE and PR #121 merged: signed retail facts, UTC and DUG/KOREKCIJA scope, unknown stock baseline semantics.
- RQ618 DONE: UTC timestamp attribution for 022 inventory movements.
- RQ620 WAITING: source upper-bound/future-dated facts, now serialized behind RQ623 for SQL024.
- RQ188 DONE: missing list price is NULL discount; unrelated missing cost is still treated as zero.
- BCI17 READY: exact 42 skipped backend tests and their required certification gates.
- RQ610 source-generation and STAB16 provider/deployed-state limitations remain separate authority gates.

## Findings, reproducible counterexamples and routing

| New prompt | Code/evidence | Testable failure mode | Status rationale |
|---|---|---|---|
| RQ621, P1 | `Database/Analytics/Intelligence/023_price_intelligence_v1.sql` latest_products: `COALESCE(PurchasePriceRsd,PurchasePrice,0)`; margin `(net_price-cost)/net_price`; API maps `cost` non-null and sorts/filters margin | Price=100, both costs NULL -> cost=0, margin=1 (100%) even though cost is unknown | READY: SQL023 and dedicated tests disjoint from RQ619 initializer and RQ623 SQL024 |
| RQ622, P1 | `DatabaseInitializer.AnalyticsIntelligenceCacheBySqlFile` has only 021/022/024; `QueryPriceIntelligenceAsync` supplies null freshness arguments; GET bypasses `ExecuteCacheQueryAsync`; SQL023 `CREATE MATERIALIZED VIEW IF NOT EXISTS` | Prior MV with old values plus same-schema new SQL definition remains old while price endpoint can return 200 or empty success | WAITING: collides with RQ619 initializer and RQ621 price schema/API |
| RQ623, P1 | SQL024 `total_units_28d` uses signed net sales as denominator and entropy applies `-p*LN(p)` to positive sales shares | Daily +10/-9 => denominator=1, positive share=10; Shannon term negative; normalized entropy can be invalid; verify with PG oracle | READY: SQL024 math-only, no initializer/SQL023 overlap |
| RQ624, P1 | SQL021/022/024 use CURRENT_TIMESTAMP-derived day in MV; cache gate only tests timestamp presence/hash, initializer only rebuilds on hash/relations/state; nightly refresh sets `refreshedObjects` | Midnight rollover or data changes without hash change can leave a ready previous-day MV; worker-only refresh may not advance cache state timestamp | WAITING: shared initializer/worker/readiness state after RQ619/RQ622/RQ620 |
| RQ625, P2 | SQL024 `COALESCE(latest_history_score,global_score,0)` and observed_products contains local-only sales; API `GetDecimal` | Local-sale-only article is given measured zero external trend despite absent trend evidence | WAITING after RQ623 and RQ620 SQL024 ownership |
| RQ626, P2 | SQL022 stock_proxy uses `GREATEST(reconstructed,0)` and stockout counts zero days; historical stock is reconstructed | Negative inferred stock is converted to zero and may be reported as a verified stockout | WAITING after RQ620 SQL022; code-level semantics hypothesis requiring owner-policy classification |
| RQ627, P2 | SQL023 `pd."Timestamp"::date AS price_date`, while ProductsDim Timestamp schema is timestamptz and RQ618 certified UTC convention | Same near-midnight instant may be assigned different dates under distinct session TimeZone settings | WAITING after RQ621/RQ622 SQL023 and date-cache ownership |

Important nuance: these are not seven observed production incidents. RQ621, RQ622, RQ623 and RQ625 have direct source/math counterexamples; RQ624, RQ626 and RQ627 require executable PostgreSQL/workflow proof. Missing external provider data, source stock history and deployed runtime state are not synthesized from fixtures.

## Execution safety

1. Original canonical queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`. Keep primary RQ READY RQ619 and additional independent RQ621/RQ623 only.
2. RQ620 waits for RQ619 and RQ623 so there are no concurrent SQL024 writes. RQ622 waits for RQ619 and RQ621; RQ624 waits for RQ620/RQ622. RQ625/626/627 serialize per-file after those owners.
3. Every new prompt contains Problem/Evidence/Scope/Read first/Do/Tests/Acceptance/Dependencies, and focused tests require a disposable PostgreSQL fixture with TRX executed/failed/skipped counts.
4. Do not execute startup SQL or DDL against local `trendplus`, Neon or other business database. No import, POS writes, production migration, automatic Render deploy or worker reconfiguration. Main merge only after governance proves canonical queue valid.
5. Existing baseline CI on main SHA `9cc10817` is green for Analytics Tests & Data Integrity and Planning Governance, but this does NOT certify newly hypothesized cases. All runtime work remains future task scope.

## Audit validation boundaries

Read-only GitHub file and queue inspections, branch-based documentation edits, and GitHub Planning Governance are the applicable checks. No direct local compiler, full test suite, PostgreSQL fixture, frontend browser or provider access was used for this documentation-only audit.

## Next

Preserve old prompt ownership; prioritize RQ619 and BCI17, with RQ621/RQ623 parallel only if their path/lock checks still hold on a fresh `origin/main`. Follow with RQ620 and RQ622. Promote the later WAITING items only after the named scope owners are terminal DONE and current-main collision checks pass.
