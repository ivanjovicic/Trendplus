# Analytics Demo Mode Plan

_Status: plan only - no implementation yet._

_Last updated: 2026-06-02_

---

## 1. Demo story

The demo simulates a 30-day pilot for a fictional footwear/apparel retailer,
**Demo Moda d.o.o.**, running Trendplus for the first time.

The buyer has just imported 90 days of sales data from their Access/MDB file.
The system has processed the import, refreshed analytics, and surfaced the
first set of decisions. The goal of the demo is to walk a prospective
customer through the full decision loop in 10 minutes.

### Story arc

| Step | Screen | What the buyer sees |
|---|---|---|
| 1 | Data Quality | Some data is dirty - missing supplier names, zero-cost products, duplicate barcodes. The system flags them before showing any recommendations. |
| 2 | Dashboard | First overview: which categories sell, which don't. Trust header shows last refresh and data quality status. |
| 3 | Supplier Scorecard | Two strong suppliers and two underperformers are visible. One has a fallback warning (30d insufficient, showing 90d signal). |
| 4 | Inventory Risk | Several SKUs are OOS risk. Several are dead stock with no rotation for 60+ days. |
| 5 | Action Queue | Buyer adds "Dopuni za SKU X" from Inventory. System shows it as `pending`. |
| 6 | Supplier Report | Buyer prints/exports the supplier decision report for the board meeting. |

---

## 2. Dataset requirements

### Scale

| Entity | Count | Notes |
|---|---|---|
| Suppliers | 7 | 2 strong, 2 weak, 1 fallback-only, 1 missing-name, 1 new/few-sales |
| Products (SKUs) | 120 | Mix of patike, sandale, čizme, odeća |
| Sales period | 180 days | Gives 90d and 30d sub-windows; covers seasonal variation |
| Stores | 3 | Store A (main), Store B (medium), Store C (low traffic) |
| Stock records | 120 SKUs x 3 stores = 360 rows | At least 40 OOS lines |

### Required fields per product

| Field | Required | Notes |
|---|---|---|
| SKU ID | yes | Stable, unique |
| Product name | yes | Some intentionally empty/garbled (data quality issue) |
| Supplier name | yes | Some intentionally null (DQ issue) |
| Category | yes | patike / sandale / čizme / odeća |
| Gender | yes | muško / žensko / dečije |
| Cost price (NabavnaCena) | yes | Some intentionally 0 (DQ issue) |
| Sale price (ProdajnaCena) | yes | |
| Current stock | yes | |
| Minimum stock (MinimalnaKolicina) | yes | |
| Markdown / nivelacija events | optional | At least 5 products with a price reduction in the period |

### Sales data shape

| Segment | SKUs | Behaviour |
|---|---|---|
| **BOOST** candidates | 15 | Strong trend (+15%), margin >= 22%, velocity >= 0.8 u/day, stock gap > 0 |
| **REPLENISH** candidates | 20 | High velocity, stock gap > 0, margin acceptable |
| **MARKDOWN** candidates | 18 | Stale >= 45 days, low velocity < 0.15 u/day, bad trend |
| **DO_NOT_ORDER** candidates | 10 | Bad trend + low margin + excess stock |
| **WATCH** (ambiguous) | 37 | Normal sales, no clear signal |
| **INSUFFICIENT_DATA** | 10 | < 3 units sold in window |
| **FIX_DATA** | 10 | Missing supplier / cost / category - these generate DQ issues |

### Intentional data quality issues

These must be present to make the Data Quality screen meaningful:

| Issue type | Count | What triggers it |
|---|---|---|
| `missing_supplier_name` | 8 SKUs | `SupplierName` = null / empty |
| `missing_cost` | 5 SKUs | `NabavnaCena` = 0 |
| `missing_category` | 4 SKUs | `Kategorija` = null / empty |
| `zero_revenue_rows` | 6 SKUs | Sales rows with 0 revenue (returns, cancellations) |
| `ignored_rows` | 5 rows | Duplicate barcode or invalid row format |
| Fallback supplier period | 1 supplier | Fewer than the minimum rows for 30d; system uses 90d signal |

### Stock cover signal distribution (for Inventory screen)

| Signal | SKU count |
|---|---|
| `Zdrava pokrivenost` (Healthy) | 55 |
| `Rizik rasprodaje` (OOS risk) | 25 |
| `Bez rotacije` (No velocity / dead stock) | 28 |
| `insufficient_data` | 12 |

---

## 3. Demo mode rules

### Isolation

- Demo data lives in a dedicated seed dataset, not in production tables.
- A demo deploy (or a separate Postgres schema/database) is used; never mix
  with a real customer's import data.
- The banner **"Demo podaci - ne koristiti za poslovne odluke"** is shown on
  every analytics screen and every exported/printed report.
- Demo mode is activated by a feature flag (e.g. `DEMO_MODE=true` env var or
  an `appsettings.Demo.json` override), not by a runtime UI toggle.

### Data freshness

- The demo refresh timestamp is set to a fixed past date (e.g. yesterday
  at 02:00) so the Trust Header shows a realistic "last refreshed" signal,
  not a live timestamp.
- `dataFreshnessStatus` is `fresh` for most screens, `stale` for one
  supplier (to show the stale warning banner).

### Reset / reseed

- A single command or script resets the demo DB to the seed state.
- Proposed: `dotnet run --project Api -- demo:reseed` (or a dedicated
  PowerShell script `demo-reseed.ps1`).
- After reseed, the cache must be invalidated so fresh data is served.

### What demo mode must NOT do

- Must not allow export of real business data (no real customer records
  in the demo DB).
- Must not send real emails, webhooks, or notifications.
- Must not run the live worker pipeline against real MDB files.

---

## 4. 10-minute demo script

### Before the demo

- Open the app to `/analytics/data-quality`.
- Confirm the demo banner is visible.
- Confirm last refresh timestamp is shown in the Trust Header.

---

### Minute 1-2 - Data Quality (`/analytics/data-quality`)

**Goal:** Show that the system catches dirty data before making recommendations.

1. Point to the data quality summary: X issues across Y products.
2. Highlight the two worst issue types: missing supplier name (8 SKUs) and
   zero-cost products (5 SKUs).
3. Explain: "Products with missing cost price get `FIX_DATA` status - no
   recommendation is generated until the data is corrected. This prevents
   the system from acting on noise."
4. Show the Pilot Intake Report link (`/analytics/reports/pilot-intake`).
5. Mention: data quality score drives the trust level shown on every
   downstream screen.

### Minute 2-4 - Dashboard (`/analytics`)

**Goal:** Give the executive overview.

1. Trust Header: period, last refresh, data quality status -> "good with warnings".
2. Top KPIs: total revenue, margin contribution, number of active products.
3. Category breakdown: patike outperform, čizme underperform.
4. Supplier revenue split: top 2 suppliers = 60% of margin.
5. Point to the `BOOST` count (15 products) and `MARKDOWN` count (18 products).
6. "Everything you see here is calculated by the backend - the UI only
   displays what the system has already decided."

### Minute 4-6 - Supplier Scorecard (`/analytics/supplier`)

**Goal:** Show multi-supplier comparison and fallback handling.

1. Sort by revenue contribution descending.
2. Highlight Supplier A (strong): high confidence, `recommendationAllowed=true`.
3. Highlight Supplier F (fallback): 30d insufficient, shows 90d signal,
   banner says "Pomoćni signal - nije finalna preporuka".
4. Highlight Supplier G (missing name): shown as "Nepoznat dobavljač",
   `dataQualityStatus=critical`, recommendation blocked.
5. "The system never silently shows a weaker dataset as if it were the
   requested period. The fallback is always labelled."

### Minute 6-8 - Inventory Risk (`/analytics/inventory`)

**Goal:** Show OOS risk and dead stock decisions.

1. Filter by `Rizik rasprodaje` - 25 SKUs appear.
2. Click a REPLENISH SKU: show `StockGap`, `VelocityUnitsPerDay`, lost sales
   estimate, reason codes.
3. Filter by `Bez rotacije` - 28 SKUs.
4. Click a MARKDOWN SKU: show days since last sale (60+), low velocity,
   recommended markdown reason.
5. "The system tells you exactly why - not just what."

### Minute 8-9 - Add to Action Queue (`/analytics/actions`)

**Goal:** Show the decision workflow.

1. From an inventory REPLENISH row, click "Dodaj u akcije".
2. Action appears in `/analytics/actions` with status `pending`, source key,
   and recommendation reason.
3. Change status to `done`. Show `ResolvedAtUtc` is set.
4. "The action queue is the bridge between the recommendation and the buyer's
   workday. Every action is traceable back to the source signal."

### Minute 9-10 - Supplier Decision Report (`/analytics/supplier/report`)

**Goal:** Show the sales artefact that goes to the board.

1. Open the supplier report for the demo period.
2. Show: period header, last refresh date, data quality status, methodology
   note, fallback warning for Supplier F.
3. Click Print / Export to Excel.
4. "This is the document the buyer brings to the supplier negotiation.
   It has a timestamp, a data quality note and a methodology section -
   so the supplier cannot dispute the numbers."

---

## 5. Open implementation questions (for when demo mode is built)

| Question | Decision needed |
|---|---|
| Seed format | Static SQL seed files vs. a C# seeder vs. a JSON fixture? |
| Demo flag | `DEMO_MODE` env var vs. a dedicated `appsettings.Demo.json`? |
| Demo banner | Component-level check or middleware-injected response header? |
| Reseed command | `dotnet run` CLI verb, PowerShell script, or admin endpoint guarded by `DEMO_MODE`? |
| Cache invalidation on reseed | Call `RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix)` after seed completes. |
| Refresh timestamp | Fixed seed value in `AnalyticsRefreshRun` (TriggeredBy = `"demo-seed"`). |
| Report watermark | Add "DEMO" watermark to PDF/print stylesheet when `DEMO_MODE` is active. |
