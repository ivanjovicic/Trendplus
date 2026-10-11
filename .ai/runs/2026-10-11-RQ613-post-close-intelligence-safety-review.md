# RQ613 post-close intelligence safety review (2026-10-11)

Task: direct owner-requested static GitHub audit and queue registration. Reviewer: ChatGPT.
Base RQ613 delivery: implementation a6acdbbb4d016767522f70b670d237ba0af4833c; close 454e81e0b4896576fa49694c9129050626147a68.
Delivery: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md and MASTER_ROADMAP.md on main.

## Verified source facts
- The join fix in 021_product_demand_signals_v1.sql, 022_inventory_risk_signals_v1.sql, 024_trend_momentum_v1.sql is sf."SaleId" = slf."SaleId". This is the correct source sale identity; sf."Id" is a surrogate PK.
- RQ613 evidence reports Analytics Tests & Data Integrity run 38156293083 and Planning Governance run 38156293080 green on a6acdbbb. GitHub backend tests were not independently executed during this review.
- Api.Tests/AnalyticsIntelligenceSmokeTests.cs reads AnalyticsConnection/DefaultConnection from appsettings and BootstrapIntelligenceSqlAsync executes DDL on that configured database. The RQ613 evidence explicitly records its local trendplus analytics_intel objects were rebuilt in the prior run. This is NOT a read-only smoke test.
- Api.Tests/AnalyticsIntelligenceSaleJoinTests.cs silently returns when _fixture.IsAvailable is false, and when either disposable database creation returns null. The focused PostgreSQL proof can therefore appear passed with no assertions if infrastructure is unavailable; require fail-closed runtime coverage.
- DatabaseInitializer executes changed-hash intelligence scripts in a fire-and-forget Task.Run, with warning-only failure. The scripts drop MV/view only if view COLUMN STRUCTURE differs; otherwise they CREATE OR REPLACE VIEW and CREATE MATERIALIZED VIEW IF NOT EXISTS. On the unchanged-schema RQ613 join fix this alone does not guarantee existing MV contents have been refreshed. An enabled nightly refresh might do so later; exact deployed freshness was not verified.
- SalesFacts has BrojRacuna and DataOrigin. Intelligence daily_sales CTEs in 021/022/024 do not explicitly apply the canonical DUG/KOREKCIJA receipt exclusion. Actual impact is a hypothesis pending independent operational SQL oracle; do not claim a proven discrepancy. Reconstructed inventory risk is not observed stock.

## Routing
- RQ614 READY P0: isolate/guard intelligence smoke SQL and fail-closed PostgreSQL tests; owns test files only.
- RQ615 READY P0: certify startup SQL-to-materialized-view freshness with old-cache upgrade fixture; owns initializer/intelligence SQL and distinct tests.
- RQ616 WAITING P1 on RQ614/RQ615: operational retail population and historical signal parity audit, without live mutation.
- RQ613 remains DONE; do not reopen it. RQ610 remains BLOCKED for cross-writer inventory generation, RQ601/RQ602/RQ606/STAB16 keep independent external gates.

## Limitations and safety
Static read-only GitHub code/evidence inspection. No local build/test, TRX, PostgreSQL execution, live provider verification, API start, local database writes, migration, import, deploy or cache refresh performed in this review. Queue validators and actual test execution must be run by the implementing agent; green documentation commits are not test proof.

Review target paths: Api.Tests/AnalyticsIntelligenceSmokeTests.cs; Api.Tests/AnalyticsIntelligenceSaleJoinTests.cs; Infrastructure/Seed/DatabaseInitializer.cs; Workers/NightlyAnalyticsRefreshWorker.cs; Database/Analytics/Intelligence/{021,022,024}; Domain/Model/{SalesFact,SalesLineFact}.cs; RQ613 evidence and canonical queue.
