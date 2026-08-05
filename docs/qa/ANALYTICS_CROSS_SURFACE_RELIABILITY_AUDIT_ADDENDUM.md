# Analytics Cross-Surface Reliability Audit Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only audit addendum

## Scope

This addendum continues the previous analytics reliability audits and focuses on cross-surface inconsistencies between analytics pages, tables, charts, exports and action queues.

Reviewed surfaces:

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- related analytics service contracts

No runtime behavior was changed.

## Additional findings

### R51 - Color analytics maps backend `insufficient_data` to `Zadrzi`

Files:

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`

Observed:

- `mapRecommendationStatus` mapped backend `insufficient_data` to local status `Zadrzi`.
- The UI status enum only had `Pojacaj`, `Zadrzi`, `Smanji`.

Risk:

- A backend decision saying “insufficient data” can look like a valid “hold/maintain” decision.
- This weakens the no-fake-green/no-fake-decision rule.

Classification: likely high-impact recommendation-semantics bug.

Recommended prompt: RQ51.

**Resolution (2026-08-05, RQ51 DONE):** `insufficient_data` maps to distinct `NedovoljnoPodataka` / “Nedovoljno podataka” (neutral `status-na` tone). Counts, export `getValue`, detail tooltip, and tests preserve the status; never shown as Zadrži.

### R52 - Color analytics can fall back to a local recommendation formula when backend recommendation is missing

File: `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`

Observed:

- If a backend recommendation exists, the page uses it.
- If it does not, the page computed `reliabilityScore`, `shareScore`, `marginNorm`, `popNorm`, `decisionScoreValue` and mapped that to `Pojacaj`/`Zadrzi`/`Smanji`.

Risk:

- A missing backend recommendation can silently become a frontend-generated business recommendation.
- This reintroduces heuristic recommendations that other reliability work has tried to remove from UI.

Classification: likely frontend trust bug.

Recommended prompt: RQ52.

**Resolution (2026-08-05, RQ52 DONE):** Local heuristic removed. Missing/unmapped backend recommendation maps to `NedovoljnoPodataka` with explicit non-decision reason; not counted as Pojačaj/Zadrži/Smanji.

### R53 - ShoeType and Color pages do not pass `dataScope` to list APIs, but detail URLs include a dataScope

Files:

- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts`
- `Klijent/clientapp/src/services/colorSalesStatsApi.ts`

Observed:

- The services support `dataScope` and append it to the API query.
- The list pages called the APIs without `dataScope`.
- Detail navigation adds `dataScope = getDataScope()` to the URL.

Risk:

- List and detail can refer to different data scopes.
- If global `dataScope` is not `all`, the page may still load `all` data while detail/report URLs imply a narrower scope.

Classification: likely cross-surface filter contract bug.

Recommended prompt: RQ53.

**Resolution (2026-08-05, RQ53 DONE):** List calls pass the same `dataScope` state used for detail/export/trust; pages reload on `trendplus:data-scope-changed`.

### R54 - Vendor pre/post nivelacija page does not expose/pass `dataScope` or `storeId` even though the API contract supports them

Files:

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts`

Observed:

- The service contract supports `storeId` and `dataScope`.
- The page filtered only by date, vendor and category, and called current/previous API requests without `storeId` or `dataScope`.

Risk:

- This page can disagree with pages that are scoped by store/data origin.
- In multi-store or pilot-data scenarios, pre/post conclusions can be based on a broader dataset than the user expects.

Classification: likely filter/data lineage gap.

Recommended prompt: RQ54.

**Resolution (2026-08-05, RQ54 DONE):** Page inherits global `dataScope` (reloads on change), exposes Objekat store filter, and passes both to current + previous requests; export/trust metadata declare scope/store.

### R55 - Supplier page can hide unknown suppliers while still using all-revenue denominators

File: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`

Observed:

- `includeUnknown=false` removes unknown supplier rows from `visibleSuppliers`.
- KPI/chart denominators still use `data.totals.ukupanPromet` from the full dataset.
- Top/concentration calculations use known visible rows but divide by or carry shares from full totals.

Risk:

- Visible supplier shares may not sum to 100%, and this may be correct mathematically but unclear visually.
- A user can interpret known-supplier concentration as full-dataset concentration while hidden unknown revenue remains in the denominator.

Classification: suspicious chart/table semantics gap.

Recommended prompt: RQ55.

### R56 - Supplier/ShoeType total cost fallback clamps impossible values to zero

Files:

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`

Observed:

- When backend `totalCost` is missing, UI falls back to `Math.max(0, revenueWithCost - marginContribution)`.

Risk:

- If input values are inconsistent, a negative implied cost becomes 0 instead of an explicit data-quality warning.
- Older or partial API responses can look cleaner than they are.

Classification: low/medium fake-zero guardrail gap.

Recommended prompt: RQ56.

### R57 - Inventory OOS/overstock risk sorting is client-side only for the current page

File: `Klijent/clientapp/src/pages/InventoryPage.tsx`

Observed:

- Server sorting maps `oosRisk`/`overstockRisk` to `kolicina`.
- The UI then sorts only the currently loaded page by forecast risk.

Risk:

- “OOS risk descending” does not mean the highest-risk SKU across all filtered inventory.
- Users can miss high-risk items on later pages.

Classification: likely table semantics bug.

Recommended prompt: RQ57.

**Resolution (2026-08-05, RQ57 DONE):** Explicit page-local labeling in sort options + warning when risk sort is active (multipage notes that higher-risk SKUs may be elsewhere). Server-side global risk sort deferred.

### R58 - Inventory “CSV ekran” can export `rows`, not the risk-sorted `displayedRows`

File: `Klijent/clientapp/src/pages/InventoryPage.tsx`

Observed:

- `displayedRows` is the risk-sorted page when sorting by `oosRisk` or `overstockRisk`.
- `exportVisibleCsv` exports `rows.map(...)`, not `displayedRows.map(...)`.

Risk:

- The exported “screen” CSV can have a different order from the table the user is seeing.
- This is especially misleading when the selected sort is risk-based.

Classification: likely export/UI mismatch.

Recommended prompt: RQ58.

**Resolution (2026-08-05, RQ58 DONE):** `exportVisibleCsv` now exports `displayedRows` (same order as the table). Filename includes risk sort token (`-oosRisk` / `-overstockRisk`); status message notes page-local risk order.

### R59 - Inventory signal-check actions can still receive an expected impact value

File: `Klijent/clientapp/src/pages/InventoryPage.tsx`

Observed:

- When `recommendationAllowed=false` or signal status is insufficient, the action is `SIGNAL_REVIEW`.
- The action still sets `expectedImpactRsd` via `resolveInventoryExpectedImpactRsd`.

Risk:

- A “check this weak signal” action can look like it has confirmed financial impact.
- This is similar in spirit to the Decision Board lost-sales fallback problem.

Classification: suspicious action-impact trust bug.

Recommended prompt: RQ59.

**Resolution (2026-08-05, RQ59 DONE):** `buildInventorySignalActionSpec` now sets `expectedImpactRsd: null` for all `SIGNAL_REVIEW` paths (including `recommendationAllowed=false` with positive inventory value). Actionable REPLENISH/SLOW_STOCK_REVIEW still attach impact when evidence exists.

### R60 - Inventory row value can become fake zero when cost and estimated value are missing

File: `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`

Observed:

- `unitCost = item.nabavnaCena ?? 0`.
- `estimatedValueAmount = item.estimatedValue ?? unitCost * positiveQuantity`.

Risk:

- Missing cost and missing estimated value become inventory value `0`.
- Inventory value, supplier value charts, CSV and risk panels can understate capital at risk without an explicit missing-cost warning per row.

Classification: likely fake-zero valuation gap.

Recommended prompt: RQ60.

**Resolution (2026-08-05, RQ60 DONE):** `buildInventoryRow` keeps `unitCost`/`estimatedValueAmount` as null when cost and estimate are missing and quantity > 0 (zero quantity remains true 0). CSV exports blank cost/value; supplier chart skips unknown rows; `formatCurrency(null)` shows "Nije dostupno".

### R61 - Inventory trust header freshness can use secondary-surface timestamps instead of primary table/balance freshness

File: `Klijent/clientapp/src/pages/InventoryPage.tsx`

Observed:

- `inventoryLastRefreshAt` is derived from action workflow, forecast, alerts, rebalance and store comparison timestamps.
- It does not include primary inventory list/balance/insights timestamps in that fallback list.
- `AnalyticsTrustHeader` uses `primaryMeta?.lastRefreshAtUtc ?? primaryMeta?.generatedAtUtc ?? inventoryLastRefreshAt`.

Risk:

- If the primary table metadata is missing, a newer forecast/alert timestamp can make the inventory table look fresher than it is.
- Different panels can have different freshness, but the header can show only one timestamp.

Classification: suspicious freshness/lineage bug.

Recommended prompt: RQ61.

**Resolution (2026-08-05, RQ61 DONE):** `InventoryPage` now resolves the header timestamp only from primary list/balance/insights meta timestamps and renders a separate freshness note when secondary panels are fresher or when primary freshness is missing. Added `InventoryPage.freshnessLineage.spec.tsx` to lock the no-fallback behavior.

### R62 - Vendor pre/post previous-period failure silently degrades comparison metrics to N/A

File: `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`

Observed:

- Current and previous period requests are loaded with `Promise.allSettled`.
- If current succeeds and previous fails, the page kept current data but set previous data/revenue to null.
- Growth and volatility then showed N/A/new-baseline style results without a prominent “previous comparison unavailable due to request failure” warning.

Risk:

- A transport/API failure for previous-period data can be mistaken for a true lack of baseline.
- This weakens period-over-period trust.

Classification: medium-priority comparison-lineage bug.

Recommended prompt: RQ62.

**Resolution (2026-08-05, RQ62 DONE):** `previousComparisonError` + warning banner; PoP/volatility show `Nedostupno` (never `Nova baza` on request failure).

### R63 - Vendor pre/post `top5SharePct` is share of absolute change, not share of revenue

File: `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`

Observed:

- `sharePct` is derived from `abs(changeRevenue) / totalAbsoluteChangeRevenue`.
- KPI label says “Top 5 udeo u promeni”, which is mostly correct, but the variable and generic percent formatting can be confused with revenue share.

Risk:

- Lower priority, but exports/details using generic `sharePct` can be misread as revenue share.
- Needs stronger field naming such as `absoluteChangeSharePct`.

Classification: low/medium naming/semantic clarity gap.

Recommended prompt: RQ63.

## Priority order

1. RQ51 - Color insufficient_data must not become Zadrzi.
2. RQ57/RQ58 - Inventory risk sort/export mismatch.
3. RQ53/RQ54 - Missing dataScope/store lineage in ShoeType/Color/Vendor pages.
4. RQ59/RQ60 - Inventory action impact and fake-zero valuation.
5. RQ52 - Remove or clearly label Color frontend recommendation fallback.
6. RQ61/RQ62 - Freshness/comparison-lineage warnings.
7. RQ55/RQ56/RQ63 - denominator, cost fallback and naming clarity.

## Recommendation

Keep this addendum WAITING. If implementation continues after RQ01 and the UI/export P0 fixes, prioritize RQ51 and RQ57/RQ58 because they can directly mislead operational decisions.
