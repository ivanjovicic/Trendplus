Task ID: analytics-indicator-audit-2026-09-03
Queue: direct-user-request
Date: 2026-09-03
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 69511be0229d326bb302e59833df0ae36c7560ee
Main verification: passed - origin/main was pushed from and contains 69511be0229d326bb302e59833df0ae36c7560ee
Evidence state: synchronized

## What was done
- Audited analytics indicator calculations for incomplete history, null-to-zero fallbacks, invalid percentage bases and recommendation trust propagation.
- MA7/MA30 now remain unavailable until the complete window exists; empty/partial history is not presented as a valid zero or full moving average.
- Analytics Details, dashboard quick insights, risk cards and chart tooltips preserve unavailable values instead of inventing zeroes.
- PoP and anomaly indicators keep a missing zero-base comparison unavailable.
- Pre/post nivelacija coverage keeps missing coverage explicit in concentration/readiness messaging.
- Dashboard aggregate actions no longer re-enable a blocked product signal merely because it has a recommendation status.
- Added focused regression coverage for all confirmed patterns.

## Files changed
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Api.Tests/CachedAnalyticsDashboardActionTrustTests.cs
- Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx
- Klijent/clientapp/src/pages/AnalyticsDashboard.tsx
- Klijent/clientapp/src/pages/AnalyticsDetails.tsx
- Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx
- Klijent/clientapp/src/pages/DailySalesStatsPage.tsx
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx
- Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx
- Klijent/clientapp/src/utils/analyticsFormatters.ts
- Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts
- Klijent/clientapp/src/pages/__tests__/analyticsIndicatorRegression.spec.ts

## Validation run
- `npm run test:run -- src/pages/__tests__/analyticsIndicatorRegression.spec.ts` -> pass after fix; 5 tests.
- `npm run test:run -- src/utils/__tests__/analyticsformatters.spec.ts` -> pass; 7 tests.
- Focused frontend analytics tests (five selected page/formatter suites) -> pass; 30 tests.
- `npm run test:analytics -- --reporter=dot` -> pass; completed without failed tests, with existing React act warnings in an unrelated pre/post test.
- `npm run check:analytics-guardrails` -> pass; encoding, guardrails and TypeScript check passed.
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~InventorySignalCalculatorTests|FullyQualifiedName~CachedAnalyticsDashboardActionTrustTests|FullyQualifiedName~AnalyticsSalesReadinessRegressionTests" --no-restore` -> pass; 19 tests.
- `npm run build` -> pass; Vite production build completed successfully.
- `git diff --check` -> pass.
- `git push origin main` -> pass; `fd10ef2f..69511be0 main -> main`.

## Validation not run
- Full repository test suite -> not run; the focused analytics suites, guardrails and production build covered the changed owners, while unrelated full-suite runtime was outside the narrow audit proof.
- Live Vercel deployment/browser verification -> not run; this task delivered repository code to `main`, not a deployment operation.

## Documentation impact
- No owner documentation required changes; implementation and regression evidence are captured in this run log.

## What was missed
- No schema or migration change was made; the previously observed Render worker/refresh and source-period issues remain operational/backend data-pipeline follow-up items.
- No production data was mutated.

## Risks
- Existing repository compiler/analyzer warnings and the known large Recharts bundle warning remain; they did not fail this build and are unrelated to the audited formulas.
- MA30 will correctly show unavailable when fewer than 30 daily points exist; the source pipeline still needs a reliable refresh/history process to make that signal available.

## Next
- After deployment/refresh, recheck the live dashboard and Data Quality period/freshness consistency; owner: analytics data pipeline/operations.
