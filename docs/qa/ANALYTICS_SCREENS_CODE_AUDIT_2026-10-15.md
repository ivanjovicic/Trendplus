# Analytics screens code audit — 2026-10-15

Base: `origin/main` `270b49e0` (after cache PR #170/#171 and UX audit PR #179). Static review of analytics frontend pages/hooks/services and backend endpoints/queries/SQL. No local database writes, no Development environment, no import, no smoke run. Every finding below was de-duplicated against RQ651–RQ679, PR #170/#171 (cache integrity), PR #179 (UX audit) and `docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md` / `ANALYTICS_FRONTEND_UX_AUDIT_2026-10-14.md`.

Severity: P1 = a number on a core screen is wrong or disagrees with another number on the same screen; P2 = wrong under a realistic condition or a policy deviation with limited reach; P3 = definition/label/copy.

## Summary

| ID | Sev | Area | Finding | RQ |
|---|---|---|---|---|
| A1 | P1 | Dashboard secondary sales endpoints | DUG/KOREKCIJA receipts included, inclusive `<= toDate` end, inner join to `Artikli` — disagree with the Dashboard KPI header on the same screen | RQ680 |
| A2 | P1 | Dashboard category/supplier/gender fast path | Pre-aggregated summary tables bypass retail population, refresh only 7 days, any non-empty partial result is accepted, different row grain/labels | RQ681 |
| A3 | P2 | Nivelacije list period filter | Real-UTC Belgrade conversion applied to wall-clock timestamps → 2 h (summer) / 1 h (winter) shift; RQ654 decision cites the policy as the analytics contract | RQ682 |
| A4 | P2 | Dashboard and Details 7-day KPIs | "Last 7 days" are the last 7 *rows* of a series without zero-filled days; Details week-over-week has no full-window guard | RQ683 |
| A5 | P3 | Daily Sales supplier concentration | Top-3/Top-5/"do 80 %" use quantity shares over a revenue-ordered list | RQ684 |
| A6 | P3 | Analytics service and Details copy | ~50 user-visible fallback errors without diacritics (`Greska pri ucitavanju…`), English fragments in Details | RQ684 |
| A7 | P3 | Inventory priority panel | Supplier value chart is built from the current table page only; only the tooltip says so | RQ684 |

No new defect was found in: cache keys (every `CachedAnalyticsEndpoints` key that has a `dataScope`/`storeId` input includes it — checked by script), Supplier previous-period math (`SupplierDecisionHubPage.tsx:231-237` is correct), Color/ShoeType/Supplier half-open `toUtcDateOnlyExclusive` usage, Data Quality health window (frontend `analyticsApi.ts:1201-1206` sends an exclusive next-day and `DataQualityEndpoints.cs:1839-1846` treats it as exclusive — consistent). Race conditions and fake-zero fallbacks were closed by RQ655–RQ657/RQ660 and were not re-raised.

## A1 — Dashboard secondary sales endpoints bypass the canonical retail population (P1)

Canonical policy: `Api/Services/SalesReceiptPopulationPolicy.cs:12-19` (DUG/KOREKCIJA excluded; RQ605 DONE). The Dashboard header uses it: `Api/Endpoints/CachedAnalyticsEndpoints.cs:5131-5139` (`BuildSalesSummarySnapshotAsync`: predicate + `DatumProdaje < toDate` + left join to `Artikli`) and `:5231-5239` (daily series, same rules).

The rest of the same Dashboard (and Details) uses queries without the predicate, with `<= toDate`, and an inner join to `Artikli` (drops lines whose article is missing):

- `/sales/transaction-stats` `:1726-1749` → "Prosečna vrednost prodajnog dokumenta" (`Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:2577-2584`); its `TotalTransactions` will differ from the header count.
- `BuildTransactionStatsSnapshotAsync` `:5555-5580`, `BuildPaymentDataSnapshotAsync` `:5666`, `BuildWeekdayDataSnapshotAsync` `:5716`, `BuildHourDataSnapshotAsync` `:5768`, `BuildQuickInsightsSnapshotAsync` `:5458`.
- `/sales/by-category` `:1484/1540/1594`, `/quick-insights` `:1642`, `/sales/category-trends` `:2193`, `/sales/top-products` `:380-386` (also no `dataScope` at all; `services/analyticsApi.ts:566-581` sends none).
- Bootstrap breakdowns: `BuildCategoryDataSnapshotAsync` `:5284-5289`, `BuildGenderDataSnapshotAsync` `:5328-5331`, `BuildSupplierDataSnapshotAsync` `:5370-5375`.

Effect: KPI header (retail only, end exclusive) and the category/supplier/gender/weekday/hour/payment/average-document cards (DUG/KOREKCIJA included, end inclusive) are computed over different populations on one screen. The Dashboard sends `toDate=YYYY-MM-DDT23:59` (`AnalyticsDashboard.tsx:801`), so the boundary difference is small there, but a custom `T00:00` end — or Access rows stored at midnight — makes the header drop a whole day the breakdowns include. No test covers these endpoints with a DUG/KOREKCIJA row (`Api.Tests` grep: only `CachedAnalyticsCriticalEndpointsIntegrationTests.cs` touches them, without that row).

## A2 — Pre-aggregated summary fast path (P1)

`CachedAnalyticsEndpoints.cs:5274-5281` (and gender `:5318-5323`, supplier `:5360-5365`): when `dataScope=all` and no store/supplier, the Dashboard reads `AnalyticsCategorySummary`/`AnalyticsGenderSummary`/`AnalyticsSupplierSummary` (`:3325-3365`) and returns it if **any** row exists (`Count > 0`).

- Writers: `Workers/AnalyticsAggregationWorker.cs:357-371/389/421-423` — no DUG/KOREKCIJA exclusion, reads `prodaja_zaglavlje`/`prodaja_stavke` (compatibility relations from `Database/Analytics/013_…`), refreshes only `_daysToRefresh = 7` (`:43`, `:198-206`); `Database/Migrations/007_AddAnalyticsOptimization.sql:183-258` one-off 90-day backfill, also without the policy. `AccessImportService.cs:4070-4087` deletes summary rows for re-imported dates, which leaves holes.
- Read side: inclusive `"Date" <= @toDate::date` (`:3344-3345`) versus raw `<`; aggregate rows carry `Pol = "Ukupno"` while the raw path groups by `Kategorija, Pol` (`:5291-5293`) — the row grain changes with filters; label `Nepoznato` (worker) vs `Ostalo` (raw).

Effect: unfiltered Dashboard breakdowns can come from a partial or stale table and include non-retail documents, while the same view with a store filter uses raw facts. Severity P1 until confirmed; if the worker is not registered in production (`Api/Config/WorkerRuntimeConfig.cs:144-145`), the tables hold only stale backfill.

## A3 — Timestamp convention mismatch on the Nivelacije period filter (P2)

Sales and journal timestamps are stored as Belgrade wall-clock values tagged UTC: `Api/Services/AccessImportService.cs:8349` (`SpecifyKind(sale.DatumProdaje, Utc)`), `:8401-8402`, `:8427` (journal `Datum`). `MASTER_ROADMAP.md` (RQ586 note) records `legacy_access_wall_clock` "where the time zone is intentionally not applied". Analytics endpoints therefore filter on UTC-midnight dates (e.g. `CachedAnalyticsEndpoints.cs:5238-5239`, `DataQualityEndpoints.cs:1839-1840`) — correct for this convention.

`/api/nivelacije` (`Api/Endpoints/AllEndpoints.cs:7287-7308`) is the only caller of `Application/Analytics/BelgradeCalendarDatePolicy.cs:7-15`, which converts Belgrade midnight to real UTC (22:00/23:00 of the previous day). On wall-clock data a "15.10." filter returns 14.10. 22:00–24:00 and drops 15.10. 22:00–24:00. The RQ654 completion decision (queue line "Decision: the backend business day is Europe/Belgrade (`BelgradeCalendarDatePolicy`…") and the comment in `Klijent/clientapp/src/utils/analyticsPeriodPresets.ts:28-29` describe this policy as the analytics contract; the frontend preset result is still right (business "today"), only the justification is wrong. Live POS paths that fall back to `DateTime.UtcNow` (`AccessImportService.cs:5279`, `:6843`) would mix real UTC into wall-clock data — to be confirmed, not fixed here.

## A4 — Row-based 7-day windows (P2)

Daily series are not zero-filled: `CachedAnalyticsEndpoints.cs:5242-5258` groups only days with sales. Consumers treat rows as days:

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:150-182` (`calculateDashboardMovingStats`): MA7 = sum of the last 7 rows / 7, momentum and elasticity compare the last 7 vs previous 7 rows. With closed Sundays or a silent day, "7 days" spans 8+ calendar days and MA7 is overstated.
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx:298-321`: MA7/MA30/anomaly bands on rows, and week-over-week `pct` without the two-full-windows guard that the Dashboard already has (`AnalyticsDashboard.tsx:167-170`), so a short previous window inflates the change.

## A5–A7 — Definitions and copy (P3)

- A5 `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:556-660` and InfoTip `:2165`: suppliers are ordered by the backend revenue order (`resolveAuthoritativeTopSuppliers`), but Top-3/Top-5 and "dobavljača do 80 %" sum quantity shares; the 80 % count is therefore not the minimal count and "top 3" are not the top 3 by quantity.
- A6 `Klijent/clientapp/src/services/analyticsApi.ts`: 50 fallback messages `Greska pri ucitavanju …` reach the screen through `getErrorText`; `pages/AnalyticsDetails.tsx:266-273` `Sazetak nije ucitan`, `Advanced snapshot nije ucitan`, `Health check nije dostupan`, `Analytics baza:`. Not covered by RQ668/RQ670 (their evidence lists other files).
- A7 `Klijent/clientapp/src/pages/InventoryPage.tsx:1074` builds `chartData` from the current page `rows`; `components/inventory/InventoryPriorityPanels.tsx:69` heading "Vrednost po dobavljaču" reads as a total.

## Registered prompts

RQ680 (P1, READY), RQ681 (P1, WAITING on RQ680 — same file), RQ682 (P2, READY, disjoint backend path), RQ683 (P2, READY, frontend-only), RQ684 (P3, WAITING on core-operator evidence / owner re-entry, like RQ670). Full prompts are in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`.

## Limits

Static analysis only. A1/A2 effects were not measured on real data (local DB is read-forbidden for writes and was not queried); each prompt starts with a confirmation step and may close as "not reproduced" with evidence.
