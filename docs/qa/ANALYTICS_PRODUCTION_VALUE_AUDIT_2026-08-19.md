# Analytics Production Value Audit - 2026-08-19

## Scope and method

This is a read-only production audit of `https://trendplus.vercel.app` and
`https://trendplus-api.onrender.com` on 2026-08-19. The tested decision flows
were Analytics Dashboard, Pilot Readiness, Executive Decision Board, Product
Decision Center, Data Quality, Supplier Consolidated, Central Actions, Pilot Intake
Report, Supplier Decision Report, Inventory, and Pre/Post Price Leveling.
No workflow action, refresh, import, or data mutation was performed.

The primary API `/ready` returned HTTP 200 with a healthy database check. The
configured Fly fallback did not return `/ready` or `/api/analytics/refresh-status`
within 20 seconds. The primary refresh-status response reported `isRunning: false`,
all refresh timestamps `null`, `dataFreshnessStatus: unknown`, and no enabled
worker process.

## Production findings

### P0 - Durable report implementation is not deployed

`/analytics/reports/pilot-intake` renders the browser-expiry screen in production:
"Pregled izvestaja je istekao jer se cuva privremeno u browseru." The direct
backend report endpoint, however, returned a durable canonical report with 24 rows,
five sections, and a stable query URL. Local uncommitted changes already make the
backend report canonical and retain browser state only for explicit previews. The
release path must ship that patch and prove the deployed behavior after a hard
reload.

The follow-up check narrowed the failure: the durable Pilot API returns the same
24-row report both with and without its stable query parameters, but the matching UI
query URL renders "Pilot intake izvestaj nema podatke". The no-query route still
renders browser expiry. This is a frontend payload-mapping/guard and canonical-link
contract defect, not absence of backend report data.

### P0 - Product and executive decision reads are too slow and too large

The repeated audit returned HTTP 200 for both decision endpoints, but Product took
16.2 seconds and transferred 990,681 bytes, while Decision Board took 17.7 seconds.
The UI eventually rendered 1,200 product rows and five urgent board items, but the
latency keeps the earlier loading/false-zero risk material. These endpoints need a
bounded summary-first contract, server-side pagination and payload budgets instead
of shipping an almost 1 MB decision dataset before the first useful action.

### P0 - Supplier report query contract is internally inconsistent

`/api/analytics/reports/supplier-decision` returned a stable query URL for
2026-02-20 through 2026-08-19, while its payload declared 2011-01-20 through
2026-06-06, effective scope `all_time`, and `usedFallback: false`. The report also
gated recommendations because data quality was critical. Users cannot evaluate a
supplier decision when the requested period, shown period, and effective period do
not agree.

### P0 - Inventory recommendations are not decision-safe

The inventory screen contains genuine catalog and stock figures, but it shows
`P1 DOPUNI 11.885`, `P1 OOS 7D -11.885`, 24 critical replenishment proposals with
zero RSD value, and recommendation rows for codes such as `0001`. The same page
marks data quality OK while detailed cards show only 55% snapshot confidence,
unavailable stock coverage, and critical 0% sell-through. These outputs are useful
for investigation, not safe order instructions, until baseline, quantity sign,
value, identity, and provenance contracts are enforced.

### P0 - Production decision queues contain synthetic smoke artifacts

Executive Decision Board and Central Actions rendered four rows named `Smoke
Dashboard Final`, `Smoke`, `Smoke Product Final`, and `Smoke Inventory Final`.
Those are the entire four-action measurement sample and three-action open queue.
Test artifacts must not
participate in production action counts, prioritization, outcomes, training or
operator queues. Existing rows need an owner-approved quarantine/cleanup plan with
an audit trail; this audit did not mutate or delete them.

### P0 - Same-context screens do not reconcile

Analytics Dashboard reports 686,400 RSD, 14 transactions and 140 units for its
31-day view. Pilot Readiness reports 836,350 RSD, 15 transactions and 145 units for
the current bootstrap. Data Quality readiness is 77/100, while Pilot Readiness calls
the same readiness "Upotrebljivo uz upozorenja (100)". Product renders 1,200 rows,
while readiness qualifies availability from a top-100 request. Supplier ranking also
changes between surfaces. Until period, scope, dataset version, cache time and
denominators reconcile, these are plausible numbers but not one verified business
truth.

### P0 - Dashboard advanced analytics has production schema drift

The main dashboard explicitly reports `42703: column p.DataOrigin does not exist`,
so advanced metrics are unavailable and the page is partial. This is a deployment/
schema compatibility failure. A partial page correctly warns the user, but main must
not declare the analytics release healthy while an expected production column is
missing.

### P0 - Price-leveling analysis overstates causal meaning

Pre/Post Price Leveling reports a -1,135,340 RSD change and -91.6% while also saying
that rolling pre/post, momentum, OOS data and DiD lookup are unavailable, category is
`N/A`, and post-window coverage is only 1%. Ten of eleven suppliers have insufficient
data. The current event-window comparison is a descriptive association, not evidence
that the price change caused the result. Price, availability, seasonality and control
group effects must be separated before this surface can drive pricing or purchasing.

### P1 - Data Quality presents two unlabeled truths

The Data Quality screen shows a 100/100 excellent sales-impact health score and
green state while also showing Pilot Intake readiness 77/100, 1,087 rows without
cost, 12,344 insufficient signals, and 656 ignored rows. The underlying values can
both be valid because health is sales-weighted and readiness is catalog/action
coverage, but the UI does not state that distinction prominently enough. A user can
reasonably infer that all recommended actions are safe.

### P1 - Freshness and deployment provenance are not trustworthy enough

Multiple pages show a recent local-looking timestamp next to `Nije poznato`
freshness. The refresh status API supplies no completed refresh timestamp and has
workers disabled. Supplier Consolidated exposes a real source label and real values,
but still says detailed data quality is unavailable. The primary API can be healthy
while analytics freshness remains unknown; the UI must make those separate facts
explicit.

### P1 - Master-data identity and value defects leak into decisions

Inventory and location drilldowns expose duplicate `Komision` labels, hashed
supplier placeholders such as `Dobavljac #-1879980587`, opaque item codes, zero-RSD
critical replenishment proposals and implausibly low acquisition values. The issue
is not cosmetic: identity and unit-value ambiguity changes grouping, ranking,
deduplication and recommended action value. Data Quality currently reports 100/100
despite these decision-critical defects.

### P1 - Action outcome measurement is not operational

Central Actions reports `measurementStatistics` missing, zero measured outcomes and
0% closed-action coverage. With only synthetic rows in the current sample, the page
cannot answer whether any recommendation worked. The UI is appropriately cautious,
but the workflow currently has no user value beyond exposing that the feedback loop
is not populated.

## Can the production numbers be treated as accurate?

Not yet as a single decision source. Several individual values are internally
plausible: supplier revenue equals the dashboard revenue for one visible context,
margin contribution is arithmetically plausible, and inventory value decomposes into
ABC buckets. However, this audit did not reconcile production results against POS
receipts, accounting totals, source import rows or an independently computed SQL
golden dataset. Cross-surface totals, periods, row limits and readiness scores already
contradict one another. The correct classification is:

- useful for exploration and locating data/stock/supplier problems;
- conditionally useful for descriptive supplier and product review when the visible
  period and warning are accepted;
- not yet reliable for replenishment approval, causal price conclusions, executive
  KPI reporting or recommendation-outcome learning.

## Value assessment

| Surface | Real data usefulness | Current decision value | Condition |
| --- | --- | --- | --- |
| Analytics Dashboard | High potential: revenue, margin, units, risk and top suppliers are present | Low-medium: stale/unknown freshness, SQL schema failure and cross-surface total drift | Reconcile one canonical context and repair schema compatibility before executive use. |
| Pilot Readiness | Good diagnostic coverage of nine required families | Low: only 1/9 ready and its totals/readiness differ from Data Quality and Dashboard | Make every checklist card cite effective period, dataset version, row limit and source status. |
| Supplier Consolidated | High: revenue, units, cost, margin, concentration and supplier rows are coherent for the selected 30-day period | Medium: all 11 suppliers are insufficient-data, so it supports diagnosis rather than an action | Keep as the canonical supplier workspace; show the minimum missing evidence per supplier. |
| Data Quality and Pilot Intake API | High: missing-cost, signal and sales-impact counts are concrete | Medium-low: two score systems are not labeled by population | Separate sales-risk health from catalog/action readiness and make gating visible. |
| Product Decision Center | Medium-high diagnostic value: 1,200 rows, sales, margin, stock cover and sell-through are real | Low decision value: all primary action counters are zero, 1,185 signals are insufficient, and initial load is about 16 seconds | Return a small summary first, paginate server-side and lead with why actionability is blocked. |
| Executive Decision Board | Medium: five urgent items, blockers and action state are composed from real sources | Low: expected impact is unavailable, supplier candidates are absent, response is about 18 seconds and synthetic smoke rows pollute the queue | Remove synthetic rows, bound the aggregate and rank only decisions with qualified impact/provenance. |
| Supplier Decision Report API | Potentially high but period provenance is broken | Low until filter parity is fixed | Reject or declare fallback rather than presenting mismatched date ranges. |
| Inventory | Useful as a stock exploration dataset: 12,422 catalog items, 3,566 units, ABC distribution and item/location detail are present | Unsafe for replenishment actions | Make recommendations investigation-only until baseline and value guards pass. |
| Pilot Intake Report UI | Backend has a durable report | None in production due stale browser-preview flow | Deploy durable-report patch and release-smoke it. |
| Central Actions | Correctly distinguishes not measured from success | None today: all four rows are smoke artifacts and measurement statistics are missing | Isolate test data, then require real execution/outcome capture before showing learning KPIs. |
| Pre/Post Price Leveling | Descriptive event-window rows exist for 11 suppliers | Unsafe for causal price decisions | Require availability controls, daily margin/profit, control group/DiD and minimum post-window coverage. |

## Cross-surface data flow

1. Source/catalog, sales, cost, supplier, store, stock and movement data feed the primary API.
2. Backend endpoints calculate health, readiness, supplier, product, board and inventory payloads.
3. Refresh/freshness metadata is supposed to qualify those payloads, but the production refresh status is unknown and workers are disabled.
4. The frontend renders trust headers, decision cards, tables, reports and action workflows.
5. Current failure modes occur at different stages: source/master-data ambiguity,
   schema drift, cache/version divergence, oversized reads, report mapping drift,
   synthetic action persistence and unsafe recommendation construction.

The system therefore has usable raw business data, but not one dependable end-to-end decision contract. The first release must make unknown, fallback, incomplete, and zero distinct at every handoff.

## Recommended release order

1. Ship and prove the already implemented durable report fix (`PROD-AN-01`).
2. Quarantine production smoke artifacts and prevent recurrence (`PROD-AN-09`).
3. Reconcile all same-context totals and dataset/cache versions (`PROD-AN-11`).
4. Repair dashboard schema/deployment compatibility (`PROD-AN-12`).
5. Make Board and Product reads bounded and fail closed (`PROD-AN-02`).
6. Correct supplier report filter/provenance parity (`PROD-AN-03`).
7. Block unsafe inventory actions and repair recommendation contracts (`PROD-AN-04`).
8. Gate price-leveling conclusions on causal evidence (`PROD-AN-13`).
9. Enforce canonical location, supplier, item and value identity (`PROD-AN-10`).
10. Make health, readiness, freshness and data quality comparable (`PROD-AN-05`).
11. Wire real action execution and measurement statistics (`PROD-AN-14`).
12. Reconcile runtime consumption of the completed observed-inventory foundation (`PROD-AN-06` / `RQ96`).
13. Establish a green analytics test and production-smoke gate before treating later UI work as release-ready (`PROD-AN-07`).
14. Improve action explanation and drilldown after correctness gates are in place (`PROD-AN-08`).

## Non-findings

- The primary Render API was responsive and database-ready when checked.
- Supplier Consolidated rendered non-zero, internally plausible financial values for the selected period.
- The audit did not place orders, approve workflow actions, alter filters, or refresh production data.
- The inventory count-to-unit relationship is a contract concern, not yet a confirmed calculation defect; the recommendation defects above are independently observable.
