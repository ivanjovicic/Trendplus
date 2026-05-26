import React from "react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "../App";
import { CORE_ANALYTICS_SMOKE_ROUTES } from "../routes/analyticsRouteDefinitions";

vi.mock("../layout/AppLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="app-layout">{children}</div>,
}));

vi.mock("../components/CircuitBreakerStatus", () => ({
  CircuitBreakerStatus: () => null,
}));

vi.mock("../pages/AnalyticsDashboard", () => ({
  default: () => <div data-testid="route-analytics">analytics</div>,
}));

vi.mock("../pages/ProductDecisionCenterPage", () => ({
  default: () => <div data-testid="route-analytics-products">products</div>,
}));

vi.mock("../pages/SupplierConsolidatedPage", () => ({
  default: () => <div data-testid="route-analytics-supplier">supplier</div>,
}));

vi.mock("../pages/InventoryPage", () => ({
  default: () => <div data-testid="route-analytics-inventory">inventory</div>,
}));

vi.mock("../pages/DataQualityPage", () => ({
  default: () => <div data-testid="route-analytics-data-quality">data-quality</div>,
}));

vi.mock("../pages/AnalyticsActionsPage", () => ({
  default: () => <div data-testid="route-analytics-actions">actions</div>,
}));

vi.mock("../pages/SupplierDecisionReportPage", () => ({
  default: () => <div data-testid="route-analytics-supplier-report">supplier-report</div>,
}));

vi.mock("../pages/PilotIntakeReportPage", () => ({
  default: () => <div data-testid="route-analytics-pilot-intake-report">pilot-intake</div>,
}));

const testIdByPath: Record<string, string> = {
  "/analytics": "route-analytics",
  "/analytics/products": "route-analytics-products",
  "/analytics/supplier": "route-analytics-supplier",
  "/analytics/inventory": "route-analytics-inventory",
  "/analytics/data-quality": "route-analytics-data-quality",
  "/analytics/actions": "route-analytics-actions",
  "/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all": "route-analytics-supplier-report",
  "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all": "route-analytics-pilot-intake-report",
};

const routeCases = CORE_ANALYTICS_SMOKE_ROUTES.map((path) => ({
  path,
  testId: testIdByPath[path],
}));

afterEach(() => {
  cleanup();
});

describe("App analytics core route smoke", () => {
  it("has test ids for all registered smoke routes", () => {
    routeCases.forEach(({ path, testId }) => {
      expect(testId, `Missing test id mapping for ${path}`).toBeDefined();
    });
  });

  it.each(routeCases)("renders mapped route for $path", async ({ path, testId }) => {
    window.history.pushState({}, "", path);

    expect(() => render(<App />)).not.toThrow();
    expect(await screen.findByTestId(testId)).toBeInTheDocument();
  });
});
