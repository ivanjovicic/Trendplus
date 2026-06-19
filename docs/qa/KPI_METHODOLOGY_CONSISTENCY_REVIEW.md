# KPI Methodology Consistency Review

Date: 2026-06-19 19:17 +02:00
Audit baseline: `a5d4f5f5ebb051a54d498e004b82f404df05cd69`
Status: review completed with focused guardrail tests added

## Purpose

This review checks that the retail analytics KPI names, formulas, and denominator handling stay consistent between the roadmap, shared helper layer, and the current frontend surfaces.

The goal is to prevent two regressions:

- missing denominator or missing ratio data turning into fake `0`
- roadmap formulas drifting away from the implementation without a clear note

## Files reviewed

- `docs/analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md`
- `docs/Analytics/KPI_METHODOLOGY_AUDIT.md`
- `docs/Analytics/STOCK_COVER_SELL_THROUGH_AUDIT.md`
- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`
- `Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts`

## Formula consistency summary

| Metric | Roadmap expectation | Code source | Status | Notes |
| --- | --- | --- | --- | --- |
| Sell-through ratio | `sold_units / (opening_stock_units + inbound_units)` | `analyticsMetricDefinitions.ts` | PASS | Formula matches the roadmap and has explicit denominator blocking. |
| Stock cover / days of supply | `current_on_hand_units / avg_daily_sales_units` | `analyticsMetricDefinitions.ts` | PASS | Formula matches the roadmap and is blocked when velocity is absent. |
| Inventory turnover | units or cost-based turnover with clear labeling | `analyticsMetricDefinitions.ts` | PASS / WATCH | Current implementation uses the cost-based variant; the roadmap allows this advanced form, but the unit-based shorthand should not be described as the same thing. |
| Gross margin % | `((revenue - cost) / revenue) * 100` | `analyticsMetricDefinitions.ts` | PASS | Consistent with the methodology audit. |
| Confidence / reliability | backend-provided metadata, not invented in UI | shared analytics helpers and page contracts | PASS | Frontend renders metadata; it should not synthesize trust values. |
| GMROI | future metric, not yet a stable contract | roadmap only | FOLLOW-UP | Keep out of the current contract until backend exposes a stable DTO and evidence. |

## Findings

### PASS

- Sell-through and stock cover formulas are already centralized and documented.
- Denominator-sensitive metrics have explicit `blockedWhen` guards.
- `fmtPctFromRatio` keeps missing values as fallback text instead of turning them into a percentage.
- The shared formatter layer already returns `N/A` for null/undefined numeric inputs.

### WATCH

- `inventoryTurnover` is intentionally cost-based in the codebase. That is acceptable because the roadmap lists both the unit-based proxy and the advanced cost-based variant, but we should avoid describing them as interchangeable.

### FOLLOW-UP

- GMROI should remain a roadmap item until backend fields are stable.
- If future docs introduce a units-based inventory turnover shorthand, it should be documented as a separate proxy, not a replacement for the current cost-based implementation.

## Tests added

- `Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts`
  - verifies `fmtPctFromRatio(null)` and `fmtPctFromRatio(undefined)` preserve fallback text
  - verifies a real ratio still formats correctly
- `Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`
  - verifies `sellThrough` and `stockCoverDays` formulas remain exact
  - verifies both metrics keep denominator guards in `blockedWhen`

## Conclusion

The current KPI methodology is consistent enough for the current pilot phase.

No formula rewrite was required. The only change needed for Q55 was to add focused guardrail tests so that missing denominator data cannot silently degrade into fake zero output.
