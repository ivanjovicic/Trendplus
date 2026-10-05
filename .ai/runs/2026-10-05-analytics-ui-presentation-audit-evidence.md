Task ID: analytics-ui-presentation-audit-2026-10-05
Queue: direct-user-request
Date: 2026-10-05
Agent/tool: Grok Bot (executor, `/workspace/rh`)
Delivery target: main
Base SHA: `4bdb4400135a6c2a6029a54d2bb26713a97b8503`
Main commit SHA: pending

## What was done
- Fresh fetch; tip `4bdb440` (newer than `7aed4aa`).
- Presentation audit vs design system (`N/A` → `Nije dostupno`), encoding guide, UX audit P-UI-50 intent.
- Fixed safe user-facing issues: shared unavailable label; formatter + table-state defaults; PDC/Actions/SupplierFootwear copy; Insight Studio diacritics + clear English headings; CSV UTF-8 BOM on Dashboard + decision timeline export.
- Did not rename confirmed product terms (Velocity kept where intentional).
- Prefer fix over new RQ.

## Files changed (high level)
- `Klijent/clientapp/src/utils/analyticsConstants.ts` (+ ANALYTICS_UNAVAILABLE_LABEL)
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Klijent/clientapp/src/utils/dailySalesPreviousPeriodComparison.ts`
- `Klijent/clientapp/src/utils/decisionTimelineExport.ts` (BOM)
- `Klijent/clientapp/src/services/analyticsTableState.ts`
- Pages: ProductDecisionCenter, AnalyticsActions, SupplierFootwear, InsightStudio, AnalyticsDashboard (BOM)
- Specs updated for Serbian unavailable + BOM
- `docs/qa/ANALYTICS_UI_PRESENTATION_AUDIT_2026-10-05.md`
- this evidence file

## Validation
- `npm run check:encoding` → OK
- Vitest: SupplierFootwear 21/21; formatters; tableState; decisionTimelineExport; unavailable label; dailySales comparison → green in focused runs

## Residuals
- Broader remaining `"N/A"` in GlobalTrends/Configuration/AnalyticsDetails and some English source labels (`dashboard`) — lower traffic / separate polish.
- Insight Studio still uses Velocity/OOS as product/metric terms by design; quarantined (RQ582).
- Full §0–154 parent brief text was not in executor storage; domains from Ivan summary + design system/UX audit were covered.

## Next
- Push via PC bundle FF.
