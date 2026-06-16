# Stable Report URL Smoke

Datum: 2026-06-16
Repo: `ivanjovicic/Trendplus`
Task: audit stable report URLs used by Pilot Readiness

## Scope

- `Klijent/clientapp/src/pages/PilotReadinessPage.tsx`
- `Klijent/clientapp/src/pages/PilotIntakeReportPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`
- `Klijent/clientapp/src/__tests__/AppAnalyticsRoutes.spec.tsx`
- `Api/Endpoints/DataQualityEndpoints.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- analytics report/cache policy docs

## Current stable report routes

| Artifact | Stable frontend route | Source of truth | Registered | Notes |
|---|---|---|---|---|
| Pilot intake report | `/analytics/reports/pilot-intake?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD&scope=all` | `BuildPilotIntakeStableQueryUrl(...)` in `Api/Endpoints/DataQualityEndpoints.cs` | Yes | Legacy alias also exists: `/analytics/data-quality/pilot-intake-report` |
| Supplier decision report | `/analytics/supplier/report?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD&scope=all` | `BuildSupplierDecisionStableQueryUrl(...)` in `Api/Endpoints/SupplierDecisionHubEndpoints.cs` | Yes | Supports optional `supplierId`, `storeId`, `section` and filter params |
| Action outcome summary | No dedicated stable report route found | `GET /api/analytics/actions/outcomes/summary` | N/A | Current stable operator surface is `/analytics/actions`, not a standalone report URL |

## Pilot Readiness link audit

### 1. Pilot intake report

- `PilotReadinessPage` uses `/analytics/reports/pilot-intake` for the `Izvestaji spremni` card CTA.
- The route is registered in `Klijent/clientapp/src/App.tsx`.
- Backend also returns a stable query URL for durable pilot intake reports.
- `PilotIntakeReportPage` accepts both `scope` and compatibility `dataScope` query params, so old links do not hard-fail.
- Missing or unavailable report data does not render fake rows:
  - API/meta error without resolved report -> `AnalyticsErrorState`
  - expired/missing preview without durable payload -> `AnalyticsEmptyState`
- Freshness/trust is visible through `AnalyticsTrustHeader` and `AnalyticsRefreshStatusBanner`, including `lastRefreshAtUtc`, `dataFreshnessStatus`, and data quality.

Status: verified

### 2. Supplier decision report

- Supplier durable report route is registered as `/analytics/supplier/report`.
- Backend-generated `stableQueryUrl` also points to `/analytics/supplier/report?...`.
- `SupplierDecisionReportPage` accepts both `scope` and compatibility `dataScope`.
- Missing or unavailable report data does not render fake content:
  - backend error without payload -> `AnalyticsErrorState`
  - expired preview without durable payload -> `AnalyticsEmptyState`
- Report metadata is visible inside the rendered report:
  - generated time
  - last refresh
  - data quality
  - freshness when available

Status: verified

### 3. Action outcome report

- No dedicated report page or stable report URL was found for action outcomes.
- The current backend contract is a summary endpoint: `/api/analytics/actions/outcomes/summary`.
- The current stable operator destination is `/analytics/actions`.
- Pilot Readiness should not claim a durable action outcome report URL until a real report page exists.

Status: gap documented, no runtime bug found

## No-fake-zero / readiness behavior

- `PilotReadinessPage` does not mark reports as ready when both pilot and supplier reports are missing.
- If one report is missing, the `Izvestaji spremni` step degrades to warning instead of green.
- If report APIs are unavailable, report pages fall back to `AnalyticsErrorState` or `AnalyticsEmptyState`; they do not fabricate healthy rows.
- `unknown`/missing state is therefore not visually equivalent to success.

## Small mismatches fixed in this audit

1. Route smoke examples now use canonical `scope=all` query params instead of `dataScope=all`, matching backend-generated stable URLs.
2. `PilotReadinessPage` test fixture for supplier report now points to the real stable route `/analytics/supplier/report`.

## Gaps and next task

1. `PilotReadinessPage` currently verifies both pilot intake and supplier durable reports, but its visible CTA opens only the pilot intake report.
2. If operators need a direct stable path to every report checked by readiness, the next small implementation task should add a second supplier report CTA or split `Izvestaji spremni` into per-report checklist entries.
3. If action outcomes need to count as a durable report in readiness, add a dedicated report page/route first; linking the summary endpoint alone is not enough.
