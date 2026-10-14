# Trendplus — analytics-first scope, 14.10.2026.

**Owner directive / current product scope.** This is a product decision, not another live queue. Canonical queue mechanics remain in `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## BUILD — retail analytics, statistics and decisions

The paying customer is a footwear retailer. Focus on: (1) daily/store/supplier/product sales and signed returns with as-of period; (2) historical and current-margin views with **confirmed vs estimated/unknown cost** and explicit denominator; (3) stock on hand, age where observed, turnover, stagnant inventory and supplier capital exposure; (4) deterministic, backend-owned inventory/supplier/pre-markdown actions and their evidence; (5) descriptive pre/post markdown comparisons and eventual prospectively *measured* outcomes, not causal claims without mature covered controls.

Keep core routes: `/analytics`, `/analytics/daily-sales`, `/analytics/inventory`, `/analytics/supplier`, `/analytics/shoe-type-sales-stats`, `/analytics/products`, `/analytics/pre-nivelacija-prioriteti`, `/analytics/nivelacije-pre-post`, `/analytics/data-quality`, `/analytics/actions`, executive and supplier reports/pulse only to the extent they serve those decisions.

Every meaningful metric must state period, last observed source horizon, scope/store, grain, revenue/cost units, coverage, unknown-versus-zero status, and business-eligible action. Summary/detail/table/chart/export must reconcile. `imported`, `existing` and `all` dataScope must not be silently mixed. Preserve signed returns and exclude DUG/KOREKCIJA per current retail population contract.

## STOP — not current product work

No further eBay, Amazon Shoes, Google Shopping, Deichmann, About You, Humanic, Scraper Hub, global trend or competition-price scraping; no new provider connectors/sync, test/demo feature screens, runtime scoring, Open Training, Insight Studio, speculative forecast models, or marketplace-specific debugging/polish/tests. The **marketplace UI work already delivered** in RQ655 is historical DONE and must not be reopened. RQ656 fake-zero audit is DONE; do not repeat.

Do **not** interpret "test functionality" as unit/integration/regression tests! Tests of canonical retail analytics remain essential. Do not delete shipped API routes, database entities/tables/SQL migrations, import readers, compatibility behavior or historical evidence simply because a feature is frozen. For now remove only experimental **primary-navigation links** (RQ659); removing legacy runtime requires a separate dependency/migration/rollback review. No production data/schema/credential/deploy changes.

Old queue files are historical evidence unless named as a live owner by `MASTER_ROADMAP.md`; do not delete dated evidence. Any historical marketplace prompt is `DEFERRED_OUT_OF_SCOPE` by owner decision, **not automatically READY**, regardless of stale prose. This label is a product-scope classification, not a new queue-status enum. Reactivation requires an explicit new owner decision.

## Product value and proof order

| Now / later | Result | Canonical owner |
|---|---|---|
| NOW P0 | Production correctness and stale/unknown warnings; confirmed sales totals and real cost provenance | existing RQ/STAB16, RQ601 |
| NOW P1 | Simplify nav (RQ659), keep real sales/supplier/inventory/markdown UX, fix proved unknown counts (RQ660) | RQ659–RQ660; do not duplicate shipped RQ593/RQ594/RQ596/RQ651–RQ656 |
| NEXT | Owner digest: five bounded useful opportunities, each with reasons and exact source period; avoid new scoring engine | existing RQ585 and Decision Intelligence owners |
| NEXT when evidence allows | Historical markdown cohorts and eventual action→outcome loop, with sample/coverage gates | RQ602, RQ592 |
| BLOCKED on real source evidence | Verified purchase costs, stock history and fresh production recommendations | RQ601 original 20 calculations, RQ610 mutation generation, STAB16 release/worker/import |
| LATER | Additional AI, forecasting, external trends, multi-tenancy only for a real customer need | explicit product-owner reapproval |

Batch #23 Access source-fact reconciliation is a real achievement: do not import again to prove historical arithmetic. Certified historical reports remain valid as **dated history**, not "today's recommended action". Missing source provenance or sample sufficiency should block the corresponding claim, not all analytics work.

## Definition of done for new work

One named business question; exact existing owner and source grain; one verified reproducible bug or new bounded capability; named affected route/API and tests; negative tests for unknown vs true zero, dates, returns, dataScope, missing cost and stale data where relevant; no duplicate prompt already DONE; real checked main SHA after delivery. Do not create another broad audit just to increase prompt counts.
