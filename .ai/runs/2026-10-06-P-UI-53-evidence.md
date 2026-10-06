Task ID: P-UI-53
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/p-ui-53-chart-accessibility / none
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Added a shared accessible chart frame that gives production charts a named figure, a concise summary of their already displayed projection, Recharts keyboard navigation, and a discoverable link when an equivalent data table exists.
- Applied the contract to 25 production analytics charts across dashboard, sales, supplier, shoe type, color, inventory, pre-nivelacija, pre/post and analytics details surfaces. Added a table alternative and keyboard supplier selection to the supplier decision quadrant.
- Added a repository checker and negative self-test so new Recharts charts must use the contract. Eight quarantined Insight Studio charts remain documented exceptions; the checker verifies their metric label/value precedes each mini-sparkline.
- Updated P-UI-38's waiting acceptance to consume the stable chart-a11y invariant produced here.
- Accessibility summaries use displayed labels and row counts only; no business values, recommendations or decision semantics were added.

## Files changed
- `Klijent/clientapp/package.json`
- `Klijent/clientapp/scripts/check-analytics-chart-accessibility.mjs`
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `Klijent/clientapp/src/components/analytics/AnalyticsChartAccessibility.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsChartAccessibility.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsDashboardCharts.accessibility.spec.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryPriorityPanels.tsx`
- `Klijent/clientapp/src/components/inventory/SizeCurveVisualization.tsx`
- `Klijent/clientapp/src/components/inventory/SizeCurveVisualization.accessibility.spec.tsx`
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionQuadrant.tsx`
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionQuadrant.accessibility.spec.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `.ai/runs/2026-10-06-P-UI-53-evidence.md`

## Validation run
- `npm run test -- --run src/components/analytics/__tests__/AnalyticsChartAccessibility.spec.tsx src/components/analytics/__tests__/AnalyticsDashboardCharts.accessibility.spec.tsx src/components/inventory/SizeCurveVisualization.accessibility.spec.tsx src/components/supplierDecisionHub/SupplierDecisionQuadrant.accessibility.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx` -> pass, 8 files / 139 tests.
- `npm run check:analytics-chart-accessibility` -> pass, 25 charts checked and 8 documented quarantined charts.
- `npm run check:analytics-chart-accessibility -- --self-test` -> pass; naked-chart negative fixture rejected.
- `npm run typecheck` -> pass.
- `npm run check:analytics-guardrails` -> pass; encoding, baseline self-test, 39 existing known findings / 0 removed, typecheck.
- `npm run build` -> pass; Vite reports the existing Recharts bundle-size warning (>500 kB).
- Agent-instructions, prompt-queues and planning-architecture validators and their self-tests -> pass.
- `git diff --check` -> pass.

## Validation not run
- Full frontend suite and browser/visual accessibility audit -> not run; focused chart/page behavior, static coverage, typecheck and production build provide the selected proof for this prompt.
- Remote Actions -> pending inspection after main delivery.

## Documentation impact
- Updated the owning P-UI queue and `MASTER_ROADMAP.md`; added the P-UI-38 invariant to its acceptance; this run log records proof and delivery.

## What was missed
- `InsightStudioPage.tsx` remains quarantined by its upstream prompt; eight sparklines receive the static label/value adjacency check but are not wrapped by the production chart frame.
- No visual screen-reader/browser session was available; behavioral proof is through semantic DOM tests and Recharts accessibility-layer integration.

## Risks
- Vite continues to warn that the existing Recharts chunk exceeds 500 kB. No chunk-splitting change was in scope.
- Remote Actions state is not yet inspected.

## Post-close routing recovery
- Recovery base `origin/main` SHA: pending post-delivery scan.
- Active owner queue/addendum files scanned: pending post-delivery scan of the full 16-file active RQ/SQL/P-UI/MASTER set.
- Completed/changed task IDs searched: P-UI-53 and dependency IDs whose state changed, pending scan.
- Newly satisfied dependencies / promoted successor: pending post-delivery scan.

## Next
- Pending full post-close cascade from fresh `origin/main`.
