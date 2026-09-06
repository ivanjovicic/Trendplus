# Analytics Reliability Audit: New Prompt Proposals

Date: 2026-09-06
Scope: analytics screens and their direct data contracts only
Queue integration: proposed WAITING entries; not promoted while `RQ163` is `IN_PROGRESS`

## Audit Basis

The audit re-read `AGENTS.md`, `docs/ai/ARCHITECTURE_BOUNDARIES.md`,
`docs/ai/VALIDATION_SELECTOR.md` and the queue protocol. It also inspected the
current source, nearest frontend tests and recent history for the affected
files. Recent related fixes include `e77af0ff` (dashboard fallback trust),
`bc71db45` (supplier cache/chart repair), `570a31e8` (pre/post comparability),
`7a3cc040` (unknown pre/post coverage) and the current `RQ163` supplier
post-observation work. None of those changes covers the findings below.

The canonical analytics queue currently has an active `RQ163` owner and must
retain one executable prompt. These proposals are deliberately recorded as
later `WAITING` candidates rather than creating a second `READY` pointer or
modifying the active owner's queue/lock.

## RQ229 - Fail closed on Dashboard aggregate chart numeric state

Status: WAITING
Priority: P1
Type: frontend/contract/tests
Feature family: dashboard-aggregate-chart-numeric-state
Owner: Analytics Dashboard
Parallel-safe: no, all Dashboard aggregate charts share the adapter boundary

### Problem

Dashboard category, gender, supplier, payment, weekday and hour chart
adapters can pass `null`, `undefined`, `NaN` or `Infinity` into Recharts. The
category reducer also evaluates `0 + null` as a measured zero. The tooltip
formatter says "Nije dostupno", but that only changes tooltip text and does
not protect axes, bars, pie slices or area data from non-finite values.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:1275-1341` builds the
  chart DTOs without a shared finite-value gate. The category reducer adds raw
  `item.totalRevenue`, and gender/supplier/payment values are passed through.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:1311-1328` fills absent
  weekday/hour groups with zero. This is a separate compatibility behavior
  and must not be changed without proving the backend group contract.
- `Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx:68-70`
  protects only formatted tooltip output.
- `Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx:118,
  139, 160, 181, 202, 229` gates most panels by array length or an
  `=== 0` check, not by finite measured values.
- Existing Dashboard integration/table tests mock charts or use finite happy
  path payloads and do not prove the non-finite chart boundary.

### Scope

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx`
- nearest Dashboard/chart regression tests
- only Dashboard aggregate chart and directly shared chart-data adapters

Do not change Daily Sales numeric semantics (`RQ154`), Dashboard trend
ranking (`RQ155`), forecast/Trend Models, or backend recommendation formulas.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ154`, `RQ155`, `RQ191` and `RQ145`
- `Klijent/clientapp/src/utils/analyticsMetricValue.ts`
- Dashboard and chart tests

### Do

1. Add a single display-boundary policy for finite measured chart values.
2. Keep a genuine finite zero as zero; represent missing, null, insufficient,
   `NaN` and `Infinity` as unavailable or as the established empty/degraded
   chart state.
3. Ensure sorting, aggregation and chart rendering cannot be poisoned by a
   non-finite input. Do not treat invalid input as a measured zero.
4. Preserve the existing weekday/hour zero-fill only where the backend
   explicitly means an absent bucket is a valid zero; otherwise expose the
   contract difference instead of guessing.
5. Keep the same value/state in chart, visible table/summary and export paths
   where those paths consume the same Dashboard response.

### Tests

- empty successful aggregate arrays
- genuine finite zero
- null and omitted numeric values
- zero from a missing denominator versus measured zero
- `NaN` and `Infinity` mixed with valid values and as the only values
- all aggregate chart families, including weekday/hour buckets
- table/chart/export parity for the same fixture
- Recharts container initial width/height `0` and `-1`
- dark, light and soft-gray theme rendering without console warnings

### Acceptance

- No Dashboard aggregate chart receives or renders non-finite numeric data.
- Unknown or invalid evidence never becomes a trusted zero.
- A real measured zero remains visible as zero.
- Empty success is not shown as a server error.
- Tooltip, axis, chart, table and export states agree.
- No recommendation, confidence or reliability semantics are invented in the
  frontend.

### Dependencies

- `RQ154` and `RQ155` remain owners of their narrower Dashboard/Daily Sales
  numeric behavior.
- `RQ145` remains the owner of complete cross-route parity.
- `RQ191` remains the shared percent-boundary follow-up.

## RQ230 - Reject malformed Dashboard periods without stale loading or data

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: dashboard-invalid-period-lifecycle
Owner: Analytics Dashboard
Parallel-safe: no, period validation and request lifecycle have one owner

### Problem

`AnalyticsDashboard` parses an invalid date by replacing it with the current
time. Consequently malformed input can look like a valid one-day period and
can be sent to the API. For a reversed period, `load` records an error and
returns before clearing loading state or stale data. During a prior request,
the stale-response guard can therefore leave a spinner and old numbers visible
under an invalid period.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:206-209` returns
  `new Date()` for invalid input instead of preserving an invalid state.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:645-653` derives range
  validity and day count from that fallback parser.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:713-718` returns on a
  reversed range before `setLoading(false)` and before clearing the prior
  result.
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.integration.spec.tsx`
  covers title/controls/basic rendering but not malformed, reversed or
  in-flight invalid-period transitions.

### Scope

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- nearest Dashboard control/integration tests
- only input validation and loading/data lifecycle for Dashboard periods

Do not duplicate `RQ208`'s divisor/timezone correction, `RQ161` Analytics
Details validation, or broad async ordering work owned by `RQ193`/`RQ194`.

### Read first

- `AGENTS.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ161`, `RQ193`, `RQ194`, `RQ208`
- Dashboard period helpers and control-bar tests

### Do

1. Distinguish empty, malformed, calendar-invalid and reversed user input.
2. Reject invalid input before any bootstrap, refresh-status or health request.
3. Ensure invalid input clears or suppresses stale data consistently and never
   leaves the page in an indefinite loading state.
4. Preserve a valid same-day period and exact inclusive day semantics.
5. Keep user-facing Serbian validation copy safe and free of raw backend codes.

### Tests

- valid same-day and multi-day periods
- empty and malformed values
- calendar-invalid values such as `2026-02-31`
- reversed periods
- invalid period while a previous request is pending
- no API call for invalid input
- no stale KPI/table/chart and no stuck spinner
- valid zero-period data remains distinct from invalid input

### Acceptance

- Dashboard never queries or displays a substituted period for malformed input.
- Reversed and malformed periods have an explicit user-facing state.
- Previous data cannot remain presented as if it belonged to the invalid range.
- Loading always terminates for the invalid-input path.
- Valid zero values and valid empty responses retain their existing semantics.

### Dependencies

- `RQ161` owns the established invalid-period vocabulary for Analytics Details.
- `RQ208` owns Dashboard calendar-day divisor semantics.
- `RQ193`/`RQ194` own broad multi-request race hardening; this prompt is only
  the invalid-period entry/exit lifecycle.

## RQ231 - Keep supplier analytics period validation fail-closed

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: supplier-period-validation
Owner: Supplier Analytics
Parallel-safe: no, supplier pages share period/deep-link state

### Problem

The supplier sales screen displays an invalid-range warning but its load
callback does not check that state. Explicit query/shared filters can therefore
populate `activeFilters` and still trigger an API request for a reversed range.
The supplier footwear screen has the same boundary for shared filters: its
`invalidRange` only disables the local Apply button, while the shared-filter
effect can write the invalid period directly into the active request state.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:610-613` computes
  `invalidRange`.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:616-640` accepts
  explicit query/shared dates into `activeFilters` without rejecting a
  reversed range.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:646-675` loads the
  active range without an invalid-period guard.
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:247` computes
  a local warning and `:255-277` applies shared filters without validating the
  range; `:291-303` then builds current and previous API ranges.
- Existing supplier tests cover successful/error/empty states but do not prove
  no request for reversed or malformed deep-link/shared periods.

### Scope

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- supplier period/deep-link tests and API request assertions

Do not change backend period formulas, pre/post comparability, supplier
recommendation ownership, or the broad lineage contract in `RQ137`.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `RQ137`, `RQ161`, `RQ163`, `RQ170`
- supplier shared-state and period parser helpers

### Do

1. Validate dates at the active-filter/request boundary, including dates from
   URL and shared embedded state.
2. Reject malformed, calendar-invalid and reversed ranges before current or
   previous supplier requests.
3. Keep the requested period visible and do not silently default it to a
   different period.
4. Keep empty success, API error, stale and partial responses distinct.
5. Preserve the same period and scope when opening supplier details and
   returning to the parent screen.

### Tests

- valid same-day and multi-day supplier periods
- reversed, empty, malformed and calendar-invalid query dates
- invalid shared embedded filters
- assert zero current/previous API calls for invalid input
- valid empty response versus request/error state
- detail deep-link period round-trip
- table/detail/export period metadata parity

### Acceptance

- Supplier analytics never queries a user-requested invalid period.
- No invalid period is silently replaced by the default or current date.
- Parent list, detail and export preserve the same accepted period and scope.
- Invalid input cannot leave stale supplier numbers presented as current.

### Dependencies

- `RQ137` owns cross-surface period lineage.
- `RQ163` owns supplier post-observation semantics and is currently active.
- `RQ170` owns Pilot Intake report period validation.

## RQ232 - Use the full supplier-footwear revenue denominator

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: supplier-footwear-share-denominator
Owner: Supplier Analytics
Parallel-safe: no, type concentration and dominant-type KPI share one owner

### Problem

The supplier-footwear helper first truncates global category revenue to eight
types and then calculates each type's percentage using only those eight as the
denominator. The chart and KPI are labelled as share of current revenue, but
the displayed shares are actually share of the top-eight subset. With more
than eight categories, the leading type is overstated and the chart sums to
100% even though omitted categories have revenue.

### Evidence

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:170-174`
  creates `globalTopTypes` with `.slice(0, 8)`, sums that truncated list into
  `globalTotal`, then calculates `sharePct` from the truncated denominator.
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:732` labels
  the KPI "Dominantan tip obuce", while `:737-746` labels the chart as top
  types by share of current revenue.
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
  covers the normal small fixture but not nine or more categories with a
  long-tail denominator.

### Scope

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- nearest supplier-footwear tests and chart/table/export projections if they
  consume this helper

Do not change price elasticity formulas, pre/post causal claims, forecast or
Trend Models, or the backend supplier decision recommendation.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `RQ140`, `RQ145`, `RQ163`, `RQ180`, `RQ182`
- supplier-footwear DTO and comparable-evidence helper

### Do

1. Calculate global type shares against the complete finite comparable revenue
   population, then truncate only the display rows.
2. Preserve unknown/non-comparable revenue as excluded evidence with a visible
   limitation; do not silently reallocate it to a known type.
3. Keep a genuine zero denominator as unavailable, not 100% or zero-share
   evidence.
4. Reuse one value/state for the dominant-type KPI, chart, table, detail and
   export where applicable.
5. Ensure non-finite revenue cannot affect ranking or denominator totals.

### Tests

- zero rows and all-unavailable comparable evidence
- one valid category with true 100% share
- nine-plus categories proving top-eight display with full-population shares
- missing, non-comparable, `NaN` and `Infinity` revenue
- true zero revenue versus missing denominator
- dominant-type KPI/chart/table/detail/export parity
- dark/light/soft-gray themes and chart container width/height `0`/`-1`

### Acceptance

- A displayed type share is a share of the full eligible revenue population,
  not a share of the truncated top-eight list.
- Omitted long-tail categories do not inflate the displayed percentages.
- Unknown and non-finite evidence remains unavailable and visible as a limit.
- KPI and chart agree numerically and preserve the same evidence basis.

### Dependencies

- `RQ140` owns causal comparability.
- `RQ145` owns complete cross-surface parity.
- `RQ163` owns the currently active supplier post-observation contract.

## Audit Conclusion

These four findings are new relative to the existing queue entries and are
limited to analytics screens or their direct presentation contracts. No
forecast, Shopify, Trend Models, Python, generic worker, migration or test-only
prompt was added. They remain proposed `WAITING` work until the active supplier
prompt is completed and the canonical queue owner advances the single READY
pointer.
