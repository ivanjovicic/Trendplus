# Analytics UI/Table/Chart Reliability Audit Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only audit addendum

## Scope

This addendum continues the analytics reliability audits and focuses on UI/table/chart/export reliability rather than only SQL/backend formulas.

Reviewed surfaces:

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`
- `Klijent/clientapp/src/services/analyticsTableState.ts`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Api/Endpoints/DocumentEndpoints.cs`
- `Infrastructure/Services/Documents/DocumentRenderer.cs`

No runtime behavior was changed.

## Additional findings

### R39 - Derived category `revShare` uses ratio while UI/export expects percent units

Files:

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`

Observed:

- Derived category intelligence calculates `revShare = approxRevenue / totalRevenue`, rounded as a ratio such as `0.1234`.
- Legacy category intelligence returns `revShare` as percent units such as `12.34`.
- Insight Studio displays category `revShare` with `fmtPct(cat.revShare)` and marks the column as `dataType: "percent"`.
- `mergeCategorySignalsAsPrimary` makes derived analytics primary whenever price/inventory/demand signals exist.

Risk:

- The same UI column can display `12.3%` for legacy data and `0.1%` for derived data for the same true 12.3% share.
- Charts, table detail, export and decisions can be off by 100x depending on which data path is active.

Classification: likely high-impact UI/data contract bug.

Recommended prompt: RQ39.

### R40 - Supplier Decision export/detail can output raw ratio for percent fields shown as percent in the UI

Files:

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`
- `Api/Endpoints/DocumentEndpoints.cs`

Observed:

- Supplier Decision visual table displays `preMarkdownMarginPct` as `fmtPct(row.preMarkdownMarginPct * 100, 2)`.
- The export column definition marks `preMarkdownMarginPct` as `dataType: "percent"`, but the exported row still contains raw ratio value.
- The export payload resolves raw column values; the API converts JSON values to strings.

Risk:

- Visual table can show `35.00%`, while exported Excel/CSV/PDF row can contain `0.35` or `0.3500`.
- Analysts may sort/filter/export and read the wrong unit.

Classification: likely high-impact export contract bug.

Recommended prompt: RQ40.

### R41 - XLSX renderer writes all cells as inline strings, not typed numbers/currency/percent/date cells

File: `Infrastructure/Services/Documents/DocumentRenderer.cs`

Observed:

- XLSX renderer writes every cell with `t="inlineStr"`.
- It does not use `DocumentColumnDefinition.DataType` or `FormatHint` for numeric cells.

Risk:

- Excel exports are visually readable but not analytically reliable for pivoting, summing, sorting, filtering or formulas.
- Numeric-looking values can behave as text.

Classification: likely spreadsheet reliability gap.

Recommended prompt: RQ41.

### R42 - Detail snapshot stringifies raw values without data-type formatting

File: `Klijent/clientapp/src/services/analyticsTableState.ts`

Observed:

- `buildAnalyticsDetailSnapshot` stores field values using `String(rawValue)`.
- It marks currency/percent fields as highlighted but does not format them with `fmtRsd`, `fmtPct`, or ratio/percent normalization.

Risk:

- Detail drawers can show raw values like `0.35` for a percent field or unlocalized currency values.
- Detail view can disagree with the row/table view.

Classification: likely UI trust bug.

Recommended prompt: RQ42.

### R43 - Browser-stored report preview can be shown when durable backend report fails

Files:

- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `Klijent/clientapp/src/services/analyticsTableState.ts`

Observed:

- Print/report payloads are stored in browser localStorage for a limited TTL.
- Supplier Decision report page falls back to browser preview if backend report fails and legacy payload exists.
- There is a warning banner, but the report still renders and can be printed/exported from the preview surface.

Risk:

- A stale browser snapshot can be mistaken for current backend-verified data.
- This is especially risky if filters or backend data changed after the local snapshot was saved.

Classification: suspicious; warning exists, but stronger watermark/disable rules may be needed.

Recommended prompt: RQ43.

### R44 - `changeBadge` displays zero/no-baseline changes as positive/up signal

File: `Klijent/clientapp/src/pages/InsightStudioPage.tsx`

Observed:

- `changeBadge` uses `change >= 0` to choose the upward arrow and success color.
- Several backend audit findings already showed no-baseline values can be encoded as `0` or `100`.

Risk:

- A no-baseline or neutral 0.0% change can be displayed as an up/positive signal.
- This compounds backend zero-baseline issues in the chart layer.

Classification: UI semantics bug.

Recommended prompt: RQ44.

### R45 - KPI margin card hides margin coverage even when backend supplies it

Files:

- `Api/Endpoints/InsightStudioEndpoints.cs`
- `Klijent/clientapp/src/services/insightStudioApi.ts`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`

Observed:

- Backend KPI snapshot returns `marginDataCoveragePct` and `revenueWithCost`.
- Frontend `KpiSnapshot` type does not expose those fields.
- KPI card displays `Bruto marža` as a single trusted percent.

Risk:

- A low-coverage estimated margin can look as trustworthy as a fully cost-backed margin.
- Users may trust profitability cards without seeing cost evidence.

Classification: UI trust-contract gap.

Recommended prompt: RQ45.

### R46 - AnalyticsTableToolbar exports only configured visible columns, so hidden trust metadata is lost

Files:

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`
- `Klijent/clientapp/src/services/analyticsTableState.ts`

Observed:

- Export payload is built from the configured `columns` array only.
- Several Insight Studio column arrays omit trust fields such as coverage, source status, data quality, cost availability or derived/legacy source marker.

Risk:

- Exported reports can remove the very metadata needed to judge reliability.
- A user can export a clean-looking table with no indication that some values were derived, estimated, fallback, low coverage, or no-baseline.

Classification: report trust gap.

Recommended prompt: RQ46.

### R47 - Supplier action source key omits some filters that affect the visible scorecard context

File: `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`

Observed:

- Active filters include `seasonId`, `minRevenue`, `onlyHighConfidence`, `supplierId`, `storeId`, `dataScope`, dates.
- `buildSupplierActionSourceKey` includes supplier, date range, store and dataScope, but not season/minRevenue/onlyHighConfidence.

Risk:

- Actions created from different scorecard contexts can collide or appear already queued.
- Users may believe an action corresponds to the current filtered scorecard when it was created under different filter semantics.

Classification: likely action/data lineage bug.

Recommended prompt: RQ47.

### R48 - Supplier action queue duplicate guard only checks first 200 open actions per status

File: `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`

Observed:

- UI checks open actions by fetching `pageSize: 200` for each open status.
- If there are more than 200 open actions for a status, some source keys are not loaded into the local duplicate guard.

Risk:

- UI can allow duplicate action creation even though an open action already exists beyond page 1.
- Backend idempotency may still protect some cases, but UI state can be wrong.

Classification: medium-priority UI/action reliability gap.

Recommended prompt: RQ48.

### R49 - Derived smart reorder is mapped back to legacy `totalReorderValue` using cost semantics under an old field name

File: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

Observed:

- `buildLegacyReorderFallbackFromSignals` maps `smart.summary.totalReorderCost` to `totalReorderValue`.
- Legacy reorder uses `totalReorderValue` as selling-price potential value.

Risk:

- The same `totalReorderValue` field can mean sales-value in legacy backend and cost-value in derived fallback.
- UI labels and exports can silently mix cost vs revenue semantics.

Classification: likely field semantic drift.

Recommended prompt: RQ49.

### R50 - Chart/table truncation hides tail values without explicit “top N + rest” semantics in several places

Files:

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`

Observed:

- Several charts use `.slice(0, 8)` or show only top items, while adjacent tables/export may contain all rows.
- Some charts include no explicit “top N only” subtitle or rest bucket.

Risk:

- Chart totals can be visually interpreted as whole-dataset totals.
- This is lower priority than formula bugs, but important for executive dashboards.

Classification: low/medium chart interpretation gap.

Recommended prompt: RQ50.

## Priority order

1. RQ39 - category share ratio/percent mismatch.
2. RQ40 - Supplier Decision percent export/detail mismatch.
3. RQ41/RQ42 - export/detail typed formatting reliability.
4. RQ45/RQ46 - trust metadata visible in cards/tables/exports.
5. RQ47/RQ48 - action lineage/duplicate guard reliability.
6. RQ43 - stale browser report preview hardening.
7. RQ44/RQ50 - chart semantics and visual interpretation guardrails.
8. RQ49 - legacy/derived reorder value semantic drift.

## Recommendation

Keep this as another WAITING addendum until the main reliability queue is advanced. If UI/report trust becomes the next priority, implement RQ39 and RQ40 before cosmetic chart work because they can change displayed/exported numbers by 100x or by field semantics.
