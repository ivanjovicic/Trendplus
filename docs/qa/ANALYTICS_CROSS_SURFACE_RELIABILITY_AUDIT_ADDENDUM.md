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

- `mapRecommendationStatus` maps backend `insufficient_data` to local status `Zadrzi`.
- The UI status enum only has `Pojacaj`, `Zadrzi`, `Smanji`.

Risk:

- A backend decision saying “insufficient data” can look like a valid “hold/maintain” decision.
- This weakens the no-fake-green/no-fake-decision rule.

Classification: likely high-impact recommendation-semantics bug.

Recommended prompt: RQ51.

### R52 - Color analytics can fall back to a local recommendation formula when backend recommendation is missing

File: `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`

Observed:

- If a backend recommendation exists, the page uses it.
- If it does not, the page computes `reliabilityScore`, `shareScore`, `marginNorm`, `popNorm`, `decisionScoreValue` and maps that to `Pojacaj`/`Zadrzi`/`Smanji`.

Risk:

- A missing backend recommendation can silently become a frontend-generated business recommendation.
- This reintroduces heuristic recommendations that other reliability work has tried to remove from UI.

Classification: likely frontend trust bug.

Recommended prompt: RQ52.

### R53 - ShoeType and Color pages do not pass `dataScope` to list APIs, but detail URLs include a dataScope

Files:

- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts`
- `Klijent/clientapp/src/services/colorSalesStatsApi.ts`

Observed:

- The services support `dataScope` and append it to the API query.
- The list pages call the APIs without `dataScope`.
- Detail navigation adds `dataScope = getDataScope()` to the URL.

Risk:

- List and detail can refer to different data scopes.
- If global `dataScope` is not `all`, the page may still load `all` data while detail/report URLs imply a narrower scope.

Classification: likely cross-surface filter contract bug.

Recommended prompt: RQ53.

### R54 - Vendor pre/post nivelacija page does not expose/pass `dataScope` or `storeId` even though the API contract supports them

Files:

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts`

Observed:

- The service contract supports `storeId` and `dataScope`.
- The page filters only by date, vendor and category, and calls current/previous API requests without `storeId` or `dataScope`.

Risk:

- This page can disagree with pages that are scoped by store/data origin.
- In multi-store or pilot-data scenarios, pre/post conclusions can be based on a broader dataset than the user expects.

Classification: likely filter/data lineage gap.

Recommended prompt: RQ54.

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

### R62 - Vendor pre/post previous-period failure silently degrades comparison metrics to N/A

File: `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`

Observed:

- Current and previous period requests are loaded with `Promise.allSettled`.
- If current succeeds and previous fails, the page keeps current data but sets previous data/revenue to null.
- Growth and volatility then show N/A/new-baseline style results without a prominent “previous comparison unavailable due to request failure” warning.

Risk:

- A transport/API failure for previous-period data can be mistaken for a true lack of baseline.
- This weakens period-over-period trust.

Classification: medium-priority comparison-lineage bug.

Recommended prompt: RQ62.

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
