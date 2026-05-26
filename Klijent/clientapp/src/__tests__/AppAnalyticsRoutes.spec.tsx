import React from "react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "../App";

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

type RouteCase = {
  path: string;
  testId: string;
};

const routeCases: RouteCase[] = [
  { path: "/analytics", testId: "route-analytics" },
  { path: "/analytics/products", testId: "route-analytics-products" },
  { path: "/analytics/supplier", testId: "route-analytics-supplier" },
  { path: "/analytics/inventory", testId: "route-analytics-inventory" },
  { path: "/analytics/data-quality", testId: "route-analytics-data-quality" },
  { path: "/analytics/actions", testId: "route-analytics-actions" },
  { path: "/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30", testId: "route-analytics-supplier-report" },
  { path: "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30", testId: "route-analytics-pilot-intake-report" },
];

afterEach(() => {
  cleanup();
});

describe("App analytics core route smoke", () => {
  it.each(routeCases)("renders mapped route for $path", async ({ path, testId }) => {
    window.history.pushState({}, "", path);

    expect(() => render(<App />)).not.toThrow();
    expect(await screen.findByTestId(testId)).toBeInTheDocument();
  });
});
