# Markdown Optimizer MVP Audit

Updated: 2026-06-19
HEAD SHA: `8ad00a56ff6feaecea39d84032625c8303163108`

## Scope Reviewed

- [Retail Analytics KPI Roadmap](../analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md)
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`
- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`

## Current Behavior

- There is no standalone markdown optimizer endpoint or simulator page yet.
- The current markdown-related surfaces are decision support surfaces:
  - Product Decision Center
  - Pre-nivelacija priority workflow
  - supplier/report views that explain markdown dependency
- The roadmap frames markdown optimizer as a scenario tool, not a guaranteed optimizer.
- Product Decision Center keeps expected impact nullable when the backend does not provide a reliable value.
- Pre-nivelacija priority now excludes `insufficient_data` candidates from the high-priority count and filter.

## Findings

### SAFE

- `ProductDecisionCenterPage` does not invent expected impact when `expectedImpactRsd` is missing.
- Existing confidence tests already cover missing-impact and insufficient-data cases.
- `analyticsMetricDefinitions` labels `markdownDependency` as a dependency signal, not a guaranteed profit metric.

### WATCH

- The term `Markdown šansa` is still present in the pre-nivelacija drilldown, but it is presented as a component of the score breakdown, not as a promise.
- The roadmap still describes markdown optimizer as a future scenario tool; there is no dedicated optimizer UI to overstate yet.

### BUG

- `PreNivelacijaPriorityPage` previously counted and filtered `priorityBand === "high"` rows without excluding `insufficient_data`.
- That could make a weak or insufficient signal appear as a fast high-priority action.

## Fixes Made

- High-priority counting/filtering now excludes `insufficient_data` rows on the pre-nivelacija page.
- Added a focused test that verifies an `insufficient_data` candidate with `priorityBand=high` does not show up as high priority.

## Test Coverage

- Existing Product Decision Center confidence coverage:
  - missing expected impact remains unavailable
  - insufficient-data recommendations do not fake a strong impact
- New pre-nivelacija coverage:
  - high-priority UI does not rank `insufficient_data` rows as urgent

## Known Limitations

- The app still does not have a dedicated markdown optimizer simulator page.
- Markdown-related wording must stay scenario-oriented until a true optimizer contract exists.
- No forecasting/modeling logic was added in this task.

## Follow-Ups

- Add a dedicated markdown scenario/simulator surface only when the data contract and trust states are explicit.
- Keep markdown-related labels tied to signal, scenario, or estimate language.
