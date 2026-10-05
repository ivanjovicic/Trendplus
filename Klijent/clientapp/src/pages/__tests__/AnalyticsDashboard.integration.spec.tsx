import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { rest } from "../../mocks/mswCompat";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { server } from "../../mocks/server";
import AnalyticsDashboard from "../AnalyticsDashboard";

// Mock the lazy-loaded AnalyticsDashboardCharts component
vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div>Charts Component</div>,
}));

describe("AnalyticsDashboard (integration)", () => {
  const bootstrapResponse = {
    summary: { totalRevenue: 12345, totalItems: 10, supplierCount: 5 },
    inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
    dailySales: [],
    categoryData: [],
    genderData: [],
    supplierData: [],
    supplierOptions: [],
    weekdayData: [],
    hourData: [],
    paymentData: [],
    quickInsights: null,
    transactionStats: null,
    advanced: null,
    topAdvanced: null,
    validationCompleteness: null,
    validationFreshness: null,
    validationLostSales: null,
    errors: [],
  };

  const healthResponse = {
    status: "ok",
    tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
    message: "ok",
  };

  beforeEach(() => {
    server.use(
      rest.get("/api/analytics/cached/dashboard/bootstrap", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(bootstrapResponse))
      ),
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) => 
        res(ctx.status(200), ctx.json([]))
      ),
      rest.get("/api/analytics/health", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(healthResponse))
      )
    );
  });

  it("renders dashboard with title", () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>
    );

    const title = screen.getByText("Pregled poslovanja");
    expect(title).toBeInTheDocument();
  });

  it("renders filter controls", () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>
    );

    expect(screen.getByTestId("analytics-control-bar")).toBeInTheDocument();
    expect(screen.getAllByText("Period").length).toBeGreaterThan(0);
  });

  it("shows overview dashboard section", () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>
    );

    expect(screen.getByTestId("analytics-control-bar")).toBeInTheDocument();
    expect(screen.getByText("Pregled poslovanja")).toBeInTheDocument();
  });

  it("shows detailed analysis section", () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>
    );

    const detailedHeader = screen.getByText("Detaljna analiza");
    expect(detailedHeader).toBeInTheDocument();
  });

  it("does not render a duplicate page-level refresh banner", async () => {
    server.use(
      rest.get("/api/analytics/cached/dashboard/bootstrap", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            ...bootstrapResponse,
            meta: { success: true, dataQualityStatus: "good" },
            summary: { ...bootstrapResponse.summary, totalTransactions: 5, totalUnits: 10 },
          })
        )
      )
    );
    const { container } = render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>
    );

    await waitFor(() => expect(container.querySelector(".analytics-executive-kpis")).toBeInTheDocument());
    const kpis = container.querySelector(".analytics-executive-kpis");
    const refreshBanner = container.querySelector(".analytics-refresh-banner");
    expect(kpis).not.toBeNull();
    expect(refreshBanner).toBeNull();
  });
});
