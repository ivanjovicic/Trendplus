# Analytics Trust Screen Matrix

Date: 2026-09-04
Owner: direct user request
Status: hardening evidence; production deployment proof is recorded separately.

Potvrđeno znači da postoji izvršni ugovor ili direktan evidence zapis. Ne znači da je trenutno Render izdanje potvrđeno.

## Screen Mapping

| Screen | React page/component | API client | Endpoint | DTO/response | Backend/service/query | Table/view/migration | Cache/refresh | Existing proof |
|---|---|---|---|---|---|---|---|---|
| /analytics | AnalyticsDashboard, AnalyticsDashboardCharts | analyticsApi.ts | /api/analytics/cached/dashboard/bootstrap | dashboard response, AnalyticsResponseMetaDto | CachedAnalyticsEndpoints.cs | sales/inventory entities and cached aggregates | dashboard cache, aggregation and refresh workers | dashboard regression, seeded and fallback tests |
| /analytics/products | ProductDecisionCenterPage | analyticsApi.ts | /api/analytics/cached/products/decision-center | ProductDecisionCenter response/why payload | PDC builder, CachedAnalyticsEndpoints.cs | PDC cache/materialized sources | import/nightly invalidation | confidence, denominator and action tests |
| /analytics/supplier | SupplierSalesStatsPage, SupplierDecisionHubPage | supplierSalesStatsApi.ts, supplierDecisionHubApi.ts | /api/analytics/supplier-sales-stats; /api/analytics/suppliers/decision-hub/* | supplier stats, ranking/summary | AllEndpoints.cs, SupplierDecisionHubEndpoints.cs | ProdajaZaglavlja, ProdajaStavke, Artikli, Dobavljaci, supplier views | supplier cache, import/nightly refresh | supplier, schema, report and stats tests |
| /analytics/inventory | InventoryPage, InventoryPageShell | analyticsApi.ts | /api/analytics/inventory/*, /api/analytics/cached/inventory/* | inventory/list/insights/forecast | InventoryEndpoints.cs, CachedAnalyticsEndpoints.cs | inventory entities and observed snapshots | inventory cache and worker | inventory snapshot/list/forecast tests |
| /analytics/actions | AnalyticsActionsPage | analyticsApi.ts | /api/analytics/actions/* | action/outcome and ledger DTOs | AnalyticsActionItemService.cs, AnalyticsActionsEndpoints.cs | action/outcome tables | live action query and write path | action lifecycle/outcome tests |
| /analytics/decision-board | ExecutiveDecisionBoardPage | analyticsApi.ts | /api/analytics/decision-board | DecisionBoardResponse/cards | DecisionBoardEndpoints.cs | contributing family sources | contributing caches and refresh status | board aggregation/trust tests |
| /analytics/data-quality | DataQualityPage | analyticsApi.ts | /api/analytics/data-quality/* | health/issues/offenders/trend | DataQualityEndpoints.cs, health service | quality snapshots/issues | quality snapshot worker | quality consistency/health tests |
| /analytics/reports | SupplierDecisionReportPage and report components | supplierDecisionReport.ts, analyticsApi.ts | /api/analytics/reports/supplier-decision | report/trust payload | AnalyticsReportsEndpoints.cs and report services | report snapshot/cache and supplier views | report cache generation and refresh | report/export/negotiation tests |
| /analytics/reports/pilot-intake | PilotIntakeReportPage | analyticsApi.ts | /api/analytics/reports/pilot-intake | intake report/quality metadata | DataQualityEndpoints.cs | quality snapshots and source entities | quality worker and refresh status | readiness/report tests |
| Trend | AnalyticsDetails, dashboard and sales stats pages | analyticsApi.ts and screen clients | cached sales/trend endpoints | trend series/meta | cached sales and DailySalesStatsService | sales entities/aggregates | sales cache and refresh | indicator/sales tests; full parity remains open |
| Forecast | Inventory forecast sections | analyticsApi.ts | /api/analytics/cached/inventory/forecast and backtest | forecast/backtest response | inventory forecast handlers | observed snapshots/forecast materialization | inventory forecast cache/worker | forecast pairing tests |
| Pre/post nivelacija | ProdajaPrePostNivelacijePage, SupplierFootwearAnalyticsPage | vendorSalesNivelacijaApi.ts, supplierSalesStatsApi.ts | /api/analytics/vendor-sales-nivelacija and supplier-sales-stats | nivelacija response/recommendation | AllEndpoints.cs, split policy, decision engine | vw_vendor_sales_nivelacija and price/sales entities | heavy cache and snapshot cost batch | split/indicator/nivelacija tests |

## Trust Matrix

| Screen | Potvrđeno | Traženi period | Efektivni period | Posmatrani period | Data scope | Vreme generisanja | Poslednji uspešan refresh | Freshness | Quality | Empty/partial/error | Recommendation allowed | Razlog ograničenja |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| /analytics | Delimično | request filters | bootstrap meta | source windows | propagated on core calls | response generatedAt | refresh-status only | stale/unknown visible | worst contributor | explicit states tested | backend action payload | period/deploy parity open |
| /analytics/products | Delimično | from/to/top/scope | PDC meta | PDC observation window | cache identity | PDC generatedAt | refresh/cache metadata | meta + refresh | backend PDC quality | empty/insufficient/error | row gate | RQ128 deployed parity |
| /analytics/supplier | Delimično | from/to/store/supplier/season/scope | scorecard/stats meta | sales/scorecard window | all/existing/imported | response generatedAt | refresh-status only | unknown/stale/partial | warning/critical/insufficient | empty/fallback/error | backend response gate | runtime schema and RQ137 |
| /analytics/inventory | Delimično | inventory/forecast filters | inventory meta | observed snapshot window | cache scope | endpoint generatedAt | refresh/snapshot metadata | stale/unknown | insufficient/critical | empty/partial/dependency | backend signal | worker/snapshot runtime |
| /analytics/actions | Da za scoped contract | list and outcome filters | action response | creation/resolution/measurement | source/scope retained | action generatedAt | no refresh snapshot | pending/partial/error | outcome gaps visible | empty != error | source decision gate | supplier dependency may be MISSING_TABLE |
| /analytics/decision-board | Delimično | contributing scopes | composed result | contributing windows | contributing scopes | board generatedAt | contributing refresh status | worst status | worst quality | section states | card payload | RQ128/STAB16 |
| /analytics/data-quality | Delimično | issue/trend filters | health snapshot | quality observation | scope explicit | report generatedAt | snapshot/refresh | stale/unknown/critical | health/blockers | empty issues != error | false when blocked | live worker proof |
| /analytics/reports | Delimično | report filters | report snapshot | report source period | payload scope | report generatedAt | report cache/refresh | expired/fallback visible | report trust | export/unavailable | report gate | runtime migration proof |
| Trend/Forecast/Pre-post | Delimično | screen-specific filters | endpoint response | daily/paired/event windows | endpoint-specific | response generatedAt | refresh/snapshot only | missing/stale visible | coverage/quality visible | no fake zero | backend payload | RQ138 and full parity |

## Open Acceptance

- RQ128: deployed PDC/Decision Board actionability parity.
- RQ132: Dashboard support-signal explanation.
- RQ137: canonical requested/effective/observed period lineage.
- RQ138: authoritative Trend Models evaluation contract.
- STAB16: worker/deployment/runtime parity, refresh history and production migration state.
- Browser console/theme/chart-size smoke and full frontend suite remain unproven when the local Vitest worker cannot start.

Hardening in this run fails closed for missing denominators, non-finite metrics, missing refresh evidence and blocked recommendations. It cannot create production refresh history or a missing database migration.
