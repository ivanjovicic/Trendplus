# Architecture Boundaries

This document maps the main backend and frontend layers so agents can follow the existing architecture instead of rebuilding it locally in pages or one-off helpers.

## Backend layers

1. Api endpoints
   Public HTTP contract, auth, parameter binding, response shape, and route registration.
2. Application/services/handlers
   Shared orchestration, refresh status assembly, decision composition, and reusable backend workflows.
3. Infrastructure EF contexts/services
   Database access, worker runtime services, cache administration, migrations, and runtime policies.
4. Domain/model/entities
   Persistent models, analytics action/outcome entities, and refresh-run records.
5. Workers/background jobs
   Heavy refresh, imports, prewarm, scheduling, and long-running maintenance tasks.

## Frontend layers

1. Pages
   Screen-level composition, filters, fetch orchestration, and safe UX states.
2. Services/API clients
   Fetch logic, endpoint URLs, DTO parsing, and request parameter shaping.
3. Shared components
   Reusable TrustHeader, ErrorState, EmptyState, tables, cards, banners, and report actions.
4. Utils/formatters
   Shared money/percent/quantity formatting and analytics meta interpretation helpers.
5. Tests
   Route smoke, page state, component contracts, and backend endpoint/service tests.

## Explicit boundaries

- Frontend displays decisions; backend computes decisions.
- Pages must not define local business scoring thresholds for recommendation, confidence, or reliability.
- Shared formatters live in `src/utils/analyticsFormatters.ts`.
- Response-meta interpretation lives in `src/utils/analyticsResponseMeta.ts`.
- Protected write UX mapping lives in `src/utils/analyticsActionWriteErrors.ts`.
- `AnalyticsTrustHeader`, `AnalyticsErrorState`, and `AnalyticsEmptyState` are shared UI primitives.
- Workers do heavy refresh and import work; the web app shows status and offers safe manual triggers.

## Module map

| Module | Route/screen | Backend endpoint/source | Main DTO/service | Shared UI/helpers | Tests | Do not change casually |
| --- | --- | --- | --- | --- | --- | --- |
| Analytics dashboard | `/analytics` | `/api/analytics/cached/dashboard/bootstrap`, cached dashboard and refresh endpoints | `src/services/analyticsApi.ts` | `AnalyticsTrustHeader`, `AnalyticsErrorState`, `AnalyticsEmptyState`, `analyticsFormatters`, `analyticsResponseMeta` | `src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx`, route smoke tests | Bootstrap contract, trust metadata, fake-zero behavior |
| Product Decision Center | `/analytics/products` | `/api/analytics/cached/products/decision-center` | `getProductDecisionCenter` in `src/services/analyticsApi.ts` | shared trust/error/empty states, `analyticsActionWriteErrors`, `analyticsFormatters` | `ProductDecisionCenterPage.confidence.spec.tsx`, `ProductDecisionCenterPage.queueStatus.spec.tsx`, `ProductDecisionCenterPage.actionStatusFallback.spec.tsx` | Backend recommendation semantics, confidence contract, action queue source keys |
| Supplier Scorecard / Supplier Decision Hub | `/analytics/supplier`, `/analytics/supplier-decision-hub` redirect path, `/analytics/supplier/report` | `/api/analytics/suppliers/decision-hub/*`, `/api/analytics/reports/supplier-decision` | `src/services/supplierDecisionHubApi.ts`, `src/services/supplierDecisionReport.ts`, `getSupplierDecisionDurableReport` | trust/error/empty states, supplier report actions, `analyticsFormatters`, `analyticsResponseMeta` | `SupplierDecisionHubPage.spec.tsx`, `SupplierConsolidatedPage.spec.tsx`, `SupplierDecisionReportPage.spec.tsx`, backend supplier report tests | Silent fallback behavior, `recommendationAllowed`, requested vs effective dataset, report cache/report section semantics |
| Inventory | `/analytics/inventory` | `/api/analytics/inventory/*`, `/api/analytics/cached/inventory/*` | `getInventoryBalance`, `getInventoryList`, `getInventoryInsights`, `getInventoryActionSuggestions` in `src/services/analyticsApi.ts` | trust/error/empty states, `analyticsActionWriteErrors`, `analyticsResponseMeta`, `InventoryPageShell` | `InventoryPage.queueStatus.spec.tsx`, `InventoryPage.forecastRestock.spec.tsx`, `InventoryPage.signalActions.spec.ts` | OOS/restock trust states, nullable impact, stale refresh warnings |
| Analytics Actions / Action Queue | `/analytics/actions` | `/api/analytics/actions/*` | `getAnalyticsActions`, `updateAnalyticsActionStatus`, `updateAnalyticsActionOutcome` in `src/services/analyticsApi.ts` | `AnalyticsTrustHeader`, `analyticsActionWriteErrors`, `analyticsFormatters` | `AnalyticsActionsPage.spec.tsx`, backend `AnalyticsActionsEndpointsTests.cs` | Write auth handling, audit/outcome semantics, source key and status workflow |
| Executive Decision Board | `/analytics/decision-board` | `/api/analytics/decision-board` | backend `DecisionBoardEndpoints.cs`, page composition in `ExecutiveDecisionBoardPage.tsx` | trust/error/empty states, `analyticsResponseMeta`, `analyticsFormatters` | `ExecutiveDecisionBoardPage.spec.ts`, backend `DecisionBoardEndpointsTests.cs` | Ranking semantics, dedupe, stale/partial visibility, aggregate contract |
| Data Quality | `/analytics/data-quality`, `/analytics/pilot-readiness`, `/analytics/reports/pilot-intake` | `/api/analytics/data-quality/*`, `/api/analytics/reports/pilot-intake`, `/api/analytics/refresh-status` | `getDataQualityIssues`, `getAnalyticsDataQualityHealth`, `getPilotDataQualityIntakeReport` in `src/services/analyticsApi.ts` | trust/error/empty states, `analyticsResponseMeta`, readiness/report components | `PilotReadinessPage.spec.tsx`, `AnalyticsSalesReadinessRegression.spec.tsx`, backend `DataQualityIssuesHandlerTests.cs` | Freshness semantics, empty vs insufficient vs error, intake report trust messaging |
| Reports / Supplier Decision Report | `/analytics/supplier/report`, `/analytics/reports/pilot-intake` | `/api/analytics/reports/supplier-decision`, `/api/analytics/reports/pilot-intake`, document/export endpoints where used | `SupplierDecisionReportPage.tsx`, `PilotIntakeReportPage.tsx`, `src/services/supplierDecisionReport.ts` | report actions, print/export handling, trust/error/empty states | `SupplierDecisionReportPage.spec.tsx`, backend `AnalyticsReportsContractTests.cs`, `SupplierNegotiationPackReportTests.cs` | Report methodology, warning visibility, expired preview/export fallback |
| Workers / refresh | `/admin/configuration` workers panel, refresh banners across analytics | `/api/workers/configuration`, `/api/analytics/refresh-status`, admin worker routes, worker hosted services | `src/services/workerApi.ts`, backend `AnalyticsRefreshStatusService`, worker hosted services | `WorkersPanel`, `AnalyticsRefreshStatusBanner`, worker alerts | `WorkersPanel.spec.tsx`, `workerApi.spec.ts`, backend refresh status tests | Web vs worker ownership, heavy refresh location, runtime control and freshness truth |

## Do not overwrite architecture

- Do not replace lazy/Suspense route structure just to satisfy tests.
- Do not remove legacy or admin compatibility routes without a redirect plan and smoke coverage.
- Do not introduce parallel formatters, response-meta interpreters, or trust components.
- Do not bypass backend DTO/service patterns by building endpoint URLs ad hoc inside pages.
- Do not put audit history into `Description` fields when structured action/outcome metadata exists or is planned.
- Do not move heavy refresh logic from worker-owned flows into page render or page-triggered loops.
