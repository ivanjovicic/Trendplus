# KPI Methodology Audit

Date: 2026-05-25  
Scope: Core analytics screens and durable reports

## Coverage Table

| Screen | KPI label | metricKey | Explain button | Methodology panel | Notes |
|---|---|---|---|---|---|
| `/analytics` | Prihod / Maržni doprinos / Prodate jedinice / Lager u riziku / Spremnost podataka | `revenue`, `marginContribution`, `unitsSold`, `stockAtRisk`, `dataReadinessScore` | Yes | Yes | Executive KPI strip uses `ExecutiveKpiRow`; panel uses `EXECUTIVE_OVERVIEW_METRIC_KEYS`. |
| `/analytics/products` | Za dopunu, Za pojačanje, Za sniženje, Ne naručivati, Proveriti podatke, Procena izgubljene prodaje, Kapital u sporoj zalihi | `replenishCount`, `boostCount`, `markdownCount`, `doNotOrderCount`, `fixDataCount`, `lostSalesEstimate`, `slowStockCapital` | Yes | Partial | Explain buttons on KPI cards and row-detail metrics; no dedicated page-level methodology panel yet. |
| `/analytics/supplier` (overview tab) | Ukupan promet, Ukupno prodato, Ukupna nabavna vrednost, Maržni doprinos, Prosečna marža, Udeo top 5 dobavljača, Ukupan PoP trend | `revenue`, `unitsSold`, `totalCost`, `marginContribution`, `grossMarginPct`, `topSupplierRevenueShare`, `popRevenueChangePct` | Yes | No | KPI coverage completed in `SupplierSalesStatsPage`; page relies on in-card explanations. |
| `/analytics/supplier` (scorecard tab) | Ukupan prihod, Udeo top 5 dobavljača, Ukupan maržni doprinos, Kapital u riziku, Promena udela pune cene | `revenue`, `topSupplierRevenueShare`, `marginContribution`, `stockAtRisk`, `fullPriceShareChange` | Yes | No | Scorecard uses signal/recommendation semantics from backend; no formula logic in UI. |
| `/analytics/inventory` | Ukupno SKU, Ukupno na stanju, Niska zaliha, Prosečno po SKU, Procena vrednosti + key inventory signals strip | `skuCount`, `stockUnits`, `lowStockCount`, `avgUnitsPerSku`, `totalInventoryValue`, `stockAtRisk`, `slowStockCapital`, `outOfStockRisk`, `lostSalesEstimate`, `sellThrough` | Yes | No | KPI cards and signal strip have explain buttons. |
| `/analytics/data-quality` | Data readiness score, Artikli bez dobavljača, Artikli bez nabavne cene, Promet bez nabavne cene, Promet nepoznatog dobavljača | `dataReadinessScore`, `missingSupplierCount`, `missingCostCount`, `missingCostRevenueShare`, `unknownSupplierRevenueShare` | Yes | Partial | Issue view has explain buttons; pilot intake view has methodology panel. |
| `SupplierDecisionReport` | Dynamic KPI rows from payload | `findAnalyticsMetricKeyByLabel(...)` fallback to row key/label | Yes | Yes | Unknown keys open graceful fallback methodology text. |
| `PilotDataQualityIntakeReport` | Spremnost, Bez dobavljača, Bez nabavne cene, Prihod bez nabavne cene, Blokirane preporuke (+ durable KPIs) | `dataReadinessScore`, `missingSupplierCount`, `missingCostCount`, `revenueWithoutCost`, `blockedRecommendationsCount` (+ durable mapped key) | Yes | Yes | Uses report/durable payload where available plus fallback key set. |

## Canonical Methodology Keys

Primary canonical keys used across core screens:
- `revenue`
- `marginContribution`
- `unitsSold`
- `stockAtRisk`
- `slowStockCapital`
- `lostSalesEstimate`
- `dataReadinessScore`
- `missingCostCount`
- `missingSupplierCount`
- `sellThrough`
- `velocity`
- `confidencePct`
- `reliabilityPct`
- `markdownDependency`
- `outOfStockRisk`

Additional rollout keys added for coverage:
- `revenueWithoutCost`
- `unknownSupplierRevenueShare`
- `stockUnits`
- `lowStockCount`
- `blockedRecommendationsCount`
- `ignoredRowsCount`
- `grossMarginPct`
- `inventoryTurnover`
- `totalCost`
- `popRevenueChangePct`

## Alias Map (Compatibility)

Required aliases retained:
- `totalRevenue -> revenue`
- `soldUnits -> unitsSold`
- `lostSales -> lostSalesEstimate`
- `dataReadiness -> dataReadinessScore`
- `stockRiskCapital -> stockAtRisk`

Additional compatibility aliases:
- `onHandUnits -> stockUnits`
- `missingCostRevenueShare -> revenueWithoutCost`
- `revenueUnknownSupplier -> unknownSupplierRevenueShare`
- `totalInventoryValue -> inventoryTotalValue`

## Roadmap / TODO

- `gmroi`: roadmap only (backend metric and DTO contract needed before frontend can document it as active KPI).
- `inventoryTurnover`: currently documented as indicative; requires stable backend data contract for strong decision usage.
