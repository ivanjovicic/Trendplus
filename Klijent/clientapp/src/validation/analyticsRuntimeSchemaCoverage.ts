import type { ZodType } from "zod";
import {
  analyticsActionCountsResponseSchema,
  analyticsActionListResponseSchema,
  analyticsActionOutcomeSummaryResponseSchema,
  colorSalesStatsResponseSchema,
  dailySalesTableResponseSchema,
  dashboardAdvancedResponseSchema,
  dashboardBootstrapResponseSchema,
  dataQualityHealthResponseSchema,
  dataQualityIssueListResponseSchema,
  dataQualityTopOffendersResponseSchema,
  dataQualityTrendResponseSchema,
  decisionBoardAggregateResponseSchema,
  decisionPulseResponseSchema,
  durableAnalyticsReportResponseSchema,
  inventoryActionWorkflowResponseSchema,
  inventoryBalanceResponseSchema,
  inventoryDetailResponseSchema,
  inventoryInsightsResponseSchema,
  inventoryPagedResponseSchema,
  inventorySignalResponseSchema,
  inventoryStoreComparisonResponseSchema,
  pilotDataQualityIntakeResponseSchema,
  preNivelacijaPriorityResponseSchema,
  productDecisionCenterResponseSchema,
  shoeTypeSalesStatsResponseSchema,
  supplierDecisionHubResponseSchema,
  supplierSalesStatsResponseSchema,
} from "./analyticsResponseSchemas";

export type AnalyticsRuntimeSchemaCoverageKind = "validated" | "reviewed_exception";

export interface AnalyticsRuntimeSchemaCoverageEntry {
  surface: string;
  route: string;
  kind: AnalyticsRuntimeSchemaCoverageKind;
  schema?: ZodType<unknown>;
  reason?: string;
}

/**
 * Explicit boundary coverage for Tier-1 analytics clients. A reviewed
 * exception is allowed only for a transport/workflow endpoint that does not
 * expose an analytics metric envelope; it is not a silent schema bypass.
 */
export const TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE: readonly AnalyticsRuntimeSchemaCoverageEntry[] = [
  { surface: "dashboard", route: "/api/analytics/cached/dashboard/advanced", kind: "validated", schema: dashboardAdvancedResponseSchema },
  { surface: "dashboard", route: "/api/analytics/cached/dashboard/bootstrap", kind: "validated", schema: dashboardBootstrapResponseSchema },
  { surface: "product-decision-center", route: "/api/analytics/cached/products/decision-center", kind: "validated", schema: productDecisionCenterResponseSchema },
  { surface: "decision-board", route: "/api/analytics/decision-board", kind: "validated", schema: decisionBoardAggregateResponseSchema },
  { surface: "data-quality", route: "/api/analytics/data-quality/list", kind: "validated", schema: dataQualityIssueListResponseSchema },
  { surface: "data-quality", route: "/api/analytics/data-quality/health", kind: "validated", schema: dataQualityHealthResponseSchema },
  { surface: "data-quality", route: "/api/analytics/data-quality/top-offenders", kind: "validated", schema: dataQualityTopOffendersResponseSchema },
  { surface: "data-quality", route: "/api/analytics/data-quality/trend", kind: "validated", schema: dataQualityTrendResponseSchema },
  { surface: "data-quality", route: "/api/analytics/data-quality/intake-report", kind: "validated", schema: pilotDataQualityIntakeResponseSchema },
  { surface: "reports", route: "/api/analytics/reports/supplier-decision", kind: "validated", schema: durableAnalyticsReportResponseSchema },
  { surface: "reports", route: "/api/analytics/reports/pilot-intake", kind: "validated", schema: durableAnalyticsReportResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/balance", kind: "validated", schema: inventoryBalanceResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/list", kind: "validated", schema: inventoryPagedResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/insights", kind: "validated", schema: inventoryInsightsResponseSchema },
  { surface: "inventory", route: "/api/analytics/inventory/{id}/detail", kind: "validated", schema: inventoryDetailResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/forecast", kind: "validated", schema: inventorySignalResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/size-curve", kind: "validated", schema: inventorySignalResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/rebalance-suggestions", kind: "validated", schema: inventorySignalResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/alerts", kind: "validated", schema: inventorySignalResponseSchema },
  { surface: "inventory", route: "/api/analytics/cached/inventory/store-comparison", kind: "validated", schema: inventoryStoreComparisonResponseSchema },
  { surface: "inventory", route: "/api/analytics/inventory/action-suggestions", kind: "validated", schema: inventoryActionWorkflowResponseSchema },
  { surface: "analytics-actions", route: "/api/analytics/actions", kind: "validated", schema: analyticsActionListResponseSchema },
  { surface: "analytics-actions", route: "/api/analytics/actions/counts", kind: "validated", schema: analyticsActionCountsResponseSchema },
  { surface: "analytics-actions", route: "/api/analytics/actions/outcomes/summary", kind: "validated", schema: analyticsActionOutcomeSummaryResponseSchema },
  { surface: "supplier", route: "/api/analytics/suppliers/decision-hub/summary", kind: "validated", schema: supplierDecisionHubResponseSchema },
  { surface: "supplier", route: "/api/analytics/suppliers/decision-hub/quadrant", kind: "validated", schema: supplierDecisionHubResponseSchema },
  { surface: "supplier", route: "/api/analytics/suppliers/decision-hub/ranking", kind: "validated", schema: supplierDecisionHubResponseSchema },
  { surface: "supplier", route: "/api/analytics/suppliers/decision-hub/{supplierId}/details", kind: "validated", schema: supplierDecisionHubResponseSchema },
  { surface: "decision-pulse", route: "/api/analytics/decision-pulse", kind: "validated", schema: decisionPulseResponseSchema },
  { surface: "supplier", route: "/api/analytics/supplier-sales-stats", kind: "validated", schema: supplierSalesStatsResponseSchema },
  { surface: "shoe-type", route: "/api/analytics/shoe-type-sales-stats", kind: "validated", schema: shoeTypeSalesStatsResponseSchema },
  { surface: "color", route: "/api/analytics/color-sales-stats", kind: "validated", schema: colorSalesStatsResponseSchema },
  { surface: "daily", route: "/api/analytics/daily-sales", kind: "validated", schema: dailySalesTableResponseSchema },
  { surface: "pre-post", route: "/api/analytics/pre-nivelacija-prioriteti", kind: "validated", schema: preNivelacijaPriorityResponseSchema },
  { surface: "analytics-actions", route: "/api/analytics/actions/{id}", kind: "reviewed_exception", reason: "Ledger detail currently returns the action resource without a shared metric meta envelope." },
  { surface: "analytics-actions", route: "/api/analytics/actions/status", kind: "reviewed_exception", reason: "Bulk action status mutation, not a metric read surface." },
  { surface: "inventory", route: "/api/analytics/inventory/export", kind: "reviewed_exception", reason: "Document transport response is validated by the export/document contract." },
  { surface: "inventory", route: "/api/analytics/inventory/print-preview", kind: "reviewed_exception", reason: "Document transport response is validated by the export/document contract." },
  { surface: "inventory", route: "/api/analytics/inventory/report-schedules", kind: "reviewed_exception", reason: "Workflow configuration endpoint, not a Tier-1 metric response." },
];

export const TIER1_ANALYTICS_RUNTIME_SCHEMA_ROUTES = new Set(
  TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE.map((entry) => entry.route),
);
