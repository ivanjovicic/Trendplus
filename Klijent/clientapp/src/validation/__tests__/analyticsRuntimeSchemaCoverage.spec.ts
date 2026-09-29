import { describe, expect, it } from "vitest";
import {
  dashboardAdvancedResponseSchema,
  supplierDecisionHubResponseSchema,
} from "../analyticsResponseSchemas";
import {
  TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE,
  TIER1_ANALYTICS_RUNTIME_SCHEMA_ROUTES,
} from "../analyticsRuntimeSchemaCoverage";

const expectedTier1Routes = [
  "/api/analytics/cached/dashboard/advanced",
  "/api/analytics/cached/dashboard/bootstrap",
  "/api/analytics/cached/products/decision-center",
  "/api/analytics/decision-board",
  "/api/analytics/data-quality/list",
  "/api/analytics/data-quality/health",
  "/api/analytics/data-quality/top-offenders",
  "/api/analytics/data-quality/trend",
  "/api/analytics/data-quality/intake-report",
  "/api/analytics/reports/supplier-decision",
  "/api/analytics/reports/pilot-intake",
  "/api/analytics/cached/inventory/balance",
  "/api/analytics/cached/inventory/list",
  "/api/analytics/cached/inventory/insights",
  "/api/analytics/inventory/{id}/detail",
  "/api/analytics/cached/inventory/forecast",
  "/api/analytics/cached/inventory/size-curve",
  "/api/analytics/cached/inventory/rebalance-suggestions",
  "/api/analytics/cached/inventory/alerts",
  "/api/analytics/cached/inventory/store-comparison",
  "/api/analytics/inventory/action-suggestions",
  "/api/analytics/actions",
  "/api/analytics/actions/counts",
  "/api/analytics/actions/outcomes/summary",
  "/api/analytics/suppliers/decision-hub/summary",
  "/api/analytics/suppliers/decision-hub/quadrant",
  "/api/analytics/suppliers/decision-hub/ranking",
  "/api/analytics/suppliers/decision-hub/{supplierId}/details",
  "/api/analytics/decision-pulse",
  "/api/analytics/supplier-sales-stats",
  "/api/analytics/shoe-type-sales-stats",
  "/api/analytics/color-sales-stats",
  "/api/analytics/daily-sales",
  "/api/analytics/pre-nivelacija-prioriteti",
  "/api/analytics/actions/{id}",
  "/api/analytics/actions/status",
  "/api/analytics/inventory/export",
  "/api/analytics/inventory/print-preview",
  "/api/analytics/inventory/report-schedules",
] as const;

const validMeta = {
  success: true,
  context: {
    contractVersion: "analytics.v1",
    state: "available",
    fingerprint: `sha256:${"a".repeat(64)}`,
  },
};

describe("Tier-1 analytics runtime schema coverage", () => {
  it("keeps the manifest unique and complete, with explicit reviewed exceptions", () => {
    const routes = TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE.map((entry) => entry.route);
    const validated = TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE.filter((entry) => entry.kind === "validated");
    const exceptions = TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE.filter((entry) => entry.kind === "reviewed_exception");

    expect(new Set(routes).size).toBe(routes.length);
    expect(new Set(routes)).toEqual(new Set(expectedTier1Routes));
    expect(TIER1_ANALYTICS_RUNTIME_SCHEMA_ROUTES).toEqual(new Set(expectedTier1Routes));
    expect(validated.length).toBeGreaterThan(30);
    expect(validated.every((entry) => entry.schema)).toBe(true);
    expect(exceptions.length).toBeGreaterThan(0);
    expect(exceptions.every((entry) => !entry.schema && Boolean(entry.reason?.trim()))).toBe(true);
  });

  it("fails closed when a newly covered response omits shared meta/context evidence", () => {
    const validDashboard = {
      generatedAtUtc: "2026-09-29T12:00:00Z",
      cards: [{ key: "revenue", value: 100, status: "good" }],
      insights: [],
      actions: [],
      validations: [],
      meta: validMeta,
    };

    expect(dashboardAdvancedResponseSchema.safeParse(validDashboard).success).toBe(true);
    expect(dashboardAdvancedResponseSchema.safeParse({ ...validDashboard, meta: undefined }).success).toBe(false);
    expect(dashboardAdvancedResponseSchema.safeParse({
      ...validDashboard,
      meta: { ...validMeta, context: { ...validMeta.context, fingerprint: "not-a-fingerprint" } },
    }).success).toBe(false);
    expect(supplierDecisionHubResponseSchema.safeParse({ meta: validMeta, items: [], totalCount: 0, page: 1, pageSize: 20 }).success).toBe(true);
    expect(supplierDecisionHubResponseSchema.safeParse({ items: [] }).success).toBe(false);
  });
});
