# Analytics Pilot Screen Data Availability Matrix

Date: 2026-08-22
Owner prompt: `RQ110`
Sources:
- `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md`
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22.md`
- current backend contract/integration tests named below

This matrix is the citable owner-supplied route/filter contract for the RQ110 pilot screen-data sequence. It reuses the STAB15 smoke contract where the operator-facing route/filter list already exists and anchors each screen to the nearest deterministic backend proof. Where the exact physical table/view/materialized-view name is not explicitly named in current evidence, that gap is called out instead of inventing a new source-of-truth label.

## Matrix

| Screen | Known authoritative source surface | Refresh owner | Canonical period / scope filters | Allowed successful-empty reasons | Deterministic seeded proof basis | Gap / note |
|---|---|---|---|---|---|---|
| Dashboard | `/api/analytics/cached/dashboard/bootstrap`, `/api/analytics/refresh-status`, dashboard cache family | `NightlyAnalyticsRefreshWorker`, `AnalyticsAggregationWorker`, `AnalyticsDataQualityHealthWorker` | Smoke uses current route state plus `dataScope=all` for refresh status; dashboard bootstrap is route-driven | partial dashboard sections, explicit warning state, honest unknown freshness, explicit empty source state | `AnalyticsResponseMetaContractTests.DashboardBootstrap_*`, `AnalyticsSalesReadinessRegressionTests.DashboardBootstrap_PartialData_ReturnsWarningMeta`, `CachedAnalyticsOperationalFallbackTests.DashboardBootstrap_InventoryOperationalFallbackIsVisibleInMeta`, STAB15 dashboard smoke | Exact physical backing object is not separately named in the reviewed evidence; current proof is strongest at route/meta and smoke level |
| Product Decision Center | `/api/analytics/cached/products/decision-center`, `/analytics/products` | import + nightly refresh path that clears `product-decision-center` | `fromDate`, `toDate`, `top`, `dataScope=all` | `no_rows_for_period`, insufficient data, explicit error meta on failure | `ProductDecisionCenterBuilderIntegrationTests`, `ProductDecisionCenterSummaryDenominatorTests`, `ProductDecisionCenterIgnoredRowsContractTests`, `AnalyticsResponseMetaContractTests.ProductDecisionCenter_*`, STAB15 product-decision smoke | The backend contract is already deterministic; no gap named in current evidence |
| Executive Decision Board | `/api/analytics/decision-board`, `/analytics/decision-board` | contributing inventory, supplier, actions and refresh-status owners | `dataScope=all` on smoke; board aggregates internal family states rather than freeform filters | section-level no-signals states, honest blocker/warning states, explicit unavailable aggregate state | `DecisionBoardEndpointsTests`, `DecisionBoardAggregationContractTests`, `DecisionBoardDataQualityHealthEvaluationTests`, `AnalyticsCriticalRouteMappingsTests`, STAB15 decision-board smoke | Board is a composed surface; its proofs come from the contributing families plus board aggregation tests |
| Inventory | `/api/analytics/inventory`, `/analytics/inventory` | inventory refresh path and the cache invalidation owner for inventory family | store, supplier, search, page, pageSize, `dataScope`; date range where relevant | explicit empty success, insufficient data, unavailable dependency, partial/warning state | `InventoryListEndpointIntegrationTests`, `InventorySnapshotContractTests`, `ObservedInventoryDailySnapshotTests`, `AnalyticsMetaContractTests.Inventory_*`, STAB15 inventory smoke | Deterministic seeded proofs exist, but some inventory truth also depends on the observed-daily snapshot foundation |
| Supplier decision / sales | `/api/analytics/suppliers/decision-hub`, `/analytics/supplier`, `/analytics/supplier/report` | `AccessImportService`, `NightlyAnalyticsRefreshWorker`, report cache version bump, `AnalyticsDataQualityHealthWorker` for report trust | `fromDate`, `toDate`, `dataScope=all`, `tab=overview` | explicit empty dataset, low sample, unavailable report, warning/partial trust state | `SupplierDecisionHubContractTests`, `SupplierDecisionSchemaSqlTests`, `SupplierNegotiationPackReportTests`, `AnalyticsReportsContractTests`, STAB15 supplier smoke | `018_AddSupplierDecisionHubViews.sql` is the named SQL migration surface in the reviewed tests; reconciliation proof now has a dated note in `docs/qa/ANALYTICS_SUPPLIER_SUMMARY_DETAIL_RECONCILIATION_2026-08-24.md` |
| Analytics actions | `/api/analytics/actions`, `/analytics/actions` | action write path; no dedicated analytics cache family confirmed | `dataScope=all`, plus status/source-type filters for counts/details | explicit empty list, unavailable source state, warning state for partial/outcome gaps | `AnalyticsActionsCriticalWorkflowTests`, `AnalyticsActionsEndpointsTests`, `AnalyticsActionItemServiceTests`, `AnalyticsCriticalRouteMappingsTests`, STAB15 actions smoke | No dedicated action cache family was found; the proof is live-query and contract driven |

## Operator smoke contract

The owner-supplied smoke matrix from STAB15 is the explicit route/filter list for the production gate:

- `GET /health`
- `GET /ready`
- `GET /api/analytics/refresh-status?dataScope=all`
- `GET /api/analytics/actions?dataScope=all`
- `GET /api/analytics/cached/products/decision-center?fromDate=2026-08-01&toDate=2026-08-22&top=10&dataScope=all`
- `/analytics`
- `/analytics/pilot-readiness`
- `/analytics/products`
- `/analytics/supplier`
- `/analytics/inventory`
- `/analytics/data-quality`
- `/analytics/actions`
- `/analytics/decision-board`
- `/analytics/reports/pilot-intake?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all`
- `/analytics/supplier/report?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all`

## Known gap classification

The reviewed evidence is strongest for the seeded non-empty proof basis on Product Decision Center, Supplier Decision, Inventory, Actions, and Decision Board contract surfaces.

Dashboard remains the least isolated proof surface in the reviewed evidence:

- it has honest partial/error contract coverage;
- it has exact-deploy smoke proof on STAB15;
- it does not yet have a separate isolated seeded-non-empty backend proof named in the reviewed evidence pack.

If that gap becomes the next runtime follow-up, it should be split into a smaller dashboard/bootstrap proof prompt rather than broadening RQ110 further.
